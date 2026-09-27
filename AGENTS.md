# Mentekus

.NET 10 Native AOT minimal API. How to run it is in `README.md`. Follow the Auth, User, and Question features as the reference layout.

## Stack constraints

- `PublishAot` and `WebApplication.CreateSlimBuilder`. Trim and Native AOT warnings are errors (`IL3050` and the `IL2xxx` family).
- Reflection JSON is forbidden. Register every request, response, and JSON DTO on `AppJsonSerializerContext`, and pass `AppJsonSerializerContext.Default.<Type>` to `PostAsJsonAsync` / `ReadFromJsonAsync`.
- Dapper.AOT intercepts queries. SQL lives in `*Sql.cs` as `public const string` raw literals. Pass the const and a parameters object. Do not use `CommandDefinition`. Dapper-mapped types are records with primary constructors, `public` or `internal`, under `Entities/`.
- No Moq, Castle DynamicProxy, or other runtime code generation.
- Services use `[RegisterScoped]` and primary constructors. Do not call `AddScoped` for features. Keep `Program.cs` to extension methods: `AddDatabase()`, `AddAdapters()`, `AddCookieAuth()`, and generated `AddMentekusApi()`.
- Accept `CancellationToken` on service and adapter methods and forward it. Dapper.AOT calls use the direct connection overloads, not `CommandDefinition`.
- New business logic goes in `Mentekus.Api/Features/<Feature>/`: `I*Service.cs`, `*Service.cs`, `*Endpoints.cs`, `*Sql.cs`, `Entities/`, `Requests/`.

## Endpoints

- Static `*Endpoints.cs` class with `[EndpointGroup]`, `using Mentekus.Api.Generated;`, and `public static void MapEndpoints`. The generator calls `MapAllEndpoints()`.
- Handlers are private static methods returning `TypedResults`.
- Feature groups call `.RequireAuthorization()` unless the route is an anonymous auth endpoint.
- The caller is `CurrentUser.GetId`. Do not take an email in the body to decide who is acting.
- Request DTOs are `sealed record`s with `[property: JsonRequired]` on mandatory fields. Responses that Dapper materializes are records in `Entities/`. Other response DTOs are `sealed record`s.
- Client errors throw `ValidationException`, `NotFoundException`, `ForbiddenException`, or `EmbeddingFailedException`. `RouteHandlerOptions.ThrowOnBadRequest` is true so invalid JSON reaches `GlobalExceptionHandler`.

## Auth

- `POST /auth/register` creates a user when the email is free. No confirmation email. Passwords are 8–128 characters, stored as PBKDF2-SHA256.
- Cookies: HttpOnly `mentekus.auth` (session) and HttpOnly `mentekus.xsrf`. Clients send `X-XSRF-TOKEN`. The token is bound to the current user, so register and login responses replace it.
- `XsrfPolicy` skips the header only when the environment is Development (`dotnet run`, Scalar at `/scalar/v1`). Production, the Compose API container, and tests require it.
- A user can always read their own expertise profile. Others can read it only when `ProfileVisible` is true. Preference updates apply only to the signed-in user. `AllowRouting` filters expert routing.

## Data

- Migrations are idempotent embedded scripts, `Shared/Database/Migrations/00NN_Name.sql`. DbUp runs them on startup.
- Email is `citext`. Compare with `CAST(@Email AS citext)`. `Email = @Email` binds text and is case-sensitive.
- Question embeddings are unconstrained `vector`. Expertise embeddings are `VECTOR(1024)`.
- Ollama model names come from `IOptions<OllamaOptions>` (`Model` and `EmbeddingModel`). Both `EmbedAsync` and `GenerateAsync` exist.
- `docker compose` and `compose.yaml`. Do not use `docker-compose`.

## Tests

- `dotnet test Mentekus.Api.Tests`. Integration tests need Docker. The factory sets the environment to `Testing`, so XSRF is required.
- Use `SessionClient` (`PostJsonAsync`, `RegisterAndSignInAsync`, `CreateSession` for a second user). `HttpClient`'s cookie container drops `SameSite=Lax` cookies on POST.
- Pure logic stays in unit tests with hand-written fakes. Database, pgvector, and Dapper.AOT queries are integration tests only.
- The test project sets `DynamicCodeSupport=false` and `JsonSerializerIsReflectionEnabledByDefault=false`, and declares `[assembly: DapperAot]`.

## Git

Conventional Commits: `type(scope): imperative summary`, no trailing period. Types: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Breaking changes use `type!:` or a `BREAKING CHANGE:` footer. Branches use `type/short-description`.
