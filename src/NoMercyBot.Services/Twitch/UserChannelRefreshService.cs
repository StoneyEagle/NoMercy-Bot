using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch.Dto;

namespace NoMercyBot.Services.Twitch;

/// <summary>
/// Service responsible for refreshing all users and their channel information during startup.
/// </summary>
public class UserChannelRefreshService
{
    private readonly ILogger<UserChannelRefreshService> _logger;
    private readonly TwitchApiService _twitchApiService;
    private readonly AppDbContext _dbContext;
    private const int MaxConcurrentRequests = 2; // Twitch rate limit: 120 requests per minute = 2 per second, be conservative

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

            int successCount = 0;
            int failureCount = 0;
            int removedCount = 0;

            foreach (User user in users)
            {
                try
                {
                    _logger.LogDebug("Refreshing user: {UserName}", user.Username);

                    // Check if user still exists on Twitch before doing a full fetch
                    List<UserInfo>? twitchUsers = await _twitchApiService.GetUsers(userId: user.Id);

                    if (twitchUsers is null || twitchUsers.Count == 0)
                    {
                        _logger.LogInformation("User no longer exists on Twitch, removing: {UserName} ({UserId})", user.Username, user.Id);
                        await RemoveUser(user);
                        removedCount++;
                        continue;
                    }

                    await _twitchApiService.FetchUser(id: user.Id);
                    successCount++;
                    _logger.LogDebug("Successfully refreshed user: {UserName}", user.Username);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogWarning("Failed to refresh user: {UserName} - {Message}", user.Username, ex.Message);

                    // Update timestamp so we don't retry this user every startup
                    user.UpdatedAt = DateTime.UtcNow;
                    _dbContext.Users.Update(user);
                    await _dbContext.SaveChangesAsync();
                }
            }

            _logger.LogInformation(
                "Completed refresh of users and channels. Success: {SuccessCount}, Removed: {RemovedCount}, Failures: {FailureCount}",
                successCount, removedCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while refreshing all users and channels");
        }
    }

    /// <summary>
    /// Removes a user and all their related data from the database.
    /// </summary>
    private async Task RemoveUser(User user)
    {
        string userId = user.Id;

        // Use raw SQL to delete all references - avoids loading large sets into memory
        // and catches all FK constraints including tables without DbSet properties

        // First, break reply chains pointing to this user's messages
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE ChatMessages SET ReplyToMessageId = NULL WHERE ReplyToMessageId IN (SELECT Id FROM ChatMessages WHERE UserId = {0})", userId);
        // Also break reply chains from this user's messages pointing to others
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE ChatMessages SET ReplyToMessageId = NULL WHERE ReplyToMessageId IN (SELECT Id FROM ChatMessages WHERE BroadcasterId = {0})", userId);

        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Records WHERE UserId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChatPresences WHERE UserId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChatPresences WHERE ChannelId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM UserTtsVoices WHERE UserId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChannelEvents WHERE UserId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChannelModerator WHERE UserId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChannelModerator WHERE ChannelId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChatMessages WHERE UserId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChatMessages WHERE BroadcasterId = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChannelInfo WHERE Id = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Channels WHERE Id = {0}", userId);
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Users WHERE Id = {0}", userId);

        // Detach the tracked entity since we deleted it via raw SQL
        _dbContext.Entry(user).State = EntityState.Detached;

        _logger.LogInformation("Removed user and all related data: {UserName} ({UserId})", user.Username, userId);
    }
}
