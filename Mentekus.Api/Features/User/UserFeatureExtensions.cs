namespace Mentekus.Api.Features.User;

public static class UserFeatureExtensions
{
    public static IServiceCollection AddUserFeature(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}