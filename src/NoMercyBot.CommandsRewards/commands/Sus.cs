
public class SusCommand : IBotCommand
{
    public string Name => "sus";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string SUS_VOICE = "en-US-GuyNeural";

    // Investigation report templates (15)
    private static readonly string[] _investigationTemplates =
    {
        "INVESTIGATION REPORT: Subject {name}. {botPercent} percent of their messages are commands. They talk to bots more than humans. Sus score: {susScore} out of 100.",
        "CLASSIFIED DOSSIER on {name}. {messages} messages logged. {commands} were commands. {songs} song requests filed. This is not a chatter. This is an operative. Sus score: {susScore}.",
        "INCIDENT REPORT: {name} has been observed issuing {commands} commands over {days} days. Bot interaction rate: {botPercent} percent. Recommend immediate surveillance. Sus score: {susScore}.",
        "INTEL BRIEFING: Asset {name}. {messages} total transmissions. {botPercent} percent directed at bots. {songs} music requests used as coded messages. Threat level: {susScore} out of 100.",
        "FIELD REPORT: Agent Big Bird assigned me to investigate {name}. Findings: {messages} messages, {commands} commands, {botPercent} percent bot talk rate. Conclusion: extremely sus. Score: {susScore}.",
        "CASE NUMBER 4-0-4: {name}. {days} days of activity. {messages} messages sent. {botPercent} percent were commands to bots. They're either a bot themselves or wish they were. Sus rating: {susScore}.",
        "SURVEILLANCE LOG: Target {name}. Observed sending {commands} commands in {messages} total messages. That's a {botPercent} percent bot interaction rate. Nobody talks to machines that much unless they ARE one. Sus score: {susScore}.",
        "FORENSIC ANALYSIS of {name}'s chat history. {messages} entries. {songs} song requests. {botPercent} percent command usage. The data doesn't lie. The chatter might. Sus score: {susScore}.",
        "INTERNAL MEMO: Re {name}. {days} days in the channel. {messages} messages. {commands} of those were commands. I've seen less bot interaction on a Raspberry Pi. Sus assessment: {susScore} out of 100.",
        "EVIDENCE FILE: {name}. Messages: {messages}. Commands: {commands}. Songs requested: {songs}. Days active: {days}. Bot talk ratio: {botPercent} percent. Every metric screams sus. Official score: {susScore}.",
        "INVESTIGATION UPDATE: {name} remains a person of interest. {botPercent} percent of their chat activity is directed at bots. In my professional opinion, that's not normal. Sus score: {susScore}.",
        "REPORT TO STONEYEAGLE: Subject {name} has a {botPercent} percent bot communication rate across {messages} messages. They've also requested {songs} songs, presumably to distract from their suspicious behavior. Score: {susScore}.",
        "DEBRIEF: Target {name}. Spent {days} days in this channel. Sent {messages} messages. {commands} of them were commands. It's like they only show up to boss the bots around. Suspicion level: {susScore}.",
        "SECURITY ADVISORY: {name} has triggered {susScore} out of 100 on the sus meter. {messages} messages. {botPercent} percent bot commands. {songs} song requests. Big Bird wants answers.",
        "TOP SECRET: {name}. Chat footprint: {messages} messages over {days} days. Command ratio: {botPercent} percent. Song requests: {songs}. This dossier has been flagged with a sus score of {susScore}.",
    };

    // Among Us style templates (10)
    private static readonly string[] _amongUsTemplates =
    {
        "{name} is acting suspicious. {botPercent} percent bot interaction rate. {songs} songs requested to cover their tracks. I'm calling an emergency meeting.",
        "I saw {name} vent. They have {commands} commands and {botPercent} percent bot talk ratio. That's textbook impostor behavior. Vote them out.",
        "{name} was NOT doing tasks. {messages} messages and {botPercent} percent of them were commands. Real crewmates don't spend that much time at the admin panel. Emergency meeting.",
        "Dead body reported. {name} was the last one seen with {commands} command entries and {songs} song requests. {botPercent} percent sus. Skip or vote?",
        "{name} acting kinda sus not gonna lie. Been here {days} days. {messages} messages. {botPercent} percent bot commands. I think they're the impostor.",
        "Where was {name}? They claim electrical but their logs show {commands} commands and {songs} song requests. Nobody does tasks that fast. They were definitely venting.",
        "{name} self-reported. {messages} messages but {botPercent} percent are just commands. That's not how a crewmate behaves. That's how an impostor with a bot addiction behaves.",
        "Trust no one. Especially not {name}. {botPercent} percent bot interaction. {songs} songs to drown out the sound of them sabotaging. Sus score: {susScore}. Voting {name}.",
        "I was in security watching {name} on cams. {commands} commands fired off in {days} days. {botPercent} percent bot talk rate. They're faking tasks. I'm certain of it.",
        "{name} was ejected. {name} was the impostor. Just kidding. But with a {botPercent} percent bot command rate and {susScore} sus score, they might as well be.",
    };

