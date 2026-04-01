using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Obs;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Services;

public class ServiceResolver
{
    private readonly ILogger<ServiceResolver> _logger;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly TwitchAuthService _twitchAuthService;
    private readonly TwitchApiService _twitchApiService;
    private readonly BotAuthService _botAuthService;

    public bool DiscordNeedsAuth { get; private set; }
    public bool SpotifyNeedsAuth { get; private set; }

    public ServiceResolver(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ServiceResolver> logger,
        TwitchAuthService twitchAuthService,
        TwitchApiService twitchApiService,
        BotAuthService botAuthService
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _twitchAuthService = twitchAuthService;
        _twitchApiService = twitchApiService;
        _botAuthService = botAuthService;
    }

    private async Task InitializeTwitch()
    {
        Service? service = await _dbContext
            .Services.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == "Twitch");

        if (service == null)
        {
            _logger.LogWarning("Twitch service not found in database");
            return;
        }

        TwitchConfig._service = service;

        if (
            service.Enabled
            && !string.IsNullOrEmpty(service.ClientId)
            && !string.IsNullOrEmpty(service.ClientSecret)
            && string.IsNullOrEmpty(service.AccessToken)
        )
        {
            _logger.LogWarning(
                "Twitch service has no access token. Starting device code authorization flow..."
            );

            await RunDeviceCodeFlow(_twitchAuthService, "Twitch", openBrowser: true);

            // Reload service from DB after auth
            service = await _dbContext
                .Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == "Twitch");

