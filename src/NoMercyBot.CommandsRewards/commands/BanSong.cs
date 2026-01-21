using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Spotify.Dto;

public class BannedSong
{
    public string SongId { get; set; } = null!;
    public string SongName { get; set; } = null!;
    public string? Reason { get; set; }
}

public class BanSongCommand: IBotCommand
{
    public string Name => "bansong";
    public CommandPermission Permission => CommandPermission.Moderator;
    
    private const string STORAGE_KEY = "BannedSong";

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        SpotifyApiService? spotifyService = (SpotifyApiService)ctx.ServiceProvider.GetService(typeof(SpotifyApiService));
        CurrentlyPlaying? currentSong = await spotifyService.GetCurrentlyPlaying();

        if (currentSong?.Item == null)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, "No song is currently playing!", ctx.Message.Id);
            return;
        }

        await spotifyService.NextTrack();
        
        await BanSong(ctx, currentSong);

        string text = $"{currentSong.Item.Name} banned from being requested again.";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);

        await CountUserBannedSongs(ctx);
    }
    
    private static async Task BanSong(CommandScriptContext ctx, CurrentlyPlaying currentSong)
    {
        string? reason = ctx.Arguments.Length > 0 ? string.Join(' ', ctx.Arguments) : null;
        
        BannedSong bannedSong = new()
        {
            SongId = currentSong.Item.Id,
            SongName = currentSong.Item.Name,
            Reason = reason
        };
        
        Record record = new()
        {
            UserId = ctx.Message.UserId,
            RecordType = STORAGE_KEY,
            Data = bannedSong.ToJson()
        };
            
        ctx.DatabaseContext.Records.Add(record);
        await ctx.DatabaseContext.SaveChangesAsync();
    }
    
    private static async Task CountUserBannedSongs(CommandScriptContext ctx)
    {
        List<Record> bannedSongs = await ctx.DatabaseContext.Records
            .Where(r => r.UserId == ctx.Message.UserId && r.RecordType == STORAGE_KEY)
            .ToListAsync(ctx.CancellationToken);
        
        if (bannedSongs.Count > 5 && bannedSongs.Count <= 10)
        {
            string text =
                "You have gotten 5 banned songs now. Don't get your ass banned from this feature.";
            await ctx.TwitchChatService.SendMessageAsBot(ctx.Message.Broadcaster.Username, text);
        }
        else if (bannedSongs.Count >= 10)
        {
            string text = "Your permission to redeem songs have been revoked, points will not be refunded if you request more";
            await ctx.TwitchChatService.SendMessageAsBot(ctx.Message.Broadcaster.Username, text);
        }
    }
}

return new BanSongCommand();