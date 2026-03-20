
public class AuctionCommand : IBotCommand
{
    public string Name => "auction";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string AUCTIONEER_VOICE = "en-US-GuyNeural";

    private static readonly string[] _auctionTemplates =
    {
        "LADIES AND GENTLEMEN welcome to the NoMercy Auction House! Up on the block tonight, LOT NUMBER {lot}, we have {name}! Starting bid: {price} channel points! This fine specimen comes with {count} unsolicited opinions and a lurking habit that would make a CIA agent jealous! Do I hear {price}? Anyone? No? SOLD to absolutely nobody for zero channel points!",
        "ALRIGHT ALRIGHT ALRIGHT we got a LIVE one here folks! {name} is UP FOR AUCTION! Starting at {price} bits! Features include: questionable taste in streams, a keyboard that never sleeps, and {count} messages that went completely ignored! Do I hear {price}? {price} anyone? Going once, going twice, RETURNED TO SENDER! No buyers today!",
        "STEP RIGHT UP! Lot number {lot}: one slightly used {name}! Comes pre-loaded with {count} hot takes and a subscription to this channel that they DEFINITELY meant to cancel! Starting bid is {price} channel points! Can I get {price}? How about half that? A quarter? A single emote? SOLD to the void for the low low price of NOTHING!",
        "WE'VE GOT A RARE FIND HERE FOLKS! {name}! Vintage chatter, approximately {count} streams old! Some wear and tear from excessive emote usage! Starting the bidding at {price} bits! Do we have any takers? ANY takers? The Big Bird himself wouldn't pay {price} for this! UNSOLD! Back to the lurk warehouse you go, {name}!",
        "WELCOME WELCOME to tonight's PREMIUM auction! Our featured item: {name}! Known for showing up, saying absolutely nothing for {count} minutes, then dropping one message and vanishing! A true collector's item! Bidding starts at {price} channel points! Anyone? Bueller? SOLD for exactly zero! Which says a lot!",
        "AND NOW the moment you've all been waiting for! Lot {lot}: {name} THE MAGNIFICENT! Endorsed by nobody! Recommended by no one! Comes with a {count} day lurk streak and an opinion on everything! Starting bid: {price} channel points! I'm hearing crickets! Going once! Going twice! DONATED TO CHARITY because no one would pay!",
        "FEAST YOUR EYES on this EXQUISITE piece of Twitch history! {name}! {count} messages sent, zero remembered! Like typing into the void but with more enthusiasm! We're starting at {price} bits! Do I see a paddle? A hand? A PULSE? SOLD to the dumpster out back for exactly what they're worth! Which is hugs! From the HugFactory! Which are free!",
        "OPEN YOUR WALLETS folks because {name} is ON THE BLOCK! This premium chatter comes with {count} messages that nobody remembers! Starting at {price} channel points! The auctioneer is BEGGING someone to bid! ANYONE! The janitor? The bot? SOLD to {name}'s own alt account for a handful of channel points and a lukewarm hug!",
        "WHAT A NIGHT to be alive! We present LOT {lot}: {name}! This absolute UNIT of a viewer has been spotted in {count} different streams this week alone! Loyalty? Never heard of it! Bidding opens at {price}! We've got... nothing! Not a single bid! {name} has been REPOSSESSED by the chat gods! Better luck next auction!",
        "CALLING ALL COLLECTORS! Tonight's auction features {name}! One careful owner! Low mileage! Only {count} timeout incidents on the odometer! Starting at a BARGAIN price of {price} bits! Anyone? The silence is DEAFENING! SOLD to the bargain bin alongside clippy and Internet Explorer! A fitting home!",
        "HEY HEY HEY it's auction time and BOY do we have a treat! {name}! Fresh from the lurk cave! Still has that new chatter smell! Comes with {count} saved clip links they'll never share! Opening bid: {price} channel points! I see no hands! I see no paddles! I see {name} slowly backing away! UNSOLD! Even the bots passed!",
        "NOW ENTERING the auction floor: {name}! Appraised value: {price} channel points! Actual value: DEBATABLE! This lot includes {count} months of chat history and a concerning amount of knowledge about the streamer's schedule! Bidding starts NOW! And it ends now too because NOBODY is interested! Tough crowd!",
        "GOING GOING GONE wait no it's not gone because nobody bid! {name} remains UNCLAIMED! We tried starting at {price}! We dropped to {count}! We dropped to ONE! Still nothing! The Big Bird is EMBARRASSED! The eagle WEEPS! {name} you are officially the first chatter to be RETURNED to the factory!",
        "HEAR YE HEAR YE! The NoMercy Auction House proudly presents {name}! A specimen of UNKNOWN value! Rumored to have {count} alt accounts! Starting bid: {price} bits! The crowd goes MILD! Not wild, MILD! And the final bid is from... nobody! {name} has been listed on eBay under miscellaneous slash other!",
        "ROLL OUT THE RED CARPET because {name} is being AUCTIONED to the highest bidder! Except there ARE no bidders! This chatter has {count} achievements in being average! Was {price} too high? Was {count} too high? Was ONE too high? Apparently YES to all three! {name} is now available at the lost and found!",
        "ATTENTION BIDDERS! Lot {lot} is a ONCE IN A LIFETIME opportunity! {name}! They type! They lurk! They occasionally use emotes! {count} hours of stream watching experience! Starting at {price} channel points! The audience is RIVETED! Riveted to their seats because they REFUSE to raise a paddle! SOLD to the shadow realm for a bag of stale popcorn!",
        "THE GAVEL IS RAISED and on the block we have {name}! Certified pre-owned chatter! {count} messages with full service history! Minor cosmetic damage from being roasted by the bot! Starting bid: {price}! Do I hear {price}? Do I hear ANYTHING? The sound of silence is my answer! {name} has been RECYCLED!",
        "GATHER ROUND for the MOST anticipated auction of the evening! {name}! Comes as-is! No warranty! No refunds! {count} typos included at no extra charge! We're opening at {price} bits! The tension is palpable! Which is to say there IS no tension because nobody cares! UNSOLD! {name} will be placed in long term storage!",
        "AND HERE WE GO! The crown jewel of tonight's auction! LOT {lot}: {name}! This distinguished chatter brings {count} streams worth of experience and absolutely NOTHING else! Bidding begins at {price}! The room falls silent! Not dramatically silent, just REGULAR silent because nobody wants to buy! SOLD to the clearance section!",
        "LAST CALL for lot {lot}! {name} MUST GO! Everything must go! {count} chat messages! A profile picture they haven't changed since 2019! And an attention span of approximately four minutes! Original price: {price}! Markdown price: ZERO! Final sale price: we'll PAY you to take them! Still no takers! {name} has officially been discontinued!",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string targetName = ctx.Arguments.Length == 0
            ? ctx.Message.DisplayName
            : ctx.Arguments[0].Replace("@", "").Trim();

        if (ctx.Arguments.Length > 0)
        {
            User targetUser = await ctx.DatabaseContext.Users
                .AsNoTracking()
                .Where(u => u.Username == targetName.ToLower())
                .FirstOrDefaultAsync(ctx.CancellationToken);

            if (targetUser != null)
                targetName = targetUser.DisplayName;
        }

        int price = Random.Shared.Next(5, 100) * 10;
        int count = Random.Shared.Next(3, 50);
        int lot = Random.Shared.Next(100, 999);

        string script = _auctionTemplates[Random.Shared.Next(_auctionTemplates.Length)]
            .Replace("{name}", targetName)
            .Replace("{price}", price.ToString())
            .Replace("{count}", count.ToString())
            .Replace("{lot}", lot.ToString());

        string chatText = script;
        if (chatText.Length > 450)
            chatText = chatText[..447] + "...";

        string processedScript = await ctx.TtsService.ApplyUsernamePronunciationsAsync(script);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(AUCTIONEER_VOICE, processedScript),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 != null)
        {
            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
            {
                text = script,
                user = new { id = ctx.Message.UserId },
                audioBase64,
                provider = "Edge",
                cost = 0m,
                characterCount = script.Length,
                cached = false,
            });
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username, chatText, ctx.Message.Id);
    }
}

return new AuctionCommand();
