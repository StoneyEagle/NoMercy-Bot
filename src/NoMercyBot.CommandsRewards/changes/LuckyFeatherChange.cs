using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;

public class LuckyFeatherChange : IRewardChangeHandler
{
    public Guid RewardId => Guid.Parse("29c1ea38-96ff-4548-9bbf-ec0b665344c0");
    public string RewardTitle => "Steal the Lucky Feather";
    public RewardPermission Permission => RewardPermission.Everyone;
    
    private const string STORAGE_KEY = "LuckyFeather";

    private static readonly string[] _perfectlyHiddenReplies =
    {
        "@{name}, you hid the Lucky Feather so well that even Google Maps gave up. You win.",
        "Congrats @{name}, the Lucky Feather is now lost to time, space, and {name}’s dignity.",
        "@{name} has hidden the Lucky Feather so effectively that archaeologists will find it in 3,000 years.",
        "Well played @{name}. The Lucky Feather is now in a place even the devs can’t locate.",
        "@{name}, you hid the Lucky Feather so deep it achieved enlightenment.",
        "Victory @{name}! The Lucky Feather is now more hidden than your motivation on Monday mornings.",
        "@{name} has concealed the Lucky Feather so perfectly that it legally counts as a disappearance.",
        "Impressive @{name}. The Lucky Feather is now in stealth mode and refuses to come out.",
        "@{name} hid the Lucky Feather so well that the rest of the server is coping and seething.",
        "Absolute masterclass @{name}. The Lucky Feather is now a myth, a legend, and definitely not findable.",
        "@{name}, you hid the Lucky Feather so flawlessly that future civilizations will assume it was never real.",
        "Legendary work @{name}. The Lucky Feather is now tucked away like a secret feature no one documented.",
        "@{name}, your hiding job was so perfect the feather now qualifies as an urban myth.",
        "Unbelievable @{name}. The Lucky Feather vanished with the confidence of a senior dev deleting legacy code.",
        "@{name}, you hid the Lucky Feather so well it just sent a postcard saying do not look for me.",
        "Peak performance @{name}. The Lucky Feather is now in a location only quantum physics could explain.",
        "@{name}, your hiding spot was so effective that even conspiracy theorists have given up.",
        "Incredible @{name}. The Lucky Feather is now more hidden than your browser’s forty seventh tab.",
        "@{name}, you hid the Lucky Feather with such precision that it triggered an existential crisis in the search algorithm.",
        "Outstanding @{name}. The Lucky Feather is now in a place so secret it auto deleted itself from memory.",
        "@{name}, your hiding technique was so perfect the feather now identifies as classified information.",
        "Bravo @{name}. The Lucky Feather is now tucked away like a feature flag no one remembers adding.",
        "@{name}, you hid the Lucky Feather so well that even the audit logs are confused.",
        "Exceptional @{name}. The Lucky Feather is now more hidden than your sleep schedule.",
        "@{name}, your hiding job was so immaculate the feather has been promoted to unrecoverable artifact.",
    };

