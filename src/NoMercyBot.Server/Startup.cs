using Asp.Versioning.ApiExplorer;
using NoMercyBot.Database;
using NoMercyBot.Server.AppConfig;
using NoMercyBot.Server.Setup;
using NoMercyBot.Services;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Seeds;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Server;

public class Startup
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly StartupOptions _options;
    private readonly ILogger<Startup> _logger;

    public Startup(IApiVersionDescriptionProvider provider, StartupOptions options, ILogger<Startup> logger)
    {
        _provider = provider;
        _options = options;
        _logger = logger;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        ServiceConfiguration.ConfigureServices(services);
        services.AddSingleton(_options);
        services.AddSingleton<CommandScriptLoader>();
        services.AddSingleton<RewardScriptLoader>();
        services.AddSingleton<RewardChangeScriptLoader>();
    }

    public void Configure(IApplicationBuilder app)
    {
        TokenStore.Initialize(app.ApplicationServices);
        
        List<TaskDelegate> startupTasks =
        [
        ];

        Start.Init(startupTasks).Wait();

        // Ensure the database is created and migrated
        SeedService seedService = app.ApplicationServices.GetRequiredService<SeedService>();
        seedService.StartAsync(CancellationToken.None).Wait();

        // Initialize services
        ServiceResolver serviceResolver = app.ApplicationServices.GetRequiredService<ServiceResolver>();
        serviceResolver.InitializeAllServices().Wait();

        // Refresh all users and their channel information
        UserChannelRefreshService userChannelRefreshService = app.ApplicationServices.GetRequiredService<UserChannelRefreshService>();
        userChannelRefreshService.RefreshAllUsersAndChannelsAsync().Wait();

        // Load user command scripts
        CommandScriptLoader scriptLoader = app.ApplicationServices.GetRequiredService<CommandScriptLoader>();
        scriptLoader.LoadAllAsync().Wait();

        // Load user reward scripts
        RewardScriptLoader rewardScriptLoader = app.ApplicationServices.GetRequiredService<RewardScriptLoader>();
        rewardScriptLoader.LoadAllAsync().Wait();

        // Load reward change handlers
        RewardChangeScriptLoader rewardChangeScriptLoader = app.ApplicationServices.GetRequiredService<RewardChangeScriptLoader>();
        TwitchRewardChangeService rewardChangeService = app.ApplicationServices.GetRequiredService<TwitchRewardChangeService>();
        rewardChangeService.SetScriptLoader(rewardChangeScriptLoader);
        rewardChangeScriptLoader.LoadAllAsync().Wait();

        // Load widget scripts and register handlers
        WidgetScriptLoader widgetScriptLoader = app.ApplicationServices.GetRequiredService<WidgetScriptLoader>();
        widgetScriptLoader.LoadAllAsync().Wait();
        IWidgetConnectionHandlerRegistry handlerRegistry = app.ApplicationServices.GetRequiredService<IWidgetConnectionHandlerRegistry>();
        foreach (var script in widgetScriptLoader.GetAllScripts())
        {
            WidgetScriptContext widgetContext = new()
            {
                DatabaseContext = app.ApplicationServices.GetRequiredService<AppDbContext>(),
                ServiceProvider = app.ApplicationServices,
                WidgetEventService = app.ApplicationServices.GetRequiredService<IWidgetEventService>(),
                TwitchApiService = app.ApplicationServices.GetRequiredService<TwitchApiService>(),
                TwitchChatService = app.ApplicationServices.GetRequiredService<TwitchChatService>()
            };
            WidgetScriptConnectionHandler handler = new(script, widgetContext, app.ApplicationServices.GetRequiredService<ILogger<WidgetScriptConnectionHandler>>());
            handlerRegistry.RegisterScriptHandler(handler);
        }

        // Check if stream is already live (must be after services are initialized)
        LuckyFeatherTimerService luckyFeatherTimerService = app.ApplicationServices.GetRequiredService<LuckyFeatherTimerService>();
        luckyFeatherTimerService.CheckIfStreamIsLiveAsync().Wait();

        ShoutoutQueueService shoutoutQueueService = app.ApplicationServices.GetRequiredService<ShoutoutQueueService>();
        shoutoutQueueService.CheckIfStreamIsLiveAsync().Wait();

        ApplicationConfiguration.ConfigureApp(app, _provider);

        // Handle redirect-based OAuth flows after server is listening
        IHostApplicationLifetime lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                try
                {
                    if (serviceResolver.SpotifyNeedsAuth)
                    {
                        SpotifyAuthService spotifyAuth = app.ApplicationServices.GetRequiredService<SpotifyAuthService>();
                        string spotifyRedirectUrl = spotifyAuth.GetRedirectUrl();
                        await serviceResolver.HandleRedirectAuthFlow("Spotify", spotifyRedirectUrl);
                    }

                    if (serviceResolver.DiscordNeedsAuth)
                    {
                        DiscordAuthService discordAuth = app.ApplicationServices.GetRequiredService<DiscordAuthService>();
                        string discordRedirectUrl = discordAuth.GetRedirectUrl();
                        await serviceResolver.HandleRedirectAuthFlow("Discord", discordRedirectUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during post-startup OAuth flow");
                }
            });
        });

    }
}