# Agent Development Guide - Mentekus

This document provides project-specific information for developers and AI agents working on the Mentekus project.

## Project Overview & Key Technologies

Mentekus is a minimal .NET 10 Native AOT web API for semantic question handling. Callers sign in with an HttpOnly cookie. Mutating requests outside the Development environment also send an XSRF header.

Core stack:
- Native AOT (`PublishAot`) + `WebApplication.CreateSlimBuilder`
- Minimal APIs + source-generated endpoint mapping
- Dapper + Dapper.AOT + DbUp migrations
- PostgreSQL + pgvector for similarity search
- Ollama for embeddings (via typed HttpClient adapter)
- Injectio for source-generated DI registration (`[RegisterScoped]`)
- xUnit + Testcontainers for testing. The test host disables dynamic code, so fakes are hand-written (`FakeOllamaAdapter`), not Moq.
- System.Text.Json source generation for AOT-safe (de)serialization

## Getting Started (Build, Run, Docker)

### Environment Requirements
- .NET 10 SDK
- Docker + Docker Compose (strongly recommended)

### Local Development Setup

Use Docker Compose (the project uses `compose.yaml`):

```bash
docker compose up -d
```

This brings up:
- `postgres` (pgvector/pgvector:pg17 image) on port 5432 with vector extension pre-enabled via init script.
- `ollama` (custom image) that auto-pulls the required models on first start (`qwen3-embedding:0.6b` and `qwen3:4b-instruct`).

The API container listens on port 8080 and depends on both. `dotnet run` uses the Development launch profile at http://localhost:5277, where Scalar is available. That profile loads `appsettings.Development.json`, which points Postgres at `localhost`. Compose overrides the connection string so the container uses the hostname `postgres`.

To run the API directly (after infra is up):

```bash
dotnet run --project Mentekus.Api
```

In Development the Scalar UI is available at `/scalar/v1` (or raw OpenAPI at `/openapi`). Development does not require the `X-XSRF-TOKEN` header, so Scalar can call POST endpoints after register or login. The API container does not set the environment, so it runs as Production and does require the header. Scalar is not mapped outside Development.

### AOT Compilation & Build Constraints

The API project uses Native AOT:

- `WebApplication.CreateSlimBuilder(args)` in [Program.cs](Mentekus.Api/Program.cs).
- All JSON-serialized types **must** be registered in `AppJsonSerializerContext` (see below).
- `[assembly: DapperAot]` is declared in Program.cs.
- The csproj contains:
  ```xml
  <PublishAot>true</PublishAot>
  <InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Dapper.AOT</InterceptorsPreviewNamespaces>
  ```
- Dapper.AOT replaces reflection-based calls with interceptors at build time.
- The final Docker image uses the smaller `runtime-deps` base image (see multi-stage [Mentekus.Api/Dockerfile](Mentekus.Api/Dockerfile); the build stage installs `clang`, `gcc`, `zlib1g-dev`).

## Repository Structure & Feature Organization

- `Mentekus.Api/` — main application
  - `Features/<FeatureName>/`
    - `I*Service.cs` + `*Service.cs` (implementation)
    - `*Endpoints.cs` (static)
    - `*Sql.cs` (static class of raw string constants)
    - `Entities/` (Dapper-mapped POCOs and DTOs returned from Queries)
    - `Requests/` (input DTOs)
  - `Features/Auth/` — cookie sign-in, registration, and the XSRF check (`XsrfPolicy`, `AuthExtensions`)
  - `Shared/`
    - `Adapters/` (external service clients, e.g. Ollama)
    - `Database/` (DbUp migrations + connection setup + VectorTypeHandler)
    - `Endpoints/` (marker attribute source is generated here at build)
  - `Infrastructure/ErrorHandling/` (GlobalExceptionHandler + custom exceptions)
  - `Serialization/AppJsonSerializerContext.cs`
  - `Program.cs` (slim builder + composition root)
