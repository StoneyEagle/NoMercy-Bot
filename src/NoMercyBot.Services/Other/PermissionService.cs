using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Other;

public class PermissionService : IService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PermissionService> _logger;

    private const string OverrideRecordType = "PermissionOverride";

    // userId -> granted level (e.g. "Subscriber", "Vip", "Moderator")
    private static readonly ConcurrentDictionary<string, string> _overrides = new();

    // Ordered from lowest to highest privilege
    private static readonly string[] _levelOrder = { "Viewer", "Subscriber", "Vip", "Moderator", "LeadModerator", "Broadcaster" };

    public PermissionService(IServiceScopeFactory serviceScopeFactory, ILogger<PermissionService> logger)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;

        LoadOverrides();
    }

    private void LoadOverrides()
    {
        try
        {
            List<Record> records = _dbContext.Records
                .Where(r => r.RecordType == OverrideRecordType)
                .ToList();

            foreach (Record record in records)
            {
                _overrides[record.UserId] = record.Data;
            }

            if (records.Count > 0)
                _logger.LogInformation("Loaded {Count} permission overrides", records.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load permission overrides");
        }
    }

    public static bool Can(IService service, string permission)
    {
        return true;
    }

    /// <summary>
    /// Returns the effective user type considering permission overrides.
    /// Uses whichever is higher: the Twitch role or the granted override.
    /// </summary>
    public string GetEffectiveUserType(string userId, string twitchUserType)
    {
        if (!_overrides.TryGetValue(userId, out string? grantedLevel))
            return twitchUserType;

        int twitchRank = Array.IndexOf(_levelOrder, twitchUserType);
        int grantedRank = Array.IndexOf(_levelOrder, grantedLevel);

        // Use whichever is higher
        return grantedRank > twitchRank ? grantedLevel : twitchUserType;
    }

    public bool HasMinLevel(string userType, string level)
    {
        return level switch
        {
            "broadcaster" => userType is "Broadcaster",
            "lead_moderator" => userType is "LeadModerator" or "Moderator" or "Broadcaster",
            "moderator" => userType is "Moderator" or "Broadcaster",
            "vip" => userType is "Vip" or "Moderator" or "Broadcaster",
            "subscriber" => userType is "Subscriber" or "Vip" or "Moderator" or "Broadcaster",
            "everyone" => true,
            _ => false
        };
    }

    /// <summary>
    /// Permission check that considers overrides for the given user.
    /// </summary>
    public bool UserHasMinLevel(string userId, string userType, string level)
    {
        string effectiveType = GetEffectiveUserType(userId, userType);
        return HasMinLevel(effectiveType, level);
    }

    public void GrantOverride(string userId, string level, AppDbContext db)
    {
        _overrides[userId] = level;

        Record? existing = db.Records
            .FirstOrDefault(r => r.UserId == userId && r.RecordType == OverrideRecordType);

        if (existing != null)
        {
            existing.Data = level;
        }
        else
        {
            db.Records.Add(new Record
            {
                UserId = userId,
                RecordType = OverrideRecordType,
                Data = level,
            });
        }

        db.SaveChanges();
    }

    public bool RevokeOverride(string userId, AppDbContext db)
    {
        _overrides.TryRemove(userId, out _);

        Record? existing = db.Records
            .FirstOrDefault(r => r.UserId == userId && r.RecordType == OverrideRecordType);

        if (existing == null)
            return false;

        db.Records.Remove(existing);
        db.SaveChanges();
        return true;
    }

    public bool HasOverride(string userId)
    {
        return _overrides.ContainsKey(userId);
    }

    public string? GetOverride(string userId)
    {
        return _overrides.TryGetValue(userId, out string? level) ? level : null;
    }
}
