using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Dto;
using NoMercyBot.Services.Twitch.Scripting;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

public class LuckyFeatherRecord
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public LuckyFeatherRecord()
    {
        
    }
}

public class LuckyFeatherReward : IReward
{
    public Guid RewardId => Guid.Parse("29c1ea38-96ff-4548-9bbf-ec0b665344c0");
    public string RewardTitle => "Lucky Feather";
    public RewardPermission Permission => RewardPermission.Everyone;
    
    private const string STORAGE_KEY = "LuckyFeatherHolder";

    private static readonly string[] _neverLostReplies =
    {
        "@{name}, congrats — you already swiped the Lucky Feather. Try pickpocketing a pigeon next time.",
        "Nice heist, @{name}, but the Lucky Feather is glued to your inventory.",
        "@{name}, the Lucky Feather is already doing a victory lap in your pocket.",
        "Hold up, @{name}: you can't pickpocket yourself unless you're into performance art.",
        "Hey @{name}, the Lucky Feather checked in and refused to check out.",
        "@{name}, the Lucky Feather filed a restraining order against leaving your side.",
        "Oopsie, @{name} — feather's already on your résumé under Possessions.",
        "@{name}, you already own the Lucky Feather; stop inventing new crimes.",
        "Nice try, @{name}. The Lucky Feather is on permanent loan to you.",
        "Sorry @{name}, self-theft isn't a valid game mechanic. Feather stays put."
    };
    
    private static readonly string[] _stolenSuccessfullyReplies =
    {
        "Nice work @{name}, you just liberated the Lucky Feather from {name2}. Consider yourself feathered.",
        "Bravo @{name}! The Lucky Feather now belongs to you; {name2} will file a dramatic complaint later.",
        "@{name}, you swiped the Lucky Feather from {name2} — add 'gentleman thief' to your bio.",
        "Hats off @{name}! You nabbed the Lucky Feather from {name2} and earned at least one smug grin.",
        "Kudos @{name}, you stole the Lucky Feather from {name2}. Please enjoy your temporary glory.",
        "@{name}, the Lucky Feather has a new owner: you. {name2} is accepting condolences.",
        "Well played @{name}! The Lucky Feather is yours; {name2} is now officially featherless.",
        "Cheers @{name}! You filched the Lucky Feather from {name2} — headline: 'Feather Heist Shocks Town.'",
        "@{name}, you pulled off a clean swipe of the Lucky Feather from {name2}. Oscars incoming.",
        "Congratulations @{name}, you executed the perfect heist on {name2} and claimed the Lucky Feather."
    };
    
    private static readonly string _titleTemplate = "Steal the Lucky Feather"; 
    private static readonly string _descriptionTemplate = "The lucky feather is the most precious item in the stream! Steal it from {name} and hold onto it for as long as you can! But the price increases each time it's stolen!"; 
    
    public async Task Init(RewardScriptContext ctx)
    {
        // Initialize storage if it doesn't exist
        Storage? storage = await ctx.DatabaseContext.Storages
            .FirstOrDefaultAsync(s => s.Key == STORAGE_KEY);
        
        if (storage == null)
        {
            storage = new()
            {
                Key = STORAGE_KEY,
                Value = new LuckyFeatherRecord().ToJson()
            };
            
            await ctx.DatabaseContext.Storages.AddAsync(storage);
            await ctx.DatabaseContext.SaveChangesAsync();
        }
    }

    public async Task Callback(RewardScriptContext ctx)
    {
        try
        {
            // Get current holder
            LuckyFeatherRecord previousHolder = await GetCurrentHolder(ctx);
            string previousHolderId = previousHolder.UserId;
            string previousHolderName = previousHolder.DisplayName;

            // Check if user is trying to steal from themselves
            if (ctx.UserId == previousHolderId)
            {
                string neverLostTemplate = _neverLostReplies[Random.Shared.Next(_neverLostReplies.Length)];
                string neverLostText = TemplateHelper.ReplaceTemplatePlaceholders(neverLostTemplate, ctx);
                await ctx.ReplyAsync(neverLostText);
                return;
            }
            
            // Update user song request tracking
            await SetCurrentHolder(ctx, ctx.UserId, ctx.UserDisplayName);
            
            // Update reward details
            await UpdateReward(ctx, previousHolderName, ctx.UserDisplayName);
            
            // Reply success message
            string successTemplate = _stolenSuccessfullyReplies[Random.Shared.Next(_stolenSuccessfullyReplies.Length)];
            string successText = TemplateHelper.ReplaceTemplatePlaceholders(successTemplate, ctx);
            successText = Regex.Replace(successText, @"\{name2\}", previousHolderName, RegexOptions.IgnoreCase);
            await ctx.ReplyAsync(successText);

        } catch (Exception ex)
        {
            string exceptionText = $"@{ctx.UserDisplayName} An error occurred while processing your reward: {ex.Message}";
            await ctx.ReplyAsync(exceptionText);
            await ctx.RefundAsync();
        }
    }

    private async Task<LuckyFeatherRecord> GetCurrentHolder(RewardScriptContext ctx)
    {
        return await ctx.DatabaseContext.Storages
            .Where(r => r.Key == STORAGE_KEY)
            .Select(r => r.Value.FromJson<LuckyFeatherRecord>())
            .FirstOrDefaultAsync() ?? new LuckyFeatherRecord();
    }

    private async Task SetCurrentHolder(RewardScriptContext ctx, string userId, string displayName)
    {
        LuckyFeatherRecord luckyFeatherRecord = await ctx.DatabaseContext.Storages
            .Where(r => r.Key == STORAGE_KEY)
            .Select(r => r.Value.FromJson<LuckyFeatherRecord>())
            .FirstOrDefaultAsync() ?? new LuckyFeatherRecord();
        
        luckyFeatherRecord.UserId = userId;
        luckyFeatherRecord.DisplayName = displayName;
        luckyFeatherRecord.Date = DateTime.UtcNow;
        
        Storage? storage = await ctx.DatabaseContext.Storages
            .FirstOrDefaultAsync(s => s.Key == STORAGE_KEY);

        if (storage != null)
        {
            storage.Value = luckyFeatherRecord.ToJson();
            ctx.DatabaseContext.Storages.Update(storage);
            await ctx.DatabaseContext.SaveChangesAsync();
        }
    }
    
    private async Task UpdateReward(RewardScriptContext ctx, string previousHolder, string currentHolder)
    {
        ChannelPointsCustomRewardsResponse rewards = await ctx.TwitchApiService.GetCustomRewards(ctx.BroadcasterId, ctx.RewardId);
        ChannelPointsCustomRewardsResponseData reward = rewards.Data.First();

        // Increase cost by 1
        int newCost = reward.Cost + 1;

        // Update title and description
        string newTitle = TemplateHelper.ReplaceTemplatePlaceholders(_titleTemplate, ctx);
        string prompt = TemplateHelper.ReplaceTemplatePlaceholders(_descriptionTemplate, ctx);
        prompt = Regex.Replace(prompt, @"\{name\}", currentHolder, RegexOptions.IgnoreCase);
        
        await ctx.TwitchApiService.UpdateCustomReward(
            ctx.BroadcasterId,
            ctx.RewardId,
            newTitle,
            newCost,
            prompt
        );
    }
}

return new LuckyFeatherReward();