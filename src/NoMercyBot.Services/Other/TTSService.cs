using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.TTS.Interfaces;
using NoMercyBot.Services.TTS.Services;
using NoMercyBot.Services.Widgets;
using DatabaseTtsVoice = NoMercyBot.Database.Models.TtsVoice;

namespace NoMercyBot.Services.Other;

public class TtsService : IDisposable
{
    private readonly IWidgetEventService _widgetEventService;
    private readonly AppDbContext _dbContext;
    private readonly ITtsProviderService _providerService;
    private readonly ITtsUsageService _usageService;
    private readonly TtsCacheService _cacheService;

    private readonly LocalAudioPlaybackService _audioPlaybackService;

    // logger
    private readonly ILogger _logger;

    public TtsService(
        AppDbContext dbContext,
        IWidgetEventService widgetEventService,
        ILogger<TtsService> logger,
        ITtsProviderService providerService,
        ITtsUsageService usageService,
        TtsCacheService cacheService,
        LocalAudioPlaybackService audioPlaybackService
    )
    {
        _dbContext = dbContext;
        _logger = logger;
        _widgetEventService = widgetEventService;
        _providerService = providerService;
        _usageService = usageService;
        _cacheService = cacheService;
        _audioPlaybackService = audioPlaybackService;
    }

    /// <summary>
    /// Sends TTS for normal chat messages with character limit enforcement
    /// </summary>
    public async Task<TtsUsageRecord?> SendTts(
        List<ChatMessageFragment> chatMessageFragments,
        string userId,
        CancellationToken cancellationToken
    )
    {
        StringBuilder textBuilder = new();
        foreach (
            ChatMessageFragment fragment in chatMessageFragments.Where(fragment =>
                fragment.Type == "text"
            )
        )
            textBuilder.Append(fragment.Text);

        string text = textBuilder.ToString();

        return await SendTts(text, userId, cancellationToken);
    }

    /// <summary>
    /// Sends TTS for normal text with character limit enforcement
    /// </summary>
    public async Task<TtsUsageRecord?> SendTts(
        string text,
        string userId,
        CancellationToken cancellationToken
    )
    {
        if (!Config.UseTts || string.IsNullOrWhiteSpace(text))
            return null;

        bool hasWidgetSubscription = await _widgetEventService.HasWidgetSubscriptionsAsync(
            "channel.chat.message.tts"
        );
        if (!hasWidgetSubscription)
        {
            _logger.LogInformation("No widgets subscribed to TTS events, skipping SendCachedTts");
            return null;
        }

        // Apply username pronunciation overrides and text preprocessing
        text = await ApplyUsernamePronunciationsAsync(text);
        text = PreprocessTextForTts(text);

        try
        {
            // Get provider and check character limits for new synthesis
            ITtsProvider? provider = await _providerService.GetBestAvailableProviderAsync(
                text.Length
            );
            if (provider == null)
            {
                _logger.LogInformation("No TTS provider available or character limit exceeded");
                return null;
            }

            string speakerId = await GetSpeakerIdForUserAsync(userId, provider, cancellationToken);
            if (string.IsNullOrWhiteSpace(speakerId))
                return null;

            byte[] audioBytes = await provider.SynthesizeAsync(text, speakerId, cancellationToken);
            decimal cost = await provider.CalculateCostAsync(text, speakerId);

            TtsUsageRecord usageRecord = await _usageService.RecordUsageAsync(
                provider.Name,
                text.Length,
                cost
            );

            _logger.LogInformation(
                "TTS synthesis created (provider: {Provider}, characters: {Length}, cost: ${Cost})",
                provider.Name,
                text.Length,
                cost.ToString("F6")
            );

            if (audioBytes.Length > 0)
                await PublishTtsEventAsync(
                    text,
                    userId,
                    audioBytes,
                    provider.Name,
                    cost,
                    text.Length,
                    false
                );

            return usageRecord;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SendTts error: {Message}", e.Message);
            return null;
        }
    }

