
public class ExcuseCommand : IBotCommand
{
    public string Name => "excuse";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _excuseTemplates =
    {
        "{name}, the official excuse is: It works on my machine.",
        "{name}, here's your excuse: That's not a bug, it's a feature. Ship it.",
        "{name}, the dev team says: The intern did it. And the intern has been let go. The intern was also imaginary.",
        "{name}, try this one: Have you tried clearing your cache? No seriously, clear everything. Burn it down.",
        "{name}, the official statement is: It worked in staging. Production is a different vibe.",
        "{name}, your excuse today is: We're aware of the issue and are actively ignoring it.",
        "{name}, go with: That's a known issue. We've known about it for two years. No fix planned.",
        "{name}, the classic: It must be a DNS issue. It's always DNS. Even when it isn't, it's DNS.",
        "{name}, try: We'll fix it in the next sprint. The next sprint is in 2030.",
        "{name}, the eagle recommends: It's not my code. Check git blame. Actually, don't check git blame.",
        "{name}, use this: The requirements changed. Again. For the fifth time. Today.",
        "{name}, certified excuse: I can't reproduce it. Therefore it doesn't exist. That's science.",
        "{name}, here you go: The tests pass locally. CI is just having a bad day. Like me.",
        "{name}, your developer excuse is: That endpoint is deprecated. Use the other one. Which is also deprecated.",
        "{name}, official response: We're refactoring that module. We've been refactoring it since 2019.",
        "{name}, StoneyEagle approved excuse: The documentation is wrong. I wrote the documentation. I stand by nothing.",
        "{name}, try this: It's a race condition. You just have to click it at exactly the right millisecond.",
        "{name}, the Big Bird suggests: That's tech debt. We'll pay it off eventually. Like student loans.",
        "{name}, go with this one: It compiles. That's my definition of done. Merge it.",
        "{name}, your excuse is: I pushed to main by accident. The accident was not caring.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string template = _excuseTemplates[Random.Shared.Next(_excuseTemplates.Length)];
        string text = template.Replace("{name}", ctx.Message.DisplayName);

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedText),
        };

        (string audioBase64, int durationMs) =
            await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
                segments,
                ctx.CancellationToken
            );
        if (audioBase64 != null)
        {
            IWidgetEventService widgetEventService =
                ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync(
                "channel.chat.message.tts",
                new
                {
                    text,
                    user = new { id = ctx.Message.UserId },
                    audioBase64,
                    provider = "Edge",
                    cost = 0m,
                    characterCount = text.Length,
                    cached = false,
                }
            );
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username,
            text,
            ctx.Message.Id
        );
    }
}

return new ExcuseCommand();
