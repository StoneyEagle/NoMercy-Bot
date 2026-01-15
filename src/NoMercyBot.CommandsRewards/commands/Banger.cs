using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Spotify;
using SpotifyAPI.Web;
using Serilog.Events;
using CurrentlyPlaying = NoMercyBot.Services.Spotify.Dto.CurrentlyPlaying;

public class BangerCommand: IBotCommand
{
    public string Name => "banger";
    public CommandPermission Permission => CommandPermission.Everyone;

    private static string _playlist;
    
    private static readonly string[] _bangerAlreadyInPlaylistReplies =
    {
        "@{name}, that banger is already banging in the playlist! Your taste is consistent, but your memory needs work.",
        "Breaking: @{name} discovers the same song can't be a banger twice! Scientists baffled, playlist unchange",
        "Nice try @{name}, but that absolute unit is already vibing in the bangers playlist. Maybe check before you wreck?",
        "Plot twist @{name}: That song was already certified banger material! At least this mistake was free!",
        "@{name} just tried to double-banger a song. That's not how bangers work, bestie. The playlist remains unchanged!",
        "Ooof @{name}, that track is already living its best life in bangers! Good thing this command doesn't cost anything.",
        "Achievement unlocked @{name}: 'Banger Déjà Vu!' Reward: The satisfaction of knowing you have great taste... twic",
        "Fun fact @{name}: That song is already banging! Less fun fact: You just wasted a perfectly good command.",
        "@{name}, you can't make a banger more banger by adding it again. That's not how banger math works!",
        "Alert: @{name} attempted to create a banger paradox! The playlist rejected this temporal anomaly. At least it was free! ️"
    };

    public async Task Init(CommandScriptContext ctx)
    {
        _playlist ??= (await ctx.DatabaseContext.Configurations
            .Where(p => p.Key == "BangersPlaylistId")
            .FirstOrDefaultAsync(ctx.CancellationToken))?.Value;
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
        
        FullPlaylist? bangersPlaylist = await spotifyService.GetPlaylist(_playlist);
        if (bangersPlaylist?.Tracks?.Items != null && bangersPlaylist.Tracks.Items.Any(item => 
                item.Track is FullTrack track && track.Id == currentSong.Item.Id))
        {
            string randomTemplate = _bangerAlreadyInPlaylistReplies[Random.Shared.Next(_bangerAlreadyInPlaylistReplies.Length)];
            string text = TemplateHelper.ReplaceTemplatePlaceholders(randomTemplate, ctx);
            await ctx.ReplyAsync(text);
            return;
        }
        
        PlaylistAddItemsRequest request = new([currentSong.Item.Uri.ToString()]);

        await spotifyService.AddToPlaylist(_playlist, request);
        
        string successText = $"Added {currentSong.Item.Name} to the bangers playlist!";
        
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, successText, ctx.Message.Id);
    }
}

return new BangerCommand();