using System.Threading.Tasks;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class EditorCommand : IBotCommand
{
    public string Name => "editor";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx) { }

    public async Task Callback(CommandScriptContext ctx)
    {
        string text = "I use Rider, Webstorm, PHPStorm, Android Studio and VSCode!";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new EditorCommand();
