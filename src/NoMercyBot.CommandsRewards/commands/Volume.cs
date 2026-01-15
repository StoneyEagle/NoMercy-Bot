using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Spotify;
using SpotifyAPI.Web;
using CurrentlyPlaying = NoMercyBot.Services.Spotify.Dto.CurrentlyPlaying;

public class VolumeCommand: IBotCommand
{
    public string Name => "volume";
    public CommandPermission Permission => CommandPermission.Moderator;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        SpotifyApiService? spotifyService = (SpotifyApiService)ctx.ServiceProvider.GetService(typeof(SpotifyApiService));
        CurrentlyPlaying? currentSong = await spotifyService.GetCurrentlyPlaying();

        if (currentSong?.Item == null)
        {
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.User.Username, "No song is currently playing!", ctx.Message.Id);
            return;
        }

        string? volumeParam = ctx.Arguments.Length > 0 ? ctx.Arguments[0] : null;
        
        if (string.IsNullOrEmpty(volumeParam))
        {
            int currentVolume = await spotifyService.GetVolume();
            string text = $"Current volume level is {currentVolume}";
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.User.Username, text, ctx.Message.Id);
            return;
        }

        if (!int.TryParse(volumeParam, out int volume) || volume < 0 || volume > 100)
        {
            await ctx.TwitchChatService.SendMessageAsBot(ctx.Message.Broadcaster.Username, "Please provide a valid volume level between 0 and 100: !volume <level> (0-100).");
            return;
        }
        
        await spotifyService.SetVolume(new(volume));
        
        string responseText = $"Volume set to {volume}%.";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.User.Username, responseText, ctx.Message.Id);
    }
}

return new VolumeCommand();