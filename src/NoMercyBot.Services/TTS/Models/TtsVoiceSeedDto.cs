namespace NoMercyBot.Services.TTS.Models;

/// <summary>
/// Data Transfer Object for seeding TTS voices from providers
/// </summary>
public class TtsVoiceSeedDto
{
    public string Id { get; set; } = string.Empty;
    public string SpeakerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Accent { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
