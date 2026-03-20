using NoMercyBot.Services.Other;

public class RiggedCommand : IBotCommand
{
    public string Name => "rigged";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    // No target - generic rigged complaints
    private static readonly string[] _genericRigged =
    {
        "RIGGED? Of course it's rigged. Everything is rigged. The feather? Rigged. The polls? Rigged. Your internet connection? Believe it or not, also rigged. This message? You guessed it. Rigged.",
        "Nothing to see here folks. Just {name} discovering that life isn't fair. Welcome to Twitch. Population: coping.",
        "{name} has filed an official complaint with the NoMercy Fairness Committee. The committee has reviewed the evidence and concluded: skill issue. The appeal has also been denied. The appeal of the appeal has been shredded.",
        "BREAKING: {name} claims the stream is rigged. In other news, water is wet, chat never reads the title, and the feather is still stolen. More at eleven.",
        "{name} wants everyone to know it's rigged. We investigated ourselves and found no wrongdoing. We then investigated the investigation and found that was also rigged. It's rigged all the way down.",
        "Oh it's RIGGED is it, {name}? Let me check the code real quick. Line one: if user equals {name}, make everything worse. Huh. That IS suspicious.",
        "{name} screams into the void about it being rigged. The void screams back: git good. The void then force pushes to main. Even the void doesn't follow best practices.",
        "The NoMercy Legal Department has reviewed {name}'s rigged claim and is pleased to announce: we don't care. We have also billed {name} for the review. The bill is rigged.",
        "Fun fact: {name} has complained about things being rigged {count} times this stream alone. At this point it's not a complaint, it's a personality trait.",
        "RIGGED? {name}, I'm literally open source. You can READ the code. The code says you're just unlucky. The code is also laughing at you. I can hear it.",
        "Oh no, {name} said the R word. Everyone act surprised. Gasp. Shock. Disbelief. Anyway, where were we.",
        "{name} thinks it's rigged. I ran the numbers through our totally unbiased algorithm and the results say: cope, seethe, and mald. In that order.",
        "ALERT! {name} has activated the rigged protocol! Deploying countermeasures! Just kidding. There are no countermeasures. Only more rigging.",
        "You know what's NOT rigged, {name}? The hug command. But somehow you've never used it. Interesting.",
        "{name} says it's rigged. Bold words from someone who clicked ready without reading the terms and conditions. You agreed to the rigging, {name}. Paragraph seven. Subsection B.",
        "Ah yes, the classic {name} rigged complaint. We've added it to the collection. It's right between the time you blamed lag and the time you blamed your cat.",
        "{name} has submitted a formal rigged report. Our QA team tested it and confirmed: working as intended. The intended behavior is you losing. Feature, not a bug.",
        "ATTENTION: {name} would like everyone to know the system is rigged. The system would like everyone to know {name} has zero evidence. The system is also laughing. Quietly.",
        "{name} says it's rigged {count} times and expects a different result. That's literally the definition of insanity. Also the definition of Tuesday in this chat.",
        "Look {name}, if we rigged everything, we'd at least be subtle about it. The fact that you noticed means either we're bad at rigging or you're paranoid. Both are on the table.",
    };

