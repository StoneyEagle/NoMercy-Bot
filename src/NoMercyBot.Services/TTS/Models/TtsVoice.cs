namespace NoMercyBot.Services.TTS.Models;

public class TtsVoice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}
