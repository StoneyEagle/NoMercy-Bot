using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;

/// <summary>
/// Voice Swap reward — Swap your TTS voice with another chatter's for 5 minutes.
/// Both users get each other's voice. Chaos ensues.
/// </summary>
public class VoiceSwapReward : IReward
{
    public Guid RewardId => Guid.Parse("a0aaddc9-36d7-4c30-bd39-5c1044e5f57d");
    public string RewardTitle => "Voice Swap";
    public RewardPermission Permission => RewardPermission.Everyone;
    public bool AutoCreate => true;
    public int Cost => 5000;
    public string Prompt => "Enter the username to swap TTS voices with for 5 minutes";
    public bool IsUserInputRequired => true;
    public string BackgroundColor => "#9B59B6";

    // Track active swaps: userId -> (originalVoiceId, partnerUserId, expiresAt)
    private static readonly ConcurrentDictionary<string, (string originalVoiceId, string partnerId, DateTime expiresAt)> _activeSwaps = new();

    private static readonly string[] _swapMessages =
    {
        "{redeemer} just swapped voices with {target}! For the next 5 minutes, they sound like each other. This can only end well.",
        "Voice swap activated! {redeemer} now sounds like {target} and vice versa. The identity crisis begins.",
        "{redeemer} and {target} have traded voices! Chat, try to keep up with who's who.",
        "The ol' switcheroo! {redeemer} and {target} swapped voices. Confusion is mandatory.",
    };

    public Task Init(RewardScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    public async Task Callback(RewardScriptContext ctx)
    {
        string? targetName = ctx.UserInput?.Trim().Replace("@", "").ToLower();

        if (string.IsNullOrEmpty(targetName))
        {
            await ctx.ReplyAsync($"@{ctx.UserDisplayName} You need to specify who to swap voices with! Type a username.");
            await ctx.RefundAsync();
            return;
        }

        // Can't swap with yourself
        if (string.Equals(targetName, ctx.UserLogin, StringComparison.OrdinalIgnoreCase))
        {
            await ctx.ReplyAsync($"@{ctx.UserDisplayName} You can't swap voices with yourself. That's just... your own voice.");
            await ctx.RefundAsync();
            return;
        }

        // Find the target user
        User? targetUser = await ctx.DatabaseContext.Users
            .AsNoTracking()
            .Where(u => u.Username == targetName)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        if (targetUser == null)
        {
            await ctx.ReplyAsync($"@{ctx.UserDisplayName} Can't find '{targetName}'. They've never been here!");
            await ctx.RefundAsync();
            return;
        }

        // Get both users' current voice preferences
        UserTtsVoice? redeemerVoice = await ctx.DatabaseContext.UserTtsVoices
            .Where(u => u.UserId == ctx.UserId)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        UserTtsVoice? targetVoice = await ctx.DatabaseContext.UserTtsVoices
            .Where(u => u.UserId == targetUser.Id)
            .FirstOrDefaultAsync(ctx.CancellationToken);

        string redeemerVoiceId = redeemerVoice?.TtsVoiceId ?? "";
        string targetVoiceId = targetVoice?.TtsVoiceId ?? "";

        // Store original voices for restoration
        _activeSwaps[ctx.UserId] = (redeemerVoiceId, targetUser.Id, DateTime.UtcNow.AddMinutes(5));
        _activeSwaps[targetUser.Id] = (targetVoiceId, ctx.UserId, DateTime.UtcNow.AddMinutes(5));

        // Swap the voices in the database
        if (redeemerVoice != null && !string.IsNullOrEmpty(targetVoiceId))
        {
            redeemerVoice.TtsVoiceId = targetVoiceId;
            redeemerVoice.SetAt = DateTime.UtcNow;
            ctx.DatabaseContext.UserTtsVoices.Update(redeemerVoice);
        }
        else if (!string.IsNullOrEmpty(targetVoiceId))
        {
            await ctx.DatabaseContext.UserTtsVoices.AddAsync(new()
            {
                UserId = ctx.UserId,
                TtsVoiceId = targetVoiceId,
                SetAt = DateTime.UtcNow
            }, ctx.CancellationToken);
        }

        if (targetVoice != null && !string.IsNullOrEmpty(redeemerVoiceId))
        {
            targetVoice.TtsVoiceId = redeemerVoiceId;
            targetVoice.SetAt = DateTime.UtcNow;
            ctx.DatabaseContext.UserTtsVoices.Update(targetVoice);
        }
        else if (!string.IsNullOrEmpty(redeemerVoiceId))
        {
            await ctx.DatabaseContext.UserTtsVoices.AddAsync(new()
            {
                UserId = targetUser.Id,
                TtsVoiceId = redeemerVoiceId,
                SetAt = DateTime.UtcNow
            }, ctx.CancellationToken);
        }

        await ctx.DatabaseContext.SaveChangesAsync(ctx.CancellationToken);

        // Announce the swap
        string msg = _swapMessages[Random.Shared.Next(_swapMessages.Length)]
            .Replace("{redeemer}", ctx.UserDisplayName)
            .Replace("{target}", targetUser.DisplayName);
        await ctx.TwitchChatService.SendMessageAsBot(ctx.BroadcasterLogin, msg);

        await ctx.FulfillAsync();

        // Schedule the swap reversal after 5 minutes
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            await RevertSwapAsync(ctx.UserId, targetUser.Id, redeemerVoiceId, targetVoiceId, ctx);
        });
    }

    private static async Task RevertSwapAsync(
        string userId1, string userId2,
        string originalVoice1, string originalVoice2,
        RewardScriptContext ctx)
    {
        try
        {
            // Only revert if the swap is still active (user hasn't manually changed voice)
            if (_activeSwaps.TryRemove(userId1, out var swap1) && swap1.partnerId == userId2)
            {
                using IServiceScope scope = ctx.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (!string.IsNullOrEmpty(originalVoice1))
                {
                    UserTtsVoice? voice1 = await db.UserTtsVoices.Where(u => u.UserId == userId1).FirstOrDefaultAsync();
                    if (voice1 != null)
                    {
                        voice1.TtsVoiceId = originalVoice1;
                        voice1.SetAt = DateTime.UtcNow;
                        db.UserTtsVoices.Update(voice1);
                    }
                }

                if (!string.IsNullOrEmpty(originalVoice2))
                {
                    UserTtsVoice? voice2 = await db.UserTtsVoices.Where(u => u.UserId == userId2).FirstOrDefaultAsync();
                    if (voice2 != null)
                    {
                        voice2.TtsVoiceId = originalVoice2;
                        voice2.SetAt = DateTime.UtcNow;
                        db.UserTtsVoices.Update(voice2);
                    }
                }

                await db.SaveChangesAsync();

                await ctx.TwitchChatService.SendMessageAsBot(ctx.BroadcasterLogin,
                    "Voice swap expired! Voices have been restored to their rightful owners.");
            }

            _activeSwaps.TryRemove(userId2, out _);
        }
        catch
        {
            // Silently fail — worst case users keep swapped voices until they manually change
        }
    }
}

return new VoiceSwapReward();
