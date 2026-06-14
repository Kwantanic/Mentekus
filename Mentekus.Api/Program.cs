using Dapper;
using Mentekus.Api.Generated;
using Mentekus.Api.Serialization;
using Mentekus.Api.Shared.Adapters;
using Mentekus.Api.Shared.Database;
using Scalar.AspNetCore;

[assembly: DapperAot]

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddDatabase();
builder.Services.AddAdapters();
builder.Services.AddMentekusApi();

builder.Services.AddOpenApi();

var app = builder.Build();

app.MigrateDatabase();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapAllEndpoints();

await app.RunAsync();