using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Services.Emotes.Dto;
using NoMercyBot.Services.Http;
using RestSharp;
using Microsoft.Extensions.Hosting;
using NoMercyBot.Services.Twitch;

namespace NoMercyBot.Services.Emotes;

public class SevenTvService : IHostedService
{
    private readonly ResilientApiClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SevenTvService> _logger;
    private readonly TwitchAuthService _twitchAuthService;
    public List<SevenTvEmote> SevenTvEmotes { get; private set; } = [];

    public SevenTvService(IServiceScopeFactory serviceScopeFactory, ILogger<SevenTvService> logger,
        TwitchAuthService twitchAuthService, ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
        _client = apiClientFactory.GetClient("https://7tv.io/v3/");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting 7TV emote service initialization");
        try
        {
            await Initialize();
            _logger.LogInformation("7TV emote service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting 7TV emote service, but continuing startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task Initialize()
    {
        _logger.LogInformation("Initializing 7TV emotes cache...");

        var globalEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            "7tv_global_emotes",
            FetchGlobalEmotes,
            _logger);
        SevenTvEmotes.AddRange(globalEmotes);
        _logger.LogInformation("Loaded {Count} global 7TV emotes", globalEmotes.Count);

        var channelEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            $"7tv_channel_emotes_{_twitchAuthService.UserId}",
            () => FetchChannelEmotes(_twitchAuthService.UserId),
            _logger);
        SevenTvEmotes.AddRange(channelEmotes);
        _logger.LogInformation("Loaded {Count} channel 7TV emotes", channelEmotes.Count);
    }

    private async Task<List<SevenTvEmote>> FetchGlobalEmotes()
    {
        RestRequest request = new("emote-sets/global");
        RestResponse response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content == null)
            throw new("Failed to fetch global 7TV emotes");

        SevenTvGlobalResponse? obj = JsonConvert.DeserializeObject<SevenTvGlobalResponse>(response.Content);
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
