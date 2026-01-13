using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Interfaces;
using NoMercyBot.Services.Other;
using NoMercyBot.Services.Twitch;
using NoMercyBot.Services.Twitch.Scripting;
using NoMercyBot.Services.Widgets;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Spotify;

public class BsodRecord
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<DateTime> Dates { get; set; } = [];
}

public class BsodReward : IReward
{
    public Guid RewardId => Guid.Parse("67b5638d-e523-4b53-81d7-68812f60889e");
    public string RewardTitle => "System.exe Has Opinions";
    public RewardPermission Permission => RewardPermission.Everyone;

    private const string STORAGE_KEY = "BSODRecords";

    // OS-specific SSML templates with phonetics and attitude
    // Placeholders: {USERNAME}, {MESSAGE}, {OS}, {USERNAME_PH}, {MESSAGE_PH}
    private static readonly Dictionary<string, string[]> _osSsmlTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---------------- WINDOWS 3.1 ----------------
        ["win31"] =
        [
            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-SteffanNeural"">
                <prosody rate=""-40%"" pitch=""-10%"">
                  <break time=""300ms""/>
                  Fatal. Exception.
                  <break time=""400ms""/>
                  User {USERNAME} has caused. A. System. Malfunction.
                  <break time=""500ms""/>
                  Error message: {MESSAGE}.
                  <break time=""600ms""/>
                  Press. Control. Alt. Delete. To pretend. This never happened.
                </prosody>
              </voice>
            </speak>",

            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-SteffanNeural"">
                <prosody rate=""-35%"" pitch=""-15%"">
                  Warning.
                  <break time=""300ms""/>
                  Program {USERNAME} has performed an illegal operation.
                  <break time=""400ms""/>
                  It will now be terminated.
                  <break time=""500ms""/>
                  Details: {MESSAGE}.
                  <break time=""600ms""/>
                  Please upgrade your human driver.
                </prosody>
              </voice>
            </speak>",

            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-SteffanNeural"">
                <prosody rate=""-45%"" pitch=""-20%"">
                  Fatal error.
                  <break time=""300ms""/>
                  User {USERNAME} has attempted advanced computing.
                  <break time=""400ms""/>
                  System could not keep up.
                  <break time=""500ms""/>
                  Error code: Zero clue. Message: {MESSAGE}.
                  <break time=""700ms""/>
                  Suggestion: Go back to Solitaire.
                </prosody>
              </voice>
            </speak>"
        ],

        // ---------------- WINDOWS 95 / 98 ----------------
        ["win95"] =
        [
            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-ChristopherNeural"">
                <prosody rate=""-15%"" pitch=""-8%"">
                  A fatal exception zero E has occurred.
                  <break time=""300ms""/>
                  The system has detected user {USERNAME} attempting something ambitious.
                  <break time=""300ms""/>
                  This was a mistake.
                  <break time=""300ms""/>
                  Error details: {MESSAGE}.
                  <break time=""500ms""/>
                  Press any key to pretend this did not happen.
                </prosody>
              </voice>
            </speak>",

            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-ChristopherNeural"">
                <prosody rate=""-10%"" pitch=""-5%"">
                  Windows ninety five has encountered an error.
                  <break time=""300ms""/>
                  Cause: User {USERNAME} and their brilliant idea.
                  <break time=""300ms""/>
                  {MESSAGE}.
                  <break time=""500ms""/>
                  Please consider never doing that again.
                </prosody>
              </voice>
            </speak>"
        ],

        ["win98"] =
        [
            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-ChristopherNeural"">
                <prosody rate=""-15%"" pitch=""-6%"">
                  Windows ninety eight has experienced a critical failure.
                  <break time=""300ms""/>
                  Responsible party: User {USERNAME}.
                  <break time=""300ms""/>
                  Statement on record: {MESSAGE}.
                  <break time=""400ms""/>
                  The system would like to file a formal complaint.
                  <break time=""500ms""/>
                  Press Control Alt Delete to restart or to confess.
                </prosody>
              </voice>
            </speak>",

            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-ChristopherNeural"">
                <prosody rate=""-20%"" pitch=""-10%"">
                  A fatal exception zero E occurred at memory address.
                  <break time=""300ms""/>
                  Some hex number no one understands.
                  <break time=""300ms""/>
                  Caused by {USERNAME} driver.
                  <break time=""400ms""/>
                  Error notes: {MESSAGE}.
                  <break time=""500ms""/>
                  Windows ninety eight will now dramatically collapse.
                </prosody>
              </voice>
            </speak>"
        ],

