using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Obs;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Seeds;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Emotes;
using NoMercyBot.Services.Widgets;
using NoMercyBot.Services.TTS.Interfaces;
using NoMercyBot.Services.TTS.Services;
using NoMercyBot.Services.TTS.Providers;
using Microsoft.Extensions.Hosting;

namespace NoMercyBot.Services;

public static class ServiceCollectionExtensions
{
    public static void AddBotServices(this IServiceCollection services)
    {
        services.AddSingleton<ResilientApiClientFactory>();
        services.AddSingleton<SeedService>();

        services.AddTokenRefreshService();

        services.AddWidgetServices();
        services.AddTwitchServices();
        services.AddSpotifyServices();
        services.AddDiscordServices();
        services.AddObsServices();
        services.AddOtherServices();
        services.AddEmoteServices();
        services.AddTtsServices();
    }

    private static void AddOtherServices(this IServiceCollection services)
    {
        services.AddSingleton<PronounService>();
        services.AddSingleton<PermissionService>();
        services.AddHostedService<GracefulShutdownService>();
        services.AddSingleton<LocalAudioPlaybackService>(); // Add local audio playback service

        services.AddSingleton<TtsService>();
        
        services.AddSingleton<TwitchApiService>();
        services.AddSingleton<LuckyFeatherTimerService>();
        services.AddHostedService(sp => sp.GetRequiredService<LuckyFeatherTimerService>());
    }

    private static void AddTtsServices(this IServiceCollection services)
    {
        // Core TTS services
        services.AddSingleton<ITtsUsageService, TtsUsageService>();
        services.AddSingleton<ITtsProviderService, TtsProviderService>();
        services.AddSingleton<TtsCacheService>();
        
        // TTS Providers
        services.AddSingleton<ITtsProvider, AzureTtsProvider>();
        services.AddSingleton<ITtsProvider, LegacyTtsProvider>();

        // Provider initialization service
        services.AddSingleton<TtsProviderInitializationService>();

        // Cache cleanup service
        services.AddHostedService<TtsCacheCleanupService>();
    }

    private static void AddEmoteServices(this IServiceCollection services)
    {
        services.AddSingletonHostedService<BttvService>();
        services.AddSingletonHostedService<FrankerFacezService>();
        services.AddSingletonHostedService<SevenTvService>();
    }

    private static void AddWidgetServices(this IServiceCollection services)
    {
        services.AddSingleton<IWidgetEventService, WidgetEventService>();
        services.AddSingleton<IWidgetScaffoldService, WidgetScaffoldService>();
        services.AddTransient<WidgetEventService>();

        // Widget connection handlers and script loader
        services.AddSingleton<IWidgetConnectionHandlerRegistry, WidgetConnectionHandlerRegistry>();
        services.AddSingleton<WidgetScriptLoader>();

        services.AddSignalR();
    }

    private static void AddTokenRefreshService(this IServiceCollection services)
    {
        services.AddHostedService<TokenRefreshService>();
        services.AddHostedService<SpotifyWebsocketService>();
    }

    // Extension method to add a service as both a singleton and a hosted service
    internal static IServiceCollection AddSingletonHostedService<TService>(this IServiceCollection services)
        where TService : class, IHostedService
    {
        services.AddSingleton<TService>();
        services.AddHostedService(provider => provider.GetRequiredService<TService>());
        return services;
    }
}