using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Twitch.Dto;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Services.Twitch;

public class LuckyFeatherTimerService : IHostedService
{
    private Guid RewardId => Guid.Parse("29c1ea38-96ff-4548-9bbf-ec0b665344c0");
    private readonly Random _rng = new();

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _holdTimerCts;
    private Task? _holdTimerTask;
    private readonly TwitchApiService _twitchApiService;
    private readonly IWidgetEventService _widgetEventService;
    private readonly ILogger _logger;

    private bool _streamOnline = false;

    // How long the feather stays with the holder before hiding
    private static readonly int HoldMinSeconds = (int)TimeSpan.FromMinutes(3).TotalSeconds;
    private static readonly int HoldMaxSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds;

    // How long the feather stays hidden before reappearing
    private static readonly int CooldownMinSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds;
    private static readonly int CooldownMaxSeconds = (int)TimeSpan.FromMinutes(10).TotalSeconds;

    public LuckyFeatherTimerService(
        TwitchApiService twitchApiService,
        IWidgetEventService widgetEventService,
        ILogger<TwitchApiService> logger
    )
    {
        _twitchApiService = twitchApiService;
        _widgetEventService = widgetEventService;
        _logger = logger;
    }

    /// <summary>
    /// Called when stream goes live - makes feather available
    /// </summary>
    public async Task OnStreamOnlineAsync(string broadcasterId)
    {
        _logger.LogInformation(
            "Stream online - enabling Lucky Feather (stays available until stolen)"
        );
        _streamOnline = true;

        // Cancel any running timer
        await CancelHoldTimerAsync();

        // Enable reward immediately - feather stays available until stolen
        try
        {
            await _twitchApiService.UpdateCustomReward(broadcasterId, RewardId, isPaused: false);
            await PublishAvailabilityAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable Lucky Feather reward on stream online");
        }
    }

    /// <summary>
    /// Called when stream goes offline - disables the reward
    /// </summary>
    public async Task OnStreamOfflineAsync(string broadcasterId)
    {
        _logger.LogInformation("Stream offline - disabling Lucky Feather reward");
        _streamOnline = false;

        // Cancel any running timer
        await CancelHoldTimerAsync();

        // Disable the reward when stream ends
        try
        {
            await _twitchApiService.UpdateCustomReward(broadcasterId, RewardId, isPaused: true);
            await PublishAvailabilityAsync(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable Lucky Feather reward on stream offline");
        }
    }

    /// <summary>
    /// Called when someone steals the feather - starts the hold timer only on first steal after appearing
    /// After hold period, feather hides for cooldown, then reappears
    /// </summary>
    public void OnFeatherStolen(string broadcasterId)
    {
        if (!_streamOnline)
        {
            _logger.LogWarning("Feather stolen but stream is offline - ignoring");
            return;
        }

        // Only start timer on the FIRST steal after feather appeared
        // If timer is already running, do nothing
        if (_holdTimerTask != null)
        {
            _logger.LogInformation("Feather stolen again - timer already running, not resetting");
            return;
        }

        _logger.LogInformation("Feather stolen (first steal) - starting hold timer");

        // Start hold timer
        _holdTimerCts = new CancellationTokenSource();
        _holdTimerTask = RunHoldAndCooldownAsync(broadcasterId, _holdTimerCts.Token);
    }

    private async Task RunHoldAndCooldownAsync(string broadcasterId, CancellationToken token)
    {
        try
        {
            // Phase 1: Hold period - feather stays with current holder
            int holdSeconds = _rng.Next(HoldMinSeconds, HoldMaxSeconds);
            _logger.LogInformation("Feather hold period: {Seconds} seconds", holdSeconds);
            await Task.Delay(holdSeconds * 1000, token);

            if (!_streamOnline || token.IsCancellationRequested)
                return;

            // Phase 2: Hide feather for cooldown
            _logger.LogInformation("Feather hold period ended - hiding feather for cooldown");

            await _twitchApiService.UpdateCustomReward(broadcasterId, RewardId, isPaused: true);
            await PublishAvailabilityAsync(false);

            int cooldownSeconds = _rng.Next(CooldownMinSeconds, CooldownMaxSeconds);
            _logger.LogInformation("Feather cooldown period: {Seconds} seconds", cooldownSeconds);
            await Task.Delay(cooldownSeconds * 1000, token);

            if (!_streamOnline || token.IsCancellationRequested)
                return;

            // Phase 3: Reappear - feather becomes available again
            _logger.LogInformation("Feather cooldown ended - feather is now available to steal");

            await _twitchApiService.UpdateCustomReward(broadcasterId, RewardId, isPaused: false);
            await PublishAvailabilityAsync(true);

            // Reset timer task so next steal starts a new timer
            _holdTimerTask = null;
            _holdTimerCts?.Dispose();
            _holdTimerCts = null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Hold/cooldown timer cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hold/cooldown timer: {Message}", ex.Message);
        }
    }

    private async Task CancelHoldTimerAsync()
    {
        if (_holdTimerCts != null)
        {
            await _holdTimerCts.CancelAsync();
            if (_holdTimerTask != null)
            {
                try
                {
                    await _holdTimerTask;
                }
                catch (OperationCanceledException) { }
            }
            _holdTimerCts.Dispose();
            _holdTimerCts = null;
            _holdTimerTask = null;
        }
    }

    private async Task PublishAvailabilityAsync(bool isAvailable)
    {
        try
        {
            object payload = new { type = "availability", isAvailable };
            await _widgetEventService.PublishEventAsync("overlay.feather.event", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish feather availability event");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Lucky Feather Timer Service");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after TwitchApiService is fully initialized to check if stream is already live.
    /// Must be called from Startup.cs after ServiceResolver.InitializeAllServices().
    /// </summary>
    public async Task CheckIfStreamIsLiveAsync()
    {
        try
        {
            string? broadcasterId = _twitchApiService.Service?.UserId;
            if (string.IsNullOrEmpty(broadcasterId))
            {
                _logger.LogWarning("Cannot check stream status - TwitchApiService not initialized");
                return;
            }

            StreamInfo? streamInfo = await _twitchApiService.GetStreamInfo(
                broadcasterId: broadcasterId
            );

            if (streamInfo != null)
            {
                _logger.LogInformation(
                    "Stream is already live on startup - enabling Lucky Feather"
                );
                await OnStreamOnlineAsync(broadcasterId);
            }
            else
            {
                _logger.LogInformation(
                    "Stream is offline on startup - Lucky Feather will be enabled when stream goes live"
                );
                // Ensure reward is paused when offline
                await _twitchApiService.UpdateCustomReward(broadcasterId, RewardId, isPaused: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check if stream is live on startup: {Message}",
                ex.Message
            );
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Lucky Feather Timer Service");

        _streamOnline = false;
        await CancelHoldTimerAsync();

        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }
    }
}
