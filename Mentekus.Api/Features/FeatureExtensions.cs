using Mentekus.Api.Features.Question;

namespace Microsoft.Extensions.DependencyInjection;

public static class FeatureExtensions
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddQuestionFeature();

        return services;
    }

    private static IServiceCollection AddQuestionFeature(this IServiceCollection services)
    {
        services.AddScoped<IQuestionService, QuestionService>();

        return services;
    }
}