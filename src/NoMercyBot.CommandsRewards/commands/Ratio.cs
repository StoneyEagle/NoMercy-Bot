
public class RatioCommand : IBotCommand
{
    public string Name => "ratio";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    // ===== SPORTS COMMENTARY (15) =====
    private static readonly string[] _sportsTemplates =
    {
        "The results are in! {winner} takes the W with {winMsgs} messages vs {loser}'s pathetic {loseMsgs}. Not even close.",
        "FINAL SCORE: {winner} {winMsgs}, {loser} {loseMsgs}. One of those is a champion. The other is {loser}.",
        "{winner} crosses the finish line with {winMsgs} messages while {loser} is still buffering at {loseMsgs}.",
        "And the crowd goes wild! {winner} with {winMsgs} absolutely demolishes {loser}'s {loseMsgs}. Mercy rule should apply.",
        "{winner} {winMsgs} vs {loser} {loseMsgs}. This isn't a competition, it's a public humiliation.",
        "Play of the game goes to {winner} with {winMsgs} messages. {loser} didn't even make the highlight reel at {loseMsgs}.",
        "{winner} wins the chat championship with {winMsgs} messages. {loser} came in last with {loseMsgs}. There were only two contestants.",
        "The ref should have stopped this fight. {winner} at {winMsgs} vs {loser} at {loseMsgs}. This is elder abuse.",
        "{winner} takes gold with {winMsgs} messages. {loser} gets a participation ribbon for {loseMsgs}. It's not even a medal.",
        "TOUCHDOWN {winner}! {winMsgs} messages to {loser}'s {loseMsgs}. The scoreboard is crying.",
        "Ladies and gentlemen, {winner} just ran laps around {loser}. {winMsgs} to {loseMsgs}. Call an ambulance. But not for {winner}.",
        "The commentators are speechless. {winner} at {winMsgs} just absolutely farmed {loser} at {loseMsgs}.",
        "{winner} dunks on {loser} from the free-throw line. {winMsgs} to {loseMsgs}. The rim is still shaking.",
        "Speed check: {winner} with {winMsgs} messages. Meanwhile {loser} is crawling at {loseMsgs}. Lag diff.",
        "And {winner} takes the series 3-0. {winMsgs} messages to {loseMsgs}. {loser} got swept harder than my chat logs.",
    };

    // ===== BOXING MATCH (10) =====
    private static readonly string[] _boxingTemplates =
    {
        "IN THE RED CORNER, {winner} with {winMsgs} messages! IN THE BLUE CORNER, {loser} with {loseMsgs}! The fight was over before it started.",
        "DING DING DING! {winner} lands {winMsgs} hits! {loser} barely threw {loseMsgs} punches before going down!",
        "Round 1 KO. {winner} with {winMsgs} messages vs {loser} with {loseMsgs}. The ref didn't even count to 10. Waste of time.",
        "{winner} enters the ring with {winMsgs}. {loser} shows up with {loseMsgs} and immediately throws in the towel.",
        "TKO in the first round! {winner} at {winMsgs} absolutely devastated {loser} at {loseMsgs}. Somebody call the medic.",
        "The bell rings and {winner} comes out swinging with {winMsgs}. {loser}'s {loseMsgs} didn't survive past the walkout.",
        "HEAVYWEIGHT CHAMPIONSHIP: {winner} ({winMsgs}) vs {loser} ({loseMsgs}). This was a mismatch and everyone knew it.",
        "{winner} threw {winMsgs} messages. {loser} threw {loseMsgs}. This wasn't a fight, it was an execution.",
        "Ladies and gentlemen, {loser} with {loseMsgs} just got absolutely rocked by {winner}'s {winMsgs}. Stay down.",
        "The corner threw in the towel for {loser} at {loseMsgs}. {winner} stands victorious with {winMsgs}. Undisputed.",
    };

