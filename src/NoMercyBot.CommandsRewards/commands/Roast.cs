
public class RoastCommand : IBotCommand
{
    public string Name => "roast";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    private static readonly string[] _roastTemplates =
    {
        // Pure character roasts - no stats needed
        "Ah, {name}. The human equivalent of a participation trophy. You show up. That's... that's about it.",
        "I looked into {name}'s soul and found a 404 error. Content not found. May never have existed.",
        "{name} is proof that natural selection doesn't apply to Twitch chat. Somehow they persist.",
        "If {name} were a spice, they'd be flour. Present in everything, flavor in nothing.",
        "{name}'s chat presence has the energy of someone who Googles 'how to be funny' before every message.",
        "The Big Bird once mistook {name} for a bot. The bot was offended.",
        // Stats-light roasts - mention one stat casually
        "{name} has been here {days} days and I still don't know what they contribute. Neither do they.",
        "In {days} days, {name} has said {messages} things. I remember none of them. And I have a database.",
        "{commands} of {name}'s messages were commands. They literally talk to bots more than people. And I say that as a bot.",
        "{name} averages {avgPerStream} messages a day. My error logs have more personality.",
        // Comparative roasts
        "{name} is the Internet Explorer of this chat. Still here somehow. Nobody knows why. Refuses to retire.",
        "If this chat were a group project, {name} would be the one who adds their name to the Google Doc and calls it a contribution.",
        "{name} brings the same energy as a printer that says it's connected but isn't.",
        "Comparing {name} to a lurker would be an insult to lurkers. At least lurkers commit to something.",
        // Self-aware roasts
        "I was asked to roast {name} but honestly the real roast is that they ASKED to be roasted. That's a cry for attention right there.",
        "{name} wanted a roast. The funniest thing about {name} is that they thought they were interesting enough to roast.",
        "Roasting {name} feels like kicking a puppy. Except the puppy has {messages} chat messages and none of them were funny.",
        // Dev-specific burns
        "{name}'s chat history reads like spaghetti code. No structure. No purpose. But somehow it compiles.",
        "If {name} were a pull request, I'd reject it. Not because it's bad. Because it's unnecessary.",
        "I git blamed {name} and the only thing they're guilty of is wasting {days} days of everyone's time.",
        // Kentucky Fried Eagle special
        "{name} has {messages} messages in {days} days. The Big Bird considered turning those stats into Kentucky Fried Eagle Wings, but even that would have more substance.",

        // ===== PURE PERSONALITY ROASTS (15) =====
        "If {name} were a candle, they'd be unscented.",
        "{name} has the charisma of a loading screen. You just sit there waiting for something to happen.",
        "If personality were a currency, {name} would be filing for bankruptcy.",
        "{name} walks into a room and the room gets quieter. Not awkward quiet. Just... less interesting.",
        "If {name} were a color, they'd be beige. Not even a warm beige. Institutional beige.",
        "{name} is the human equivalent of a terms and conditions page. Everyone scrolls past.",
        "If {name} were a sandwich, they'd be two slices of bread. Just bread. Not even toasted.",
        "{name} radiates the energy of someone who reminds the teacher about homework on a Friday.",
        "If {name} were a movie genre, they'd be the 15 minutes of previews nobody asked for.",
        "I'd compare {name} to cardboard, but cardboard is actually useful in a pinch.",
        "{name} is the kind of person who claps when the plane lands.",
        "If {name} were a season, they'd be that weird week between winter and spring where it's just mud.",
        "{name} has the vibe of a store-brand battery. Technically functional, but nobody's excited about it.",
        "If {name} were a font, they'd be Comic Sans at a funeral.",
        "{name} is the human equivalent of a 'this page intentionally left blank' notice.",

        // ===== POP CULTURE BURNS (10) =====
        "{name} gives off main character energy, but they're actually NPC number 47 standing near a barrel.",
        "{name} is the Jar Jar Binks of this chat. Nobody asked for them but here they are, every single time.",
        "If this chat were the Avengers, {name} would be Hawkeye in the first movie. Just sort of... there.",
        "{name} has the plot armor of a Game of Thrones character in season 8. Inexplicably still alive, but nobody's happy about it.",
        "They say every villain has an origin story. {name}'s is just disappointing Wi-Fi and too much free time.",
        "{name} speedruns being forgettable. World record pace every stream.",
        "{name} is the tutorial level of a person. Necessary, but everyone wants to skip past.",
        "If {name} were a boss fight, you'd beat them by accident while checking your inventory.",
        "{name} rolled a natural 1 on charisma and just kept going.",
        "{name} has the same energy as a side quest you accept but never actually complete.",

        // ===== TWITCH-SPECIFIC ROASTS (10) =====
        "{name} lurks so hard that when they finally type something, I have to check if my database got hacked.",
        "The only exercise {name} gets is jumping to conclusions in chat and running their mouth.",
        "{name} redeems channel points like they're investing in crypto. A lot of confidence, zero returns.",
        "{name} types 'LULW' at things that aren't funny. Which, coincidentally, describes their own messages.",
        "I've seen {name} backseat so hard they should have their own steering wheel peripheral.",
        "{name} acts like a mod but has the authority of a channel point prediction.",
        "Imagine malding over channel points. Now stop imagining because {name} does it for real.",
        "{name} has 'first time chatter' energy despite being here for {days} days.",
        "{name} watches the stream like it's a Netflix series they can't stop hate-watching.",
        "Every time {name} types in chat, a lurker somewhere makes the conscious decision to stay silent. Wise choice.",

        // ===== DEV/TECH ROASTS (15) =====
        "{name} is like a try-catch block with an empty catch. Something goes wrong and they just... ignore it.",
        "If {name} were code, they'd be a TODO comment that's been there for three years.",
        "{name}'s life is an infinite loop with no break statement.",
        "I ran {name} through a linter and got back 'file too broken to parse.'",
        "{name} is the merge conflict nobody wants to resolve.",
        "If {name} were a data structure, they'd be a linked list. Unnecessarily complicated and slow to get to the point.",
        "{name} is the kind of developer who commits directly to main and says 'it works on my machine.'",
        "{name}'s personality has the same energy as an undocumented API. Nobody knows what to expect and it's usually disappointing.",
        "Stack Overflow would mark {name}'s existence as a duplicate. It's been done before, and better.",
        "{name} is a null reference in human form. You think something is there, then boom, NullReferenceException.",
        "If {name} were a git repo, they'd have one commit: 'initial commit' followed by six months of silence.",
        "{name} is the semicolon you forget at 2 AM. Small, insignificant, but somehow breaks everything.",
        "I tried to refactor {name} but there's nothing to work with. You can't refactor an empty file.",
        "{name} has the reliability of a production deployment on a Friday at 4:59 PM.",
        "If {name} were a design pattern, they'd be the anti-pattern. The one they teach you NOT to use.",

        // ===== SELF-DEPRECATING META ROASTS (5) =====
        "I was going to roast {name} but honestly I'm a bot running on StoneyEagle's code, so who am I to judge anyone.",
        "This roast of {name} was brought to you by NoMercyBot, a bot with no mercy and, apparently, no good material either.",
        "I'm programmed to roast {name} but even my AI thinks this is punching down. And I don't even have feelings. Allegedly.",
        "Roasting {name} is hard. Not because they're unroastable but because I'm literally picking from a list of pre-written strings. We're both frauds here.",
        "I just randomly picked this roast for {name} from an array. If it's bad, blame StoneyEagle. If it's good, I take full credit.",

        // ===== BACKHANDED COMPLIMENTS (10) =====
        "Actually, {name} is pretty smart. For someone who spends {days} days watching a guy code on Twitch.",
        "Credit where it's due, {name} is consistent. Consistently mid, but consistent.",
        "I'll say this for {name}: they have great taste in streams. Everything else is questionable.",
        "{name} is honestly one of the most dedicated people in this chat. It takes real commitment to be this unremarkable for {days} days straight.",
        "You know what, {name} has potential. I mean, I've been saying that for {days} days now but one day it might be true.",
        "{name} is brave. It takes courage to show up every day and contribute nothing. Not everyone can do that.",
        "I respect {name}'s confidence. Most people with their chat history would have switched to lurk-only mode.",
        "{name} is actually really good at one thing: lowering the bar for everyone else. And honestly, we appreciate the service.",
        "Not gonna lie, {name} typed something genuinely funny once. I don't remember when. But statistically in {messages} messages, it probably happened.",
        "Hats off to {name}. They've sent {messages} messages and not a single one got them banned. That's restraint.",

        // ===== ABSURD/SURREAL ROASTS (10) =====
        "Scientists studied {name}'s chat messages and concluded that language was a mistake.",
        "If you rearrange the letters in {name}'s username, you get... nothing useful. Just like their chat messages.",
        "I asked ChatGPT to summarize {name}'s chat history and it responded with 'I'd rather not.'",
        "A parallel universe exists where {name} is funny. Unfortunately, we are not in that universe.",
        "{name}'s messages have been classified by the FDA as a mild sedative.",
        "Archaeologists will one day discover {name}'s chat logs and conclude that this civilization was doomed.",
        "If you stare at {name}'s chat history long enough, it stares back. And then you both feel uncomfortable.",
        "Legend has it that if you whisper {name}'s username three times in a mirror, nothing happens. Because even ghosts aren't interested.",
        "{name} once typed a message so bland that my sentiment analysis returned 'why.'",
        "The Big Bird saw {name}'s message history and briefly considered migrating to YouTube. That's how serious this is.",

        // ===== STATS-BASED CREATIVE ROASTS (5) =====
        "It took {name} {days} days to type {messages} messages. A sloth with a keyboard would have been more productive.",
        "{name} has used {commands} commands. That means roughly {commands} times they needed a bot to be interesting for them.",
        "{name} averages {avgPerStream} messages a day. My garbage collector runs more often than that and it's more entertaining.",
        "In {days} days, {name} has generated {messages} messages of pure filler content. Wikipedia's 'list of nothing' has more substance.",
        "{name} has been here {days} days. That's {days} consecutive days of choosing this over therapy.",
    };

