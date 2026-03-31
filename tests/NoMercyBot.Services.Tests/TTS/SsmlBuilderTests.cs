using NoMercyBot.Services.TTS;
using Xunit;

namespace NoMercyBot.Services.Tests.TTS;

public class SsmlBuilderTests
{
    private const string SpeakOpen =
        "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\" xmlns:mstts=\"https://www.w3.org/2001/mstts\">";

    private const string SpeakClose = "</speak>";

    #region ToSsml — basic

    [Fact]
    public void Empty_builder_returns_empty_speak_element()
    {
        string ssml = SsmlBuilder.Create().ToSsml();

        Assert.Equal($"{SpeakOpen}{SpeakClose}", ssml);
    }

    [Fact]
    public void Single_voice_plain_text()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-JennyNeural", "Hello world")
            .ToSsml();

        Assert.Equal(
            $"{SpeakOpen}<voice name=\"en-US-JennyNeural\">Hello world</voice>{SpeakClose}",
            ssml);
    }

    [Fact]
    public void Multiple_segments_same_voice_shares_voice_tag()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "Part one")
            .Say("en-US-GuyNeural", "Part two")
            .ToSsml();

        Assert.Equal(
            $"{SpeakOpen}<voice name=\"en-US-GuyNeural\">Part onePart two</voice>{SpeakClose}",
            ssml);
    }

    [Fact]
    public void Multiple_segments_different_voices_get_separate_tags()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "Hello")
            .Say("en-US-JennyNeural", "World")
            .ToSsml();

        Assert.Equal(
            $"{SpeakOpen}" +
            "<voice name=\"en-US-GuyNeural\">Hello</voice>" +
            "<voice name=\"en-US-JennyNeural\">World</voice>" +
            $"{SpeakClose}",
            ssml);
    }

    [Fact]
    public void Voice_switching_back_and_forth_creates_correct_tags()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "A")
            .Say("en-US-JennyNeural", "B")
            .Say("en-US-GuyNeural", "C")
            .ToSsml();

        Assert.Equal(
            $"{SpeakOpen}" +
            "<voice name=\"en-US-GuyNeural\">A</voice>" +
            "<voice name=\"en-US-JennyNeural\">B</voice>" +
            "<voice name=\"en-US-GuyNeural\">C</voice>" +
            $"{SpeakClose}",
            ssml);
    }

    #endregion

    #region ToSsml — silence / breaks

    [Fact]
    public void Silence_between_voices_becomes_break_element()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "Before")
            .Silence(500)
            .Say("en-US-GuyNeural", "After")
            .ToSsml();

        Assert.Contains("<break time=\"500ms\"/>", ssml);
    }

    [Fact]
    public void Silence_at_start_renders_break_before_any_voice()
    {
        string ssml = SsmlBuilder.Create()
            .Silence(300)
            .Say("en-US-GuyNeural", "Hello")
            .ToSsml();

        Assert.StartsWith($"{SpeakOpen}<break time=\"300ms\"/>", ssml);
    }

    [Fact]
    public void Multiple_silences_all_render()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "A")
            .Silence(100)
            .Silence(200)
            .Say("en-US-GuyNeural", "B")
            .ToSsml();

        Assert.Contains("<break time=\"100ms\"/>", ssml);
        Assert.Contains("<break time=\"200ms\"/>", ssml);
    }

    #endregion

    #region ToSsml — prosody

    [Fact]
    public void Prosody_with_rate_wraps_in_prosody_tag()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p.Text("Slow speech").SetRate("-40%"))
            .ToSsml();

        Assert.Contains("<prosody rate=\"-40%\">Slow speech</prosody>", ssml);
    }

    [Fact]
    public void Prosody_with_all_attributes()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p
                .Text("Full config")
                .SetRate("-10%")
                .SetPitch("-5%")
                .SetVolume("100"))
            .ToSsml();

        Assert.Contains("volume=\"100\"", ssml);
        Assert.Contains("pitch=\"-5%\"", ssml);
        Assert.Contains("rate=\"-10%\"", ssml);
        Assert.Contains("Full config", ssml);
    }

    [Fact]
    public void Prosody_with_breaks_between_text()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-SteffanNeural", p => p
                .SetRate("-40%").SetPitch("-10%")
                .Break(300)
                .Text("Fatal. Exception.")
                .Break(400)
                .Text("System error."))
            .ToSsml();

        Assert.Contains("<break time=\"300ms\"/>", ssml);
        Assert.Contains("Fatal. Exception.", ssml);
        Assert.Contains("<break time=\"400ms\"/>", ssml);
        Assert.Contains("System error.", ssml);
        Assert.Contains("rate=\"-40%\"", ssml);
        Assert.Contains("pitch=\"-10%\"", ssml);
    }

    [Fact]
    public void Prosody_with_style_wraps_in_express_as()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p.Text("I AM ANGRY").SetStyle("shouting"))
            .ToSsml();

        Assert.Contains("<mstts:express-as style=\"shouting\">I AM ANGRY</mstts:express-as>", ssml);
    }

    [Fact]
    public void Prosody_with_style_and_rate_nests_correctly()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p
                .Text("Angry and slow")
                .SetStyle("shouting")
                .SetRate("-20%"))
            .ToSsml();

        // Style should be inside prosody
        Assert.Contains("<prosody rate=\"-20%\"><mstts:express-as style=\"shouting\">Angry and slow</mstts:express-as></prosody>", ssml);
    }

    #endregion

    #region ToSsml — XML escaping

    [Fact]
    public void Special_characters_are_escaped_in_text()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-JennyNeural", "Tom & Jerry <3 \"quotes\" and 'apostrophes'")
            .ToSsml();

        Assert.Contains("Tom &amp; Jerry &lt;3 &quot;quotes&quot; and &apos;apostrophes&apos;", ssml);
        // Ensure the SSML is valid (no raw < > & in text)
        Assert.DoesNotContain("<3", ssml);
    }

    [Fact]
    public void Special_characters_escaped_in_prosody_text()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p.Text("A < B & C > D").SetRate("+10%"))
            .ToSsml();

        Assert.Contains("A &lt; B &amp; C &gt; D", ssml);
    }

    [Fact]
    public void Empty_text_produces_valid_ssml()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-JennyNeural", "")
            .ToSsml();

        Assert.Contains("<voice name=\"en-US-JennyNeural\">", ssml);
        Assert.Contains("</voice>", ssml);
    }

    #endregion

    #region ToSegments — basic

    [Fact]
    public void Single_segment_produces_correct_tuple()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "Hello")
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal("Hello", segments[0].text);
        Assert.Equal("en-US-GuyNeural", segments[0].voiceId);
    }

    [Fact]
    public void Multiple_segments_produces_correct_tuples()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "Intro")
            .Say("en-US-JennyNeural", "Message")
            .ToSegments();

        Assert.Equal(2, segments.Count);
        Assert.Equal(("Intro", "en-US-GuyNeural"), segments[0]);
        Assert.Equal(("Message", "en-US-JennyNeural"), segments[1]);
    }

    [Fact]
    public void Silence_segment_uses_silence_voiceId_convention()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "Before")
            .Silence(600)
            .Say("en-US-GuyNeural", "After")
            .ToSegments();

        Assert.Equal(3, segments.Count);
        Assert.Equal(("600", "silence"), segments[1]);
    }

    [Fact]
    public void Rate_override_encodes_into_voiceId_pipe()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-IN-PrabhatNeural", p => p.Text("Fast speech").SetRate("+30%"))
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal("Fast speech", segments[0].text);
        Assert.Equal("en-IN-PrabhatNeural|rate:+30%", segments[0].voiceId);
    }

    [Fact]
    public void Plain_text_segment_has_no_pipe_in_voiceId()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "No rate")
            .ToSegments();

        Assert.DoesNotContain("|", segments[0].voiceId);
    }

    [Fact]
    public void Prosody_text_with_breaks_concatenates_text_only()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p
                .Text("Part one")
                .Break(300)
                .Text("Part two"))
            .ToSegments();

        // Breaks are stripped in segments mode (they only matter in SSML mode)
        Assert.Equal("Part one Part two", segments[0].text);
    }

    [Fact]
    public void Empty_builder_produces_empty_segments()
    {
        var segments = SsmlBuilder.Create().ToSegments();

        Assert.Empty(segments);
    }

    #endregion

    #region Count property

    [Fact]
    public void Count_reflects_added_segments()
    {
        var builder = SsmlBuilder.Create()
            .Say("voice1", "text1")
            .Silence(100)
            .Say("voice2", "text2");

        Assert.Equal(3, builder.Count);
    }

    [Fact]
    public void Count_is_zero_for_new_builder()
    {
        Assert.Equal(0, SsmlBuilder.Create().Count);
    }

    #endregion

    #region EscapeXml static method

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("a < b", "a &lt; b")]
    [InlineData("a > b", "a &gt; b")]
    [InlineData("a \"b\" c", "a &quot;b&quot; c")]
    [InlineData("a 'b' c", "a &apos;b&apos; c")]
    [InlineData("", "")]
    [InlineData("no special chars", "no special chars")]
    [InlineData("&<>\"'", "&amp;&lt;&gt;&quot;&apos;")]
    public void EscapeXml_handles_all_special_characters(string input, string expected)
    {
        Assert.Equal(expected, SsmlBuilder.EscapeXml(input));
    }

    [Fact]
    public void EscapeXml_null_returns_empty()
    {
        Assert.Equal(string.Empty, SsmlBuilder.EscapeXml(null!));
    }

    #endregion

    #region Real-world scenario tests (matching existing codebase patterns)

    /// <summary>
    /// Matches the pattern used by Yell.cs — single voice, single segment.
    /// </summary>
    [Fact]
    public void Scenario_Yell_single_voice()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "THIS IS LOUD")
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal("THIS IS LOUD", segments[0].text);
        Assert.Equal("en-US-GuyNeural", segments[0].voiceId);
    }

    /// <summary>
    /// Matches Whisper.cs / Slow.cs — bot intro + user message.
    /// </summary>
    [Fact]
    public void Scenario_Whisper_two_voices()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-EmmaMultilingualNeural", "User whispers:")
            .Say("en-GB-RyanNeural", "I have a secret")
            .ToSegments();

        Assert.Equal(2, segments.Count);
        Assert.Equal("en-US-EmmaMultilingualNeural", segments[0].voiceId);
        Assert.Equal("en-GB-RyanNeural", segments[1].voiceId);
    }

    /// <summary>
    /// Matches Dramatic.cs — intro, silence, chunked words with silences.
    /// </summary>
    [Fact]
    public void Scenario_Dramatic_chunked_with_silences()
    {
        var builder = SsmlBuilder.Create()
            .Say("en-US-EmmaMultilingualNeural", "And the message is...")
            .Silence(600);

        string[] chunks = ["The quick", "brown fox", "jumps"];
        foreach (string chunk in chunks)
        {
            builder.Say("en-US-GuyNeural", chunk).Silence(500);
        }

        var segments = builder.ToSegments();

        // intro + silence + (3 chunks * 2 each) = 8 segments
        Assert.Equal(8, segments.Count);
        Assert.Equal(("And the message is...", "en-US-EmmaMultilingualNeural"), segments[0]);
        Assert.Equal(("600", "silence"), segments[1]);
        Assert.Equal(("The quick", "en-US-GuyNeural"), segments[2]);
        Assert.Equal(("500", "silence"), segments[3]);
    }

    /// <summary>
    /// Matches Scam.cs — intro + message with rate override.
    /// </summary>
    [Fact]
    public void Scenario_Scam_with_rate_override()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-EmmaMultilingualNeural", "Incoming scam call!")
            .Say("en-IN-PrabhatNeural", p => p.Text("Hello sir your computer has virus").SetRate("+30%"))
            .ToSegments();

        Assert.Equal(2, segments.Count);
        Assert.Equal("en-US-EmmaMultilingualNeural", segments[0].voiceId);
        Assert.Equal("en-IN-PrabhatNeural|rate:+30%", segments[1].voiceId);
        Assert.Equal("Hello sir your computer has virus", segments[1].text);
    }

    /// <summary>
    /// Matches Mock.cs — three segments (intro + mocked text + outro).
    /// </summary>
    [Fact]
    public void Scenario_Mock_three_segments()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-EmmaMultilingualNeural", "User said:")
            .Say("en-US-JennyNeural", "hElLo WoRlD")
            .Say("en-US-EmmaMultilingualNeural", "End quote.")
            .ToSegments();

        Assert.Equal(3, segments.Count);
        Assert.Equal("End quote.", segments[2].text);
    }

    /// <summary>
    /// Matches DjVoice.cs — three segments all same voice.
    /// </summary>
    [Fact]
    public void Scenario_DjVoice_three_same_voice()
    {
        const string DJ = "en-US-GuyNeural";
        var segments = SsmlBuilder.Create()
            .Say(DJ, "DJ NoMercy in the house!")
            .Say(DJ, "User wants to hear some beats")
            .Say(DJ, "That was fire!")
            .ToSegments();

        Assert.Equal(3, segments.Count);
        Assert.All(segments, s => Assert.Equal(DJ, s.voiceId));
    }

    /// <summary>
    /// Matches Bsod.cs Win31 template — full SSML with prosody and breaks.
    /// </summary>
    [Fact]
    public void Scenario_Bsod_win31_template_as_ssml()
    {
        string username = "TestUser";
        string message = "something broke";

        string ssml = SsmlBuilder.Create()
            .Say("en-US-SteffanNeural", p => p
                .SetRate("-40%").SetPitch("-10%")
                .Break(300)
                .Text("Fatal. Exception.")
                .Break(400)
                .Text($"User {username} has caused. A. System. Malfunction.")
                .Break(500)
                .Text($"Error message: {message}.")
                .Break(600)
                .Text("Press. Control. Alt. Delete. To pretend. This never happened."))
            .ToSsml();

        // Verify structure
        Assert.Contains("<voice name=\"en-US-SteffanNeural\">", ssml);
        Assert.Contains("rate=\"-40%\"", ssml);
        Assert.Contains("pitch=\"-10%\"", ssml);
        Assert.Contains("<break time=\"300ms\"/>", ssml);
        Assert.Contains("Fatal. Exception.", ssml);
        Assert.Contains("<break time=\"400ms\"/>", ssml);
        Assert.Contains("User TestUser has caused. A. System. Malfunction.", ssml);
        Assert.Contains("<break time=\"500ms\"/>", ssml);
        Assert.Contains("Error message: something broke.", ssml);
        Assert.Contains("<break time=\"600ms\"/>", ssml);
        Assert.Contains("Press. Control. Alt. Delete.", ssml);
        Assert.StartsWith(SpeakOpen, ssml);
        Assert.EndsWith(SpeakClose, ssml);
    }

    /// <summary>
    /// Matches LuckyFeatherChange.cs — simple static announcement via ToSsml.
    /// </summary>
    [Fact]
    public void Scenario_LuckyFeather_static_announcement()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-JennyNeural", "Heads up! The Lucky Feather has appeared. Grab it if you can!")
            .ToSsml();

        Assert.Contains("<voice name=\"en-US-JennyNeural\">", ssml);
        Assert.Contains("Heads up! The Lucky Feather has appeared.", ssml);
        Assert.StartsWith(SpeakOpen, ssml);
        Assert.EndsWith(SpeakClose, ssml);
    }

    /// <summary>
    /// Verifies that BSOD-style SSML with user input containing special chars is safe.
    /// </summary>
    [Fact]
    public void Scenario_Bsod_with_malicious_input_is_escaped()
    {
        string username = "<script>alert('xss')</script>";
        string message = "a & b < c > d";

        string ssml = SsmlBuilder.Create()
            .Say("en-US-SteffanNeural", p => p
                .SetRate("-40%")
                .Text($"User {username} broke it with: {message}"))
            .ToSsml();

        Assert.DoesNotContain("<script>", ssml);
        Assert.Contains("&lt;script&gt;", ssml);
        Assert.Contains("a &amp; b &lt; c &gt; d", ssml);
    }

    /// <summary>
    /// Matches the TTSService.cs rate-override SSML building (lines 660-665).
    /// When using ToSsml, rate should produce proper prosody tags.
    /// </summary>
    [Fact]
    public void Scenario_rate_override_as_ssml()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-IN-PrabhatNeural", p => p.Text("Fast talk").SetRate("+20%"))
            .ToSsml();

        Assert.Contains("<prosody rate=\"+20%\">Fast talk</prosody>", ssml);
        Assert.Contains("<voice name=\"en-IN-PrabhatNeural\">", ssml);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Only_silences_produces_only_breaks()
    {
        string ssml = SsmlBuilder.Create()
            .Silence(100)
            .Silence(200)
            .ToSsml();

        Assert.Equal($"{SpeakOpen}<break time=\"100ms\"/><break time=\"200ms\"/>{SpeakClose}", ssml);
    }

    [Fact]
    public void Builder_is_reusable_for_both_outputs()
    {
        var builder = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", "Hello")
            .Silence(500)
            .Say("en-US-JennyNeural", "World");

        string ssml = builder.ToSsml();
        var segments = builder.ToSegments();

        // Both should work on the same builder
        Assert.Contains("<voice name=\"en-US-GuyNeural\">Hello", ssml);
        Assert.Contains("<voice name=\"en-US-JennyNeural\">World</voice>", ssml);
        Assert.Equal(3, segments.Count);
    }

    [Fact]
    public void Prosody_with_no_text_elements_produces_empty_content()
    {
        var segments = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p.SetRate("+10%"))
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal("", segments[0].text);
        Assert.Equal("en-US-GuyNeural|rate:+10%", segments[0].voiceId);
    }

    [Fact]
    public void Prosody_with_only_breaks_produces_breaks_in_ssml()
    {
        string ssml = SsmlBuilder.Create()
            .Say("en-US-GuyNeural", p => p.Break(500).Break(300))
            .ToSsml();

        Assert.Contains("<break time=\"500ms\"/>", ssml);
        Assert.Contains("<break time=\"300ms\"/>", ssml);
    }

    #endregion

    #region Locale inference

    [Fact]
    public void Locale_inferred_from_first_voice_id()
    {
        string ssml = SsmlBuilder.Create()
            .Say("ru-RU-SvetlanaNeural", "Привет мир")
            .ToSsml();

        Assert.Contains("xml:lang=\"ru-RU\"", ssml);
    }

    [Fact]
    public void Locale_defaults_to_en_US_when_no_voices()
    {
        string ssml = SsmlBuilder.Create()
            .Silence(100)
            .ToSsml();

        Assert.Contains("xml:lang=\"en-US\"", ssml);
    }

    [Fact]
    public void Locale_defaults_to_en_US_for_empty_builder()
    {
        string ssml = SsmlBuilder.Create().ToSsml();

        Assert.Contains("xml:lang=\"en-US\"", ssml);
    }

    [Fact]
    public void Locale_inferred_from_first_voice_skipping_leading_silence()
    {
        string ssml = SsmlBuilder.Create()
            .Silence(300)
            .Say("ja-JP-NanamiNeural", "こんにちは")
            .ToSsml();

        Assert.Contains("xml:lang=\"ja-JP\"", ssml);
    }

    [Fact]
    public void WithLocale_overrides_inference()
    {
        string ssml = SsmlBuilder.Create()
            .WithLocale("fr-FR")
            .Say("en-US-JennyNeural", "Bonjour")
            .ToSsml();

        Assert.Contains("xml:lang=\"fr-FR\"", ssml);
        Assert.DoesNotContain("xml:lang=\"en-US\"", ssml);
    }

    [Fact]
    public void Locale_inferred_from_multilingual_voice()
    {
        string ssml = SsmlBuilder.Create()
            .Say("de-DE-ConradNeural", "Hallo Welt")
            .ToSsml();

        Assert.Contains("xml:lang=\"de-DE\"", ssml);
    }

    [Theory]
    [InlineData("en-US-GuyNeural", "en-US")]
    [InlineData("ru-RU-SvetlanaNeural", "ru-RU")]
    [InlineData("ja-JP-NanamiNeural", "ja-JP")]
    [InlineData("en-GB-RyanNeural", "en-GB")]
    [InlineData("unknown", "en-US")]
    [InlineData("x", "en-US")]
    public void ExtractLocale_parses_voice_ids(string voiceId, string expectedLocale)
    {
        Assert.Equal(expectedLocale, SsmlBuilder.ExtractLocale(voiceId));
    }

    #endregion

    #region Parity: ToSsml output matches existing hardcoded SSML sent to providers

    // These tests verify that builder.ToSsml() produces the exact same string
    // that existing code passes to provider.SynthesizeSsmlAsync().
    // Any mismatch means the migration would change what the TTS API receives.

    [Theory]
    [InlineData("Heads up! The Lucky Feather has appeared. Grab it if you can!", "en-US-JennyNeural")]
    [InlineData("Sneaky alert! The feather is free for the taking!", "en-US-JennyNeural")]
    [InlineData("Attention! The Lucky Feather is out in the wild. Go get it!", "en-US-JennyNeural")]
    [InlineData("Look alive! The Lucky Feather can be stolen now!", "en-US-JennyNeural")]
    public void Parity_LuckyFeather_found_ssml(string text, string voiceId)
    {
        // Exact string from LuckyFeatherChange.cs _featherFoundSsml
        string expected =
            $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\" xmlns:mstts=\"https://www.w3.org/2001/mstts\">"
            + $"<voice name=\"{voiceId}\">{text}</voice></speak>";

        string actual = SsmlBuilder.Create()
            .Say(voiceId, text)
            .ToSsml();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Bravo! You just stole the Lucky Feather. Enjoy your glory.", "en-US-GuyNeural")]
    [InlineData("Kudos, feather thief! You're officially the top pickpocket.", "en-US-GuyNeural")]
    [InlineData("Congratulations! The Lucky Feather now belongs to you. Smug time!", "en-US-GuyNeural")]
    [InlineData("Well played! You claimed the Lucky Feather. Bask in your victory.", "en-US-GuyNeural")]
    public void Parity_LuckyFeather_congrats_ssml(string text, string voiceId)
    {
        // Exact string from LuckyFeatherChange.cs _winnerCongratsSsml
        // Note: apostrophes in source are literal ' chars, the builder must escape them
        string expected =
            "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\" xmlns:mstts=\"https://www.w3.org/2001/mstts\">"
            + $"<voice name=\"{voiceId}\">{SsmlBuilder.EscapeXml(text)}</voice></speak>";

        string actual = SsmlBuilder.Create()
            .Say(voiceId, text)
            .ToSsml();

        Assert.Equal(expected, actual);
    }

    #endregion

    #region Parity: ToSegments output matches TtsService.Segment / Silence

    // These tests verify that builder.ToSegments() produces the exact same
    // List<(string, string)> that the existing TtsService.Segment() / Silence()
    // static methods produce. This is the contract for SynthesizeMultiVoiceSsmlAsync.

    [Theory]
    [InlineData("en-US-GuyNeural", "Hello world")]
    [InlineData("en-US-JennyNeural", "Some text with special chars: & < >")]
    [InlineData("ru-RU-SvetlanaNeural", "Привет мир")]
    [InlineData("en-GB-RyanNeural", "")]
    public void Parity_Segment_plain_text(string voiceId, string text)
    {
        // TtsService.Segment(voiceId, text) returns (text, voiceId)
        (string expectedText, string expectedVoice) = (text, voiceId);

        var segments = SsmlBuilder.Create()
            .Say(voiceId, text)
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal(expectedText, segments[0].text);
        Assert.Equal(expectedVoice, segments[0].voiceId);
    }

    [Theory]
    [InlineData("en-IN-PrabhatNeural", "Fast talk", "+30%")]
    [InlineData("en-US-GuyNeural", "Slow speech", "-40%")]
    [InlineData("en-US-JennyNeural", "Normal", "+0%")]
    public void Parity_Segment_with_rate(string voiceId, string text, string rate)
    {
        // TtsService.Segment(voiceId, text, rate) returns (text, "{voiceId}|rate:{rate}")
        string expectedVoice = $"{voiceId}|rate:{rate}";

        var segments = SsmlBuilder.Create()
            .Say(voiceId, p => p.Text(text).SetRate(rate))
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal(text, segments[0].text);
        Assert.Equal(expectedVoice, segments[0].voiceId);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(600)]
    [InlineData(1000)]
    public void Parity_Silence(int ms)
    {
        // TtsService.Silence(ms) returns (ms.ToString(), "silence")
        var segments = SsmlBuilder.Create()
            .Silence(ms)
            .ToSegments();

        Assert.Single(segments);
        Assert.Equal(ms.ToString(), segments[0].text);
        Assert.Equal("silence", segments[0].voiceId);
    }

    /// <summary>
    /// Exact parity with Whisper.cs: bot intro + user message segments.
    /// </summary>
    [Fact]
    public void Parity_Whisper_segments()
    {
        const string BOT_VOICE = "en-US-EmmaMultilingualNeural";
        const string userVoice = "en-GB-RyanNeural";

        // What TtsService.Segment produces:
        var expected = new List<(string text, string voiceId)>
        {
            ("User whispers:", BOT_VOICE),
            ("I have a secret", userVoice),
        };

        var actual = SsmlBuilder.Create()
            .Say(BOT_VOICE, "User whispers:")
            .Say(userVoice, "I have a secret")
            .ToSegments();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Exact parity with Scam.cs: intro + scammer voice with rate override.
    /// </summary>
    [Fact]
    public void Parity_Scam_segments()
    {
        const string BOT_VOICE = "en-US-EmmaMultilingualNeural";
        const string SCAMMER_VOICE = "en-IN-PrabhatNeural";

        // What TtsService.Segment produces:
        var expected = new List<(string text, string voiceId)>
        {
            ("Incoming scam call!", BOT_VOICE),
            ("Hello sir your computer has virus", "en-IN-PrabhatNeural|rate:+30%"),
        };

        var actual = SsmlBuilder.Create()
            .Say(BOT_VOICE, "Incoming scam call!")
            .Say(SCAMMER_VOICE, p => p.Text("Hello sir your computer has virus").SetRate("+30%"))
            .ToSegments();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Exact parity with Mock.cs: intro + mocked + "End quote." (three segments, two voices).
    /// </summary>
    [Fact]
    public void Parity_Mock_segments()
    {
        const string BOT_VOICE = "en-US-EmmaMultilingualNeural";
        const string MOCK_VOICE = "en-US-JennyNeural";

        var expected = new List<(string text, string voiceId)>
        {
            ("User said:", BOT_VOICE),
            ("hElLo WoRlD", MOCK_VOICE),
            ("End quote.", BOT_VOICE),
        };

        var actual = SsmlBuilder.Create()
            .Say(BOT_VOICE, "User said:")
            .Say(MOCK_VOICE, "hElLo WoRlD")
            .Say(BOT_VOICE, "End quote.")
            .ToSegments();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Exact parity with Dramatic.cs: intro, silence, chunked words with silences.
    /// </summary>
    [Fact]
    public void Parity_Dramatic_segments()
    {
        const string BOT_VOICE = "en-US-EmmaMultilingualNeural";
        const string userVoice = "en-US-GuyNeural";

        // Simulate what Dramatic.cs builds with TtsService.Segment/Silence
        var expected = new List<(string text, string voiceId)>
        {
            ("And the message is...", BOT_VOICE),
            ("600", "silence"),
            ("The quick brown", userVoice),
            ("500", "silence"),
            ("fox jumps over", userVoice),
            ("500", "silence"),
            ("the lazy dog", userVoice),
        };

        var actual = SsmlBuilder.Create()
            .Say(BOT_VOICE, "And the message is...")
            .Silence(600)
            .Say(userVoice, "The quick brown")
            .Silence(500)
            .Say(userVoice, "fox jumps over")
            .Silence(500)
            .Say(userVoice, "the lazy dog")
            .ToSegments();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Exact parity with DjVoice.cs: three segments same voice.
    /// </summary>
    [Fact]
    public void Parity_DjVoice_segments()
    {
        const string DJ_VOICE = "en-US-GuyNeural";

        var expected = new List<(string text, string voiceId)>
        {
            ("DJ NoMercy in the house!", DJ_VOICE),
            ("Someone wants to hear some beats!", DJ_VOICE),
            ("That was fire! DJ NoMercy signing off.", DJ_VOICE),
        };

        var actual = SsmlBuilder.Create()
            .Say(DJ_VOICE, "DJ NoMercy in the house!")
            .Say(DJ_VOICE, "Someone wants to hear some beats!")
            .Say(DJ_VOICE, "That was fire! DJ NoMercy signing off.")
            .ToSegments();

        Assert.Equal(expected, actual);
    }

    #endregion

    #region Parity: XML escaping matches TtsProviderBase.SanitizeText

    // TtsProviderBase.SanitizeText replaces: " → &quot;  & → &amp;  ' → &apos;  < → &lt;  > → &gt;
    // The builder's EscapeXml must produce the same output for the same input.
    // Note: SanitizeText replaces " before &, which would double-encode &quot;
    // if the input contains ". The builder does & first (correct XML order).
    // This test documents where the builder FIXES a bug in the old code.

    [Fact]
    public void Parity_EscapeXml_matches_SanitizeText_for_common_inputs()
    {
        // Inputs that don't contain " followed by & (where the bug would differ)
        string[] inputs =
        [
            "Hello world",
            "Tom & Jerry",
            "a < b > c",
            "it's fine",
            "",
            "no special chars here",
            "multiple & ampersands & here",
            "<script>alert('xss')</script>",
        ];

        foreach (string input in inputs)
        {
            // Replicate SanitizeText logic (same order as TtsProviderBase)
            string sanitized = input
                .Replace("\"", "&quot;")
                .Replace("&", "&amp;")
                .Replace("'", "&apos;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");

            string builderResult = SsmlBuilder.EscapeXml(input);

            Assert.Equal(sanitized, builderResult);
        }
    }

    [Fact]
    public void EscapeXml_ampersand_first_prevents_double_encoding()
    {
        // The builder escapes & first, which is the correct XML escaping order.
        // Input with a quote: the old SanitizeText would turn " into &quot;
        // then & into &amp; producing &amp;quot; (double-encoded).
        // The builder does & first → &amp; then " → &quot; (correct).
        string input = "He said \"hello\" & left";
        string result = SsmlBuilder.EscapeXml(input);

        Assert.Equal("He said &quot;hello&quot; &amp; left", result);
        Assert.DoesNotContain("&amp;quot;", result);
    }

    #endregion
}