    // ===== DEVELOPER COMPARISON (10) =====
    private static readonly string[] _devTemplates =
    {
        "{winner} has shipped {winMsgs} commits to chat. {loser} managed {loseMsgs}. One is a senior dev. The other is an unpaid intern.",
        "Code review complete. {winner}: {winMsgs} contributions, approved. {loser}: {loseMsgs} contributions, rejected. Please resubmit.",
        "{winner} has {winMsgs} messages in production. {loser} has {loseMsgs} still stuck in staging. Deployment failed.",
        "Sprint retro: {winner} closed {winMsgs} tickets. {loser} closed {loseMsgs}. One gets promoted. The other gets a PIP.",
        "{winner}: {winMsgs} merged PRs. {loser}: {loseMsgs} rejected drafts. The GitHub contribution graph tells the whole story.",
        "According to git blame, {winner} owns {winMsgs} lines vs {loser}'s {loseMsgs}. One is the tech lead. The other is the TODO comment.",
        "{winner} pushed {winMsgs} to main. {loser} pushed {loseMsgs}. StoneyEagle is considering revoking {loser}'s repo access.",
        "CI/CD pipeline results: {winner} passed with {winMsgs}. {loser} failed at {loseMsgs}. Build broken. Again.",
        "The standup report: {winner} delivered {winMsgs} units of chat. {loser} delivered {loseMsgs}. One is agile. The other is fragile.",
        "{winner} has a {winMsgs}-commit streak. {loser} has a {loseMsgs}-commit streak. One is on fire. The other is the fire. In production.",
    };

    // ===== BRUTAL (10) =====
    private static readonly string[] _brutalTemplates =
    {
        "{winner} with {winMsgs} messages just ratio'd {loser}'s {loseMsgs} into the shadow realm. No recovery.",
        "The gap between {winner}'s {winMsgs} and {loser}'s {loseMsgs} is wider than my error log on a bad day.",
        "{loser} really thought their {loseMsgs} messages could compete with {winner}'s {winMsgs}. Delusion is a powerful drug.",
        "{winner}: {winMsgs}. {loser}: {loseMsgs}. I've seen better competition between my CPU cores.",
        "Comparing {winner} at {winMsgs} to {loser} at {loseMsgs} is like comparing fiber to dial-up. Both exist. Only one matters.",
        "The Big Bird looked at {loser}'s {loseMsgs} messages, then looked at {winner}'s {winMsgs}, and chose a side. It wasn't {loser}'s.",
        "{winner} at {winMsgs} just made {loser}'s {loseMsgs} look like a rounding error. Statistically insignificant.",
        "If chat activity were a currency, {winner} would be rich at {winMsgs}. {loser} would be begging at {loseMsgs}.",
        "{loser} brought {loseMsgs} messages to a {winMsgs}-message fight. Outgunned. Outclassed. Out-chatted.",
        "{winner} ({winMsgs}) and {loser} ({loseMsgs}) walked into a bar. Only one walked out relevant.",
    };

    // ===== TIE-SPECIFIC (5) =====
    private static readonly string[] _tieTemplates =
    {
        "Plot twist! {winner} and {loser} are basically tied at {winMsgs} and {loseMsgs} messages. They're equally mid.",
        "{winner} at {winMsgs} and {loser} at {loseMsgs}. It's so close that nobody wins. Which means everyone loses.",
        "Dead heat between {winner} ({winMsgs}) and {loser} ({loseMsgs}). Two equally matched forces of mediocrity.",
        "{winner} has {winMsgs} and {loser} has {loseMsgs}. I can't even pick a winner here. You're both losers in my book.",
        "The ratio between {winner} ({winMsgs}) and {loser} ({loseMsgs}) is basically 1:1. Congratulations, you're both average.",
    };

    private static readonly string[] _notFoundResponses =
    {
        "I can't find that user in my database. Hard to ratio a ghost.",
        "User not found. They're so irrelevant even my database doesn't know them.",
        "404: User not found. Can't compare what doesn't exist in my records.",
    };

