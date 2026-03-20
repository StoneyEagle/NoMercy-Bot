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

public class SevenTvService : IHostedService
{
    private readonly ResilientApiClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SevenTvService> _logger;
    private readonly TwitchAuthService _twitchAuthService;
    public List<SevenTvEmote> SevenTvEmotes { get; private set; } = [];

    public SevenTvService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SevenTvService> logger,
        TwitchAuthService twitchAuthService,
        ResilientApiClientFactory apiClientFactory
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
        _client = apiClientFactory.GetClient("https://7tv.io/v3/");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting 7TV emote service initialization");

        // Load from cache immediately if available, then refresh in background
        LoadFromCacheIfAvailable();

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Initialize();
                    _logger.LogInformation("7TV emote service initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error initializing 7TV emote service, but continuing with cached data"
                    );
                }
            },
            cancellationToken
        );

        return Task.CompletedTask;
    }

    private void LoadFromCacheIfAvailable()
    {
        var cachedGlobal = EmoteCacheHelper.Load<List<SevenTvEmote>>("7tv_global_emotes", _logger);
        if (cachedGlobal is { Count: > 0 })
        {
            SevenTvEmotes.AddRange(cachedGlobal);
            _logger.LogInformation(
                "7TV: Loaded {Count} global emotes from cache",
                cachedGlobal.Count
            );
        }

        string channelKey = $"7tv_channel_emotes_{_twitchAuthService.UserId}";
        var cachedChannel = EmoteCacheHelper.Load<List<SevenTvEmote>>(channelKey, _logger);
        if (cachedChannel is { Count: > 0 })
        {
            SevenTvEmotes.AddRange(cachedChannel);
            _logger.LogInformation(
                "7TV: Loaded {Count} channel emotes from cache",
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
            "7tv_global_emotes",
            FetchGlobalEmotes,
            _logger
        );

        var channelEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            $"7tv_channel_emotes_{_twitchAuthService.UserId}",
            () => FetchChannelEmotes(_twitchAuthService.UserId),
            _logger
        );

        // Replace cached data with fresh data
        List<SevenTvEmote> fresh = new(globalEmotes.Count + channelEmotes.Count);
        fresh.AddRange(globalEmotes);
        fresh.AddRange(channelEmotes);
        SevenTvEmotes = fresh;

        _logger.LogInformation(
            "7TV: Refreshed {Global} global + {Channel} channel emotes",
            globalEmotes.Count,
            channelEmotes.Count
        );
    }

    private async Task<List<SevenTvEmote>> FetchGlobalEmotes()
    {
        RestRequest request = new("emote-sets/global");
        RestResponse response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content == null)
            throw new("Failed to fetch global 7TV emotes");

        SevenTvGlobalResponse? obj = JsonConvert.DeserializeObject<SevenTvGlobalResponse>(
            response.Content
        );
        return obj?.Emotes?.ToList() ?? [];
    }

    private async Task<List<SevenTvEmote>> FetchChannelEmotes(string broadcasterId)
    {
        RestRequest request = new($"users/twitch/{broadcasterId}");
        RestResponse response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content == null)
            return [];

        SevenTvChannelEmotesResponse? obj =
            JsonConvert.DeserializeObject<SevenTvChannelEmotesResponse>(response.Content);

        if (obj?.EmoteSet is not null)
            return obj.EmoteSet.Emotes.ToList();

        return [];
    }
}
