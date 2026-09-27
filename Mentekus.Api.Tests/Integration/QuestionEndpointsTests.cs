using System.Net;
using System.Net.Http.Json;
using Dapper;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.Expertise.Requests;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class QuestionEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Ask_ReturnsOk_AndSavesToDatabase()
    {
        await Client.RegisterAndSignInAsync("Test User", "test@example.com");

        var questionText = "What is Native AOT?";
        var expectedEmbedding = Enumerable.Repeat(0.1f, 1024).ToArray();
        Ollama.EmbedAny(expectedEmbedding);

        var response = await Client.PostJsonAsync("/question/ask", new QuestionAskRequest(questionText), AppJsonSerializerContext.Default.QuestionAskRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Question saved (ID:", content);
        Assert.Contains("Embedding length: 1024", content);
        Assert.Equal([questionText], Ollama.EmbedCalls);
    }

    [Fact]
    public async Task Similarity_ReturnsMostSimilarQuestions()
    {
        var question1 = "What is .NET?";
        var embedding1 = Enumerable.Repeat(0.0f, 1024).ToArray(); embedding1[0] = 1.0f;
        var question2 = "What is Java?";
        var embedding2 = Enumerable.Repeat(0.0f, 1024).ToArray(); embedding2[1] = 1.0f;
        var searchQuery = "Tell me about .NET";
        var searchEmbedding = Enumerable.Repeat(0.0f, 1024).ToArray(); searchEmbedding[0] = 0.9f;

        await Client.RegisterAndSignInAsync("User One", "one@example.com");
        var other = CreateSession();
        await other.RegisterAndSignInAsync("User Two", "two@example.com");

        Ollama.Embed(question1, embedding1);
        Ollama.Embed(question2, embedding2);

        await Client.PostJsonAsync("/question/ask", new QuestionAskRequest(question1), AppJsonSerializerContext.Default.QuestionAskRequest);
        await other.PostJsonAsync("/question/ask", new QuestionAskRequest(question2), AppJsonSerializerContext.Default.QuestionAskRequest);

        Ollama.Embed(searchQuery, searchEmbedding);

        var response = await Client.PostJsonAsync("/question/similarity", new QuestionSimilarityRequest(searchQuery, 2), AppJsonSerializerContext.Default.QuestionSimilarityRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<QuestionSimilarity>>(
            AppJsonSerializerContext.Default.ListQuestionSimilarity);

        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.Equal(question1, results[0].Text);
        Assert.Equal("one@example.com", results[0].AskedByEmail);
        Assert.NotEqual(Guid.Empty, results[0].AskedByUserId);
        Assert.Equal("two@example.com", results[1].AskedByEmail);
        Assert.NotEqual(Guid.Empty, results[1].AskedByUserId);
        Assert.True(results[0].Similarity > results[1].Similarity);
    }

    [Fact]
    public async Task Ask_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await Client.PostJsonAsync("/question/ask", new QuestionAskRequest("Some question"), AppJsonSerializerContext.Default.QuestionAskRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Contains("Authentication is required.", problem.Detail);
    }

    [Fact]
    public async Task Answer_Success_UpdatesVectorAndReturnsOk_BlendingSideEffect()
    {
        await Client.RegisterAndSignInAsync("Q Owner", "qowner@example.com");
        var answerer = CreateSession();
        await answerer.RegisterAndSignInAsync("Answerer", "answerer@example.com");

        Ollama.Embed("Initial Q", Enumerable.Range(0, 1024).Select(i => 0.01f).ToArray());
        var askResp = await Client.PostJsonAsync("/question/ask", new QuestionAskRequest("Initial Q"), AppJsonSerializerContext.Default.QuestionAskRequest);
        Assert.Equal(HttpStatusCode.OK, askResp.StatusCode);
        var askContent = await askResp.Content.ReadAsStringAsync();
        var qidStr = askContent.Split("ID: ")[1].Split(')')[0];
        var questionId = Guid.Parse(qidStr);

        var ansEmb = Enumerable.Range(0, 1024).Select(i => i == 0 ? 0.8f : 0.02f).ToArray();
        Ollama.Embed("This is the answer text about AOT.", ansEmb);
        Ollama.GenerateAny("[\"aot\", \"dotnet\"]");

        var ansResponse = await answerer.PostJsonAsync(
            "/question/answer",
            new QuestionAnswerRequest(questionId, "This is the answer text about AOT."),
            AppJsonSerializerContext.Default.QuestionAnswerRequest);

        Assert.Equal(HttpStatusCode.OK, ansResponse.StatusCode);
        var ansStr = await ansResponse.Content.ReadAsStringAsync();
        Assert.Contains("Answer recorded", ansStr);
        Assert.Contains("Expertise updated", ansStr);

        using var scope = Services.CreateScope();
        var expertise = scope.ServiceProvider.GetRequiredService<IExpertiseService>();
        var profile = await expertise.GetUserExpertiseAsync((await scope.ServiceProvider.GetRequiredService<IUserService>().GetUserIdByEmailAsync("answerer@example.com"))!.Value);
        Assert.NotNull(profile);
        Assert.Contains("aot", profile.TopTopics);

        var conn = scope.ServiceProvider.GetRequiredService<System.Data.IDbConnection>();
        var dim = await conn.ExecuteScalarAsync<int>("SELECT vector_dims(ExpertiseEmbedding) FROM Users WHERE LOWER(Email)=LOWER(@e)", new { e = "answerer@example.com" });
        Assert.Equal(1024, dim);
    }

    [Fact]
    public async Task Answer_InvalidQuestionId_Returns404()
    {
        await Client.RegisterAndSignInAsync("Ans", "ans@example.com");
        var resp = await Client.PostJsonAsync(
            "/question/answer",
            new QuestionAnswerRequest(Guid.NewGuid(), "ans text"),
            AppJsonSerializerContext.Default.QuestionAnswerRequest);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemDetails>(AppJsonSerializerContext.Default.ProblemDetails);
        Assert.NotNull(problem);
        Assert.Contains("not found", problem.Detail ?? "");
    }

    [Fact]
    public async Task Answer_GenerateFailure_StillUpdatesVector_NonFatal()
    {
        await Client.RegisterAndSignInAsync("Q2", "q2@example.com");
        var answerer = CreateSession();
        await answerer.RegisterAndSignInAsync("Ans2", "ans2@example.com");

        Ollama.EmbedAny(Enumerable.Range(0, 1024).Select(_ => 0.05f).ToArray());
        var askR = await Client.PostJsonAsync("/question/ask", new QuestionAskRequest("Q2"), AppJsonSerializerContext.Default.QuestionAskRequest);
        var qid = Guid.Parse((await askR.Content.ReadAsStringAsync()).Split("ID: ")[1].Split(')')[0]);

        Ollama.Embed("failing answer contrib", Enumerable.Range(0, 1024).Select(i => 0.77f).ToArray());
        Ollama.GenerateThrows(_ => true, new Exception("LLM down for generate"));

        var resp = await answerer.PostJsonAsync(
            "/question/answer",
            new QuestionAnswerRequest(qid, "failing answer contrib"),
            AppJsonSerializerContext.Default.QuestionAnswerRequest);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = Services.CreateScope();
        var conn = scope.ServiceProvider.GetRequiredService<System.Data.IDbConnection>();
        var dim = await conn.ExecuteScalarAsync<int>("SELECT vector_dims(ExpertiseEmbedding) FROM Users WHERE LOWER(Email) = LOWER(@e)", new { e = "ans2@example.com" });
        Assert.Equal(1024, dim);
    }

    [Fact]
    public async Task ExpertiseDocument_Ingest_UpdatesExpertise_AndReturnsMessage()
    {
        await Client.RegisterAndSignInAsync("Doc User", "docuser@example.com");

        var docEmb = Enumerable.Range(0, 1024).Select(i => 0.03f + (i % 10) * 0.001f).ToArray();
        Ollama.Embed("CV text for senior dotnet pgvector role.", docEmb);
        Ollama.GenerateAny("[\"dotnet\", \"pgvector\", \"senior engineer\"]");

        var resp = await Client.PostJsonAsync(
            "/expertise/document",
            new ExpertiseDocumentIngestRequest("CV text for senior dotnet pgvector role."),
            AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var msg = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Document ingested. Expertise updated.", msg);

        using var scope = Services.CreateScope();
        var conn = scope.ServiceProvider.GetRequiredService<System.Data.IDbConnection>();
        var dim = await conn.ExecuteScalarAsync<int>("SELECT vector_dims(ExpertiseEmbedding) FROM Users WHERE LOWER(Email)=LOWER(@e)", new { e = "docuser@example.com" });
        Assert.Equal(1024, dim);
    }

    [Fact]
    public async Task ExpertiseRoute_RanksWithScoreVecSimMatchedTopics_RespectsAllowRoutingFlag()
    {
        var emb1 = new float[1024]; emb1[10] = 0.95f; emb1[20] = 0.8f;
        var emb2 = new float[1024]; emb2[10] = 0.96f; emb2[30] = 0.7f;
        var emb3 = new float[1024]; emb3[10] = 0.80f; emb3[40] = 0.6f;
        var emb4 = new float[1024]; emb4[10] = 0.70f; emb4[50] = 0.5f;
        Ollama.Embed(_ => true, t =>
        {
            if (t.Contains("r1")) return emb1;
            if (t.Contains("r2")) return emb2;
            if (t.Contains("r3")) return emb3;
            return emb4;
        });
        Ollama.GenerateAny("[\"r4-skill\", \"topicx\"]");

        var r1 = await SeedAsync("R1 Expert", "r1@example.com", "r1 text dotnet pgvector");
        await SeedAsync("R2 Expert", "r2@example.com", "r2 text dotnet");
        await SeedAsync("R3 Expert", "r3@example.com", "r3 text embeddings");
        await SeedAsync("R4 Expert", "r4@example.com", "r4 text r4-skill topicx");

        var blocked = CreateSession();
        await blocked.RegisterAndSignInAsync("NoRoute", "noroute@example.com");
        var pref = await blocked.PostJsonAsync(
            "/user/noroute@example.com/preferences",
            new UserPreferencesUpdateRequest(AllowRouting: false),
            AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);
        Assert.Equal(HttpStatusCode.OK, pref.StatusCode);

        var routeResp = await r1.PostJsonAsync("/expertise/route", new ExpertiseRouteRequest("r4-skill expert topicx", 3), AppJsonSerializerContext.Default.ExpertiseRouteRequest);
        Assert.Equal(HttpStatusCode.OK, routeResp.StatusCode);

        var matches = await routeResp.Content.ReadFromJsonAsync<List<ExpertiseRouteMatch>>(AppJsonSerializerContext.Default.ListExpertiseRouteMatch);
        Assert.NotNull(matches);
        Assert.Equal(3, matches.Count);
        Assert.All(matches, m => Assert.DoesNotContain("noroute", m.Email));
        Assert.Equal("r4@example.com", matches[0].Email);
        Assert.True(matches[0].Score >= matches[1].Score);
        Assert.Contains(matches, m => m.MatchedTopics.Length > 0 || m.VecSim > 0);
        Assert.All(matches, m => { Assert.True(m.VecSim >= 0 && m.VecSim <= 1.0001); Assert.True(m.Confidence >= 0); });
    }

    [Fact]
    public async Task UserExpertiseProfile_SelfSeesFull_HiddenFromOthers()
    {
        await Client.RegisterAndSignInAsync("Hidden Self", "hidden@example.com");
        await Client.PostJsonAsync(
            "/user/hidden@example.com/preferences",
            new UserPreferencesUpdateRequest(ProfileVisible: false),
            AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);

        Ollama.EmbedAny(Enumerable.Repeat(0.1f, 1024).ToArray());
        await Client.PostJsonAsync(
            "/expertise/document",
            new ExpertiseDocumentIngestRequest("hidden profile topics here"),
            AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);

        var profileResp = await Client.GetAsync("/user/hidden@example.com/expertise");
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);
        var profile = await profileResp.Content.ReadFromJsonAsync<UserExpertiseProfile>(AppJsonSerializerContext.Default.UserExpertiseProfile);
        Assert.NotNull(profile);
        Assert.Equal("Hidden Self", profile.Name);
        Assert.Equal("hidden@example.com", profile.Email);
        Assert.NotNull(profile.LastUpdated);

        var other = CreateSession();
        await other.RegisterAndSignInAsync("Other Person", "other-hidden@example.com");
        var hidden = await other.GetAsync("/user/hidden@example.com/expertise");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var shown = await Client.PostJsonAsync(
            "/user/hidden@example.com/preferences",
            new UserPreferencesUpdateRequest(ProfileVisible: true),
            AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);
        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);

        var visible = await other.GetAsync("/user/hidden@example.com/expertise");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
    }

    private async Task<SessionClient> SeedAsync(string name, string email, string document)
    {
        var session = CreateSession();
        await session.RegisterAndSignInAsync(name, email);
        var ingest = await session.PostJsonAsync(
            "/expertise/document",
            new ExpertiseDocumentIngestRequest(document),
            AppJsonSerializerContext.Default.ExpertiseDocumentIngestRequest);
        Assert.Equal(HttpStatusCode.OK, ingest.StatusCode);
        return session;
    }
}