            if (service != null)
                TwitchConfig._service = service;
        }

        _logger.LogInformation(
            "Twitch service initialized. Enabled: {Enabled}, UserId: {UserId}, UserName: {UserName}",
            service!.Enabled,
            service.UserId,
            service.UserName
        );
    }

    private async Task InitializeSpotify()
    {
        Service? service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Name == "Spotify");
        if (service == null)
        {
            _logger.LogWarning("Spotify service not found in database");
            return;
        }

        SpotifyConfig._service = service;

        if (
            service.Enabled
            && !string.IsNullOrEmpty(service.ClientId)
            && !string.IsNullOrEmpty(service.ClientSecret)
            && string.IsNullOrEmpty(service.AccessToken)
        )
        {
            _logger.LogWarning(
                "Spotify service has no access token. Will prompt for auth after server starts."
            );
            SpotifyNeedsAuth = true;
        }

        _logger.LogInformation("Spotify service initialized. Enabled: {Enabled}", service.Enabled);
    }

    private async Task InitializeDiscord()
    {
        Service? service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Name == "Discord");
        if (service == null)
        {
            _logger.LogWarning("Discord service not found in database");
            return;
        }

        DiscordConfig._service = service;

        if (
            service.Enabled
            && !string.IsNullOrEmpty(service.ClientId)
            && !string.IsNullOrEmpty(service.ClientSecret)
            && string.IsNullOrEmpty(service.AccessToken)
        )
        {
            _logger.LogWarning(
                "Discord service has no access token. Will prompt for auth after server starts."
            );
            DiscordNeedsAuth = true;
        }

        _logger.LogInformation("Discord service initialized. Enabled: {Enabled}", service.Enabled);
    }

    private async Task InitializeObs()
    {
        Service? service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Name == "OBS");
        if (service == null)
        {
            _logger.LogWarning("OBS service not found in database");
            return;
        }

        ObsConfig._service = service;

        if (service.Enabled && string.IsNullOrEmpty(service.ClientId))
        {
            _logger.LogWarning(
                "OBS service has no host configured. Please enter OBS WebSocket credentials."
            );

            Console.Write("OBS WebSocket Host (e.g. ws://192.168.1.100:4455): ");
            string? host = Console.ReadLine()?.Trim();

            Console.Write("OBS WebSocket Password (leave empty if none): ");
            string? password = Console.ReadLine()?.Trim();

            if (!string.IsNullOrEmpty(host))
            {
                service.ClientId = host;
                service.ClientSecret = password ?? string.Empty;

                _dbContext.Services.Update(service);
                await _dbContext.SaveChangesAsync();
                ObsConfig._service = service;

                _logger.LogInformation("OBS credentials saved. Host: {Host}", host);
            }
            else
            {
                _logger.LogWarning("No OBS host provided. OBS service will not be available.");
            }
        }

        _logger.LogInformation("OBS service initialized. Enabled: {Enabled}", service.Enabled);
    }

    private async Task InitializeBotProvider()
    {
        BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();

        if (botAccount != null && ValidateBotOAuth(botAccount))
        {
            _logger.LogInformation(
                "Bot provider initialized with username: {Username}",
                botAccount.Username
            );
            return;
        }

        if (botAccount != null && !string.IsNullOrEmpty(botAccount.RefreshToken))
        {
            _logger.LogWarning(
                "Bot provider OAuth credentials are invalid or expired. Attempting to refresh token..."
            );

            try
            {
                (User user, TokenResponse response) = await _twitchAuthService.RefreshToken(
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
                    .RunAsync();

                _logger.LogInformation(
                    "Bot provider OAuth credentials refreshed successfully. Username: {Username}",
                    botAccount.Username
                );
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh bot token. Starting device code flow...");
            }
        }

        // No bot account or refresh failed — start device code flow
        Service? twitchService = TwitchConfig._service;
        if (twitchService == null || string.IsNullOrEmpty(twitchService.ClientId))
        {
            _logger.LogWarning("Cannot start bot auth flow — Twitch service not configured.");
            return;
        }

        _logger.LogWarning("Bot account needs authorization. Starting device code flow...");

        try
        {
            string[] botScopes = BotConfig.AvailableScopes.Keys.ToArray();
            DeviceCodeResponse deviceCode = await _twitchAuthService.AuthorizeWithScopes(botScopes);

            // Do NOT open browser — log to console so user can open in any browser
            _logger.LogWarning(
                "Bot authorization required. Visit {VerificationUri} and enter code: {UserCode}",
                deviceCode.VerificationUri,
                deviceCode.UserCode
            );
            Console.WriteLine();
            Console.WriteLine("=== Bot Account Authorization ===");
            Console.WriteLine($"Visit: {deviceCode.VerificationUri}");
            Console.WriteLine($"Enter code: {deviceCode.UserCode}");
            Console.WriteLine("(Open this URL in the browser where your bot account is logged in)");
            Console.WriteLine("=================================");
            Console.WriteLine();

            TokenResponse tokenResponse = await PollForDeviceToken(
                _twitchAuthService,
                deviceCode.DeviceCode,
                deviceCode.Interval,
                "Bot"
            );

            // Store bot account
            await _botAuthService.StoreTokens(tokenResponse);

            _logger.LogInformation("Bot account authorized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authorize bot account: {Message}", ex.Message);
        }
    }

    private async Task RunDeviceCodeFlow(
        TwitchAuthService authService,
        string providerName,
        bool openBrowser
    )
    {
        try
        {
            DeviceCodeResponse deviceCode = await authService.Authorize();

            if (openBrowser)
            {
                _logger.LogInformation(
                    "{Provider} authorization required. Opening browser to {VerificationUri} — enter code: {UserCode}",
                    providerName,
                    deviceCode.VerificationUri,
                    deviceCode.UserCode
                );

                BrowserHelper.OpenUrl(deviceCode.VerificationUri);
            }

            Console.WriteLine();
            Console.WriteLine($"=== {providerName} Authorization ===");
            Console.WriteLine($"Visit: {deviceCode.VerificationUri}");
            Console.WriteLine($"Enter code: {deviceCode.UserCode}");
            Console.WriteLine($"Waiting for authorization...");
            Console.WriteLine(new string('=', providerName.Length + 22));
            Console.WriteLine();

            TokenResponse tokenResponse = await PollForDeviceToken(
                authService,
                deviceCode.DeviceCode,
                deviceCode.Interval,
                providerName
            );

            // Fetch user info first (may fail, but we try)
            User? user = null;
            try
            {
                user = await _twitchApiService.FetchUser(accessToken: tokenResponse.AccessToken);
                _logger.LogInformation(
                    "{Provider} user fetched: {UserId} / {UserName}",
                    providerName,
                    user.Id,
                    user.Username
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Provider} failed to fetch user info: {Message}. Will store tokens without user info.",
                    providerName,
                    ex.Message
                );
            }

            // Store tokens using tracked entity update (value converters will encrypt)
            _logger.LogDebug(
                "{Provider} storing tokens - AccessToken length: {AccessTokenLen}, RefreshToken length: {RefreshTokenLen}",
                providerName,
                tokenResponse.AccessToken?.Length ?? 0,
                tokenResponse.RefreshToken?.Length ?? 0
            );

            // Use a fresh scope to ensure clean DbContext state
            using IServiceScope freshScope = _scope
                .ServiceProvider.GetRequiredService<IServiceScopeFactory>()
                .CreateScope();
            AppDbContext freshDb = freshScope.ServiceProvider.GetRequiredService<AppDbContext>();

            Service? serviceToUpdate = await freshDb.Services.FirstOrDefaultAsync(s =>
                s.Name == providerName
            );
            if (serviceToUpdate == null)
            {
                _logger.LogError("{Provider} service not found in database", providerName);
                return;
            }

            serviceToUpdate.AccessToken = tokenResponse.AccessToken;
            serviceToUpdate.RefreshToken = tokenResponse.RefreshToken;
            serviceToUpdate.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            serviceToUpdate.UserId = user?.Id ?? serviceToUpdate.UserId;
            serviceToUpdate.UserName = user?.Username ?? serviceToUpdate.UserName;
            serviceToUpdate.UpdatedAt = DateTime.UtcNow;

            freshDb.Services.Update(serviceToUpdate);
            await freshDb.SaveChangesAsync();

            _logger.LogInformation("{Provider} tokens stored successfully", providerName);

            // Reload and update static config
            _dbContext.ChangeTracker.Clear();
            Service? service = await _dbContext
                .Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == providerName);

            if (service != null)
            {
                TwitchConfig._service = service;
                _logger.LogInformation(
                    "{Provider} authorized successfully as {UserName}",
                    providerName,
                    service.UserName
                );
            }
            else
            {
                _logger.LogWarning(
                    "{Provider} service not found after storing tokens",
                    providerName
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to authorize {Provider}: {Message}",
                providerName,
                ex.Message
            );
        }
    }

    private async Task<TokenResponse> PollForDeviceToken(
        TwitchAuthService authService,
        string deviceCode,
        int intervalSeconds,
        string providerName
    )
    {
        int interval = Math.Max(intervalSeconds, 5) * 1000;

        while (true)
        {
            await Task.Delay(interval);

            try
            {
                TokenResponse tokenResponse = await authService.PollForToken(deviceCode);
                return tokenResponse;
            }
            catch (Exception ex)
            {
                if (
                    ex.Message.Contains("authorization_pending", StringComparison.OrdinalIgnoreCase)
                )
                {
                    _logger.LogDebug("{Provider} authorization pending...", providerName);
                    continue;
                }

                if (ex.Message.Contains("slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    interval += 5000;
                    _logger.LogDebug(
                        "{Provider} polling too fast, slowing down to {Interval}ms",
                        providerName,
                        interval
                    );
                    continue;
                }

                throw;
            }
        }
    }

    public async Task HandleRedirectAuthFlow(string providerName, string redirectUrl)
    {
        _logger.LogInformation(
            "{Provider} authorization required. Opening browser...",
            providerName
        );

        BrowserHelper.OpenUrl(redirectUrl);

        Console.WriteLine();
        Console.WriteLine($"=== {providerName} Authorization ===");
        Console.WriteLine("A browser window has been opened for authentication.");
        Console.WriteLine("Complete the authorization in your browser.");
        Console.WriteLine($"Waiting for {providerName} callback...");
        Console.WriteLine(new string('=', providerName.Length + 22));
        Console.WriteLine();

        // Poll the database until the token appears
        int maxAttempts = 120; // 10 minutes at 5-second intervals
        for (int i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(5000);

            _dbContext.ChangeTracker.Clear();
            Service? service = await _dbContext
                .Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Name == providerName);

            if (service != null && !string.IsNullOrEmpty(service.AccessToken))
            {
                _logger.LogInformation("{Provider} authorized successfully.", providerName);

                // Update the static config
                switch (providerName)
                {
                    case "Spotify":
                        SpotifyConfig._service = service;
                        SpotifyNeedsAuth = false;
                        break;
                    case "Discord":
                        DiscordConfig._service = service;
                        DiscordNeedsAuth = false;
                        break;
                }

                return;
            }

            if (i % 12 == 0 && i > 0)
                _logger.LogInformation(
                    "Still waiting for {Provider} authorization...",
                    providerName
                );
        }

        _logger.LogWarning(
            "{Provider} authorization timed out. You can authorize later via the API.",
            providerName
        );
    }

    private bool ValidateBotOAuth(BotAccount botAccount)
    {
        return !string.IsNullOrEmpty(botAccount.AccessToken)
            && botAccount.TokenExpiry.HasValue
            && botAccount.TokenExpiry.Value > DateTime.UtcNow;
    }

    public async Task InitializeAllServices()
    {
        _dbContext.ChangeTracker.Clear();

        await InitializeTwitch();
        await InitializeBotProvider();
        await InitializeSpotify();
        await InitializeDiscord();
        await InitializeObs();
    }
}
