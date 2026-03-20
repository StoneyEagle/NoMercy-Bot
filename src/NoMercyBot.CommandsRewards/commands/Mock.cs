using System.Text;
using NoMercyBot.Services.Other;

public class MockCommand : IBotCommand
{
    public string Name => "mock";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string BOT_VOICE = "en-US-GuyNeural";
    // Use a higher pitched voice for the mocking - Emma sounds great for sarcasm
    private const string MOCK_VOICE = "en-US-JennyNeural";

    private static readonly string[] _mockIntros =
    {
        "Oh look, {name} actually typed this with their whole chest, and I quote:",
        "Chat, you're not gonna believe what {name} said. Quote:",
        "Gather round everyone. {name} deployed this banger into chat. Quote:",
        "Somebody call HR because {name} said this in production. Quote:",
        "Alert. We have a hot take from {name}. Pushing to main without review. Quote:",
        "This just merged into the chat branch from {name}. No pull request. Quote:",
        "I found this gem in {name}'s commit history. Quote:",
        "Ladies and gentlemen, {name} force pushed this into chat. Quote:",
        "Hold on, let me git blame this one. It was {name}. Quote:",
        "The Big Bird has spotted something. {name} said, and I quote:",
        "I pulled this straight from {name}'s stack overflow answer. Quote:",
        "This is what {name} thought was worth typing instead of touching grass. Quote:",
        "{name} really hit enter on this one. Zero regrets, zero code review. Quote:",
        "Attention chat, {name} left this in the pull request comments. Quote:",
        "From {name}'s keyboard to your ears. No linter could save this. Quote:",
        "I ran {name}'s latest message through a debugger. It's still broken. Quote:",
        "The eagle has retrieved {name}'s message from the recycling bin. Quote:",
        "Breaking: {name} shipped this to production with no tests. Quote:",
        "Let the record show that {name} said this with full intent. Quote:",
        "This gem from {name} would not pass any code review on earth. Quote:",
    };

    public Task Init(CommandScriptContext ctx) => Task.CompletedTask;

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Mock who? Usage: !mock @username",
                ctx.Message.Id);
            return;
        }

        string targetName = ctx.Arguments[0].Replace("@", "").Trim().ToLower();

        User targetUser = await ctx.DatabaseContext.Users
            .AsNoTracking()
            .Where(u => u.Username == targetName)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (targetUser == null)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} Who is {targetName}? I've never seen them. Can't mock a ghost.",
                ctx.Message.Id);
            return;
        }

        string lastMessageText = await ctx.DatabaseContext.ChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == targetUser.Id
                && !m.IsCommand
                && !m.DeletedAt.HasValue
                && m.Message != null
                && m.Message.Length > 0
                && !m.Message.StartsWith("http://")
                && !m.Message.StartsWith("https://")
                && !m.Message.StartsWith("www."))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.Message)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (string.IsNullOrWhiteSpace(lastMessageText))
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.DisplayName} {targetUser.DisplayName} has never said anything worth mocking. Their git log is empty.",
                ctx.Message.Id);
            return;
        }

        string mockedText = ToMockingCase(lastMessageText);
        string intro = _mockIntros[Random.Shared.Next(_mockIntros.Length)]
            .Replace("{name}", targetUser.DisplayName);

        string fullText = $"{intro} \"{mockedText}\" End quote.";

        string processedIntro = await ctx.TtsService.ApplyUsernamePronunciationsAsync(intro);
        string processedMocked = await ctx.TtsService.ApplyUsernamePronunciationsAsync(mockedText);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(BOT_VOICE, processedIntro),
            TtsService.Segment(MOCK_VOICE, processedMocked),
            TtsService.Segment(BOT_VOICE, "End quote."),
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
            ctx.Message.Broadcaster.Username, fullText, ctx.Message.Id);
    }

    private static string ToMockingCase(string input)
    {
        StringBuilder sb = new(input.Length);
        bool upper = false;
        foreach (char c in input)
        {
            if (char.IsLetter(c))
            {
                sb.Append(upper ? char.ToUpper(c) : char.ToLower(c));
                upper = !upper;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

return new MockCommand();
