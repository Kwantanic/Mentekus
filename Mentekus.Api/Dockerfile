FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        clang \
        gcc \
        zlib1g-dev && \
    rm -rf /var/lib/apt/lists/*
# Restore needs the source generator project because the API references it.
COPY Mentekus.Api.SourceGenerators/Mentekus.Api.SourceGenerators.csproj Mentekus.Api.SourceGenerators/
COPY Mentekus.Api/Mentekus.Api.csproj Mentekus.Api/
RUN dotnet restore Mentekus.Api/Mentekus.Api.csproj -r linux-x64
COPY . .
WORKDIR /src/Mentekus.Api
RUN dotnet publish Mentekus.Api.csproj -c $BUILD_CONFIGURATION -r linux-x64 -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["./Mentekus.Api"]
