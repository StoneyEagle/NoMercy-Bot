using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database.Models;
using NoMercyBot.Database;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

public class RecordsCommand: IBotCommand
{
    public string Name => "records";
    public CommandPermission Permission => CommandPermission.Everyone;
    
    private static Storage? _recordStorage;

    public async Task Init(CommandScriptContext ctx)
    {
        _recordStorage = await ctx.DatabaseContext.Storages
            .Where(r => r.Key == "SpotifyRecords")
            .FirstOrDefaultAsync();

        if (_recordStorage == null)
        {
            Storage newRecordStorage = new Storage
            {
                Key = "SpotifyRecords",
                Value = "[]"
            };
            
            ctx.DatabaseContext.Storages.Add(newRecordStorage);
            await ctx.DatabaseContext.SaveChangesAsync();
        }
    }
    
    public async Task Callback(CommandScriptContext ctx)
    {
        List<SongRecord> songs = await ctx.DatabaseContext.Storages
                .Where(r => r.Key == "SpotifyRecords")
                .Select(r => r.Value.FromJson<List<SongRecord>>())
                .FirstOrDefaultAsync();
        
        List<RecordItem> records = new List<RecordItem>();

        // Check if user has song records
        var userSong = songs.FirstOrDefault(song => song.UserId == ctx.Message.User.Id);
        if (userSong != null)
        {
            records.Add(new()
            {
                Type = "song",
                Amount = userSong.Count
            });
        }

        if (records.Any())
        {
            string recordText = string.Join(", ", records.Select(r => $"{r.Type}: {r.Amount} times"));
            string text = $"@{ctx.Message.User.DisplayName} your records: {recordText}";
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Channel, text, ctx.Message.Id);
        }
        else
        {
            string text = $"@{ctx.Message.User.DisplayName} you have no records yet!";
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Channel, text, ctx.Message.Id);
        }
    }
}

public class SongRecord
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<DateTime> Dates { get; set; } = new();
}

public class RecordItem
{
    public string Type { get; set; } = string.Empty;
    public int Amount { get; set; }
}

return new RecordsCommand();