    private static readonly string[] _noDataResponses =
    {
        "I tried to roast them but my database returned null. They're literally a ghost.",
        "Who? I have zero records on that person. Can't roast what doesn't exist.",
        "My query returned empty. They have no chat history. They are the dev/null of this channel.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Roast who? Give me a target. Usage: !roast @username",
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
            string noData = _noDataResponses[Random.Shared.Next(_noDataResponses.Length)];
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {noData}",
                ctx.Message.Id
            );
            return;
        }

        var stats = await ctx.DatabaseContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == targetUser.Id && !m.DeletedAt.HasValue)
            .GroupBy(m => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Commands = g.Count(m => m.IsCommand),
                Earliest = g.Min(m => m.CreatedAt),
            })
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (stats == null || stats.Total == 0)
        {
            string noData = _noDataResponses[Random.Shared.Next(_noDataResponses.Length)];
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {noData}",
                ctx.Message.Id
            );
            return;
        }

        int totalMessages = stats.Total;
        int commandCount = stats.Commands;
        int daysSinceFirst = Math.Max(1, (int)(DateTime.UtcNow - stats.Earliest).TotalDays);
        int avgPerDay = totalMessages / daysSinceFirst;

        string template = _roastTemplates[Random.Shared.Next(_roastTemplates.Length)];
        string text = template
            .Replace("{name}", targetUser.DisplayName)
            .Replace("{messages}", totalMessages.ToString())
            .Replace("{commands}", commandCount.ToString())
            .Replace("{days}", daysSinceFirst.ToString())
            .Replace("{avgPerStream}", avgPerDay.ToString());

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

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
            text,
            ctx.Message.Id
        );
    }
}

return new RoastCommand();
