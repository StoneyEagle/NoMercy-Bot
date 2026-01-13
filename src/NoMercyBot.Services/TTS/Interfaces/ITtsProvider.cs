using NoMercyBot.Services.TTS.Models;

namespace NoMercyBot.Services.TTS.Interfaces;

public interface ITtsProvider
{
    string Name { get; }
    string Type { get; }
    bool IsEnabled { get; }
    int Priority { get; }
    bool IsAvailable { get; }

    Task<byte[]> SynthesizeAsync(string text, string voiceId, CancellationToken cancellationToken = default);
    Task<byte[]> SynthesizeSsmlAsync(string ssml, string voiceId, CancellationToken cancellationToken = default);
    Task<decimal> CalculateCostAsync(string text, string voiceId);
    Task<string> GetDefaultVoiceIdAsync();
    Task InitializeAsync();
    Task<bool> IsAvailableAsync();
    Task<List<TtsVoice>> GetAvailableVoicesAsync();
}