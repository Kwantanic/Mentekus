using Mentekus.Api.Shared.Adapters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Mentekus.Api.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; set; } = string.Empty;
    public Mock<IOllamaAdapter> OllamaAdapterMock { get; } = new();

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
            // Remove the real adapter and add the mock
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOllamaAdapter));
            if (descriptor != null) services.Remove(descriptor);
            services.AddSingleton(OllamaAdapterMock.Object);
        });
    }
}
