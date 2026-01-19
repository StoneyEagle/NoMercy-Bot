using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Widgets;
using TwitchLib.EventSub.Core.EventArgs.Channel;

namespace NoMercyBot.Services.Twitch;

public enum RewardPermission
{
    Broadcaster,
    LeadModerator,
    Moderator,
    Vip,
    Subscriber,
    Everyone
}

public class RewardContext
{
    public string BroadcasterLogin { get; set; } = null!;
    public Channel Channel { get; set; } = null!;
    public User User { get; set; } = null!;
    public User Broadcaster { get; set; } = null!;
    public string ChannelId { get; set; } = null!;
    public string BroadcasterId { get; set; } = null!;
    public Guid RewardId { get; set; }
    public string RewardTitle { get; set; } = null!;
    public string RedemptionId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string UserLogin { get; set; } = null!;
    public string UserDisplayName { get; set; } = null!;
    public string? UserInput { get; set; }
    public int Cost { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset RedeemedAt { get; set; }
    public Func<string, Task> ReplyAsync { get; set; } = null!;
    public Func<Task> RefundAsync { get; set; } = null!;
    public Func<Task> FulfillAsync { get; set; } = null!;
    public required AppDbContext DatabaseContext { get; init; } = null!;
    public TwitchRewardService RewardService { get; set; } = null!;
    public TwitchApiService TwitchApiService { get; set; } = null!;
    public WidgetEventService WidgetEventService { get; set; } = null!;
    public IServiceProvider ServiceProvider { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
}

public class TwitchReward
{
    public Guid RewardId { get; set; }
    public string? RewardTitle { get; set; }
    public RewardPermission Permission { get; set; } = RewardPermission.Everyone;
    public object? Storage { get; set; }
    public Func<RewardContext, Task> Callback { get; set; } = null!;
}

public class TwitchRewardService
{
    private static readonly ConcurrentDictionary<string, TwitchReward> RewardsByTitle = new();
    private static readonly ConcurrentDictionary<Guid, TwitchReward> RewardsById = new();
    private readonly ILogger<TwitchRewardService> _logger;
    private readonly AppDbContext _appDbContext;
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly IServiceProvider _serviceProvider;
    private readonly PermissionService _permissionService;
    private readonly WidgetEventService _widgetEventService;

    public TwitchRewardService(
        AppDbContext appDbContext,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        WidgetEventService widgetEventService,
        PermissionService permissionService,
        IServiceScopeFactory scopeFactory,
        ILogger<TwitchRewardService> logger)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _widgetEventService = widgetEventService;
        IServiceScope scope = scopeFactory.CreateScope();
        _serviceProvider = scope.ServiceProvider;
        _permissionService = permissionService;

        LoadRewardsFromDatabase();
    }

    private void LoadRewardsFromDatabase()
    {
        // Only load user-defined rewards that have actual response text
        // Skip tracking entries (empty response or hex color codes from legacy data)
        // Script-loaded rewards have their own callbacks and shouldn't be overwritten
        List<Reward> dbRewards = _appDbContext.Rewards
            .Where(r => r.IsEnabled && !string.IsNullOrEmpty(r.Response))
            .ToList()
            .Where(r => !IsTrackingEntry(r.Response))
            .ToList();

        foreach (Reward dbReward in dbRewards)
            RegisterReward(new()
            {
                RewardId = dbReward.Id,
                RewardTitle = dbReward.Title,
                Permission = Enum.TryParse(dbReward.Permission, true, out RewardPermission perm)
                    ? perm
                    : RewardPermission.Everyone,
                Callback = async ctx => await ctx.ReplyAsync(dbReward.Response)
            });
    }

    private static bool IsTrackingEntry(string response)
    {
        // Tracking entries have empty response or hex color codes (legacy)
        if (string.IsNullOrEmpty(response)) return true;
        if (response.StartsWith("#") && response.Length == 7) return true;
        return false;
    }

    public bool RegisterReward(TwitchReward reward)
    {
        if (reward.RewardId != Guid.Empty) 
        {
            RewardsById[reward.RewardId] = reward;
            _logger.LogDebug("Registered reward by ID: {RewardId}", reward.RewardId);
        }

        if (!string.IsNullOrEmpty(reward.RewardTitle))
        {
            string lowerTitle = reward.RewardTitle.ToLowerInvariant();
            RewardsByTitle[lowerTitle] = reward;
            _logger.LogDebug("Registered reward by title: {RewardTitle} -> {LowerTitle}", reward.RewardTitle, lowerTitle);
        }

        _logger.LogInformation("Registered/Updated reward: {RewardTitle} (ID: {RewardId})",
            reward.RewardTitle ?? "Unknown", reward.RewardId);
        return true;
    }

    private bool RemoveReward(string identifier)
    {
        bool removedById = false;
        bool removedByTitle = RewardsByTitle.TryRemove(identifier.ToLowerInvariant(), out _);

        // Try to parse as Guid for ID removal
        if (Guid.TryParse(identifier, out Guid guidId)) removedById = RewardsById.TryRemove(guidId, out _);

        return removedById || removedByTitle;
    }

    public bool UpdateReward(TwitchReward reward)
    {
        if (reward.RewardId != Guid.Empty) RewardsById[reward.RewardId] = reward;

        if (!string.IsNullOrEmpty(reward.RewardTitle)) RewardsByTitle[reward.RewardTitle.ToLowerInvariant()] = reward;

        return true;
    }

    public IEnumerable<TwitchReward> ListRewards()
    {
        return RewardsById.Values.Concat(RewardsByTitle.Values).Distinct();
    }

    public async Task ExecuteReward(ChannelPointsCustomRewardRedemptionArgs args)
    {
        string twitchRewardId = args.Payload.Event.Reward.Id;
        string twitchRedeemId = args.Payload.Event.Id;
        string rewardTitle = args.Payload.Event.Reward.Title;
        string broadcasterId = args.Payload.Event.BroadcasterUserId;
        string broadcasterLogin = args.Payload.Event.BroadcasterUserLogin;

        // Try to find reward by converting Twitch string ID to Guid for database lookup
        TwitchReward? reward = null;

        // First try to find by Twitch reward ID converted to Guid
        if (Guid.TryParse(twitchRewardId, out Guid rewardGuid))
        {
            RewardsById.TryGetValue(rewardGuid, out reward);
            if (reward != null)
            {
                _logger.LogDebug("Found reward by ID: {RewardId} -> {RewardTitle}", rewardGuid, reward.RewardTitle);
            }
        }
        else
        {
            _logger.LogWarning("Could not parse Twitch reward ID as GUID: {TwitchRewardId}", twitchRewardId);
        }

        // Fallback to title lookup
        if (reward == null)
        {
            string lowerTitle = rewardTitle.ToLowerInvariant();
            RewardsByTitle.TryGetValue(lowerTitle, out reward);
            if (reward != null)
            {
                _logger.LogDebug("Found reward by title: {LowerTitle} -> {RewardTitle}", lowerTitle, reward.RewardTitle);
            }
            else
            {
                _logger.LogWarning("Reward not found by title. Available titles: {AvailableTitles}", 
                    string.Join(", ", RewardsByTitle.Keys));
            }
        }

        if (reward != null)
        {
            // Check permissions
            User? user = _appDbContext.Users.FirstOrDefault(u => u.Id == args.Payload.Event.UserId);
            user ??= await _twitchApiService.FetchUser(id: args.Payload.Event.UserId);

            User? broadcaster = _appDbContext.Users.FirstOrDefault(u => u.Id == broadcasterId);
            broadcaster ??= await _twitchApiService.FetchUser(id: broadcasterId);

            Channel? channel = _appDbContext.Channels.FirstOrDefault(c => c.Id == broadcasterId);

            string userType = DetermineUserType(user, broadcasterId);

            if (!_permissionService.HasMinLevel(userType, reward.Permission.ToString().ToLowerInvariant()))
            {
                _logger.LogWarning("User {User} lacks permission {RequiredPermission} for reward {RewardTitle}",
                    args.Payload.Event.UserLogin, reward.Permission, rewardTitle);

                // Refund the points by updating redemption status to CANCELED
                await _twitchApiService.UpdateRedemptionStatus(broadcasterId, twitchRewardId, twitchRedeemId,
                    "CANCELED");

                await _twitchChatService.SendMessageAsBot(
                    broadcasterLogin,
                    $"@{args.Payload.Event.UserName}, you don't have permission to use this reward. Your points have been refunded.");

                return;
            }

            RewardContext context = new()
            {
                Channel = channel,
                User = user,
                Broadcaster = broadcaster,
                BroadcasterId = broadcasterId,
                RewardId = reward.RewardId, // Use the Guid from our reward
                RewardTitle = rewardTitle,
                RedemptionId = args.Payload.Event.Id,
                UserId = args.Payload.Event.UserId,
                UserLogin = args.Payload.Event.UserLogin,
                UserDisplayName = args.Payload.Event.UserName,
                UserInput = args.Payload.Event.UserInput,
                Cost = args.Payload.Event.Reward.Cost,
                Status = args.Payload.Event.Status,
                RedeemedAt = args.Payload.Event.RedeemedAt,
                RewardService = this,
                ServiceProvider = _serviceProvider,
                TwitchApiService = _twitchApiService,
                DatabaseContext = _appDbContext,
                ReplyAsync = async (reply) =>
                {
                    await _twitchChatService.SendMessageAsBot(broadcasterLogin, reply);
                },
                RefundAsync = async () =>
                {
                    await _twitchApiService.UpdateRedemptionStatus(broadcasterId, twitchRewardId, twitchRedeemId,
                        "CANCELED");
                },
                FulfillAsync = async () =>
                {
                    await _twitchApiService.UpdateRedemptionStatus(broadcasterId, twitchRewardId, twitchRedeemId,
                        "FULFILLED");
                }
            };

            try
            {
                await reward.Callback(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing reward {RewardTitle} for user {User}",
                    rewardTitle, args.Payload.Event.UserLogin);

                // Refund on error
                await _twitchApiService.UpdateRedemptionStatus(broadcasterId, twitchRewardId, twitchRedeemId,
                    "CANCELED");

                await _twitchChatService.SendMessageAsBot(
                    broadcasterLogin,
                    $"@{args.Payload.Event.UserName}, there was an error processing your reward. Your points have been refunded.");
            }
        }
        else
        {
            _logger.LogDebug("No handler found for reward: {RewardTitle} (ID: {RewardId})", rewardTitle,
                twitchRewardId);
        }
    }

    private string DetermineUserType(User? user, string broadcasterId)
    {
        if (user?.Id == broadcasterId) return "broadcaster";
        return "everyone";
    }

    public async Task AddOrUpdateUserRewardAsync(Guid rewardId, string? rewardTitle, string response,
        string permission = "everyone", bool isEnabled = true, string? description = null)
    {
        Reward? dbReward = await _appDbContext.Rewards.FirstOrDefaultAsync(r => r.Id == rewardId);
        if (dbReward == null)
        {
            dbReward = new()
            {
                Id = rewardId,
                Title = rewardTitle,
                Response = response,
                Permission = permission,
                IsEnabled = isEnabled,
                Description = description
            };
            await _appDbContext.Rewards.AddAsync(dbReward);
        }
        else
        {
            dbReward.Title = rewardTitle;
            dbReward.Response = response;
            dbReward.Permission = permission;
            dbReward.IsEnabled = isEnabled;
            dbReward.Description = description;
            _appDbContext.Rewards.Update(dbReward);
        }

        await _appDbContext.SaveChangesAsync();

        RegisterReward(new()
        {
            RewardId = dbReward.Id,
            RewardTitle = dbReward.Title,
            Permission = Enum.TryParse<RewardPermission>(dbReward.Permission, true, out RewardPermission perm)
                ? perm
                : RewardPermission.Everyone,
            Callback = async ctx => await ctx.ReplyAsync(dbReward.Response)
        });
    }

    public async Task<bool> RemoveUserRewardAsync(string identifier)
    {
        Guid? rewardId = Guid.TryParse(identifier, out Guid result) ? result : null;
        Reward? dbReward =
            await _appDbContext.Rewards.FirstOrDefaultAsync(r => r.Id == rewardId || r.Title == identifier);
        if (dbReward == null) return false;

        _appDbContext.Rewards.Remove(dbReward);
        await _appDbContext.SaveChangesAsync();
        RemoveReward(identifier);
        return true;
    }
}