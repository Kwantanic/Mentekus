using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Dapper;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Features.User.Requests;
using Mentekus.Api.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class QuestionEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Ask_ReturnsOk_AndSavesToDatabase()
    {
        // 1. Add user
        var userRequest = new UserAddRequest("Test User", "test@example.com");
        await Client.PostAsJsonAsync("/user/add", userRequest);

        // 2. Ask question
        var questionText = "What is Native AOT?";
        var expectedEmbedding = Enumerable.Repeat(0.1f, 1024).ToArray();

        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        var request = new QuestionAskRequest(questionText, "test@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Question saved (ID:", content);
        Assert.Contains("Embedding length: 1024", content);

        // Verify the mock was called
        OllamaAdapterMock.Verify(a => a.EmbedAsync(questionText, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Similarity_ReturnsMostSimilarQuestions()
    {
        // Arrange
        var question1 = "What is .NET?";
        var embedding1 = Enumerable.Repeat(0.0f, 1024).ToArray(); embedding1[0] = 1.0f;
        var question2 = "What is Java?";
        var embedding2 = Enumerable.Repeat(0.0f, 1024).ToArray(); embedding2[1] = 1.0f;
        var searchQuery = "Tell me about .NET";
        var searchEmbedding = Enumerable.Repeat(0.0f, 1024).ToArray(); searchEmbedding[0] = 0.9f;

        // 0. Add users
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("User One", "one@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("User Two", "two@example.com"));

        // 1. Setup mock for inserting questions
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(question1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding1);
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(question2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding2);

        await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest(question1, "one@example.com"));
        await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest(question2, "two@example.com"));

        // 2. Setup mock for similarity search
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(searchQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchEmbedding);

        var similarityRequest = new QuestionSimilarityRequest(searchQuery, 2);

        // Act
        var response = await Client.PostAsJsonAsync("/question/similarity", similarityRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<QuestionSimilarityResponse>>(
            AppJsonSerializerContext.Default.ListQuestionSimilarityResponse);

        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.Equal(question1, results[0].Text); // Should be more similar to .NET question
        Assert.Equal("one@example.com", results[0].AskedByEmail);
        Assert.NotEqual(Guid.Empty, results[0].AskedByUserId);
        Assert.Equal("two@example.com", results[1].AskedByEmail);
        Assert.NotEqual(Guid.Empty, results[1].AskedByUserId);
        Assert.True(results[0].Similarity > results[1].Similarity);
    }

    [Fact]
    public async Task Ask_ReturnsOk_CaseInsensitiveEmail()
    {
        // 1. Add user with mixed case
        var userRequest = new UserAddRequest("Mixed User", "Mixed@Example.Com");
        await Client.PostAsJsonAsync("/user/add", userRequest);

        // 2. Ask question with different case
        var questionText = "Case sensitivity test";
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Repeat(0.5f, 1024).ToArray());

        var request = new QuestionAskRequest(questionText, "mixed@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ask_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new QuestionAskRequest("Some question", "nonexistent@example.com");

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Contains("not found", problem.Detail);
    }

    [Fact]
    public async Task Answer_Success_UpdatesVectorAndReturnsOk_BlendingSideEffect()
    {
        // 1. Add users and a question
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Q Owner", "qowner@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Answerer", "answerer@example.com"));

        OllamaAdapterMock
            .Setup(a => a.EmbedAsync("Initial Q", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 1024).Select(i => 0.01f).ToArray());
        var askResp = await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest("Initial Q", "qowner@example.com"));
        Assert.Equal(HttpStatusCode.OK, askResp.StatusCode);
        var askContent = await askResp.Content.ReadAsStringAsync();
        var qidStr = askContent.Split("ID: ")[1].Split(')')[0];
        var questionId = Guid.Parse(qidStr);

        // 2. Setup for answer embed (1024 dim), generate for topics in update
        var ansEmb = Enumerable.Range(0, 1024).Select(i => i == 0 ? 0.8f : 0.02f).ToArray();
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync("This is the answer text about AOT.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ansEmb);
        OllamaAdapterMock
            .Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"aot\", \"dotnet\"]");

        var ansReq = new QuestionAnswerRequest(questionId, "This is the answer text about AOT.", "answerer@example.com");
        var ansResponse = await Client.PostAsJsonAsync("/question/answer", ansReq);

        Assert.Equal(HttpStatusCode.OK, ansResponse.StatusCode);
        var ansStr = await ansResponse.Content.ReadAsStringAsync();
        Assert.Contains("Answer recorded", ansStr);
        Assert.Contains("Expertise updated", ansStr);

        // Verify blending side effect via profile (topics) and direct DB for 1024 dim vector
        using var scope = Services.CreateScope();
        var expertise = scope.ServiceProvider.GetRequiredService<IExpertiseService>();
        var profile = await expertise.GetUserExpertiseAsync((await scope.ServiceProvider.GetRequiredService<Mentekus.Api.Features.User.IUserService>().GetUserIdByEmailAsync("answerer@example.com"))!.Value);
        Assert.NotNull(profile);
        Assert.Contains("aot", profile.TopTopics);

        // DB check for embedding dim (1024 expected post blend)
#pragma warning disable DAP005
        var conn = scope.ServiceProvider.GetRequiredService<System.Data.IDbConnection>();
        var dim = await conn.ExecuteScalarAsync<int>("SELECT vector_dims(ExpertiseEmbedding) FROM Users WHERE LOWER(Email)=LOWER(@e)", new { e = "answerer@example.com" });
        Assert.Equal(1024, dim);
#pragma warning restore DAP005
    }

    [Fact]
    public async Task Answer_InvalidQuestionId_Returns404()
    {
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Ans", "ans@example.com"));
        var badReq = new QuestionAnswerRequest(Guid.NewGuid(), "ans text", "ans@example.com");
        var resp = await Client.PostAsJsonAsync("/question/answer", badReq);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Contains("not found", problem.Detail ?? "");
    }

    [Fact]
    public async Task Answer_GenerateFailure_StillUpdatesVector_NonFatal()
    {
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Q2", "q2@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Ans2", "ans2@example.com"));

        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 1024).Select(_ => 0.05f).ToArray());
        var askR = await Client.PostAsJsonAsync("/question/ask", new QuestionAskRequest("Q2", "q2@example.com"));
        var qid = Guid.Parse((await askR.Content.ReadAsStringAsync()).Split("ID: ")[1].Split(')')[0]);

        // Simulate generate (topics) fail, but embed for answer succeeds; vector must update
        OllamaAdapterMock
            .Setup(a => a.EmbedAsync("failing answer contrib", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 1024).Select(i => 0.77f).ToArray());
        OllamaAdapterMock
            .Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM down for generate"));

        var ansReq = new QuestionAnswerRequest(qid, "failing answer contrib", "ans2@example.com");
        var resp = await Client.PostAsJsonAsync("/question/answer", ansReq);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = Services.CreateScope();
#pragma warning disable DAP005
        var conn = scope.ServiceProvider.GetRequiredService<System.Data.IDbConnection>();
        var dim = await conn.ExecuteScalarAsync<int>("SELECT vector_dims(ExpertiseEmbedding) FROM Users WHERE LOWER(Email) = LOWER(@e)", new { e = "ans2@example.com" });
        Assert.Equal(1024, dim); // vector updated despite generate fail
#pragma warning restore DAP005
    }

    [Fact]
    public async Task ExpertiseDocument_Ingest_UpdatesExpertise_AndReturnsMessage()
    {
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Doc User", "docuser@example.com"));

        var docEmb = Enumerable.Range(0, 1024).Select(i => 0.03f + (i % 10) * 0.001f).ToArray();
        OllamaAdapterMock.Setup(a => a.EmbedAsync("CV text for senior dotnet pgvector role.", It.IsAny<CancellationToken>())).ReturnsAsync(docEmb);
        OllamaAdapterMock.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("[\"dotnet\", \"pgvector\", \"senior engineer\"]");

        var ingestReq = new ExpertiseDocumentIngestRequest("CV text for senior dotnet pgvector role.", "docuser@example.com");
        var resp = await Client.PostAsJsonAsync("/expertise/document", ingestReq, AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var msg = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Document ingested. Expertise updated.", msg);

        using var scope = Services.CreateScope();
#pragma warning disable DAP005
        var conn = scope.ServiceProvider.GetRequiredService<System.Data.IDbConnection>();
        var dim = await conn.ExecuteScalarAsync<int>("SELECT vector_dims(ExpertiseEmbedding) FROM Users WHERE LOWER(Email)=LOWER(@e)", new { e = "docuser@example.com" });
        Assert.Equal(1024, dim);
#pragma warning restore DAP005
    }

    [Fact]
    public async Task ExpertiseRoute_RanksWithScoreVecSimMatchedTopics_RespectsAllowRoutingFlag()
    {
        // users (4 routable + 1 filtered to test exclusion + overfetch for hybrid promotion)
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("R1 Expert", "r1@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("R2 Expert", "r2@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("R3 Expert", "r3@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("R4 Expert", "r4@example.com"));
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("NoRoute", "noroute@example.com"));

        // set one to not allow routing
        await Client.PostAsJsonAsync("/user/noroute@example.com/preferences", new UserPreferencesUpdateRequest(AllowRouting: false), AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);

        // seed distinct embeddings so pure-vec DB order (via <=> ) would be r2 (best), r1, r3, r4 (worst)
        // but query topics will overlap r4 strongly -> topic bonus promotes r4 in hybrid re-rank (verifies overfetch + re-rank fix)
        var emb1 = new float[1024]; emb1[10] = 0.95f; emb1[20] = 0.8f;
        var emb2 = new float[1024]; emb2[10] = 0.96f; emb2[30] = 0.7f;
        var emb3 = new float[1024]; emb3[10] = 0.80f; emb3[40] = 0.6f;
        var emb4 = new float[1024]; emb4[10] = 0.70f; emb4[50] = 0.5f;
        OllamaAdapterMock.Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string t, CancellationToken _) =>
            {
                if (t.Contains("r1")) return Task.FromResult<float[]?>(emb1);
                if (t.Contains("r2")) return Task.FromResult<float[]?>(emb2);
                if (t.Contains("r3")) return Task.FromResult<float[]?>(emb3);
                return Task.FromResult<float[]?>(emb4);
            });
        // generate returns topics that strongly match r4's seeded expertise (for promotion test)
        OllamaAdapterMock.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"r4-skill\", \"topicx\"]");

        // make docs to trigger blend + topics
        await Client.PostAsJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("r1 text dotnet pgvector", "r1@example.com"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);
        await Client.PostAsJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("r2 text dotnet", "r2@example.com"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);
        await Client.PostAsJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("r3 text embeddings", "r3@example.com"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);
        await Client.PostAsJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("r4 text r4-skill topicx", "r4@example.com"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        // route query (topics will boost r4 above pure-vec ordering)
        var routeReq = new ExpertiseRouteRequest("r4-skill expert topicx", 3);
        var routeResp = await Client.PostAsJsonAsync("/expertise/route", routeReq, AppJsonSerializerContext.Default.ExpertiseRouteRequest);
        Assert.Equal(HttpStatusCode.OK, routeResp.StatusCode);

        var matches = await routeResp.Content.ReadFromJsonAsync<List<ExpertiseRouteMatch>>(AppJsonSerializerContext.Default.ListExpertiseRouteMatch);
        Assert.NotNull(matches);
        Assert.Equal(3, matches.Count); // noroute excluded; 3 routable after limit
        Assert.All(matches, m => Assert.DoesNotContain("noroute", m.Email));
        // r4 promoted to top via hybrid (topic bonus overcomes lower pure VecSim)
        Assert.Equal("r4@example.com", matches[0].Email);
        Assert.True(matches[0].Score >= matches[1].Score);
        Assert.Contains(matches, m => m.MatchedTopics.Length > 0 || m.VecSim > 0); // has some scoring info
        Assert.All(matches, m => { Assert.True(m.VecSim >= 0 && m.VecSim <= 1.0001); Assert.True(m.Confidence >= 0); });
    }

    [Fact]
    public async Task UserExpertiseProfile_SelfSeesFull_EvenIfNotVisible()
    {
        await Client.PostAsJsonAsync("/user/add", new UserAddRequest("Hidden Self", "hidden@example.com"));
        // set not visible
        await Client.PostAsJsonAsync("/user/hidden@example.com/preferences", new UserPreferencesUpdateRequest(ProfileVisible: false), AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);

        // seed some expertise
        OllamaAdapterMock.Setup(a => a.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Enumerable.Repeat(0.1f, 1024).ToArray());
        await Client.PostAsJsonAsync("/expertise/document", new ExpertiseDocumentIngestRequest("hidden profile topics here", "hidden@example.com"), AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        var profileResp = await Client.GetAsync("/user/hidden@example.com/expertise");
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);
        var profile = await profileResp.Content.ReadFromJsonAsync<UserExpertiseProfile>(AppJsonSerializerContext.Default.UserExpertiseProfile);
        Assert.NotNull(profile);
        Assert.Equal("Hidden Self", profile.Name);
        Assert.Equal("hidden@example.com", profile.Email);
        // full fields visible even though !ProfileVisible (endpoint behavior for self/profile access)
        Assert.NotNull(profile.LastUpdated);
    }
}