- `Mentekus.Api.SourceGenerators/` — incremental generator for `[EndpointGroup]` → `MapAllEndpoints()`
- `Mentekus.Api.Tests/` — integration tests (primary) + some unit tests
- `compose.yaml`, `postgres/init/`, `Ollama/`, root-level Dockerfiles

All new business logic goes under a feature folder following the above layout.

## Git

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for every commit.

- Subject line: `type(scope): imperative summary`. No trailing period. Scope is optional; use one when the change is local (`aot`, `dapper`, `expertise`, `api`).
- Types: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
- A body, when the subject is not enough, says why. Breaking changes use `type!:` or a `BREAKING CHANGE:` footer.
- Branch names use the same type prefix: `type/short-description` (for example `chore/aot-tests`).

## Coding Conventions

### Services & Dependency Injection
- Services are registered with the `[RegisterScoped(ServiceType = typeof(IFooService))]` attribute (Injectio source generator). No manual `AddScoped` calls for features.
- Implementations use **primary constructors** exclusively for DI:
  ```csharp
  [RegisterScoped(ServiceType = typeof(IQuestionService))]
  public class QuestionService(
      IOllamaAdapter ollamaAdapter,
      IExpertiseService expertiseService,
      IDbConnection connection) : IQuestionService
  ```
- Keep `Program.cs` clean by using extension methods (`AddDatabase()`, `AddAdapters()`, `AddCookieAuth()`, `AddMentekusApi()` generated by Injectio).
- Always accept and forward `CancellationToken`.

### Endpoints (Minimal APIs + Source Generator)
- Endpoints live in static classes named `*Endpoints.cs`.
- Decorate the class with `[EndpointGroup]` (the attribute is emitted by the source generator into `Mentekus.Api.Generated`).
- The class must contain a public static `MapEndpoints(IEndpointRouteBuilder endpoints)` method.
- You **must** add `using Mentekus.Api.Generated;` and the generator wires everything via the call to `app.MapAllEndpoints();` in Program.cs.
- Handlers are private static methods that return `Task<Ok<T>>` (or other `IResult` types) using `TypedResults`:
  ```csharp
  [EndpointGroup]
  public static class QuestionEndpoints
  {
      public static void MapEndpoints(IEndpointRouteBuilder endpoints)
      {
          var group = endpoints.MapGroup("question/").WithTags("Question").RequireAuthorization();
          group.MapPost("ask", HandleAskAsync);
          group.MapPost("similarity", HandleSimilarityAsync);
      }

      private static async Task<Ok<string>> HandleAskAsync(
          QuestionAskRequest request, IQuestionService questionService, ClaimsPrincipal user, CancellationToken cancellationToken)
      {
          var answer = await questionService.AskAsync(request.Question, CurrentUser.GetId(user), cancellationToken);
          return TypedResults.Ok(answer);
      }
  }
  ```
- Example source generator lives in `Mentekus.Api.SourceGenerators/EndpointGroupGenerator.cs`.

### Data Transfer Objects (Requests, Responses, Entities)
- Request DTOs are `sealed record` with `[property: JsonRequired]` on mandatory parameters:
  ```csharp
  public sealed record QuestionAskRequest(
      [property: JsonRequired] string Question);
  ```
- Response DTOs are usually `sealed record` if not used for Dapper mapping. If a response DTO is populated directly from a Dapper query, it **must** be a `record` in the `Entities/` folder.
- **Entities** (and Dapper-mapped response types) are `record` types with primary constructors. They exist for Dapper materialization (Native AOT requirement):
  ```csharp
  public record Question(
      Guid Id,
      string Text,
      Vector? Embedding,
      DateTime CreatedAt,
      Guid? AskedByUserId);
  ```

