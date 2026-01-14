using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Widgets;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using Stream = NoMercyBot.Database.Models.Stream;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class ChatEventHandler : TwitchEventHandlerBase
{
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchCommandService _twitchCommandService;
    private readonly TwitchMessageDecorator _twitchMessageDecorator;
    private readonly IWidgetEventService _widgetEventService;
    private readonly TtsService _ttsService;
    private readonly CancellationToken _cancellationToken;
    private Stream? _currentStream;

    public ChatEventHandler(
        AppDbContext dbContext,
        ILogger<ChatEventHandler> logger,
        TwitchApiService twitchApiService,
        TwitchChatService twitchChatService,
        TwitchCommandService twitchCommandService,
        TwitchMessageDecorator twitchMessageDecorator,
        IWidgetEventService widgetEventService,
        TtsService ttsService,
        CancellationToken cancellationToken = default)
        : base(dbContext, logger, twitchApiService)
    {
        _twitchChatService = twitchChatService;
        _twitchCommandService = twitchCommandService;
        _twitchMessageDecorator = twitchMessageDecorator;
        _widgetEventService = widgetEventService;
        _ttsService = ttsService;
        _cancellationToken = cancellationToken;

        // Initialize current stream reference
        _currentStream = DbContext.Streams
            .FirstOrDefault(stream => stream.UpdatedAt == stream.CreatedAt);
    }

    public void SetCurrentStream(Stream? stream)
    {
        _currentStream = stream;
    }

    public override async Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelChatMessage += OnChannelChatMessage;
        eventSubWebsocketClient.ChannelChatClear += OnChannelChatClear;
        eventSubWebsocketClient.ChannelChatClearUserMessages += OnChannelChatClearUserMessages;
        eventSubWebsocketClient.ChannelChatMessageDelete += OnChannelChatMessageDelete;
        eventSubWebsocketClient.ChannelChatNotification += OnChannelChatNotification;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelChatMessage -= OnChannelChatMessage;
        eventSubWebsocketClient.ChannelChatClear -= OnChannelChatClear;
        eventSubWebsocketClient.ChannelChatClearUserMessages -= OnChannelChatClearUserMessages;
        eventSubWebsocketClient.ChannelChatMessageDelete -= OnChannelChatMessageDelete;
        eventSubWebsocketClient.ChannelChatNotification -= OnChannelChatNotification;
        await Task.CompletedTask;
    }

    private async Task OnChannelChatMessage(object sender, ChannelChatMessageArgs args)
    {
        Logger.LogInformation("Chat message: {User}: {Message}",
            args.Notification.Payload.Event.ChatterUserLogin,
            args.Notification.Payload.Event.Message.Text);
        
        try
        {
            User user = await TwitchApiService.GetOrFetchUser(args.Notification.Payload.Event.ChatterUserId);
            User broadcaster = await TwitchApiService.GetOrFetchUser(args.Notification.Payload.Event.BroadcasterUserId);

            ChatMessage chatMessage = new(args.Notification, _currentStream, user, broadcaster);
            if (chatMessage.UserId == TwitchChatService._botUserId && !chatMessage.Message.StartsWith("!so")) return;

            await _twitchMessageDecorator.DecorateMessage(chatMessage);

            if (chatMessage.IsCommand)
                await _twitchCommandService.ExecuteCommand(chatMessage);
            else {
                await _widgetEventService.PublishEventAsync("twitch.chat.message", chatMessage);

                await DbContext.ChatMessages
                    .Upsert(chatMessage)
                    .RunAsync(_cancellationToken);
            }

            // Uncomment if TTS for regular messages is needed
            // await Task.Delay(1000, _cancellationToken);
            // await _ttsService.SendTts(chatMessage.Fragments, chatMessage.UserId, _cancellationToken);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to save chat message from {User} in {Ex}",
                args.Notification.Payload.Event.ChatterUserLogin, e.Message);
            throw;
        }
    }

    private async Task OnChannelChatClear(object sender, ChannelChatClearArgs args)
    {
        Logger.LogInformation("Chat clear: Chat was cleared");

        await SaveChannelEvent(
            args.Notification.Metadata.MessageId,
            "channel.chat.clear",
            args.Notification.Payload.Event,
            args.Notification.Payload.Event.BroadcasterUserId
        );

        await DbContext.ChatMessages
            .Where(c => _currentStream != null && c.StreamId == _currentStream.Id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.DeletedAt, DateTime.UtcNow)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        await _widgetEventService.PublishEventAsync("channel.chat.clear", new Dictionary<string, string?>());
    }

    private async Task OnChannelChatClearUserMessages(object sender, ChannelChatClearUserMessagesArgs args)
    {
        Logger.LogInformation("User messages cleared: {User}'s messages were cleared",
            args.Notification.Payload.Event.TargetUserLogin);

        await SaveChannelEvent(
            args.Notification.Metadata.MessageId,
            "channel.chat.clear.user.messages",
            args.Notification.Payload.Event,
            args.Notification.Payload.Event.BroadcasterUserId,
            args.Notification.Payload.Event.TargetUserId
        );

        await DbContext.ChatMessages
            .Where(c => _currentStream != null 
                && c.StreamId == _currentStream.Id
                && c.UserId == args.Notification.Payload.Event.TargetUserId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.DeletedAt, DateTime.UtcNow)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        Logger.LogInformation("Marked messages as deleted for user {User}",
            args.Notification.Payload.Event.TargetUserLogin);
    }

    private async Task OnChannelChatMessageDelete(object sender, ChannelChatMessageDeleteArgs args)
    {
        Logger.LogInformation("Message deleted: A message from {User} was deleted",
            args.Notification.Payload.Event.TargetUserLogin);

        await SaveChannelEvent(
            args.Notification.Metadata.MessageId,
            "channel.chat.message.delete",
            args.Notification.Payload.Event,
            args.Notification.Payload.Event.BroadcasterUserId,
            args.Notification.Payload.Event.TargetUserId
        );

        await DbContext.ChatMessages
            .Where(c => c.Id == args.Notification.Payload.Event.MessageId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.DeletedAt, DateTime.UtcNow)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        Logger.LogInformation("Marked message as deleted: {MessageId}",
            args.Notification.Payload.Event.MessageId);
    }

    private async Task OnChannelChatNotification(object sender, ChannelChatNotificationArgs args)
    {
        Logger.LogInformation("Chat notification: {Message}",
            args.Notification.Payload.Event.Message.Text);

        await SaveChannelEvent(
            args.Notification.Metadata.MessageId,
            "channel.chat.notification",
            args.Notification.Payload.Event,
            args.Notification.Payload.Event.BroadcasterUserId
        );
    }
}
