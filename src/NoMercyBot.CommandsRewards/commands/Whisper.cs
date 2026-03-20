using NoMercyBot.Services.Other;

public class WhisperCommand : IBotCommand
{
    public string Name => "whisper";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _botIntros =
    {
        "Psst. {name} has something to whisper.",
        "Shhh. {name} wants to say something quietly.",
        "Everyone be quiet. {name} is whispering.",
        "Incoming whisper from {name}.",
        "Lower your volume. {name} is about to get real quiet.",
        "Lean in, chat. {name} doesn't want the mods to hear this.",
        "Breaking news at zero decibels. {name} has a whisper.",
        "{name} has switched to stealth mode. Deploying whisper.",
        "Hold on, Big Bird is picking up a faint signal from {name}.",
        "The eagle's ears just perked up. {name} is whispering something.",
        "{name} is speaking so softly, even the bots are straining to hear.",
        "Audio level dropping. {name} incoming at one percent volume.",
        "Quiet in the chat. {name} is pushing a whisper to production.",
        "This message has been classified as top secret by {name}.",
        "{name} activated ASMR mode. Here it comes.",
        "I can barely hear this, but {name} wants to say something.",
        "Fun fact: {name} could just type this. But no. We're whispering.",
        "Someone tell the lurkers to stop breathing. {name} is whispering.",
        "If you're wearing headphones, good luck. {name} is going subatomic.",
        "Chat, {name} is about to drop the quietest take of all time.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Whisper what? Usage: !whisper <message>",
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

return new WhisperCommand();