        // ---------------- WINDOWS 2000 ----------------
        ["win2000"] =
        [
            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-GuyNeural"">
                <prosody rate=""-10%"" pitch=""-5%"">
                  Stop error.
                  <break time=""300ms""/>
                  The system has encountered a serious problem.
                  <break time=""300ms""/>
                  User {USERNAME} has triggered a critical operation.
                  <break time=""300ms""/>
                  Technical information: {MESSAGE}.
                  <break time=""500ms""/>
                  If this is the first time you have seen this message,
                  <break time=""300ms""/>
                  it is already too late.
                </prosody>
              </voice>
            </speak>",

            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-GuyNeural"">
                <prosody rate=""-8%"" pitch=""-5%"">
                  Windows two thousand has stopped to prevent damage
                  to your computer and to its reputation.
                  <break time=""300ms""/>
                  Responsible process: {USERNAME} driver.
                  <break time=""400ms""/>
                  Error summary: {MESSAGE}.
                  <break time=""500ms""/>
                  Consult your system administrator.
                  <break time=""300ms""/>
                  Unless you are the system administrator.
                  <break time=""300ms""/>
                  In which case, good luck.
                </prosody>
              </voice>
            </speak>"
        ],

        // ---------------- WINDOWS XP ----------------
        ["winXp"] =
        [
            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-GuyNeural"">
                <prosody rate=""-12%"" pitch=""-4%"">
                  A problem has been detected and Windows X P has been shut down
                  to prevent further humiliation.
                  <break time=""400ms""/>
                  The problem appears to be caused by the following user:
                  <break time=""200ms""/>
                  {USERNAME}.
                  <break time=""400ms""/>
                  Their bright idea was: {MESSAGE}.
                  <break time=""600ms""/>
                  Technical information:
                  <break time=""200ms""/>
                  We are so very doomed.
                </prosody>
              </voice>
            </speak>",

            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-GuyNeural"">
                <prosody rate=""-10%"" pitch=""-3%"">
                  Windows X P is trying its best.
                  <break time=""300ms""/>
                  Unfortunately, {USERNAME} is also here.
                  <break time=""300ms""/>
                  Latest action: {MESSAGE}.
                  <break time=""400ms""/>
                  This combination was not survivable.
                  <break time=""600ms""/>
                  Please contact your system administrator,
                  <break time=""300ms""/>
                  or just reboot and lie about it.
                </prosody>
              </voice>
            </speak>"
        ],

