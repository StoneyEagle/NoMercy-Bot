using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Twitch.Scripting;

public class RewardScriptContext
{
    public Channel Channel { get; init; } = null!;
    public string BroadcasterLogin { get; init; } = null!;
    public string BroadcasterId { get; init; } = null!;
    public Guid RewardId { get; init; } = Guid.Empty;
    public string RewardTitle { get; init; } = null!;
    public string RedemptionId { get; init; } = null!;
    public User User { get; init; } = null!;
    public string UserId { get; init; } = null!;
    public string UserLogin { get; init; } = null!;
    public string UserDisplayName { get; init; } = null!;
    public string? UserInput { get; init; }
    public int Cost { get; init; }
    public string Status { get; init; } = null!;
    public DateTimeOffset RedeemedAt { get; init; }
    public Func<string, Task> ReplyAsync { get; init; } = null!;
    public Func<Task> RefundAsync { get; init; } = null!;
    public Func<Task> FulfillAsync { get; init; } = null!;
    public required AppDbContext DatabaseContext { get; init; } = null!;
    public required TwitchChatService TwitchChatService { get; init; }
    public required TwitchApiService TwitchApiService { get; set; } = null!;
    public required IServiceProvider ServiceProvider { get; init; }
    public CancellationToken CancellationToken { get; set; }
}
