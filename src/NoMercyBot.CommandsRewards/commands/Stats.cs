
public class StatsCommand : IBotCommand
{
    public string Name => "stats";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _statsTemplates =
    {
        // FBI dossier style (10)
        "CLASSIFIED DOSSIER: Subject {name}. Messages: {messages}. Commands issued: {commands}. Songs requested: {songs}. Days on file: {days}. Threat level: annoying.",
        "FBI FILE dash {name}. Total transmissions: {messages}. Bot interactions: {commands}. Spotify requests: {songs}. Active for {days} days. Status: under surveillance. Chat activity: {chatPercent} percent original thought.",
        "CONFIDENTIAL. Eyes only. Subject {name} has been operating in this channel for {days} days. They have transmitted {messages} messages, {chatPercent} percent of which were actual conversation. {songs} songs requested. Assessment: needs monitoring.",
        "INTELLIGENCE BRIEFING: Asset {name}. {messages} communications intercepted over {days} days. {commands} were bot commands. {songs} music requests logged. Conclusion: more bot than human.",
        "CASE FILE SEVEN SEVEN THREE. {name}. Surveillance period: {days} days. Messages logged: {messages}. Automated commands: {commands}. Song requests: {songs}. The Big Bird has flagged this individual for further review.",
        "TOP SECRET. Subject codename: {name}. Chat footprint: {messages} messages across {days} days. Bot dependency ratio: {commands} commands. Musical interference: {songs} songs. Risk assessment: mid.",
        "DOSSIER UPDATE: {name} remains active. {messages} total messages. {commands} commands. {songs} songs queued. {days} days in the field. Recommended action: continued observation.",
        "CLASSIFIED. Agent report on {name}. This individual has generated {messages} data points over {days} days. Of those, {chatPercent} percent were human speech. The rest were commands. {songs} songs deployed. Suspicion level: maximum.",
        "REDACTED FILE: {name}. The agency has cataloged {messages} messages from this target. {commands} were directed at bots. {songs} were music requests. {days} days of activity. Cover status: blown.",
        "NATIONAL SECURITY ALERT: {name} has been active for {days} days. {messages} messages intercepted. {commands} commands executed. {songs} songs requested. Clearance level: none. Threat level: mildly irritating.",

        // Sports commentary (10)
        "{name}'s season stats are in. {messages} messages. {commands} commands. {songs} song requests over {days} days. The scouts are NOT impressed.",
        "And now the career stats for {name}. {messages} total messages with a {chatPercent} percent chat rate. {songs} songs requested. {days} days on the roster. Commentators are calling it a rebuilding year. Every year.",
        "Welcome to the {name} highlight reel. {messages} messages thrown. {commands} intercepted by bots. {songs} songs on the jukebox. Career span: {days} days. The crowd is... politely quiet.",
        "ESPN Bottom Line: {name} posts {messages} career messages. Commands: {commands}. Songs: {songs}. Days active: {days}. Analysts say they peaked in their first week.",
        "The {name} scouting report is in. {messages} messages across {days} games. {chatPercent} percent were actual plays. {songs} song requests audible. Draft stock: undrafted.",
        "Play by play: {name} has been on the field for {days} days. {messages} attempts. {commands} turnovers to bots. {songs} songs called from the bench. Win rate: questionable.",
        "Post-game interview with {name}. Reporter asks about their {messages} messages in {days} days. {commands} were bot commands. {songs} songs requested. {name} had no comment. Neither did anyone else.",
        "Fantasy Twitch update: {name} scored {messages} message points over {days} matchdays. {commands} penalty commands. {songs} bonus songs. If you drafted {name}, you lost your league.",
        "The referee has reviewed {name}'s stats. {messages} messages. {commands} fouls on the bot. {songs} songs from the DJ booth. {days} days of eligibility remaining: unfortunately, all of them.",
        "Coach's report: {name} showed up for {days} days. Threw {messages} passes into chat. {commands} were incomplete bot commands. Requested {songs} songs from the locker room. Benched indefinitely.",

        // Report card (10)
        "{name}'s stream report card: Attendance: {days} days. Participation: {messages} messages. Bot reliance: {commands} commands. Music taste: {songs} songs requested. Grade: D minus.",
        "REPORT CARD for {name}. Days enrolled: {days}. Total homework submitted: {messages} messages. Times asking the teacher for help: {commands}. Jukebox credits used: {songs}. Teacher's note: tries hard. Fails harder.",
        "Academic transcript for {name}. Semesters completed: {days} days worth. Papers published: {messages}. Citations of bot work: {commands}. Elective music courses: {songs}. GPA: zero point chat.",
        "Parent teacher conference for {name}. Your child has attended {days} days and contributed {messages} messages. {chatPercent} percent were original. {commands} were copied from the bot. {songs} songs played during class. We need to talk.",
        "{name}'s quarterly review. Messages: {messages}. Performance: {chatPercent} percent actual content. Commands: {commands}. Songs: {songs}. Days present: {days}. Promotion: denied.",
        "Progress report: {name}. Category: chat participation. Score: {messages} out of impressive. {commands} were just bot commands. {songs} songs requested. {days} days of attendance. Needs improvement in literally everything.",
        "End of term results for {name}. {messages} messages submitted. {commands} were plagiarized from the command list. {songs} extra credit songs. {days} days of perfect attendance. Still failing.",
        "Student evaluation: {name}. This student has been present for {days} days and has produced {messages} pieces of work. {chatPercent} percent original content. {songs} requests to play music during study hall. Recommendation: summer school.",
        "{name}'s performance review is ready. {days} days on the job. {messages} tasks completed. {commands} were delegated to automation. {songs} breaks taken at the jukebox. Rating: meets minimum expectations. Barely.",
        "Diploma status for {name}: PENDING. {messages} credits earned over {days} semesters. {commands} were auto-graded. {songs} music theory electives. Graduation ceremony: do not hold your breath.",

        // Tech / dev analysis (10)
        "Running diagnostics on {name}. CPU usage: {chatPercent} percent chat, rest commands. Uptime: {days} days. Packets sent: {messages}. Songs queued: {songs}. Status: needs reboot.",
        "System scan of user {name} complete. {messages} processes executed over {days} cycles. {commands} were system calls. {songs} audio threads spawned. Memory leak detected: they keep coming back.",
        "Stack trace for {name}. Depth: {days} days. {messages} function calls logged. {commands} were API requests to the bot. {songs} Spotify webhooks fired. Exit code: still running for some reason.",
        "Performance profile for {name}. Runtime: {days} days. Throughput: {messages} messages. Overhead: {commands} commands. I/O: {songs} songs. Optimization recommendation: have they tried turning themselves off and back on?",
        "Git log for {name}. Commits: {messages}. Merge conflicts with bots: {commands}. Songs pushed to playlist branch: {songs}. Days since first commit: {days}. Code review: rejected.",
        "Docker stats for container {name}. Running for {days} days. Logs: {messages} entries. Health checks: {commands}. Volume mounts to Spotify: {songs}. Image size: suspiciously large for what it does.",
        "Kubernetes pod report: {name}. Pod age: {days} days. Events: {messages}. Readiness probes: {commands}. ConfigMap songs: {songs}. Resource efficiency: {chatPercent} percent useful output. Should be scaled to zero.",
        "CI/CD pipeline report for {name}. Builds: {messages}. Failed tests: probably {commands}. Deployments to Spotify: {songs}. Pipeline duration: {days} days and counting. Status: permanently yellow.",
        "Terraform plan for {name}. Resources managed: {messages} messages over {days} days. Imports: {commands} bot commands. Outputs: {songs} songs. Plan: {messages} to add, 0 to change, everything to destroy.",
        "Monitoring dashboard for {name}. Uptime: {days} days. Request count: {messages}. Error rate: about {commands} bot calls. Audio alerts: {songs}. SLA: not met. Never met. Will never be met.",

        // Snarky summary (10)
        "{name} in a nutshell: {messages} messages, {commands} commands, {songs} songs, {days} days. And somehow, none of it was interesting.",
        "Let me summarize {name} for you. {days} days in this channel. {messages} messages sent. {chatPercent} percent were actual human words. {songs} songs requested. They exist. That's about it.",
        "The {name} experience: show up for {days} days, type {messages} things, ask the bot {commands} times for help, request {songs} songs, contribute nothing of lasting value. Rinse and repeat.",
        "If {name} were a resume: {days} days of experience. {messages} messages of output. {commands} automated tasks. {songs} creative contributions via Spotify. References: none willing to come forward.",
        "I compiled everything about {name} into a report. {messages} messages over {days} days. {chatPercent} percent original content. {commands} bot interactions. {songs} songs. The report was two pages. One was blank.",
        "{name} speedrun stats. Days: {days}. Messages: {messages}. Commands: {commands}. Songs: {songs}. Percent human: {chatPercent}. World record pace for being aggressively average.",
        "The legend of {name}. Chapter one: they arrived. Chapter two: {messages} messages. Chapter three: {commands} commands. Chapter four: {songs} songs. Epilogue: nobody noticed.",
        "Wikipedia article on {name}. This article is a stub. Known data: {messages} messages, {commands} commands, {songs} songs, {days} days. This article has been flagged for lack of notability.",
        "Yelp review of {name}: one star. Visited for {days} days. Left {messages} comments. {commands} were complaints to management. Played {songs} songs on the jukebox. Would not recommend.",
        "Amazon product listing: {name}. Customer reviews based on {days} days of use. Features: {messages} messages, {commands} commands, {songs} songs. Rating: {chatPercent} percent useful. Frequently bought with: disappointment.",
    };

