# Mentekus

Mentekus is a minimal .NET 10 Native AOT web API for semantic question handling using vector embeddings (Ollama), pgvector similarity search in PostgreSQL, Dapper.AOT, Injectio source-generated DI, and DbUp migrations.

## Features
- Minimal APIs
- Ollama embeddings + PostgreSQL + pgvector
- Native AOT + source-generated DI (Injectio)
- Integration tests with Testcontainers
- User expertise system: answer contribution + document ingest for vector blending (with decay) + topic graph, expertise profiles (with visibility prefs), hybrid routing to experts (vector sim + MatchedTopics + flag filtering for AllowRouting)

## Prerequisites
- Docker & Docker Compose (recommended — handles .NET build automatically)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (only for local `dotnet run` / testing without Docker)

## Setup (Docker)
```bash
docker-compose up -d