    /// <summary>
    /// Sends TTS with caching for repetitive content like shoutouts - NO character limits for cached content
    /// </summary>
    public async Task<TtsUsageRecord?> SendCachedTts(
        List<ChatMessageFragment> chatMessageFragments,
        string userId,
        CancellationToken cancellationToken
    )
    {
        StringBuilder textBuilder = new();
        foreach (
            ChatMessageFragment fragment in chatMessageFragments.Where(fragment =>
                fragment.Type == "text"
            )
        )
            textBuilder.Append(fragment.Text);

        string text = textBuilder.ToString();

        return await SendCachedTts(text, userId, cancellationToken);
    }

    /// <summary>
    /// Sends TTS with caching for repetitive content - NO character limits for cached content
    /// </summary>
    public async Task<TtsUsageRecord?> SendCachedTts(
        string text,
        string userId,
        CancellationToken cancellationToken
    )
    {
        if (!Config.UseTts || string.IsNullOrWhiteSpace(text))
            return null;

        bool hasWidgetSubscription = await _widgetEventService.HasWidgetSubscriptionsAsync(
            "channel.chat.message.tts"
        );
        if (!hasWidgetSubscription)
        {
            _logger.LogInformation("No widgets subscribed to TTS events");
            return null;
        }

        // Apply username pronunciation overrides and text preprocessing
        text = await ApplyUsernamePronunciationsAsync(text);
        text = PreprocessTextForTts(text);

        try
        {
            ITtsProvider? provider =
                await _providerService.GetBestAvailableProviderIgnoringLimitsAsync();
            if (provider == null)
            {
                _logger.LogInformation("No TTS provider available");
                return null;
            }

            string speakerId = await GetSpeakerIdForUserAsync(userId, provider, cancellationToken);
            if (string.IsNullOrWhiteSpace(speakerId))
                return null;

            TtsCacheEntry? cachedEntry = await _cacheService.GetCachedEntryAsync(
                text,
                speakerId,
                cancellationToken
            );

            if (cachedEntry != null)
            {
                byte[] cachedAudioBytes = await File.ReadAllBytesAsync(
                    cachedEntry.FilePath,
                    cancellationToken
                );
                await PublishTtsEventAsync(
                    text,
                    userId,
                    cachedAudioBytes,
                    cachedEntry.Provider,
                    cachedEntry.Cost,
                    text.Length,
                    true
                );

                _logger.LogInformation(
                    "Using cached TTS (saved ${Cost})",
                    cachedEntry.Cost.ToString("F6")
                );

                return new()
                {
                    ProviderId = $"{cachedEntry.Provider}_cached",
                    CharactersUsed = 0,
                    Cost = 0,
                    CreatedAt = DateTime.UtcNow,
                };
            }

            int currentUsage = await _usageService.GetCurrentUsageAsync(provider.Name);
            int remainingCharacters = await _usageService.GetRemainingCharactersAsync(
                provider.Name
            );

            if (remainingCharacters <= 0 || remainingCharacters - text.Length < 0)
            {
                _logger.LogInformation(
                    "TTS spend limit exceeded! Current usage: {CurrentUsage}, Remaining: {RemainingCharacters}, Requested: {Length}.",
                    currentUsage,
                    remainingCharacters,
                    text.Length
                );

                return null;
            }

            _logger.LogInformation(
                "Creating new TTS synthesis (provider: {Provider}, characters: {Length})",
                provider.Name,
                text.Length
            );

            byte[] audioBytes = await provider.SynthesizeAsync(text, speakerId, cancellationToken);
            decimal cost = await provider.CalculateCostAsync(text, speakerId);

            TtsUsageRecord usageRecord = await _usageService.RecordUsageAsync(
                provider.Name,
                text.Length,
                cost
            );

            if (audioBytes.Length > 0)
            {
                // Cache for future use
                await _cacheService.CreateCacheEntryAsync(
                    text,
                    speakerId,
                    provider.Name,
                    audioBytes,
                    cost,
                    cancellationToken
                );
                await PublishTtsEventAsync(
                    text,
                    userId,
                    audioBytes,
                    provider.Name,
                    cost,
                    text.Length,
                    false
                );

                _logger.LogInformation(
                    "Created new TTS synthesis (cost ${Cost})",
                    cost.ToString("F6")
                );
            }

            return usageRecord;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SendCachedTts error: {Message}", e.Message);
            return null;
        }
    }