    private static readonly string[] _noDataResponses =
    {
        "ERROR 404: {name} not found. The database has no records. They are literally file not found in human form.",
        "I queried every table for {name} and got null. They don't exist in my system. Are you sure they're real?",
        "The stats for {name} returned empty. Zero messages. Zero everything. They are the dev null of Twitch chat.",
        "I tried to pull {name}'s file but the filing cabinet was empty. Not even a lurk on record.",
        "Searching for {name} dot dot dot. No results. They have the digital footprint of a ghost in airplane mode.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Stats for who? Give me a target. Usage: !stats @username",
                ctx.Message.Id
            );
            return;
        }

        string targetName = ctx.Arguments[0].Replace("@", "").Trim().ToLower();

        User targetUser = await ctx.DatabaseContext
            .Users.AsNoTracking()
            .Where(u => u.Username == targetName)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (targetUser == null)
        {
            string noData = _noDataResponses[Random.Shared.Next(_noDataResponses.Length)]
                .Replace("{name}", targetName);
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {noData}",
                ctx.Message.Id
            );
            return;
        }

        int totalMessages = await ctx.DatabaseContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == targetUser.Id && !m.DeletedAt.HasValue)
            .CountAsync(ctx.CancellationToken);

        int commandCount = await ctx.DatabaseContext.ChatMessages
            .AsNoTracking()
            .Where(m =>
                m.UserId == targetUser.Id && !m.DeletedAt.HasValue && m.IsCommand
            )
            .CountAsync(ctx.CancellationToken);

        DateTime? firstSeen = await ctx.DatabaseContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == targetUser.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => (DateTime?)m.CreatedAt)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        int songRequests = await ctx.DatabaseContext.Records
            .AsNoTracking()
            .Where(r => r.UserId == targetUser.Id && r.RecordType == "Spotify")
            .CountAsync(ctx.CancellationToken);

        if (totalMessages == 0 && songRequests == 0)
        {
            string noData = _noDataResponses[Random.Shared.Next(_noDataResponses.Length)]
                .Replace("{name}", targetUser.DisplayName);
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {noData}",
                ctx.Message.Id
            );
            return;
        }

        int days = firstSeen.HasValue
            ? Math.Max(1, (int)(DateTime.UtcNow - firstSeen.Value).TotalDays)
            : 1;

        int chatMessages = totalMessages - commandCount;
        int chatPercent = totalMessages > 0
            ? (int)Math.Round((double)chatMessages / totalMessages * 100)
            : 0;

        string template = _statsTemplates[Random.Shared.Next(_statsTemplates.Length)];
        string text = template
            .Replace("{name}", targetUser.DisplayName)
            .Replace("{messages}", totalMessages.ToString())
            .Replace("{commands}", commandCount.ToString())
            .Replace("{songs}", songRequests.ToString())
            .Replace("{days}", days.ToString())
            .Replace("{chatPercent}", chatPercent.ToString());

        string chatText = text;
        if (chatText.Length > 450)
            chatText = chatText[..447] + "...";

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(
            text
        );

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
            chatText,
            ctx.Message.Id
        );
    }
}

return new StatsCommand();
