using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Database.Models.ChatMessage;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using Serilog.Events;
using System.Threading;

public class ShoutoutCommand: IBotCommand
{
    public string Name => "so";
    public CommandPermission Permission => CommandPermission.Moderator;

    private static readonly string[] SnarkyShoutoutReplies =
    {
        "Check out {displayname}! {Subject} has some great {game} content. Go give {object} a follow! {Subject} {tense} practically a pro, or at least {Subject} play one on Twitch.",
        "Yo, peep this! {displayname} {tense} rocking some {game} stuff. Go give {object} a follow! {Subject} {tense} so good, it's almost annoying.",
        "Attention, earthlings! {displayname} has {game} videos you need to see. Go give {object} a follow! {Subject} {tense} probably putting on a masterclass, or a clown show – either way, it's entertaining.",
        "Incoming awesome! {displayname} has some {game} action for you. Go give {object} a follow! {Subject} {tense} crushing it, or at least {Subject} looks like {Subject} is.",
        "Don't walk, run! {displayname} has more {game} than you can handle. Go give {object} a follow! {Subject} {tense} definitely worth interrupting your snack for.",
        "Our resident legend, {displayname}, has awesome {game}! Go give {object} a follow! {Subject} {tense} probably about to pull off something epic, or face-plant gloriously.",
        "Heads up, buttercups! {displayname} has some {game} for you. Go give {object} a follow! {Subject} {tense} proving once again that {Subject} {tense} awesome (don't tell {object} I said that).",
        "Guess who's got content? {displayname}! {Subject} {tense} rocking {game}. Go give {object} a follow! {Subject} {tense} bringing the vibes, whether {Subject} likes it or not.",
        "Behold! {displayname} has some solid {game} for you. Go give {object} a follow! {Subject} {tense} gracing us with {object} presence and questionable decision-making in {game}."
    };

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"@{ctx.Message.User.DisplayName} You need to specify a user to shoutout!", ctx.Message.Id);
            return;
        }
        
        string name = ctx.Arguments[0].Replace("@", "").ToLower();

        try
        {
            User user = await ctx.TwitchApiService.GetOrFetchUser(name: name);
            Channel? channel = await ctx.TwitchApiService.GetOrFetchChannel(id: user.Id);
            ChannelInfo channelInfo = await ctx.TwitchApiService.GetOrFetchChannelInfo(id: user.Id);
            
            string gameName = "Something awesome";
            string title = "";
            bool isLive = false;

            gameName = channelInfo.GameName ?? "something awesome";
            title = channelInfo.Title ?? "";
            isLive = channelInfo.IsLive;

            // Create modified context for template replacement
            CommandScriptContext modifiedCtx = new CommandScriptContext
            {
                Message = new()
                {
                    UserId = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    User = user,
                },
                Channel = ctx.Message.Broadcaster.Username,
                BroadcasterId = ctx.BroadcasterId,
                CommandName = ctx.CommandName,
                Arguments = ctx.Arguments,
                ReplyAsync = ctx.ReplyAsync,
                CancellationToken = ctx.CancellationToken,
                ServiceProvider = ctx.ServiceProvider,
                TwitchChatService = ctx.TwitchChatService,
                TtsService = ctx.TtsService,
                TwitchApiService = ctx.TwitchApiService,
                DatabaseContext = ctx.DatabaseContext, 
            };

            string randomTemplate = channel?.ShoutoutTemplate ?? SnarkyShoutoutReplies[Random.Shared.Next(SnarkyShoutoutReplies.Length)];
            string text = TemplateHelper.ReplaceTemplatePlaceholders(randomTemplate, modifiedCtx, isLive, gameName, title);

            try
            {
                await ctx.TwitchApiService.SendShoutoutAsync(
                    ctx.Message.BroadcasterId, 
                    ctx.Message.BroadcasterId,
                    user.Id);
            }
            catch (Exception e)
            {
                // Silently handle API errors - announcement was already sent
                Logger.Twitch($"Failed to send shoutout for user {user.Username}: {e.Message}", LogEventLevel.Error);
            }
            
            try
            {
                await ctx.TwitchApiService.SendAnnouncement(
                    ctx.Message.BroadcasterId, 
                    ctx.Message.BroadcasterId,
                    text);
                
                await ctx.TtsService.SendCachedTts(text, user.Id, new());
            }
            catch (Exception e)
            {
                Logger.Twitch($"Failed to send announcement for shoutout: {e.Message}", LogEventLevel.Error);
            }

        }
        catch (Exception ex)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"@{ctx.Message.DisplayName} An error occurred while processing the shoutout.", ctx.Message.Id);
        }
    }
}

return new ShoutoutCommand();