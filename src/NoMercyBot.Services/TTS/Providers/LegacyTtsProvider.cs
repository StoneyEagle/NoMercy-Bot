using NoMercyBot.Services.TTS.Interfaces;
using NoMercyBot.Services.TTS.Models;

namespace NoMercyBot.Services.TTS.Providers;

public class LegacyTtsProvider : ITtsProvider
{
    public string Name => "Legacy";
    public string Type => "legacy";
    public bool IsEnabled => true;
    public int Priority => 999; // Low priority as fallback
    public bool IsAvailable => true;

    public async Task<byte[]> SynthesizeAsync(string text, string voiceId, CancellationToken cancellationToken = default)
    {
        // Simple fallback implementation to prevent blocking
        await Task.Delay(100, cancellationToken); // Simulate processing
        
        // Return minimal WAV header + silence to prevent null reference errors
        byte[] wavHeader = new byte[] 
        {
            0x52, 0x49, 0x46, 0x46, 0x24, 0x08, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02, 0x00,
            0x22, 0x56, 0x00, 0x00, 0x88, 0x58, 0x01, 0x00,
            0x04, 0x00, 0x10, 0x00, 0x64, 0x61, 0x74, 0x61,
            0x00, 0x08, 0x00, 0x00
        };
        
        // Add 1 second of silence (44100 Hz * 2 channels * 2 bytes per sample)
        byte[] silence = new byte[44100 * 2 * 2];
        
        byte[] result = new byte[wavHeader.Length + silence.Length];
        Buffer.BlockCopy(wavHeader, 0, result, 0, wavHeader.Length);
        Buffer.BlockCopy(silence, 0, result, wavHeader.Length, silence.Length);
        
        return result;
    }

    public Task<byte[]> SynthesizeSsmlAsync(string ssml, string voiceId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<decimal> CalculateCostAsync(string text, string voiceId)
    {
        // Legacy provider has no cost
        return Task.FromResult(0.0m);
    }

    public Task<string> GetDefaultVoiceIdAsync()
    {
        return Task.FromResult("default");
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<TtsVoice>> GetAvailableVoicesAsync()
    {
        throw new NotImplementedException();
    }
}
