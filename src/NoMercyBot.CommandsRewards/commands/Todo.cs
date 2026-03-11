using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Globals.NewtonSoftConverters;

public class TodoItem
{
    public string Text { get; set; } = null!;
    public bool IsDone { get; set; }
}

public class TargetInfo
{
    public string UserId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string[] RemainingArgs { get; set; } = Array.Empty<string>();
}

public class TodoCommand : IBotCommand
{
    public string Name => "todo";
    public CommandPermission Permission => CommandPermission.Everyone;

    private const string RECORD_TYPE = "Todo";

    public async Task Init(CommandScriptContext ctx) { }

    public async Task Callback(CommandScriptContext ctx)
    {
        if (ctx.Arguments.Length == 0)
        {
            await ShowUsage(ctx);
            return;
        }

        string subCommand = ctx.Arguments[0].ToLowerInvariant();
        string[] subArgs = ctx.Arguments.Skip(1).ToArray();

        switch (subCommand)
        {
            case "add":
                await AddTodo(ctx, subArgs);
                break;
            case "list":
                await ListTodos(ctx, subArgs);
                break;
            case "done":
                await MarkDone(ctx, subArgs);
                break;
            case "remove":
                await RemoveTodo(ctx, subArgs);
                break;
            case "clear":
                await ClearTodos(ctx, subArgs);
                break;
            default:
                await ShowUsage(ctx);
                break;
        }
    }

    private static async Task ShowUsage(CommandScriptContext ctx)
    {
        string text = "Usage: !todo add <text> | !todo list | !todo done <#> | !todo remove <#> | !todo clear -- Broadcaster: append @user to any subcommand";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }

    private static bool IsBroadcaster(CommandScriptContext ctx)
    {
        return ctx.Message.UserType == "Broadcaster";
    }

    private static async Task<TargetInfo> ResolveTarget(CommandScriptContext ctx, string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("@"))
        {
            if (!IsBroadcaster(ctx))
            {
                await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                    "@" + ctx.Message.DisplayName + " Only the broadcaster can manage other users' todos.",
                    ctx.Message.Id);
                return null;
            }

            string targetUsername = args[0].Replace("@", "").ToLower();
            string[] remaining = args.Skip(1).ToArray();

