using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch.Dto;
using NoMercyBot.Services.Twitch.Scripting;

namespace NoMercyBot.Services.Twitch;

public class ShoutoutQueueService : IHostedService
{
    private static readonly TimeSpan MinGlobalCooldown = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PerUserCooldown = TimeSpan.FromHours(1);
    private static readonly TimeSpan ProcessingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AutoShoutoutDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AutoShoutoutMessageDelay = TimeSpan.FromMinutes(2);

    private static readonly string[] SnarkyShoutoutReplies =
    {
        "Check out {displayname}! {Subject} {verb:has|have} some great {game} content. Go give {object} a follow! {Subject} {tense} practically a pro, or at least {subject} {verb:plays|play} one on Twitch.",
        "Yo, peep this! {displayname} {tense} rocking some {game} stuff. Go give {object} a follow! {Subject} {tense} so good, it's almost annoying.",
        "Attention, earthlings! {displayname} has {game} videos you need to see. Go give {object} a follow! {Subject} {tense} probably putting on a masterclass, or a clown show – either way, it's entertaining.",
        "Incoming awesome! {displayname} has some {game} action for you. Go give {object} a follow! {Subject} {tense} crushing it, or at least {subject} {verb:looks|look} like {subject} {presentTense}.",
        "Don't walk, run! {displayname} has more {game} than you can handle. Go give {object} a follow! {Subject} {tense} definitely worth interrupting your snack for.",
        "Our resident legend, {displayname}, has awesome {game}! Go give {object} a follow! {Subject} {tense} probably about to pull off something epic, or face-plant gloriously.",
        "Heads up, buttercups! {displayname} has some {game} for you. Go give {object} a follow! {Subject} {tense} proving once again that {Subject} {tense} awesome (don't tell {object} I said that).",
        "Guess who's got content? {displayname}! {Subject} {tense} rocking {game}. Go give {object} a follow! {Subject} {tense} bringing the vibes, whether {subject} {verb:likes|like} it or not.",
        "Behold! {displayname} has some solid {game} for you. Go give {object} a follow! {Subject} {tense} gracing us with {possessive} presence and questionable decision-making in {game}."
    };

    private record ShoutoutRequest(
        string ChannelId,
        string TargetUserId,
        string ChannelName,
        bool IsManual,
        bool IsRaid,
        DateTime EnqueuedAt);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<ShoutoutRequest>> _channelQueues = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ShoutoutRequest>> _channelPriorityQueues = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastGlobalShoutout = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastUserShoutout = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _sessionChatters = new();
    private readonly ConcurrentDictionary<string, DateTime> _streamOnlineTimes = new();

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TwitchApiService _twitchApiService;
    private readonly TwitchChatService _twitchChatService;
    private readonly TtsService _ttsService;
    private readonly ILogger<ShoutoutQueueService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    public ShoutoutQueueService(
        IServiceScopeFactory serviceScopeFactory,
        TwitchApiService twitchApiService,
        TwitchChatService twitchChatService,
        TtsService ttsService,
        ILogger<ShoutoutQueueService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _twitchApiService = twitchApiService;
        _twitchChatService = twitchChatService;
        _ttsService = ttsService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Shoutout Queue Service");

        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = ProcessQueueLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Shoutout Queue Service");

        if (_cts != null)
            await _cts.CancelAsync();

        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }

