// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeMadeStatic.Global

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

namespace NoMercyBot.Services.Twitch;

public class TwitchAuthService : IAuthService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _conf;
    private readonly ILogger<TwitchAuthService> _logger;
    private readonly TwitchApiService _twitchApiService;
    private readonly ResilientApiClient _authClient;

    public Service Service => TwitchConfig.Service();

    public string ClientId =>
        Service.ClientId ?? throw new InvalidOperationException("Twitch ClientId is not set.");

    private string ClientSecret =>
        Service.ClientSecret
        ?? throw new InvalidOperationException("Twitch ClientSecret is not set.");

    private string[] Scopes =>
        Service.Scopes ?? throw new InvalidOperationException("Twitch Scopes are not set.");
    public string UserId =>
        Service.UserId ?? throw new InvalidOperationException("Twitch UserId is not set.");
    public string UserName =>
        Service.UserName ?? throw new InvalidOperationException("Twitch UserName is not set.");

    public Dictionary<string, string> AvailableScopes =>
        TwitchConfig.AvailableScopes
        ?? throw new InvalidOperationException("Twitch Scopes are not set.");

    public TwitchAuthService(
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration conf,
        ILogger<TwitchAuthService> logger,
        TwitchApiService twitchApiService,
        ResilientApiClientFactory apiClientFactory
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _conf = conf;
        _logger = logger;
        _twitchApiService = twitchApiService;
        _authClient = apiClientFactory.GetClient(TwitchConfig.AuthUrl);
    }

    public async Task<(User, TokenResponse)> Callback(string code)
    {
        RestRequest request = new("token", Method.Post);
        request.AddParameter("client_id", ClientId);
        request.AddParameter("client_secret", ClientSecret);
        request.AddParameter("code", code);
        request.AddParameter("scope", string.Join(' ', Scopes));
        request.AddParameter("grant_type", "authorization_code");
        request.AddParameter("redirect_uri", TwitchConfig.RedirectUri);

        RestResponse response = await _authClient.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch token from Twitch.");

        TokenResponse? tokenResponse = response.Content.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Twitch.");

        User user = await _twitchApiService.FetchUser();

        await StoreTokens(tokenResponse, user);

        return (user, tokenResponse);
    }

    public Task<(User, TokenResponse)> ValidateToken(HttpRequest request)
    {
        string authorizationHeader =
            request.Headers["Authorization"].First() ?? throw new InvalidOperationException();
        string accessToken = authorizationHeader["Bearer ".Length..];

        return ValidateToken(accessToken);
    }

    public async Task<(User, TokenResponse)> ValidateToken(string accessToken)
    {
        RestRequest request = new("validate");
        request.AddHeader("Authorization", $"Bearer {accessToken}");

        RestResponse response = await _authClient.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch token from Twitch.");

        TokenResponse? tokenResponse = response.Content.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Twitch.");

        Service service =
            await _dbContext
                .Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == Service.Name)
            ?? throw new InvalidOperationException(
                $"_service {Service.Name} not found in database."
            );

        return (
            new(),
            new()
            {
                AccessToken = service.AccessToken,
                RefreshToken = service.RefreshToken,
                ExpiresIn = (int)(service.TokenExpiry - DateTime.UtcNow).Value.TotalSeconds,
            }
        );
    }

    public async Task<(User, TokenResponse)> RefreshToken(string refreshToken)
    {
        RestRequest request = new("token", Method.Post);
        request.AddParameter("client_id", ClientId);
        request.AddParameter("client_secret", ClientSecret);
        request.AddParameter("refresh_token", refreshToken);
        request.AddParameter("grant_type", "refresh_token");

        RestResponse response = await _authClient.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch token from Twitch.");

        TokenResponse? tokenResponse = response.Content?.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Twitch.");

        return (new(), tokenResponse);
    }

    public async Task RevokeToken(string accessToken)
    {
        RestRequest request = new("revoke", Method.Post);
        request.AddParameter("client_id", ClientId);
        request.AddParameter("token", accessToken);
        request.AddParameter("token_type_hint", "access_token");

        RestResponse response = await _authClient.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch token from Twitch.");
    }

    public string GetRedirectUrl()
    {
        NameValueCollection query = HttpUtility.ParseQueryString(string.Empty);
        query.Add("response_type", "code");
        query.Add("client_id", ClientId);
        query.Add("redirect_uri", TwitchConfig.RedirectUri);
        query.Add("scope", string.Join(' ', Scopes));
        // query.Add("force_verify", "true");

        UriBuilder uriBuilder = new(TwitchConfig.AuthUrl + "/authorize")
        {
            Query = query.ToString(),
            Scheme = Uri.UriSchemeHttps,
        };

        return uriBuilder.ToString();
    }

    public async Task<DeviceCodeResponse> Authorize(string[]? scopes = null)
    {
        RestRequest request = new("device", Method.Post);
        request.AddParameter("client_id", ClientId);
        request.AddParameter("scopes", string.Join(' ', scopes ?? Scopes));

        RestResponse response = await _authClient.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch device code from Twitch.");

        DeviceCodeResponse? deviceCodeResponse = response.Content.FromJson<DeviceCodeResponse>();
        if (deviceCodeResponse == null)
            throw new("Invalid response from Twitch.");

        return deviceCodeResponse;
    }

    public async Task<TokenResponse> PollForToken(string deviceCode)
    {
        RestRequest request = new("token", Method.Post);
        request.AddParameter("client_id", ClientId);
        request.AddParameter("client_secret", ClientSecret);
        request.AddParameter("grant_type", "urn:ietf:params:oauth:grant-type:device_code");
        request.AddParameter("device_code", deviceCode);
        request.AddParameter("scopes", string.Join(' ', Scopes));

        RestResponse response = await _authClient.ExecuteAsync(request);
        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch token from Twitch.");

        TokenResponse? tokenResponse = response.Content.FromJson<TokenResponse>();
        if (tokenResponse == null)
            throw new("Invalid response from Twitch.");

        return tokenResponse;
    }

    public async Task<TokenResponse> BotToken()
    {
        RestRequest request = new("token", Method.Post);
        request.AddParameter("client_id", ClientId);
        request.AddParameter("client_secret", ClientSecret);
        request.AddParameter("grant_type", "client_credentials");
        request.AddParameter("scope", string.Join(' ', Scopes));

        RestResponse response = await _authClient.ExecuteAsync(request);
        if (!response.IsSuccessful)
            throw new("Failed to fetch bot token.");

        TokenResponse? botToken = response.Content?.FromJson<TokenResponse>();
        if (botToken is null)
            throw new("Failed to parse bot token.");

        return botToken;
    }

    public async Task StoreTokens(TokenResponse tokenResponse, User user)
    {
        // Always check the database for the current user before overwriting tokens
        Service? existingService = await _dbContext
            .Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == Service.Name);
        if (
            existingService != null
            && !string.IsNullOrEmpty(existingService.UserId)
            && existingService.UserId != user.Id
        )
        {
            _logger.LogWarning(
                "Attempt to overwrite Twitch provider tokens for {Provider} with a different user. Existing: {ExistingUserId}, Incoming: {IncomingUserId}",
                Service.Name,
                existingService.UserName,
                user.Username
            );
            throw new InvalidOperationException(
                "This provider is already linked to a different user."
            );
        }

        Service updateService = new()
        {
            Name = Service.Name,
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            UserId = user.Id,
            UserName = user.Username,
        };

        await _dbContext
            .Services.Upsert(updateService)
            .On(u => u.Name)
            .WhenMatched(
                (oldUser, newUser) =>
                    new()
                    {
                        AccessToken = newUser.AccessToken,
                        RefreshToken = newUser.RefreshToken,
                        TokenExpiry = newUser.TokenExpiry,
                        UserId = newUser.UserId,
                        UserName = newUser.UserName,
                        UpdatedAt = DateTime.UtcNow,
                    }
            )
            .RunAsync();

        Service.AccessToken = updateService.AccessToken;
        Service.RefreshToken = updateService.RefreshToken;
        Service.TokenExpiry = updateService.TokenExpiry;
        Service.UserId = updateService.UserId;
        Service.UserName = updateService.UserName;
    }

    public async Task<bool> ConfigureService(ProviderConfigRequest config)
    {
        try
        {
            // Find existing service or create new one
            Service service = await _dbContext.Services.FirstAsync(s => s.Name == "Twitch");

            // Update the configuration
            service.ClientId = config.ClientId;
            service.ClientSecret = config.ClientSecret;
            service.Scopes = config.Scopes;
            service.Enabled = true;

            _dbContext.Services.Update(service);

            await _dbContext.SaveChangesAsync();

            // Update the static reference
            TwitchConfig._service = service;

            _logger.LogInformation("Twitch service configured successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to configure Twitch service: {Error}", ex.Message);
            return false;
        }
    }

    public string GetRedirectUrlWithScopes(string[] specificScopes)
    {
        NameValueCollection query = HttpUtility.ParseQueryString(string.Empty);
        query.Add("response_type", "code");
        query.Add("client_id", ClientId);
        query.Add("redirect_uri", TwitchConfig.RedirectUri);
        query.Add("scope", string.Join(' ', specificScopes));
        query.Add("force_verify", "true");

        UriBuilder uriBuilder = new(TwitchConfig.AuthUrl + "/authorize")
        {
            Query = query.ToString(),
            Scheme = Uri.UriSchemeHttps,
        };

        return uriBuilder.ToString();
    }

    // Method to authorize with specific scopes (used by BotAuthService)
    public async Task<DeviceCodeResponse> AuthorizeWithScopes(string[] specificScopes)
    {
        RestRequest request = new("device", Method.Post);
        request.AddParameter("client_id", ClientId);
        request.AddParameter("scopes", string.Join(' ', specificScopes));

        RestResponse response = await _authClient.ExecuteAsync(request);

        if (!response.IsSuccessful || response.Content is null)
            throw new(response.Content ?? "Failed to fetch device code from Twitch.");

        DeviceCodeResponse? deviceCodeResponse = response.Content.FromJson<DeviceCodeResponse>();
        if (deviceCodeResponse == null)
            throw new("Invalid response from Twitch.");

        return deviceCodeResponse;
    }
}
