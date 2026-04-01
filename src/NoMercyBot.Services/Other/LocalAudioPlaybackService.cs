using System.Diagnostics;
using System.Runtime.InteropServices;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.SystemCalls;
using Serilog.Events;

namespace NoMercyBot.Services.Other;

/// <summary>
/// Service for playing TTS audio locally through system speakers
/// </summary>
public class LocalAudioPlaybackService
{
    /// <summary>
    /// Plays audio bytes through the system's default audio output device
    /// </summary>
    public async Task PlayAudioAsync(
        byte[] audioBytes,
        CancellationToken cancellationToken = default
    )
    {
        if (!Config.PlayTtsLocally)
        {
            Logger.Twitch("Local TTS playback is disabled in config", LogEventLevel.Debug);
            return;
        }

        try
        {
            // Create a temporary file for audio playback
            string tempAudioFile = Path.GetTempFileName();
            string audioFilePath = Path.ChangeExtension(tempAudioFile, ".wav");

            // Write audio bytes to temporary file
            await File.WriteAllBytesAsync(audioFilePath, audioBytes, cancellationToken);

            // Play the audio file using platform-specific method
            await PlayAudioFileAsync(audioFilePath, cancellationToken);

            // Clean up temporary file after a delay to ensure playback completes
            _ = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)
                .ContinueWith(
                    _ =>
                    {
                        try
                        {
                            if (File.Exists(audioFilePath))
                                File.Delete(audioFilePath);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    },
                    cancellationToken
                );
        }
        catch (Exception ex)
        {
            Logger.Twitch($"Error playing TTS audio locally: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private static async Task PlayAudioFileAsync(
        string audioFilePath,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                // Use Windows Media Player command line for audio playback
                await RunProcessAsync(
                    "powershell",
                    $"-c \"(New-Object Media.SoundPlayer '{audioFilePath}').PlaySync()\"",
                    cancellationToken
                );
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                // Use aplay on Linux
                await RunProcessAsync("aplay", $"\"{audioFilePath}\"", cancellationToken);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                // Use afplay on macOS
                await RunProcessAsync("afplay", $"\"{audioFilePath}\"", cancellationToken);
            else
                Logger.Twitch(
                    "Local audio playback not supported on this platform",
                    LogEventLevel.Warning
                );
        }
        catch (Exception ex)
        {
            Logger.Twitch(
                $"Error executing audio playback command: {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }

    private static async Task RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken
    )
    {
        using Process process = new()
        {
            StartInfo = new()
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
    }
}
