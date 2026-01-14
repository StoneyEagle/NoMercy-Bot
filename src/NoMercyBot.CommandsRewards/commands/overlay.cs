using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class OverlayCommand: IBotCommand
{
    public string Name => "overlay";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        string text = "What started as a codepen from @jeroenvanwissen turned into full overlay. " +
                      "Now we are sharing code back and forth to improve it, so you may recognise it from his stream. " +
                      "You can find the code on GitHub: https://github.com/StoneyEagle/Stream-Overlay " +
                      "If you have any ideas or suggestions, feel free to tell us!";

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new OverlayCommand();