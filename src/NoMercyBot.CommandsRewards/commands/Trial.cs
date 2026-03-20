
public class TrialCommand : IBotCommand
{
    public string Name => "trial";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string JUDGE_VOICE = "en-US-GuyNeural";

    private static readonly string[] _trialTemplates =
    {
        "Order in the chat! The People versus {name}. The charge: excessive lurking in the first degree. Digital forensics confirm {name} has been online for hours without typing a single message. The jury of chatters finds the defendant guilty. Sentence: 50 chat messages, to be served consecutively, starting now.",
        "Court is now in session. {name} stands accused of criminal backseat gaming. Exhibit A from the git log shows {name} typed 'you should have' no fewer than twelve times this stream. The verdict: guilty. {name} is sentenced to play the game themselves on stream while chat backseats them. No mercy.",
        "All rise. The honorable NoMercyBot presiding. {name} is charged with willful failure to use enough emotes. The code of conduct clearly states a minimum of three emotes per message. {name} has been typing in plain text like some kind of animal. Guilty. Sentenced to 10 hugs from the HugFactory.",
        "The prosecution presents its case against {name}. The crime: being suspiciously quiet during a hype moment. While {count} chatters were spamming, {name} sat there in silence. The digital forensics team found zero keystrokes. Verdict: guilty. Sentenced to community service of 30 channel point predictions, all of which will be wrong.",
        "Order! {name} is brought before this court on charges of having criminally bad takes. Exhibit A: their chat history. The jury didn't even need to deliberate. Guilty on all counts. {name} is sentenced to probation from having opinions for the remainder of the stream.",
        "This court calls the case of Chat versus {name}. The charge: not following the channel. The evidence is damning. {name} has been watching for weeks without pressing one simple button. The jury is appalled. Guilty. Sentenced to follow immediately and write a 200-word apology essay in chat.",
        "The defendant {name} is charged with typing too slow. By the time {name} finishes a message, the conversation has moved on three topics. Digital forensics clocked their WPM at an embarrassing 4. Guilty. Sentenced to typing practice: must type the entire bee movie script in chat.",
        "Court is in session. {name} stands accused of command abuse in the second degree. Records show {name} has used bot commands {count} times today. That's not interaction, that's harassment. Guilty. {name} is sentenced to 1 hour of no commands. Cold turkey. The withdrawal will be severe.",
        "All rise for the trial of {name}. The charge: not laughing at the streamer's jokes. Exhibit A from the git log shows zero LOLs, zero KEKWs, and zero polite chuckles from {name} during the last three punchlines. The code of conduct is clear. Guilty. Sentenced to 10 forced LOLs, to be delivered with enthusiasm.",
        "The People versus {name}. Today's charge: unauthorized use of Caps Lock. On the evening in question, {name} deployed a fully capitalized message without a hype train, raid, or any other qualifying event. The jury gasped. Guilty. Sentenced to whisper-only mode for 15 minutes.",
        "Order in the court! {name} faces charges of conspiracy to steal Big Bird's feather. Surveillance footage shows {name} eyeing the feather suspiciously on at least {count} occasions. The eagle has filed a restraining order. Guilty. Sentenced to 20 compliments directed at Big Bird's plumage.",
        "This court has convened to try {name} for the crime of stream sniping the streamer's snacks. Every time Stoney reaches for a drink, {name} types 'hydrate.' Coincidence? The prosecution thinks not. Exhibit A: timestamps. Guilty. Sentenced to donate a water bottle to the streamer's collection.",
        "{name} is hereby charged with lurking with intent to do absolutely nothing. The defendant has been present in chat for over {count} hours across multiple streams, contributing exactly zero messages. The audacity. Guilty. Sentenced to write fifty chat messages by end of stream. The court has spoken.",
        "The court calls {name} to the stand. The charge: reckless deployment of bad puns. {name} has inflicted no fewer than {count} puns upon this chat, each worse than the last. The jury suffered emotional damage. Guilty. {name} is sentenced to only communicate in haiku for the next hour.",
        "All rise. {name} is accused of first-degree clip chimping. Digital forensics reveal {name} has clipped every mildly embarrassing moment from the last {count} streams. The evidence folder is enormous. Guilty as charged. Sentenced to have their most embarrassing moment clipped and pinned in Discord.",
        "Court is now in session for the trial of {name}. The charge: impersonating a moderator. {name} has been telling other chatters to 'behave' and 'read the rules' despite having zero mod privileges. Exhibit A: the chat logs. Exhibit B: the audacity. Guilty. Sentenced to actually become a mod and see how they like it.",
        "The prosecution presents exhibit A: {name}'s message history. The charge: chronic oversharing. {name} has shared details about their lunch, their cat, their cat's lunch, and their opinion on JavaScript frameworks nobody asked about. Guilty. Sentenced to 5 minutes of talking about the actual stream content.",
        "This court finds {name} in contempt of chat. The specific crime: using the wrong emote at the wrong time. {name} dropped a Kappa during a serious moment. The vibe was ruined. {count} chatters filed complaints. Guilty. Sentenced to emote counseling: must learn the appropriate context for each emote before using them again.",
        "Order! {name} is charged with grand theft content. The defendant has been watching the stream, learning the streamer's coding techniques, and using them in their own projects without proper attribution. The git log doesn't lie. Guilty. Sentenced to star the streamer's GitHub repos. All of them.",
        "The jury has reviewed the case against {name}. The charge: being too wholesome. Yes, you heard that right. {name} has been relentlessly positive, supportive, and encouraging in a chat that thrives on sarcasm. It's suspicious. It's unnatural. Guilty of wholesomeness in the first degree. Sentenced to one sarcastic comment. Just one. You can do it, {name}.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string targetName;

        if (ctx.Arguments.Length == 0)
        {
            targetName = ctx.Message.DisplayName;
        }
        else
        {
            targetName = ctx.Arguments[0].Replace("@", "").Trim();

            User targetUser = await ctx.DatabaseContext.Users
                .AsNoTracking()
                .Where(u => u.Username == targetName.ToLower())
                .FirstOrDefaultAsync(ctx.CancellationToken);

            if (targetUser != null)
            {
                targetName = targetUser.DisplayName;
            }
        }

        int count = Random.Shared.Next(7, 42);

        string template = _trialTemplates[Random.Shared.Next(_trialTemplates.Length)];

        string text = template
            .Replace("{name}", targetName)
            .Replace("{count}", count.ToString());

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(JUDGE_VOICE, processedText),
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

return new TrialCommand();
