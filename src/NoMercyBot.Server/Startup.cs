using Asp.Versioning.ApiExplorer;
using NoMercyBot.Database;
using NoMercyBot.Server.AppConfig;
using NoMercyBot.Server.Setup;
using NoMercyBot.Services;
using NoMercyBot.Services.Seeds;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;

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

        ApplicationConfiguration.ConfigureApp(app, _provider);

    }
}