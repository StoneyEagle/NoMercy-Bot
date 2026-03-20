using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Widgets;
using NoMercyBot.Globals.NewtonSoftConverters;

public class RecordDto
{
    [JsonProperty("display_name")] public string DisplayName { get; set; }
    [JsonProperty("count")] public int Count { get; set; }

    public RecordDto()
    {
        
    }
}

public class LeaderboardCommand: IBotCommand
{
    public string Name => "leaderboard";
    public CommandPermission Permission => CommandPermission.Everyone;
    
    private string[] STORAGE_KEYS => new[]
    {
        "Spotify",
        "Lurker",
        "TTS",
        "BSOD",
        "LuckyFeather",
        "CommandUsage"
    };

    public async Task Init(CommandScriptContext ctx)
    {
        
    }
    
    public async Task Callback(CommandScriptContext ctx)
    {
        List<Record> records = await ctx.DatabaseContext.Records
            .Include(r => r.User)
            .Where(r => STORAGE_KEYS.Contains(r.RecordType))
            .ToListAsync();
        
        List<IGrouping<string, Record>> groupedRecords = records
            .GroupBy(r => r.RecordType)
            .ToList();
        
        Dictionary<string, List<RecordDto>> topRecords = new();

        foreach (IGrouping<string, Record> group in groupedRecords)
        {
            var userGroups = group
                .GroupBy(r => r.UserId)
                .OrderByDescending(r => r.Count())
                .Take(3)
                .Select(g => g.First())
                .Select(r => new RecordDto()
                {
                    DisplayName = r.User?.DisplayName ?? "Unknown",
                    Count = group.Count(gr => gr.UserId == r.UserId)
                })
                .ToList();

            topRecords[group.Key] = userGroups;
        }
        
        WidgetEventService _widgetEventService = (WidgetEventService)ctx.ServiceProvider.GetService(typeof(WidgetEventService));
        await _widgetEventService.PublishEventAsync("overlay.leaderboard.show", topRecords);
        
       
    }
}

return new LeaderboardCommand();