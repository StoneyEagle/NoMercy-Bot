using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Obs;

public static class ObsServiceExtensions
{
    public static IServiceCollection AddObsServices(this IServiceCollection services)
    {
        services.AddSingleton<ObsApiService>();
        services.AddSingleton<ObsAuthService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<ObsAuthService>());

        return services;
    }
}