    /// <summary>
    /// Synthesizes multiple text segments with different voices and concatenates the audio.
    /// Each segment is an independent TTS call; the resulting MP3 chunks are concatenated in order.
    /// </summary>
    public async Task SendMultiVoiceTtsAsync(
        List<(string text, string userId)> segments,
        string publishUserId,
        CancellationToken cancellationToken
    )
    {
        if (!Config.UseTts || segments.Count == 0)
            return;

        bool hasWidgetSubscription = await _widgetEventService.HasWidgetSubscriptionsAsync(
            "channel.chat.message.tts"
        );
        if (!hasWidgetSubscription)
        {
            _logger.LogInformation("No widgets subscribed to TTS events, skipping multi-voice TTS");
            return;
        }

        try
        {
            ITtsProvider? provider =
                await _providerService.GetBestAvailableProviderIgnoringLimitsAsync();
            if (provider == null)
            {
                _logger.LogInformation("No TTS provider available for multi-voice TTS");
                return;
            }

            using MemoryStream combinedAudio = new();
            StringBuilder combinedText = new();
            decimal totalCost = 0;
            int totalCharacters = 0;

            foreach ((string text, string userId) in segments)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                string processedText = PreprocessTextForTts(
                    await ApplyUsernamePronunciationsAsync(text)
                );
                string? speakerId = await GetSpeakerIdForUserAsync(
                    userId,
                    provider,
                    cancellationToken
                );
                if (string.IsNullOrWhiteSpace(speakerId))
                    continue;

                byte[] audioBytes = await provider.SynthesizeAsync(
                    processedText,
                    speakerId,
                    cancellationToken
                );
                decimal cost = await provider.CalculateCostAsync(processedText, speakerId);

                if (audioBytes.Length > 0)
                {
                    combinedAudio.Write(audioBytes, 0, audioBytes.Length);
                    totalCost += cost;
                    totalCharacters += processedText.Length;
                }

                if (combinedText.Length > 0)
                    combinedText.Append(' ');
                combinedText.Append(text);
            }

            byte[] finalAudio = combinedAudio.ToArray();
            if (finalAudio.Length == 0)
                return;

            await _usageService.RecordUsageAsync(provider.Name, totalCharacters, totalCost);

            _logger.LogInformation(
                "Multi-voice TTS created (provider: {Provider}, segments: {Segments}, characters: {Length}, cost: ${Cost})",
                provider.Name,
                segments.Count,
                totalCharacters,
                totalCost.ToString("F6")
            );

            await PublishTtsEventAsync(
                combinedText.ToString(),
                publishUserId,
                finalAudio,
                provider.Name,
                totalCost,
                totalCharacters,
                false
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Multi-voice TTS error: {Message}", e.Message);
        }
    }

