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

public class FrankerFacezService : IHostedService
{
    private readonly ResilientApiClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<FrankerFacezService> _logger;
    private readonly TwitchAuthService _twitchAuthService;
    public List<Emoticon> FrankerFacezEmotes { get; private set; } = [];

    public FrankerFacezService(IServiceScopeFactory serviceScopeFactory, ILogger<FrankerFacezService> logger,
        TwitchAuthService twitchAuthService, ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
        _client = apiClientFactory.GetClient("https://api.frankerfacez.com/v1/");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting FrankerFacez emote service initialization");
        try
        {
            // Run initialization in background so it doesn't block startup
            _ = Task.Run(async () =>
            {
                try
                {
                    await Initialize();
                    _logger.LogInformation("FrankerFacez emote service initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize FrankerFacez emotes, but continuing startup");
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting FrankerFacez emote service, but continuing startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task Initialize()
    {
        _logger.LogInformation("Initializing FrankerFacez emotes cache...");

        var globalEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            "ffz_global_emotes",
            FetchGlobalEmotes,
            _logger);
        FrankerFacezEmotes.AddRange(globalEmotes);
        _logger.LogInformation("Loaded {Count} global FFZ emotes", globalEmotes.Count);

        var channelEmotes = await EmoteCacheHelper.FetchWithRetryAndCache(
            $"ffz_channel_emotes_{_twitchAuthService.UserName}",
            () => FetchChannelEmotes(_twitchAuthService.UserName),
            _logger);
        FrankerFacezEmotes.AddRange(channelEmotes);
        _logger.LogInformation("Loaded {Count} channel FFZ emotes", channelEmotes.Count);
    }

    private async Task<List<Emoticon>> FetchGlobalEmotes()
    {
        RestRequest request = new("set/global");
        RestResponse response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content == null)
            throw new("Failed to fetch global FFZ emotes");

        FrankerFacezResponse? obj = JsonConvert.DeserializeObject<FrankerFacezResponse>(response.Content);
        List<Emoticon> emotes = [];

        foreach (int setId in obj?.DefaultSets ?? [])
            if (obj?.Sets.TryGetValue(setId.ToString(), out FrankerFacezSet? set) ?? false)
                foreach (Emoticon emote in set.Emoticons)
                    emotes.Add(emote);

        return emotes;
    }

    private async Task<List<Emoticon>> FetchChannelEmotes(string channelName)
    {
        RestRequest request = new($"room/{channelName}");
        RestResponse response = await _client.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content == null)
            return [];

        FrankerFacezResponse? frankerFacezResponse =
            JsonConvert.DeserializeObject<FrankerFacezResponse>(response.Content);

        List<Emoticon> emotes = [];
        if (frankerFacezResponse?.Sets != null)
            foreach (FrankerFacezSet set in frankerFacezResponse.Sets.Values)
            foreach (Emoticon emote in set.Emoticons)
                emotes.Add(emote);

        return emotes;
    }
}
