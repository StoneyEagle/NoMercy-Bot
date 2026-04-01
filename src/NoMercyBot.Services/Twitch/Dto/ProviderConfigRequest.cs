using System.ComponentModel.DataAnnotations;

namespace NoMercyBot.Services.Twitch.Dto;

public class ProviderConfigRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    public string[] Scopes { get; set; } = [];
}
