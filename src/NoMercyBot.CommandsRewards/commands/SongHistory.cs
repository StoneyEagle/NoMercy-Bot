
public class SongHistoryCommand : IBotCommand
{
    public string Name => "songhistory";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    // ===== MUSIC TASTE ROASTS (15) =====
    private static readonly string[] _musicTasteTemplates =
    {
        "According to my records, {name} has requested {songs} songs. I've listened to all of them. I need therapy now.",
        "{name} has requested {songs} songs and somehow each one was worse than the last. That takes talent.",
        "{songs} song requests from {name}. My audio circuits are filing a harassment complaint.",
        "{name} has inflicted {songs} songs upon this channel. The Geneva Convention has opinions about this.",
        "I analyzed {name}'s {songs} song requests and my recommendation algorithm just quit.",
        "{name} dropped {songs} songs in the queue. Spotify sent me a formal apology.",
        "{songs} requests from {name}. If bad taste were a skill, they'd be speedrunning world records.",
        "{name}'s {songs} song requests have been classified as a war crime by the Hague. Pending trial.",
        "In all my years of service, {name}'s {songs} song requests are the strongest argument for muting audio.",
        "{name} has queued {songs} songs. The Big Bird considered migrating just to escape the playlist.",
        "{songs} songs from {name}. Each one a unique expression of questionable life choices.",
        "{name} really sat there and thought 'yes, this channel needs {songs} of MY song picks.' The audacity.",
        "I cross-referenced {name}'s {songs} song requests with a database of good music. Zero matches.",
        "{name} has requested {songs} songs and somehow turned the playlist into a cry for help.",
        "After {songs} song requests, {name} has single-handedly proven that freedom of music choice was a mistake.",
    };

    // ===== DJ COMPARISON (10) =====
    private static readonly string[] _djTemplates =
    {
        "{name} has requested {songs} songs. That makes them the worst DJ this channel has ever had. And that's saying something.",
        "With {songs} requests, {name} is basically the channel DJ. Unfortunately, it's the kind of DJ who clears the dance floor.",
        "{name} has {songs} song requests. DJ {name} in the house. And by house I mean a house that everyone is leaving.",
        "{songs} songs from {name}. If this were a nightclub, they'd have been fired after the first set.",
        "They call {name} DJ Shuffle because their {songs} song requests have the consistency of random noise.",
        "{name} has dropped {songs} tracks. The only thing dropping harder is the viewer count when their songs play.",
        "With {songs} requests, {name} has DJ'd more than anyone asked for. Literally. Nobody asked.",
        "{name}: {songs} songs requested. Zero bangers detected. DJ license revoked.",
        "{songs} requests and {name} still hasn't figured out what this channel likes. Persistence without improvement.",
        "Someone give {name} a DJ name. I suggest 'DJ {songs} Misses' because they haven't hit once.",
    };

    // ===== QUANTITY VS QUALITY (10) =====
    private static readonly string[] _quantityTemplates =
    {
        "{songs} song requests from {name}. Quantity over quality is their motto. Mostly quantity of bad choices.",
        "{name} believes if you throw {songs} songs at the wall, one might stick. Spoiler: none stuck.",
        "Imagine requesting {songs} songs and not a single one being a banger. {name} doesn't have to imagine.",
        "{name} has requested {songs} songs. That's {songs} opportunities to pick something good and {songs} misses.",
        "{songs} requests. {name} is playing the numbers game with music and losing spectacularly.",
        "Statistically, out of {songs} songs, at least ONE should be good. {name} defies statistics.",
        "{name} has {songs} song requests. The law of averages says some should be decent. The law of {name} says otherwise.",
        "{songs} songs from {name}. Proof that more is definitely not better.",
        "{name} went for volume over value with {songs} requests. Like ordering everything on the menu and liking none of it.",
        "They say a broken clock is right twice a day. {name}'s {songs} song requests suggest otherwise.",
    };

    // ===== ZERO SONGS =====
    private static readonly string[] _zeroTemplates =
    {
        "{name} has never requested a single song. Coward. Even lurkers have better playlist contributions.",
        "Zero song requests from {name}. They've been here this whole time contributing absolutely nothing to the vibe.",
        "{name}: zero songs requested. They freeload off everyone else's music taste. Impressive commitment to doing nothing.",
    };

    // ===== LOW COUNT (1-5) =====
    private static readonly string[] _lowTemplates =
    {
        "{name} has requested {songs} songs. Barely a blip on the radar. Are they even trying?",
        "{songs} song requests from {name}. That's not a playlist, that's a sticky note.",
        "{name} has contributed {songs} songs to this channel. The bare minimum. The absolute floor.",
        "Only {songs} songs from {name}. They dipped a toe in the playlist pool and immediately retreated.",
    };

    // ===== MID COUNT (6-20) =====
    private static readonly string[] _midTemplates =
    {
        "{name} has requested {songs} songs. A respectable amount of damage to everyone's ears.",
        "{songs} requests from {name}. Just enough to be annoying but not enough to be memorable.",
        "{name} is at {songs} song requests. Solidly mediocre playlist participation. The C-minus of music contribution.",
    };

    // ===== ABSURD (5) =====
    private static readonly string[] _absurdTemplates =
    {
        "Scientists analyzed {name}'s {songs} song requests and classified them as a new form of noise pollution.",
        "NASA detected {name}'s {songs} song requests from orbit. They thought it was a distress signal. They were right.",
        "The Big Bird once tried to listen to all {songs} of {name}'s song requests in a row. The Big Bird is still in therapy.",
        "I fed {name}'s {songs} song requests into a machine learning model. The model learned to say no.",
        "Archaeologists will one day discover {name}'s {songs} song requests and conclude our civilization deserved to fall.",
    };

    private static readonly string[] _notFoundResponses =
    {
        "Who? I have no records of that person. They're a ghost with no playlist history.",
        "I searched my entire database for that user and found nothing. They don't exist in my reality.",
        "Error 404: User not found. Can't judge music taste that doesn't exist. Lucky them.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Check whose song history? Usage: !songhistory @username",
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
            string noData = _notFoundResponses[Random.Shared.Next(_notFoundResponses.Length)];
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {noData}",
                ctx.Message.Id
            );
            return;
        }

        int songCount = await ctx.DatabaseContext
            .Records.AsNoTracking()
            .Where(r => r.UserId == targetUser.Id && r.RecordType == "Spotify")
            .CountAsync(ctx.CancellationToken);

        string template;

        if (songCount == 0)
        {
            template = _zeroTemplates[Random.Shared.Next(_zeroTemplates.Length)];
        }
        else if (songCount <= 5)
        {
            template = _lowTemplates[Random.Shared.Next(_lowTemplates.Length)];
        }
        else if (songCount <= 20)
        {
            template = _midTemplates[Random.Shared.Next(_midTemplates.Length)];
        }
        else
        {
            // 21+ songs: pick from all the general pools
            string[][] pools = new[]
            {
                _musicTasteTemplates,
                _djTemplates,
                _quantityTemplates,
                _absurdTemplates,
            };
            string[] chosenPool = pools[Random.Shared.Next(pools.Length)];
            template = chosenPool[Random.Shared.Next(chosenPool.Length)];
        }

        string text = template
            .Replace("{name}", targetUser.DisplayName)
            .Replace("{songs}", songCount.ToString());

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

return new SongHistoryCommand();
