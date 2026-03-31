using System.Text;

namespace NoMercyBot.Services.TTS;

/// <summary>
/// Fluent builder for constructing TTS SSML markup and multi-voice segment lists.
/// Replaces all ad-hoc SSML string construction across the codebase.
/// </summary>
public sealed class SsmlBuilder
{
    private const string DefaultLocale = "en-US";
    private readonly List<ISsmlSegment> _segments = [];
    private string? _locale;

    private SsmlBuilder() { }

    public static SsmlBuilder Create() => new();

    /// <summary>
    /// Explicitly sets the xml:lang for the SSML document.
    /// If not called, the locale is inferred from the first voice segment's ID
    /// (e.g. "ru-RU-SvetlanaNeural" → "ru-RU"), falling back to "en-US".
    /// </summary>
    public SsmlBuilder WithLocale(string locale)
    {
        _locale = locale;
        return this;
    }

    /// <summary>
    /// Adds a voice segment with plain text (no prosody overrides).
    /// </summary>
    public SsmlBuilder Say(string voiceId, string text)
    {
        _segments.Add(new VoiceSegment(voiceId, text, null));
        return this;
    }

    /// <summary>
    /// Adds a voice segment with prosody configuration.
    /// </summary>
    public SsmlBuilder Say(string voiceId, Action<ProsodyBuilder> configure)
    {
        ProsodyBuilder pb = new();
        configure(pb);
        _segments.Add(new VoiceSegment(voiceId, null, pb));
        return this;
    }

    /// <summary>
    /// Adds a silence gap (for multi-voice synthesis via FFmpeg concatenation).
    /// </summary>
    public SsmlBuilder Silence(int milliseconds)
    {
        _segments.Add(new SilenceSegment(milliseconds));
        return this;
    }

