using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    private static readonly int MinIntervalSeconds = (int)TimeSpan.FromMinutes(2).TotalSeconds; // 2 minute
    private static readonly int MaxIntervalSeconds = (int)TimeSpan.FromMinutes(10).TotalSeconds; // 10 minutes

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
        
        // Don't start the timer loop yet - it will start when stream goes online
        _timerTask = TimerLoopAsync(_cts.Token);
        
        return Task.CompletedTask;
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
                // Random interval in seconds
                int intervalSeconds = _rng.Next(MinIntervalSeconds, MaxIntervalSeconds);
                await Task.Delay(intervalSeconds * 1000, token);

                try
                {
                    if (IsEnabled)
                    {
                        IsEnabled = false;
                        
                        await _twitchApiService.UpdateCustomReward(
                            _twitchApiService.Service.UserId,
                            RewardId,
                            isPaused: true
                        );
                    } 
                    else
                    {
                        IsEnabled = true;
                        
                        await _twitchApiService.UpdateCustomReward(
                            _twitchApiService.Service.UserId,
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
