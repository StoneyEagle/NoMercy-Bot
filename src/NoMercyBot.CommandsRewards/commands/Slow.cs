using NoMercyBot.Services.Other;

public class SlowCommand : IBotCommand
{
    public string Name => "slow";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _botIntros =
    {
        "Oh great. {name} wants to say something. Very. Slowly.",
        "{name} has activated sloth mode. This is going to take a while.",
        "Buckle up chat. {name} is about to waste everyone's time. Slowly.",
        "Time has stopped. {name} is speaking in slow motion.",
        "{name} is buffering. Please hold.",
        "Loading message from {name}. Estimated time: the rest of the stream.",
        "The Big Bird could fly south and back before {name} finishes this sentence.",
        "{name} is running on dial-up apparently.",
        "I've seen git clones finish faster than {name} talks.",
        "Warning: {name}'s words per minute just hit single digits.",
        "Chat, grab a snack. {name} is going to be a while.",
        "{name} is speaking at one X speed. There is no fast forward.",
        "If patience is a virtue, {name} is about to make saints of us all.",
        "Scientists have confirmed: {name} is now slower than Internet Explorer.",
        "Fun fact: this message started loading during the last stream.",
        "{name} is approaching absolute zero on the words per minute scale.",
        "Plot twist: {name} actually finished this sentence yesterday. It's just arriving now.",
        "Someone check on {name}'s ping. This latency is unacceptable.",
        "The heat death of the universe might come first, but let's hear {name} out.",
        "{name} has entered power saving mode. Low performance. Maximum drama.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Sloooow what? Usage: !slow <message>",
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

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedIntro),
            TtsService.Segment(userVoice, processedMessage),
        };

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

return new SlowCommand();
