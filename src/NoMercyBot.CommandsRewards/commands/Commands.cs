using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;

public class CommandsCommand : IBotCommand
{
    public string Name => "commands";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx) { }

    public async Task Callback(CommandScriptContext ctx)
    {
        TwitchCommandService commandService = (TwitchCommandService)ctx.ServiceProvider
            .GetService(typeof(TwitchCommandService));
        PermissionService permissionService = (PermissionService)ctx.ServiceProvider
            .GetService(typeof(PermissionService));

        string userType = ctx.Message.UserType;

        List<string> available = commandService.ListCommands()
            .Where(c => permissionService.HasMinLevel(userType, c.Permission.ToString().ToLowerInvariant()))
            .Select(c => $"!{c.Name}")
            .OrderBy(c => c)
            .ToList();

        if (available.Count == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                $"@{ctx.Message.User.DisplayName} No commands available.", ctx.Message.Id);
            return;
        }

        string text = $"@{ctx.Message.User.DisplayName} Available commands ({available.Count}): {string.Join(", ", available)} — Use !help <command> for details.";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new CommandsCommand();
