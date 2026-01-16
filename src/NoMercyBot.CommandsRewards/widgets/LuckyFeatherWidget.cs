using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Widgets;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;

public class LuckyFeatherWidget : IWidgetScript
{
    private static readonly Guid RewardId = Guid.Parse("29c1ea38-96ff-4548-9bbf-ec0b665344c0");
    private const string StorageKey = "LuckyFeather";

    private WidgetScriptContext _context = null!;

    public IReadOnlyList<string> EventTypes { get; } = new[]
    {
        "overlay.feather.steal",
        "overlay.feather.event"
    };

    public Task Init(WidgetScriptContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public async Task OnConnected(WidgetScriptContext context, Ulid widgetId)
    {
        // Get current holder from database
        Record? currentHolder = await context.DatabaseContext.Records
            .Include(r => r.User)
            .Where(r => r.RecordType == StorageKey)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        // Get reward status to check if paused
        string? broadcasterId = context.TwitchApiService.Service?.UserId;
        bool isAvailable = false;

        if (!string.IsNullOrEmpty(broadcasterId))
        {
            try
            {
                ChannelPointsCustomRewardsResponse? rewards =
                    await context.TwitchApiService.GetCustomRewards(broadcasterId, RewardId);
                ChannelPointsCustomRewardsResponseData? reward = rewards?.Data?.FirstOrDefault();
                isAvailable = reward?.IsEnabled == true && reward?.IsPaused == false;
            }
            catch (Exception)
            {
                // Ignore errors fetching reward status
            }
        }

        // Build holder info (uses same structure as theft event for consistency)
        string previousHolderId = currentHolder?.User?.Id ?? broadcasterId ?? "unknown";
        string previousHolderName = currentHolder?.User?.DisplayName ?? "Broadcaster";
        string previousHolderImage = currentHolder?.User?.ProfileImageUrl ?? "";
        string previousHolderColor = currentHolder?.User?.Color ?? "#9147FF";

        object payload = new
        {
            type = "init",
            previousHolder = new
            {
                id = previousHolderId,
                display_name = previousHolderName,
                image_url = previousHolderImage,
                color = previousHolderColor
            },
            isAvailable
        };

        await context.WidgetEventService.PublishEventToWidgetAsync(widgetId, "overlay.feather.event", payload);
    }

    public Task OnDisconnected(WidgetScriptContext context, Ulid widgetId)
    {
        // No cleanup needed for Lucky Feather
        return Task.CompletedTask;
    }
}

return new LuckyFeatherWidget();
