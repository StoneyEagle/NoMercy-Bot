using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Services.Emotes;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Services.Twitch;

public static class TwitchServiceExtensions
{
    public static void AddTwitchServices(this IServiceCollection services)
    {
        services.AddSingleton<TwitchAuthService>();
        services.AddSingleton<BotAuthService>();
        services.AddSingleton<TwitchApiService>();
        services.AddSingleton<TwitchChatService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<TwitchAuthService>());
        services.AddTransient<HtmlMetadataService>();
        services.AddTransient<TwitchMessageDecorator>();
        services.AddTransient<TwitchCommandService>();
        services.AddTransient<TwitchRewardService>();
        services.AddTransient<RewardScriptLoader>();

        services.AddSingletonHostedService<TwitchBadgeService>();
    }
}