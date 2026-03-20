
public class QuoteCommand : IBotCommand
{
    public string Name => "quote";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _quoteTemplates =
    {
        // Historical / philosophical (10)
        "On this day, the great philosopher {name} proclaimed: \"{quote}\". Truly ahead of their time.",
        "In the year of our stream, {name} bestowed upon us these immortal words: \"{quote}\". Scholars are still debating the meaning.",
        "As the ancient proverb attributed to {name} goes: \"{quote}\". Confucius could never.",
        "From the collected works of {name}, volume 37, chapter 12: \"{quote}\". A masterpiece of modern literature.",
        "The philosopher {name}, while contemplating existence in Twitch chat, once wrote: \"{quote}\". Plato is shaking.",
        "Engraved on the walls of the great library of StoneyEagle's channel, {name} wrote: \"{quote}\". Historians are baffled.",
        "And on the seventh day, {name} spoke unto chat: \"{quote}\". And it was... something.",
        "The Dead Sea Scrolls have been updated. Turns out {name} authored the passage: \"{quote}\". Biblical scholars in shambles.",
        "From {name}'s doctoral thesis on Twitch culture: \"{quote}\". They did not receive the degree.",
        "The Oracle at Delphi was asked about {name}. The prophecy returned: \"{quote}\". Nobody knows what it means.",

        // Breaking news (10)
        "BREAKING: {name} was caught on record saying: \"{quote}\". More at eleven.",
        "LIVE from the newsroom: {name} just dropped this bombshell: \"{quote}\". Chat is in shambles.",
        "This just in: leaked DMs reveal {name} once typed: \"{quote}\". PR team has been notified.",
        "ALERT: {name}'s chat logs have been subpoenaed. Exhibit one reads: \"{quote}\". No further comment.",
        "EXCLUSIVE REPORT: {name} said \"{quote}\" and thought nobody was watching. We were watching.",
        "HEADLINE: {name} trending worldwide after saying: \"{quote}\". Twitter is having a field day.",
        "URGENT: Sources confirm {name} typed \"{quote}\" with their whole chest. Retraction not expected.",
        "DEVELOPING STORY: {name} went on record with: \"{quote}\". Legal team is reviewing.",
        "BREAKING NEWS TICKER: {name} quote \"{quote}\" end quote. We'll keep you updated as this story unfolds.",
        "NEWS FLASH: The Big Bird's investigative team has uncovered that {name} once said: \"{quote}\". Shocking.",

        // Court / legal evidence (10)
        "Exhibit A, your honor. {name} said: \"{quote}\". The defense rests.",
        "Ladies and gentlemen of the jury, I present to you {name}'s own words: \"{quote}\". I rest my case.",
        "Your honor, the prosecution would like to submit into evidence the following statement from {name}: \"{quote}\". Objection overruled.",
        "Court transcript, page 47. The defendant {name} stated under oath: \"{quote}\". The courtroom gasped.",
        "The forensic chat analysts have confirmed that {name} typed: \"{quote}\". The fingerprints match.",
        "Deposition of {name}, recorded and notarized: \"{quote}\". Their lawyer has advised them to plead the fifth going forward.",
        "The judge looked at {name} and read their own words back to them: \"{quote}\". Sentencing is next week.",
        "Evidence bag number 42 contains the following message from {name}: \"{quote}\". CSI chat division is on the case.",
        "The witness {name} was asked to explain their statement: \"{quote}\". They could not. Case closed.",
        "Internal affairs has flagged {name}'s message: \"{quote}\". An investigation is underway.",

        // Out of context / dramatic (10)
        "Without context, {name} once typed: \"{quote}\". We have questions.",
        "Taken completely out of context, {name} said: \"{quote}\". And honestly it's funnier this way.",
        "{name} thought this message would be forgotten: \"{quote}\". The database remembers everything.",
        "Deep in the archives, buried under thousands of messages, {name} once whispered: \"{quote}\". It was not buried deep enough.",
        "A dramatic reading from {name}'s chat history: \"{quote}\". There wasn't a dry eye in the house. From laughter.",
        "The year was uncertain. The vibes were off. And {name} felt compelled to type: \"{quote}\". Nobody asked for it.",
        "In a moment of pure unfiltered energy, {name} blessed this chat with: \"{quote}\". We are still processing.",
        "Let the record show that {name}, of their own free will, typed: \"{quote}\". No one was holding a gun to their keyboard.",
        "Recovered from {name}'s deleted messages folder... just kidding, it was public: \"{quote}\". Imagine if they had a filter.",
        "{name} said \"{quote}\" and then acted like it was normal. It was not normal.",

        // Celebrity / creative attribution (10)
        "As {name} once wisely said: \"{quote}\". Truly the Shakespeare of our time.",
        "{name}, winner of the Pulitzer Prize for Twitch Chat Literature, is quoted as saying: \"{quote}\". The committee had no other nominees.",
        "In their TED Talk titled 'Messages That Changed Nothing,' {name} opened with: \"{quote}\". Standing ovation.",
        "Grammy-nominated lyricist {name} penned the following: \"{quote}\". The album drops never.",
        "{name}'s acceptance speech at the Chat Message Awards: \"{quote}\". Not a dry eye in the house.",
        "The Big Bird once asked {name} for wisdom. They responded: \"{quote}\". The eagle has not asked again.",
        "Nobel laureate {name}'s most cited work reads: \"{quote}\". Peer review is pending. Indefinitely.",
        "From {name}'s bestselling autobiography 'I Typed And Hit Enter': \"{quote}\". Currently out of print.",
        "Motivational speaker {name} closed their keynote with: \"{quote}\". The audience left confused but inspired.",
        "{name}'s Wikipedia page has been updated with their most notable contribution to society: \"{quote}\". The edit was reverted.",
    };

