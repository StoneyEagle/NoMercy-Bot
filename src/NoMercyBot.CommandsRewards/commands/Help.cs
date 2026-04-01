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

    public Task Init(CommandScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        string broadcaster = ctx.Message.Broadcaster.Username;

        if (ctx.Arguments.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(broadcaster,
                "Use !help <command> to get help for a specific command, or !commands to see what's available.", ctx.Message.Id);
            return;
        }

        string command = ctx.Arguments[0].ToLower().TrimStart('!');
        string helpText = command switch
        {
            // Everyone
            "accountage" => "!accountage — Shows how old your Twitch account is.",
            "auction" => "!auction — Start or participate in a chat auction.",
            "banger" => "!banger — Adds the currently playing song to the bangers playlist.",
            "commands" => "!commands — Lists all available commands for your permission level.",
            "confess" => "!confess <text> — Make a dramatic confession in chat.",
            "detective" => "!detective <@user> — Investigate a user's chat activity.",
            "discord" => "!discord — Shows the Discord invite link.",
            "dramatic" => "!dramatic <text> — Delivers your message with dramatic flair.",
            "editor" => "!editor — Shows which editors/IDEs are used on stream.",
            "excuse" => "!excuse — Generates a random excuse.",
            "followage" => "!followage — Shows how long you have been following the channel.",
            "help" => "!help <command> — Shows help info for a specific command.",
            "hug" => "!hug <@user> — Give someone a hug. Results may vary.",
            "karen" => "!karen — Unleash your inner Karen on chat.",
            "leaderboard" => "!leaderboard — Displays the top 3 users across various categories.",
            "lurk" => "!lurk — Marks you as lurking in chat.",
            "mock" => "!mock <@user> — Mocks the last message of the specified user.",
            "narrator" => "!narrator <text> — Narrates your message like a nature documentary.",
            "overlay" => "!overlay — Shows info about the stream overlay.",
            "playlist" => "!playlist — Gives you the Spotify link to the bangers playlist.",
            "project" => "!project — Describes the NoMercy TV project.",
            "quote" => "!quote [add <text> | #number] — View or add stream quotes.",
            "ratio" => "!ratio <@user> — Try to ratio someone in chat.",
            "records" => "!records — Shows your personal stream redemption records.",
            "rigged" => "!rigged — Declare that something on stream is rigged.",
            "roast" => "!roast <@user> — Roast someone in chat.",
            "scam" => "!scam — Generate a fake scam message.",
            "slow" => "!slow <text> — Types out your message... very... slowly.",
            "song" => "!song — Shows the current song playing on stream.",
            "songhistory" => "!songhistory — Shows recently played songs.",
            "sr" => "!sr <spotify url or song name> — Request a song to be added to the queue.",
            "stats" => "!stats [@user] — Shows chat stats for yourself or another user.",
            "stoneyai" => "!stoneyai <question> — Ask the Stoney AI a question.",
            "sus" => "!sus <@user> — Call someone sus in chat.",
            "telsell" => "!telsell — Generate a fake infomercial pitch.",
            "todo" => "!todo add <text> | list | done <#> | remove <#> | clear — Manage your todo list.",
            "translate" => "!translate <text> — Translate text through multiple languages and back.",
            "trial" => "!trial <@user> — Put someone on trial in chat court.",
            "unlurk" => "!unlurk — Marks you as no longer lurking.",
            "update" => "!update [@user] — Updates user info from Twitch.",
            "voice" => "!voice languages | get <lang> | set <voice> | current — Manage your TTS voice.",
            "weather" => "!weather <location> — Shows the current weather for a location.",
            "whisper" => "!whisper <text> — Whispers your message dramatically.",
            "yell" => "!yell <text> — YELLS YOUR MESSAGE IN CHAT.",

            // Moderator
            "bansong" => "!bansong [reason] — (Mod) Bans the current song and skips it.",
            "so" or "shoutout" => "!so <username> — (Mod) Gives a shoutout to the specified user.",
            "skip" => "!skip — (Mod) Skips the currently playing song.",
            "volume" => "!volume [0-100] — (Mod) Gets or sets the music volume.",

            // Broadcaster
            "claude" => "!claude <prompt> — (Broadcaster) Ask Claude to make changes to the bot.",
            "raid" => "!raid <username> [delay] — (Broadcaster) Starts a raid.",
            "setpronoun" => "!setpronoun <@user> <pronoun> — (Broadcaster) Override a user's pronouns.",
            "whitelist" => "!whitelist <@user> <level> — (Broadcaster) Grants permission level to a user.",
            "unwhitelist" => "!unwhitelist <@user> — (Broadcaster) Revokes permission override from a user.",

            _ => $"Unknown command \"{command}\". Use !commands to see what's available."
        };

        await ctx.TwitchChatService.SendReplyAsBot(broadcaster, helpText, ctx.Message.Id);
    }
}

return new HelpCommand();
