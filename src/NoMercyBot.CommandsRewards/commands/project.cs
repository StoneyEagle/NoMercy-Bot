using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Interfaces;

public class ProjectCommand: IBotCommand
{
    public string Name => "project";
    public CommandPermission Permission => CommandPermission.Everyone;

    public async Task Init(CommandScriptContext ctx)
    {
    }

    public async Task Callback(CommandScriptContext ctx)
    {
        string text = "I'm working on a project named NoMercy TV, the Effortless Encoder. " +
                      "It is a self-hosted streaming solution that allows you to stream your own movies, tv shows and music. " +
                      "Rip and archive your physical cd's, dvd's and blu-rays effortlessly, no more need to manually put in all the effort. " +
                      "It is time to take back control of your media and return to enjoying what you love. " +
                      "No tracking, no ads, no data collection, NoMercy!";

        await ctx.TwitchChatService.SendReplyAsBot(ctx.Message.Broadcaster.Username, text, ctx.Message.Id);

        // TODO: Implement Spotify volume control and blerp playback
        // await spotifyClient.Volume(10);
        // await PlayBlerp("nomercy");
        // await spotifyClient.Volume(70);
    }
}

return new ProjectCommand();