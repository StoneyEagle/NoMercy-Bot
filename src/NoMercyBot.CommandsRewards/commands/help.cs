using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class HelpCommand: IBotCommand
{
    public string Name => "help";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            string errorText = $"@{ctx.Message.User.DisplayName} Invalid usage of the help command. Use !commands to see what commands are available for you.";
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, errorText, ctx.Message.Id);
            return;
        }

        string command = ctx.Arguments[0].ToLower();
        string helpText = command switch
        {
            "banger" => "!banger will add the current song to the banger list.",
            "command" => "!command will list all available commands for your permission level.",
            "commands" => "!commands will list all available commands for your permission level.",
            "followage" => "!followage will show how long you have been following the channel.",
            "overlay" => "!overlay will tell you about it.",
            "playlist" => "!playlist will give you the spotify link to the bangers playlist.",
            "records" => "!records will show your personal records of stream redemptions.",
            "shoutout" => "!shoutout <username> will give a shoutout to the specified user.",
            "skip" => "!skip will skip the current song playing on stream.",
            "song" => "!song will show the current song playing on stream.",
            "unwhitelist" => "!unwhitelist <username> will revoke special abilities.",
            "whitelist" => "!whitelist <username> will give special abilities to the specified user.",
            "volume" => "!volume <0-100> will set the volume of the music in the stream.",
            _ => "This command does not exist, use !commands to see what commands are available to you."
        };

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"@{ctx.Message.User.DisplayName} {helpText}", ctx.Message.Id);
    }
}

return new HelpCommand();