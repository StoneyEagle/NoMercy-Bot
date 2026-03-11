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
        try
        {
            await GetGlobalEmotes();
            await GetChannelEmotes(_twitchAuthService.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get global BTTV emotes");
        }
    }

    private async Task GetGlobalEmotes()
    {
        try
        {
            _logger.LogInformation("Fetching global BTTV emotes");

            RestRequest request = new("cached/emotes/global");
            RestResponse response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content == null)
                throw new("Failed to fetch global BTTV emotes");

            BttvEmotes = JsonConvert.DeserializeObject<List<BttvEmote>>(response.Content) ?? [];

            _logger.LogInformation($"Loaded {BttvEmotes.Count} global BTTV emotes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching global BTTV emotes");
        }
    }

    private async Task GetChannelEmotes(string broadcasterId)
    {
        try
        {
            RestRequest request = new($"cached/users/twitch/{broadcasterId}");
            RestResponse response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content == null)
                return;

            ChannelBttvEmotesResponse? result =
                JsonConvert.DeserializeObject<ChannelBttvEmotesResponse>(response.Content);
            if (result != null)
            {
                BttvEmotes.AddRange(result.ChannelEmotes);
                BttvEmotes.AddRange(result.SharedEmotes);

                _logger.LogInformation(
                    $"Loaded {result.ChannelEmotes.Length + result.SharedEmotes.Length} channel BTTV emotes for {broadcasterId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading channel BTTV emotes for {broadcasterId}: {ex.Message}");
        }
    }
}
