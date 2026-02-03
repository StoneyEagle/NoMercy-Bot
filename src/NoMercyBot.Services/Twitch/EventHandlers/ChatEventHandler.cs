using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch.Models;
using NoMercyBot.Services.Widgets;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using Stream = NoMercyBot.Database.Models.Stream;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class ChatEventHandler : TwitchEventHandlerBase
{
    private readonly TwitchChatService _twitchChatService;
    private readonly TwitchCommandService _twitchCommandService;
    private readonly TwitchMessageDecorator _twitchMessageDecorator;
    private readonly IWidgetEventService _widgetEventService;
    private readonly TtsService _ttsService;
    private readonly ShoutoutQueueService _shoutoutQueueService;
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
        ShoutoutQueueService shoutoutQueueService,
        CancellationToken cancellationToken = default)
        : base(dbContext, logger, twitchApiService)
    {
        _twitchChatService = twitchChatService;
        _twitchCommandService = twitchCommandService;
        _twitchMessageDecorator = twitchMessageDecorator;
        _widgetEventService = widgetEventService;
        _ttsService = ttsService;
        _shoutoutQueueService = shoutoutQueueService;
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

    private async Task OnChannelChatMessage(object? sender, ChannelChatMessageArgs args)
    {
        Logger.LogInformation("Chat message: {User}: {Message}",
            args.Payload.Event.ChatterUserLogin,
            args.Payload.Event.Message.Text);
        
        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.chat.message",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.ChatterUserId
        );
        
        try
        {
            User user = await TwitchApiService.GetOrFetchUser(args.Payload.Event.ChatterUserId);
            User broadcaster = await TwitchApiService.GetOrFetchUser(args.Payload.Event.BroadcasterUserId);

            ChatMessage chatMessage = new(args, _currentStream, user, broadcaster);
            if (chatMessage.UserId == TwitchChatService._botUserId && !chatMessage.Message.StartsWith("!so")) return;

            await _twitchMessageDecorator.DecorateMessage(chatMessage);

            if (chatMessage.IsCommand)
                await _twitchCommandService.ExecuteCommand(chatMessage);
            else {
                await _widgetEventService.PublishEventAsync("twitch.chat.message", chatMessage);

                await DbContext.ChatMessages
                    .Upsert(chatMessage)
                    .RunAsync(_cancellationToken);

                // Auto-shoutout: check if this user should be shouted out
                if (chatMessage.UserId != TwitchChatService._botUserId)
                {
                    await _shoutoutQueueService.OnUserChatMessage(
                        chatMessage.BroadcasterId,
                        chatMessage.UserId,
                        chatMessage.Broadcaster.Username);
                }
            }

            // Uncomment if TTS for regular messages is needed
            // await Task.Delay(1000, _cancellationToken);
            // await _ttsService.SendTts(chatMessage.Fragments, chatMessage.UserId, _cancellationToken);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to save chat message from {User} in {Ex}",
                args.Payload.Event.ChatterUserLogin, e.Message);
            throw;
        }
    }

    private async Task OnChannelChatClear(object? sender, ChannelChatClearArgs args)
    {
        Logger.LogInformation("Chat clear: Chat was cleared");

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.chat.clear",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );

        await DbContext.ChatMessages
            .Where(c => _currentStream != null && c.StreamId == _currentStream.Id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.DeletedAt, DateTime.UtcNow)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        await _widgetEventService.PublishEventAsync("channel.chat.clear", new Dictionary<string, string?>());
    }

    private async Task OnChannelChatClearUserMessages(object? sender, ChannelChatClearUserMessagesArgs args)
    {
        Logger.LogInformation("User messages cleared: {User}'s messages were cleared",
            args.Payload.Event.TargetUserLogin);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.chat.clear.user.messages",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.TargetUserId
        );

        await DbContext.ChatMessages
            .Where(c => _currentStream != null 
                && c.StreamId == _currentStream.Id
                && c.UserId == args.Payload.Event.TargetUserId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.DeletedAt, DateTime.UtcNow)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        Logger.LogInformation("Marked messages as deleted for user {User}",
            args.Payload.Event.TargetUserLogin);
    }

    private async Task OnChannelChatMessageDelete(object? sender, ChannelChatMessageDeleteArgs args)
    {
        Logger.LogInformation("Message deleted: A message from {User} was deleted",
            args.Payload.Event.TargetUserLogin);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.chat.message.delete",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.TargetUserId
        );

        await DbContext.ChatMessages
            .Where(c => c.Id == args.Payload.Event.MessageId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.DeletedAt, DateTime.UtcNow)
                .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), cancellationToken: _cancellationToken);

        Logger.LogInformation("Marked message as deleted: {MessageId}",
            args.Payload.Event.MessageId);
    }

    private async Task OnChannelChatNotification(object? sender, ChannelChatNotificationArgs args)
    {
        Logger.LogInformation("Chat notification: {Message}",
            args.Payload.Event.Message.Text);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.chat.notification",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }
}
