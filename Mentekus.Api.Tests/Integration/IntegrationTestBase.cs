using Dapper;
using Mentekus.Api.Features.Expertise;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: DapperAot]

namespace Mentekus.Api.Tests.Integration;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlContainer PostgreSqlContainer = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .Build();

    private TestWebApplicationFactory _factory = null!;

    protected SessionClient Client { get; private set; } = null!;
    protected IServiceProvider Services { get; private set; } = null!;
    protected FakeOllamaAdapter Ollama => _factory.Ollama;

    public virtual async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();

        _factory = new TestWebApplicationFactory
        {
            ConnectionString = PostgreSqlContainer.GetConnectionString()
        };

        Services = _factory.Services;
        Client = CreateSession();
    }

    protected SessionClient CreateSession()
    {
        var handler = new CookieJarHandler(_factory.Server.CreateHandler());
        var client = new HttpClient(handler) { BaseAddress = _factory.Server.BaseAddress };
        return new SessionClient(client, WaitForTopicsAsync);
    }

    protected async Task WaitForTopicsAsync()
    {
        if (Services == null)
            return;

        var queue = Services.GetService<TopicExtractionQueue>();
        if (queue == null)
            return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await queue.WaitUntilIdleAsync(timeout.Token);
    }

    public virtual async Task DisposeAsync()
    {
        if (_factory != null)
        {
            try
            {
                await WaitForTopicsAsync();
            }
            catch (OperationCanceledException)
            {
                // Topic extraction did not finish before the host shut down.
            }

            await _factory.DisposeAsync();
        }

        await PostgreSqlContainer.DisposeAsync();
    }
}
