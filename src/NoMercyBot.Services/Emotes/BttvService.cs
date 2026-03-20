using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Services.Emotes.Dto;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Twitch;
using RestSharp;

namespace NoMercyBot.Services.Emotes;

public class BttvService : IHostedService
{
    private readonly ResilientApiClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BttvService> _logger;
    private readonly TwitchAuthService _twitchAuthService;
    public List<BttvEmote> BttvEmotes { get; private set; } = [];

    public BttvService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BttvService> logger,
        TwitchAuthService twitchAuthService,
        ResilientApiClientFactory apiClientFactory
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
        _client = apiClientFactory.GetClient("https://api.betterttv.net/3/");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting BTTV emote service initialization");

        // Load from cache immediately if available, then refresh in background
        LoadFromCacheIfAvailable();

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Initialize();
                    _logger.LogInformation("BTTV emote service initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error initializing BTTV emote service, but continuing with cached data"
                    );
                }
            },
            cancellationToken
        );

        return Task.CompletedTask;
    }

    private void LoadFromCacheIfAvailable()
    {
        var cachedGlobal = EmoteCacheHelper.Load<List<BttvEmote>>("bttv_global_emotes", _logger);
        if (cachedGlobal is { Count: > 0 })
        {
            BttvEmotes.AddRange(cachedGlobal);
            _logger.LogInformation(
                "BTTV: Loaded {Count} global emotes from cache",
                cachedGlobal.Count
            );
        }

        string channelKey = $"bttv_channel_emotes_{_twitchAuthService.UserId}";
        var cachedChannel = EmoteCacheHelper.Load<List<BttvEmote>>(channelKey, _logger);
        if (cachedChannel is { Count: > 0 })
        {
            BttvEmotes.AddRange(cachedChannel);
            _logger.LogInformation(
                "BTTV: Loaded {Count} channel emotes from cache",
                cachedChannel.Count
            );
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task Initialize()
    {
        var globalEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            "bttv_global_emotes",
            FetchGlobalEmotes,
            _logger
        );

        var channelEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            $"bttv_channel_emotes_{_twitchAuthService.UserId}",
            () => FetchChannelEmotes(_twitchAuthService.UserId),
            _logger
        );

        // Replace cached data with fresh data
        List<BttvEmote> fresh = new(globalEmotes.Count + channelEmotes.Count);
        fresh.AddRange(globalEmotes);
        fresh.AddRange(channelEmotes);
        BttvEmotes = fresh;

        _logger.LogInformation(
            "BTTV: Refreshed {Global} global + {Channel} channel emotes",
            globalEmotes.Count,
            channelEmotes.Count
        );
    }

    private async Task<List<BttvEmote>> FetchGlobalEmotes()
    {
        RestRequest request = new("cached/emotes/global");
        RestResponse response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content == null)
            throw new("Failed to fetch global BTTV emotes");

        return JsonConvert.DeserializeObject<List<BttvEmote>>(response.Content) ?? [];
    }

    private async Task<List<BttvEmote>> FetchChannelEmotes(string broadcasterId)
    {
        RestRequest request = new($"cached/users/twitch/{broadcasterId}");
        RestResponse response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content == null)
            return [];

        ChannelBttvEmotesResponse? result =
            JsonConvert.DeserializeObject<ChannelBttvEmotesResponse>(response.Content);

        if (result == null)
            return [];

        List<BttvEmote> emotes = [];
        emotes.AddRange(result.ChannelEmotes);
        emotes.AddRange(result.SharedEmotes);
        return emotes;
    }
}
