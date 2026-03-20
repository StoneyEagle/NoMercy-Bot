
public class KarenCommand : IBotCommand
{
    public string Name => "karen";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";
    private const string KAREN_VOICE = "en-US-JennyNeural";

    private static readonly string[] _karenRantsTargeted =
    {
        "EXCUSE ME. I have been watching this channel for {count} MONTHS and I have NEVER seen {name} contribute ANYTHING of value to this chat. I want to speak to the head moderator IMMEDIATELY.",
        "I need to file a FORMAL COMPLAINT. {name} has been lurking in this chat for {count} streams without saying a SINGLE WORD. That is THEFT of community engagement and I will NOT stand for it.",
        "Hi yes, I'd like to report {name} for EXCESSIVE use of emotes. I counted {count} in the last hour alone. This is a PROFESSIONAL stream, not an emoji dumpster. Where is the manager?",
        "I am APPALLED that {name} is allowed to type in this chat without proper qualifications. I have been here since DAY ONE and I demand that all chatters pass a vibe check before being allowed to speak. Starting with {name}.",
        "UNACCEPTABLE. {name} just used an emote that I don't have access to. I pay GOOD channel points to be here and I expect EQUAL emote rights. I will be contacting Twitch corporate about this DISCRIMINATION.",
        "I would like to speak to whoever is in charge of channel points because {name} just redeemed something and it is RUINING my viewing experience. I have {count} channel points saved up and I demand priority service.",
        "Listen here. I have been a LOYAL viewer for {count} months and I just watched {name} get a shoutout before ME. Do you know how many bits I've donated? This is FAVORITISM and the Big Bird will be hearing from my lawyer.",
        "I am NOT leaving until someone explains to me why {name} gets to use that username. It is OFFENSIVE to me personally and I demand they change it. I've already drafted a {count} page complaint to Twitch support.",
        "Excuse me but {name}'s message just pushed MY message off screen and I was NOT done being acknowledged. I demand a FULL REFUND of the attention I was owed. This is the WORST customer service I've ever experienced in a Twitch chat.",
        "I have RECEIPTS showing that {name} has been in this chat for {count} streams without following. That is basically STEALING content and I for one will not TOLERATE freeloaders in MY community.",
        "Oh so {name} gets to backseat the stream but when I do it I get timed out? This is a DOUBLE STANDARD and I will be leaving a one star review on every social media platform. My cousin works at Amazon by the way.",
        "I need to speak to the owner of this bot IMMEDIATELY. {name} just got a hug from the HugFactory and I have been WAITING for {count} minutes. I was here FIRST. Check the logs.",
        "Can someone PLEASE explain why {name} is allowed to have fun in this chat while I sit here SUFFERING? I have been watching for {count} months and this is NOT the experience I was promised. I want a refund on my TIME.",
        "I am FILING a formal grievance. {name} changed their display name {count} times this month and it is causing me EMOTIONAL DISTRESS. Pick a name and COMMIT to it like the rest of us, {name}.",
        "THIS IS OUTRAGEOUS. {name} just sent a message that was LONGER than mine and got MORE responses. I demand a character limit be imposed on everyone except ME. I'm a founding member of this community.",
        "I want it on RECORD that {name} laughed at something the streamer said and I laughed FIRST. My laugh was in my head but it COUNTS. I've been the number one fan here for {count} months and I will NOT be outperformed.",
        "MANAGEMENT. NOW. {name} just suggested a game and the streamer actually CONSIDERED it. I have submitted {count} game suggestions and not ONE has been acknowledged. This is CHATTER SUPPRESSION.",
        "I didn't spend {count} hours watching this stream to have {name} waltz in here and steal the spotlight with their so-called personality. Some of us EARNED our place in this chat through DEDICATED lurking.",
        "HELLO? Is anyone LISTENING? {name} just used the word 'pog' and I find that INCREDIBLY unprofessional. This stream has STANDARDS. Or at least it DID before {name} showed up {count} streams ago.",
        "I am writing a STRONGLY WORDED letter to the Big Bird himself about {name}'s behavior. {count} incidents. I've documented ALL of them. In a spreadsheet. Color coded. With graphs. This ends TODAY.",
    };

    private static readonly string[] _karenRantsGeneral =
    {
        "I need to speak to whoever is RUNNING this stream because the schedule has changed {count} times this month and I have built my ENTIRE LIFE around it. This is UNACCEPTABLE.",
        "EXCUSE ME but I was told this was a coding stream and I have seen ZERO lines of production-ready code. I want a REFUND of my time. All {count} hours of it.",
        "I would like to file a complaint about the chat rules. Rule number {count} is CLEARLY targeting me specifically and I will NOT be silenced. My opinions are VALID and IMPORTANT.",
        "Why is the stream NOT in 4K? I pay for internet and I expect PREMIUM quality content. The Big Bird would be ASHAMED of these bitrate settings. I demand an upgrade IMMEDIATELY.",
        "This bot just said something SARCASTIC to me and I want it REPROGRAMMED. I am a PAYING viewer and I deserve RESPECT from all entities in this chat, artificial or otherwise.",
    };

    private static readonly string[] _botIntros =
    {
        "We have a complaint from the management.",
        "Oh no. She's back.",
        "Security alert. Karen has entered the chat.",
        "Attention. A formal complaint has been filed.",
        "Code red. Entitled viewer inbound.",
        "The complaint department is now open. Unfortunately.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        bool hasTarget = ctx.Arguments.Length > 0;
        string targetName = hasTarget
            ? ctx.Arguments[0].Replace("@", "").Trim()
            : "";

        if (hasTarget)
        {
            User targetUser = await ctx.DatabaseContext.Users
                .AsNoTracking()
                .Where(u => u.Username == targetName.ToLower())
                .FirstOrDefaultAsync(ctx.CancellationToken);

            if (targetUser != null)
                targetName = targetUser.DisplayName;
        }

        int count = Random.Shared.Next(3, 48);

        string rant;
        if (hasTarget)
        {
            rant = _karenRantsTargeted[Random.Shared.Next(_karenRantsTargeted.Length)]
                .Replace("{name}", targetName)
                .Replace("{count}", count.ToString());
        }
        else
        {
            rant = _karenRantsGeneral[Random.Shared.Next(_karenRantsGeneral.Length)]
                .Replace("{count}", count.ToString());
        }

        string intro = _botIntros[Random.Shared.Next(_botIntros.Length)];
        string fullText = $"{intro} \"{rant}\"";

        string chatText = fullText;
        if (chatText.Length > 450)
            chatText = chatText[..447] + "...";

        string processedIntro = await ctx.TtsService.ApplyUsernamePronunciationsAsync(intro);
        string processedRant = await ctx.TtsService.ApplyUsernamePronunciationsAsync(rant);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedIntro),
            TtsService.Segment(KAREN_VOICE, processedRant),
        };

        (string audioBase64, int durationMs) = await ctx.TtsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);
        if (audioBase64 != null)
        {
            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
            {
                text = fullText,
                user = new { id = ctx.Message.UserId },
                audioBase64,
                provider = "Edge",
                cost = 0m,
                characterCount = fullText.Length,
                cached = false,
            });
        }

        await ctx.TwitchChatService.SendReplyAsBot(
            ctx.Message.Broadcaster.Username, chatText, ctx.Message.Id);
    }
}

return new KarenCommand();
