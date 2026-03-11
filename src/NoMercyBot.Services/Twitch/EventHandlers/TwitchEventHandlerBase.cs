using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Twitch.EventHandlers.Interfaces;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public abstract class TwitchEventHandlerBase : ITwitchEventHandler
{
    protected readonly IDbContextFactory<AppDbContext> DbContextFactory;
    protected readonly ILogger Logger;
    protected readonly TwitchApiService TwitchApiService;

    protected TwitchEventHandlerBase(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger logger,
        TwitchApiService twitchApiService)
    {
        DbContextFactory = dbContextFactory;
        Logger = logger;
        TwitchApiService = twitchApiService;
    }

    public abstract Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient);
    public abstract Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient);

    protected async Task SaveChannelEvent(string id, string type, object data, string? channelId = null, string? userId = null)
    {
        try
        {
            if (userId != null)
                await TwitchApiService.GetOrFetchUser(id: userId);

            _ = await TwitchApiService.GetOrFetchUser(id: channelId);

            await using AppDbContext db = await DbContextFactory.CreateDbContextAsync();
            await db.ChannelEvents
                .Upsert(new()
                {
                    Id = id,
                    Type = type,
                    Data = data,
                    ChannelId = channelId,
                    UserId = userId
                })
                .On(p => p.Id)
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to save channel event: {Type} for {ChannelId}", type, channelId);
            throw;
        }
    }
}
