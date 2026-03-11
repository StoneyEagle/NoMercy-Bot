using System.Collections.Specialized;
using System.Text;
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
using NoMercyBot.Services.Spotify.Dto;
using NoMercyBot.Services.Twitch.Dto;
using RestSharp;

namespace NoMercyBot.Services.Spotify;

public class SpotifyAuthService : IAuthService
{
    private readonly IConfiguration _conf;
    private readonly ILogger<SpotifyAuthService> _logger;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly SpotifyApiService _spotifyApiService;
    private readonly ResilientApiClient _authClient;

    public Service Service => SpotifyConfig.Service();

    public string ClientId => Service.ClientId ?? throw new InvalidOperationException("Spotify ClientId is not set.");

    private string ClientSecret =>
        Service.ClientSecret ?? throw new InvalidOperationException("Spotify ClientSecret is not set.");

    private string[] Scopes => Service.Scopes ?? throw new InvalidOperationException("Spotify Scopes are not set.");
    public string UserId => Service.UserId ?? throw new InvalidOperationException("Spotify UserId is not set.");
    public string UserName => Service.UserName ?? throw new InvalidOperationException("Spotify UserName is not set.");

    public Dictionary<string, string> AvailableScopes => SpotifyConfig.AvailableScopes ??
                                                         throw new InvalidOperationException(
                                                             "Spotify Scopes are not set.");

    public SpotifyAuthService(IServiceScopeFactory serviceScopeFactory, IConfiguration conf,
        ILogger<SpotifyAuthService> logger, SpotifyApiService spotifyApiService, ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _conf = conf;
        _logger = logger;
        _spotifyApiService = spotifyApiService;
        _authClient = apiClientFactory.GetClient(SpotifyConfig.AuthUrl);
    }

    public string GetRedirectUrl()
    {
        NameValueCollection query = HttpUtility.ParseQueryString(string.Empty);
        query.Add("response_type", "code");
        query.Add("client_id", ClientId);
        query.Add("redirect_uri", SpotifyConfig.RedirectUri);
        query.Add("scope", string.Join(' ', Scopes));

        UriBuilder uriBuilder = new("https://accounts.spotify.com/authorize")
        {
            Query = query.ToString()
        };

        return uriBuilder.ToString();
    }

    public async Task<(User, TokenResponse)> Callback(string code)
    {
        RestRequest request = new("token", Method.Post);
        request.AddHeader("Authorization",
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
        request.AddParameter("grant_type", "authorization_code");
        request.AddParameter("code", code);
        request.AddParameter("redirect_uri", SpotifyConfig.RedirectUri);

        RestResponse response = await _authClient.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch token from Spotify.");

        TokenResponse? tokenResponse = response.Content.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Spotify.");

        Service.AccessToken = tokenResponse.AccessToken;
        Service.RefreshToken = tokenResponse.RefreshToken;
        Service.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

        SpotifyMeResponse meResponse = await _spotifyApiService.GetSpotifyMe();
        User user = new()
        {
            Username = meResponse.DisplayName,
            Id = meResponse.Id
        };

        await StoreTokens(tokenResponse, user);

        return (user, tokenResponse);
    }

    public Task<(User, TokenResponse)> ValidateToken(HttpRequest request)
    {
        string authorizationHeader = request.Headers["Authorization"].First() ?? throw new InvalidOperationException();
        string accessToken = authorizationHeader["Bearer ".Length..];

        return ValidateToken(accessToken);
    }

    public async Task<(User, TokenResponse)> ValidateToken(string accessToken)
    {
        SpotifyMeResponse meResponse = await _spotifyApiService.GetSpotifyMe();

        return (new()
        {
            Username = meResponse.DisplayName,
            Id = meResponse.Id
        }, new()
        {
            AccessToken = accessToken,
            RefreshToken = Service.RefreshToken!,
            ExpiresIn = (int)(Service.TokenExpiry - DateTime.UtcNow)!.Value.TotalSeconds
        });
    }

    public async Task<(User, TokenResponse)> RefreshToken(string refreshToken)
    {
        RestRequest request = new("token", Method.Post);
        request.AddHeader("Authorization",
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
        request.AddParameter("grant_type", "refresh_token");
        request.AddParameter("refresh_token", refreshToken);

        RestResponse response = await _authClient.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to refresh token from Spotify.");

        TokenResponse? tokenResponse = response.Content.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Spotify.");

        if (string.IsNullOrEmpty(tokenResponse.RefreshToken))
            tokenResponse.RefreshToken = refreshToken;

        return (new(), tokenResponse);
    }

    public Task RevokeToken(string accessToken)
    {
        // Spotify doesn't have a revoke endpoint - tokens will expire naturally
        // Just return completed task for interface compatibility
        return Task.CompletedTask;
    }

    public Task<DeviceCodeResponse> Authorize(string[]? scopes = null)
    {
        throw new NotImplementedException("Spotify uses Authorization Code Flow. Use GetRedirectUrl() instead.");
    }

    public Task<TokenResponse> PollForToken(string deviceCode)
    {
        throw new NotImplementedException("Spotify uses Authorization Code Flow. Use Callback() instead.");
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

        await _dbContext.Services.Upsert(updateService)
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
