using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Services.Obs;

public class ObsAuthService : IAuthService
{
    private readonly IServiceScope _scope;
    private readonly IConfiguration _conf;
    private readonly ILogger<ObsAuthService> _logger;
    private readonly AppDbContext _db;
    private readonly ObsApiService _api;

    public Service Service => ObsConfig.Service();

    public string ClientId => Service.ClientId ?? throw new InvalidOperationException("OBS ClientId is not set.");

    private string ClientSecret =>
        Service.ClientSecret ?? throw new InvalidOperationException("OBS ClientSecret is not set.");

    private string[] Scopes => Service.Scopes ?? throw new InvalidOperationException("OBS Scopes are not set.");
    public string UserId => Service.UserId ?? throw new InvalidOperationException("Twitch UserId is not set.");
    public string UserName => Service.UserName ?? throw new InvalidOperationException("Twitch UserName is not set.");

    public Dictionary<string, string> AvailableScopes =>
        ObsConfig.AvailableScopes ?? throw new InvalidOperationException("OBS Scopes are not set.");

    public ObsAuthService(IServiceScopeFactory serviceScopeFactory, IConfiguration conf, ILogger<ObsAuthService> logger,
        ObsApiService api)
    {
        _scope = serviceScopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _conf = conf;
        _logger = logger;
        _api = api;
    }

    public Task<(User, TokenResponse)> Callback(string code)
    {
        throw new NotImplementedException();
    }

    public async Task<(User, TokenResponse)> ValidateToken(HttpRequest request)
    {
        string authorizationHeader = request.Headers["Authorization"].First() ?? throw new InvalidOperationException();
        string accessToken = authorizationHeader["Bearer ".Length..];

        await ValidateToken(accessToken);

        return (new(), new()
        {
            AccessToken = accessToken,
            ExpiresIn = 3600,
            RefreshToken = null
        });
    }

    public Task<(User, TokenResponse)> ValidateToken(string accessToken)
    {
        throw new NotImplementedException();
    }

    public Task<(User, TokenResponse)> RefreshToken(string refreshToken)
    {
        throw new NotImplementedException();
    }

    public Task RevokeToken(string accessToken)
    {
        throw new NotImplementedException();
    }

    public string GetRedirectUrl()
    {
        throw new NotImplementedException();
    }

    public Task<DeviceCodeResponse> Authorize(string[]? scopes = null)
    {
        throw new NotImplementedException("OBS uses WebSocket credentials. No OAuth flow.");
    }

    public Task<TokenResponse> PollForToken(string deviceCode)
    {
        throw new NotImplementedException("OBS uses WebSocket credentials. No OAuth flow.");
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
                UpdatedAt = DateTime.UtcNow
            })
            .RunAsync();

        Service.AccessToken = updateService.AccessToken;
        Service.RefreshToken = updateService.RefreshToken;
        Service.TokenExpiry = updateService.TokenExpiry;
    }

    public Task<bool> ConfigureService(ProviderConfigRequest config)
    {
        throw new NotImplementedException();
    }
}