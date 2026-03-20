public class YellCommand : IBotCommand
{
    public string Name => "yell";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";
    private const string YELL_VOICE = "en-US-GuyNeural";

    private static readonly string[] _botIntros =
    {
        "EVERYBODY SHUT UP. {name} HAS SOMETHING TO SAY.",
        "CLEAR THE CHAT. {name} IS YELLING.",
        "WARNING. {name} is about to be very loud.",
        "Brace yourselves. {name} is screaming.",
        "CODE RED. {name} HAS CAPS LOCK AND IS NOT AFRAID TO USE IT.",
        "Big Bird just flinched. {name} is about to go full volume.",
        "RIP headphone users. {name} is deploying maximum decibels.",
        "EMERGENCY ALERT. THIS IS NOT A DRILL. {name} IS YELLING.",
        "{name} just pushed a hotfix directly to your eardrums.",
        "Chat, lower your volume now. {name} has chosen violence.",
        "The eagle has detected a noise complaint incoming from {name}.",
        "Whoever gave {name} a microphone, this is your fault.",
        "ATTENTION. {name} has escalated this chat to DEFCON 1.",
        "{name} is about to make the stream's audio peak look like a flatline.",
        "I hope you have insurance on those speakers. {name} is yelling.",
        "Your neighbors are about to file a complaint. Thanks, {name}.",
        "Mods, brace for impact. {name} is going nuclear.",
        "THIS JUST IN. {name} FORGOT WHERE THE INSIDE VOICE BUTTON IS.",
        "{name} has entered beast mode. Volume slider is irrelevant.",
        "The only thing louder than {name} right now is the Big Bird's disappointment.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} YELL WHAT?! Usage: !yell <message>",
                ctx.Message.Id);
            return;
        }

        string rawMessage = string.Join(" ", ctx.Arguments);
        // ALL CAPS for chat display
        string chatMessage = rawMessage.ToUpper();
        // For TTS: keep short words (acronyms like OMG, GG, WTF) uppercase, rest in original case
        // so TTS spells out acronyms but speaks normal words naturally
        string ttsMessage = string.Join(" ", rawMessage.Split(' ').Select(w =>
        {
            // If the original word was already ALL CAPS and short (2-5 chars), keep it — it's likely an acronym
            if (w.Length >= 2 && w.Length <= 5 && w == w.ToUpper() && w.All(c => char.IsLetter(c)))
                return w;
            // Otherwise use original case
            return w;
        }));

        string processedMessage = await ctx.TtsService.ApplyUsernamePronunciationsAsync(ttsMessage);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(YELL_VOICE, processedMessage),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 == null) return;

        IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
        await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
        {
            text = chatMessage,
            user = new { id = ctx.Message.UserId },
            audioBase64,
            provider = "Edge",
            cost = 0m,
            characterCount = chatMessage.Length,
            cached = false,
        });
    }
}

return new YellCommand();
