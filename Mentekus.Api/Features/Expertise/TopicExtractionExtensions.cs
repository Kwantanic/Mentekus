namespace Mentekus.Api.Features.Expertise;

public static class TopicExtractionExtensions
{
    public static IServiceCollection AddTopicExtraction(this IServiceCollection services)
    {
        services.AddSingleton<TopicExtractionQueue>();
        services.AddSingleton<ITopicExtractionQueue>(serviceProvider => serviceProvider.GetRequiredService<TopicExtractionQueue>());
        services.AddHostedService<TopicExtractionWorker>();
        return services;
    }
}
