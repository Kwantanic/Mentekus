FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        clang \
        gcc \
        zlib1g-dev
COPY ["Mentekus.Api/Mentekus.Api.csproj", "Mentekus.Api/"]
RUN dotnet restore "Mentekus.Api/Mentekus.Api.csproj"
COPY . .
WORKDIR "/src/Mentekus.Api"
RUN dotnet build "./Mentekus.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Mentekus.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Mentekus.Api"]