    // Conspiracy theory templates (10)
    private static readonly string[] _conspiracyTemplates =
    {
        "I've connected the dots on {name}. {messages} messages. {commands} commands. {songs} song requests. The pattern is clear. I just don't know what it means.",
        "Wake up sheeple. {name} has sent exactly {messages} messages over {days} days. {botPercent} percent were commands. Coincidence? Big Bird doesn't think so.",
        "They don't want you to know this, but {name} has a {botPercent} percent bot interaction rate. {songs} song requests. {commands} commands. Follow the money. Or in this case, the channel points.",
        "The truth about {name}: {messages} messages. {commands} commands. {songs} song requests. {days} days. If you rearrange these numbers you get... still suspicious numbers.",
        "I went down the {name} rabbit hole. {messages} messages deep. {botPercent} percent bot commands. {songs} songs requested. It all leads back to one thing: they're definitely up to something.",
        "{name} is a plant. {days} days. {messages} messages. {botPercent} percent commands. Nobody is this consistent without an agenda. The eagles aren't just watching, they're taking notes.",
        "Jet fuel can't melt steel beams, but it CAN explain {name}'s {botPercent} percent bot command ratio. Actually no it can't. Nothing can explain that. Sus score: {susScore}.",
        "The {name} files. Page one: {messages} messages. Page two: {commands} commands. Page three: {songs} songs requested. Page four is redacted. What are they hiding? Sus score: {susScore}.",
        "Think about it. {name}. {messages} messages in {days} days. {botPercent} percent directed at bots. {songs} song requests. The simulation is leaking and {name} is the glitch.",
        "I'm not saying {name} is a sleeper agent, but {botPercent} percent bot communication rate, {commands} commands logged, and {songs} songs requested? The evidence speaks for itself. {susScore} out of 100.",
    };

    // Behavioral analysis templates (10)
    private static readonly string[] _behavioralTemplates =
    {
        "{name} has been here {days} days. They've sent {messages} messages, {botPercent} percent of which were commands. This is not normal human behavior. This is bot behavior with extra steps.",
        "Behavioral profile of {name}: {messages} messages over {days} days. {botPercent} percent bot command ratio. {songs} song requests. Diagnosis: terminally online with a side of sus.",
        "Subject {name} exhibits classic sus patterns. High bot interaction at {botPercent} percent. {songs} song requests indicating attempt to blend in. {days} days of observation confirm: extremely sus.",
        "Psych evaluation of {name}. They've sent {commands} commands out of {messages} messages. That's {botPercent} percent bot talk. The clinical term for this is 'down bad for robots.' Sus score: {susScore}.",
        "{name}'s behavior over {days} days suggests they think this channel is their personal command line. {commands} commands. {botPercent} percent bot interaction. They don't chat. They execute.",
        "Analysis complete. {name}: {messages} messages. {botPercent} percent commands. {songs} songs. They treat this chat like a REST API. Send command, get response, repeat. Humans don't do this. Bots do. Sus score: {susScore}.",
        "In {days} days of monitoring, {name} has demonstrated a {botPercent} percent preference for talking to bots over humans. {songs} songs requested. They're not here for the stream. They're here for the bots.",
        "Clinical assessment of {name}. Symptoms: {commands} commands, {botPercent} percent bot interaction, {songs} songs requested in {days} days. Prognosis: peak suspicious activity. Prescribing immediate investigation.",
        "{name}'s chat fingerprint is unusual. {messages} messages total. {botPercent} percent are commands. {songs} song requests. Most chatters talk to people. {name} talks to machines. Concerning. {susScore} out of 100.",
        "Profiler's notes on {name}. {days} days active. {messages} messages logged. Bot command ratio: {botPercent} percent. Song request count: {songs}. This individual requires further observation. Sus confidence: {susScore} percent.",
    };

    // Verdict templates (5)
    private static readonly string[] _verdictClearTemplates =
    {
        "VERDICT: {name} has been investigated. Sus score: {susScore} out of 100. They're clean. Suspiciously clean. Which is... kind of sus in itself. But I'll allow it.",
        "FINAL RULING: {name}. {messages} messages. {botPercent} percent bot interaction. Sus score: {susScore}. Not guilty. But I'm keeping the file open.",
        "CASE CLOSED: {name} scored {susScore} on the sus meter. Below the threshold. They walk free today. But Big Bird is watching. Big Bird is always watching.",
    };