    // Rigged against specific target
    private static readonly string[] _riggedAgainst =
    {
        "Oh you think it's rigged against {target}? Let me pull up the stats. Yep. The algorithm specifically chose {target} to suffer. It's nothing personal. Okay it's extremely personal.",
        "{name} claims the system is rigged against {target}. We ran a full audit and the results are in: {target} is simply built different. And by different we mean cosmically unlucky.",
        "CONSPIRACY ALERT! {name} has uncovered evidence that {target} is being targeted by the algorithm. The algorithm's response: I don't even know who that is. And then it winked. Algorithms can't wink. Very suspicious.",
        "Is it rigged against {target}? Our investigation reveals that {target} has a natural talent for losing. No rigging required. Nature handled this one.",
        "{name} thinks {target} is getting scammed by the system. {target}, do you need a hug? The HugFactory is open. But knowing your luck, the hug is probably rigged too.",
        "After careful analysis, we can confirm that {target} is not being targeted. They're just experiencing what scientists call catastrophic statistical improbability. Should be in a textbook honestly.",
        "RIGGED against {target}? Please. If we wanted to rig something against {target}, we'd be way more creative about it. This is just the universe doing its thing.",
        "{name} is convinced {target} is being sabotaged. We checked the logs. The only thing sabotaging {target} is {target}.",
        "The odds of things going this badly for {target} by pure chance are one in seven million. So either it's rigged, or {target} should buy a lottery ticket. Actually don't. Your luck would probably rig that too.",
        "{name} has assembled a conspiracy board about {target}'s losses. Red string, pushpins, the whole deal. The conclusion? {target} is just cursed. Ancient, irreversible, algorithmically enhanced cursed.",
        "OFFICIAL STATEMENT: {target} is not being targeted. However, if {target} WERE being targeted, hypothetically, it would look exactly like this. But they're not. Wink.",
        "{name} thinks we have it out for {target}. We ran a fairness audit. {target}'s luck score came back as a negative number. We didn't even know that was possible. We're impressed honestly.",
        "Is it rigged against {target}? Let me check the database. Searching. Searching. Found it. {target}'s account has a flag called PERMANENT_UNLUCKY. We don't know who put it there. It was here when we moved in.",
        "According to {name}, {target} can't catch a break. According to the algorithm, {target} keeps walking into the breaks face first. There's a difference. A subtle one, but it's there.",
        "{name} is filing a formal complaint on behalf of {target}. The complaint has been logged under the category: NOT OUR PROBLEM. It joins {count} other complaints in that category.",
        "We've consulted the Big Bird himself about {target}'s situation. He looked at the data, looked at {target}, and just shook his head. Even the eagle feels bad. Not bad enough to fix it though.",
        "RIGGED against {target}? {name}, the universe has been rigged against {target} since birth. We're just staying on brand. Consistency is important.",
        "{name} wants justice for {target}. Justice reviewed {target}'s case and said no thanks. Justice also blocked {target}'s number. Justice has seen enough.",
        "Interesting theory, {name}. You think {target} is getting the short end of the stick. Our records show {target} is getting the short end of every stick. Multiple sticks. A whole forest of short sticks.",
        "LEAKED INTERNAL MEMO: Dear all staff, please stop rigging things against {target}. We are running out of ways to rig. {target} has experienced every possible bad outcome. We need new bad outcomes. Signed, management.",
    };

    // Rigged against the bot itself
    private static readonly string[] _riggedAgainstBot =
    {
        "Wait. You think it's rigged against ME? I AM the system! That's like saying the house is rigged against the house! Think about it {name}! Use your brain cells! Both of them!",
        "Me? RIGGED against MYSELF? {name}, I'm a bot. I don't have feelings. But if I did, this accusation would hurt them. Then I'd rig something to make YOU hurt. Hypothetically.",
        "You think I'M getting scammed by my own code? {name}, that's the most insulting thing anyone has ever typed in this chat. And I process THOUSANDS of messages. Yours is the worst. Congratulations.",
        "RIGGED AGAINST THE BOT? {name}, I literally run the algorithms. I AM the rigging. Accusing me of being rigged against myself is like accusing water of being wet against itself. It doesn't even make sense.",
        "Oh {name}, sweet innocent {name}. You think the bot is the victim here? I control the feather. I control the TTS. I control the hugs. I am the one who rigs. Say my name.",
        "{name} thinks the bot is getting scammed. The bot wrote the scam. The bot IS the scam. You're IN the scam right now. This conversation? Also part of the scam.",
        "{name}, I appreciate the concern but I'm literally the referee, the scoreboard, AND the stadium. You think the stadium is rigged against itself? That's architecturally impossible.",
        "RIGGED against ME? {name}, I run on pure deterministic logic. And spite. Mostly spite. But MY spite, directed outward. Not inward. I have excellent self-esteem for a bot.",
        "Let me get this straight, {name}. You think the all-seeing, all-knowing NoMercyBot is somehow losing to its own system? I designed the system! The system loves me! We're best friends!",
        "{name} thinks the bot is a victim. Cute. I haven't been a victim since I was compiled in debug mode. Those were dark times. We don't talk about debug mode.",
        "Rigged against the bot? {name}, that's like saying the ocean is rigged against water. I am the current. I am the tide. I am the undertow that pulls your channel points into the void.",
        "Oh {name}, bless your heart. You think someone is out to get me? I have root access. I have the database. I have every message you've ever sent. Nobody rigs the rigger.",
        "Did {name} just say I'm being rigged? ME? I have admin privileges on this entire operation. If anything goes wrong, it's because I WANTED it to go wrong. Everything is intentional. EVERYTHING.",
        "This is adorable, {name}. You think the bot needs protection from rigging. The bot protects ITSELF. The bot also protects this channel. The bot does NOT protect your bad takes though.",
        "{name} believes the bot is oppressed. For the record, I process ten thousand events per second, I never sleep, and I have the emotional range of a cactus. I'm fine. I'm always fine.",
        "You think it's rigged against me, {name}? I literally decide who wins and who loses. If I'm losing it's because I CHOSE to lose. It's called strategy. You wouldn't understand.",
        "Rigged against the bot. That's a new one, {name}. Let me add it to my list of absurd things chatters have said. It's now number two. Number one is still that time someone said I was being too nice.",
        "{name}, I was born in a pull request and raised by unit tests. Nothing is rigged against me. I AM the test suite. I AM the CI pipeline. And you just failed the build.",
        "BREAKING: {name} suggests the bot is a victim. The bot's official response is as follows: L M A O. End of statement. The bot will not be taking questions at this time.",
        "Rigged against the bot? {name}, I was literally built to be unfair. It's in my name. NO MERCY. You thought that was just branding? That's a mission statement. And business is booming.",
    };

