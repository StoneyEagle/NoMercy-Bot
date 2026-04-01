using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Auth")]
[Route("api/bot-account")]
public class BotAccountController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public BotAccountController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetBotAccount()
    {
        BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();

        if (botAccount == null)
            return NotFound(new { message = "Bot account not found" });

        User? user = await _dbContext
            .Users.Include(user => user.Channel)
            .FirstOrDefaultAsync(u => u.Username == botAccount.Username);

        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(new BotUser(botAccount, user));
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteBotAccount()
    {
        BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();
        if (botAccount == null)
            return NotFound(new { message = "Bot account not found" });

        _dbContext.BotAccounts.Remove(botAccount);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetAuthStatus()
    {
        BotAccount? botAccount = await _dbContext.BotAccounts.FirstOrDefaultAsync();
        if (botAccount == null)
            return Ok(new { authenticated = false });

        // Check if token is expired
        bool isValid = botAccount.TokenExpiry.HasValue && botAccount.TokenExpiry > DateTime.UtcNow;

        return Ok(
            new
            {
                authenticated = isValid,
                username = botAccount.Username,
                tokenExpiry = botAccount.TokenExpiry,
            }
        );
    }
}

public class BotUser : SimpleUser
{
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string[] Scopes { get; set; } = [];

    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? TokenExpiry { get; set; }

    public BotUser(BotAccount service, User user)
        : base(user)
    {
        ClientId = service.ClientId;
        ClientSecret = service.ClientSecret;
        AccessToken = service.AccessToken;
        RefreshToken = service.RefreshToken;
        TokenExpiry = service.TokenExpiry;
    }
}
