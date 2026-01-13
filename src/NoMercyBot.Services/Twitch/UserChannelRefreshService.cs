using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Twitch;

/// <summary>
/// Service responsible for refreshing all users and their channel information during startup.
/// </summary>
public class UserChannelRefreshService
{
    private readonly ILogger<UserChannelRefreshService> _logger;
    private readonly TwitchApiService _twitchApiService;
    private readonly AppDbContext _dbContext;
    private const int MaxConcurrentRequests = 10; // Twitch rate limit: 120 requests per minute = 2 per second, be conservative

    public UserChannelRefreshService(
        IServiceScopeFactory serviceScopeFactory,
        TwitchApiService twitchApiService,
        ILogger<UserChannelRefreshService> logger)
    {
        _twitchApiService = twitchApiService;
        _logger = logger;
        IServiceScope scope = serviceScopeFactory.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>
    /// Refreshes all users and their channel information from the Twitch API.
    /// Only refreshes users that haven't been updated in the last 30 days.
    /// Uses concurrent requests while respecting Twitch rate limits.
    /// </summary>
    public async Task RefreshAllUsersAndChannelsAsync()
    {
        try
        {
            _logger.LogInformation("Starting refresh of all users and channels");

            // Get all users that haven't been updated in the last 30 days
            DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            List<User> users = await _dbContext.Users
                .Where(u => u.UpdatedAt < thirtyDaysAgo)
                .ToListAsync();

            if (!users.Any())
            {
                _logger.LogWarning("No users found that need refresh (all updated within last 30 days)");
                return;
            }

            _logger.LogInformation("Found {UserCount} users to refresh", users.Count);

            // Refresh users concurrently with rate limiting
            int successCount = 0;
            int failureCount = 0;
            
            using (var semaphore = new SemaphoreSlim(MaxConcurrentRequests))
            {
                var tasks = users.Select(async user =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        _logger.LogDebug("Refreshing user: {UserName}", user.Username);
                        await _twitchApiService.FetchUser(login: user.Username);
                        Interlocked.Increment(ref successCount);
                        _logger.LogDebug("Successfully refreshed user: {UserName}", user.Username);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failureCount);
                        _logger.LogWarning(ex, "Failed to refresh user: {UserName}", user.Username);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            _logger.LogInformation(
                "Completed refresh of users and channels. Success: {SuccessCount}, Failures: {FailureCount}",
                successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while refreshing all users and channels");
        }
    }
}
