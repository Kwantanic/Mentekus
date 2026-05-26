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
  - The project file must include `<InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Dapper.AOT</InterceptorsPreviewNamespaces>`.
  - `[assembly: DapperAot]` must be present in the project (usually in `Program.cs`).
  - Dapper.AOT uses C# Interceptors to replace reflection-based Dapper calls with AOT-friendly code at build time.

## 2. Testing Information

### Running Tests

Tests are located in `Mentekus.Api.Tests`. Use the standard .NET CLI to run them:

```bash
dotnet test
```

### Adding New Tests

- **Unit Tests**: Use `xUnit` and `Moq`.
- **Database Tests**: Since the project uses `pgvector`, standard `InMemoryDatabase` provider will fail for entities
  containing `Vector` properties. For integration tests involving the database, use a real PostgreSQL instance (e.g.,
  via Testcontainers).

### Test Example (Mocking Adapter)

```csharp
[Fact]
public async Task EmbedAsync_ReturnsResponse_WhenApiReturnsOk()
{
    var expectedResponse = new OllamaEmbedResponse(new[] { new float[] { 0.1f, 0.2f } });
    var handlerMock = new Mock<HttpMessageHandler>();
    handlerMock.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = JsonContent.Create(expectedResponse) });

    var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:11434") };
    var adapter = new OllamaAdapter(httpClient);
    var result = await adapter.EmbedAsync(new OllamaEmbedRequest("model", "input"));

    Assert.NotNull(result);
    Assert.Single(result.Embeddings);
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
