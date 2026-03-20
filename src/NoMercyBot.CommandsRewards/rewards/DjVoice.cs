using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Widgets;

/// <summary>
/// DJ Voice reward — Your message gets read in an over-the-top radio DJ style
/// with SSML emphasis, pitch changes, and dramatic delivery.
/// </summary>
public class DjVoiceReward : IReward
{
    public Guid RewardId => Guid.Parse("862b9490-b9bc-44f4-b50e-0c454ee3f09d");
    public string RewardTitle => "DJ Voice";
    public RewardPermission Permission => RewardPermission.Everyone;
    public bool AutoCreate => true;
    public int Cost => 2000;
    public string Prompt => "Enter your message to be read in DJ style";
    public bool IsUserInputRequired => true;
    public string BackgroundColor => "#E74C3C";

    // Deep male voice for DJ style
    private const string DJ_VOICE = "en-US-GuyNeural";

    private static readonly string[] _djIntros =
    {
        "AND NOW, COMING TO YOU LIVE FROM THE CHAT,",
        "LADIES AND GENTLEMEN, YOU ARE NOT READY FOR THIS,",
        "TURN IT UP BECAUSE HERE COMES A MESSAGE FROM THE CROWD,",
        "YOOOO CHAT, LISTEN UP, WE GOT A BANGER INCOMING,",
        "BREAKING INTO YOUR REGULARLY SCHEDULED PROGRAMMING,",
        "OH SNAP, SOMEBODY REDEEMED THE DJ VOICE, HERE WE GO,",
        "THIS JUST IN FROM THE HOTTEST CHAT ON TWITCH,",
        "DROPPING IN WITH A FRESH MESSAGE, LET'S GOOO,",
    };

    private static readonly string[] _djOutros =
    {
        "AND THAT'S WHAT I'M TALKING ABOUT!",
        "MAKE SOME NOISE IN THE CHAT!",
        "THAT WAS FIRE, NO CAP!",
        "DON'T TOUCH THAT DIAL!",
        "YOU HEARD IT HERE FIRST, CHAT!",
        "THE CROWD GOES WILD!",
    };

    public Task Init(RewardScriptContext ctx)
    {
        return Task.CompletedTask;
    }

    public async Task Callback(RewardScriptContext ctx)
    {
        string? userInput = ctx.UserInput?.Trim();

        if (string.IsNullOrEmpty(userInput))
        {
            await ctx.ReplyAsync($"@{ctx.UserDisplayName} The DJ needs something to say! Include a message.");
            await ctx.RefundAsync();
            return;
        }

        string intro = _djIntros[Random.Shared.Next(_djIntros.Length)];
        string outro = _djOutros[Random.Shared.Next(_djOutros.Length)];

        // Keep original case for TTS (ALL CAPS makes Edge TTS spell out letters)
        string introText = $"{intro} It's your boy {ctx.UserDisplayName}!";
        string outroText = outro;

        TtsService ttsService = ctx.ServiceProvider.GetRequiredService<TtsService>();

        string processedIntro = await ttsService.ApplyUsernamePronunciationsAsync(introText);
        string processedMessage = await ttsService.ApplyUsernamePronunciationsAsync(userInput);

        var segments = new List<(string ssml, string voiceId)>
        {
            TtsService.Segment(DJ_VOICE, processedIntro),
            TtsService.Segment(DJ_VOICE, processedMessage),
            TtsService.Segment(DJ_VOICE, outroText),
        };

        (string? audioBase64, int durationMs) = await ttsService.SynthesizeMultiVoiceSsmlAsync(
            segments, ctx.CancellationToken);

        if (audioBase64 == null)
        {
            await ctx.ReplyAsync($"@{ctx.UserDisplayName} DJ Voice synthesis failed. Points refunded.");
            await ctx.RefundAsync();
            return;
        }

        // Send the DJ-styled message in chat (ALL CAPS fine for chat display)
        string chatMsg = $"{intro} IT'S {ctx.UserDisplayName}! \"{userInput}\" {outro}";
        await ctx.TwitchChatService.SendMessageAsBot(ctx.BroadcasterLogin, chatMsg);

        IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
        await widgetEventService.PublishEventAsync("channel.chat.message.tts", new
        {
            text = chatMsg,
            user = new { id = ctx.UserId },
            audioBase64,
            provider = "Edge",
            cost = 0m,
            characterCount = chatMsg.Length,
            cached = false,
        });

        await ctx.FulfillAsync();
    }
}

return new DjVoiceReward();
