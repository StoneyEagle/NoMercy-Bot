using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Discord;

public static class DiscordServiceExtensions
{
    public static IServiceCollection AddDiscordServices(this IServiceCollection services)
    {
        services.AddSingleton<DiscordApiService>();
        services.AddSingleton<DiscordAuthService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<DiscordAuthService>());

        return services;
    }
}
