using Mentekus.Api.Shared.Adapters;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Mentekus.Api.Tests.Integration;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlContainer PostgreSqlContainer = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .Build();

    private TestWebApplicationFactory _factory = null!;

    protected HttpClient Client { get; private set; } = null!;
    protected IServiceProvider Services { get; private set; } = null!;
    protected Mock<IOllamaAdapter> OllamaAdapterMock => _factory.OllamaAdapterMock;

    public virtual async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();

        _factory = new TestWebApplicationFactory
        {
            ConnectionString = PostgreSqlContainer.GetConnectionString()
        };

        Client = _factory.CreateClient();
        Services = _factory.Services;
    }

    public virtual async Task DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await PostgreSqlContainer.DisposeAsync();
    }
}