using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Spotify;
using NoMercyBot.Services.Spotify.Dto;

public class SongCommand: IBotCommand
{
    public string Name => "song";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        SpotifyApiService? spotifyService = (SpotifyApiService)ctx.ServiceProvider.GetService(typeof(SpotifyApiService));
        CurrentlyPlaying? currentSong = await spotifyService.GetCurrentlyPlaying();
        
        if (currentSong == null)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, "No song is currently playing!", ctx.Message.Id);
            return;
        }

        string text = $"The current song is: {currentSong.Item.Name} by {currentSong.Item.Artists[0]?.Name} {currentSong.Item.Href}";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new SongCommand();