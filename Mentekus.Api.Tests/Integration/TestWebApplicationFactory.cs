using Mentekus.Api.Shared.Adapters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mentekus.Api.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; set; } = string.Empty;
    public FakeOllamaAdapter Ollama { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOllamaAdapter));
            if (descriptor != null) services.Remove(descriptor);
            services.AddSingleton<IOllamaAdapter>(Ollama);
        });
    }
}
