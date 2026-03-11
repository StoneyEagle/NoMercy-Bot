using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Spotify;
using Newtonsoft.Json;
using RestSharp;

namespace NoMercyBot.Services.Discord;

public class DiscordApiService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _conf;
    private readonly ILogger<DiscordApiService> _logger;
    private readonly ResilientApiClient _apiClient;

    private Service Service => DiscordConfig.Service();

    public string ClientId => Service.ClientId ?? throw new InvalidOperationException("Discord ClientId is not set.");

    public DiscordApiService(
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration conf,
        ILogger<DiscordApiService> logger,
        ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _conf = conf;
        _logger = logger;
        _apiClient = apiClientFactory.GetClient(DiscordConfig.ApiUrl);
    }

    public async Task<string?> GetSpotifyToken()
    {
        if (string.IsNullOrWhiteSpace(DiscordConfig.SessionToken))
        {
            _logger.LogWarning("Discord session token is not set. Cannot retrieve Spotify token.");
            return null;
        }

        try
        {
            RestRequest request = new($"users/@me/connections/spotify/{SpotifyConfig.Service().UserId}/access-token");
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", DiscordConfig.SessionToken);

            RestResponse response = await _apiClient.ExecuteAsync(request);

            if (!response.IsSuccessful || response.Content is null)
                throw new(response.Content ?? $"Error: {response.StatusCode}");

            dynamic? data = JsonConvert.DeserializeObject(response.Content);
            return data?.access_token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Spotify token for user {SpotifyUserId}",
                SpotifyConfig.Service().UserName);
            return null;
        }
    }
}
