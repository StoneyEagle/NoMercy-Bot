
public class DetectiveCommand : IBotCommand
{
    public string Name => "detective";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string DETECTIVE_VOICE = "en-GB-RyanNeural";

    private static readonly string[] _investigationTemplatesWithData =
    {
        "The name's NoMercy. I've been tailing {name} for weeks. Their last known transmission read: \"{lastMessage}\". {messageCount} messages on file. Every single one of them... suspicious.",
        "Case file seven-seven-three. Subject: {name}. I've logged {messageCount} messages from this individual. The latest one said: \"{lastMessage}\". What it means, I'm still trying to figure out. This city never gives you easy answers.",
        "I found {name}'s prints all over this chat. {messageCount} messages deep. The most recent one read: \"{lastMessage}\". It was a cold night. But this trail? It just got warm.",
        "The dame walked into my office and dropped a name. {name}. {messageCount} entries in the database. Last words recorded: \"{lastMessage}\". Either this chatter is clean, or they're the best liar this side of Twitch.",
        "Rain on the window. Bourbon in the glass. And a case file marked {name}. {messageCount} messages. Last known words: \"{lastMessage}\". The Big Bird hired me to get answers. So far, all I've got are questions.",
        "They call themselves {name}. {messageCount} messages in the system. I pulled their latest communique: \"{lastMessage}\". It didn't make sense. In this line of work, the ones that don't make sense are the ones that matter most.",
        "Stakeout report. Subject {name} has been observed transmitting {messageCount} messages into this channel. Most recent intercept: \"{lastMessage}\". I've seen a lot of things in this chat. But this? This is new.",
        "I cracked open the {name} file. {messageCount} entries. Every message a piece of a puzzle I didn't ask to solve. The last piece read: \"{lastMessage}\". The picture it paints? Disturbing.",
        "The streets of this chat are dark. But {name} leaves footprints everywhere. {messageCount} of them. Their latest whisper to the void: \"{lastMessage}\". I've been in this business long enough to know when someone's hiding something.",
        "Another night. Another case. {name}. {messageCount} messages logged under that alias. The freshest one: \"{lastMessage}\". In my experience, the chatty ones always have the most to hide.",
    };

    private static readonly string[] _investigationTemplatesNoData =
    {
        "Case file: {name}. No prior records. No fingerprints. No chat history. Either this is a ghost, or we're dealing with a professional. The kind that lurks in the shadows and never leaves a trace.",
        "I ran the name {name} through every database I've got. Nothing. Clean as a fresh repo. That's what worries me. Nobody's that clean. Not in this chat.",
        "The file on {name} is empty. Blank. Like staring into the void. In thirty years of detective work, the empty files are always the most dangerous ones.",
        "I went looking for {name} in the records. Found nothing. Zip. Nada. A phantom. The Big Bird swears they exist, but the evidence says otherwise. This case just got interesting.",
        "{name}. The one that got away. No messages. No trail. No alibi because they don't need one. You can't pin anything on a shadow. And believe me. I've tried.",
        "They sent me after {name}. But there's nothing to find. Not a single message on record. Either they're innocent, or they're the most careful criminal this chat has ever seen. My gut says the latter.",
        "Dead end after dead end. {name} is a ghost in the machine. No history. No evidence. Just a username flickering in the darkness like a neon sign in a back alley. This one's going to keep me up at night.",
        "The {name} case. It haunts me. Every lead goes cold. Every database comes up empty. Whoever they are, they've covered their tracks better than anyone I've ever investigated.",
        "I followed {name} into the darkest corners of this chat. Nothing. They move like smoke through a server room. No logs. No records. Just the faint echo of a username nobody can trace.",
        "Some say {name} doesn't exist at all. A myth. A legend whispered among the regulars. But I've been doing this too long to believe in fairy tales. They're out there. And I'll find them.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string targetName = ctx.Arguments.Length == 0
            ? ctx.Message.DisplayName
            : ctx.Arguments[0].Replace("@", "").Trim();

        User targetUser = await ctx.DatabaseContext.Users
            .AsNoTracking()
            .Where(u => u.Username == targetName.ToLower())
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (targetUser != null)
            targetName = targetUser.DisplayName;

        string investigation;

        if (targetUser != null)
        {
            int messageCount = await ctx.DatabaseContext.ChatMessages
                .AsNoTracking()
                .Where(m => m.UserId == targetUser.Id && !m.DeletedAt.HasValue)
                .CountAsync(ctx.CancellationToken);

            string lastMsg = await ctx.DatabaseContext.ChatMessages
                .AsNoTracking()
                .Where(m => m.UserId == targetUser.Id
                    && !m.IsCommand
                    && !m.DeletedAt.HasValue
                    && m.Message != null
                    && m.Message.Length > 0)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Message)
                .FirstOrDefaultAsync(ctx.CancellationToken);

            if (messageCount > 0 && lastMsg != null)
            {
                if (lastMsg.Length > 80)
                    lastMsg = lastMsg[..77] + "...";

                investigation = _investigationTemplatesWithData[Random.Shared.Next(_investigationTemplatesWithData.Length)]
                    .Replace("{name}", targetName)
                    .Replace("{messageCount}", messageCount.ToString())
                    .Replace("{lastMessage}", lastMsg);
            }
            else
            {
                investigation = _investigationTemplatesNoData[Random.Shared.Next(_investigationTemplatesNoData.Length)]
                    .Replace("{name}", targetName);
            }
        }
        else
        {
            investigation = _investigationTemplatesNoData[Random.Shared.Next(_investigationTemplatesNoData.Length)]
                .Replace("{name}", targetName);
        }

        string chatText = investigation;
        if (chatText.Length > 450)
            chatText = chatText[..447] + "...";

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(investigation);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(DETECTIVE_VOICE, processedText),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 != null)
        {
            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
            {
                text = investigation,
                user = new { id = ctx.Message.UserId },
                audioBase64,
                provider = "Edge",
                cost = 0m,
                characterCount = investigation.Length,
                cached = false,
            });
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username, chatText, ctx.Message.Id);
    }
}

return new DetectiveCommand();
