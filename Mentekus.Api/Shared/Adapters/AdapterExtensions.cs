using Mentekus.Api.Shared.Adapters;

namespace Microsoft.Extensions.DependencyInjection;

public static class AdapterExtensions
{
    public static IServiceCollection AddAdapters(this IServiceCollection services)
    {
        services.AddHttpClient<IOllamaAdapter, OllamaAdapter>((serviceProvider, httpClient) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            var baseUrl = configuration["Ollama:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Configuration value 'Ollama:BaseUrl' is required.");

            httpClient.BaseAddress = new Uri(baseUrl);
        });

        return services;
    }
}
