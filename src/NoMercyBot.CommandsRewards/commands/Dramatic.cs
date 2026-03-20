using NoMercyBot.Services.Other;

public class DramaticCommand : IBotCommand
{
    public string Name => "dramatic";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _botIntros =
    {
        "In a world. Where {name} had something to say.",
        "Coming soon to a chat near you. A message. From {name}.",
        "The following contains dramatic content. Viewer discretion is advised. {name} speaks.",
        "This summer. One chatter. One message. {name} presents.",
        "From the studio that brought you lurking. And backseating. Comes a new epic. By {name}.",
        "They said it couldn't be typed. They were wrong. {name}. The Message.",
        "Every generation has a hero. This chat has {name}. Close enough.",
        "Based on true events. Mostly. {name} presents a story for the ages.",
        "The eagle soars. The chat waits. And {name}. Speaks.",
        "One channel. One moment. One message that will change absolutely nothing. {name}.",
        "Critics are calling it. The most unnecessary message of the year. {name} delivers.",
        "Rated M for Mediocre. A {name} production.",
        "Previously on this stream. Nothing this dramatic happened. Until now. {name}.",
        "The saga continues. Chapter unknown. Author: {name}.",
        "When the bits are down. And the subs run dry. One chatter rises. {name}.",
        "A tale of courage. Of bandwidth. Of questionable life choices. Starring {name}.",
        "Executive produced by the Big Bird. Written and directed by {name}.",
        "No chatters were harmed in the making of this message. Except maybe {name}.",
        "The chat will remember this. Probably not. But {name} tried.",
        "Academy Award consideration for outstanding achievement in typing. {name}.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} In a world... where you forgot to type a message. Usage: !dramatic <message>",
                ctx.Message.Id);
            return;
        }

        string message = string.Join(" ", ctx.Arguments);
        string processedMessage = await ctx.TtsService.ApplyUsernamePronunciationsAsync(message);
        string userVoice = await ctx.TtsService.GetSpeakerIdForUserAsync(
            ctx.Message.UserId, ctx.CancellationToken) ?? "en-US-EmmaMultilingualNeural";

        string intro = _botIntros[Random.Shared.Next(_botIntros.Length)]
            .Replace("{name}", ctx.Message.DisplayName);
        string processedIntro = await ctx.TtsService.ApplyUsernamePronunciationsAsync(intro);

        // Build dramatic effect by inserting silence between word groups
        string[] words = processedMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedIntro),
            TtsService.Silence(600),
        };

        // Group words into chunks of 2-3, with silence pauses between them
        var chunk = new System.Text.StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            chunk.Append(words[i]);
            if (i < words.Length - 1)
                chunk.Append(' ');

            if ((i + 1) % 3 == 0 || i == words.Length - 1)
            {
                segments.Add(TtsService.Segment(userVoice, chunk.ToString()));
                if (i < words.Length - 1)
                    segments.Add(TtsService.Silence(500));
                chunk.Clear();
            }
        }

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 == null) return;

        IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
        await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
        {
            text = message,
            user = new { id = ctx.Message.UserId },
            audioBase64,
            provider = "Edge",
            cost = 0m,
            characterCount = message.Length,
            cached = false,
        });
    }
}

return new DramaticCommand();
