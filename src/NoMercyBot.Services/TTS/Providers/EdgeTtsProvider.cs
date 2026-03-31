using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.TTS.Models;
using Serilog.Events;

namespace NoMercyBot.Services.TTS.Providers;

/// <summary>
/// TTS provider using Microsoft Edge's Read Aloud service via reverse-engineered WebSocket API.
/// Based on the edge-tts Python library: https://github.com/rany2/edge-tts
/// Free, no API key required. Limited voice set compared to paid Azure TTS.
/// </summary>
public class EdgeTtsProvider : TtsProviderBase
{
    // https://github.com/rany2/edge-tts/blob/master/src/edge_tts/constants.py
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string BaseUrl = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";
    private const string ChromiumVersion = "143.0.3650.75";
    private const long WinEpoch = 11644473600;
    private const int MaxChunkBytes = 4096;

    private static readonly string s_wssUrl =
        $"wss://{BaseUrl}/edge/v1?TrustedClientToken={TrustedClientToken}";

    private static readonly string s_voiceListUrl =
        $"https://{BaseUrl}/voices/list?trustedclienttoken={TrustedClientToken}";

    private static readonly string s_secMsGecVersion = $"1-{ChromiumVersion}";

    private static readonly string s_userAgent =
        $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
        + $"(KHTML, like Gecko) Chrome/{ChromiumVersion.Split('.')[0]}.0.0.0 Safari/537.36 "
        + $"Edg/{ChromiumVersion.Split('.')[0]}.0.0.0";

    private static readonly HttpClient s_httpClient = new();
    private List<TtsVoice>? _cachedVoices;

    public EdgeTtsProvider()
        : base("Edge", "edge", true, 1) // Priority 1: primary free provider
    { }

    #region DRM Token Generation

