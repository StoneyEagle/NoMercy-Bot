using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Obs;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;
using Serilog.Events;

namespace NoMercyBot.Services.Other;

public class TokenRefreshService : BackgroundService
{
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _refreshThreshold = TimeSpan.FromMinutes(5);

    public TokenRefreshService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TokenRefreshService> logger
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Token refresh service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshTokensIfNeeded(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while refreshing tokens");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task RefreshTokensIfNeeded(CancellationToken cancellationToken)
    {
        List<Service> services = await _dbContext.Services.ToListAsync(cancellationToken);

        foreach (
            Service authService in services.Where(authService =>
                !string.IsNullOrEmpty(authService.RefreshToken)
            )
        )
        {
            if (authService.TokenExpiry == null)
                continue;

            DateTime expiryTime = authService.TokenExpiry.Value;
            DateTime refreshTime = expiryTime.AddMinutes(-_refreshThreshold.TotalMinutes);

            if (DateTime.UtcNow < refreshTime)
                continue;

            Logger.System(DateTime.UtcNow.ToLongTimeString(), LogEventLevel.Verbose);
            Logger.System(refreshTime.ToLongTimeString(), LogEventLevel.Verbose);
            await RefreshServiceToken(authService, _scope, cancellationToken);
        }

        List<BotAccount> botAccounts = await _dbContext
            .BotAccounts.Where(b => b.TokenExpiry != null)
            .ToListAsync(cancellationToken);

        foreach (BotAccount botAccount in botAccounts)
        {
            if (botAccount.TokenExpiry == null)
                continue;

            DateTime expiryTime = botAccount.TokenExpiry!.Value;
            DateTime refreshTime = expiryTime.AddMinutes(-_refreshThreshold.TotalMinutes);

            if (DateTime.UtcNow < refreshTime)
                continue;

            await RefreshBotToken(botAccount, _scope, cancellationToken);
        }
    }

    private async Task RefreshServiceToken(
        Service service,
        IServiceScope scope,
        CancellationToken cancellationToken
    )
    {
        try
        {
            IAuthService? authService = GetAuthServiceForProvider(service.Name, scope);

            if (authService == null)
                return;

            _logger.LogDebug("Refreshing token for service {ServiceName}", service.Name);

            (User user, TokenResponse response) = await authService.RefreshToken(
                service.RefreshToken!
            );

            authService.Service.AccessToken = response.AccessToken;
            authService.Service.RefreshToken = response.RefreshToken;
            authService.Service.TokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
            authService.Service.UserId = string.IsNullOrWhiteSpace(user.Id)
                ? authService.Service.UserId
                : user.Id;
            authService.Service.UserName = string.IsNullOrWhiteSpace(user.Username)
                ? authService.Service.UserName
                : user.Username;

            await _dbContext
                .Services.Upsert(authService.Service)
                .On(u => u.Name)
                .WhenMatched(
                    (oldService, newService) =>
                        new()
                        {
                            AccessToken = newService.AccessToken,
                            RefreshToken = newService.RefreshToken,
                            TokenExpiry = newService.TokenExpiry,
                            UserId = newService.UserId,
                            UserName = newService.UserName,
                            UpdatedAt = DateTime.UtcNow,
                        }
                )
                .RunAsync(cancellationToken);

            service.AccessToken = authService.Service.AccessToken;
            service.RefreshToken = authService.Service.RefreshToken;
            service.TokenExpiry = authService.Service.TokenExpiry;
            service.UserId = authService.Service.UserId;
            service.UserName = authService.Service.UserName;
            service.UpdatedAt = authService.Service.UpdatedAt;

            _logger.LogDebug("Successfully refreshed token for {ServiceName}", service.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token for service {ServiceName}", service.Name);
        }
    }

    internal async Task RefreshBotToken(
        BotAccount botAccount,
        IServiceScope scope,
        CancellationToken cancellationToken = new()
    )
    {
        try
        {
            // Renew app access token (for bot badge) if it's near expiry
            if (!string.IsNullOrEmpty(botAccount.AppAccessToken) && botAccount.AppTokenExpiry != null)
            {
                DateTime appRefreshTime = botAccount.AppTokenExpiry.Value.AddMinutes(-_refreshThreshold.TotalMinutes);
                if (DateTime.UtcNow >= appRefreshTime)
                {
                    await RenewClientCredentialsBotToken(botAccount, scope, cancellationToken);
                }
            }

            if (string.IsNullOrEmpty(botAccount.RefreshToken))
                return; // No user token to refresh

            IAuthService? authService = GetAuthServiceForProvider("Twitch", scope);

            if (authService == null)
                return;

            _logger.LogDebug("Refreshing token for bot account {BotName}", botAccount.Username);

            (User user, TokenResponse response) = await authService.RefreshToken(
                botAccount.RefreshToken!
            );

            botAccount.AccessToken = response.AccessToken;
            botAccount.RefreshToken = response.RefreshToken;
            botAccount.TokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);

            botAccount.Username = string.IsNullOrWhiteSpace(user.Username)
                ? botAccount.Username
                : user.Username;

            await _dbContext
                .BotAccounts.Upsert(botAccount)
                .On(u => u.Username)
                .WhenMatched(
                    (oldBot, newBot) =>
                        new()
                        {
                            AccessToken = newBot.AccessToken,
                            RefreshToken = newBot.RefreshToken,
                            TokenExpiry = newBot.TokenExpiry,
                            Username = newBot.Username,
                        }
                )
                .RunAsync(cancellationToken);

            _logger.LogDebug(
                "Successfully refreshed token for bot account {BotName}",
                botAccount.Username
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to refresh token for bot account {BotName}",
                botAccount.Username
            );
        }
    }

    private async Task RenewClientCredentialsBotToken(
        BotAccount botAccount,
        IServiceScope scope,
        CancellationToken cancellationToken
    )
    {
        try
        {
            TwitchAuthService twitchAuth =
                scope.ServiceProvider.GetRequiredService<TwitchAuthService>();

            _logger.LogDebug(
                "Renewing client credentials token for bot account {BotName}",
                botAccount.Username
            );

            TokenResponse response = await twitchAuth.BotToken();

            botAccount.AppAccessToken = response.AccessToken;
            botAccount.AppTokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);

            await _dbContext
                .BotAccounts.Upsert(botAccount)
                .On(u => u.Username)
                .WhenMatched(
                    (oldBot, newBot) =>
                        new()
                        {
                            AppAccessToken = newBot.AppAccessToken,
                            AppTokenExpiry = newBot.AppTokenExpiry,
                        }
                )
                .RunAsync(cancellationToken);

            _logger.LogDebug(
                "Successfully renewed app access token for bot account {BotName}",
                botAccount.Username
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to renew client credentials token for bot account {BotName}",
                botAccount.Username
            );
        }
    }

    private IAuthService? GetAuthServiceForProvider(string provider, IServiceScope scope)
    {
        return provider.ToLower() switch
        {
            "twitch" => scope.ServiceProvider.GetService<TwitchAuthService>(),
            "spotify" => scope.ServiceProvider.GetService<SpotifyAuthService>(),
            "discord" => scope.ServiceProvider.GetService<DiscordAuthService>(),
            "obs" => scope.ServiceProvider.GetService<ObsAuthService>(),
            _ => null,
        };
    }
}