        // ---------------- WINDOWS 10 ----------------
        ["win10"] =
        [
            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-JennyMultilingualNeural"">
                <prosody rate=""-5%"" pitch=""-2%"">
                  Your P C ran into a problem and needs to restart.
                  <break time=""300ms""/>
                  The problem was {USERNAME}. Specifically: {MESSAGE}.
                  <break time=""500ms""/>
                  We are collecting some error information.
                  <break time=""300ms""/>
                  Translation: We are judging you.
                  <break time=""600ms""/>
                  When we're done, we will restart and pretend this never happened.
                </prosody>
              </voice>
            </speak>",

            @"<speak version=""1.0"" xml:lang=""en-US"">
              <voice name=""en-US-JennyMultilingualNeural"">
                <prosody rate=""-4%"" pitch=""-1%"">
                  Just so you know, your P C did not crash on its own.
                  <break time=""300ms""/>
                  It was assisted by user {USERNAME}.
                  <break time=""400ms""/>
                  With the helpful input: {MESSAGE}.
                  <break time=""500ms""/>
                  Thank you for choosing chaos.
                  <break time=""300ms""/>
                  Windows ten will now reboot and act innocent.
                </prosody>
              </voice>
            </speak>"
        ]
    };

    public async Task Init(RewardScriptContext ctx)
    {
        Storage? storage = await ctx.DatabaseContext.Storages
            .FirstOrDefaultAsync(s => s.Key == STORAGE_KEY);

        if (storage == null)
        {
            storage = new()
            {
                Key = STORAGE_KEY,
                Value = "[]"
            };
            await ctx.DatabaseContext.Storages.AddAsync(storage);
            await ctx.DatabaseContext.SaveChangesAsync();
        }
    }

    public async Task Callback(RewardScriptContext ctx)
    {
        string? userInput = ctx.UserInput?.Trim();

        if (string.IsNullOrEmpty(userInput))
        {
            string text = $"@{ctx.UserDisplayName} Please provide a valid BSOD message. Your points have been refunded.";
            await ctx.ReplyAsync(text);
            await ctx.RefundAsync();
            return;
        }

        try
        {
            Widget? widget = await ctx.DatabaseContext.Widgets
                .Where(db => db.Id == Ulid.Parse("01KCC89B7Z14M1Z0QEC8PDAGZB"))
                .FirstOrDefaultAsync(ctx.CancellationToken);

            string settingsJson = widget.SettingsJson;
            Dictionary<string, dynamic>? settings = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(settingsJson);

            JObject osConfig = (JObject)settings["osConfig"];
            List<string> enabledOsKeys = osConfig
                .Properties()                     // use Properties() instead of Children<JProperty>()
                .Where(p => (bool)p.Value["enabled"])
                .Select(p => p.Name)
                .ToList();

            if (enabledOsKeys.Count == 0)
            {
                await ctx.ReplyAsync($"@{ctx.UserDisplayName} No enabled OS configured. Points refunded.");
                await ctx.RefundAsync();
                return;
            }

            // Pick a random enabled OS
            string chosenOs = enabledOsKeys[Random.Shared.Next(enabledOsKeys.Count)];

            // Pick a random SSML template for the chosen OS
            string[] templates = _osSsmlTemplates[chosenOs];
            string ssmlTemplate = templates[Random.Shared.Next(templates.Length)];

            string username = ctx.UserDisplayName;
            string message = userInput;

            // Replace placeholders
            string ssml = ssmlTemplate
                .Replace("{USERNAME}", EscapeXml(username))
                .Replace("{MESSAGE}", EscapeXml(message));

            string speakerId = "en-US-GuyNeural"; // Default voice; SSML overrides

            // TTS
            TtsService ttsService = ctx.ServiceProvider.GetRequiredService<TtsService>();
            string? audioBase64 = await ttsService.SynthesizeSsmlAsync(ssml, speakerId, ctx.CancellationToken);

            if (audioBase64 == null)
            {
                await ctx.ReplyAsync($"@{ctx.UserDisplayName} TTS synthesis failed. Points refunded.");
                await ctx.RefundAsync();
                return;
            }

            var payload = new
            {
                user = new
                {
                    id = ctx.UserId,
                    display_name = ctx.UserDisplayName
                },
                reward = new
                {
                    id = RewardId,
                    title = RewardTitle
                },
                audio = audioBase64,
                input = userInput,
                os = chosenOs
            };

            IWidgetEventService widgetEventService = ctx.ServiceProvider.GetRequiredService<IWidgetEventService>();
            await widgetEventService.PublishEventAsync("bsod.trigger", payload);

            // Get the OS timings
            JToken? chosenOsConfig = osConfig[chosenOs];
            JToken? timings = chosenOsConfig["timings"];
            int ttsDurationMs =
                (int)timings["glitch"] +
                (int)timings["black"] +
                (int)timings["bios"] +
                (int)timings["boot"] +
                (int)timings["bsod"] +
                (int)timings["startup"];

            SpotifyApiService? spotifyService = (SpotifyApiService)ctx.ServiceProvider.GetService(typeof(SpotifyApiService));
            await spotifyService.Pause();
            await Task.Delay(ttsDurationMs);
            await spotifyService.ResumePlayback();
        }
        catch (Exception ex)
        {
            await ctx.ReplyAsync($"@{ctx.UserDisplayName} TTS error: {ex.Message}. Points refunded.");
            Logger.Twitch($"BSOD Reward TTS error for user {ctx.UserDisplayName} ({ctx.UserId}): {ex}");
            await ctx.RefundAsync();
        }
    }


    private static string EscapeXml(string input)
    {
        return System.Security.SecurityElement.Escape(input) ?? string.Empty;
    }

    private static string EscapePhoneme(string input)
    {
        // For now just basic XML escape; if you later use real IPA, you might want a different path
        return System.Security.SecurityElement.Escape(input) ?? string.Empty;
    }
}

return new BsodReward();