    /// <summary>
    /// Generates the Sec-MS-GEC anti-abuse token.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/drm.py#L103
    /// </summary>
    private static string GenerateSecMsGec()
    {
        double ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ticks += WinEpoch;
        ticks -= ticks % 300; // Round to 5-minute boundary
        long ticksNs = (long)(ticks * 1e7);

        string input = $"{ticksNs}{TrustedClientToken}";
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Generates a random MUID cookie value.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/drm.py#L137
    /// </summary>
    private static string GenerateMuid()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    /// <summary>
    /// Formats the current UTC time as a JavaScript-style date string.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/communicate.py#L279
    /// </summary>
    private static string DateToString()
    {
        return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss")
            + " GMT+0000 (Coordinated Universal Time)";
    }

    #endregion

    #region WebSocket Helpers

    private static void SetWsHeaders(ClientWebSocket ws)
    {
        ws.Options.SetRequestHeader("User-Agent", s_userAgent);
        ws.Options.SetRequestHeader(
            "Origin",
            "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold"
        );
        ws.Options.SetRequestHeader("Pragma", "no-cache");
        ws.Options.SetRequestHeader("Cache-Control", "no-cache");
        ws.Options.SetRequestHeader("Cookie", $"muid={GenerateMuid()};");
    }

    private static async Task SendTextAsync(
        ClientWebSocket ws,
        string message,
        CancellationToken ct
    )
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    /// <summary>
    /// Sends the speech.config message to configure audio output format.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/communicate.py#L395
    /// </summary>
    private static async Task SendConfigAsync(ClientWebSocket ws, CancellationToken ct)
    {
        string message =
            $"X-Timestamp:{DateToString()}\r\n"
            + "Content-Type:application/json; charset=utf-8\r\n"
            + "Path:speech.config\r\n\r\n"
            + "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{"
            + "\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"true\""
            + "},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";

        await SendTextAsync(ws, message, ct);
    }

    /// <summary>
    /// Sends an SSML synthesis request.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/communicate.py#L411
    /// </summary>
    private static async Task SendSsmlAsync(ClientWebSocket ws, string ssml, CancellationToken ct)
    {
        string requestId = Guid.NewGuid().ToString("N");
        string message =
            $"X-RequestId:{requestId}\r\n"
            + "Content-Type:application/ssml+xml\r\n"
            + $"X-Timestamp:{DateToString()}Z\r\n"
            + "Path:ssml\r\n\r\n"
            + ssml;

        await SendTextAsync(ws, message, ct);
    }

    /// <summary>
    /// Receives a complete WebSocket message (may span multiple frames).
    /// </summary>
    private static async Task<(WebSocketMessageType type, byte[] data)> ReceiveMessageAsync(
        ClientWebSocket ws,
        byte[] buffer,
        CancellationToken ct
    )
    {
        using MemoryStream ms = new();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return (WebSocketMessageType.Close, []);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return (result.MessageType, ms.ToArray());
    }

    /// <summary>
    /// Extracts MP3 audio data from a binary WebSocket message.
    /// Binary format: 2-byte header length (big-endian) + header text + audio data.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/communicate.py#L482
    /// </summary>
    private static byte[]? ExtractAudioFromBinary(byte[] data)
    {
        if (data.Length < 2)
            return null;

        int headerLength = (data[0] << 8) | data[1];
        int dataStart = 2 + headerLength;

        if (dataStart >= data.Length)
            return null;

        string headers = Encoding.UTF8.GetString(data, 2, headerLength);
        if (!headers.Contains("Path:audio"))
            return null;

        byte[] audio = new byte[data.Length - dataStart];
        Buffer.BlockCopy(data, dataStart, audio, 0, audio.Length);
        return audio;
    }

    /// <summary>
    /// Receives audio chunks from the WebSocket until turn.end is received.
    /// </summary>
    private static async Task ReceiveAudioAsync(
        ClientWebSocket ws,
        MemoryStream audioBuffer,
        CancellationToken ct
    )
    {
        byte[] recvBuffer = new byte[8192];

        while (ws.State == WebSocketState.Open)
        {
            (WebSocketMessageType type, byte[] data) = await ReceiveMessageAsync(
                ws,
                recvBuffer,
                ct
            );

            if (type == WebSocketMessageType.Close)
                break;

            if (type == WebSocketMessageType.Text)
            {
                string msg = Encoding.UTF8.GetString(data);
                if (msg.Contains("Path:turn.end"))
                    break;
                if (msg.Contains("Path:turn.start"))
                    continue;
            }
            else if (type == WebSocketMessageType.Binary)
            {
                byte[]? audio = ExtractAudioFromBinary(data);
                if (audio != null)
                    audioBuffer.Write(audio, 0, audio.Length);
            }
        }
    }

    #endregion

    #region Text Splitting

    /// <summary>
    /// Splits text into chunks that fit within the 4096-byte SSML limit.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/communicate.py#L185
    /// </summary>
    private static List<string> SplitText(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= MaxChunkBytes)
            return [text];

        List<string> chunks = [];
        string[] words = text.Split(' ');
        StringBuilder current = new();

        foreach (string word in words)
        {
            string candidate = current.Length > 0 ? $"{current} {word}" : word;
            if (Encoding.UTF8.GetByteCount(candidate) > MaxChunkBytes && current.Length > 0)
            {
                chunks.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
            else
            {
                current.Clear();
                current.Append(candidate);
            }
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks.Count > 0 ? chunks : [text];
    }

    #endregion

    #region SSML Building

    /// <summary>
    /// Builds SSML markup for Edge TTS synthesis.
    /// https://github.com/rany2/edge-tts/blob/master/src/edge_tts/communicate.py#L254
    /// </summary>
    private string BuildEdgeSsml(string text, string voiceId)
    {
        string sanitized = SanitizeText(text);
        string locale = ExtractLocale(voiceId);
        return $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{locale}'>"
            + $"<voice name='{voiceId}'>"
            + $"<prosody pitch='+0Hz' rate='+0%' volume='+0%'>"
            + sanitized
            + "</prosody></voice></speak>";
    }

    /// <summary>
    /// Extracts the locale (e.g. "ru-RU") from a voice ID like "ru-RU-SvetlanaNeural".
    /// </summary>
    private static string ExtractLocale(string voiceId)
    {
        string[] parts = voiceId.Split('-');
        if (parts.Length >= 2)
            return $"{parts[0]}-{parts[1]}";
        return "en-US";
    }

    #endregion

    #region ITtsProvider Implementation

    public override async Task<byte[]> SynthesizeAsync(
        string text,
        string voiceId,
        CancellationToken cancellationToken = default
    )
    {
        ValidateInputs(text, voiceId);

        List<string> chunks = SplitText(text);
        using MemoryStream audioBuffer = new();

        string connectionId = Guid.NewGuid().ToString("N");
        string secMsGec = GenerateSecMsGec();
        string wsUrl =
            $"{s_wssUrl}&ConnectionId={connectionId}"
            + $"&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={s_secMsGecVersion}";

        using ClientWebSocket ws = new();
        SetWsHeaders(ws);

        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), cancellationToken);
            await SendConfigAsync(ws, cancellationToken);

            foreach (string chunk in chunks)
            {
                string ssml = BuildEdgeSsml(chunk, voiceId);
                await SendSsmlAsync(ws, ssml, cancellationToken);
                await ReceiveAudioAsync(ws, audioBuffer, cancellationToken);
            }

            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        catch (WebSocketException) when (audioBuffer.Length > 0)
        {
            // Server closed connection after sending audio — this is normal
        }
        catch (Exception ex)
        {
            string closeInfo = ws.CloseStatus.HasValue
                ? $" (close: {ws.CloseStatus} - {ws.CloseStatusDescription})"
                : "";
            Logger.Setup(
                $"Edge TTS synthesis error: {ex.Message}{closeInfo}",
                LogEventLevel.Warning
            );
            throw new($"Edge TTS synthesis error: {ex.Message}", ex);
        }

        return audioBuffer.ToArray();
    }

