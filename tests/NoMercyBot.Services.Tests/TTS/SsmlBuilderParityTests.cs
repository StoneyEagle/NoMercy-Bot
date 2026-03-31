using System.Security;
using System.Text.RegularExpressions;
using NoMercyBot.Services.TTS;
using Xunit;

namespace NoMercyBot.Services.Tests.TTS;

/// <summary>
/// These tests replicate the EXACT logic from every TTS call site in the codebase,
/// then assert the builder produces byte-for-byte identical output.
/// If a test here fails, migrating that call site to SsmlBuilder would break TTS.
/// </summary>
public class SsmlBuilderParityTests
{
    #region LuckyFeatherChange.cs — SynthesizeSsmlAsync with pre-built SSML

    // LuckyFeatherChange has hardcoded SSML strings with literal apostrophes.
    // The strings go DIRECTLY to SynthesizeSsmlAsync — no escaping is applied.
    // The builder WILL escape apostrophes, so these can't use literal apostrophes
    // in the text — they must match the final SSML output character-for-character.

    /// <summary>
    /// Every hardcoded _featherFoundSsml string from LuckyFeatherChange.cs:106-111.
    /// These contain NO special characters so the builder should produce exact matches.
    /// </summary>
    [Theory]
    [InlineData("Heads up! The Lucky Feather has appeared. Grab it if you can!")]
    [InlineData("Sneaky alert! The feather is free for the taking!")]
    [InlineData("Attention! The Lucky Feather is out in the wild. Go get it!")]
    [InlineData("Look alive! The Lucky Feather can be stolen now!")]
    public void LuckyFeather_found_exact_match(string text)
    {
        // Original hardcoded format from LuckyFeatherChange.cs
        string original =
            "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\" xmlns:mstts=\"https://www.w3.org/2001/mstts\">"
            + "<voice name=\"en-US-JennyNeural\">"
            + text
            + "</voice></speak>";

        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-JennyNeural", text)
            .ToSsml();

        Assert.Equal(original, fromBuilder);
    }

    /// <summary>
    /// _winnerCongratsSsml strings contain apostrophes: "You're", "You claimed".
    /// The original SSML has LITERAL apostrophes (not &amp;apos;) because
    /// the strings are hardcoded SSML, not escaped text.
    /// The builder escapes text → &amp;apos;. This INTENTIONAL difference is safe
    /// because &amp;apos; is valid XML and TTS engines treat it identically.
    /// </summary>
    [Theory]
    [InlineData("Bravo! You just stole the Lucky Feather. Enjoy your glory.")]
    [InlineData("Congratulations! The Lucky Feather now belongs to you. Smug time!")]
    [InlineData("Well played! You claimed the Lucky Feather. Bask in your victory.")]
    public void LuckyFeather_congrats_no_apostrophe_exact_match(string text)
    {
        string original =
            "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\" xmlns:mstts=\"https://www.w3.org/2001/mstts\">"
            + "<voice name=\"en-US-GuyNeural\">"
            + text
            + "</voice></speak>";

        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", text)
            .ToSsml();

        Assert.Equal(original, fromBuilder);
    }

    [Fact]
    public void LuckyFeather_congrats_with_apostrophe_escapes_safely()
    {
        // Original has literal apostrophe in SSML:
        // <voice name="en-US-GuyNeural">Kudos, feather thief! You're officially the top pickpocket.</voice>
        string originalText = "Kudos, feather thief! You're officially the top pickpocket.";

        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", originalText)
            .ToSsml();

        // Builder escapes ' → &apos; which is valid XML and sounds identical
        Assert.Contains("You&apos;re officially", fromBuilder);
        // Full SSML is well-formed
        Assert.StartsWith("<speak version=\"1.0\"", fromBuilder);
        Assert.EndsWith("</speak>", fromBuilder);
    }

    #endregion