    private static readonly string[] _noQuoteResponses =
    {
        "I searched {name}'s entire chat history and found nothing worth quoting. They are the lorem ipsum of this channel.",
        "{name} has never said anything quotable. Their chat contributions are like comments in production code: nonexistent.",
        "I dug through {name}'s messages looking for something memorable. I found nothing. Absolutely nothing.",
        "The archives contain zero quotable material from {name}. It's like their keyboard only has a lurk button.",
        "{name}'s quotation page is blank. Even a random string generator produces more meaningful content.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Quote who? Give me a target. Usage: !quote @username",
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
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Who is {targetName}? My database has never heard of them. Can't quote a phantom.",
                ctx.Message.Id
            );
            return;
        }

        int count = await ctx.DatabaseContext.ChatMessages
            .AsNoTracking()
            .Where(m =>
                m.UserId == targetUser.Id
                && !m.IsCommand
                && !m.DeletedAt.HasValue
                && m.Message != null
                && m.Message.Length > 5
                && !m.Message.StartsWith("http://")
                && !m.Message.StartsWith("https://")
            )
            .CountAsync(ctx.CancellationToken);

        if (count == 0)
        {
            string noQuote = _noQuoteResponses[Random.Shared.Next(_noQuoteResponses.Length)]
                .Replace("{name}", targetUser.DisplayName);
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {noQuote}",
                ctx.Message.Id
            );
            return;
        }

        int skip = Random.Shared.Next(count);
        string quote = await ctx.DatabaseContext.ChatMessages
            .AsNoTracking()
            .Where(m =>
                m.UserId == targetUser.Id
                && !m.IsCommand
                && !m.DeletedAt.HasValue
                && m.Message != null
                && m.Message.Length > 5
                && !m.Message.StartsWith("http://")
                && !m.Message.StartsWith("https://")
            )
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Select(m => m.Message)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (string.IsNullOrWhiteSpace(quote))
        {
            string noQuote = _noQuoteResponses[Random.Shared.Next(_noQuoteResponses.Length)]
                .Replace("{name}", targetUser.DisplayName);
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {noQuote}",
                ctx.Message.Id
            );
            return;
        }

        if (quote.Length > 200)
            quote = quote[..197] + "...";

        string template = _quoteTemplates[Random.Shared.Next(_quoteTemplates.Length)];
        string text = template
            .Replace("{name}", targetUser.DisplayName)
            .Replace("{quote}", quote);

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

return new QuoteCommand();
