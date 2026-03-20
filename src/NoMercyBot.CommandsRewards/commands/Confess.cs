using NoMercyBot.Services.Other;

public class ConfessCommand : IBotCommand
{
    public string Name => "confess";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _confessionIntros =
    {
        "Anonymous confession incoming.",
        "Someone has something to get off their chest.",
        "A mysterious chatter confesses.",
        "From the shadows, a confession.",
        "The confessional is open.",
        "Someone who shall remain nameless says.",
        "A dark secret emerges from chat.",
        "An unidentified chatter has entered the confessional.",
        "The Big Bird has intercepted an anonymous message.",
        "Someone in this chat is about to expose themselves. Figuratively.",
        "A coward with something to say. And I respect that.",
        "This confession was found in an unsigned commit.",
        "The following message was pushed anonymously. No git blame available.",
        "Alert. Unattributed hot take detected.",
        "A nameless soul whispers from the void of chat.",
        "Someone is hiding behind anonymous mode. Smart.",
        "I have been asked to read something. The author has fled the scene.",
        "One of you typed this. You know who you are.",
        "Deploying anonymous confession. Sender has been redacted.",
        "A ghost in the chat has something to say.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Confess what? Your secrets won't tell themselves. Usage: !confess <message>",
                ctx.Message.Id);
            return;
        }

        string confession = string.Join(" ", ctx.Arguments);
        string intro = _confessionIntros[Random.Shared.Next(_confessionIntros.Length)];
        string fullText = $"{intro} \"{confession}\"";

        List<NoMercyBot.Database.Models.TtsVoice> allVoices = await ctx.DatabaseContext.TtsVoices
            .AsNoTracking()
            .Where(v => v.IsActive && v.Provider == "Edge")
            .ToListAsync(ctx.CancellationToken);

        string confessionVoice = "en-US-EmmaMultilingualNeural";
        if (allVoices.Count > 0)
            confessionVoice = allVoices[Random.Shared.Next(allVoices.Count)].SpeakerId;

        string processedIntro = await ctx.TtsService.ApplyUsernamePronunciationsAsync(intro);
        string processedConfession = await ctx.TtsService.ApplyUsernamePronunciationsAsync(confession);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedIntro),
            TtsService.Segment(confessionVoice, processedConfession),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 != null)
        {
            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
            {
                text = fullText,
                user = new { id = ctx.BroadcasterId },
                audioBase64,
                provider = "Edge",
                cost = 0m,
                characterCount = fullText.Length,
                cached = false,
            });
        }

        await ctx.TwitchChatService.SendMessageAsBot(
            ctx.Message.Broadcaster.Username, fullText);
    }
}

return new ConfessCommand();
