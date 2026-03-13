using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class SetPronounCommand : IBotCommand
{
    public string Name => "setpronoun";
    public CommandPermission Permission => CommandPermission.Broadcaster;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        string broadcaster = ctx.Message.Broadcaster.Username;

        if (ctx.Arguments.Length < 2)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                broadcaster,
                $"@{ctx.Message.DisplayName} Usage: !setpronoun <username> <pronoun> — e.g. !setpronoun someone he/him, she/her, they/them, or clear",
                ctx.Message.Id);
            return;
        }

        string targetName = ctx.Arguments[0].Replace("@", "").ToLower();
        string pronounArg = ctx.Arguments[1].ToLower();

        try
        {
            User user = await ctx.TwitchApiService.GetOrFetchUser(name: targetName);

            if (pronounArg == "clear" || pronounArg == "reset")
            {
                user.Pronoun = null;
                user.PronounManualOverride = false;

                await ctx.DatabaseContext.Users
                    .Where(u => u.Id == user.Id)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(x => x.Pronoun, (Pronoun?)null)
                        .SetProperty(x => x.PronounManualOverride, false));

                await ctx.TwitchChatService.SendReplyAsBot(
                    broadcaster,
                    $"@{ctx.Message.DisplayName} Cleared pronoun override for {user.DisplayName}. Will use their alejo.io setting next time.",
                    ctx.Message.Id);
                return;
            }

            Pronoun pronoun = await ctx.DatabaseContext.Pronouns
                .FirstOrDefaultAsync(p => p.Name.ToLower() == pronounArg);

            if (pronoun == null)
            {
                string available = string.Join(", ",
                    await ctx.DatabaseContext.Pronouns.Select(p => p.Name).ToListAsync());
                await ctx.TwitchChatService.SendReplyAsBot(
                    broadcaster,
                    $"@{ctx.Message.DisplayName} Unknown pronoun '{pronounArg}'. Available: {available}",
                    ctx.Message.Id);
                return;
            }

            user.Pronoun = pronoun;
            user.PronounManualOverride = true;

            await ctx.DatabaseContext.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(x => x.Pronoun, pronoun)
                    .SetProperty(x => x.PronounManualOverride, true));

            await ctx.TwitchChatService.SendReplyAsBot(
                broadcaster,
                $"@{ctx.Message.DisplayName} Set pronouns for {user.DisplayName} to {pronoun.Name} ({pronoun.Subject}/{pronoun.Object}).",
                ctx.Message.Id);
        }
        catch (Exception ex)
        {
            await ctx.TwitchChatService.SendReplyAsBot(
                broadcaster,
                $"@{ctx.Message.DisplayName} Could not find user '{targetName}'.",
                ctx.Message.Id);
        }
    }
}

return new SetPronounCommand();
