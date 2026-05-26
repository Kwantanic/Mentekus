# Agent Development Guide - Mentekus

This document provides project-specific information for developers and AI agents working on the Mentekus project.

## 1. Build & Configuration

### Environment Requirements

- **.NET 10 SDK**
- **Docker & Docker Compose** (required for PostgreSQL with `pgvector` and Ollama)

### Local Development Setup

The easiest way to start the required infrastructure is via Docker Compose:

```bash
docker-compose up -d
```

This starts:

- **Postgres**: With `pgvector` extension enabled. Accessible on port `5432`.
- **Ollama**: Pre-configured with models defined in `ollama/init-models.sh`.

### AOT Compilation

The project is configured for **Native AOT** (`<PublishAot>true</PublishAot>`).

- Use `WebApplication.CreateSlimBuilder(args)` for minimal footprint.
- All types used in JSON serialization must be registered in `AppJsonSerializerContext`.
- **Dapper.AOT** is used to ensure Dapper is AOT-compatible.
    - The project file must include
      `<InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Dapper.AOT</InterceptorsPreviewNamespaces>`.
    - `[assembly: DapperAot]` must be present in the project (usually in `Program.cs`).
    - Dapper.AOT uses C# Interceptors to replace reflection-based Dapper calls with AOT-friendly code at build time.

## 2. Testing Information

### Running Tests

Tests are located in `Mentekus.Api.Tests`. Use the standard .NET CLI to run them:

```bash
dotnet test
```

### Adding New Tests

- **Unit Tests**: Use `xUnit` and `Moq`. These are suitable for business logic that does not depend on Dapper extension
  methods.
- **Dapper & Vector Tests**:
    - Standard in-memory databases (EF Core InMemory, SQLite) **do not support pgvector**.
    - Mocking `IDbConnection` for Dapper (especially `Dapper.AOT`) is fragile because extension methods are difficult to
      mock and often require casting to `DbConnection`.
    - **Recommended Approach**: Use **Integration Tests** with a real PostgreSQL instance via **Testcontainers**. This
      is the only reliable way to verify `pgvector` queries and Dapper.AOT compatibility.

#### Integration Test Example (Testcontainers & WebApplicationFactory)

Integration tests use `TestWebApplicationFactory` (which inherits from `WebApplicationFactory`) to spin up the API and
`Testcontainers` to provide a real PostgreSQL instance. `IntegrationTestBase` manages the lifecycle and provides access
to the API client and mocks.

```csharp
public class QuestionEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Ask_ReturnsOk_AndSavesToDatabase()
    {
        // Arrange
        var questionText = "What is Native AOT?";
        var expectedEmbedding = new[] { new float[] { 0.1f, 0.2f, 0.3f } };

        OllamaAdapterMock
            .Setup(a => a.EmbedAsync(It.IsAny<OllamaEmbedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaEmbedResponse(expectedEmbedding));

        var request = new QuestionAskRequest(questionText);

        // Act
        var response = await Client.PostAsJsonAsync("/question/ask", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(questionText, content);
    }
}
```

Base class `IntegrationTestBase` handles the lifecycle of the PostgreSQL container and the test factory.

```csharp
public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlContainer PostgreSqlContainer = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .Build();

    private TestWebApplicationFactory _factory = null!;

    protected HttpClient Client { get; private set; } = null!;
    protected Mock<IOllamaAdapter> OllamaAdapterMock => _factory.OllamaAdapterMock;

    public virtual async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();

        _factory = new TestWebApplicationFactory
        {
            ConnectionString = PostgreSqlContainer.GetConnectionString()
        };

        Client = _factory.CreateClient();
    }

    public virtual async Task DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await PostgreSqlContainer.DisposeAsync();
    }
}
```

## 3. Additional Development Information

### Code Style & Architecture

- **Feature-based Structure**: Code is organized by features (e.g., `Features/Question`).
- **Minimal APIs**: Endpoints are defined using Minimal APIs in `*Endpoints.cs` files and mapped in `Program.cs`.
- **Dependency Injection**: Extensions like `AddAdapters()` and `AddFeatures()` are used to keep `Program.cs` clean.

### JSON Serialization (AOT)

Due to AOT, reflection-based serialization is discouraged.

- Always use `AppJsonSerializerContext.Default` when configuring JSON options or using `HttpClient` JSON extensions.
- When adding new DTOs or Entities that will be serialized, add them to `AppJsonSerializerContext` using
  `[JsonSerializable]`.
- Types like `Pgvector.Vector` must also be registered if they are serialized.

### Database

- **Migrations**: **DbUp** is used for database migrations. Scripts are located in
  `Mentekus.Api/Shared/Database/Migrations` and are embedded in the assembly.
- **Micro-ORM**: **Dapper** is used for database access. To ensure Native AOT compatibility, avoid features that rely on
  runtime IL generation where possible.
- **Pgvector**: Vector embeddings are handled via `Npgsql` and `Pgvector` packages.
  `NpgsqlDataSourceBuilder.UseVector()` must be called when configuring the connection.
