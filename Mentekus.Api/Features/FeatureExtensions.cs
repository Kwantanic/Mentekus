using Mentekus.Api.Features.Question;
using Mentekus.Api.Features.User;

namespace Microsoft.Extensions.DependencyInjection;

public static class FeatureExtensions
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddUserFeature();
        services.AddQuestionFeature();

        return services;
    }

    private static IServiceCollection AddQuestionFeature(this IServiceCollection services)
    {
        services.AddScoped<IQuestionService, QuestionService>();

        return services;
    }
}