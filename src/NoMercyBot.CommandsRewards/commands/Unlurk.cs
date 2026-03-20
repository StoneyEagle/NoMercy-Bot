using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database.Models;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Globals.NewtonSoftConverters;

public class UnlurkCommand: IBotCommand
{ 
   public string Name =>  "unlurk";
   public CommandPermission Permission => CommandPermission.Everyone;

   private static Storage? _lurkerStorage;

   private static readonly string[] _snarkyReplies =
   {
       "Look alive, everyone! {name} has emerged from the lurk zone. We almost forgot about you!",
       "Well, well, well, if it isn't {name}, gracing us with their active presence once more. Did you miss us?",
       "The legend, {name}, has returned! We've been holding your pixelated seat. Don't touch anything, it's still warm.",
       "Breaking news: {name} has successfully completed their top-secret lurker mission. Welcome back to the land of the living!",
       "{name} has bravely decided to rejoin the chaos. We're shocked, honestly. What took you so long?",
       "Hark! Is that... chatter? It is! {name} has officially unlurked. Your silence was deafening, just sayin'.",
       "Welcome back, {name}! We hope your lurking was productive. Now get to work, there's chat to be had!",
       "{name} has resurfaced! Did you bring snacks from the shadows? No? Aw, well, welcome back anyway.",
       "The prophecy is true! {name} has shed their lurker skin. Prepare for... well, whatever you do when you're not lurking.",
       "It's true, {name} is no longer a ghost in the machine. Your keyboard must be so lonely no more. Welcome back!",
       "Hold the phone! {name} has decided to grace us with their voice again. We thought you'd joined a silent monastery!",
       "A wild {name} appeared! {Subject} used !unlurk. It was super effective. Welcome back, we guess.",
       "Did anyone else hear that? Oh, it's just {name} finally rejoining the chat. The lurk spell has been broken!",
       "Look what the cat dragged in! It's {name}, back from the digital wilderness. Don't worry, we saved you some crumbs.",
       "Well, hello there, {name}! Decided to abandon your lurking duties, have we? Good to see your pixels again.",
       "{name} has respawned! The lurk cooldown has expired. Quick, someone give them a recap of everything they missed!",
       "The eagle has landed! {name} swoops back from the lurk nest. Big Bird approves of your return.",
       "Oh great, {name} is back. There goes the peaceful chat we were enjoying. Just kidding... or am I?",
       "{name} just did a `git stash pop` on their chat presence. Welcome back to the merge conflicts of conversation!",
       "Alert: {name} has exited headless mode. UI restored, keyboard engaged. The lurk process has been terminated."
   };

   private static readonly string[] _notLurkingUnlurkReplies =
   {
       "{name}, you can't unlurk if you weren't even lurking! Were you trying to escape from something else?",
       "Did we miss something, {name}? You just tried to unlurk, but we didn't even know you were gone. What's your secret?",
       "{name} just used unlurk. My dude, you were never in the lurk zone to begin with! Are you okay?",
       "Hold on, {name}. unlurk? Were you secretly lurking under a rock this whole time? We definitely saw you chatting!",
       "Is this a magic trick, {name}? You can't unlurk from a state you weren't in! Stay hydrated, buddy.",
       "{name} is trying to reverse lurk. Fascinating. But you haven't been lurking! Get back to your active chat duties!",
       "Uh, {name}? Your unlurk command seems to have glitched. You've been here the whole time! Try again when you actually vanish.",
       "Welcome back from... not lurking, {name}! We're thrilled you're still here, even if your command is confused.",
       "{name} just attempted an unlurk. Newsflash: You've been chatting away! No escape for you!",
       "My systems indicate {name} has always been here. No need to unlurk from the land of the actively chatting. What gives?",
       "Woah, weren't you hanging out int he shadows? Do you want to take part again?",
       "{name} just tried to return from a journey they never took. You've been here the whole time, you absolute goober.",
       "Nice try, {name}. You can't dramatically return if you never dramatically left. That's just... standing here.",
       "{name} used !unlurk but it had no effect. You weren't lurking! Were you trying to feel important? Because it worked.",
       "Error 403: {name} cannot unlurk. Reason: was never lurking. Have you tried turning yourself off and on again?",
       "Big Bird is confused. {name} is trying to come back, but they never left the nest. Are you feeling okay?",
       "{name}, that's like trying to `git pull` when you're already up to date. Nothing to fetch here, chief.",
       "Plot twist: {name} was never lurking. The real lurk was the friends we made along the way. Now sit down.",
       "Ah yes, {name}, the classic unlurk-without-lurking maneuver. Bold strategy. Doesn't work, but bold.",
       "{name} is out here unlurking from an alternate timeline where they actually lurked. In this reality? You've been chatting, fam."
   };

   public async Task Init(CommandScriptContext ctx)
   {
       _lurkerStorage = await ctx.DatabaseContext.Storages
           .Where(p => p.Key == "LurkersList")
           .FirstOrDefaultAsync(ctx.CancellationToken);
        
       if (_lurkerStorage == null)
       {
           Storage newLurkerStorage = new Storage
           {
               Key = "LurkersList",
               Value = "[]"
           };
            
           ctx.DatabaseContext.Storages.Add(newLurkerStorage);
           await ctx.DatabaseContext.SaveChangesAsync();
       }
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

           if (!lurkers.Contains(username))
           {
               string randomTemplate = _notLurkingUnlurkReplies[Random.Shared.Next(_notLurkingUnlurkReplies.Length)];
               string text = TemplateHelper.ReplaceTemplatePlaceholders(randomTemplate, ctx);
               await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
               await ctx.TtsService.SendCachedTts(text, ctx.Message.Broadcaster.Id, new());
               return;
           }

           string randomUnlurkTemplate = _snarkyReplies[Random.Shared.Next(_snarkyReplies.Length)];
           string unlurkText = TemplateHelper.ReplaceTemplatePlaceholders(randomUnlurkTemplate, ctx);
           await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, unlurkText, ctx.Message.Id);
           await ctx.TtsService.SendCachedTts(unlurkText, ctx.Message.Broadcaster.Id, new());
           
           lurkers.Remove(username);
           
           _lurkers.Value = lurkers.ToJson();
           ctx.DatabaseContext.Storages.Update(_lurkers);
           await ctx.DatabaseContext.SaveChangesAsync();
       }
       catch (Exception ex)
       {
           await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"Something went wrong with the unlurk command. {ex.Message}", ctx.Message.Id);
       }
   }
}

return new UnlurkCommand();