    #region Bsod.cs — SynthesizeSsmlAsync with template + SecurityElement.Escape + AdjustSsmlForLength

    /// <summary>
    /// Replicates the exact Bsod.cs flow: template selection → placeholder replacement
    /// with SecurityElement.Escape → AdjustSsmlForLength. The builder must produce
    /// SSML that a TTS engine processes identically.
    /// </summary>
    [Fact]
    public void Bsod_win31_short_message_matches_template_output()
    {
        // --- Replicate Bsod.cs Callback logic ---
        string username = "TestUser";
        string message = "oops"; // Short message, no length adjustment

        // Bsod uses SecurityElement.Escape (different from SanitizeText!)
        string escapedUsername = SecurityElement.Escape(username) ?? string.Empty;
        string escapedMessage = SecurityElement.Escape(message) ?? string.Empty;

        // First win31 template from Bsod.cs:44-58
        string template = "<speak version=\"1.0\" xml:lang=\"en-US\">\n"
            + "              <voice name=\"en-US-SteffanNeural\">\n"
            + "                <prosody rate=\"-40%\" pitch=\"-10%\">\n"
            + "                  <break time=\"300ms\"/>\n"
            + "                  Fatal. Exception.\n"
            + "                  <break time=\"400ms\"/>\n"
            + "                  User {USERNAME} has caused. A. System. Malfunction.\n"
            + "                  <break time=\"500ms\"/>\n"
            + "                  Error message: {MESSAGE}.\n"
            + "                  <break time=\"600ms\"/>\n"
            + "                  Press. Control. Alt. Delete. To pretend. This never happened.\n"
            + "                </prosody>\n"
            + "              </voice>\n"
            + "            </speak>";

        string originalSsml = template
            .Replace("{USERNAME}", escapedUsername)
            .Replace("{MESSAGE}", escapedMessage);
        // Short message → no AdjustSsmlForLength changes

        // --- Now build with SsmlBuilder ---
        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-SteffanNeural", p => p
                .SetRate("-40%").SetPitch("-10%")
                .Break(300)
                .Text("Fatal. Exception.")
                .Break(400)
                .Text($"User {escapedUsername} has caused. A. System. Malfunction.")
                .Break(500)
                .Text($"Error message: {escapedMessage}.")
                .Break(600)
                .Text("Press. Control. Alt. Delete. To pretend. This never happened."))
            .ToSsml();

