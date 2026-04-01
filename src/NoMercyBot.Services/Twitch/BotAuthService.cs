using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Services.Twitch;

public class BotAuthService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BotAuthService> _logger;
    private readonly TwitchAuthService _twitchAuthService;

    public BotAuthService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BotAuthService> logger,
        TwitchAuthService twitchAuthService
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
    }

    public string ClientId =>
        TwitchConfig.Service().ClientId
        ?? throw new InvalidOperationException("Twitch ClientId is not set.");

    public string ClientSecret =>
        TwitchConfig.Service().ClientSecret
        ?? throw new InvalidOperationException("Twitch ClientSecret is not set.");

    private string[] Scopes =>
        BotConfig.AvailableScopes.Keys.ToArray()
        ?? throw new InvalidOperationException("Twitch Scopes are not set.");

    public Task<(User, TokenResponse)> Callback(string code)
    {
        return _twitchAuthService.Callback(code);
    }

    public async Task<(User, TokenResponse)> ValidateToken(HttpRequest request)
    {
        string authorizationHeader =
            request.Headers["Authorization"].First() ?? throw new InvalidOperationException();
        string accessToken = authorizationHeader["Bearer ".Length..];

        return await ValidateToken(accessToken);
    }

    public async Task<(User, TokenResponse)> ValidateToken(string accessToken)
    {
        return await _twitchAuthService.ValidateToken(accessToken);
    }

    public async Task<(User, TokenResponse)> RefreshToken(string refreshToken)
    {
        return await _twitchAuthService.RefreshToken(refreshToken);
    }

    public async Task RevokeToken(string accessToken)
    {
        await _twitchAuthService.RevokeToken(accessToken);
    }

    public string GetRedirectUrl()
    {
        return _twitchAuthService.GetRedirectUrlWithScopes(Scopes);
    }

    public async Task<DeviceCodeResponse> Authorize(string[]? scopes = null)
    {
        return await _twitchAuthService.Authorize(Scopes);
    }

    public async Task<TokenResponse> PollForToken(string deviceCode)
    {
        return await _twitchAuthService.PollForToken(deviceCode);
    }

    public async Task StoreTokens(TokenResponse tokenResponse)
    {
        BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();

        if (botAccount == null)
        {
            botAccount = new()
            {
                Username = "BotAccount",
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                ClientId = ClientId,
                ClientSecret = ClientSecret,
            };

            _dbContext.BotAccounts.Add(botAccount);
        }
        else
        {
            botAccount.AccessToken = tokenResponse.AccessToken;
            botAccount.RefreshToken = tokenResponse.RefreshToken;
            botAccount.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        }

        await _dbContext.SaveChangesAsync();

        try
        {
            TwitchApiService twitchApiService =
                _scope.ServiceProvider.GetRequiredService<TwitchApiService>();
            List<UserInfo>? user = await twitchApiService.GetUsers(
                accessToken: tokenResponse.AccessToken
            );

            if (user != null && user.Any())
            {
                botAccount.Username = user.First().Login;
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update bot account username");
        }
    }

    public Task<bool> ConfigureService(ProviderConfigRequest config)
    {
        return _twitchAuthService.ConfigureService(config);
    }
}