    private static readonly string[] _featherAppearedReplies =
    {
        "Heads up. The Lucky Feather just reappeared. @{name} hid it like someone who thinks obfuscation means renaming a variable to x.",
        "The Lucky Feather has surfaced. @{name} hid it with all the stealth of a print statement in production.",
        "The Lucky Feather is out in the open. @{name}, your hiding job had more leaks than a junior dev’s first API.",
        "The Lucky Feather has been detected. @{name}, your hiding attempt was the software equivalent of works on my machine.",
        "The Lucky Feather has surfaced. @{name}’s hiding skills have officially been downgraded to tragic.",
        "Alert. The Lucky Feather is out in the open. @{name}, that hiding spot was bold. And by bold, I mean terrible.",
        "Heads up. The Lucky Feather just reappeared. @{name}, the feather escaped your hiding spot out of pure embarrassment.",
        "The Lucky Feather has resurfaced. @{name}, your hiding technique has been reported to the comedy department.",
        "The Lucky Feather has surfaced. Hunters, gremlins, and opportunists, @{name} clearly dropped the ball.",
        "Alert. The Lucky Feather just reappeared. Apparently @{name} used wishful thinking as a strategy.",
        "The Lucky Feather has returned to the open world. @{name}’s hiding skills have been revoked.",
        "Looks like @{name} hid the feather with the same strategy they use for losing their keys. It is already out again.",
        "Whatever @{name} called a hiding spot lasted about as long as a free trial.",
        "Breaking news. @{name}’s hiding attempt has been officially classified as decorative. The feather is visible.",
        "In a shocking twist, @{name}’s hiding job has failed faster than a Monday morning deploy.",
        "Well, @{name}’s hiding spot had the durability of a sandcastle at high tide. The feather is back.",
        "Turns out @{name} hid the feather with the confidence of someone who did not save their work.",
        "The feather escaped @{name}’s hiding spot like it had a meeting to get to.",
        "Apparently @{name} thought behind this thing counted as stealth. It did not.",
        "The feather just walked out of @{name}’s hiding place like it was not even locked.",
        "Whatever @{name} did to hide the feather, it had the stealth of a neon sign.",
        "The feather is back in the open. @{name} hid it like someone who believes out of sight means behind me.",
        "Turns out @{name}’s hiding technique was just wishful thinking wearing a trench coat.",
        "The feather bailed on @{name}’s hiding spot faster than a dev bails on a meeting that could have been an email.",
        "Apparently @{name} hid the feather using the hope and pray method. Shockingly, it did not work.",
        "The feather reappeared because @{name}’s hiding spot had the security of a sticky note password.",
        "Whatever @{name} tried, the feather rejected it like a bad merge request.",
        "The feather is visible again. @{name} hid it with the precision of a cat trying to bury a laser pointer.",
        "Turns out @{name}’s hiding spot was so bad the feather filed a complaint.",
        "The feather escaped @{name}’s hiding attempt like it was late for a dentist appointment.",
        "Apparently @{name} hid the feather using the same logic as if I cannot see it, no one can. Spoiler. We can.",
    };
    
    private static readonly string[] _featherEnabledReplies =
    {
        "The Lucky Feather is live again. {name}, you begin the stream as the reigning keeper. Enjoy it while it lasts.",
        "The Lucky Feather is active. {name}, you are starting the stream on top. Let us see how long that lasts.",
        "The Lucky Feather is back in play. {name}, you are the current keeper. Try not to lose it immediately.",
        "The Lucky Feather is enabled. {name}, you are still holding our most precious item. For now.",
        "The Lucky Feather is ready. {name}, you start the stream in possession of greatness. Good luck keeping it."
    };
    
    private static readonly string[] _featherDisabledReplies =
    {
        "The Lucky Feather is now resting. {name}, your reign continues only because the stream ended.",
        "The Lucky Feather is tucked away for now. {name}, enjoy the peace while you can. It will not last next stream.",
        "The Lucky Feather is disabled. {name}, you get to keep it for the night, but do not get too comfortable.",
        "The Lucky Feather is off for now. {name}, your victory is temporary and you know it.",
        "The Lucky Feather is safely stored. {name}, you survived this stream, but tomorrow is another story."
    };
    
    private static readonly string _hiddenDescriptionTemplate = "The Lucky Feather is currently hidden by {name}. Our most precious item deserved better, but here we are. Who will find it next? The price increases each time it's stolen!";

    private static readonly string _foundDescriptionTemplate =
        "The lucky feather is the most precious item in the stream! Steal it from {name} and hold onto it for as long as you can! But the price increases each time it's stolen!";

    public async Task Init(RewardChangeContext ctx)
    {
        
    }

