using System.Threading.Tasks;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class DiscordCommand : IBotCommand
{
    public string Name => "discord";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx) { }

    public async Task Callback(CommandScriptContext ctx)
    {
        string text = "Join our Discord community! https://discord.gg/dHHvnvFsXR";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new DiscordCommand();
