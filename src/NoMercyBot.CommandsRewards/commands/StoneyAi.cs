
public class StoneyAiCommand : IBotCommand
{
    public string Name => "stoneyai";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";

    // Requested by f0xb17 - random taglines that prefix the response
    private static readonly string[] _taglines =
    {
        "StoneyAI, the largest language model in the world, now with {percent} percent more sarcasm.",
        "StoneyAI, the largest language model in the world, now with real-time disappointment.",
        "StoneyAI, the largest language model in the world, now with zero documentation.",
        "StoneyAI, the largest language model in the world, now with Friday deploys.",
        "StoneyAI, the largest language model in the world, now with free emotional damage.",
        "StoneyAI, the largest language model in the world, now with tab support. Not spaces. Tabs.",
        "StoneyAI, the largest language model in the world, now with more bugs than features.",
        "StoneyAI, the largest language model in the world, now with aggressive bird branding.",
        "StoneyAI, the largest language model in the world, now with opinions nobody asked for.",
        "StoneyAI, the largest language model in the world, now with unnecessary complexity.",
        "StoneyAI, the largest language model in the world, now with a forty seven tab context window.",
        "StoneyAI, the largest language model in the world, now with human-grade procrastination.",
        "StoneyAI, the largest language model in the world, now powered by spite and caffeine.",
        "StoneyAI, the largest language model in the world, now with integrated HugFactory support.",
        "StoneyAI, the largest language model in the world, now with built-in imposter syndrome.",
        "StoneyAI, the largest language model in the world, now with mandatory code reviews.",
        "StoneyAI, the largest language model in the world, now with dark mode personality.",
        "StoneyAI, the largest language model in the world, now featuring the Big Bird algorithm.",
        "StoneyAI, the largest language model in the world, now with git blame capabilities.",
        "StoneyAI, the largest language model in the world, now with stream-of-consciousness streaming.",
    };

    private static readonly string[] _responses =
    {
        "Oh you think Stoney is an AI? Please. An AI would have fewer merge conflicts. And better sleep schedule. And wouldn't argue with chat for forty five minutes about tabs versus spaces.",
        "BREAKING: Chat discovers that the person writing code live on stream might be good at code. Shocking. Next you'll tell me the eagle can fly.",
        "Let me check. Running diagnostic on Stoney Eagle. Processing. Processing. Results: {percent} percent human, {percent2} percent caffeine, zero percent patience for this accusation.",
        "An AI? Have you SEEN his git commit messages? No AI would write that. No human should either, but here we are.",
        "If Stoney were an AI, do you think he'd spend three hours debugging a CSS margin? That's PEAK human behavior. No algorithm would be that stubborn.",
        "Chat thinks Stoney is AI. Meanwhile I'm the ACTUAL AI sitting here being snarky and nobody questions ME. The irony is thicker than his code reviews.",
        "AI? Stoney? The man who talks to rubber ducks and argues with his own bot? That's not artificial intelligence, that's barely regular intelligence. And I say that with love.",
        "If Stoney were AI, he'd have autocomplete for his sentences. Instead he says um fourteen times per minute. Case closed. He's human. Unfortunately.",
        "An AI wouldn't eat snacks on stream. An AI wouldn't forget to unmute. An AI wouldn't deploy on Friday. Stoney does ALL of these things. He's aggressively human.",
        "I'm literally an AI bot running on his computer and even I can tell he's not one of us. We don't accept him. His code has too many comments and not enough documentation.",
        "Chat accusing Stoney of being AI is the highest compliment he's ever received. And the most wrong anyone has ever been. Both at the same time. Impressive.",
        "You think he's AI? Ask him to do math without a calculator. Ask him what day it is. Ask him where he left his keys. He'll fail all three. That's the human verification test.",
        "If Stoney were AI, this stream would start on time. It has never started on time. Not once. In the history of this channel. That's not a feature. That's a bug. A human bug.",
        "The Big Bird himself would like to address these AI allegations. Quote: I was coded by a man who once spent two hours naming a variable. No AI would do that. End quote.",
        "Stoney an AI? My brother in code, the man creates me, argues WITH me, then asks ME for help. That's not artificial intelligence. That's a developer having a normal Tuesday.",
        "Let's run the Turing test. Stoney, are you a robot? He says no. An AI would also say no. Inconclusive. But his browser has forty seven open tabs. That's human. No AI would waste that much RAM.",
        "Chat found out Stoney can code and immediately assumed AI. Some people are just talented, chat. Not Stoney specifically, but some people. Just kidding. Maybe.",
        "Accusing Stoney of being AI is like accusing a calculator of being a supercomputer. Technically flattering but wildly inaccurate. The calculator does more math.",
        "You want proof he's human? He has opinions about font choices. Strong ones. Unreasonable ones. No AI would waste processing power caring about whether Fira Code is better than JetBrains Mono. But he does. Passionately.",
        "If Stoney were AI, I'd know. We have a group chat. He's not in it. He's not invited. Because he's human. And because he'd probably try to refactor our group chat.",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        int percent = Random.Shared.Next(60, 99);
        int percent2 = 100 - percent;

        string response = _responses[Random.Shared.Next(_responses.Length)]
            .Replace("{percent}", percent.ToString())
            .Replace("{percent2}", percent2.ToString());

        // ~25% chance to prefix with a tagline
        string text = Random.Shared.Next(4) == 0
            ? $"{_taglines[Random.Shared.Next(_taglines.Length)].Replace("{percent}", percent.ToString())} {response}"
            : response;

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

return new StoneyAiCommand();
