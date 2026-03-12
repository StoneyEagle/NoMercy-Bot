using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using RestSharp;
using Newtonsoft.Json;
using NoMercyBot.Services.Emotes.Dto;
using NoMercyBot.Services.Http;
using Microsoft.Extensions.Hosting;
using NoMercyBot.Services.Twitch;

namespace NoMercyBot.Services.Emotes;

public class BttvService : IHostedService
{
    private readonly ResilientApiClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BttvService> _logger;
    private readonly TwitchAuthService _twitchAuthService;
    public List<BttvEmote> BttvEmotes { get; private set; } = [];

    public BttvService(IServiceScopeFactory serviceScopeFactory, ILogger<BttvService> logger,
        TwitchAuthService twitchAuthService, ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
        _client = apiClientFactory.GetClient("https://api.betterttv.net/3/");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting BTTV emote service initialization");
        try
        {
            await Initialize();
            _logger.LogInformation("BTTV emote service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting BTTV emote service, but continuing startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task Initialize()
    {
        _logger.LogInformation("Initializing BTTV emotes cache...");

        var globalEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            "bttv_global_emotes",
            FetchGlobalEmotes,
            _logger);
        BttvEmotes.AddRange(globalEmotes);
        _logger.LogInformation("Loaded {Count} global BTTV emotes", globalEmotes.Count);

        var channelEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            $"bttv_channel_emotes_{_twitchAuthService.UserId}",
            () => FetchChannelEmotes(_twitchAuthService.UserId),
            _logger);
        BttvEmotes.AddRange(channelEmotes);
        _logger.LogInformation("Loaded {Count} channel BTTV emotes", channelEmotes.Count);
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

        if (result == null) return [];

        List<BttvEmote> emotes = [];
        emotes.AddRange(result.ChannelEmotes);
        emotes.AddRange(result.SharedEmotes);
        return emotes;
    }
}
