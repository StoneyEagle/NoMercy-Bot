
public class TranslateCommand : IBotCommand
{
    public string Name => "translate";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly (string voiceId, string language)[] _foreignVoices =
    {
        ("ja-JP-KeitaNeural", "Japanese"),
        ("de-DE-ConradNeural", "German"),
        ("fr-FR-HenriNeural", "French"),
        ("es-ES-AlvaroNeural", "Spanish"),
        ("ko-KR-InJoonNeural", "Korean"),
        ("zh-CN-YunxiNeural", "Chinese"),
        ("it-IT-DiegoNeural", "Italian"),
        ("ru-RU-DmitryNeural", "Russian"),
    };

    private static readonly string[] _introTemplates =
    {
        "Alright {name}, translating your message to {language}. Don't blame me for the accent.",
        "{name} requested a {language} translation. The Big Bird is now bilingual. You're welcome.",
        "One {language} translation coming right up for {name}. I am not responsible for international incidents.",
        "{name} wants {language}? Fine. Deploying foreign language module. No warranty included.",
        "Translating to {language} for {name}. My {language} is about as good as your code. So, questionable.",
        "Roger that {name}. Switching to {language} mode. This is definitely how {language} works.",
        "{name} just unlocked the {language} DLC. Buffering accent now.",
        "The eagle is going international for {name}. {language} translation incoming. Brace yourselves.",
        "You want {language}, {name}? Sure. I took one Duolingo lesson. That counts, right?",
        "Initiating {language} protocol for {name}. If this sounds wrong, it's a feature.",
        "{name} has requested {language}. Compiling accent. Please hold.",
        "Switching to {language} for {name}. StoneyEagle's bot goes global. No refunds.",
        "One moment {name}, loading the {language} language pack. It was a free download so manage expectations.",
        "{name}, your {language} translation has been approved. Merging to production now.",
        "The Big Bird speaks {language} now, {name}. At least that's what the pull request says.",
        "{name} wants to go international. Fine. Here's your message in totally accurate {language}.",
        "Translating for {name}. Fun fact: I learned {language} from reading stack overflow in incognito mode.",
        "Processing {language} request from {name}. My neural network says this is close enough.",
        "{name} asked for {language} and the eagle delivers. Quality not guaranteed. Actually, quality not even attempted.",
        "Deploying {language} voice module for {name}. This is what peak localization looks like.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Translate what? I'm a bot, not a mind reader. Usage: !translate <message>",
                ctx.Message.Id
            );
            return;
        }

        string message = string.Join(" ", ctx.Arguments);
        var picked = _foreignVoices[Random.Shared.Next(_foreignVoices.Length)];
        string foreignVoice = picked.voiceId;
        string language = picked.language;

        string introTemplate = _introTemplates[Random.Shared.Next(_introTemplates.Length)];
        string intro = introTemplate
            .Replace("{name}", ctx.Message.DisplayName)
            .Replace("{language}", language);

        string fullText = $"{intro} \"{message}\"";

        string processedIntro = await ctx.TtsService.ApplyUsernamePronunciationsAsync(intro);
        string processedMessage = await ctx.TtsService.ApplyUsernamePronunciationsAsync(message);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedIntro),
            TtsService.Segment(foreignVoice, processedMessage),
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
                    text = fullText,
                    user = new { id = ctx.Message.UserId },
                    audioBase64,
                    provider = "Edge",
                    cost = 0m,
                    characterCount = fullText.Length,
                    cached = false,
                }
            );
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username,
            fullText,
            ctx.Message.Id
        );
    }
}

return new TranslateCommand();
