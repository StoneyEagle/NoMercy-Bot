using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Widgets;
using NoMercyBot.Services.TTS.Interfaces;
using NoMercyBot.Services.TTS.Services;
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

    public TtsService(AppDbContext dbContext, IWidgetEventService widgetEventService, ILogger<TtsService> logger,
        ITtsProviderService providerService, ITtsUsageService usageService, TtsCacheService cacheService,
        LocalAudioPlaybackService audioPlaybackService)
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
    public async Task<TtsUsageRecord?> SendTts(List<ChatMessageFragment> chatMessageFragments, string userId,
        CancellationToken cancellationToken)
    {
        StringBuilder textBuilder = new();
        foreach (ChatMessageFragment fragment in chatMessageFragments.Where(fragment => fragment.Type == "text"))
            textBuilder.Append(fragment.Text);

        string text = textBuilder.ToString();

        return await SendTts(text, userId, cancellationToken);
    }

    /// <summary>
    /// Sends TTS for normal text with character limit enforcement
    /// </summary>
    public async Task<TtsUsageRecord?> SendTts(string text, string userId, CancellationToken cancellationToken)
    {
        if (!Config.UseTts || string.IsNullOrWhiteSpace(text)) return null;
        
        bool hasWidgetSubscription = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
        if (!hasWidgetSubscription)
        {
            _logger.LogInformation("No widgets subscribed to TTS events, skipping SendCachedTts");
            return null;
        }

        try
        {
            // Get provider and check character limits for new synthesis
            ITtsProvider? provider = await _providerService.GetBestAvailableProviderAsync(text.Length);
            if (provider == null)
            {
                _logger.LogInformation("No TTS provider available or character limit exceeded");
                return null;
            }

            string speakerId = await GetSpeakerIdForUserAsync(userId, provider, cancellationToken);
            if (string.IsNullOrWhiteSpace(speakerId)) return null;

            byte[] audioBytes = await provider.SynthesizeAsync(text, speakerId, cancellationToken);
            decimal cost = await provider.CalculateCostAsync(text, speakerId);

            TtsUsageRecord usageRecord = await _usageService.RecordUsageAsync(provider.Name, text.Length, cost);
            
            _logger.LogInformation("TTS synthesis created (provider: {Provider}, characters: {Length}, cost: ${Cost})",
                provider.Name, text.Length, cost.ToString("F6"));

            if (audioBytes.Length > 0)
                await PublishTtsEventAsync(text, userId, audioBytes, provider.Name, cost, text.Length, false);

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
    public async Task<TtsUsageRecord?> SendCachedTts(List<ChatMessageFragment> chatMessageFragments, string userId,
        CancellationToken cancellationToken)
    {
        StringBuilder textBuilder = new();
        foreach (ChatMessageFragment fragment in chatMessageFragments.Where(fragment => fragment.Type == "text"))
            textBuilder.Append(fragment.Text);

        string text = textBuilder.ToString();

        return await SendCachedTts(text, userId, cancellationToken);
    }

    /// <summary>
    /// Sends TTS with caching for repetitive content - NO character limits for cached content
    /// </summary>
    public async Task<TtsUsageRecord?> SendCachedTts(string text, string userId, CancellationToken cancellationToken)
    {
        if (!Config.UseTts || string.IsNullOrWhiteSpace(text)) return null;
        
        bool hasWidgetSubscription = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
        if (!hasWidgetSubscription)
        {
            _logger.LogInformation("No widgets subscribed to TTS events");
            return null;
        }

        try
        {
            ITtsProvider? provider = await _providerService.GetBestAvailableProviderIgnoringLimitsAsync();
            if (provider == null)
            {
                _logger.LogInformation("No TTS provider available");
                return null;
            }

            string speakerId = await GetSpeakerIdForUserAsync(userId, provider, cancellationToken);
            if (string.IsNullOrWhiteSpace(speakerId)) return null;

            TtsCacheEntry? cachedEntry =
                await _cacheService.GetCachedEntryAsync(text, speakerId, cancellationToken);

            if (cachedEntry != null)
            {
                byte[] cachedAudioBytes = await File.ReadAllBytesAsync(cachedEntry.FilePath, cancellationToken);
                await PublishTtsEventAsync(text, userId, cachedAudioBytes, cachedEntry.Provider, cachedEntry.Cost,
                    text.Length, true);

                _logger.LogInformation("Using cached TTS (saved ${Cost})", cachedEntry.Cost.ToString("F6"));

                return new()
                {
                    ProviderId = $"{cachedEntry.Provider}_cached",
                    CharactersUsed = 0,
                    Cost = 0,
                    CreatedAt = DateTime.UtcNow
                };
            }

            int currentUsage = await _usageService.GetCurrentUsageAsync(provider.Name);
            int remainingCharacters = await _usageService.GetRemainingCharactersAsync(provider.Name);

            if (remainingCharacters <= 0 || remainingCharacters - text.Length < 0)
            {
                _logger.LogInformation(
                    "TTS spend limit exceeded! Current usage: {CurrentUsage}, Remaining: {RemainingCharacters}, Requested: {Length}.",
                    currentUsage, remainingCharacters, text.Length);
                
                return new()
                {
                    ProviderId = $"{provider.Name}_HARD_BLOCKED",
                    CharactersUsed = 0,
                    Cost = 0,
                    CreatedAt = DateTime.UtcNow
                };
            }

            _logger.LogInformation("Creating new TTS synthesis (provider: {Provider}, characters: {Length})", provider.Name, text.Length);
            
            byte[] audioBytes = await provider.SynthesizeAsync(text, speakerId, cancellationToken);
            decimal cost = await provider.CalculateCostAsync(text, speakerId);

            TtsUsageRecord usageRecord = await _usageService.RecordUsageAsync(provider.Name, text.Length, cost);

            if (audioBytes.Length > 0)
            {
                // Cache for future use
                await _cacheService.CreateCacheEntryAsync(text, speakerId, provider.Name, audioBytes, cost, cancellationToken);
                await PublishTtsEventAsync(text, userId, audioBytes, provider.Name, cost, text.Length, false);

                _logger.LogInformation("Created new TTS synthesis (cost ${Cost})", cost.ToString("F6"));
            }

            return usageRecord;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SendCachedTts error: {Message}", e.Message);
            return null;
        }
    }

    private async Task PublishTtsEventAsync(string text, string userId, byte[] audioBytes, string providerName,
        decimal cost, int characterCount, bool cached)
    {
        string audioBase64 = $"data:audio/wav;base64,{Convert.ToBase64String(audioBytes)}";

        // Save to disk if configured
        if (Config.SaveTtsToDisk)
        {
            string filePath = Path.Combine(AppFiles.CachePath, "tts", $"{userId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.wav");
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
            cached = cached
        };

        await _widgetEventService.PublishEventAsync("channel.chat.message.tts", ttsEvent);

        // Play locally if configured
        if (Config.PlayTtsLocally)
            _ = Task.Run(async () =>
                await _audioPlaybackService.PlayAudioAsync(audioBytes, CancellationToken.None));
    }

    private async Task<string?> GetSpeakerIdForUserAsync(string userId, ITtsProvider provider,
        CancellationToken cancellationToken)
    {
        // Try to get user's voice preference
        string? userVoiceId = await _dbContext.UserTtsVoices
            .AsNoTracking()
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

                if (string.Equals(preferredProvider, provider.Name, StringComparison.OrdinalIgnoreCase))
                {
                    DatabaseTtsVoice? voice = await _dbContext.TtsVoices
                        .AsNoTracking()
                        .Where(v => v.Id == userVoiceId && v.IsActive && v.Provider == provider.Name)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (voice != null) return voice.SpeakerId;
                }
            }
            else if (string.Equals(provider.Name, "Legacy", StringComparison.OrdinalIgnoreCase))
            {
                // Legacy voice format
                DatabaseTtsVoice? voice = await _dbContext.TtsVoices
                    .AsNoTracking()
                    .Where(v => v.Id == userVoiceId && v.IsActive && v.Provider == "Legacy")
                    .FirstOrDefaultAsync(cancellationToken);

                if (voice != null) return voice.SpeakerId;
            }
        }

        // Use provider's default voice
        string defaultVoiceId = await provider.GetDefaultVoiceIdAsync();

        // Save as user's preference
        string providerVoiceId = $"{provider.Name}:{defaultVoiceId}";
        await _dbContext.UserTtsVoices.Upsert(new()
            {
                UserId = userId,
                TtsVoiceId = providerVoiceId
            })
            .On(u => u.UserId)
            .WhenMatched((existing, incoming) => new()
            {
                TtsVoiceId = incoming.TtsVoiceId,
                SetAt = DateTime.UtcNow
            })
            .RunAsync(cancellationToken);

        return defaultVoiceId;
    }

    public async Task<string?> SynthesizeSsmlAsync(
        string ssml,
        string voiceId,
        CancellationToken cancellationToken = default)
    {
        ITtsProvider? provider = await _providerService.GetBestAvailableProviderIgnoringLimitsAsync();
        if (provider == null)
        {
            _logger.LogInformation("No TTS provider available");
            return null;
        }

        TtsCacheEntry? cachedEntry =
            await _cacheService.GetCachedEntryAsync(ssml, voiceId, cancellationToken);

        if (cachedEntry != null)
        {
            byte[] cachedAudioBytes = await File.ReadAllBytesAsync(cachedEntry.FilePath, cancellationToken);
            _logger.LogInformation("Using cached TTS (saved ${Cost})", cachedEntry.Cost.ToString("F6"));

            string audioBase64 = $"data:audio/wav;base64,{Convert.ToBase64String(cachedAudioBytes)}";

            return audioBase64;
        }

        int currentUsage = await _usageService.GetCurrentUsageAsync(provider.Name);
        int remainingCharacters = await _usageService.GetRemainingCharactersAsync(provider.Name);

        if (remainingCharacters <= 0 || remainingCharacters - ssml.Length < 0)
        {
            _logger.LogInformation(
                "TTS spend limit exceeded! Current usage: {CurrentUsage}, Remaining: {RemainingCharacters}, Requested: {Length}.",
                currentUsage, remainingCharacters, ssml.Length);
            return null;
        }

        _logger.LogInformation("Creating new TTS synthesis (provider: {Provider}, characters: {Length})", provider.Name,
            ssml.Length);

        byte[] audioBytes = await provider.SynthesizeSsmlAsync(ssml, voiceId, cancellationToken);
        decimal cost = await provider.CalculateCostAsync(ssml, voiceId);

        await _usageService.RecordUsageAsync(provider.Name, ssml.Length, cost);

        if (audioBytes.Length > 0)
        {            
            _logger.LogInformation("Created new TTS synthesis (cost ${Cost})", cost.ToString("F6"));
            
            await _cacheService.CreateCacheEntryAsync(ssml, voiceId, provider.Name, audioBytes, cost,
                cancellationToken);

            string audioBase64 = $"data:audio/wav;base64,{Convert.ToBase64String(audioBytes)}";

            return audioBase64;
        }

        return null;
    }

    public void Dispose()
    {
    }
}