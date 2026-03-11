using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using RestSharp;
using Newtonsoft.Json;
using Microsoft.Extensions.Hosting;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Services.Emotes.Dto;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Twitch;

namespace NoMercyBot.Services.Emotes;

public class TwitchBadgeService : IHostedService
{
    private readonly ResilientApiClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TwitchBadgeService> _logger;
    private readonly TwitchAuthService _twitchAuthService;
    private const int MaxCredentialWaitAttempts = 60;
    private const int CredentialWaitDelayMs = 5000;
    public List<ChatBadge> TwitchBadges { get; private set; } = [];

    public TwitchBadgeService(IServiceScopeFactory serviceScopeFactory, ILogger<TwitchBadgeService> logger,
        TwitchAuthService twitchAuthService, ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
        _client = apiClientFactory.GetClient("https://api.twitch.tv/helix/chat/badges");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Twitch badge service initialization");
        // Run initialization in background to not block startup
        _ = Task.Run(async () =>
        {
            try
            {
                await Initialize(cancellationToken);
                _logger.LogInformation("Twitch badge service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Twitch badge service, but continuing startup");
            }
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task Initialize(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waiting for Twitch credentials before fetching badges...");

        // Wait for credentials to be available
        for (int attempt = 0; attempt < MaxCredentialWaitAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                var service = _twitchAuthService.Service;
                if (!string.IsNullOrEmpty(service?.AccessToken) &&
                    !string.IsNullOrEmpty(service?.ClientId) &&
                    !string.IsNullOrEmpty(service?.UserId))
                {
                    _logger.LogInformation("Twitch credentials available, fetching badges...");
                    break;
                }
            }
            catch
            {
                // Service not ready yet
            }

            if (attempt > 0 && attempt % 12 == 0)
            {
                _logger.LogInformation("Still waiting for Twitch credentials... ({Attempt}/{Max})",
                    attempt, MaxCredentialWaitAttempts);
            }

            await Task.Delay(CredentialWaitDelayMs, cancellationToken);

            // Reload from database
            await ReloadCredentials();
        }

        // Final check
        try
        {
            var service = _twitchAuthService.Service;
            if (string.IsNullOrEmpty(service?.AccessToken) || string.IsNullOrEmpty(service?.ClientId))
            {
                _logger.LogWarning("Twitch credentials not available after waiting. Badge service will not load badges.");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify Twitch credentials. Badge service will not load badges.");
            return;
        }

        _logger.LogInformation("Initializing Twitch badges cache...");
        try
        {
            await GetGlobalBadges();
            await GetChannelBadges(_twitchAuthService.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Twitch badges");
        }
    }

    private async Task ReloadCredentials()
    {
        try
        {
            var service = await _dbContext.Services
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == "Twitch");

            if (service != null)
            {
                TwitchConfig._service = service;
            }
        }
        catch
        {
            // Ignore errors during reload
        }
    }

    private async Task GetGlobalBadges()
    {
        try
        {
            _logger.LogInformation("Fetching global Twitch badges");

            RestRequest request = new("/global");
            request.AddHeader("Authorization", $"Bearer {_twitchAuthService.Service.AccessToken}");
            request.AddHeader("Client-Id", _twitchAuthService.ClientId);

            RestResponse response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content == null)
                throw new("Failed to fetch global Twitch badges");

            TwitchGlobalBadgesResponse? result =
                JsonConvert.DeserializeObject<TwitchGlobalBadgesResponse>(response.Content);
            if (result?.Data == null)
                throw new("No global Twitch badges found");

            foreach (TwitchGlobalBadgesResponseData badge in result.Data)
            foreach (TwitchGlobalBadgesVersion version in badge.Versions)
                TwitchBadges.Add(new()
                {
                    SetId = badge.SetId,
                    Id = version.Id,
                    Info = version.Description,
                    Urls = new()
                    {
                        { "1", version.ImageUrl1X },
                        { "2", version.ImageUrl2X },
                        { "4", version.ImageUrl4X }
                    }
                });

            _logger.LogInformation($"Loaded {TwitchBadges.Count} global Twitch badges");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching global Twitch badges");
        }
    }

    private async Task GetChannelBadges(string broadcasterId)
    {
        try
        {
            RestRequest request = new($"?broadcaster_id={broadcasterId}");
            request.AddHeader("Authorization", $"Bearer {_twitchAuthService.Service.AccessToken}");
            request.AddHeader("Client-Id", _twitchAuthService.ClientId);

            RestResponse response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content == null)
                return;

            TwitchGlobalBadgesResponse? channelBadges =
                JsonConvert.DeserializeObject<TwitchGlobalBadgesResponse>(response.Content);
            if (channelBadges != null)
            {
                foreach (TwitchGlobalBadgesResponseData badge in channelBadges.Data)
                foreach (TwitchGlobalBadgesVersion version in badge.Versions)
                    TwitchBadges.Add(new()
                    {
                        SetId = badge.SetId,
                        Id = version.Id,
                        Info = version.Title,
                        Urls = new()
                        {
                            { "1", version.ImageUrl1X },
                            { "2", version.ImageUrl2X },
                            { "4", version.ImageUrl4X }
                        }
                    });

                _logger.LogInformation($"Loaded {channelBadges.Data.Length} channel Twitch badges for {broadcasterId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading channel Twitch badges for {broadcasterId}: {ex.Message}");
        }
    }
}