    private static readonly string[] _verdictSusTemplates =
    {
        "VERDICT: {name} has been found GUILTY of being sus. Score: {susScore} out of 100. {botPercent} percent bot commands. {songs} song requests. The evidence is overwhelming. Court dismissed.",
        "FINAL JUDGMENT: {name} is officially sus. {susScore} out of 100. {messages} messages and {botPercent} percent of them were commands. Even Big Bird couldn't pardon this one.",
    };

    private static readonly string[] _unknownUserTemplates =
    {
        "Subject does not exist in our records, which is the most suspicious thing of all. Where did they come from? Where did they go? Where did they come from, Cotton-Eye {name}?",
        "I searched every database. {name} is a ghost. No records. No footprint. No evidence they were ever here. That's not innocent. That's professional-grade sus.",
        "{name}? Never heard of them. Zero entries in the system. They've achieved the impossible: being sus by pure absence. That takes talent.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string targetName;
        bool selfSus = false;

        if (ctx.Arguments.Length == 0)
        {
            targetName = ctx.Message.DisplayName;
            selfSus = true;
        }
        else
        {
            targetName = ctx.Arguments[0].Replace("@", "").Trim();
        }

        User targetUser = await ctx.DatabaseContext.Users
            .AsNoTracking()
            .Where(u => u.Username == targetName.ToLower())
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (targetUser != null)
            targetName = targetUser.DisplayName;

        string text;

        if (targetUser == null)
        {
            text = _unknownUserTemplates[Random.Shared.Next(_unknownUserTemplates.Length)]
                .Replace("{name}", targetName);
        }
        else
        {
            int totalMsgs = await ctx.DatabaseContext.ChatMessages.AsNoTracking()
                .Where(m => m.UserId == targetUser.Id && !m.DeletedAt.HasValue)
                .CountAsync(ctx.CancellationToken);

            int cmdCount = await ctx.DatabaseContext.ChatMessages.AsNoTracking()
                .Where(m => m.UserId == targetUser.Id && !m.DeletedAt.HasValue && m.IsCommand)
                .CountAsync(ctx.CancellationToken);

            int botTalkPercent = totalMsgs > 0 ? (cmdCount * 100 / totalMsgs) : 0;

            int songCount = await ctx.DatabaseContext.Records.AsNoTracking()
                .Where(r => r.UserId == targetUser.Id && r.RecordType == "Spotify")
                .CountAsync(ctx.CancellationToken);

            DateTime? firstSeen = await ctx.DatabaseContext.ChatMessages.AsNoTracking()
                .Where(m => m.UserId == targetUser.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => (DateTime?)m.CreatedAt)
                .FirstOrDefaultAsync(ctx.CancellationToken);

            int days = firstSeen.HasValue ? Math.Max(1, (int)(DateTime.UtcNow - firstSeen.Value).TotalDays) : 0;

            int susScore = Math.Min(100, botTalkPercent + (songCount > 20 ? 20 : songCount) + (totalMsgs < 10 ? 30 : 0) + Random.Shared.Next(0, 20));

            // Pick a template category
            string[] pool;
            int roll = Random.Shared.Next(100);

            if (roll < 30)
                pool = _investigationTemplates;
            else if (roll < 50)
                pool = _amongUsTemplates;
            else if (roll < 70)
                pool = _conspiracyTemplates;
            else if (roll < 90)
                pool = _behavioralTemplates;
            else
                pool = susScore < 40 ? _verdictClearTemplates : _verdictSusTemplates;

            text = pool[Random.Shared.Next(pool.Length)]
                .Replace("{name}", targetName)
                .Replace("{messages}", totalMsgs.ToString())
                .Replace("{commands}", cmdCount.ToString())
                .Replace("{songs}", songCount.ToString())
                .Replace("{days}", days.ToString())
                .Replace("{botPercent}", botTalkPercent.ToString())
                .Replace("{susScore}", susScore.ToString());

            if (selfSus)
                text = text + " (You sus'd yourself. That's a power move.)";
        }

        string chatText = text;
        if (chatText.Length > 450)
            chatText = chatText[..447] + "...";

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(SUS_VOICE, processedText),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);

        if (audioBase64 != null)
        {
            IWidgetEventService wes = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await wes.PublishEventAsync("channel.chat.message.tts", new
            {
                text,
                user = new { id = ctx.Message.UserId },
                audioBase64,
                provider = "Edge",
                cost = 0m,
                characterCount = text.Length,
                cached = false,
            });
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username, chatText, ctx.Message.Id);
    }
}

return new SusCommand();
