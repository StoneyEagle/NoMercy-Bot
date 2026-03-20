using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Globals.Extensions;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch.Models;
using NoMercyBot.Services.Widgets;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class MonetizationEventHandler : TwitchEventHandlerBase
{
    private static readonly string[] s_snarkyCheerWithMessage =
    [
        "Oh, how touching. {name} oh so graciously gave {bits} bits and had something to say about it too.",
        "Well well well, {name} just threw {bits} bits at us. And apparently they have opinions.",
        "Alert! {name} decided to part with {bits} of their hard-earned bits. They even left a note.",
        "Oh wow, look at {name} splashing out a whole {bits} bits. They also said.",
        "Breaking news! {name} just made it rain with {bits} bits. And get this, they had something to say.",
        "Hold the phone. {name} dropped {bits} bits on us. Let's hear what was so important they had to pay to say it.",
        "Somebody call the press. {name} graced us with {bits} bits. And naturally, they have a statement.",
        "Look at {name}, throwing around {bits} bits like confetti. And they want everyone to hear this.",
        "{name} just flexed with {bits} bits. Bold move. And they had something to get off their chest.",
        "Oh look, {name} bought {bits} bits worth of attention. Let's see if it was worth it.",
    ];

    private static readonly string[] s_snarkyCheerNoMessage =
    [
        "{name} just dropped {bits} bits and vanished into the shadows. A true mystery benefactor.",
        "Oh look, {name} tossed {bits} bits at us and said absolutely nothing. Very mysterious.",
        "{name} just donated {bits} bits in complete silence. The strong and silent type, I see.",
        "A wild {name} appeared! They used {bits} bits! It was... wordless. But effective.",
        "{name} slid {bits} bits across the counter. No words. Just vibes.",
        "{name} threw {bits} bits into the chat and walked away. Not a word. Respect.",
        "{name} dropped {bits} bits and said nothing. Actions speak louder than words, I guess.",
        "Look at that, {name} just tossed {bits} bits our way. A generous soul of few words.",
    ];

    private static readonly Regex s_cheermotePattern = new(@"\b[A-Za-z]+\d+\b", RegexOptions.Compiled);

    private static string StripCheermotes(string message)
    {
        string cleaned = s_cheermotePattern.Replace(message, "");
        return Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
    }

    private readonly TwitchChatService _twitchChatService;
    private readonly IWidgetEventService _widgetEventService;
    private readonly TtsService _ttsService;
    private readonly CancellationToken _cancellationToken;

    public MonetizationEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<MonetizationEventHandler> logger,
        TwitchApiService twitchApiService,
        TwitchChatService twitchChatService,
        IWidgetEventService widgetEventService,
        TtsService ttsService,
        CancellationToken cancellationToken = default)
        : base(dbContextFactory, logger, twitchApiService)
    {
        _twitchChatService = twitchChatService;
        _widgetEventService = widgetEventService;
        _ttsService = ttsService;
        _cancellationToken = cancellationToken;
    }

    public override async Task RegisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelSubscribe += OnChannelSubscribe;
        eventSubWebsocketClient.ChannelSubscriptionGift += OnChannelSubscriptionGift;
        eventSubWebsocketClient.ChannelSubscriptionMessage += OnChannelSubscriptionMessage;
        eventSubWebsocketClient.ChannelCheer += OnChannelCheer;
        eventSubWebsocketClient.ChannelAdBreakBegin += OnChannelAdBreakBegin;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(EventSubWebsocketClient eventSubWebsocketClient)
    {
        eventSubWebsocketClient.ChannelSubscribe -= OnChannelSubscribe;
        eventSubWebsocketClient.ChannelSubscriptionGift -= OnChannelSubscriptionGift;
        eventSubWebsocketClient.ChannelSubscriptionMessage -= OnChannelSubscriptionMessage;
        eventSubWebsocketClient.ChannelCheer -= OnChannelCheer;
        eventSubWebsocketClient.ChannelAdBreakBegin -= OnChannelAdBreakBegin;
        await Task.CompletedTask;
    }

    private async Task OnChannelSubscribe(object? sender, ChannelSubscribeArgs args)
    {
        Logger.LogInformation("Subscribe: {User} subscribed at tier {Tier}",
            args.Payload.Event.UserLogin,
            args.Payload.Event.Tier);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.subscribe",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _widgetEventService.PublishEventAsync("channel.subscribe", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "tier", args.Payload.Event.Tier },
            { "isGift", args.Payload.Event.IsGift.ToString() }
        });

        string message = args.Payload.Event.IsGift 
            ? $"@{args.Payload.Event.UserName} been gifted a tier {args.Payload.Event.Tier} subscription!"
            : $"@{args.Payload.Event.UserName} just subscribed at tier {args.Payload.Event.Tier}! Thank you!";

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            message.ReplaceTierNumbers());
        
        bool widgetSubscriptions = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
        if (widgetSubscriptions)
        {
            await _ttsService.SendCachedTts(
                message.ReplaceTierNumbers(),
                args.Payload.Event.BroadcasterUserId,
                _cancellationToken);
        }
    }

    private async Task OnChannelSubscriptionGift(object? sender, ChannelSubscriptionGiftArgs args)
    {
        Logger.LogInformation("Subscription gift: {User} gifted {Count} subs",
            args.Payload.Event.UserLogin,
            args.Payload.Event.Total);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.subscription.gift",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _widgetEventService.PublishEventAsync("channel.subscription.gift", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "count", args.Payload.Event.Total.ToString() },
            { "tier", args.Payload.Event.Tier },
            { "cumulativeTotal", args.Payload.Event.CumulativeTotal?.ToString() },
            { "isAnonymous", args.Payload.Event.IsAnonymous.ToString() }
        });

        string message = args.Payload.Event.IsAnonymous 
            ? $"A generous user just gifted {args.Payload.Event.Total} subs! Thank you!"
            : $"@{args.Payload.Event.UserName} just gifted {args.Payload.Event.Total} subs! Thank you!";

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            message.ReplaceTierNumbers());
        
        bool widgetSubscriptions = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
        if (widgetSubscriptions)
        {
            await _ttsService.SendCachedTts(
                message.ReplaceTierNumbers(),
                args.Payload.Event.BroadcasterUserId,
                _cancellationToken);
        }
    }

    private async Task OnChannelSubscriptionMessage(object? sender, ChannelSubscriptionMessageArgs args)
    {
        Logger.LogInformation("Resubscribe message: {User} resubscribed for {Months} months",
            args.Payload.Event.UserLogin,
            args.Payload.Event.CumulativeMonths);
        
        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.subscription.message",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );
        
        string eventMessage = args.Payload.Event.Message.Text;

        await _widgetEventService.PublishEventAsync("channel.subscription.message", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "months", args.Payload.Event.CumulativeMonths.ToString() },
            { "tier", args.Payload.Event.Tier },
            { "streak", args.Payload.Event.StreakMonths?.ToString() },
            { "message", eventMessage }
        });

        string chatMessage = args.Payload.Event.StreakMonths > 0
            ? $"@{args.Payload.Event.UserName} just resubscribed for {args.Payload.Event.CumulativeMonths} months with a {args.Payload.Event.StreakMonths}-month streak! You're awesome!"
            : $"@{args.Payload.Event.UserName} just resubscribed for {args.Payload.Event.CumulativeMonths} months! You're awesome!";

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            chatMessage.ReplaceTierNumbers());

        // Handle TTS for subscription message if widgets are subscribed
        bool widgetSubscriptions = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
        if (widgetSubscriptions && !string.IsNullOrEmpty(eventMessage))
        {
            await _ttsService.SendCachedTts(
                eventMessage.ReplaceTierNumbers(), 
                args.Payload.Event.BroadcasterUserId, 
                _cancellationToken);
        }
    }

    private async Task OnChannelCheer(object? sender, ChannelCheerArgs args)
    {
        Logger.LogInformation("Cheer: {User} cheered {Bits} bits",
            args.Payload.Event.IsAnonymous ? "Anonymous" : args.Payload.Event.UserLogin,
            args.Payload.Event.Bits);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.cheer",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId,
            args.Payload.Event.UserId
        );

        await _widgetEventService.PublishEventAsync("channel.cheer", new Dictionary<string, string?>
        {
            { "user", args.Payload.Event.UserName },
            { "bits", args.Payload.Event.Bits.ToString() },
            { "isAnonymous", args.Payload.Event.IsAnonymous.ToString() },
            { "message", args.Payload.Event.Message }
        });

        string chatMessage = args.Payload.Event.IsAnonymous
            ? $"An anonymous user just cheered {args.Payload.Event.Bits} bits! Thank you!"
            : $"@{args.Payload.Event.UserName} just cheered {args.Payload.Event.Bits} bits! Thank you!";

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            chatMessage);

        // Multi-voice TTS for non-anonymous cheers
        bool widgetSubscriptions = await _widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
        if (!widgetSubscriptions || args.Payload.Event.IsAnonymous) return;

        string userName = args.Payload.Event.UserName;
        int bits = args.Payload.Event.Bits;
        string cleanedMessage = !string.IsNullOrWhiteSpace(args.Payload.Event.Message)
            ? StripCheermotes(args.Payload.Event.Message)
            : "";

        if (!string.IsNullOrWhiteSpace(cleanedMessage))
        {
            // User has a message: bot snarky intro (broadcaster voice) + user message (user's voice)
            string template = s_snarkyCheerWithMessage[Random.Shared.Next(s_snarkyCheerWithMessage.Length)];
            string botText = template.Replace("{name}", userName).Replace("{bits}", bits.ToString());

            await _ttsService.SendMultiVoiceTtsAsync(
            [
                (botText, args.Payload.Event.BroadcasterUserId),
                (cleanedMessage, args.Payload.Event.UserId),
            ],
                args.Payload.Event.UserId,
                _cancellationToken);
        }
        else
        {
            // No message: just bot snarky acknowledgement in broadcaster voice
            string template = s_snarkyCheerNoMessage[Random.Shared.Next(s_snarkyCheerNoMessage.Length)];
            string botText = template.Replace("{name}", userName).Replace("{bits}", bits.ToString());

            await _ttsService.SendCachedTts(botText, args.Payload.Event.BroadcasterUserId, _cancellationToken);
        }
    }
    
    private async Task OnChannelAdBreakBegin(object? sender, ChannelAdBreakBeginArgs args)
    {
        Logger.LogInformation("Ad break started for {Duration} seconds", args.Payload.Event.DurationSeconds);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.ad.break.begin",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );

        await _widgetEventService.PublishEventAsync("channel.ad.break.begin", new Dictionary<string, string?>
        {
            { "channel", args.Payload.Event.BroadcasterUserLogin },
            { "duration", args.Payload.Event.DurationSeconds.ToString() }
        });

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            $"An ad break has started for {args.Payload.Event.DurationSeconds.ToHumanTime()}. Please stay tuned!");
        
        // await _ttsService.SendCachedTts(
        //     "Attention chat: Jeff Bezos just checked his bank account and—surprise—he’s a few billion short for his next rocket. Please enjoy this ad break and help him reach orbit!",
        //     args.Payload.Event.BroadcasterUserId,
        //     CancellationToken.None);

        await Task.Delay(args.Payload.Event.DurationSeconds * 1000, CancellationToken.None);
        Logger.LogInformation("Ad break ended");

        await _twitchChatService.SendMessageAsBot(
            args.Payload.Event.BroadcasterUserLogin,
            "The ad break has ended. Thanks for your patience!");
    }
}
