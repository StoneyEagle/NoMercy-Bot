using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Globals.SystemCalls;
using SpotifyAPI.Web;

public class PlaylistCommand: IBotCommand
{
    public string Name => "playlist";
    public CommandPermission Permission => CommandPermission.Everyone;

    private static string _playlist;
    
    public async Task Init(CommandScriptContext ctx)
    {
        _playlist ??= (await ctx.DatabaseContext.Configurations
                    .Where(p => p.Key == "BangersPlaylistId")
                    .FirstOrDefaultAsync(ctx.CancellationToken))?.Value;
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        SpotifyApiService? spotifyService = (SpotifyApiService)ctx.ServiceProvider.GetService(typeof(SpotifyApiService));
        
        if (string.IsNullOrEmpty(_playlist))
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, "No playlist ID configured.", ctx.Message.Id);
            return; 
        }
        
        FullPlaylist? playlist = await spotifyService.GetPlaylist(_playlist);
        
        if (playlist == null || !playlist.ExternalUrls.TryGetValue("spotify", out string? url))
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, "Playlist not found.", ctx.Message.Id);
            return;
        }
        
        string text = $"The bangers playlist is: {url}";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new PlaylistCommand();