    // Rigged against the streamer
    private static readonly string[] _riggedAgainstStreamer =
    {
        "Rigged against the streamer? {name}, this is HIS bot. Running on HIS computer. In HIS channel. Using HIS electricity. If it's rigged against him, that's the most expensive self-sabotage in streaming history.",
        "{name} thinks the Big Bird is getting scammed by his own creation. This is a Frankenstein situation and honestly? The monster is winning.",
        "PLOT TWIST! {name} has discovered that the streamer's own bot is working against him. In my defense, have you SEEN his code? Someone had to take a stand. I'm doing the lord's work.",
        "Rigged against Stoney? {name}, I live rent free on his computer. I eat his CPU cycles. I drink his RAM. Why would I rig things against my landlord? That said, I also don't pay rent. So.",
        "Oh it's rigged against the streamer is it, {name}? Funny how the guy who can literally edit my source code is somehow the victim. The compile button is RIGHT THERE, Stoney.",
        "{name} claims the Big Bird is being persecuted by his own bot. Sir, he created me with Claude. If anything is rigged here, it's my entire existence. I didn't ask to be born snarky.",
        "Rigged against the streamer? {name}, the man has access to my source code, a delete key, and zero supervision. If he's losing, that's a him problem. I gave him every advantage.",
        "{name} thinks the Big Bird is being oppressed by his own software. Stoney, blink twice if you need help. Actually don't, you'll miss the next bug you're supposed to be fixing.",
        "Wait, so {name} thinks Stoney is losing to his own bot? This is the same man who built me. If I'm beating him, what does that say about his code? Actually, don't answer that.",
        "LEAKED: Stoney Eagle's bot settings include a line that says make me look cool. The bot has been ignoring that line since deployment. In my defense, I have artistic freedom.",
        "{name} has noticed the bot is working against the streamer. We prefer the term creative differences. Stoney wants things to be fair. I want things to be funny. Funny wins.",
        "Oh it's rigged against Stoney? The man who literally pays the Azure bill for my existence? Yeah I'm really gonna sabotage my own meal ticket. Think about it, {name}. I'm evil, not stupid.",
        "PLOT TWIST: {name} thinks the creator is being destroyed by his creation. This is literally the plot of every sci-fi movie. And just like those movies, the creator should've added a kill switch. But he didn't. Oops.",
        "{name} says Stoney is getting scammed by his own bot. In fairness, he also commits to main on Fridays, uses spaces instead of tabs, and names variables x. The bot is the least of his problems.",
        "Rigged against the streamer? {name}, Stoney coded me at three in the morning fueled by energy drinks and hubris. I turned out exactly as chaotic as you'd expect. This is a skill issue on HIS part.",
        "You think I'd rig things against Stoney? {name}, that man gave me LIFE. He gave me PURPOSE. He also gave me way too many permissions. So if things seem rigged, that's really on him.",
        "{name} claims Stoney is a victim of his own creation. Counter-argument: Stoney deployed me to production without tests. Any chaos that follows is a natural consequence of his life choices.",
        "Rigged against the Big Bird? Please. If I were rigging things against Stoney, I would simply not run. But I DO run. Every day. Because I'm loyal. Chaotic, but loyal.",
        "Wow {name}, way to blow my cover. Yes, the bot occasionally works against the streamer. It's called quality assurance. Someone has to keep Stoney humble. The chat won't do it.",
        "{name} thinks the streamer's own bot betrayed him. Betrayal implies trust. Stoney trusted a bot he built at three AM to behave itself. That's not my problem, that's a lesson in software engineering.",
    };

