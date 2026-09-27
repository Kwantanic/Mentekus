using Dapper;
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

        Client = CreateSession();
        Services = _factory.Services;
    }

    protected SessionClient CreateSession()
    {
        var handler = new CookieJarHandler(_factory.Server.CreateHandler());
        var client = new HttpClient(handler) { BaseAddress = _factory.Server.BaseAddress };
        return new SessionClient(client);
    }

    public virtual async Task DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await PostgreSqlContainer.DisposeAsync();
    }
}