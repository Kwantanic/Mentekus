using System.Net;
using System.Net.Http.Json;
using Dapper;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.Expertise.Requests;
using Mentekus.Api.Features.Question;
using Mentekus.Api.Features.Question.Entities;
using Mentekus.Api.Features.Question.Requests;
using Mentekus.Api.Features.User;
using Mentekus.Api.Features.User.Requests;
using Npgsql;
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
        var created = await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.QuestionCreated);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal([questionText], Ollama.EmbedCalls);

        var detailResponse = await Client.GetAsync($"/question/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.QuestionDetail);
        Assert.NotNull(detail);
        Assert.Equal(questionText, detail.Text);
        Assert.Equal("test@example.com", detail.AskedByEmail);
        Assert.Empty(detail.Answers);
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
        var created = await askResp.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.QuestionCreated);
        Assert.NotNull(created);
        var questionId = created.Id;

        var ansEmb = Enumerable.Range(0, 1024).Select(i => i == 0 ? 0.8f : 0.02f).ToArray();
        Ollama.Embed("This is the answer text about AOT.", ansEmb);
        Ollama.GenerateAny("[\"aot\", \"dotnet\"]");

        var ansResponse = await answerer.PostJsonAsync(
            "/question/answer",
            new QuestionAnswerRequest(questionId, "This is the answer text about AOT."),
            AppJsonSerializerContext.Default.QuestionAnswerRequest);

        Assert.Equal(HttpStatusCode.OK, ansResponse.StatusCode);
        var answerCreated = await ansResponse.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.AnswerCreated);
        Assert.NotNull(answerCreated);
        Assert.Equal(questionId, answerCreated.QuestionId);

        var detailResponse = await Client.GetAsync($"/question/{questionId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.QuestionDetail);
        Assert.NotNull(detail);
        Assert.Equal("Initial Q", detail.Text);
        var stored = Assert.Single(detail.Answers);
        Assert.Equal(answerCreated.Id, stored.Id);
        Assert.Equal("This is the answer text about AOT.", stored.Text);
        Assert.Equal("answerer@example.com", stored.AnsweredByEmail);

        using var scope = Services.CreateScope();
        var expertise = scope.ServiceProvider.GetRequiredService<IExpertiseService>();
        var profile = await expertise.GetUserExpertiseAsync((await scope.ServiceProvider.GetRequiredService<IUserService>().GetUserIdByEmailAsync("answerer@example.com"))!.Value);
        Assert.NotNull(profile);
        Assert.Contains("aot", profile.TopTopics);

        await using var conn = await scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
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
        var created = await askR.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.QuestionCreated);
        Assert.NotNull(created);
        var qid = created.Id;

        Ollama.Embed("failing answer contrib", Enumerable.Range(0, 1024).Select(i => 0.77f).ToArray());
        Ollama.GenerateThrows(_ => true, new Exception("LLM down for generate"));

        var resp = await answerer.PostJsonAsync(
            "/question/answer",
            new QuestionAnswerRequest(qid, "failing answer contrib"),
            AppJsonSerializerContext.Default.QuestionAnswerRequest);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = Services.CreateScope();
        await using var conn = await scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        var dim = await conn.ExecuteScalarAsync<int>("SELECT vector_dims(ExpertiseEmbedding) FROM Users WHERE LOWER(Email) = LOWER(@e)", new { e = "ans2@example.com" });
        Assert.Equal(1024, dim);
    }

    [Fact]
    public async Task ExpertiseDocument_Ingest_UpdatesExpertise_AndReturnsProfile()
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
        var profile = await resp.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.UserExpertiseProfile);
        Assert.NotNull(profile);
        Assert.Equal("Doc User", profile.Name);
        Assert.Equal("docuser@example.com", profile.Email);
        Assert.NotNull(profile.LastUpdated);

        using var scope = Services.CreateScope();
        await using var conn = await scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
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

    [Fact]
    public async Task Ask_WrongEmbeddingDimension_ReturnsBadRequest_AndStoresNothing()
    {
        await Client.RegisterAndSignInAsync("Dim User", "dim@example.com");
        Ollama.EmbedAny(new float[512]);

        var response = await Client.PostJsonAsync(
            "/question/ask",
            new QuestionAskRequest("bad dim"),
            AppJsonSerializerContext.Default.QuestionAskRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var connection = await Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Questions");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SimilarityAndGet_HideAsker_WhenProfileIsHidden()
    {
        await Client.RegisterAndSignInAsync("Hidden Asker", "hidden-asker@example.com");
        await Client.PostJsonAsync(
            "/user/hidden-asker@example.com/preferences",
            new UserPreferencesUpdateRequest(ProfileVisible: false),
            AppJsonSerializerContext.Default.UserPreferencesUpdateRequest);

        var embedding = Enumerable.Repeat(0.4f, 1024).ToArray();
        const string question = "visible only to me";
        Ollama.Embed(question, embedding);
        var ask = await Client.PostJsonAsync(
            "/question/ask",
            new QuestionAskRequest(question),
            AppJsonSerializerContext.Default.QuestionAskRequest);
        var created = await ask.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.QuestionCreated);
        Assert.NotNull(created);

        var other = CreateSession();
        await other.RegisterAndSignInAsync("Searcher", "searcher@example.com");
        var hiddenSearch = await other.PostJsonAsync(
            "/question/similarity",
            new QuestionSimilarityRequest(question, 5),
            AppJsonSerializerContext.Default.QuestionSimilarityRequest);
        var hiddenResults = await hiddenSearch.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.ListQuestionSimilarity);
        Assert.NotNull(hiddenResults);
        var hiddenHit = Assert.Single(hiddenResults, result => result.Text == question);
        Assert.Null(hiddenHit.AskedByEmail);
        Assert.Null(hiddenHit.AskedByUserId);

        var hiddenGet = await other.GetAsync($"/question/{created.Id}");
        var hiddenDetail = await hiddenGet.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.QuestionDetail);
        Assert.NotNull(hiddenDetail);
        Assert.Equal(question, hiddenDetail.Text);
        Assert.Null(hiddenDetail.AskedByEmail);
        Assert.Null(hiddenDetail.AskedByUserId);

        var ownSearch = await Client.PostJsonAsync(
            "/question/similarity",
            new QuestionSimilarityRequest(question, 5),
            AppJsonSerializerContext.Default.QuestionSimilarityRequest);
        var ownResults = await ownSearch.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.ListQuestionSimilarity);
        Assert.NotNull(ownResults);
        var ownHit = Assert.Single(ownResults, result => result.Text == question);
        Assert.Equal("hidden-asker@example.com", ownHit.AskedByEmail);
        Assert.NotNull(ownHit.AskedByUserId);
    }

    [Fact]
    public async Task Search_WhenEmbeddingFails_ReturnsServiceUnavailable()
    {
        await Client.RegisterAndSignInAsync("Search User", "search-fail@example.com");

        var similarity = await Client.PostJsonAsync(
            "/question/similarity",
            new QuestionSimilarityRequest("no embedding", 5),
            AppJsonSerializerContext.Default.QuestionSimilarityRequest);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, similarity.StatusCode);

        var route = await Client.PostJsonAsync(
            "/expertise/route",
            new ExpertiseRouteRequest("no embedding", 5),
            AppJsonSerializerContext.Default.ExpertiseRouteRequest);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, route.StatusCode);
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
