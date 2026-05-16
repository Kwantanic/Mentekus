using Mentekus.Api.Features.Question;

namespace Mentekus.Api.Features;

public static class FeatureExtensions
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddScoped<IQuestionService, QuestionService>();

        return services;
    }
}