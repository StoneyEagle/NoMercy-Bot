using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Obs;
using NoMercyBot.Services.Twitch;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services;

public static class EventSubServiceCollectionExtensions
{
    public static IServiceCollection AddEventSubServices(this IServiceCollection services)
    {
        // Register the EventSub websocket client
        services.AddSingleton<EventSubWebsocketClient>();

        // Register the TwitchApiService as singleton to avoid duplicate instances
        services.AddSingleton<TwitchApiService>();

        // Register the EventSub services as singletons to ensure events work properly
        services.AddSingleton<TwitchEventSubService>();
        services.AddScoped<DiscordEventSubService>();
        services.AddScoped<ObsEventSubService>();

        // Register the TwitchWebsocketHostedService as a hosted service
        // This needs to be after TwitchEventSubService to ensure proper dependency resolution
        services.AddHostedService<TwitchWebsocketHostedService>();

        // Register the provider-specific services to the IEventSubService interface
        services.AddSingleton<IEventSubService>(sp =>
            sp.GetRequiredService<TwitchEventSubService>()
        );
        services.AddScoped<IEventSubService, DiscordEventSubService>(sp =>
            sp.GetRequiredService<DiscordEventSubService>()
        );
        services.AddScoped<IEventSubService, ObsEventSubService>(sp =>
            sp.GetRequiredService<ObsEventSubService>()
        );

        return services;
    }
}
