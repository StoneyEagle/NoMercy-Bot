using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMercyBot.Services.TTS.Services;

/// <summary>
/// Background service that periodically cleans up old TTS cache entries to save disk space
/// </summary>
public class TtsCacheCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TtsCacheCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Run cleanup every 6 hours
    private readonly TimeSpan _maxCacheAge = TimeSpan.FromDays(30); // Remove entries older than 30 days
    private const int MinAccessCountThreshold = 2; // Keep entries that have been accessed at least twice

    public TtsCacheCleanupService(
        IServiceProvider serviceProvider,
        ILogger<TtsCacheCleanupService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _serviceProvider.CreateScope();
                TtsCacheService cacheService =
                    scope.ServiceProvider.GetRequiredService<TtsCacheService>();

                await cacheService.CleanupOldCacheEntriesAsync(
                    _maxCacheAge,
                    MinAccessCountThreshold,
                    stoppingToken
                );

                _logger.LogInformation("TTS cache cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TTS cache cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}
