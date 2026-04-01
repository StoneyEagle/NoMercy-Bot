using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Services.Twitch.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.Models.Polls;
using TwitchLib.EventSub.Websockets;

namespace NoMercyBot.Services.Twitch.EventHandlers;

public class PollEventHandler : TwitchEventHandlerBase
{
    private readonly TwitchChatService _twitchChatService;

    public PollEventHandler(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ILogger<PollEventHandler> logger,
        TwitchApiService twitchApiService,
        TwitchChatService twitchChatService
    )
        : base(dbContextFactory, logger, twitchApiService)
    {
        _twitchChatService = twitchChatService;
    }

    public override async Task RegisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.ChannelPollBegin += OnChannelPollBegin;
        eventSubWebsocketClient.ChannelPollProgress += OnChannelPollProgress;
        eventSubWebsocketClient.ChannelPollEnd += OnChannelPollEnd;
        await Task.CompletedTask;
    }

    public override async Task UnregisterEventHandlersAsync(
        EventSubWebsocketClient eventSubWebsocketClient
    )
    {
        eventSubWebsocketClient.ChannelPollBegin -= OnChannelPollBegin;
        eventSubWebsocketClient.ChannelPollProgress -= OnChannelPollProgress;
        eventSubWebsocketClient.ChannelPollEnd -= OnChannelPollEnd;
        await Task.CompletedTask;
    }

    private async Task OnChannelPollBegin(object? sender, ChannelPollBeginArgs args)
    {
        Logger.LogInformation("Poll started: \"{Title}\"", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.poll.begin",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPollProgress(object? sender, ChannelPollProgressArgs args)
    {
        Logger.LogInformation("Poll progress: \"{Title}\"", args.Payload.Event.Title);

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.poll.progress",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );
    }

    private async Task OnChannelPollEnd(object? sender, ChannelPollEndArgs args)
    {
        Logger.LogInformation(
            "Poll ended: \"{Title}\". Status: {Status}",
            args.Payload.Event.Title,
            args.Payload.Event.Status
        );

        await SaveChannelEvent(
            args.Metadata.GetMessageId(),
            "channel.poll.end",
            args.Payload.Event,
            args.Payload.Event.BroadcasterUserId
        );

        // Send poll results to chat
        if (
            string.Equals(
                args.Payload.Event.Status,
                "completed",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            PollChoice[] choices = args.Payload.Event.Choices;
            int totalVotes = choices.Sum(c => c.Votes ?? 0);

            PollChoice winner = choices.OrderByDescending(c => c.Votes ?? 0).First();
            int winnerVotes = winner.Votes ?? 0;
            string percentage = totalVotes > 0 ? $" ({winnerVotes * 100 / totalVotes}%)" : "";

            string results = string.Join(
                " | ",
                choices.Select(c =>
                {
                    int votes = c.Votes ?? 0;
                    string pct = totalVotes > 0 ? $" ({votes * 100 / totalVotes}%)" : "";
                    return $"{c.Title}: {votes}{pct}";
                })
            );

            string message =
                $"📊 Poll ended: \"{args.Payload.Event.Title}\" — Winner: {winner.Title}{percentage} | {results}";

            await _twitchChatService.SendMessageAsBot(
                args.Payload.Event.BroadcasterUserLogin,
                message
            );
        }
    }
}
