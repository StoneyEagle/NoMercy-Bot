using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Route("api/bot")]
[Tags("Bot")]
public class BotAuthController : BaseController
{
    private readonly AppDbContext _dbContext;
    private readonly BotAuthService _botAuthService;
    private readonly TwitchAuthService _twitchAuthService;
    private readonly TwitchApiService _twitchApiService;
    private readonly TwitchChatService _twitchChatService;
    private readonly ILogger<BotAuthController> _logger;

    public BotAuthController(
        AppDbContext dbContext,
        TwitchApiService twitchApiService,
        TwitchAuthService twitchAuthService,
        TwitchChatService twitchChatService,
        BotAuthService botAuthService,
        ILogger<BotAuthController> logger
    )
    {
        _dbContext = dbContext;
        _botAuthService = botAuthService;
        _twitchAuthService = twitchAuthService;
        _twitchApiService = twitchApiService;
        _twitchChatService = twitchChatService;
        _logger = logger;
    }

    [HttpGet("authenticate")]
    public async Task<IActionResult> Authenticate()
    {
        try
        {
            // Use device code flow
            DeviceCodeResponse deviceCodeResponse = await _botAuthService.Authorize();

            return Ok(deviceCodeResponse);
        }
        catch (Exception ex)
        {
            return BadRequestResponse(ex.Message);
        }
    }

    [HttpPost("device/token")]
    public async Task<IActionResult> GetTokenFromDeviceCode([FromBody] DeviceCodeRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.DeviceCode))
                return BadRequestResponse("Device code is required");

            // Use the TwitchAuthService to poll for the token
            TokenResponse tokenResponse = await _botAuthService.PollForToken(request.DeviceCode);

            // Get user information using the token
            User? user;
            try
            {
                user = await _twitchApiService.FetchUser(accessToken: tokenResponse.AccessToken);
                _logger.LogInformation("Successfully fetched user: {Username}", user.DisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user information");
                return BadRequestResponse($"Error fetching user information: {ex.Message}");
            }

            // Store the bot account
            BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();
            if (botAccount == null)
            {
                _logger.LogInformation(
                    "Creating new bot account for user {Username}",
                    user.DisplayName
                );
                botAccount = new()
                {
                    Username = user.Username,
                    ClientId = _botAuthService.ClientId,
                    ClientSecret = _botAuthService.ClientSecret,
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken,
                    TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                };
                _dbContext.BotAccounts.Add(botAccount);
            }
            else
            {
                _logger.LogInformation(
                    "Updating existing bot account for user {Username}",
                    user.DisplayName
                );
                botAccount.Username = user.Username;
                botAccount.AccessToken = tokenResponse.AccessToken;
                botAccount.RefreshToken = tokenResponse.RefreshToken;
                botAccount.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Bot account saved successfully");

            return Ok(new { success = true, username = user.DisplayName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get token from device code");
            return BadRequestResponse($"Failed to get token: {ex.Message}");
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetAuthStatus()
    {
        try
        {
            BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();

            if (botAccount == null)
                return Ok(new { authenticated = false });

            bool isValid = true;
            string username = botAccount.Username;

            // Check if token is expired
            if (botAccount.TokenExpiry.HasValue && botAccount.TokenExpiry < DateTime.UtcNow)
                try
                {
                    if (!string.IsNullOrEmpty(botAccount.RefreshToken))
                    {
                        // User token - refresh with refresh token
                        (User user, TokenResponse tokenResponse) = await _botAuthService.RefreshToken(
                            botAccount.RefreshToken
                        );

                        botAccount.AccessToken = tokenResponse.AccessToken;
                        botAccount.RefreshToken = tokenResponse.RefreshToken;
                        botAccount.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                        username = user.DisplayName;
                    }
                    else
                    {
                        // Client credentials token - request a new one
                        TokenResponse tokenResponse = await _botAuthService.BotToken();
                        botAccount.AccessToken = tokenResponse.AccessToken;
                        botAccount.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    }

                    await _dbContext.SaveChangesAsync();
                }
                catch
                {
                    isValid = false;
                }

            return Ok(
                new
                {
                    authenticated = isValid,
                    username = username,
                    tokenExpiry = botAccount.TokenExpiry,
                }
            );
        }
        catch (Exception ex)
        {
            return BadRequestResponse($"Failed to get status: {ex.Message}");
        }
    }

    [HttpPost("client-credentials")]
    public async Task<IActionResult> SwitchToClientCredentials()
    {
        try
        {
            TokenResponse tokenResponse = await _botAuthService.BotToken();

            BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();
            if (botAccount == null)
                return BadRequestResponse("No bot account configured. Authenticate first.");

            botAccount.AccessToken = tokenResponse.AccessToken;
            botAccount.RefreshToken = string.Empty; // Client credentials has no refresh token
            botAccount.TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Bot account {BotName} switched to client credentials token",
                botAccount.Username
            );

            return Ok(new { success = true, username = botAccount.Username, tokenExpiry = botAccount.TokenExpiry });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to client credentials");
            return BadRequestResponse($"Failed to switch to client credentials: {ex.Message}");
        }
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] BotSendMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequestResponse("Message is required.");

            if (string.IsNullOrWhiteSpace(request.Channel))
            {
                // Send to the bot's own channel (broadcaster)
                await _twitchChatService.SendMessageAsBot(
                    TwitchConfig.Service().UserName!, request.Message);
            }
            else
            {
                await _twitchChatService.SendOneOffMessageAsBot(request.Channel, request.Message);
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequestResponse($"Failed to send message: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a device code that a channel owner can use to authorize the bot
    /// with just channel:bot scope. Share the verification URL with the streamer.
    /// </summary>
    [HttpGet("channel-auth")]
    public async Task<IActionResult> GetChannelAuthCode()
    {
        try
        {
            DeviceCodeResponse deviceCode =
                await _twitchAuthService.AuthorizeWithScopes(new[] { "channel:bot" });

            return Ok(deviceCode);
        }
        catch (Exception ex)
        {
            return BadRequestResponse($"Failed to generate channel auth code: {ex.Message}");
        }
    }

    /// <summary>
    /// Polls for the channel owner's authorization. Once complete, the bot can
    /// chat in their channel with the bot badge. The token is not stored since
    /// we only need the one-time authorization grant.
    /// </summary>
    [HttpPost("channel-auth/poll")]
    public async Task<IActionResult> PollChannelAuth([FromBody] DeviceCodeRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.DeviceCode))
                return BadRequestResponse("Device code is required");

            TokenResponse tokenResponse = await _botAuthService.PollForToken(request.DeviceCode);

            // Get the user who authorized so we know which channel
            User? user = await _twitchApiService.FetchUser(accessToken: tokenResponse.AccessToken);

            return Ok(new
            {
                success = true,
                channel = user?.DisplayName ?? "Unknown",
                channelId = user?.Id,
            });
        }
        catch (Exception ex)
        {
            return BadRequestResponse($"{ex.Message}");
        }
    }
}

public class BotSendMessageRequest
{
    public string Channel { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class DeviceCodeRequest
{
    public string DeviceCode { get; set; } = string.Empty;
}
