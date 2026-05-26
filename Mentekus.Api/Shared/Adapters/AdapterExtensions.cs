using Microsoft.Extensions.Options;

namespace Mentekus.Api.Shared.Adapters;

public static class AdapterExtensions
{
    public static IServiceCollection AddAdapters(this IServiceCollection services)
    {
        services.AddOptions<OllamaOptions>()
            .BindConfiguration(OllamaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IOllamaAdapter, OllamaAdapter>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OllamaOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException("Configuration value 'Ollama:BaseUrl' is required.");

            httpClient.BaseAddress = new Uri(options.BaseUrl);
        });

        return services;
    }
}