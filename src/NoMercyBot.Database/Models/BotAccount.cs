using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NoMercyBot.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Username), IsUnique = true)]
public class BotAccount
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime? TokenExpiry { get; set; }

    /// <summary>
    /// App access token (client credentials) used for sending chat messages with the bot badge.
    /// The regular AccessToken (user token) is still needed for user:bot authorization.
    /// </summary>
    public string AppAccessToken { get; set; } = string.Empty;

    public DateTime? AppTokenExpiry { get; set; }
}