    private async Task PublishTtsEventAsync(
        string text,
        string userId,
        byte[] audioBytes,
        string providerName,
        decimal cost,
        int characterCount,
        bool cached
    )
    {
        string audioBase64 = $"data:audio/wav;base64,{Convert.ToBase64String(audioBytes)}";

        // Save to disk if configured
        if (Config.SaveTtsToDisk)
        {
            string filePath = Path.Combine(
                AppFiles.CachePath,
                "tts",
                $"{userId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.wav"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
            await File.WriteAllBytesAsync(filePath, audioBytes);
        }

        var ttsEvent = new
        {
            text = text,
            user = new { id = userId },
            audioBase64 = audioBase64,
            provider = providerName,
            cost = cost,
            characterCount = characterCount,
            cached = cached,
        };

        await _widgetEventService.PublishEventAsync("channel.chat.message.tts", ttsEvent);

        // Play locally if configured
        if (Config.PlayTtsLocally)
            _ = Task.Run(async () =>
                await _audioPlaybackService.PlayAudioAsync(audioBytes, CancellationToken.None)
            );
    }

    public async Task<string?> GetSpeakerIdForUserAsync(
        string userId,
        CancellationToken cancellationToken
    )
    {
        ITtsProvider? provider =
            await _providerService.GetBestAvailableProviderIgnoringLimitsAsync();
        if (provider == null)
            return null;
        return await GetSpeakerIdForUserAsync(userId, provider, cancellationToken);
    }

    public async Task<string?> GetSpeakerIdForUserAsync(
        string userId,
        ITtsProvider provider,
        CancellationToken cancellationToken
    )
    {
        // Try to get user's voice preference
        string? userVoiceId = await _dbContext
            .UserTtsVoices.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.TtsVoiceId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(userVoiceId))
        {
            // Check if user has a provider-specific voice preference (format: "provider:voiceId")
            if (userVoiceId.Contains(':'))
            {
                string[] parts = userVoiceId.Split(':', 2);
                string preferredProvider = parts[0];
                string speakerId = parts[1];

                if (
                    string.Equals(
                        preferredProvider,
                        provider.Name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // Exact provider match
                    DatabaseTtsVoice? voice = await _dbContext
                        .TtsVoices.AsNoTracking()
                        .Where(v =>
                            v.Id == userVoiceId && v.IsActive && v.Provider == provider.Name
                        )
                        .FirstOrDefaultAsync(cancellationToken);

                    if (voice != null)
                        return voice.SpeakerId;
                }
                else
                {
                    // Different provider - try to find the same speaker ID in the current provider
                    DatabaseTtsVoice? crossProviderVoice = await _dbContext
                        .TtsVoices.AsNoTracking()
                        .Where(v =>
                            v.SpeakerId == speakerId && v.IsActive && v.Provider == provider.Name
                        )
                        .FirstOrDefaultAsync(cancellationToken);

                    if (crossProviderVoice != null)
                        return crossProviderVoice.SpeakerId;
                }
            }
            else if (string.Equals(provider.Name, "Legacy", StringComparison.OrdinalIgnoreCase))
            {
                // Legacy voice format
                DatabaseTtsVoice? voice = await _dbContext
                    .TtsVoices.AsNoTracking()
                    .Where(v => v.Id == userVoiceId && v.IsActive && v.Provider == "Legacy")
                    .FirstOrDefaultAsync(cancellationToken);

                if (voice != null)
                    return voice.SpeakerId;
            }

            // User has a preference but it doesn't match this provider — use default
            // without overwriting their stored preference
            return await provider.GetDefaultVoiceIdAsync();
        }

        // No preference at all — assign a random English voice for variety
        List<DatabaseTtsVoice> englishVoices = await _dbContext
            .TtsVoices.AsNoTracking()
            .Where(v => v.Provider == provider.Name && v.IsActive && v.Locale.StartsWith("en-"))
            .ToListAsync(cancellationToken);

        string assignedSpeakerId;
        string assignedVoiceId;

        if (englishVoices.Count > 0)
        {
            DatabaseTtsVoice randomVoice = englishVoices[Random.Shared.Next(englishVoices.Count)];
            assignedSpeakerId = randomVoice.SpeakerId;
            assignedVoiceId = randomVoice.Id;
        }
        else
        {
            assignedSpeakerId = await provider.GetDefaultVoiceIdAsync();
            assignedVoiceId = $"{provider.Name}:{assignedSpeakerId}";
        }

        await _dbContext
            .UserTtsVoices.Upsert(new() { UserId = userId, TtsVoiceId = assignedVoiceId })
            .On(u => u.UserId)
            .WhenMatched(
                (existing, incoming) =>
                    new() { TtsVoiceId = incoming.TtsVoiceId, SetAt = DateTime.UtcNow }
            )
            .RunAsync(cancellationToken);

        return assignedSpeakerId;
    }

    /// <summary>
    /// Builds a segment for use with SynthesizeMultiVoiceSsmlAsync.
    /// Optionally accepts a rate like "+20%" to speed up or "-20%" to slow down.
    /// </summary>
    public static (string text, string voiceId) Segment(
        string voiceId,
        string text,
        string? rate = null
    )
    {
        if (rate != null)
        {
            // Encode rate into voiceId with a pipe separator
            return (text, $"{voiceId}|rate:{rate}");
        }

        return (text, voiceId);
    }

    /// <summary>
    /// Creates a silence segment for use in SynthesizeMultiVoiceSsmlAsync.
    /// </summary>
    public static (string ssml, string voiceId) Silence(int milliseconds)
    {
        return (milliseconds.ToString(), "silence");
    }

    /// <summary>
    /// Synthesizes multiple SSML segments in parallel (each with its own voice),
    /// stitches them together with FFmpeg, and returns combined audio.
    /// Supports silence segments via TtsService.Silence(ms).
    /// </summary>
    public async Task<(string? audioBase64, int durationMs)> SynthesizeMultiVoiceSsmlAsync(
        List<(string ssml, string voiceId)> segments,
        CancellationToken cancellationToken = default
    )
    {
        ITtsProvider? provider =
            await _providerService.GetBestAvailableProviderIgnoringLimitsAsync();
        if (provider == null)
            return (null, 0);

        // Synthesize all voice segments in parallel, collect silence segments separately
        List<(int index, byte[] audio)> indexedSegments = [];
        List<Task<(int index, byte[]? audio)>> tasks = [];

        for (int i = 0; i < segments.Count; i++)
        {
            int idx = i;
            if (segments[i].voiceId == "silence")
            {
                // Silence segments are generated later via FFmpeg
                indexedSegments.Add((idx, []));
                continue;
            }

            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            string voiceId = segments[idx].voiceId;
                            string text = PreprocessTextForTts(segments[idx].ssml);
                            byte[] audio;

                            // Check if voiceId contains prosody overrides (e.g. "en-IN-PrabhatNeural|rate:+20%")
                            if (voiceId.Contains('|'))
                            {
                                string[] parts = voiceId.Split('|');
                                voiceId = parts[0];
                                string rate = "+0%";
                                foreach (string part in parts.Skip(1))
                                {
                                    if (part.StartsWith("rate:"))
                                        rate = part[5..];
                                }

                                string[] voiceParts = voiceId.Split('-');
                                string locale =
                                    voiceParts.Length >= 2
                                        ? $"{voiceParts[0]}-{voiceParts[1]}"
                                        : "en-US";
                                string sanitized =
                                    System.Security.SecurityElement.Escape(text) ?? string.Empty;
                                string ssml =
                                    $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{locale}'>"
                                    + $"<voice name='{voiceId}'>"
                                    + $"<prosody rate='{rate}'>"
                                    + sanitized
                                    + "</prosody></voice></speak>";
                                audio = await provider.SynthesizeSsmlAsync(
                                    ssml,
                                    voiceId,
                                    cancellationToken
                                );
                            }
                            else
                            {
                                audio = await provider.SynthesizeAsync(
                                    text,
                                    voiceId,
                                    cancellationToken
                                );
                            }

                            return (idx, audio.Length > 0 ? audio : (byte[]?)null);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                "Multi-voice segment {Index} failed (voice: {Voice}): {Error}",
                                idx,
                                segments[idx].voiceId,
                                ex.Message
                            );
                            return (idx, (byte[]?)null);
                        }
                    },
                    cancellationToken
                )
            );
        }

        var results = await Task.WhenAll(tasks);
        foreach (var (index, audio) in results)
        {
            if (audio != null)
                indexedSegments.Add((index, audio));
        }

        if (indexedSegments.All(s => s.audio.Length == 0))
            return (null, 0);

        // Sort by original index
        indexedSegments.Sort((a, b) => a.index.CompareTo(b.index));

        // Always use FFmpeg to ensure correct audio format
        string tempDir = Path.Combine(Path.GetTempPath(), $"nomercy_tts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Write each segment to a temp file (voice segments as mp3, silence via ffmpeg)
            List<string> inputFiles = [];
            for (int i = 0; i < indexedSegments.Count; i++)
            {
                var (index, audio) = indexedSegments[i];
                string path = Path.Combine(tempDir, $"seg_{i}.mp3");

                if (segments[index].voiceId == "silence")
                {
                    // Generate silence with FFmpeg
                    int durationMs = int.TryParse(segments[index].ssml, out int ms) ? ms : 500;
                    double durationSec = durationMs / 1000.0;
                    using System.Diagnostics.Process silenceProc = new()
                    {
                        StartInfo = new()
                        {
                            FileName = "ffmpeg",
                            Arguments =
                                $"-y -f lavfi -i anullsrc=r=24000:cl=mono -t {durationSec:F3} -c:a libmp3lame -b:a 48k \"{path}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                        },
                    };
                    silenceProc.Start();
                    await silenceProc.WaitForExitAsync(cancellationToken);
                }
                else
                {
                    await File.WriteAllBytesAsync(path, audio, cancellationToken);
                }

                inputFiles.Add(path);
            }

            // Build FFmpeg concat file
            string concatFile = Path.Combine(tempDir, "concat.txt");
            string concatContent = string.Join(
                "\n",
                inputFiles.Select(f => $"file '{f.Replace("\\", "/")}'")
            );
            await File.WriteAllTextAsync(concatFile, concatContent, cancellationToken);

            string outputFile = Path.Combine(tempDir, "output.mp3");

            // Run FFmpeg to concatenate
            using System.Diagnostics.Process ffmpeg = new()
            {
                StartInfo = new()
                {
                    FileName = "ffmpeg",
                    Arguments =
                        $"-y -f concat -safe 0 -i \"{concatFile}\" -c copy \"{outputFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            ffmpeg.Start();
            await ffmpeg.WaitForExitAsync(cancellationToken);

            if (ffmpeg.ExitCode != 0 || !File.Exists(outputFile))
            {
                string error = await ffmpeg.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning(
                    "FFmpeg concat failed (exit {Code}): {Error}",
                    ffmpeg.ExitCode,
                    error
                );
                return (null, 0);
            }

            byte[] combinedBytes = await File.ReadAllBytesAsync(outputFile, cancellationToken);
            string audioBase64 = $"data:audio/mp3;base64,{Convert.ToBase64String(combinedBytes)}";

            // Estimate duration from file size (MP3 at ~48kbps)
            int estimatedDurationMs = (int)(combinedBytes.Length * 8.0 / 48.0);

            return (audioBase64, estimatedDurationMs);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }

    /// <summary>
    /// Synthesizes SSML and returns both audio base64 and duration in milliseconds
    /// </summary>
    public async Task<(string? audioBase64, int durationMs)> SynthesizeSsmlAsync(
        string ssml,
        string voiceId,
        CancellationToken cancellationToken = default
    )
    {
        ITtsProvider? provider =
            await _providerService.GetBestAvailableProviderIgnoringLimitsAsync();
        if (provider == null)
        {
            _logger.LogInformation("No TTS provider available");
            return (null, 0);
        }

        TtsCacheEntry? cachedEntry = await _cacheService.GetCachedEntryAsync(
            ssml,
            voiceId,
            cancellationToken
        );

        if (cachedEntry != null)
        {
            byte[] cachedAudioBytes = await File.ReadAllBytesAsync(
                cachedEntry.FilePath,
                cancellationToken
            );
            _logger.LogInformation(
                "Using cached TTS (saved ${Cost})",
                cachedEntry.Cost.ToString("F6")
            );

            string audioBase64 =
                $"data:audio/wav;base64,{Convert.ToBase64String(cachedAudioBytes)}";
            int durationMs = GetWavDurationMs(cachedAudioBytes);

            return (audioBase64, durationMs);
        }

        int currentUsage = await _usageService.GetCurrentUsageAsync(provider.Name);
        int remainingCharacters = await _usageService.GetRemainingCharactersAsync(provider.Name);

        if (remainingCharacters <= 0 || remainingCharacters - ssml.Length < 0)
        {
            _logger.LogInformation(
                "TTS spend limit exceeded! Current usage: {CurrentUsage}, Remaining: {RemainingCharacters}, Requested: {Length}.",
                currentUsage,
                remainingCharacters,
                ssml.Length
            );
            return (null, 0);
        }

        _logger.LogInformation(
            "Creating new TTS synthesis (provider: {Provider}, characters: {Length})",
            provider.Name,
            ssml.Length
        );

        byte[] audioBytes = await provider.SynthesizeSsmlAsync(ssml, voiceId, cancellationToken);
        decimal cost = await provider.CalculateCostAsync(ssml, voiceId);

        await _usageService.RecordUsageAsync(provider.Name, ssml.Length, cost);

        if (audioBytes.Length > 0)
        {
            _logger.LogInformation("Created new TTS synthesis (cost ${Cost})", cost.ToString("F6"));

            await _cacheService.CreateCacheEntryAsync(
                ssml,
                voiceId,
                provider.Name,
                audioBytes,
                cost,
                cancellationToken
            );

            string audioBase64 = $"data:audio/wav;base64,{Convert.ToBase64String(audioBytes)}";
            int durationMs = GetWavDurationMs(audioBytes);

            return (audioBase64, durationMs);
        }

        return (null, 0);
    }

    /// <summary>
    /// Calculates the duration of a WAV file from its byte array.
    /// Reads the sample rate and byte rate from the WAV header to calculate duration.
    /// </summary>
    private static int GetWavDurationMs(byte[] wavBytes)
    {
        try
        {
            if (wavBytes.Length < 44)
                return 0; // WAV header is at least 44 bytes

            // WAV format: bytes 24-27 = sample rate, bytes 32-33 = block align, bytes 40-43 = subchunk2 size (data size)
            int sampleRate = BitConverter.ToInt32(wavBytes, 24);
            int dataSize = BitConverter.ToInt32(wavBytes, 40);

            if (sampleRate <= 0)
                return 0;

            // The block align at bytes 32-33 tells us bytes per sample frame
            int blockAlign = BitConverter.ToInt16(wavBytes, 32);

            if (blockAlign <= 0)
                return 0;

            int totalSamples = dataSize / blockAlign;
            int durationMs = (int)((totalSamples * 1000L) / sampleRate);

            return durationMs;
        }
        catch (Exception ex)
        {
            // If parsing fails, return 0
            return 0;
        }
    }

    /// <summary>
    /// Preprocesses text for TTS by expanding abbreviations, leetspeak, and internet slang
    /// into speakable words. Call this on text before sending to TTS synthesis.
    /// </summary>
    public static string PreprocessTextForTts(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Leetspeak number replacements
        text = text.Replace("1337", "leet");

        // Split into words and process each
        string[] words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            // Skip @mentions (username pronunciations handle those)
            if (words[i].StartsWith("@"))
                continue;

            string lower = words[i].TrimEnd('.', ',', '!', '?', ';', ':').ToLowerInvariant();
            string suffix = words[i].Length > lower.Length ? words[i][lower.Length..] : "";

            string? replacement = lower switch
            {
                // Common abbreviations
                "lol" => "lawl",
                "lmao" => "laughing my ass off",
                "lmfao" => "laughing my fucking ass off",
                "rofl" => "roffle",
                "roflmao" => "roffle mow",
                "omg" => "oh my god",
                "omfg" => "oh my god",
                "wtf" => "what the fuck",
                "wth" => "what the hell",
                "stfu" => "shut the fuck up",
                "gtfo" => "get the fuck out",
                "af" => "as fuck",
                "imo" => "in my opinion",
                "imho" => "in my humble opinion",
                "tbh" => "to be honest",
                "tbf" => "to be fair",
                "idk" => "i don't know",
                "idc" => "i don't care",
                "idgaf" => "i don't give a fuck",
                "irl" => "in real life",
                "afk" => "away from keyboard",
                "brb" => "be right back",
                "brt" => "be right there",
                "btw" => "by the way",
                "gg" => "good game",
                "ggs" => "good games",
                "ggwp" => "good game well played",
                "wp" => "well played",
                "glhf" => "good luck have fun",
                "gl" => "good luck",
                "hf" => "have fun",
                "ty" => "thank you",
                "tyvm" => "thank you very much",
                "tysm" => "thank you so much",
                "thx" => "thanks",
                "np" => "no problem",
                "nvm" => "never mind",
                "smh" => "shaking my head",
                "ffs" => "for fucks sake",
                "jk" => "just kidding",
                "ngl" => "not gonna lie",
                "fr" => "for real",
                "frfr" => "for real for real",
                "rn" => "right now",
                "istg" => "i swear to god",
                "icymi" => "in case you missed it",
                "ftw" => "for the win",
                "fyi" => "for your information",
                "tldr" => "too long didn't read",
                "goat" => "greatest of all time",
                "goated" => "greatest of all time",
                "pog" => "pog",
                "poggers" => "poggers",
                "pogchamp" => "pog champ",
                "kappa" => "kappa",
                "copium" => "copium",
                "hopium" => "hopium",
                "copege" => "cope age",
                "sadge" => "sad",
                "pepega" => "pepe ga",
                "monkas" => "monka s",
                "kekw" => "kek w",
                "5head" => "five head",
                "4head" => "four head",
                "3head" => "three head",
                "pepehands" => "pepe hands",
                "pepelaugh" => "pepe laugh",
                "ez" => "easy",
                "ezclap" => "easy clap",
                "rekt" => "wrecked",
                "noob" => "noob",
                "n00b" => "noob",
                "pwned" => "owned",
                "pls" => "please",
                "plz" => "please",
                "u" when i > 0 || words.Length > 1 => "you",
                "ur" => "your",
                "r" when i > 0 || words.Length > 1 => "are",
                "w" when i > 0 || words.Length > 1 => "with",
                "b4" => "before",
                "2" when i > 0 && !char.IsDigit(words[i - 1].Last()) => "to",
                "4" when i > 0 && !char.IsDigit(words[i - 1].Last()) => "for",
                "gr8" => "great",
                "m8" => "mate",
                "l8r" => "later",
                "h8" => "hate",
                "sk8" => "skate",
                "w8" => "wait",
                "str8" => "straight",
                "2day" => "today",
                "2nite" => "tonight",
                "2moro" or "2morrow" => "tomorrow",
                "w/" => "with",
                "w/o" => "without",
                "bc" or "cuz" or "coz" => "because",
                "dm" => "d m",
                "dms" => "d m s",
                "og" => "oh gee",
                "op" => "oh pee",
                "mvp" => "m v p",
                "sus" => "sus",
                "sussy" => "sussy",
                "bussin" => "bussin",
                "sheesh" => "sheesh",
                "yeet" => "yeet",
                "yeeted" => "yeeted",
                "vibe" => "vibe",
                "vibes" => "vibes",
                "lowkey" => "low key",
                "highkey" => "high key",
                "deadass" => "dead ass",
                "simp" => "simp",
                "based" => "based",
                "cringe" => "cringe",
                "mid" => "mid",
                "slay" => "slay",
                "fam" => "fam",
                "lit" => "lit",
                "bet" => "bet",
                "cap" => "cap",
                "nocap" => "no cap",
                "oof" => "oof",
                "yikes" => "yikes",
                "bruh" => "bruh",
                "bro" => "bro",
                "fml" => "fuck my life",
                "tmi" => "too much information",
                "hmu" => "hit me up",
                "lmk" => "let me know",
                "wya" => "where you at",
                "wyd" => "what you doing",
                "smth" => "something",
                "sth" => "something",
                "abt" => "about",
                "diff" => "difference",
                "prob" => "probably",
                "probs" => "probably",
                "obv" => "obviously",
                "def" => "definitely",
                _ => null,
            };

            if (replacement != null)
                words[i] = replacement + suffix;
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Replaces usernames in TTS text with their pronunciation overrides from the Channel model.
    /// Handles @mentions and bare username occurrences.
    /// </summary>
    public async Task<string> ApplyUsernamePronunciationsAsync(string text)
    {
        // Get all channels that have a pronunciation override set
        List<Channel> channelsWithPronunciation = await _dbContext
            .Channels.AsNoTracking()
            .Where(c => c.UsernamePronunciation != null && c.UsernamePronunciation != "")
            .Select(c => new Channel
            {
                Name = c.Name,
                UsernamePronunciation = c.UsernamePronunciation,
            })
            .ToListAsync();

        if (channelsWithPronunciation.Count == 0)
            return text;

        foreach (Channel channel in channelsWithPronunciation)
        {
            if (
                string.IsNullOrEmpty(channel.Name)
                || string.IsNullOrEmpty(channel.UsernamePronunciation)
            )
                continue;

            // Replace @username mentions
            text = Regex.Replace(
                text,
                "@" + Regex.Escape(channel.Name),
                channel.UsernamePronunciation,
                RegexOptions.IgnoreCase
            );

            // Replace bare username occurrences (whole word only)
            text = Regex.Replace(
                text,
                @"\b" + Regex.Escape(channel.Name) + @"\b",
                channel.UsernamePronunciation,
                RegexOptions.IgnoreCase
            );
        }

        return text;
    }

    public void Dispose() { }
}
