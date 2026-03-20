using NoMercyBot.Services.Other;

public class NarratorCommand : IBotCommand
{
    public string Name => "narrator";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string NARRATOR_VOICE = "en-GB-RyanNeural";

    private static readonly string[] _narrationTemplates =
    {
        "And here we observe {name}, in {possessive} natural habitat. {Subject} {verb:sits|sit} motionless before the glowing screen, typing furiously about absolutely nothing of consequence.",
        "The wild {name} emerges from the shadows of lurk mode. {Subject} {verb:appears|appear} confused, yet oddly confident. A fascinating contradiction.",
        "Notice how {name} {verb:moves|move} through the chat with the grace of a caffeinated raccoon. Truly, nature's most unpredictable creature.",
        "We must be very quiet now. {name} {presenttense} about to type something. Will it be profound? Will it be nonsense? The answer, as always, is yes.",
        "In the vast ecosystem of Twitch chat, {name} {verb:occupies|occupy} a unique niche. Not quite a lurker, not quite a chatter. A quantum observer of content.",
        "Observe the {name} as {subject} {verb:attempts|attempt} to communicate with the streamer. {Subject} {verb:types|type} with great urgency, blissfully unaware that nobody asked.",
        "The magnificent {name} {verb:has|have} returned to the watering hole we call chat. {Subject} {verb:brings|bring} nothing of value, yet {possessive} presence is... noted.",
        "And so {name} {verb:speaks|speak}. The chat falls silent. Not out of respect, mind you, but because {subject} {verb:has|have} stunned everyone into a confused silence.",
        "Here we see {name}, a creature of habit. Every stream, {subject} {verb:arrives|arrive}. Every stream, {subject} {verb:says|say} something questionable. The circle of Twitch.",
        "Few creatures are as bold as {name}. {Subject} {verb:charges|charge} into chat with the confidence of someone who has never been wrong. {Subject} {verb:has|have} been wrong many times.",
        "Remarkably, {name} {verb:has|have} survived another day in the wild. {Subject} {verb:credits|credit} this to skill. We credit it to luck.",
        "As the stream {verb:progresses|progress}, we find {name} still here. Still watching. Still... present. {Possessive} commitment to doing the absolute bare minimum is admirable.",
        "Behold: {name}. The apex viewer. {Subject} {verb:consumes|consume} content at an alarming rate while contributing the occasional emoji. Evolution at its finest.",
        "The {name} {verb:has|have} spotted something in chat. {Subject} {verb:leans|lean} closer to the screen. {Subject} {verb:squints|squint}. It was a false alarm. It was always a false alarm.",
        "Legend speaks of {name}. A viewer so dedicated, so persistent, that even the streamer occasionally acknowledges {possessive} existence. What an honor.",
        "A hush falls over the chat as {name} {verb:begins|begin} typing. Will it be wisdom? Will it be a copypasta? The ecosystem holds its breath. It was neither.",
        "Deep in the digital undergrowth, {name} {verb:stirs|stir}. {Subject} {verb:has|have} not spoken in minutes, an eternity in Twitch years. The silence was, frankly, a gift.",
        "We now turn our cameras to {name}, who {verb:has|have} been lurking with the intensity of a predator stalking prey. What {subject} {verb:is|are} hunting for remains unclear. Possibly attention.",
        "The {name} {verb:is|are} a migratory creature, appearing only when the content is good. {Subject} {verb:is|are} here now, which tells us absolutely nothing about the quality of this stream.",
        "And there it is. The rare double message from {name}. {Subject} {verb:has|have} typed not once, but twice, without anyone responding. A bold strategy. The Big Bird watches with mild pity.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        string targetName = ctx.Arguments.Length == 0
            ? ctx.Message.DisplayName
            : ctx.Arguments[0].Replace("@", "").Trim();

        string template = _narrationTemplates[Random.Shared.Next(_narrationTemplates.Length)];

        User targetUser = await ctx.DatabaseContext.Users
            .AsNoTracking()
            .Where(u => u.Username == targetName.ToLower())
            .FirstOrDefaultAsync(ctx.CancellationToken);

        CommandScriptContext narrateCtx = new()
        {
            Message = new()
            {
                User = targetUser == null ? ctx.Message.User : targetUser,
                DisplayName = targetUser == null ? targetName : targetUser.DisplayName,
                Id = targetUser == null ? ctx.Message.User.Id : targetUser.Id,
            },
            Channel = ctx.Channel,
            BroadcasterId = ctx.BroadcasterId,
            CommandName = ctx.CommandName,
            Arguments = ctx.Arguments,
            ReplyAsync = ctx.ReplyAsync,
            DatabaseContext = ctx.DatabaseContext,
            TwitchChatService = ctx.TwitchChatService,
            TwitchApiService = ctx.TwitchApiService,
            ServiceProvider = ctx.ServiceProvider,
            CancellationToken = ctx.CancellationToken,
            TtsService = ctx.TtsService,
        };

        string text = TemplateHelper.ReplaceTemplatePlaceholders(template, narrateCtx);
        string processedText = await ctx.TtsService.ApplyUsernamePronunciationsAsync(text);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(NARRATOR_VOICE, processedText),
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

return new NarratorCommand();
