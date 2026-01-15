using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Twitch.Scripting;

public class RewardChangeContext
{
    public Channel? Channel { get; set; }
    public User? Broadcaster { get; set; }
    public string BroadcasterId { get; set; } = null!;
    public string BroadcasterLogin { get; set; } = null!;
    
    public Guid RewardId { get; set; }
    public string RewardTitle { get; set; } = null!;
    
    // Old values
    public string? OldTitle { get; set; }
    public string? OldDescription { get; set; }
    public int? OldCost { get; set; }
    public bool? OldIsEnabled { get; set; }
    public bool? OldIsPaused { get; set; }
    public string? OldBackgroundColor { get; set; }
    public int? OldCooldownExpiresAt { get; set; }
    
    // New values
    public string? NewTitle { get; set; }
    public string? NewDescription { get; set; }
    public int? NewCost { get; set; }
    public bool? NewIsEnabled { get; set; }
    public bool? NewIsPaused { get; set; }
    public string? NewBackgroundColor { get; set; }
    public int? NewCooldownExpiresAt { get; set; }
    
    // Detected change type
    public RewardChangeType? DetectedChangeType { get; set; }
    
    // Utilities
    public AppDbContext DatabaseContext { get; set; } = null!;
    public IServiceProvider ServiceProvider { get; set; } = null!;
    public TwitchChatService TwitchChatService { get; set; } = null!;
    public TwitchApiService TwitchApiService { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
    
    // Helper method to send a chat message
    public async Task ReplyAsync(string message)
    {
        await TwitchChatService.SendMessageAsBot(BroadcasterLogin, message);
    }
}

