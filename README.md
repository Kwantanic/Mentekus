# Mentekus

Mentekus is a minimal .NET 10 Native AOT web API for semantic question handling. It stores embeddings in PostgreSQL with pgvector, calls Ollama for embeddings and topic extraction, and uses Dapper.AOT, Injectio, and DbUp.

## Features

- Minimal APIs with source-generated endpoint mapping
- Ollama embeddings and text generation
- PostgreSQL + pgvector similarity search
- HttpOnly cookie sessions and an XSRF token
- Expertise profiles: question and answer contributions, document ingest, vector blending with decay, a topic graph, and hybrid expert routing

## Prerequisites

- Docker Desktop with Compose v2
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) for `dotnet run` and `dotnet test`

## Run with Docker Compose

```bash
docker compose up -d --build
```

This starts:

- `mentekus.api` on http://localhost:8080
- Postgres with pgvector on localhost:5432
- Ollama, which pulls `qwen3-embedding:0.6b` and `qwen3:4b-instruct` the first time it starts

The API container does not set `ASPNETCORE_ENVIRONMENT`, so it runs as Production. Mutating requests must send the XSRF header. Scalar is mapped only when the environment is Development.

The Ollama service reserves a GPU. On a machine without one, remove the `deploy.resources.reservations` block from `compose.yaml` before starting.

## Run the API on the host

Start Postgres with Compose, then:

```bash
dotnet run --project Mentekus.Api
```

The launch profile is Development and listens on http://localhost:5277. Scalar is at http://localhost:5277/scalar/v1.

`appsettings.json` uses the Compose hostname `postgres`. `appsettings.Development.json` points `dotnet run` at `localhost` instead. The API container still uses `postgres` because Compose sets `ConnectionStrings__DefaultConnection`.

`Mentekus.Api.http` targets port 8080. For `dotnet run`, set `Mentekus.Api_HostAddress` to `http://localhost:5277`.

## Authentication

`POST /auth/register` creates a user. Email is unique case-insensitively. Registration does not send a confirmation message. Passwords must be 8 to 128 characters. A duplicate email returns 400.

`POST /auth/login` checks the password and starts a session. `POST /auth/logout` ends it. `GET /auth/me` returns the signed-in user.

`mentekus.auth` is the HttpOnly session cookie. `mentekus.xsrf` is a second HttpOnly cookie. Clients send the matching value in the `X-XSRF-TOKEN` header. `GET /auth/xsrf` returns that value, and register and login responses include `xsrfToken` as well.

The Development environment does not require the header, so Scalar on the local launch profile can POST after register or login. Every other environment, including the test host, rejects a mutating request without a matching token.

Question, user, and expertise routes require a signed-in user. Asking, answering, and document ingest act as that user. You can always read your own expertise profile. Another user can read it only when `ProfileVisible` is true. Preference updates apply only to your own account. Routing still omits users with `AllowRouting` set to false.

## Tests

```bash
dotnet test Mentekus.Api.Tests
```

Integration tests need a running Docker engine. The test host uses the `Testing` environment, so it requires the XSRF header. `SessionClient` loads the token and sends it.