    public async Task OnEnabled(RewardChangeContext ctx)
    {
        Record currentHolder = await GetCurrentHolder(ctx);
        string currentHolderName = currentHolder?.User?.DisplayName ?? ctx.BroadcasterLogin;
        
        string hiddenTemplate = _featherEnabledReplies[Random.Shared.Next(_featherEnabledReplies.Length)];
        string hiddenText = hiddenTemplate.Replace("{name}", currentHolderName);
        await ctx.ReplyAsync(hiddenText);
    }

    public async Task OnDisabled(RewardChangeContext ctx)
    {
        Record currentHolder = await GetCurrentHolder(ctx);
        string currentHolderName = currentHolder?.User?.DisplayName ?? ctx.BroadcasterLogin;
        
        string hiddenTemplate = _featherDisabledReplies[Random.Shared.Next(_featherDisabledReplies.Length)];
        string hiddenText = hiddenTemplate.Replace("{name}", currentHolderName);
        await ctx.ReplyAsync(hiddenText);
    }

    public async Task OnPauseStatusChanged(RewardChangeContext ctx)
    {
        if (!ctx.NewIsPaused.HasValue) return;

        Record currentHolder = await GetCurrentHolder(ctx);
        string currentHolderName = currentHolder?.User?.DisplayName ?? ctx.BroadcasterLogin;

        if (ctx.NewIsPaused.Value)
        {
            string hiddenTemplate = _perfectlyHiddenReplies[Random.Shared.Next(_perfectlyHiddenReplies.Length)];
            string hiddenText = hiddenTemplate.Replace("{name}", currentHolderName);
            await ctx.ReplyAsync(hiddenText);

            string hiddenPrompt = _hiddenDescriptionTemplate.Replace("{name}", currentHolderName);
            await ctx.TwitchApiService.UpdateCustomReward(
                ctx.BroadcasterId,
                ctx.RewardId,
                prompt: hiddenPrompt
            );
        }
        else
        {
            string appearedTemplate = _featherAppearedReplies[Random.Shared.Next(_featherAppearedReplies.Length)];
            string appearedText = appearedTemplate.Replace("{name}", currentHolderName);
            await ctx.ReplyAsync(appearedText);
        
            string foundPrompt = _foundDescriptionTemplate.Replace("{name}", currentHolderName);
            await ctx.TwitchApiService.UpdateCustomReward(
                ctx.BroadcasterId,
                ctx.RewardId,
                prompt: foundPrompt
            );
        }
    }
    
    public async Task OnResumeStatusChanged(RewardChangeContext ctx)
    {
        Logger.Twitch("Am i even doing anything here?");
        // Record currentHolder = await GetCurrentHolder(ctx);
        // string currentHolderName = currentHolder?.User?.DisplayName ?? ctx.BroadcasterLogin;
        //
        // string appearedTemplate = _featherAppearedReplies[Random.Shared.Next(_featherAppearedReplies.Length)];
        // string appearedText = appearedTemplate.Replace("{name}", currentHolderName);
        // await ctx.ReplyAsync(appearedText);
        //
        // string foundPrompt = _foundDescriptionTemplate.Replace("{name}", currentHolderName);
        // await ctx.TwitchApiService.UpdateCustomReward(
        //     ctx.BroadcasterId,
        //     ctx.RewardId,
        //     prompt: foundPrompt
        // );
    }
    
    private async Task<Record?> GetCurrentHolder(RewardChangeContext ctx)
    {
        return await ctx.DatabaseContext.Records
            .Where(r => r.RecordType == STORAGE_KEY)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
    }
    
    
    public Task OnPriceChanged(RewardChangeContext ctx)
    {
        return Task.CompletedTask;
    }
    public Task OnTitleChanged(RewardChangeContext ctx)
    {
        return Task.CompletedTask;
    }
    public Task OnDescriptionChanged(RewardChangeContext ctx)
    {
        return Task.CompletedTask;
    }
    public Task OnCooldownChanged(RewardChangeContext ctx)
    {
        return Task.CompletedTask;
    }
    public Task OnBackgroundColorChanged(RewardChangeContext ctx)
    {
        return Task.CompletedTask;
    }
    
}

return new LuckyFeatherChange();

