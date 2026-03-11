using System.Collections.Specialized;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Dto;
using RestSharp;

namespace NoMercyBot.Services.Discord;

public class DiscordAuthService : IAuthService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _conf;
    private readonly ILogger<DiscordAuthService> _logger;
    private readonly DiscordApiService _api;
    private readonly ResilientApiClient _apiClient;

    public Service Service => DiscordConfig.Service();

    public string ClientId => Service.ClientId ?? throw new InvalidOperationException("Discord ClientId is not set.");

    private string ClientSecret =>
        Service.ClientSecret ?? throw new InvalidOperationException("Discord ClientSecret is not set.");

    private string[] Scopes => Service.Scopes ?? throw new InvalidOperationException("Discord Scopes are not set.");
    public string UserId => Service.UserId ?? throw new InvalidOperationException("Twitch UserId is not set.");
    public string UserName => Service.UserName ?? throw new InvalidOperationException("Twitch UserName is not set.");

    public Dictionary<string, string> AvailableScopes => DiscordConfig.AvailableScopes ??
                                                         throw new InvalidOperationException(
                                                             "Discord Scopes are not set.");

    public DiscordAuthService(IServiceScopeFactory serviceScopeFactory, IConfiguration conf,
        ILogger<DiscordAuthService> logger, DiscordApiService api, ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _conf = conf;
        _logger = logger;
        _api = api;
        _apiClient = apiClientFactory.GetClient(DiscordConfig.ApiUrl);
    }

    public string GetRedirectUrl()
    {
        NameValueCollection query = HttpUtility.ParseQueryString(string.Empty);
        query.Add("response_type", "code");
        query.Add("client_id", DiscordConfig.Service().ClientId);
        query.Add("redirect_uri", DiscordConfig.RedirectUri);
        query.Add("scope", string.Join(' ', DiscordConfig.Service().Scopes));

        UriBuilder uriBuilder = new("https://discord.com/oauth2/authorize")
        {
            Query = query.ToString()
        };

        return uriBuilder.ToString();
    }

    public async Task<(User, TokenResponse)> Callback(string code)
    {
        RestRequest request = new("oauth2/token", Method.Post);
        request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        request.AddParameter("client_id", DiscordConfig.Service().ClientId);
        request.AddParameter("client_secret", DiscordConfig.Service().ClientSecret);
        request.AddParameter("grant_type", "authorization_code");
        request.AddParameter("code", code);
        request.AddParameter("redirect_uri", DiscordConfig.RedirectUri);

        RestResponse response = await _apiClient.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch token from Discord.");

        TokenResponse? tokenResponse = response.Content.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Discord.");

        await StoreTokens(tokenResponse, new()
        {
            Id = "",
            Username = ""
        });

        return (new(), tokenResponse);
    }

    public Task<(User, TokenResponse)> ValidateToken(HttpRequest request)
    {
        string authorizationHeader = request.Headers["Authorization"].First() ?? throw new InvalidOperationException();
        string accessToken = authorizationHeader["Bearer ".Length..];

        return ValidateToken(accessToken);
    }

    public async Task<(User, TokenResponse)> ValidateToken(string accessToken)
    {
        RestRequest request = new("users/@me");
        request.AddHeader("Authorization", $"Bearer {accessToken}");

        RestResponse response = await _apiClient.ExecuteAsync(request);

        if (!response.IsSuccessful)
            throw new("Invalid access token");

        // Discord doesn't have a dedicated validate endpoint, so we just check if we can access the user's info

        return (new(), new()
        {
            AccessToken = accessToken,
            RefreshToken = Service.RefreshToken,
            ExpiresIn = (int)(Service.TokenExpiry - DateTime.UtcNow).Value.TotalSeconds
        });
    }

    public async Task<(User, TokenResponse)> RefreshToken(string refreshToken)
    {
        RestRequest request = new("oauth2/token", Method.Post);
        request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        request.AddParameter("client_id", DiscordConfig.Service().ClientId);
        request.AddParameter("client_secret", DiscordConfig.Service().ClientSecret);
        request.AddParameter("grant_type", "refresh_token");
        request.AddParameter("refresh_token", refreshToken);

        RestResponse response = await _apiClient.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to refresh token from Discord.");

        TokenResponse? tokenResponse = response.Content.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Discord.");

        return (new(), tokenResponse);
    }

    public async Task RevokeToken(string accessToken)
    {
        RestRequest request = new("oauth2/token/revoke", Method.Post);
        request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        request.AddParameter("client_id", DiscordConfig.Service().ClientId);
        request.AddParameter("client_secret", DiscordConfig.Service().ClientSecret);
        request.AddParameter("token", accessToken);
        request.AddParameter("token_type_hint", "access_token");

        RestResponse response = await _apiClient.ExecuteAsync(request);

        if (!response.IsSuccessful)
            throw new("Failed to revoke token from Discord.");
    }

    public Task<DeviceCodeResponse> Authorize(string[]? scopes = null)
    {
        throw new NotImplementedException("Discord uses Authorization Code Flow. Use GetRedirectUrl() instead.");
    }

    public Task<TokenResponse> PollForToken(string deviceCode)
    {
        throw new NotImplementedException("Discord uses Authorization Code Flow. Use Callback() instead.");
    }

    public async Task StoreTokens(TokenResponse tokenResponse, User user)
    {
        Service updateService = new()
        {
            Name = Service.Name,
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            UserId = user.Id,
            UserName = user.Username
        };

        AppDbContext dbContext = new();
        await dbContext.Services.Upsert(updateService)
            .On(u => u.Name)
            .WhenMatched((oldUser, newUser) => new()
            {
                AccessToken = newUser.AccessToken,
                RefreshToken = newUser.RefreshToken,
                TokenExpiry = newUser.TokenExpiry,
                UserId = newUser.UserId,
                UserName = newUser.UserName,
                UpdatedAt = DateTime.UtcNow
            })
            .RunAsync();

        Service.AccessToken = updateService.AccessToken;
        Service.RefreshToken = updateService.RefreshToken;
        Service.TokenExpiry = updateService.TokenExpiry;
        Service.UserId = updateService.UserId;
        Service.UserName = updateService.UserName;
    }

    public Task<bool> ConfigureService(ProviderConfigRequest config)
    {
        throw new NotImplementedException();
    }
}
