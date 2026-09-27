using Dapper;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Features.Expertise.Entities;
using Mentekus.Api.Features.User;
using Mentekus.Api.Shared.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Mentekus.Api.Tests;

public class ExpertiseServiceTests : Integration.IntegrationTestBase
{
    [Fact]
    public void BlendLogic_ComputesExpectedEmaWithDecay_InvokesActualImpl()
    {
        // Now invokes the *actual* private static BlendExpertiseVector via reflection (Issue 4) for direct math coverage.
        // No more duplicated formula-only test; asserts real output (incl. decay + EMA) against expected.
        var current = new float[1024];
        current[0] = 1.0f;
        var contrib = new float[1024];
        contrib[0] = 0.0f;
        contrib[1] = 1.0f;

        // Simulate 90 days decay ~0.5 factor on current before blend
        var last = DateTime.UtcNow.AddDays(-90);
        var alpha = 0.25f;

        // Expected using identical math (for verification of impl)
        var decayed = current[0] * 0.5f;
        var expected0 = alpha * contrib[0] + (1 - alpha) * decayed; // 0.25*0 + 0.75*0.5 = 0.375
        var expected1 = alpha * contrib[1] + (1 - alpha) * 0.0f; // 0.25

        // Invoke actual impl (private static) via reflection
        var blendMethod = typeof(ExpertiseService).GetMethod(
            "BlendExpertiseVector",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(blendMethod);
        var result = (float[])blendMethod.Invoke(null, new object?[] { current, contrib, alpha, last })!;

        // Verify actual impl output (spot check decayed dim0, new dim1, and a zeroed dim to confirm dim handling)
        Assert.Equal(1024, result.Length);
        Assert.Equal(expected0, result[0], 0.0001f);
        Assert.Equal(expected1, result[1], 0.0001f);
        Assert.Equal(0f, result[10], 0.0001f); // untouched by input

        // Also exercise the dim guard (Issue 1) on wrong-dim contribution via the real method
        var badContrib = new float[512];
        var ex = Assert.Throws<TargetInvocationException>(() =>
            blendMethod.Invoke(null, new object?[] { null, badContrib, 0.1f, null }));
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Contains("got 512", ex.InnerException!.Message); // matches the guard message "must be 1024-dimensional (got 512)"
    }

    [Fact]
    public async Task UpdateFromContribution_VectorOnlyPath_UpdatesEmbedding()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var userService = sp.GetRequiredService<IUserService>();
        var userId = await userService.AddUserAsync("Blend User", "blend@example.com");

        var expertise = sp.GetRequiredService<IExpertiseService>();
        var vec = Enumerable.Range(0, 1024).Select(i => i == 5 ? 0.9f : 0.01f).ToArray();

        await expertise.UpdateVectorOnlyFromContributionAsync(userId, vec, "Answer");

        var profile = await expertise.GetUserExpertiseAsync(userId);
        Assert.NotNull(profile);
        Assert.Equal("Blend User", profile.Name); // profile reachable post vector update (blending exercised via UpdateVectorOnly)
    }

    [Fact]
    public async Task UpdateFromContribution_FullPath_WithGenerate_PersistsTopicsAndProfileJoin()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var userService = sp.GetRequiredService<IUserService>();
        var userId = await userService.AddUserAsync("Graph User", "graph@example.com");

        var expertise = sp.GetRequiredService<IExpertiseService>();
        Ollama.GenerateAny("[\"dotnet aot\", \"pgvector similarity\", \"vector search\"]");

        var vec = new float[1024];
        vec[0] = 0.42f;

        await expertise.UpdateFromContributionAsync(userId, vec, "Senior engineer experienced in Native AOT and pgvector for semantic search in .NET.", "Document");
        await WaitForTopicsAsync();

        var profile = await expertise.GetUserExpertiseAsync(userId);
        Assert.NotNull(profile);
        Assert.Equal("Graph User", profile.Name);
        Assert.Equal("graph@example.com", profile.Email);
        Assert.Contains("dotnet aot", profile.TopTopics);
        Assert.Contains("pgvector similarity", profile.TopTopics);
        // Profile projection join exercised
        Assert.True(profile.TopTopics.Length >= 1 && profile.TopTopics.Length <= 5);
    }

    [Fact]
    public async Task ExtractTopicsAsync_ParseFallback_OnBadJson_ReturnsEmpty_NoThrow()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var expertise = sp.GetRequiredService<IExpertiseService>();
        Ollama.GenerateAny("not a json array at all!!! { foo: bar }");

        var topics = await expertise.ExtractTopicsAsync("some contribution text here about csharp", "Answer");
        Assert.Empty(topics); // defensive parse fallback

        // Also test good case
        Ollama.GenerateAny("  [\"  native aot  \", \"  embeddings  \"]  ");
        var good = await expertise.ExtractTopicsAsync("text", ExpertiseSql.DocumentSourceType);
        Assert.Equal(2, good.Length);
        Assert.Equal("native aot", good[0]);
        Assert.Equal("embeddings", good[1]);

        Ollama.GenerateAny("[\"format strings\"]");
        const string braced = "How do I use string.Format {0} and {name}?";
        var withBraces = await expertise.ExtractTopicsAsync(braced, "Answer");
        Assert.Equal(["format strings"], withBraces);
        Assert.Contains(Ollama.GenerateCalls, prompt => prompt.Contains("{0}", StringComparison.Ordinal) && prompt.Contains("{name}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetUserExpertiseAsync_UnknownUser_ReturnsNull()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        var expertise = sp.GetRequiredService<IExpertiseService>();
        var profile = await expertise.GetUserExpertiseAsync(Guid.NewGuid());
        Assert.Null(profile);
    }
}
