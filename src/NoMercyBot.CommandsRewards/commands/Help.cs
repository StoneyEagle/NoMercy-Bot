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
            string text = $"@{ctx.Message.User.DisplayName} Use !help <command> to get help for a specific command, or !commands to see what's available.";
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
            return;
        }

        string command = ctx.Arguments[0].ToLower().TrimStart('!');
        string helpText = command switch
        {
            "banger" => "!banger — Adds the currently playing song to the bangers playlist.",
            "bansong" => "!bansong [reason] — (Mod) Bans the current song from being requested again and skips it.",
            "commands" => "!commands — Lists all available commands for your permission level.",
            "discord" => "!discord — Shows the Discord invite link.",
            "editor" => "!editor — Shows which editors/IDEs are used on stream.",
            "followage" => "!followage — Shows how long you have been following the channel.",
            "help" => "!help <command> — Shows help info for a specific command.",
            "leaderboard" => "!leaderboard — Displays the top 3 users across various categories.",
            "lurk" => "!lurk — Marks you as lurking in chat.",
            "overlay" => "!overlay — Shows info about the stream overlay.",
            "playlist" => "!playlist — Gives you the Spotify link to the bangers playlist.",
            "project" => "!project — Describes the NoMercy TV project.",
            "raid" => "!raid <username> [delay] — (Broadcaster) Starts a raid to the specified channel. Optional delay in seconds (10-300, default 90).",
            "records" => "!records — Shows your personal stream redemption records.",
            "so" or "shoutout" => "!so <username> — (Mod) Gives a shoutout to the specified user.",
            "skip" => "!skip — (Mod) Skips the currently playing song.",
            "song" => "!song — Shows the current song playing on stream.",
            "theme" => "!theme — Shows the editor theme used on stream with links.",
            "todo" => "!todo add <text> | list | done <#> | remove <#> | clear — Manage your todo list. Broadcaster can append @user to manage other users' todos.",
            "unlurk" => "!unlurk — Marks you as no longer lurking.",
            "unwhitelist" => "!unwhitelist <username> — (Broadcaster) Revokes special abilities from a user.",
            "update" => "!update [@username] — Updates user info from Twitch. Mods/broadcaster can update other users.",
            "voice" => "!voice languages | get <lang> | set <voice> | current — Manage your TTS voice preference.",
            "volume" => "!volume [0-100] — (Mod) Gets or sets the music volume.",
            "whitelist" => "!whitelist <username> — (Broadcaster) Grants special abilities to a user.",
            _ => $"Unknown command \"{command}\". Use !commands to see what's available."
        };

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, $"@{ctx.Message.User.DisplayName} {helpText}", ctx.Message.Id);
    }
}

return new HelpCommand();
