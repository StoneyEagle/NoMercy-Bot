using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.User;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class UserEventHandler : TwitchEventHandlerBase
{
    public UserEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<UserEventHandler> logger,
        TwitchApiService twitchApiService
    )
        : base(dbContextFactory, logger, twitchApiService) { }

    public override async Task RegisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.UserUpdate += OnUserUpdate;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.UserUpdate -= OnUserUpdate;
        await Task.CompletedTask;
    }

    private async Task OnUserUpdate(object? sender, UserUpdateArgs args)
    {
        Logger.LogInformation("User updated: {User}", args.Payload.Event.UserName);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "user.update",
            args.Payload.Event,
            args.Payload.Event.UserId
        );

        User user = await TwitchApiService.FetchUser(id: args.Payload.Event.UserId);

        await using AppDbContext db = await DbContextFactory.CreateDbContextAsync();
        await db
            .Users.Upsert(user)
            .On(u => u.Id)
            .WhenMatched(
                (u, n) =>
                    new()
                    {
                        DisplayName = n.DisplayName,
                        ProfileImageUrl = n.ProfileImageUrl,
                        OfflineImageUrl = n.OfflineImageUrl,
                        Description = n.Description,
                        BroadcasterType = n.BroadcasterType,
                        UpdatedAt = DateTime.UtcNow,
                    }
            )
            .RunAsync();

        Logger.LogInformation("Updated user info for {User}", args.Payload.Event.UserName);
    }
}
