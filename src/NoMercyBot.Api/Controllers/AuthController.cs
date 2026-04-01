using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercyBot.Api.Helpers;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Obs;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Tags("Auth")]
[Route("api/oauth/{provider}")]
public class AuthController : BaseController
{
    private readonly Dictionary<string, IAuthService> _authServices;

    public AuthController(
        [FromServices] TwitchAuthService twitchAuthService,
        [FromServices] SpotifyAuthService spotifyAuthService,
        [FromServices] DiscordAuthService discordAuthService,
        [FromServices] ObsAuthService obsAuthService
    )
    {
        _authServices = new()
        {
            ["twitch"] = twitchAuthService,
            ["spotify"] = spotifyAuthService,
            ["discord"] = discordAuthService,
            ["obs"] = obsAuthService,
        };
    }

    private IActionResult GetAuthService([FromRoute] string provider, out IAuthService? service)
    {
        service = null;

        if (!_authServices.TryGetValue(provider.ToLower(), out IAuthService? foundService))
            return NotFoundResponse($"Provider '{provider}' not found");

        if (!foundService.Service.Enabled)
            return ServiceUnavailableResponse($"Provider '{provider}' is not enabled");

        service = foundService;
        return Ok();
    }

    // get a redirect url for the user to login directly to twitch
    [HttpGet("login")]
    public IActionResult Login([FromRoute] string provider)
    {
        try
        {
            IActionResult serviceResult = GetAuthService(provider, out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            string authorizationUrl = authService!.GetRedirectUrl();

            return Redirect(authorizationUrl);
        }
        catch (Exception e)
        {
            return BadRequestResponse(e.Message);
        }
    }

    [HttpGet("authorize")]
    public async Task<IActionResult> Authorize([FromRoute] string provider)
    {
        try
        {
            IActionResult serviceResult = GetAuthService(provider, out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            DeviceCodeResponse result = await authService!.Authorize();
            return Ok(result);
        }
        catch (NotImplementedException)
        {
            return NotImplementedResponse(
                $"Authorize is not implemented for provider '{provider}'"
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Failed to authorize: {ex.Message}");
        }
    }

    [HttpGet("redirect")]
    public IActionResult RedirectUrl([FromRoute] string provider)
    {
        try
        {
            IActionResult serviceResult = GetAuthService(provider, out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            string redirectUrl = authService!.GetRedirectUrl();
            return Ok(new { url = redirectUrl });
        }
        catch (NotImplementedException)
        {
            return NotImplementedResponse(
                $"RedirectUrl is not implemented for provider '{provider}'"
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Failed to get redirect URL: {ex.Message}");
        }
    }

    [HttpGet("callback")]
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromRoute] string provider, [FromQuery] string code)
    {
        try
        {
            if (string.IsNullOrEmpty(code))
                return BadRequestResponse("Authorization code is required");

            IActionResult serviceResult = GetAuthService(provider, out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            (User user, TokenResponse tokenResponse) = await authService!.Callback(code);

            return Ok(
                new
                {
                    Message = "Logged in successfully",
                    User = new UserWithTokenDto(user, tokenResponse),
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return Forbid(ex.Message);
        }
        catch (NotImplementedException)
        {
            return NotImplementedResponse($"Callback is not implemented for provider '{provider}'");
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(ex.Message);
        }
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        [FromRoute] string provider,
        [FromBody] TokenRequest request
    )
    {
        User? currentUser = User.User();
        if (currentUser is null)
            return UnauthenticatedResponse("User not logged in.");
        try
        {
            IActionResult serviceResult = GetAuthService(provider, out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            (User, TokenResponse) result = await authService!.ValidateToken(request.AccessToken);

            return Ok(
                new
                {
                    Message = "Session validated successfully",
                    User = new UserWithTokenDto(currentUser, result.Item2),
                }
            );
        }
        catch (NotImplementedException)
        {
            return NotImplementedResponse($"Validate is not implemented for provider '{provider}'");
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Failed to validate token: {ex.Message}");
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromRoute] string provider,
        [FromBody] TokenRequest request
    )
    {
        User? currentUser = User.User();
        if (currentUser is null)
            return UnauthenticatedResponse("User not logged in.");

        try
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
                return BadRequestResponse("Refresh token is required");

            IActionResult serviceResult = GetAuthService(provider, out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            (User, TokenResponse) result = await authService!.RefreshToken(request.RefreshToken);

            return Ok(
                new
                {
                    Message = "Token refreshed successfully",
                    User = new UserWithTokenDto(currentUser, result.Item2),
                }
            );
        }
        catch (NotImplementedException)
        {
            return NotImplementedResponse($"Refresh is not implemented for provider '{provider}'");
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Failed to refresh token: {ex.Message}");
        }
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(
        [FromRoute] string provider,
        [FromBody] TokenRequest request
    )
    {
        try
        {
            if (string.IsNullOrEmpty(request.AccessToken))
                return BadRequestResponse("Access token is required");

            IActionResult serviceResult = GetAuthService(provider, out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            await authService!.RevokeToken(request.AccessToken);
            return Ok(new { success = true });
        }
        catch (NotImplementedException)
        {
            return NotImplementedResponse($"Revoke is not implemented for provider '{provider}'");
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Failed to revoke token: {ex.Message}");
        }
    }

    [HttpGet("config-status")]
    public IActionResult GetProviderConfigStatus([FromRoute] string provider)
    {
        try
        {
            if (!_authServices.TryGetValue(provider.ToLower(), out IAuthService? foundService))
                return NotFoundResponse($"Provider '{provider}' not found");

            bool isConfigured = false;

            try
            {
                isConfigured =
                    foundService.Service
                        is {
                            Enabled: true,
                            ClientId: not null,
                            ClientSecret: not null,
                            Scopes.Length: > 0
                        };
            }
            catch (InvalidOperationException)
            {
                // Configuration is missing
            }

            return Ok(
                new
                {
                    isConfigured,
                    foundService.Service.Name,
                    foundService.Service.Enabled,
                    foundService.Service.ClientId,
                    foundService.Service.ClientSecret,
                }
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(
                $"Error checking provider configuration: {ex.Message}"
            );
        }
    }

    [HttpPost("configure")]
    public async Task<IActionResult> ConfigureProvider(
        [FromRoute] string provider,
        [FromBody] ProviderConfigRequest request
    )
    {
        try
        {
            if (!_authServices.TryGetValue(provider.ToLower(), out IAuthService? foundService))
                return NotFoundResponse($"Provider '{provider}' not found");

            bool result = await foundService.ConfigureService(request);

            if (!result)
                return BadRequestResponse("Failed to configure the provider");

            return Ok(
                new { success = true, message = $"{provider} provider configured successfully" }
            );
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Error configuring provider: {ex.Message}");
        }
    }

    [HttpGet("scopes")]
    public IActionResult GetAvailableScopes([FromRoute] string provider)
    {
        try
        {
            if (!_authServices.TryGetValue(provider.ToLower(), out IAuthService? foundService))
                return NotFoundResponse($"Provider '{provider}' not found");

            return Ok(foundService.AvailableScopes);
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"Error retrieving available scopes: {ex.Message}");
        }
    }

    [NonAction]
    [HttpGet("bot/login")]
    public IActionResult BotLogin()
    {
        try
        {
            IActionResult serviceResult = GetAuthService("twitch", out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            string authorizationUrl = authService!.GetRedirectUrl();

            return Redirect(authorizationUrl);
        }
        catch (Exception e)
        {
            return BadRequestResponse(e.Message);
        }
    }

    [NonAction]
    [HttpGet("bot-popup")]
    public IActionResult BotPopupLogin()
    {
        try
        {
            IActionResult serviceResult = GetAuthService("twitch", out IAuthService? authService);
            if (serviceResult is not OkResult)
                return serviceResult;

            // Get the redirect URL with force_verify to ensure a fresh login
            string authorizationUrl = authService!.GetRedirectUrl();

            // Add special parameters for the popup flow
            authorizationUrl += "&prompt=consent&response_type=code&force_verify=true";

            return Redirect(authorizationUrl);
        }
        catch (Exception e)
        {
            return BadRequestResponse(e.Message);
        }
    }
}

public class TokenRequest
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