    private static readonly string[] _sameUserResponses =
    {
        "You want me to ratio someone against themselves? That's not confidence, that's a cry for help.",
        "Comparing a user to themselves? The only person who loses here is you for asking.",
        "Same user twice? The real ratio is the brain cells you lost typing this command.",
        "You just ratio'd yourself by entering the same name twice. Well done.",
        "This is like arm-wrestling yourself. You always win. You always lose. It's just sad.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length < 2)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} I need two targets to compare. Usage: !ratio @user1 @user2",
                ctx.Message.Id
            );
            return;
        }

        string name1 = ctx.Arguments[0].Replace("@", "").Trim().ToLower();
        string name2 = ctx.Arguments[1].Replace("@", "").Trim().ToLower();

        // Same user check
        if (name1 == name2)
        {
            string sameResp =
                _sameUserResponses[Random.Shared.Next(_sameUserResponses.Length)];
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {sameResp}",
                ctx.Message.Id
            );
            return;
        }

        User user1 = await ctx.DatabaseContext
            .Users.AsNoTracking()
            .Where(u => u.Username == name1)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        User user2 = await ctx.DatabaseContext
            .Users.AsNoTracking()
            .Where(u => u.Username == name2)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (user1 == null || user2 == null)
        {
            string who = user1 == null ? name1 : name2;
            string noData =
                _notFoundResponses[Random.Shared.Next(_notFoundResponses.Length)];
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {who}? {noData}",
                ctx.Message.Id
            );
            return;
        }

        int msgs1 = await ctx.DatabaseContext
            .ChatMessages.AsNoTracking()
            .Where(m => m.UserId == user1.Id && !m.DeletedAt.HasValue)
            .CountAsync(ctx.CancellationToken);

        int msgs2 = await ctx.DatabaseContext
            .ChatMessages.AsNoTracking()
            .Where(m => m.UserId == user2.Id && !m.DeletedAt.HasValue)
            .CountAsync(ctx.CancellationToken);

        int songs1 = await ctx.DatabaseContext
            .Records.AsNoTracking()
            .Where(r => r.UserId == user1.Id && r.RecordType == "Spotify")
            .CountAsync(ctx.CancellationToken);

        int songs2 = await ctx.DatabaseContext
            .Records.AsNoTracking()
            .Where(r => r.UserId == user2.Id && r.RecordType == "Spotify")
            .CountAsync(ctx.CancellationToken);

        // Determine winner by message count
        string winnerName;
        string loserName;
        int winMsgs;
        int loseMsgs;
        int winSongs;
        int loseSongs;

        if (msgs1 >= msgs2)
        {
            winnerName = user1.DisplayName;
            loserName = user2.DisplayName;
            winMsgs = msgs1;
            loseMsgs = msgs2;
            winSongs = songs1;
            loseSongs = songs2;
        }
        else
        {
            winnerName = user2.DisplayName;
            loserName = user1.DisplayName;
            winMsgs = msgs2;
            loseMsgs = msgs1;
            winSongs = songs2;
            loseSongs = songs1;
        }

        // Pick template pool
        string template;
        bool isTie =
            winMsgs == loseMsgs
            || (winMsgs > 0 && (double)loseMsgs / winMsgs > 0.9);

        if (isTie)
        {
            template = _tieTemplates[Random.Shared.Next(_tieTemplates.Length)];
        }
        else
        {
            string[][] pools = new[]
            {
                _sportsTemplates,
                _boxingTemplates,
                _devTemplates,
                _brutalTemplates,
            };
            string[] chosenPool = pools[Random.Shared.Next(pools.Length)];
            template = chosenPool[Random.Shared.Next(chosenPool.Length)];
        }

        string text = template
            .Replace("{winner}", winnerName)
            .Replace("{loser}", loserName)
            .Replace("{winMsgs}", winMsgs.ToString())
            .Replace("{loseMsgs}", loseMsgs.ToString())
            .Replace("{winSongs}", winSongs.ToString())
            .Replace("{loseSongs}", loseSongs.ToString());

        string processedText =
            await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

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
            IWidgetEventService wes =
                ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await wes.PublishEventAsync(
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

return new RatioCommand();
