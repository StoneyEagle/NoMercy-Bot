using NoMercyBot.Services.TTS.Interfaces;
using NoMercyBot.Services.TTS.Models;

namespace NoMercyBot.Services.TTS.Providers;

public abstract class TtsProviderBase : ITtsProvider
{
    protected TtsProviderBase(string name, string type, bool isEnabled = true, int priority = 100)
    {
        Name = name;
        Type = type;
        IsEnabled = isEnabled;
        Priority = priority;
    }

    public string Name { get; }
    public string Type { get; }
    public bool IsEnabled { get; }
    public int Priority { get; }
    public bool IsAvailable { get; }

    public abstract Task<byte[]> SynthesizeAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken = default
    );

    public abstract Task<byte[]> SynthesizeSsmlAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken
    );

    public abstract Task<bool> IsAvailableAsync();
    public abstract Task<List<TtsVoice>> GetAvailableVoicesAsync();

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task<int> GetCharacterCountAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(0);

        // Basic character count implementation
        return Task.FromResult(text.Length);
    }

    public abstract Task<decimal> CalculateCostAsync(string text, string voiceId);
    public abstract Task<string> GetDefaultVoiceIdAsync();

    protected virtual string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.Replace("\"", "&quot;")
            .Replace("&", "&amp;")
            .Replace("'", "&apos;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    protected virtual void ValidateInputs(string text, string voiceId)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));
        if (string.IsNullOrWhiteSpace(voiceId))
            throw new ArgumentException("VoiceId cannot be empty.", nameof(voiceId));
    }
}