    /// <summary>
    /// Builds a single SSML document. All voice segments are rendered sequentially
    /// inside one &lt;speak&gt; element. Silence segments become &lt;break&gt; elements.
    /// Best for single-voice or when you need one SSML string.
    /// </summary>
    public string ToSsml()
    {
        string locale = ResolveLocale();

        if (_segments.Count == 0)
            return $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{EscapeXmlAttribute(locale)}\" xmlns:mstts=\"https://www.w3.org/2001/mstts\"></speak>";

        StringBuilder sb = new();
        sb.Append(
            $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"{EscapeXmlAttribute(locale)}\" xmlns:mstts=\"https://www.w3.org/2001/mstts\">"
        );

        string? currentVoice = null;

        foreach (ISsmlSegment segment in _segments)
        {
            switch (segment)
            {
                case VoiceSegment voice:
                    if (voice.VoiceId != currentVoice)
                    {
                        if (currentVoice != null)
                            sb.Append("</voice>");
                        sb.Append($"<voice name=\"{EscapeXmlAttribute(voice.VoiceId)}\">");
                        currentVoice = voice.VoiceId;
                    }

                    if (voice.Prosody != null)
                        sb.Append(voice.Prosody.ToSsmlContent());
                    else
                        sb.Append(EscapeXml(voice.PlainText ?? string.Empty));
                    break;

                case SilenceSegment silence:
                    sb.Append($"<break time=\"{silence.Milliseconds}ms\"/>");
                    break;
            }
        }

        if (currentVoice != null)
            sb.Append("</voice>");

        sb.Append("</speak>");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a list of (text, voiceId) segments compatible with
    /// TtsService.SynthesizeMultiVoiceSsmlAsync. Each voice segment becomes
    /// one entry; silence segments use the "silence" voiceId convention.
    /// Rate overrides are encoded into the voiceId via pipe separator.
    /// </summary>
    public List<(string text, string voiceId)> ToSegments()
    {
        List<(string text, string voiceId)> result = [];

        foreach (ISsmlSegment segment in _segments)
        {
            switch (segment)
            {
                case VoiceSegment voice:
                    string text = voice.PlainText ?? voice.Prosody?.GetText() ?? string.Empty;
                    string voiceId = voice.VoiceId;

                    // Encode rate into voiceId using the existing pipe convention
                    if (voice.Prosody?.Rate != null)
                        voiceId += $"|rate:{voice.Prosody.Rate}";

                    result.Add((text, voiceId));
                    break;

                case SilenceSegment silence:
                    result.Add((silence.Milliseconds.ToString(), "silence"));
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the number of segments added to this builder.
    /// </summary>
    public int Count => _segments.Count;

    /// <summary>
    /// Extracts the locale (e.g. "ru-RU") from a voice ID like "ru-RU-SvetlanaNeural".
    /// Returns "en-US" if the voice ID doesn't contain a parseable locale.
    /// </summary>
    public static string ExtractLocale(string voiceId)
    {
        string[] parts = voiceId.Split('-');
        if (parts.Length >= 2)
            return $"{parts[0]}-{parts[1]}";
        return DefaultLocale;
    }

    private string ResolveLocale()
    {
        if (_locale != null)
            return _locale;

        // Infer from the first voice segment
        VoiceSegment? firstVoice = _segments.OfType<VoiceSegment>().FirstOrDefault();
        if (firstVoice != null)
            return ExtractLocale(firstVoice.VoiceId);

        return DefaultLocale;
    }

    /// <summary>
    /// XML-escapes text content for safe inclusion in SSML.
    /// </summary>
    public static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// XML-escapes attribute values (same as content escaping for SSML).
    /// </summary>
    internal static string EscapeXmlAttribute(string value) => EscapeXml(value);

    #region Segment Types

    internal interface ISsmlSegment;

    internal sealed record VoiceSegment(string VoiceId, string? PlainText, ProsodyBuilder? Prosody)
        : ISsmlSegment;

    internal sealed record SilenceSegment(int Milliseconds) : ISsmlSegment;

    #endregion
}

/// <summary>
/// Fluent sub-builder for configuring prosody, style, breaks, and text within a voice segment.
/// </summary>
public sealed class ProsodyBuilder
{
    private readonly List<IProsodyElement> _elements = [];

    public string? Rate { get; private set; }
    public string? Pitch { get; private set; }
    public string? Volume { get; private set; }
    public string? Style { get; private set; }

    public ProsodyBuilder Text(string text)
    {
        _elements.Add(new TextElement(text));
        return this;
    }

    public ProsodyBuilder Break(int milliseconds)
    {
        _elements.Add(new BreakElement(milliseconds));
        return this;
    }

    public ProsodyBuilder SetRate(string rate)
    {
        Rate = rate;
        return this;
    }

    public ProsodyBuilder SetPitch(string pitch)
    {
        Pitch = pitch;
        return this;
    }

    public ProsodyBuilder SetVolume(string volume)
    {
        Volume = volume;
        return this;
    }

    public ProsodyBuilder SetStyle(string style)
    {
        Style = style;
        return this;
    }

    /// <summary>
    /// Gets the concatenated plain text from all Text elements (for ToSegments output).
    /// </summary>
    internal string GetText()
    {
        return string.Join(" ", _elements.OfType<TextElement>().Select(e => e.Content));
    }

    /// <summary>
    /// Renders this prosody configuration as SSML content (without outer voice tags).
    /// </summary>
    internal string ToSsmlContent()
    {
        StringBuilder inner = new();
        foreach (IProsodyElement element in _elements)
        {
            switch (element)
            {
                case TextElement text:
                    inner.Append(SsmlBuilder.EscapeXml(text.Content));
                    break;
                case BreakElement brk:
                    inner.Append($"<break time=\"{brk.Milliseconds}ms\"/>");
                    break;
            }
        }

        string content = inner.ToString();

        // If a style is set, wrap in mstts:express-as
        if (!string.IsNullOrEmpty(Style))
        {
            content =
                $"<mstts:express-as style=\"{SsmlBuilder.EscapeXmlAttribute(Style)}\">{content}</mstts:express-as>";
        }

        // If any prosody attribute is set, wrap in prosody
        if (Rate != null || Pitch != null || Volume != null)
        {
            StringBuilder attrs = new();
            if (Volume != null)
                attrs.Append($" volume=\"{SsmlBuilder.EscapeXmlAttribute(Volume)}\"");
            if (Pitch != null)
                attrs.Append($" pitch=\"{SsmlBuilder.EscapeXmlAttribute(Pitch)}\"");
            if (Rate != null)
                attrs.Append($" rate=\"{SsmlBuilder.EscapeXmlAttribute(Rate)}\"");
            content = $"<prosody{attrs}>{content}</prosody>";
        }

        return content;
    }

    #region Element Types

    internal interface IProsodyElement;

    internal sealed record TextElement(string Content) : IProsodyElement;

    internal sealed record BreakElement(int Milliseconds) : IProsodyElement;

    #endregion
}
