using Microsoft.Extensions.Options;

namespace Mentekus.Api.Shared.Adapters;

public static class AdapterExtensions
{
    public static IServiceCollection AddAdapters(this IServiceCollection services)
    {
        services.AddOptions<OllamaOptions>()
            .BindConfiguration(OllamaOptions.SectionName)
            .ValidateOnStart();

        services.AddHttpClient<IOllamaAdapter, OllamaAdapter>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException("Configuration value 'Ollama:BaseUrl' is required.");
            if (string.IsNullOrWhiteSpace(options.KeepAlive))
                throw new InvalidOperationException("Configuration value 'Ollama:KeepAlive' is required.");
            if (options.TimeoutSeconds <= 0)
                throw new InvalidOperationException("Configuration value 'Ollama:TimeoutSeconds' must be positive.");

            httpClient.BaseAddress = new Uri(options.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}