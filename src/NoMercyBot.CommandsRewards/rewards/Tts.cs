using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Widgets;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;

public class TtsRecord
{
    public string Message { get; set; } = null!;
}

public class TtsReward : IReward
{
    public Guid RewardId => Guid.Parse("e8168189-8d2c-41fb-b8f4-2785b083a35e");
    public string RewardTitle => "Text-to-Speech Message";
    public RewardPermission Permission => RewardPermission.Everyone;
    
    private static readonly string[] _noTtsSubscriptionsReplies =
    {
        "@{name}, sorry but TTS is not currently available! Your points have been refunded.",
        "Oops @{name}! TTS widgets aren't active right now. Points refunded!",
        "@{name}, no one's listening to TTS right now! Your points are safe with you.",
        "TTS is taking a break @{name}! Don't worry, your points have been returned.",
        "@{name}, looks like TTS is offline! Your channel points have been graciously refunded."
    };

    private static readonly string[] _emptyMessageReplies =
    {
        "@{name}, you forgot to include your message! Points refunded - use them wisely next time!",
        "Hey @{name}! TTS needs words to work its magic. Your points are back!",
        "@{name}, silent treatment much? Add a message next time! Points refunded.",
        "Plot twist @{name}: TTS can't read minds! Include your message and try again. Points saved!",
        "@{name}, your message was as empty as my soul! But at least your points are refunded."
    };
    
    private const string STORAGE_KEY = "TTS";

    public async Task Init(RewardScriptContext ctx)
    {
        
    }

    public async Task Callback(RewardScriptContext ctx)
    {
        string? userInput = ctx.UserInput?.Trim();
        
        if (string.IsNullOrEmpty(userInput)) 
        {
            string randomTemplate = _emptyMessageReplies[Random.Shared.Next(_emptyMessageReplies.Length)];
            string text = TemplateHelper.ReplaceTemplatePlaceholders(randomTemplate, ctx);
            await ctx.ReplyAsync(text);
            await ctx.RefundAsync();
            return;
        }

        try
        {
            // Check if any widgets are subscribed to TTS events
            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            bool hasWidgetSubscriptions = await widgetEventService.HasWidgetSubscriptionsAsync("channel.chat.message.tts");
            
            if (!hasWidgetSubscriptions)
            {
                string randomTemplate = _noTtsSubscriptionsReplies[Random.Shared.Next(_noTtsSubscriptionsReplies.Length)];
                string text = TemplateHelper.ReplaceTemplatePlaceholders(randomTemplate, ctx);
                await ctx.ReplyAsync(text);
                await ctx.RefundAsync();
                return;
            }

            // Update user TTS request tracking
            await StoreRecordAsync(ctx, userInput);

            // Send TTS using the TTS service
            TtsService ttsService = ctx.ServiceProvider.GetRequiredService<TtsService>();
            TtsUsageRecord? usageRecord = await ttsService.SendCachedTts(userInput, ctx.UserId, ctx.CancellationToken);

            if (usageRecord != null)
            {
                await ctx.FulfillAsync();
            }
            else
            {
                await ctx.ReplyAsync($"@{ctx.UserDisplayName} TTS request failed. Points refunded.");
                await ctx.RefundAsync();
            }
        }
        catch (Exception ex)
        {
            await ctx.ReplyAsync($"@{ctx.UserDisplayName} TTS error: {ex.Message}. Points refunded.");
            await ctx.RefundAsync();
        }
    }

    private async Task StoreRecordAsync(RewardScriptContext ctx, string message)
    {
        TtsRecord newTtsRecord = new()
        {
            Message = message
        };
        
        Record record = new()
        {
            UserId = ctx.UserId,
            RecordType = STORAGE_KEY,
            Data = newTtsRecord.ToJson(),
        };
            
        ctx.DatabaseContext.Records.Add(record);
        await ctx.DatabaseContext.SaveChangesAsync();
    }
}

return new TtsReward();
