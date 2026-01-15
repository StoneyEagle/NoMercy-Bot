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

public class SkipCommand: IBotCommand
{
    public string Name => "skip";
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
            await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, "No song is currently playing!", ctx.Message.Id);
            return;
        }

        await spotifyService.NextTrack();

        string text = "I know right, Stoney's song choices are always on point! Skipped to the next track.";
        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);
    }
}

return new SkipCommand();