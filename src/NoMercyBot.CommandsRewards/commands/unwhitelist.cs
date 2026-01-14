using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Scripting;

public class unWhitelistCommand: IBotCommand
{
    public string Name => "unwhitelist";
    public CommandPermission Permission => CommandPermission.Broadcaster;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
       
    }
}

return new unWhitelistCommand();