            try
            {
                User targetUser = await ctx.TwitchApiService.GetOrFetchUser(name: targetUsername);
                return new TargetInfo { UserId = targetUser.Id, DisplayName = targetUser.DisplayName, RemainingArgs = remaining };
            }
            catch
            {
                await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                    "@" + ctx.Message.DisplayName + " Could not find user \"" + targetUsername + "\".",
                    ctx.Message.Id);
                return null;
            }
        }

        return new TargetInfo { UserId = ctx.Message.UserId, DisplayName = ctx.Message.DisplayName, RemainingArgs = args };
    }

    private static async Task AddTodo(CommandScriptContext ctx, string[] args)
    {
        TargetInfo target = await ResolveTarget(ctx, args);
        if (target == null) return;

        if (target.RemainingArgs.Length == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "Please provide a todo text. Usage: !todo add <text>", ctx.Message.Id);
            return;
        }

        string todoText = string.Join(' ', target.RemainingArgs);

        TodoItem todoItem = new()
        {
            Text = todoText,
            IsDone = false
        };

        Record record = new()
        {
            UserId = target.UserId,
            RecordType = RECORD_TYPE,
            Data = todoItem.ToJson()
        };

        ctx.DatabaseContext.Records.Add(record);
        await ctx.DatabaseContext.SaveChangesAsync();

        int count = await ctx.DatabaseContext.Records
            .CountAsync(r => r.UserId == target.UserId && r.RecordType == RECORD_TYPE, ctx.CancellationToken);

        bool isSelf = target.UserId == ctx.Message.UserId;
        string owner = isSelf ? "" : " for " + target.DisplayName;

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
            "Todo #" + count + " added" + owner + ": " + todoText, ctx.Message.Id);
    }

    private static async Task ListTodos(CommandScriptContext ctx, string[] args)
    {
        TargetInfo target = await ResolveTarget(ctx, args);
        if (target == null) return;

        List<Record> records = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == target.UserId && r.RecordType == RECORD_TYPE)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ctx.CancellationToken);

        bool isSelf = target.UserId == ctx.Message.UserId;
        string ownerLabel = isSelf ? "Your" : target.DisplayName + "'s";

        if (records.Count == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                ownerLabel + " todo list is empty!", ctx.Message.Id);
            return;
        }

        List<string> items = new();
        for (int i = 0; i < records.Count; i++)
        {
            TodoItem todo = records[i].Data.FromJson<TodoItem>();
            string status = (todo != null && todo.IsDone) ? "v" : "x";
            items.Add("#" + (i + 1) + " [" + status + "] " + (todo != null ? todo.Text : "?"));
        }

        string text = ownerLabel + " todos (" + records.Count + "): " + string.Join(" | ", items);
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }

    private static async Task MarkDone(CommandScriptContext ctx, string[] args)
    {
        TargetInfo target = await ResolveTarget(ctx, args);
        if (target == null) return;

        int index = 0;
        if (target.RemainingArgs.Length == 0 || !int.TryParse(target.RemainingArgs[0], out index) || index < 1)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "Please provide a valid todo number. Usage: !todo done <#>", ctx.Message.Id);
            return;
        }

        List<Record> records = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == target.UserId && r.RecordType == RECORD_TYPE)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ctx.CancellationToken);

        if (index > records.Count)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "Only " + records.Count + " todo(s) found.", ctx.Message.Id);
            return;
        }

        Record record = records[index - 1];
        TodoItem todo = record.Data.FromJson<TodoItem>();

        if (todo == null)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "Something went wrong reading that todo.", ctx.Message.Id);
            return;
        }

        todo.IsDone = !todo.IsDone;
        record.Data = todo.ToJson();
        ctx.DatabaseContext.Records.Update(record);
        await ctx.DatabaseContext.SaveChangesAsync();

        bool isSelf = target.UserId == ctx.Message.UserId;
        string owner = isSelf ? "" : " (" + target.DisplayName + ")";
        string status = todo.IsDone ? "done" : "not done";

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
            "Todo #" + index + owner + " marked as " + status + ": " + todo.Text, ctx.Message.Id);
    }

    private static async Task RemoveTodo(CommandScriptContext ctx, string[] args)
    {
        TargetInfo target = await ResolveTarget(ctx, args);
        if (target == null) return;

        int index = 0;
        if (target.RemainingArgs.Length == 0 || !int.TryParse(target.RemainingArgs[0], out index) || index < 1)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "Please provide a valid todo number. Usage: !todo remove <#>", ctx.Message.Id);
            return;
        }

        List<Record> records = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == target.UserId && r.RecordType == RECORD_TYPE)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ctx.CancellationToken);

        if (index > records.Count)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "Only " + records.Count + " todo(s) found.", ctx.Message.Id);
            return;
        }

        Record record = records[index - 1];
        TodoItem todo = record.Data.FromJson<TodoItem>();

        ctx.DatabaseContext.Records.Remove(record);
        await ctx.DatabaseContext.SaveChangesAsync();

        bool isSelf = target.UserId == ctx.Message.UserId;
        string owner = isSelf ? "" : " (" + target.DisplayName + ")";

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
            "Removed todo #" + index + owner + ": " + (todo != null ? todo.Text : "?"), ctx.Message.Id);
    }

    private static async Task ClearTodos(CommandScriptContext ctx, string[] args)
    {
        TargetInfo target = await ResolveTarget(ctx, args);
        if (target == null) return;

        List<Record> records = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == target.UserId && r.RecordType == RECORD_TYPE)
            .ToListAsync(ctx.CancellationToken);

        bool isSelf = target.UserId == ctx.Message.UserId;
        string ownerLabel = isSelf ? "your" : target.DisplayName + "'s";

        if (records.Count == 0)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
                "No todos to clear for " + ownerLabel + " list!", ctx.Message.Id);
            return;
        }

        int count = records.Count;
        ctx.DatabaseContext.Records.RemoveRange(records);
        await ctx.DatabaseContext.SaveChangesAsync();

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username,
            "Cleared all " + count + " of " + ownerLabel + " todo(s)!", ctx.Message.Id);
    }
}

return new TodoCommand();
