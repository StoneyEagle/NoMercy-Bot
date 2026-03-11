using System.Threading.Tasks;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class ThemeCommand : IBotCommand
{
    public string Name => "theme";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx) { }

    public async Task Callback(CommandScriptContext ctx)
    {
        string text = "I use the One Dark theme! JetBrains: https://plugins.jetbrains.com/plugin/11938-one-dark-theme | VSCode: https://marketplace.visualstudio.com/items?itemName=zhuangtongfa.Material-theme";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new ThemeCommand();