### SQL Queries
- Every feature has a `*Sql.cs` file containing `public const string` raw literals.
- Use C# raw string literals (`"""`).
- Formatting rules (follow exactly for consistency):
  - Opening `"""` followed by newline.
  - First SQL keyword on its own line, indented with 4 spaces.
  - Subsequent lines indented to align under the keyword.
  - Closing `""";` on its own line at the same indent level as the opening content.
- Correct example from the codebase:
  ```csharp
  public static class QuestionSql
  {
      public const string InsertQuestion = """
          INSERT INTO Questions (Id, Text, Embedding, CreatedAt, AskedByUserId) 
          VALUES (@Id, @Text, @Embedding, @CreatedAt, @AskedByUserId)
          """;

      public const string FindSimilarQuestions = """
          SELECT q.Text, 1 - (q.Embedding <=> @Vector) AS Similarity, q.AskedByUserId, u.Email AS AskedByEmail
          FROM Questions q
          JOIN Users u ON q.AskedByUserId = u.Id
          WHERE q.Embedding IS NOT NULL
          ORDER BY q.Embedding <=> @Vector
          LIMIT @Limit
          """;
  }
  ```
- Always pass the const + parameters object (or entity) to `connection.ExecuteAsync` / `QueryAsync`.

### JSON Serialization (AOT)
- Reflection-based JSON is forbidden.
- Configure once in Program.cs:
  ```csharp
  builder.Services.ConfigureHttpJsonOptions(options =>
  {
      options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
      options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
  });
  ```
- Manually maintain [AppJsonSerializerContext.cs](Mentekus.Api/Serialization/AppJsonSerializerContext.cs):
  ```csharp
  [JsonSerializable(typeof(QuestionAskRequest))]
  [JsonSerializable(typeof(QuestionSimilarityResponse))]
  [JsonSerializable(typeof(List<QuestionSimilarityResponse>))]
  [JsonSerializable(typeof(OllamaEmbedRequest))]
  [JsonSerializable(typeof(OllamaEmbedResponse))]
  [JsonSerializable(typeof(Vector))]
  [JsonSerializable(typeof(ProblemDetails))]
  [JsonSerializable(typeof(ValidationProblemDetails))]
  [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ...)]
  public partial class AppJsonSerializerContext : JsonSerializerContext { }
  ```
- Usage (HttpClient + minimal APIs):
  - `PostAsJsonAsync(..., AppJsonSerializerContext.Default.YourType)`
  - `ReadFromJsonAsync(..., AppJsonSerializerContext.Default.YourType)`
- Always add new request/response types (and `Vector`, ProblemDetails derivatives) here immediately.
- `[property: JsonRequired]` on records gives good validation errors that the GlobalExceptionHandler turns into clean messages.

### Configuration & Options
- Use strongly-typed options classes with a `SectionName` constant:
  ```csharp
  public sealed class OllamaOptions
  {
      public const string SectionName = "Ollama";
      public string? BaseUrl { get; set; }
      public string Model { get; set; } = "qwen3:4b-instruct";
      public string EmbeddingModel { get; set; } = "qwen3-embedding:0.6b";
  }
  ```
- Bind + validate in the adapter extension:
  ```csharp
  services.AddOptions<OllamaOptions>()
      .BindConfiguration(OllamaOptions.SectionName)
      .ValidateOnStart();
  ```
- The main `appsettings.json` + `compose.yaml` environment variables drive config (`Ollama__BaseUrl` etc.).

### Error Handling
- Throw custom exceptions from [Infrastructure/ErrorHandling/Exceptions/](Mentekus.Api/Infrastructure/ErrorHandling/Exceptions/):
  - `ValidationException` (for business/validation errors; supports property → messages dict)
  - `NotFoundException`
  - `ForbiddenException` (signed in, but not allowed to act on that resource)
  - `EmbeddingFailedException`
