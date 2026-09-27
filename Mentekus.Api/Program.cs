using System.Text.Json.Serialization;
using Dapper;
using Mentekus.Api.Features.Auth;
using Mentekus.Api.Features.Expertise;
using Mentekus.Api.Generated;
using Mentekus.Api.Infrastructure.ErrorHandling;
using Mentekus.Api.Serialization;
using Mentekus.Api.Shared;
using Mentekus.Api.Shared.Adapters;
using Mentekus.Api.Shared.Database;
using Scalar.AspNetCore;

[assembly: DapperAot]

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddDatabase();
builder.Services.AddAdapters();
builder.Services.AddTopicExtraction();
builder.Services.AddCookieAuth();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddMentekusApi();

builder.Services.AddOpenApi();

var app = builder.Build();

app.MigrateDatabase();

app.UseExceptionHandler();
app.UseCookieAuth();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealth();
app.MapAllEndpoints();

await app.RunAsync();
