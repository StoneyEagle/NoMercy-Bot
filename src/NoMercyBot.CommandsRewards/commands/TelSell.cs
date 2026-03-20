using NoMercyBot.Services.Other;

public class TelSellCommand : IBotCommand
{
    public string Name => "telsell";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";
    // Enthusiastic female voice for the infomercial pitch
    private const string PITCH_VOICE = "en-US-JennyNeural";

    private static readonly string[] _infomercials =
    {
        "ARE YOU TIRED of {name}'s mediocre chat messages? WELL NOW, for just {price} channel points, you can experience their PREMIUM content! BUT WAIT, THERE'S MORE! Order in the next 30 seconds and we'll throw in a FREE lurk! That's TWO lurks for the price of one! Call now!",
        "INTRODUCING the all new {name} 3000! Now with {percent} percent more opinions and absolutely ZERO filter! Are your streams missing that special something? {name} fills the void you never knew you had! Order now and receive a COMPLIMENTARY hug from the HugFactory! Operators are standing by!",
        "DO YOU STRUGGLE with empty chat? Does your stream feel LIFELESS? Well look no further because {name} is HERE! For just {price} easy payments of one follow, you get unlimited access to their hot takes! AND if you order RIGHT NOW, we'll include their unfiltered thoughts ABSOLUTELY FREE! That's a {price} dollar value!",
        "HAS THIS EVER HAPPENED TO YOU? You're streaming, having a great time, and suddenly NO ONE is talking? Well say GOODBYE to awkward silence because {name} NEVER stops typing! {name} is the solution to problems you didn't even know you had! Available in chat NOW for the low low price of FREE!",
        "ATTENTION VIEWERS! Do NOT change the channel! What I'm about to tell you will CHANGE YOUR LIFE! {name} has been lurking in this chat for YEARS developing the ULTIMATE chatting technique! And TODAY, for the first time EVER, they're sharing it with YOU! Side effects may include confusion and excessive use of emotes!",
        "BREAKING NEWS from the NoMercy Shopping Network! We have a LIMITED TIME offer on {name}'s legendary chat presence! Normally valued at {price} dollars, TODAY ONLY you can experience {name} for absolutely NOTHING! But WAIT, we'll also throw in their questionable opinions at NO EXTRA CHARGE! This deal is SO good it should be ILLEGAL!",
        "STOP EVERYTHING! Have you ever wished your chat had MORE chaos? MORE drama? MORE completely unnecessary takes? Then YOU need {name}! Now available in extra large! {name} comes with a lifetime warranty against being boring! Warning: {name} may cause spontaneous laughter, confusion, or both! NOT SOLD IN STORES!",
        "Ladies and gentlemen, feast your eyes on the REVOLUTIONARY {name}! Unlike cheap imitations, {name} is made from {percent} percent GENUINE chatter! Each {name} comes fully loaded with hot takes, bad puns, and an alarming amount of free time! But don't take MY word for it! Just look at these REAL testimonials! Quote: I didn't ask for this. End quote. FIVE STARS!",
        "GOOD NEWS EVERYONE! The Big Bird himself has PERSONALLY endorsed {name} as the OFFICIAL chatter of this stream! That's right, {name} has been certified by the Stoney Eagle Quality Assurance Department! Each message is inspected for maximum entertainment value! Results may vary! Batteries not included! Some assembly required!",
        "IT SLICES! IT DICES! IT types messages at THREE words per minute! It's {name}! The ONLY chatter that comes with a {count} stream watch streak guarantee! If you're not COMPLETELY satisfied with {name}'s chat contributions, simply disconnect and reconnect! Problem solved! Call now and we'll throw in a FREE mock command!",
        "YOU WON'T BELIEVE THIS DEAL! For the price of ZERO subscriptions, you get access to {name}'s ENTIRE catalogue of chat messages! That includes the good ones, the bad ones, and the ones that make you question everything! PLUS, act now and receive {name}'s EXCLUSIVE emoji spam technique! A {price} dollar value, YOURS FREE!",
        "FROM THE MAKERS OF NoMercyBot comes the NEXT GENERATION of chat entertainment! {name}! Now featuring {percent} percent more sass, DOUBLE the emotes, and absolutely NO quality control! {name} is the upgrade your chat DIDN'T ASK FOR but DESPERATELY NEEDS! Available wherever WiFi is found! CALL NOW!",
        "WAIT! Don't touch that remote! Are your lurks BORING? Are your messages FORGETTABLE? Then you need the {name} LURK UPGRADE PACKAGE! For just {price} channel points you get {name}'s PATENTED lurking technique that has been honed over {count} streams! The Big Bird PERSONALLY guarantees each lurk is top tier! NO REFUNDS!",
        "WE INTERRUPT THIS STREAM for a SPECIAL BULLETIN! {name} is NOW AVAILABLE for adoption! This premium chatter comes HOUSE TRAINED, FULLY LOADED with {percent} percent genuine Twitch knowledge, and a START BUTTON that never stops! Endorsed by the HugFactory! Approved by NoMercyBot! Feared by moderators! ACT FAST because {name} is a LIMITED EDITION!",
        "HAVE WE GOT A DEAL FOR YOU! Introducing {name}'s STREAM SURVIVAL KIT! Inside you'll find {count} pre-written chat messages, a tutorial on how to blame lag, and a COUPON for one free complaint about the bot being rigged! All this for {price} dollars! But if you call NOW we'll throw in {name}'s SECRET EMOTE TIER LIST! The Big Bird doesn't want you to see this!",
        "TIRED of watching streams ALONE? Introducing {name} as a SERVICE! That's right, N-A-A-S! For just {price} channel points, {name} will lurk in YOUR channel too! Features include: random messages, unsolicited opinions, and a {percent} percent chance they'll accidentally type in the wrong chat! ORDER NOW and get a free HugFactory membership!",
        "CALLING ALL VIEWERS! The NoMercy Home Shopping Network is PROUD to present: {name} DELUXE EDITION! Comes with {count} bonus emotes, a SIGNED certificate of chatterhood, and a LIFETIME supply of bad takes! Originally {price} dollars but TODAY we're GIVING it away because frankly we can't stop {name} from showing up anyway! OPERATORS ARE BEGGING YOU!",
        "JUST WHEN YOU THOUGHT CHAT couldn't get ANY BETTER, we present {name} TURBO MODE! Activated by the feather gods themselves! {name} TURBO types {percent} percent faster, lurks {percent} percent harder, and hugs {count} times per stream! Big Bird looked at {name} and said quote THIS is peak performance end quote! Get yours TODAY for {price} easy payments of NOTHING!",
        "TEST-I-MOAN-IALS are IN folks and they are ELECTRIC! Quote: {name} changed my life. I used to have fun but now I have MORE fun. End quote. That's the power of {name} PLATINUM! Now with CLOUD BASED chatting! Your messages are stored in the NoMercy data center which is definitely a real building and not just Stoney's closet! ORDER NOW for {price} dollars!",
        "BOGO ALERT! BOGO ALERT! Buy ONE {name} get ONE {name} ABSOLUTELY FREE! That's TWO {name}s for the price of ONE! Where else are you gonna find a deal like THAT? Each {name} comes with {count} pre-loaded opinions and {percent} percent more chaos than the leading brand! The HugFactory can barely keep up! THIS DEAL EXPIRES WHEN THE STREAM ENDS!",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string targetName = ctx.Arguments.Length == 0
            ? ctx.Message.DisplayName
            : ctx.Arguments[0].Replace("@", "").Trim();

        // Check if target exists
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
        int percent = Random.Shared.Next(2, 10) * 100;
        int count = Random.Shared.Next(3, 25);

        string script = _infomercials[Random.Shared.Next(_infomercials.Length)]
            .Replace("{name}", targetName)
            .Replace("{price}", price.ToString())
            .Replace("{percent}", percent.ToString())
            .Replace("{count}", count.ToString());

        string chatText = script;
        if (chatText.Length > 450)
            chatText = chatText[..447] + "...";

        string processedScript = await ctx.TtsService.ApplyUsernamePronunciationsAsync(script);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(PITCH_VOICE, processedScript),
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

return new TelSellCommand();