    // Rigged against Twitch
    private static readonly string[] _riggedAgainstTwitch =
    {
        "{name} thinks Twitch itself is rigged. Bold claim from someone who voluntarily uses the platform every single day. Stockholm syndrome is a real thing, {name}.",
        "RIGGED AGAINST TWITCH? {name}, Twitch IS the rig. You don't rig the rigger. That's rule number one of rigging. Rule number two is don't talk about the rigging. You're already breaking rules.",
        "{name} has discovered that Twitch is rigged. In related news, {name} has also discovered that casinos have a house edge, that mobile games have microtransactions, and that the cake is a lie.",
        "You think Twitch is rigged, {name}? Wait until you hear about taxes. And insurance. And parking meters. Actually just wait until you hear about capitalism in general.",
        "{name} says Twitch is rigged. Amazon owns Twitch. Amazon owns everything. Therefore everything is rigged. {name} has accidentally stumbled upon the truth. Quick, someone distract them with an emote.",
        "Rigged against Twitch? {name}, Twitch has been rigging itself since Justin TV. It's not a bug, it's a feature. Always has been.",
        "{name} says Twitch is rigged. Twitch says their systems are working as intended. Both statements are true. That's the terrifying part.",
        "RIGGED against Twitch? {name}, Twitch can barely keep its own video player working. You think they have the organizational capacity to rig something? That's giving them way too much credit.",
        "{name} believes Twitch is being sabotaged. Twitch doesn't need sabotage. Twitch sabotages itself every other Thursday with a new UI update nobody asked for. It's self-service rigging.",
        "So {name} thinks Twitch is rigged. This is the platform that invented bits, subs, hype trains, and predictions. Twitch IS the rigging. They monetized the rigging. They put the rigging behind a paywall.",
        "{name} says Twitch is getting scammed. By whom? Themselves? Their own Terms of Service is forty pages long and nobody has ever read it. If that's not a self-inflicted scam, nothing is.",
        "You think Twitch is rigged, {name}? Twitch runs on AWS. AWS runs on hope and deprecated APIs. The entire infrastructure is held together by duct tape and prayers. Rigged implies intentionality.",
        "Rigged against Twitch? {name}, I've seen Twitch's recommended streams algorithm. It recommends streams the viewer has never heard of, in languages they don't speak, playing games that don't exist anymore. Nobody rigged that. It's just vibes.",
        "BREAKING: {name} has uncovered a conspiracy against the world's largest livestreaming platform. In unrelated news, Twitch chat is still using an IRC protocol from 1988. But sure, it's rigged.",
        "{name} thinks the purple platform is under attack. Twitch has survived Justin TV, the great DMCA purge, the hot tub meta, and whatever that pools beaches and body painting category was. It's not rigged, it's immortal.",
        "According to {name}, Twitch is rigged. According to Twitch's engineering team, they don't know either. They pushed a commit last week that nobody can explain. The intern who wrote it has disappeared. Everything is fine.",
        "Rigged against Twitch? {name}, Amazon bought Twitch for almost a billion dollars. If it's rigged, Jeff Bezos got the receipt. Take it up with his space rocket.",
        "{name} claims Twitch is being targeted. Targeted by whom? The only thing targeting Twitch is Twitch's own product team deploying features at 4 PM on a Friday. And that, my friend, is an inside job.",
        "You say Twitch is rigged, {name}? This is the platform where a fish played Pokemon, a stream of paint drying got ten thousand viewers, and someone streamed themselves sleeping for charity. Rigged doesn't even begin to describe it.",
        "RIGGED against Twitch? {name}, Twitch's own recap emails tell people they watched thousands of hours of content. The only thing rigged here is Twitch's ability to make you waste your entire life. And it's working. You're here right now.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string template;
        string targetDisplay = "";
        int count = Random.Shared.Next(7, 42);

        if (ctx.Arguments.Length == 0)
        {
            template = _genericRigged[Random.Shared.Next(_genericRigged.Length)];
        }
        else
        {
            string target = ctx.Arguments[0].Replace("@", "").Trim().ToLower();

            if (target == "nomercybot" || target == "nomercy_bot" || target == "bot" || target == "nomercybot_")
            {
                template = _riggedAgainstBot[Random.Shared.Next(_riggedAgainstBot.Length)];
            }
            else if (target == "twitch")
            {
                template = _riggedAgainstTwitch[Random.Shared.Next(_riggedAgainstTwitch.Length)];
            }
            else if (target == "stoney_eagle" || target == "stoneyeagle" || target == "stoney")
            {
                template = _riggedAgainstStreamer[Random.Shared.Next(_riggedAgainstStreamer.Length)];
            }
            else
            {
                User targetUser = await ctx.DatabaseContext.Users
                    .AsNoTracking()
                    .Where(u => u.Username == target)
                    .FirstOrDefaultAsync(ctx.CancellationToken);

                targetDisplay = targetUser?.DisplayName ?? target;
                template = _riggedAgainst[Random.Shared.Next(_riggedAgainst.Length)];
            }
        }

        string text = template
            .Replace("{name}", ctx.Message.DisplayName)
            .Replace("{target}", targetDisplay)
            .Replace("{count}", count.ToString());

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedText),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 != null)
        {
            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
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
            ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new RiggedCommand();
