
public class WeatherCommand : IBotCommand
{
    public string Name => "weather";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string ANCHOR_VOICE = "en-US-GuyNeural";

    private static readonly string[] _weatherTemplates =
    {
        "Good evening chat, this is your NoMercy Weather Report. We're tracking a massive copypasta front moving in from the east, expected to flood chat within the hour. Currently {count} viewers are in the splash zone. Temperatures holding steady at {temp} degrees of pure cope. Back to you, Big Bird.",
        "WEATHER ALERT: A lurker pressure system has settled over the channel. Barometric chat activity is at an all-time low, with {percent} percent of viewers contributing absolutely nothing. The forecast calls for continued silence with a chance of someone typing LUL around {temp} minutes past the hour.",
        "Tonight's forecast: Heavy emote storms rolling through chat with gusts up to {count} Kappas per minute. Visibility is low due to dense copypasta fog. The National Twitch Service advises all chatters to seek shelter in lurk mode immediately.",
        "BREAKING: The lurker drought continues for day {count}. Chat activity has dropped to {percent} percent below seasonal averages. The channel point economy is in shambles. The Big Bird sends his regards.",
        "Hype train lightning has been spotted on the horizon. Our radar shows {count} chatters preparing to type in all caps. Current wind speeds are at {temp} WPMs of pure keyboard mashing. Take cover, this one's gonna be loud.",
        "The bit precipitation index is dangerously low tonight, folks. We're seeing only {count} bits per hour, well below the federal minimum for streamer happiness. Channel point inflation has risen {percent} percent as a result. Economists are baffled. Big Bird is not amused.",
        "RAID ADVISORY in effect for the greater NoMercy metropolitan area. A category {count} raid could make landfall at any moment. Residents are advised to spam the incoming emotes and prepare for total chat chaos. This is not a drill.",
        "Good evening, I'm your NoMercy meteorologist. Channel point inflation has reached {percent} percent, the highest we've seen since the Great Prediction Crash of last Tuesday. {count} viewers lost everything. Thoughts and prayers.",
        "Mod activity barometric pressure is holding steady at {temp} degrees of sus. Our sensors detect {count} messages flagged for review and {percent} percent chance of a timeout before the stream ends. Chat, behave yourselves. You won't, but I said it.",
        "MIGRATION UPDATE: Big Bird migration patterns indicate the eagle is heading toward a coding session. {count} viewers have already begun their annual lurk migration. Expect reduced chat activity and increased keyboard ASMR for the next {temp} minutes.",
        "HugFactory humidity levels are at {percent} percent tonight. That's {count} hugs per hour saturating the chat atmosphere. If you're not careful, you WILL be hugged. There is no umbrella for this. The HugFactory does not close.",
        "A NoMercy cold front is sweeping through the channel. Temperatures have dropped to {temp} degrees of savage. {count} chatters have already been roasted. Wind chill makes it feel like negative {percent} degrees of mercy. There is, as always, no mercy.",
        "EXTENDED FORECAST: Monday through Friday, expect continuous streams of code with intermittent bug showers. {percent} percent chance of the streamer blaming the compiler. {count} instances of the phrase 'it works on my machine' expected by end of week.",
        "This just in from the NoMercy Doppler radar: a wall of KEKW is approaching from the south at {temp} miles per hour. {count} chatters are already in its path. Estimated time of impact: the next time Stoney makes a typo. So approximately thirty seconds.",
        "Current conditions over the channel: {temp} degrees with {percent} percent chat humidity. Dew point is at {count} lurkers, which is unusually high. The atmosphere is thick with unspoken opinions. Someone is about to have a take. Brace yourselves.",
        "SEVERE COPYPASTA WARNING: The National Twitch Service has issued a severe copypasta warning for the NoMercy region. {count} identical messages expected in the next five minutes. Seek shelter. Do not engage. Do not copy. Do not paste. You will anyway.",
        "Tonight's Big Bird feather forecast: {percent} percent chance of stolen feathers, with {count} viewers actively plotting theft. Current feather security level is at {temp} degrees, which our analysts describe as 'not great.' The eagle remains vigilant.",
        "TRAFFIC REPORT: Chat highway is experiencing major congestion. {count} messages backed up at the mod checkpoint. Average speed has dropped to {temp} words per minute. Alternate routes through whispers are recommended. This has been your NoMercy traffic update.",
        "The five-day outlook shows a persistent high-pressure system of backseating settling over the channel. {percent} percent of chatters think they know better than the streamer. {count} unsolicited suggestions expected before midnight. The streamer's patience is at {temp} degrees and dropping.",
        "SPECIAL BULLETIN: A rare phenomenon known as the Double Gifted Sub Rainbow has been spotted over the channel. Scientists say it occurs once every {count} streams. {percent} percent of viewers missed it because they were tabbed out. Classic.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        int count = Random.Shared.Next(10, 201);
        int percent = Random.Shared.Next(10, 100);
        int temp = Random.Shared.Next(50, 101);

        string template = _weatherTemplates[Random.Shared.Next(_weatherTemplates.Length)];

        string text = template
            .Replace("{count}", count.ToString())
            .Replace("{percent}", percent.ToString())
            .Replace("{temp}", temp.ToString());

        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(ANCHOR_VOICE, processedText),
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

return new WeatherCommand();
