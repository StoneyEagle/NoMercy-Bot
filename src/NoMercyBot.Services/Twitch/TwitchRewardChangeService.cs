using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Scripting;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace NoMercyBot.Services.Twitch;

public class TwitchRewardChangeService
{
    private readonly ILogger<TwitchRewardChangeService> _logger;
    private readonly AppDbContext _appDbContext;
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchApiService _twitchApiService;
    private readonly IServiceProvider _serviceProvider;
    private RewardChangeScriptLoader? _scriptLoader;

    public TwitchRewardChangeService(
        AppDbContext appDbContext,
        TwitchChatService twitchChatService,
        TwitchApiService twitchApiService,
        IServiceProvider serviceProvider,
        ILogger<TwitchRewardChangeService> logger)
    {
        _logger = logger;
        _appDbContext = appDbContext;
        _twitchChatService = twitchChatService;
        _twitchApiService = twitchApiService;
        _serviceProvider = serviceProvider;
    }

    public void SetScriptLoader(RewardChangeScriptLoader scriptLoader)
    {
        _scriptLoader = scriptLoader;
    }

    public async Task ExecuteRewardChangedAsync(ChannelPointsCustomRewardArgs args, CancellationToken cancellationToken = default)
    {
        if (_scriptLoader == null)
        {
            _logger.LogWarning("RewardChangeScriptLoader not initialized");
            return;
        }

        ChannelPointsCustomReward rewardData = args.Payload.Event;
        string rewardId = rewardData.Id;
        string rewardTitle = rewardData.Title;
        string broadcasterId = rewardData.BroadcasterUserId;
        string broadcasterLogin = rewardData.BroadcasterUserLogin;

        // Try to find handler by ID first (immutable identifier)
        IRewardChangeHandler? handler = null;
        
        if (Guid.TryParse(rewardId, out Guid guidId))
        {
            handler = _scriptLoader.GetHandler(guidId);
        }
        
        // Fallback to title lookup if ID lookup fails
        if (handler == null)
        {
            handler = _scriptLoader.GetHandler(rewardTitle);
        }

        if (handler == null)
        {
            _logger.LogDebug("No change handler found for reward: {RewardTitle} (ID: {RewardId})", rewardTitle, rewardId);
            return;
        }

        // Build context - detect changes by comparing current data
        RewardChangeContext context = new()
        {
            BroadcasterId = broadcasterId,
            BroadcasterLogin = broadcasterLogin,
            RewardId = Guid.TryParse(rewardId, out Guid gId) ? gId : Guid.Empty,
            RewardTitle = rewardTitle,
            NewTitle = rewardData.Title,
            NewCost = rewardData.Cost,
            NewIsEnabled = rewardData.IsEnabled,
            NewIsPaused = rewardData.IsPaused,
            NewBackgroundColor = rewardData.BackgroundColor,
            DatabaseContext = _appDbContext,
            ServiceProvider = _serviceProvider,
            TwitchChatService = _twitchChatService,
            TwitchApiService = _twitchApiService,
            CancellationToken = cancellationToken
        };

        try
        {
            // Detect what changed and call the appropriate handler method
            // For now, we'll detect based on common changes
            // In a real scenario, you might have previous state stored in the database
            
            // Try to determine what changed
            RewardChangeType? changeType = DetermineChangeType(context, rewardData);
            if (changeType.HasValue)
            {
                context.DetectedChangeType = changeType.Value;
                _logger.LogInformation("Reward change detected: {RewardTitle} - {ChangeType}", rewardTitle, changeType);
                
                await ExecuteChangeHandlerAsync(handler, context, changeType.Value);
            }
            else
            {
                _logger.LogDebug("Could not determine change type for reward: {RewardTitle}", rewardTitle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing change handler for reward {RewardTitle}", rewardTitle);
        }
    }

    private RewardChangeType? DetermineChangeType(RewardChangeContext context, ChannelPointsCustomReward rewardData)
    {
        // Try to load previous state from database if available
        var dbReward = _appDbContext.Rewards.FirstOrDefault(r => r.Id == context.RewardId);
        
        if (dbReward == null)
        {
            // First time seeing this reward
            // Assume it was just enabled as the initial change
            SaveRewardState(context.RewardId, rewardData);
            
            if (rewardData.IsEnabled)
            {
                return RewardChangeType.Enabled;
            }
            else
            {
                return RewardChangeType.Disabled;
            }
        }

        // Compare properties with stored state
        // Check for enabled/disabled
        if (rewardData.IsEnabled != dbReward.IsEnabled)
        {
            // Update stored state
            SaveRewardState(context.RewardId, rewardData);
            return rewardData.IsEnabled ? RewardChangeType.Enabled : RewardChangeType.Disabled;
        }

        // Check for cost/price change
        // Description format: "cost|backgroundColor"
        if (dbReward.Description != null)
        {
            string[] parts = dbReward.Description.Split('|');
            if (parts.Length > 0 && int.TryParse(parts[0], out int oldCost))
            {
                context.OldCost = oldCost;
                if (rewardData.Cost != oldCost)
                {
                    SaveRewardState(context.RewardId, rewardData);
                    return RewardChangeType.PriceChanged;
                }
            }
        }

        // Check for pause status change
        // The pause state is stored in the Description field as "cost|backgroundColor|isPaused"
        bool storedIsPaused = false;
        if (!string.IsNullOrEmpty(dbReward.Description))
        {
            string[] parts = dbReward.Description.Split('|');
            if (parts.Length >= 3 && bool.TryParse(parts[2], out bool parsedIsPaused))
            {
                storedIsPaused = parsedIsPaused;
            }
        }
        
        bool isPauseStatusDifferent = rewardData.IsPaused != storedIsPaused;
        
        if (isPauseStatusDifferent)
        {
            SaveRewardState(context.RewardId, rewardData);
            // Track the old pause state
            context.OldIsPaused = storedIsPaused;
            // Check if we're resuming (was paused, now not) or pausing (wasn't paused, now is)
            bool isResuming = storedIsPaused && !rewardData.IsPaused;
            return isResuming ? RewardChangeType.ResumeStatusChanged : RewardChangeType.PauseStatusChanged;
        }

        // Check for title change
        if (dbReward.Title != rewardData.Title)
        {
            context.OldTitle = dbReward.Title;
            SaveRewardState(context.RewardId, rewardData);
            return RewardChangeType.TitleChanged;
        }

        // If we get here, update the stored state for future comparisons
        SaveRewardState(context.RewardId, rewardData);
        
        // No detected change type
        return null;
    }

    private void SaveRewardState(Guid rewardId, ChannelPointsCustomReward rewardData)
    {
        // Store reward state in database for future comparisons
        // Use the existing Reward model to store current state
        var reward = _appDbContext.Rewards.FirstOrDefault(r => r.Id == rewardId);
        
        if (reward == null)
        {
            reward = new()
            {
                Id = rewardId,
                Title = rewardData.Title,
                IsEnabled = rewardData.IsEnabled,
                Permission = "everyone", // Keep original permission field, don't override with pause state
                // Empty response indicates this is a tracking entry, not a user-defined reward
                Response = "",
                // Description format: "cost|backgroundColor|isPaused"
                Description = $"{rewardData.Cost}|{rewardData.BackgroundColor}|{rewardData.IsPaused}"
            };
            _appDbContext.Rewards.Add(reward);
        }
        else
        {
            reward.Title = rewardData.Title;
            reward.IsEnabled = rewardData.IsEnabled;
            // Keep original permission field, don't override with pause state
            // Only update tracking data in Description; preserve existing Response
            reward.Description = $"{rewardData.Cost}|{rewardData.BackgroundColor}|{rewardData.IsPaused}";
            _appDbContext.Rewards.Update(reward);
        }
        
        // Save synchronously to ensure state is persisted before handler runs
        try
        {
            _appDbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save reward state for reward {RewardId}", rewardId);
        }
    }

    private async Task ExecuteChangeHandlerAsync(IRewardChangeHandler handler, RewardChangeContext context, RewardChangeType changeType)
    {
        switch (changeType)
        {
            case RewardChangeType.Enabled:
                await handler.OnEnabled(context);
                break;
            case RewardChangeType.Disabled:
                await handler.OnDisabled(context);
                break;
            case RewardChangeType.PriceChanged:
                await handler.OnPriceChanged(context);
                break;
            case RewardChangeType.TitleChanged:
                await handler.OnTitleChanged(context);
                break;
            case RewardChangeType.DescriptionChanged:
                await handler.OnDescriptionChanged(context);
                break;
            case RewardChangeType.ResumeStatusChanged:
                await handler.OnResumeStatusChanged(context);
                break;
            case RewardChangeType.PauseStatusChanged:
                await handler.OnPauseStatusChanged(context);
                break;
            case RewardChangeType.CooldownChanged:
                await handler.OnCooldownChanged(context);
                break;
            case RewardChangeType.BackgroundColorChanged:
                await handler.OnBackgroundColorChanged(context);
                break;
        }
    }
}