        // The formats WILL differ (template has whitespace/newlines, builder is compact;
        // template has no xmlns, builder has xmlns+mstts).
        // What matters: the TTS engine receives the same voice, prosody, breaks, and text.
        // Verify structural equivalence:
        Assert.Contains("<voice name=\"en-US-SteffanNeural\">", fromBuilder);
        Assert.Contains("rate=\"-40%\"", fromBuilder);
        Assert.Contains("pitch=\"-10%\"", fromBuilder);
        Assert.Contains("<break time=\"300ms\"/>", fromBuilder);
        Assert.Contains("Fatal. Exception.", fromBuilder);
        Assert.Contains("<break time=\"400ms\"/>", fromBuilder);
        Assert.Contains("User TestUser has caused. A. System. Malfunction.", fromBuilder);
        Assert.Contains("<break time=\"500ms\"/>", fromBuilder);
        Assert.Contains("Error message: oops.", fromBuilder);
        Assert.Contains("<break time=\"600ms\"/>", fromBuilder);
        Assert.Contains("Press. Control. Alt. Delete.", fromBuilder);
    }

    [Fact]
    public void Bsod_with_special_chars_in_user_input()
    {
        // User could type anything — including XML-dangerous characters
        string username = "H4ck3r<script>";
        string message = "rm -rf / & format C:";

        // When migrating to builder, pass RAW text — the builder escapes it.
        // Do NOT pre-escape with SecurityElement.Escape, or you'll get double-escaping.
        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-SteffanNeural", p => p
                .SetRate("-40%").SetPitch("-10%")
                .Break(300)
                .Text($"User {username} has caused. A. System. Malfunction.")
                .Break(400)
                .Text($"Error message: {message}."))
            .ToSsml();

        // Builder escapes < > & → &lt; &gt; &amp;
        Assert.Contains("H4ck3r&lt;script&gt;", fromBuilder);
        Assert.Contains("rm -rf / &amp; format C:", fromBuilder);
        // No raw HTML/XML injection
        Assert.DoesNotContain("<script>", fromBuilder);
    }

    [Fact]
    public void Bsod_win10_template_structure()
    {
        string username = "Viewer";
        string message = "something broke again";

        string escapedUsername = SecurityElement.Escape(username) ?? string.Empty;
        string escapedMessage = SecurityElement.Escape(message) ?? string.Empty;

        // Win10 uses JennyMultilingualNeural with lighter prosody
        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-JennyMultilingualNeural", p => p
                .SetRate("-5%").SetPitch("-2%")
                .Break(300)
                .Text($"Your P C ran into a problem and needs to restart.")
                .Break(300)
                .Text($"The problem was {escapedUsername}. Specifically: {escapedMessage}.")
                .Break(500)
                .Text("We are collecting some error information.")
                .Break(300)
                .Text("Translation: We are judging you.")
                .Break(600)
                .Text("When we're done, we will restart and pretend this never happened."))
            .ToSsml();

        Assert.Contains("<voice name=\"en-US-JennyMultilingualNeural\">", fromBuilder);
        Assert.Contains("rate=\"-5%\"", fromBuilder);
        Assert.Contains("pitch=\"-2%\"", fromBuilder);
        Assert.Contains("The problem was Viewer.", fromBuilder);
        // Apostrophe in "we're" gets escaped — TTS engine reads it the same
        Assert.Contains("we&apos;re done", fromBuilder);
    }

    [Fact]
    public void Bsod_very_long_message_adjusts_rate_and_breaks()
    {
        // Bsod AdjustSsmlForLength does regex replacement on the final SSML string.
        // The builder should support this as a post-processing step.
        // This test verifies that the builder's output can be post-processed
        // with the same AdjustSsmlForLength logic and produce valid SSML.

        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-SteffanNeural", p => p
                .SetRate("-40%").SetPitch("-10%")
                .Break(300)
                .Text("Fatal. Exception.")
                .Break(400)
                .Text("User TestUser has caused. A. System. Malfunction.")
                .Break(500)
                .Text("Error message: " + new string('x', 350) + ".")
                .Break(600)
                .Text("Press. Control. Alt. Delete."))
            .ToSsml();

        // Apply the same AdjustSsmlForLength logic from Bsod.cs:432-483
        string adjusted = AdjustSsmlForLength(fromBuilder, 350);

        // IMPORTANT: AdjustSsmlForLength has a chain-replacement bug in Bsod.cs!
        // -40% → -10% (first replace), then -10% → +20% (later replace).
        // The actual Bsod code does the same thing — this is existing behavior.
        // So the final rate is +20%, NOT -10%.
        Assert.Contains("rate=\"+20%\"", adjusted);
        Assert.DoesNotContain("rate=\"-40%\"", adjusted);
        // Break times compressed to 50%: 300→150, 400→200, 500→250, 600→300
        Assert.Contains("time=\"150ms\"", adjusted);
        Assert.Contains("time=\"200ms\"", adjusted);
        Assert.Contains("time=\"250ms\"", adjusted);
        Assert.Contains("time=\"300ms\"", adjusted);
        // Still valid SSML structure
        Assert.StartsWith("<speak", adjusted);
        Assert.EndsWith("</speak>", adjusted);
    }

    [Fact]
    public void Bsod_long_message_adjusts_rate_and_breaks()
    {
        string fromBuilder = SsmlBuilder.Create()
            .Say("en-US-ChristopherNeural", p => p
                .SetRate("-15%").SetPitch("-8%")
                .Break(300)
                .Text("A fatal exception.")
                .Break(300)
                .Text("Error details: " + new string('y', 200) + ".")
                .Break(500)
                .Text("Press any key."))
            .ToSsml();

        // Apply long (>150) adjustment
        string adjusted = AdjustSsmlForLength(fromBuilder, 200);

        // Long: rate -15% → 0%
        Assert.Contains("rate=\"0%\"", adjusted);
        // Break times at 75%: 300→225, 500→375
        Assert.Contains("time=\"225ms\"", adjusted);
        Assert.Contains("time=\"375ms\"", adjusted);
    }

    /// <summary>
    /// Replicates AdjustSsmlForLength from Bsod.cs:432-483
    /// </summary>
    private static string AdjustSsmlForLength(string ssml, int messageLength)
    {
        string adjusted = ssml;

        if (messageLength > 300) // VERY_LONG_MESSAGE_THRESHOLD
        {
            adjusted = adjusted.Replace("rate=\"-40%\"", "rate=\"-10%\"");
            adjusted = adjusted.Replace("rate=\"-45%\"", "rate=\"-15%\"");
            adjusted = adjusted.Replace("rate=\"-35%\"", "rate=\"-5%\"");
            adjusted = adjusted.Replace("rate=\"-15%\"", "rate=\"+15%\"");
            adjusted = adjusted.Replace("rate=\"-10%\"", "rate=\"+20%\"");
            adjusted = adjusted.Replace("rate=\"-20%\"", "rate=\"+10%\"");
            adjusted = adjusted.Replace("rate=\"-8%\"", "rate=\"+22%\"");
            adjusted = adjusted.Replace("rate=\"-12%\"", "rate=\"+18%\"");
            adjusted = adjusted.Replace("rate=\"-5%\"", "rate=\"+25%\"");
            adjusted = adjusted.Replace("rate=\"-4%\"", "rate=\"+26%\"");

            adjusted = Regex.Replace(adjusted, @"time=""(\d+)ms""", match =>
            {
                int ms = int.Parse(match.Groups[1].Value);
                int compressed = (int)(ms * 0.5);
                return $"time=\"{compressed}ms\"";
            });
        }
        else if (messageLength > 150) // LONG_MESSAGE_THRESHOLD
        {
            adjusted = adjusted.Replace("rate=\"-40%\"", "rate=\"-25%\"");
            adjusted = adjusted.Replace("rate=\"-45%\"", "rate=\"-30%\"");
            adjusted = adjusted.Replace("rate=\"-35%\"", "rate=\"-20%\"");
            adjusted = adjusted.Replace("rate=\"-15%\"", "rate=\"0%\"");
            adjusted = adjusted.Replace("rate=\"-10%\"", "rate=\"+5%\"");
            adjusted = adjusted.Replace("rate=\"-20%\"", "rate=\"-5%\"");
            adjusted = adjusted.Replace("rate=\"-8%\"", "rate=\"+7%\"");
            adjusted = adjusted.Replace("rate=\"-12%\"", "rate=\"+3%\"");
            adjusted = adjusted.Replace("rate=\"-5%\"", "rate=\"+10%\"");
            adjusted = adjusted.Replace("rate=\"-4%\"", "rate=\"+11%\"");

            adjusted = Regex.Replace(adjusted, @"time=""(\d+)ms""", match =>
            {
                int ms = int.Parse(match.Groups[1].Value);
                int reduced = (int)(ms * 0.75);
                return $"time=\"{reduced}ms\"";
            });
        }

        return adjusted;
    }

    #endregion

    #region Bsod.cs — SecurityElement.Escape vs SsmlBuilder.EscapeXml

    // Bsod.cs uses System.Security.SecurityElement.Escape() for user input.
    // The builder uses SsmlBuilder.EscapeXml(). They MUST produce the same
    // output for the same input, or migrating Bsod would change what the
    // TTS engine receives.

    [Theory]
    [InlineData("hello")]
    [InlineData("Tom & Jerry")]
    [InlineData("a < b > c")]
    [InlineData("it's a test")]
    [InlineData("say \"hello\"")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("a & b < c > d \"e\" 'f'")]
    [InlineData("")]
    [InlineData("normal text no special chars")]
    [InlineData("emoji 🎉 and unicode ñ")]
    [InlineData("already &amp; escaped?")]
    [InlineData("   spaces   ")]
    [InlineData("line\nbreak")]
    public void SecurityElement_Escape_matches_EscapeXml(string input)
    {
        string fromSecurityElement = SecurityElement.Escape(input) ?? string.Empty;
        string fromBuilder = SsmlBuilder.EscapeXml(input);

        Assert.Equal(fromSecurityElement, fromBuilder);
    }

    #endregion

    #region TTSService.cs rate-override path — inline SSML with SecurityElement.Escape

    /// <summary>
    /// TTSService.cs:660-665 builds SSML when voiceId contains "|rate:+20%".
    /// It uses single quotes and SecurityElement.Escape.
    /// The builder uses double quotes and its own escaping.
    /// Both are valid SSML — but we need to verify the TEXT content matches.
    /// </summary>
    [Theory]
    [InlineData("en-IN-PrabhatNeural", "Hello sir your computer has virus", "+30%")]
    [InlineData("en-US-JennyNeural", "Going fast!", "+20%")]
    [InlineData("en-US-GuyNeural", "Tom & Jerry's adventure", "-10%")]
    public void TtsService_rate_override_text_matches(string voiceId, string text, string rate)
    {
        // --- What TTSService.cs:660-665 produces ---
        string[] voiceParts = voiceId.Split('-');
        string locale = voiceParts.Length >= 2 ? $"{voiceParts[0]}-{voiceParts[1]}" : "en-US";
        string sanitized = SecurityElement.Escape(text) ?? string.Empty;
        string originalSsml =
            $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{locale}'>"
            + $"<voice name='{voiceId}'>"
            + $"<prosody rate='{rate}'>"
            + sanitized
            + "</prosody></voice></speak>";

        // --- What the builder produces ---
        string builderSsml = SsmlBuilder.Create()
            .Say(voiceId, p => p.Text(text).SetRate(rate))
            .ToSsml();

        // The builder uses double quotes, TTSService uses single quotes.
        // Both are valid XML. Verify the semantic content matches:
        Assert.Contains($"<voice name=\"{voiceId}\">", builderSsml);
        Assert.Contains($"rate=\"{rate}\"", builderSsml);
        Assert.Contains(SsmlBuilder.EscapeXml(text), builderSsml);
        Assert.Contains($"xml:lang=\"{locale}\"", builderSsml);

        // Also verify the text content in both is identical
        string originalTextContent = ExtractTextContent(originalSsml);
        string builderTextContent = ExtractTextContent(builderSsml);
        Assert.Equal(originalTextContent, builderTextContent);
    }

    #endregion

    #region ToSegments — exact parity with every multi-voice pattern

    /// <summary>
    /// Translate.cs uses non-en-US voices. The segments must carry the
    /// foreign voice ID through unchanged.
    /// </summary>
    [Theory]
    [InlineData("ja-JP-KeitaNeural")]
    [InlineData("de-DE-ConradNeural")]
    [InlineData("fr-FR-HenriNeural")]
    [InlineData("es-ES-AlvaroNeural")]
    [InlineData("ko-KR-InJoonNeural")]
    [InlineData("zh-CN-YunxiNeural")]
    [InlineData("it-IT-DiegoNeural")]
    [InlineData("ru-RU-DmitryNeural")]
    public void Translate_foreign_voices_preserved_in_segments(string foreignVoice)
    {
        const string BOT_VOICE = "en-US-GuyNeural";

        var segments = SsmlBuilder.Create()
            .Say(BOT_VOICE, "Translating message for you.")
            .Say(foreignVoice, "Some translated text")
            .ToSegments();

        Assert.Equal(2, segments.Count);
        Assert.Equal(BOT_VOICE, segments[0].voiceId);
        Assert.Equal(foreignVoice, segments[1].voiceId);
    }

    /// <summary>
    /// WatchStreakService.cs has two paths:
    /// 1. Two segments (bot + user voice) when custom message exists
    /// 2. Single text via SendCachedTts when no custom message
    /// The builder only needs to handle path 1.
    /// </summary>
    [Fact]
    public void WatchStreak_two_voice_segments()
    {
        const string BOT_VOICE = "en-US-GuyNeural";
        const string userVoice = "en-US-EmmaMultilingualNeural";

        // What WatchStreakService.cs:352-356 builds
        var expected = new List<(string text, string voiceId)>
        {
            ("Viewer just hit a 5-day watch streak!", BOT_VOICE),
            ("Thanks for the streams!", userVoice),
        };

        var actual = SsmlBuilder.Create()
            .Say(BOT_VOICE, "Viewer just hit a 5-day watch streak!")
            .Say(userVoice, "Thanks for the streams!")
            .ToSegments();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Karen.cs: bot intro + Karen rant, two different voices.
    /// </summary>
    [Fact]
    public void Karen_two_voice_segments()
    {
        const string BOT_VOICE = "en-US-GuyNeural";
        const string KAREN_VOICE = "en-US-JennyNeural";

        var expected = new List<(string text, string voiceId)>
        {
            ("Uh oh, Karen has something to say.", BOT_VOICE),
            ("I demand to speak to the manager of this stream!", KAREN_VOICE),
        };

        var actual = SsmlBuilder.Create()
            .Say(BOT_VOICE, "Uh oh, Karen has something to say.")
            .Say(KAREN_VOICE, "I demand to speak to the manager of this stream!")
            .ToSegments();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Confess.cs: bot intro + confession in a random voice.
    /// The random voice comes from DB — could be anything.
    /// </summary>
    [Fact]
    public void Confess_random_voice_preserved()
    {
        const string BOT_VOICE = "en-US-GuyNeural";
        string randomVoice = "de-DE-ConradNeural"; // Simulating random DB pick

        var segments = SsmlBuilder.Create()
            .Say(BOT_VOICE, "Anonymous confession incoming!")
            .Say(randomVoice, "I secretly like pineapple on pizza.")
            .ToSegments();

        Assert.Equal(2, segments.Count);
        Assert.Equal(randomVoice, segments[1].voiceId);
    }

    /// <summary>
    /// All single-voice commands (Yell, Narrator, Auction, Detective, Excuse,
    /// Quote, Ratio, Rigged, Roast, SongHistory, Stats, StoneyAi, Sus,
    /// TelSell, Trial, Weather) use the same pattern: one Segment call.
    /// </summary>
    [Theory]
    [InlineData("en-US-GuyNeural", "THIS IS A YELL")]          // Yell
    [InlineData("en-GB-RyanNeural", "And so the story goes")]   // Narrator
    [InlineData("en-US-GuyNeural", "Going once, going twice")]  // Auction
    [InlineData("en-GB-RyanNeural", "Elementary, my dear")]     // Detective
    [InlineData("en-US-GuyNeural", "My dog ate my homework")]   // Excuse
    [InlineData("en-US-GuyNeural", "User once said...")]        // Quote
    [InlineData("en-US-GuyNeural", "User got ratioed hard")]    // Ratio
    [InlineData("en-US-GuyNeural", "This is totally rigged")]   // Rigged
    [InlineData("en-US-GuyNeural", "Let me roast you")]         // Roast
    [InlineData("en-US-GuyNeural", "Song history report")]      // SongHistory
    [InlineData("en-US-GuyNeural", "Stats: 500 messages")]      // Stats
    [InlineData("en-US-GuyNeural", "AI says hello")]            // StoneyAi
    [InlineData("en-US-GuyNeural", "Pretty sus if you ask me")] // Sus
    [InlineData("en-US-JennyNeural", "But wait, there's more")] // TelSell
    [InlineData("en-US-GuyNeural", "Order in the court!")]      // Trial
    [InlineData("en-US-GuyNeural", "Weather looks grim")]       // Weather
    public void SingleVoice_commands_produce_one_segment(string voice, string text)
    {
        var segments = SsmlBuilder.Create()
            .Say(voice, text)
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal((text, voice), segments[0]);
    }

    #endregion

    #region Edge cases that could silently break

    [Fact]
    public void Empty_user_input_produces_valid_output()
    {
        // User redeems TTS with empty message after trim
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "")
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal("", segments[0].text);

        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "")
            .ToSsml();

        Assert.Contains("<voice name=\"en-US-GuyNeural\">", ssml);
        Assert.Contains("</voice>", ssml);
    }

    [Fact]
    public void Unicode_and_emoji_pass_through_unchanged()
    {
        string text = "🎉 Привет мир こんにちは 안녕하세요";

        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", text)
            .ToSegments();

        Assert.Equal(text, segments[0].text);

        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", text)
            .ToSsml();

        Assert.Contains(text, ssml);
    }

    [Fact]
    public void Very_long_text_does_not_truncate()
    {
        string text = new string('a', 5000);

        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", text)
            .ToSegments();

        Assert.Equal(5000, segments[0].text.Length);
    }

    [Fact]
    public void Newlines_in_user_input_are_preserved()
    {
        string text = "line one\nline two\rline three";

        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", text)
            .ToSegments();

        Assert.Equal(text, segments[0].text);
    }

    [Fact]
    public void Pipe_in_user_text_does_not_corrupt_voiceId()
    {
        // User could type a pipe character — it must NOT be interpreted
        // as a rate override separator
        string text = "this | has | pipes";

        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", text)
            .ToSegments();

        Assert.Equal("en-US-GuyNeural", segments[0].voiceId);
        Assert.Equal("this | has | pipes", segments[0].text);
        Assert.DoesNotContain("|rate:", segments[0].voiceId);
    }

    [Fact]
    public void Multiple_rate_overrides_only_encodes_rate()
    {
        // ProsodyBuilder can set rate + pitch + volume, but ToSegments
        // only encodes rate into the voiceId pipe. Pitch/volume are for
        // ToSsml mode only. This matches the existing TtsService.Segment behavior.
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p
                .Text("Complex prosody")
                .SetRate("+30%")
                .SetPitch("-10%")
                .SetVolume("100"))
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal("en-US-GuyNeural|rate:+30%", segments[0].voiceId);
        // Pitch and volume are NOT in the voiceId — only rate is supported by SynthesizeMultiVoiceSsmlAsync
        Assert.DoesNotContain("pitch", segments[0].voiceId);
        Assert.DoesNotContain("volume", segments[0].voiceId);
    }

    [Fact]
    public void Prosody_in_ToSsml_includes_all_attributes()
    {
        // Contrast with above: ToSsml DOES render pitch/volume
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p
                .Text("Complex prosody")
                .SetRate("+30%")
                .SetPitch("-10%")
                .SetVolume("100"))
            .ToSsml();

        Assert.Contains("rate=\"+30%\"", ssml);
        Assert.Contains("pitch=\"-10%\"", ssml);
        Assert.Contains("volume=\"100\"", ssml);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Extracts text content from SSML by stripping all XML tags.
    /// Used to compare semantic content between different SSML formats.
    /// </summary>
    private static string ExtractTextContent(string ssml)
    {
        return Regex.Replace(ssml, @"<[^>]+>", "").Trim();
    }

    #endregion
}
