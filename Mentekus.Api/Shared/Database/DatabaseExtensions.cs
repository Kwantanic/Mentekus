using System.Data;
using System.Reflection;
using Dapper;
using DbUp;
using Npgsql;
using Pgvector;

namespace Mentekus.Api.Shared.Database;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        SqlMapper.AddTypeHandler(new VectorTypeHandler());

        services.AddScoped(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        services.AddScoped<IDbConnection>(serviceProvider =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            return dataSource.OpenConnection();
        });

        return services;
    }

    public static IHost MigrateDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IHost>>();
            logger.LogError(result.Error, "Database migration failed: {Message}", result.Error?.Message);
            throw new Exception("Database migration failed.", result.Error);
        }

        return host;
    }

    public sealed class VectorTypeHandler : SqlMapper.TypeHandler<Vector>
    {
        public override void SetValue(IDbDataParameter parameter, Vector? value)
        {
            parameter.Value = value;
        }

        public override Vector Parse(object value)
        {
            return (Vector)value;
        }
    }
}