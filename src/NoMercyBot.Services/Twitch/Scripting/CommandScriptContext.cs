using NoMercyBot.Database;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Services.Other;

namespace NoMercyBot.Services.Twitch.Scripting;

public class CommandScriptContext
{
    public string Channel { get; init; }
    public string BroadcasterId { get; init; }
    public string CommandName { get; init; }
    public string[] Arguments { get; init; }
    public ChatMessage Message { get; init; }
    public Func<string, Task> ReplyAsync { get; init; }
    public required AppDbContext DatabaseContext { get; init; } = null!;
    public required TwitchChatService TwitchChatService { get; init; }
    public required TwitchApiService TwitchApiService { get; set; } = null!;
    public required IServiceProvider ServiceProvider { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public TtsService TtsService { get; set; }
}