    public override async Task<byte[]> SynthesizeSsmlAsync(
        string ssml,
        string voiceId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(ssml))
            throw new ArgumentException("SSML input cannot be empty.", nameof(ssml));

        string connectionId = Guid.NewGuid().ToString("N");
        string secMsGec = GenerateSecMsGec();
        string wsUrl =
            $"{s_wssUrl}&ConnectionId={connectionId}"
            + $"&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={s_secMsGecVersion}";

        using ClientWebSocket ws = new();
        SetWsHeaders(ws);
        using MemoryStream audioBuffer = new();

        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), cancellationToken);
            await SendConfigAsync(ws, cancellationToken);
            await SendSsmlAsync(ws, ssml, cancellationToken);
            await ReceiveAudioAsync(ws, audioBuffer, cancellationToken);

            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        catch (WebSocketException) when (audioBuffer.Length > 0)
        {
            // Server closed connection after sending audio — this is normal
        }
        catch (Exception ex)
        {
            Logger.Setup($"Edge TTS SSML synthesis error: {ex.Message}", LogEventLevel.Warning);
            throw new($"Edge TTS synthesis error: {ex.Message}", ex);
        }

        return audioBuffer.ToArray();
    }

    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);

    public override async Task<List<TtsVoice>> GetAvailableVoicesAsync()
    {
        if (_cachedVoices != null)
            return _cachedVoices;

        try
        {
            string secMsGec = GenerateSecMsGec();
            string muid = GenerateMuid();
            string url =
                $"{s_voiceListUrl}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={s_secMsGecVersion}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", s_userAgent);
            request.Headers.Add("Cookie", $"muid={muid};");

            HttpResponseMessage response = await s_httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            List<EdgeVoiceInfo>? edgeVoices = JsonSerializer.Deserialize<List<EdgeVoiceInfo>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (edgeVoices != null)
            {
                _cachedVoices = edgeVoices
                    .Select(v => new TtsVoice
                    {
                        Id = v.ShortName,
                        Name = v.ShortName,
                        DisplayName = v.FriendlyName ?? v.ShortName,
                        Locale = v.Locale,
                        Gender = v.Gender,
                        Provider = Name,
                        IsDefault = v.ShortName == "en-US-EmmaMultilingualNeural",
                    })
                    .ToList();

                return _cachedVoices;
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error fetching Edge TTS voices: {ex.Message}", LogEventLevel.Warning);
        }

        _cachedVoices = GetDefaultEdgeVoices();
        return _cachedVoices;
    }

    public override Task<decimal> CalculateCostAsync(string text, string voiceId)
    {
        return Task.FromResult(0m); // Edge TTS is free
    }

    public override Task<string> GetDefaultVoiceIdAsync()
    {
        return Task.FromResult("en-US-EmmaMultilingualNeural");
    }

    #endregion

    #region Default Voices

    private List<TtsVoice> GetDefaultEdgeVoices()
    {
        return
        [
            new()
            {
                Id = "en-US-EmmaMultilingualNeural",
                Name = "Emma",
                DisplayName = "Emma (English US)",
                Locale = "en-US",
                Gender = "Female",
                Provider = Name,
                IsDefault = true,
            },
            new()
            {
                Id = "en-US-AndrewMultilingualNeural",
                Name = "Andrew",
                DisplayName = "Andrew (English US)",
                Locale = "en-US",
                Gender = "Male",
                Provider = Name,
            },
            new()
            {
                Id = "en-US-AvaMultilingualNeural",
                Name = "Ava",
                DisplayName = "Ava (English US)",
                Locale = "en-US",
                Gender = "Female",
                Provider = Name,
            },
            new()
            {
                Id = "en-US-BrianMultilingualNeural",
                Name = "Brian",
                DisplayName = "Brian (English US)",
                Locale = "en-US",
                Gender = "Male",
                Provider = Name,
            },
            new()
            {
                Id = "en-GB-SoniaNeural",
                Name = "Sonia",
                DisplayName = "Sonia (English UK)",
                Locale = "en-GB",
                Gender = "Female",
                Provider = Name,
            },
            new()
            {
                Id = "en-GB-RyanNeural",
                Name = "Ryan",
                DisplayName = "Ryan (English UK)",
                Locale = "en-GB",
                Gender = "Male",
                Provider = Name,
            },
            new()
            {
                Id = "en-AU-NatashaNeural",
                Name = "Natasha",
                DisplayName = "Natasha (English AU)",
                Locale = "en-AU",
                Gender = "Female",
                Provider = Name,
            },
            new()
            {
                Id = "en-AU-WilliamNeural",
                Name = "William",
                DisplayName = "William (English AU)",
                Locale = "en-AU",
                Gender = "Male",
                Provider = Name,
            },
        ];
    }

    #endregion

    private class EdgeVoiceInfo
    {
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string FriendlyName { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Locale { get; set; } = "";
    }
}
