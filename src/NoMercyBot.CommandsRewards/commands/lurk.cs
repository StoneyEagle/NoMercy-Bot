using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database.Models;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Globals.NewtonSoftConverters;

public class LurkCommand: IBotCommand
{
    public string Name => "lurk";
    public CommandPermission Permission => CommandPermission.Everyone;

    private static Storage? _lurkerStorage;

    private static readonly string[] _snarkyLurkReplies = 
    {
        "Oh, {name} is going to !lurk? Don't strain yourself with all that... not chatting. We'll try to have fun without you.",
        "{name} has bravely entered the lurk zone. We'll miss your... well, you know. Your active participation. Maybe.",
        "Another one bites the dust! {name} is now a professional lurker. Enjoy your silent observations!",
        "Farewell, {name}! May your lurk be ever watchful and your keyboard ever silent. We'll save you a pixelated seat.",
        "{name} is off to their top-secret lurking mission. Don't worry, we'll try not to have *too* much fun without you.",
        "And just like that, {name} vanishes into the shadows of !lurk. Try not to get too comfortable back there!",
        "It's true, {name} is now officially in stealth mode. We appreciate your dedication to... being here, but not really.",
        "Well, look at {name}, pulling a Houdini with the !lurk command. Don't forget to blink once in a while!",
        "Lurk initiated for {name}. We'll assume you're busy with super important lurker business. Don't mind us!",
        "Confirmed: {name} has successfully executed !lurk. Your silence is now deafening. Just kidding... mostly."
    };

    private static readonly string[] _alreadyLurkingReplies = 
    {
        "{name}, you're trying to lurk again? We thought you were already in the shadows! Did you forget to bring snacks?",
        "Wait, {name}, you're still here? And trying to lurk? Get lost! (Just kidding... mostly).",
        "{name} is attempting to lurk... *again*. Didn't you already have your vanishing act? Go on, shoo!",
        "Are you new to this, {name}? You're already lurking! The 'disappear' button only works once. Now scram!",
        "Uh, {name}? You just tried to lurk, but you've been a ghost for ages. Did you briefly consider rejoining chat?",
        "{name}, you can't lurk if you're already successfully lurking. Get back to your silent duties!",
        "Is this a joke, {name}? You're already lurking. Don't make me send you to the *super* lurk zone.",
        "Someone tell {name} the lurk command isn't a continuous loop. We already wrote you off! (In a loving way, of course).",
        "{name} is trying to double-lurk. Impressive, but unnecessary. You're already invisible to us!",
        "Ah, {name}, back for round two of lurking? You never truly left our hearts... or our 'currently lurking' list."
    };

    public async Task Init(CommandScriptContext ctx)
    {
        _lurkerStorage = await ctx.DatabaseContext.Storages
            .Where(p => p.Key == "LurkersList")
            .FirstOrDefaultAsync(ctx.CancellationToken);
        
        Storage newLurkerStorage = new Storage
        {
            Key = "LurkersList",
            Value = "[]"
        };
        
        ctx.DatabaseContext.Storages.Upsert(newLurkerStorage)
            .On(c => c.Key)
            .WhenMatched((oldConfig, newConfig) => new()
            {
                Value = newConfig.Value
            })
            .RunAsync(ctx.CancellationToken);
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        Storage _lurkers = await ctx.DatabaseContext.Storages
            .Where(p => p.Key == "LurkersList")
            .FirstOrDefaultAsync(ctx.CancellationToken);
        
        List<string> lurkers = _lurkers != null 
            ? _lurkers.Value.FromJson<List<string>>() ?? new List<string>() 
            : new();
        
        try
        {
            string username = ctx.Message.User.Username;

            if (lurkers.Contains(username))
            {
                string randomTemplate = _alreadyLurkingReplies[Random.Shared.Next(_alreadyLurkingReplies.Length)];
                string text = TemplateHelper.ReplaceTemplatePlaceholders(randomTemplate, ctx);
                await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
                await ctx.TtsService.SendCachedTts(text, ctx.Message.Broadcaster.Id, new());
                return;
            }

            string randomLurkTemplate = _snarkyLurkReplies[Random.Shared.Next(_snarkyLurkReplies.Length)];
            string lurkText = TemplateHelper.ReplaceTemplatePlaceholders(randomLurkTemplate, ctx);
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, lurkText, ctx.Message.Id);
            await ctx.TtsService.SendCachedTts(lurkText, ctx.Message.Broadcaster.Id, new());
            
            lurkers.Add(username);
            
            _lurkers.Value = lurkers.ToJson();
            ctx.DatabaseContext.Storages.Update(_lurkers);
            await ctx.DatabaseContext.SaveChangesAsync();

        }
        catch (Exception ex)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"Something went wrong with the lurk command. {ex.Message}", ctx.Message.Id);
        }
    }
}

return new LurkCommand();