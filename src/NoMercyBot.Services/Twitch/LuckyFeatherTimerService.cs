using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Services.Twitch;

public class LuckyFeatherTimerService : IHostedService
{
    private Guid RewardId => Guid.Parse("29c1ea38-96ff-4548-9bbf-ec0b665344c0");
    private readonly Random _rng = new();

    private CancellationTokenSource? _cts;
    private Task? _timerTask;
    private readonly TwitchApiService _twitchApiService;
    private readonly ILogger _logger;

    private bool IsEnabled { get; set; } = false;
    private bool _timerRunning = false;
    
    // Active state (reward is available to steal) - shorter duration
    private static readonly int ActiveMinIntervalSeconds = (int)TimeSpan.FromMinutes(1).TotalSeconds; // 2 minutes
    private static readonly int ActiveMaxIntervalSeconds = (int)TimeSpan.FromMinutes(5).TotalSeconds; // 5 minutes
    
    // Inactive state (reward is hidden) - longer duration
    private static readonly int InactiveMinIntervalSeconds = (int)TimeSpan.FromMinutes(10).TotalSeconds; // 5 minutes
    private static readonly int InactiveMaxIntervalSeconds = (int)TimeSpan.FromMinutes(15).TotalSeconds; // 15 minutes

    public LuckyFeatherTimerService(
        TwitchApiService twitchApiService,
        ILogger<TwitchApiService> logger)
    {
        _twitchApiService = twitchApiService;
        _logger = logger;
    }

    /// <summary>
    /// Called when stream goes live - starts the ping-pong timer
    /// </summary>
    public async Task OnStreamOnlineAsync(string broadcasterId)
    {
        _logger.LogInformation("Stream online - starting Lucky Feather timer");
        _timerRunning = true;
        
        // Ensure reward is enabled when stream starts
        try
        {
            IsEnabled = false; // Reset state
            await _twitchApiService.UpdateCustomReward(
                broadcasterId,
                RewardId,
                isPaused: false
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable Lucky Feather reward on stream online");
        }
    }

    /// <summary>
    /// Called when stream goes offline - stops the ping-pong timer and disables the reward
    /// </summary>
    public async Task OnStreamOfflineAsync(string broadcasterId)
    {
        _logger.LogInformation("Stream offline - stopping Lucky Feather timer and disabling reward");
        _timerRunning = false;
        IsEnabled = false;
        
        // Disable the reward when stream ends
        try
        {
            await _twitchApiService.UpdateCustomReward(
                broadcasterId,
                RewardId,
                isPaused: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable Lucky Feather reward on stream offline");
        }
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Lucky Feather Timer Service");

        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start the timer loop (it will only toggle reward when _timerRunning is true)
        // Note: CheckIfStreamIsLiveAsync is called from Startup.cs after services are initialized
        _timerTask = TimerLoopAsync(_cts.Token);

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

            // First, ensure reward state matches our initial IsEnabled=false state (paused)
            await _twitchApiService.UpdateCustomReward(
                broadcasterId,
                RewardId,
                isPaused: true
            );

            StreamInfo? streamInfo = await _twitchApiService.GetStreamInfo(broadcasterId: broadcasterId);

            if (streamInfo != null)
            {
                _logger.LogInformation("Stream is already live on startup - enabling Lucky Feather timer loop");
                _timerRunning = true;
                // Timer loop is already running from IHostedService.StartAsync
                // It will randomly unpause the reward on its next iteration
            }
            else
            {
                _logger.LogInformation("Stream is offline on startup - Lucky Feather timer will start when stream goes live");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if stream is live on startup: {Message}", ex.Message);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Lucky Feather Timer Service");
        
        _timerRunning = false;
        await _cts!.CancelAsync();
        
        if (_timerTask != null)
        {
            try
            {
                await _timerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when service shuts down
            }
        }
        
        _cts?.Dispose();
        _cts = null;
    }

    private async Task TimerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // Only run the timer logic when the stream is live
            if (_timerRunning)
            {
                // Choose interval based on current state
                int intervalSeconds;
                if (IsEnabled)
                {
                    // Active state - shorter interval before hiding
                    intervalSeconds = _rng.Next(ActiveMinIntervalSeconds, ActiveMaxIntervalSeconds);
                }
                else
                {
                    // Inactive state - longer interval before showing again
                    intervalSeconds = _rng.Next(InactiveMinIntervalSeconds, InactiveMaxIntervalSeconds);
                }
                
                _logger.LogInformation("Toggling Lucky Feather reward after {IntervalSeconds} seconds, current state is {State}", intervalSeconds, IsEnabled);
                await Task.Delay(intervalSeconds * 1000, token);

                try
                {
                    // Ensure TwitchApiService is initialized before accessing UserId
                    string? broadcasterId = _twitchApiService.Service?.UserId;
                    if (string.IsNullOrEmpty(broadcasterId))
                    {
                        _logger.LogWarning("[LuckyFeatherTimer] Cannot toggle reward - TwitchApiService not initialized");
                        continue;
                    }

                    if (IsEnabled)
                    {
                        IsEnabled = false;
                        
                        await _twitchApiService.UpdateCustomReward(
                            broadcasterId,
                            RewardId,
                            isPaused: true
                        );
                    } 
                    else
                    {
                        IsEnabled = true;
                        
                        await _twitchApiService.UpdateCustomReward(
                            broadcasterId,
                            RewardId,
                            isPaused: false
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[LuckyFeatherTimer] Exception toggling reward: {Message}", ex.Message);
                }
            }
            else
            {
                // Sleep briefly while waiting for stream to come online
                await Task.Delay(1000, token);
            }
        }
    }
}