- The `GlobalExceptionHandler` (registered via `AddExceptionHandler`) maps them to correct HTTP status codes + RFC 7807 ProblemDetails (with `traceId` extension).
- `JsonException` (from missing `[property: JsonRequired]`) and `ArgumentException` also become 400s.
- `RouteHandlerOptions.ThrowOnBadRequest` is true so invalid JSON is thrown into `GlobalExceptionHandler` instead of an empty 400.
- Never return raw strings or throw generic exceptions from endpoint handlers for client errors — use the typed exceptions.

## Database, Migrations & pgvector

- **Connection setup** lives in [Shared/Database/DatabaseExtensions.cs](Mentekus.Api/Shared/Database/DatabaseExtensions.cs):
  - `NpgsqlDataSourceBuilder` + `.UseVector()`
  - `IDbConnection` resolved per scope from the data source
  - `SqlMapper.AddTypeHandler(new VectorTypeHandler())`
- **Migrations**: DbUp runs on every startup via `app.MigrateDatabase()` (see Program.cs). Scripts are embedded resources:
  ```xml
  <EmbeddedResource Include="Shared\Database\Migrations\*.sql"/>
  ```
- Scripts live in `Mentekus.Api/Shared/Database/Migrations/`, named `00NN_DescriptiveName.sql`.
- Scripts must be idempotent (`CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, etc.).
- A separate `postgres/init/001-enable-vector.sql` (and later citext migration) runs automatically when the Postgres container is created for the first time.
- Current schema notes: emails use `citext` for case-insensitive uniqueness; `Users.PasswordHash` stores the PBKDF2 hash; questions link to users via `AskedByUserId`. Compare emails with `CAST(@Email AS citext)`. A plain `Email = @Email` binds the parameter as text and PostgreSQL then compares case-sensitively.

## Authentication

- `POST /auth/register` creates any non-conflicting user. Email uniqueness is case-insensitive. There is no confirmation step. Passwords are 8 to 128 characters and are stored as a PBKDF2-SHA256 hash.
- `POST /auth/login` and `POST /auth/logout` start and end the session. `GET /auth/me` returns the signed-in user.
- The session cookie is `mentekus.auth` (HttpOnly). The XSRF cookie is `mentekus.xsrf` (HttpOnly). Clients send `X-XSRF-TOKEN` with the value from `GET /auth/xsrf` or from the `xsrfToken` field on register and login.
- `XsrfPolicy` skips that header only when the host environment is Development, which is the local `dotnet run` profile Scalar uses. Production, the Compose API container, and the test host all require it.
- Feature endpoints call `.RequireAuthorization()`. The handler reads the caller with `CurrentUser.GetId`. Do not accept an email in the body to decide who is acting.
- A user can always read their own expertise profile. Other signed-in users can read it only when `ProfileVisible` is true. Preference updates are limited to the signed-in user. `AllowRouting` still controls expert routing.

## External Adapters

Follow the Ollama pattern:
- Interface in `Shared/Adapters/IOllamaAdapter.cs`
- Implementation decorated with `[RegisterScoped]`, primary ctor taking `HttpClient` + `IOptions<>`
- Configuration and `HttpClient` registration in a static `*Extensions.cs`
- Always use the `AppJsonSerializerContext` overloads for `PostAsJsonAsync` / `ReadFromJsonAsync`
- The adapter file also defines the small request/response records used only for the external call.

`IOllamaAdapter` implements both `EmbedAsync` (embedding model) and `GenerateAsync` (instruct model). Model names come from `OllamaOptions`.

## Testing

### Running Tests
- Standard: `dotnet test Mentekus.Api.Tests`
- Inside the Grok TUI you may also have access to a `run_test` helper.

### Native AOT behavior in the test host
`dotnet test` hosts the API with `WebApplicationFactory`, so `Mentekus.Api.runtimeconfig.json` is not applied. The test project sets the same switches Native AOT sets for the app:

- `DynamicCodeSupport=false` (`RuntimeFeature.IsDynamicCodeSupported` is false). Reflection.Emit and other runtime code generation throw, the same as a published Native AOT binary.
- `JsonSerializerIsReflectionEnabledByDefault=false`. JSON that is not registered on `AppJsonSerializerContext` throws. `PostAsJsonAsync` / `ReadFromJsonAsync` in tests must pass `AppJsonSerializerContext.Default.<Type>`.
- `System.Linq.Expressions.CanEmitObjectArrayDelegate=false`.

Do not add Moq, Castle DynamicProxy, or any other library that generates code at runtime. Replace services with a hand-written fake (`FakeOllamaAdapter`).

The test project declares `[assembly: DapperAot]`. Dapper calls in tests are intercepted the same way as the API. Do not use `CommandDefinition`.

`Mentekus.Api` treats Native AOT and trim warnings as errors (`IL3050` and the `IL2xxx` trim codes). `dotnet test` builds that project, so a new dynamic-code call fails the test run before any test executes. Test-only reflection (for example invoking a private helper) stays in the test project, which is not published with Native AOT.

### Unit vs Integration Tests
- Pure logic or adapter tests (no Dapper/pgvector) → unit tests with hand-written fakes (see [OllamaAdapterTests.cs](Mentekus.Api.Tests/OllamaAdapterTests.cs)).
- Anything touching the database, vectors, or Dapper.AOT compiled queries → **integration tests only**.

### Integration Test Pattern (Testcontainers + WebApplicationFactory)
Use the provided base classes. They give you a real Postgres + pgvector container and replace `IOllamaAdapter` with `FakeOllamaAdapter`.

Accurate minimal example (reflects current [IntegrationTestBase.cs](Mentekus.Api.Tests/Integration/IntegrationTestBase.cs) + factory + tests):

```csharp
public class QuestionEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task Ask_ReturnsOk_AndSavesToDatabase()
    {
        await Client.RegisterAndSignInAsync("Test User", "test@example.com");

        var questionText = "What is Native AOT?";
        Ollama.Embed(questionText, [0.1f, 0.2f, 0.3f]);

        var response = await Client.PostJsonAsync(
            "/question/ask",
            new QuestionAskRequest(questionText),
            AppJsonSerializerContext.Default.QuestionAskRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

Base (current shape):

```csharp
public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly PostgreSqlContainer PostgreSqlContainer = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .Build();

    private TestWebApplicationFactory _factory = null!;

    protected SessionClient Client { get; private set; } = null!;
    protected FakeOllamaAdapter Ollama => _factory.Ollama;

    public virtual async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();

        _factory = new TestWebApplicationFactory
        {
            ConnectionString = PostgreSqlContainer.GetConnectionString()
        };

        Client = CreateSession();
    }

    public virtual async Task DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await PostgreSqlContainer.DisposeAsync();
    }
}
```

Factory overrides the connection string, replaces the real adapter with `FakeOllamaAdapter`, and sets the environment to `Testing`. That is outside Development, so `SessionClient.PostJsonAsync` fetches `GET /auth/xsrf` and sends `X-XSRF-TOKEN`. `RegisterAndSignInAsync` stores the session cookie on that client. A second user in the same test needs `CreateSession()`, because one client holds one session.

Always pass `AppJsonSerializerContext.Default.XXX` to `PostJsonAsync` and `ReadFromJsonAsync`. Reflection JSON is disabled in this process, so a call without `JsonTypeInfo` throws even when the server context already knows the type.

## Common Pitfalls & Gotchas

- Forgetting to add the new request/response type to `AppJsonSerializerContext` → the test host throws because reflection JSON is disabled. The same call fails in the Native AOT publish.
- Introducing Moq or any `Reflection.Emit` helper. `DynamicCodeSupport` is false in the test project; proxy generation throws `PlatformNotSupportedException`.
- Calling `PostAsJsonAsync` / `ReadFromJsonAsync` without `AppJsonSerializerContext`.
- Posting from a test with `HttpClient` directly and omitting `X-XSRF-TOKEN`. The test host is `Testing`, not Development. Use `SessionClient`. `HttpClient`'s cookie container also drops `SameSite=Lax` cookies on POST, so a raw client will not send `mentekus.auth` or `mentekus.xsrf`.
- Treating an email field as the caller. Authentication comes from `mentekus.auth`.
- Requiring the XSRF header in Development, which breaks Scalar, or skipping it outside Development.
- Using `docker-compose` (v1) instead of `docker compose` and `compose.yaml`.
- Writing SQL queries directly in service files instead of `*Sql.cs`.
- Wrong raw-string formatting for SQL (the closing `""";` indent controls dedent — misalignment produces ugly or broken SQL at runtime).
- Throwing `ArgumentException` / returning `BadRequest(string)` instead of `ValidationException` or `NotFoundException`.
- Trying to unit-test Dapper + pgvector with in-memory fakes (it won't work; use the Testcontainers integration base).
- Omitting `using Mentekus.Api.Generated;` in a new `*Endpoints.cs` file.
- Registering services with `AddScoped` instead of the `[RegisterScoped]` attribute.
- Using classes for types that Dapper must hydrate (use records with primary constructors).
- Placing Dapper-mapped types anywhere other than the `Entities/` folder.
- Private records/classes used in Dapper queries (must be `internal` or `public` for Dapper.AOT).
- Not calling `MigrateDatabase()` or having non-idempotent migration scripts.
- Hard-coding model names instead of reading from `IOptions<OllamaOptions>`.
- Forgetting `CancellationToken` propagation on service/adapter boundaries.
- Using `CommandDefinition` (Dapper) which is not supported by Dapper.AOT (use direct `connection` method overloads instead).
- Placing response DTOs in a different namespace than the one used in the `JsonSerializable` attribute.

## Checklist: Adding a New Feature

1. Create folder `Features/NewFeature/`.
2. Define `INewFeatureService.cs` (interface with `Task<...> Method(..., CancellationToken ct = default)`).
3. Create `NewFeatureService.cs`:
   - Primary constructor dependencies.
   - `[RegisterScoped(ServiceType = typeof(INewFeatureService))]`.
   - Implement using `IDbConnection` + `NewFeatureSql.XXX` constants.
4. Create `NewFeatureSql.cs` with properly formatted raw string constants.
5. Create `Requests/NewFeatureXxxRequest.cs` (sealed records + `[property: JsonRequired]` where needed).
6. Create `Entities/` folder and add Dapper-mapped entities or response classes (records with primary constructors).
7. Create `NewFeatureEndpoints.cs`:
   - `using Mentekus.Api.Generated;`
   - `[EndpointGroup] public static class ...`
   - `public static void MapEndpoints(...)`
   - Private static handler methods using TypedResults.
   - `.RequireAuthorization()` on the group unless the route is an anonymous auth endpoint.
8. Add every new serializable type to `AppJsonSerializerContext.cs` (requests, responses, any internal records passed to JSON).
9. Add or update an integration test that uses `SessionClient` (register and sign in when the endpoint requires a user).
10. Update any OpenAPI/Scalar-friendly tags or the `.http` file if you want quick manual verification.
11. `dotnet build` + `dotnet test` (or run the integration scenario).

## Dev Tools & References

- API exploration: Scalar UI (`/scalar/v1` in dev) or the `.http` file in the Mentekus.Api folder.
- Migrations are re-run on every `dotnet run` / container start (DbUp is idempotent).
- The source generator for endpoints runs automatically on build; generated code lives under `obj/...` or the `Mentekus.Api.Generated` namespace at runtime.
- All environment configuration for Docker is in `compose.yaml` (override via environment variables using double-underscore or colon syntax).

Follow the patterns in Auth, User, and Question as the canonical reference.