        _cts?.Dispose();
        _cts = null;
    }

    public async Task CheckIfStreamIsLiveAsync()
    {
        try
        {
            string? broadcasterId = _twitchApiService.Service?.UserId;
            if (string.IsNullOrEmpty(broadcasterId))
            {
                _logger.LogWarning("Cannot check stream status for shoutout queue - TwitchApiService not initialized");
                return;
            }

            StreamInfo? streamInfo = await _twitchApiService.GetStreamInfo(broadcasterId: broadcasterId);
            if (streamInfo == null)
            {
                _logger.LogInformation("Stream is offline on startup - shoutout queue will start when stream goes live");
                return;
            }

            _logger.LogInformation("Stream is already live on startup (started {StartedAt}) - restoring shoutout queue state", streamInfo.StartedAt);
            _streamOnlineTimes[broadcasterId] = streamInfo.StartedAt;
            _sessionChatters.GetOrAdd(broadcasterId, _ => new ConcurrentDictionary<string, byte>());

            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Restore per-user cooldowns from Channel.LastShoutout records within the last hour
            DateTime oneHourAgo = DateTime.UtcNow - PerUserCooldown;
            List<Channel> recentlyShoutedChannels = await dbContext.Channels
                .AsNoTracking()
                .Where(c => c.LastShoutout != null && c.LastShoutout > oneHourAgo)
                .ToListAsync();

            foreach (Channel c in recentlyShoutedChannels)
            {
                _lastUserShoutout[$"{broadcasterId}:{c.Id}"] = c.LastShoutout!.Value;
                _logger.LogDebug("Restored per-user shoutout cooldown for {UserId} (last: {LastShoutout})", c.Id, c.LastShoutout);
            }

            // Restore Channel.LastShoutout as global cooldown
            Channel? channel = await dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == broadcasterId);

            if (channel?.LastShoutout != null)
                _lastGlobalShoutout[broadcasterId] = channel.LastShoutout.Value;

            // Restore session chatters from chat messages in the current stream
            Database.Models.Stream? currentStream = await dbContext.Streams
                .AsNoTracking()
                .Where(s => s.ChannelId == broadcasterId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (currentStream != null)
            {
                List<string> chatterIds = await dbContext.ChatMessages
                    .AsNoTracking()
                    .Where(m => m.StreamId == currentStream.Id && m.BroadcasterId == broadcasterId)
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToListAsync();

                ConcurrentDictionary<string, byte> chatters = _sessionChatters.GetOrAdd(broadcasterId, _ => new ConcurrentDictionary<string, byte>());
                foreach (string chatterId in chatterIds)
                    chatters.TryAdd(chatterId, 0);

                _logger.LogInformation("Restored {Count} session chatters and {ShoutoutCount} shoutout cooldowns for {BroadcasterId}",
                    chatterIds.Count, recentlyShoutedChannels.Count, broadcasterId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check stream status for shoutout queue: {Message}", ex.Message);
        }
    }

    public void EnqueueShoutout(string channelId, string targetUserId, string channelName, bool isManual, bool isRaid = false)
    {
        ConcurrentQueue<ShoutoutRequest> targetQueue = isRaid
            ? _channelPriorityQueues.GetOrAdd(channelId, _ => new ConcurrentQueue<ShoutoutRequest>())
            : _channelQueues.GetOrAdd(channelId, _ => new ConcurrentQueue<ShoutoutRequest>());

        // Check for duplicates across both queues
        ShoutoutRequest[] priorityItems = _channelPriorityQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? pq) ? pq.ToArray() : [];
        ShoutoutRequest[] regularItems = _channelQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? rq) ? rq.ToArray() : [];
        ShoutoutRequest? existing = priorityItems.FirstOrDefault(r => r.TargetUserId == targetUserId)
                                    ?? regularItems.FirstOrDefault(r => r.TargetUserId == targetUserId);

        if (existing != null)
        {
            if (isManual && !existing.IsManual)
            {
                _logger.LogInformation("Shoutout for {UserId} already queued (auto), manual request noted for {Channel}", targetUserId, channelName);
            }
            else
            {
                _logger.LogDebug("Shoutout for {UserId} already queued in {Channel}, skipping", targetUserId, channelName);
                return;
            }
        }

        targetQueue.Enqueue(new ShoutoutRequest(channelId, targetUserId, channelName, isManual, isRaid, DateTime.UtcNow));
        _logger.LogInformation("Shoutout queued for {UserId} in {Channel} (manual: {IsManual}, raid: {IsRaid})", targetUserId, channelName, isManual, isRaid);
    }

    public async Task OnUserChatMessage(string channelId, string userId, string channelName)
    {
        ConcurrentDictionary<string, byte> chatters = _sessionChatters.GetOrAdd(channelId, _ => new ConcurrentDictionary<string, byte>());

        // If user already chatted this session, skip
        if (!chatters.TryAdd(userId, 0))
            return;

        // First message this session - check if the user's channel has auto-shoutout enabled
        try
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Channel? userChannel = await dbContext.Channels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == userId && c.Enabled);

            if (userChannel == null)
                return;

            _logger.LogInformation("Auto-shoutout triggered for {UserId} in {Channel} (first message this session)", userId, channelName);
            EnqueueShoutout(channelId, userId, channelName, isManual: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check channel record for {UserId} in {Channel}: {Message}", userId, channelName, ex.Message);
        }
    }

    public void OnStreamOnline(string channelId)
    {
        _logger.LogInformation("Stream online - resetting shoutout session for {ChannelId}", channelId);
        _streamOnlineTimes[channelId] = DateTime.UtcNow;
        _sessionChatters[channelId] = new ConcurrentDictionary<string, byte>();

        if (_channelPriorityQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? priorityQueue))
            while (priorityQueue.TryDequeue(out _)) { }

        if (_channelQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? queue))
            while (queue.TryDequeue(out _)) { }
    }

    public void OnStreamOffline(string channelId)
    {
        _logger.LogInformation("Stream offline - clearing shoutout session for {ChannelId}", channelId);
        _streamOnlineTimes.TryRemove(channelId, out _);
        _sessionChatters.TryRemove(channelId, out _);

        if (_channelPriorityQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? priorityQueue))
            while (priorityQueue.TryDequeue(out _)) { }

        if (_channelQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? queue))
            while (queue.TryDequeue(out _)) { }
    }

    private async Task ProcessQueueLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Collect all channel IDs from both queues
                HashSet<string> allChannelIds = [.._channelPriorityQueues.Keys, .._channelQueues.Keys];

                foreach (string channelId in allChannelIds)
                {
                    // Try priority queue (raids) first, then regular queue
                    _channelPriorityQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? priorityQueue);
                    _channelQueues.TryGetValue(channelId, out ConcurrentQueue<ShoutoutRequest>? regularQueue);

                    bool hasPriority = priorityQueue is { IsEmpty: false };
                    bool hasRegular = regularQueue is { IsEmpty: false };
                    if (!hasPriority && !hasRegular)
                        continue;

                    ConcurrentQueue<ShoutoutRequest> queue = hasPriority ? priorityQueue! : regularQueue!;

                    // Check global cooldown for this channel (Twitch API limit is 2 minutes)
                    if (_lastGlobalShoutout.TryGetValue(channelId, out DateTime lastGlobal))
                    {
                        TimeSpan elapsed = DateTime.UtcNow - lastGlobal;
                        if (elapsed < MinGlobalCooldown)
                        {
                            continue; // Too soon, try again on next tick
                        }
                    }

                    if (!queue.TryPeek(out ShoutoutRequest? request))
                        continue;

                    // For auto-shoutouts, enforce delay from stream start so chat has time to fill up
                    if (!request.IsManual &&
                        _streamOnlineTimes.TryGetValue(channelId, out DateTime streamStart) &&
                        DateTime.UtcNow - streamStart < AutoShoutoutDelay)
                    {
                        continue; // Too early in the stream, wait for chat to fill up
                    }

                    // For auto-shoutouts, wait a bit after the user's first message so it doesn't feel too aggressive
                    if (!request.IsManual && DateTime.UtcNow - request.EnqueuedAt < AutoShoutoutMessageDelay)
                    {
                        continue; // Too soon after first message, wait
                    }

                    // Check per-user cooldown (skip for manual shoutouts)
                    string userKey = $"{channelId}:{request.TargetUserId}";
                    if (!request.IsManual &&
                        _lastUserShoutout.TryGetValue(userKey, out DateTime lastUser) &&
                        DateTime.UtcNow - lastUser < PerUserCooldown)
                    {
                        // User was already shouted out recently, discard
                        queue.TryDequeue(out _);
                        _logger.LogDebug("Discarded shoutout for {UserId} in {Channel} - per-user cooldown active", request.TargetUserId, request.ChannelName);
                        continue;
                    }

                    // Dequeue and execute
                    if (!queue.TryDequeue(out _))
                        continue;

                    bool success = await ExecuteShoutoutAsync(request, token);
                    if (!success)
                    {
                        // Re-enqueue on failure so it's not lost
                        queue.Enqueue(request);
                        _logger.LogWarning("Shoutout for {UserId} failed, re-enqueued", request.TargetUserId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in shoutout queue processing loop: {Message}", ex.Message);
            }

            await Task.Delay(ProcessingInterval, token);
        }
    }

    private async Task<bool> ExecuteShoutoutAsync(ShoutoutRequest request, CancellationToken token)
    {
        try
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            User user = await _twitchApiService.GetOrFetchUser(id: request.TargetUserId);
            Channel? channel = await _twitchApiService.GetOrFetchChannel(id: request.TargetUserId);
            ChannelInfo channelInfo = await _twitchApiService.GetOrFetchChannelInfo(id: request.TargetUserId);

            string gameName = channelInfo.GameName ?? "something awesome";
            string title = channelInfo.Title ?? "";
            bool isLive = channelInfo.IsLive;

            // Determine template from the target user's channel
            string template;
            if (channel?.ShoutoutTemplate != null &&
                channel.ShoutoutTemplate != AppDbConfig.DefaultShoutoutTemplate)
            {
                template = channel.ShoutoutTemplate;
            }
            else
            {
                template = SnarkyShoutoutReplies[Random.Shared.Next(SnarkyShoutoutReplies.Length)];
            }

            // Build context for template replacement
            CommandScriptContext templateCtx = new()
            {
                Message = new ChatMessage
                {
                    UserId = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    User = user,
                },
                Channel = request.ChannelName,
                BroadcasterId = request.ChannelId,
                CommandName = "so",
                Arguments = [],
                DatabaseContext = dbContext,
                TwitchChatService = _twitchChatService,
                TwitchApiService = _twitchApiService,
                ServiceProvider = scope.ServiceProvider
            };

            // Generate text for chat announcement (actual username for {displayname}, but pronunciation for "any" pronoun substitution)
            string chatText = TemplateHelper.ReplaceTemplatePlaceholders(template, templateCtx, isLive, gameName, title,
                pronounNameOverride: channel?.UsernamePronunciation);

            // Generate text for TTS with pronunciation override for all name placeholders
            string ttsText = TemplateHelper.ReplaceTemplatePlaceholders(template, templateCtx, isLive, gameName, title,
                channel?.UsernamePronunciation, channel?.UsernamePronunciation);

            // Send shoutout via Twitch API
            try
            {
                await _twitchApiService.SendShoutoutAsync(
                    request.ChannelId,
                    request.ChannelId,
                    user.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send Twitch shoutout API call for {Username}: {Message}", user.Username, e.Message);
            }

            // Send announcement (uses actual names) and TTS (uses pronunciation)
            try
            {
                await _twitchChatService.SendAnnouncementAsBot(
                    request.ChannelId,
                    chatText);

                if (request.IsManual || request.IsRaid)
                    await _ttsService.SendCachedTts(ttsText, user.Id, token);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send shoutout announcement for {Username}: {Message}", user.Username, e.Message);
            }

            // Update rate-limit tracking
            _lastGlobalShoutout[request.ChannelId] = DateTime.UtcNow;
            _lastUserShoutout[$"{request.ChannelId}:{request.TargetUserId}"] = DateTime.UtcNow;

            // Update the target user's channel LastShoutout
            await dbContext.Channels
                .Where(c => c.Id == request.TargetUserId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(c => c.LastShoutout, DateTime.UtcNow)
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: token);

            _logger.LogDebug("Updated LastShoutout for user {UserId}", request.TargetUserId);

            _logger.LogInformation("Shoutout executed for {Username} in {Channel} (manual: {IsManual})",
                user.Username, request.ChannelName, request.IsManual);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute shoutout for {UserId} in {Channel}: {Message}",
                request.TargetUserId, request.ChannelName, ex.Message);
            return false;
        }
    }
}
