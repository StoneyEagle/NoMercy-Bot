using Microsoft.CognitiveServices.Speech;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Services.TTS.Models;
using NoMercyBot.Globals.SystemCalls;
using Serilog.Events;
using System.Text;
using NoMercyBot.Globals.Information;

namespace NoMercyBot.Services.TTS.Providers;

public class AzureTtsProvider : TtsProviderBase, IDisposable
{
    private readonly AppDbContext _dbContext;
    private SpeechSynthesizer? _synthesizer;
    private SpeechConfig? _speechConfig;
    
    private static HttpClient _httpClient = new();
    private static readonly object _initLock = new object(); // Lock for thread-safe initialization
    private bool _initialized = false;

    public AzureTtsProvider(AppDbContext dbContext)
        : base("Azure", "azure", true, 10) // Fallback when Edge is unavailable
    {
        _dbContext = dbContext;
    }

    public override async Task InitializeAsync()
    {
        // Use double-check locking pattern to avoid repeated initialization
        if (_initialized) return;

        lock (_initLock)
        {
            // Check again inside the lock
            if (_initialized) return;

            // Initialize synchronously inside the lock to prevent concurrent database access
            InitializeSynchronous();
            _initialized = true;
        }
    }

    private void InitializeSynchronous()
    {
        try
        {
            // Use synchronous database access inside the lock to avoid threading issues
            var apiKey = _dbContext.Configurations
                .AsNoTracking()
                .Where(c => c.Key == "tts_azure_api_key")
                .Select(c => c.SecureValue)
                .FirstOrDefault();

            var region = _dbContext.Configurations
                .AsNoTracking()
                .Where(c => c.Key == "tts_azure_region")
                .Select(c => c.Value)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(region)) return;
            
            try
            {
                _speechConfig = SpeechConfig.FromSubscription(apiKey, region);
                _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm);
                _synthesizer = new(_speechConfig);
            }
            catch (Exception)
            {
                _speechConfig = null;
                _synthesizer?.Dispose();
                _synthesizer = null;
            }
            
            _httpClient.BaseAddress = new($"https://{region}.tts.speech.microsoft.com");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error initializing Azure TTS provider: {ex.Message}", LogEventLevel.Warning);
        }
    }
    
    public override async Task<bool> IsAvailableAsync()
    {
        if (_synthesizer == null || _speechConfig == null) await InitializeAsync();

        return _synthesizer != null && _speechConfig != null;
    }
    
    public override async Task<byte[]> SynthesizeAsync(string text, string voiceId,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(text, voiceId);

        string sanitizedText = SanitizeText(text);

        // Build SSML for Azure TTS with voice selection
        string ssml = BuildSsml(sanitizedText, voiceId);

        try
        {
            StringContent content = new(ssml, Encoding.UTF8, "application/ssml+xml");
            HttpResponseMessage response = await _httpClient.PostAsync("cognitiveservices/v1", content, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            else
            {
                // Log the error response for debugging
                string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.Setup($"Azure TTS API returned {response.StatusCode}: {responseContent}", LogEventLevel.Warning);
                Logger.Setup($"SSML sent: {ssml}", LogEventLevel.Debug);
                throw new($"Azure TTS synthesis failed: HTTP {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new($"Azure TTS synthesis error: {ex.Message}", ex);
        }
    }
    
    public override async Task<byte[]> SynthesizeSsmlAsync(
        string ssml,
        string voiceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ssml))
            throw new ArgumentException("SSML input cannot be empty.", nameof(ssml));

        if (string.IsNullOrWhiteSpace(voiceId))
            throw new ArgumentException("Voice ID cannot be empty.", nameof(voiceId));

        try
        {
            StringContent content = new(ssml, Encoding.UTF8, "application/ssml+xml");
            HttpResponseMessage response = await _httpClient.PostAsync("cognitiveservices/v1", content, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            else
            {
                // Log the full error response for debugging
                string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.Setup($"Azure TTS API returned {response.StatusCode}", LogEventLevel.Warning);
                if (!string.IsNullOrEmpty(responseContent))
                {
                    Logger.Setup($"Response: {responseContent}", LogEventLevel.Warning);
                }
                Logger.Setup($"SSML sent: {ssml}", LogEventLevel.Warning);
                Logger.Setup($"Voice ID: {voiceId}", LogEventLevel.Warning);
                throw new($"Azure TTS synthesis failed: HTTP {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            throw new($"Azure TTS synthesis error: {ex.Message}", ex);
        }
    }

    public override async Task<List<TtsVoice>> GetAvailableVoicesAsync()
    {
        if (_synthesizer == null)
        {
            await InitializeAsync();
            if (_synthesizer == null)
            {
                Logger.Setup("Azure TTS provider not initialized - using fallback voices", LogEventLevel.Warning);
                return GetDefaultAzureVoices();
            }
        }
        
        try
        {
            using SynthesisVoicesResult voicesResult = await _synthesizer.GetVoicesAsync();

            if (voicesResult.Reason == ResultReason.VoicesListRetrieved)
            {
                List<TtsVoice> azureVoices = voicesResult.Voices.Select(voice => new TtsVoice
                {
                    Id = voice.ShortName,
                    Name = voice.ShortName,
                    DisplayName = voice.LocalName,
                    Locale = voice.Locale,
                    Gender = voice.Gender.ToString(),
                    Provider = Name,
                    IsDefault = voice.ShortName == "en-US-JennyNeural"
                }).ToList();

                return azureVoices;
            }
            else
            {
                Logger.Setup($"Failed to retrieve Azure TTS voices: {voicesResult.Reason}",
                    LogEventLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error retrieving Azure TTS voices from API: {ex.Message}", LogEventLevel.Warning);
        }

        return GetDefaultAzureVoices();
    }

    public override Task<int> GetCharacterCountAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(0);

        // Azure charges by character for TTS
        return Task.FromResult(text.Length);
    }

    public override Task<decimal> CalculateCostAsync(string text, string voiceId)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(0m);

        int characterCount = text.Length;

        // Azure TTS pricing (as of 2024):
        // Neural voices: $16 per 1 million characters
        // Standard voices: $4 per 1 million characters

        // All Azure neural voices end with "Neural" in their ID
        bool isNeuralVoice = voiceId.EndsWith("Neural", StringComparison.OrdinalIgnoreCase);

        decimal costPerMillionCharacters = isNeuralVoice ? 16.00m : 4.00m;
        decimal cost = characterCount / 1_000_000m * costPerMillionCharacters;

        return Task.FromResult(cost);
    }

    public override Task<string> GetDefaultVoiceIdAsync()
    {
        return Task.FromResult("en-US-JennyNeural");
    }

    private string BuildSsml(string text, string voiceId)
    {
        // Text is already XML-escaped by SanitizeText in the base class
        // Split text into words and analyze each for special processing
        (string word, int pitch, double rate, string style)[] wordTuples = text
            .Split(' ')
            .Select(word => (word: word, pitch: 0, rate: 1.0, style: string.Empty))
            .ToArray();

        // Process each word for special cases
        for (int i = 0; i < wordTuples.Length; i++)
        {
            (string word, int pitch, double rate, string style) = wordTuples[i];

            // URL detection and processing
            if (Uri.TryCreate(word, UriKind.Absolute, out Uri? uri))
            {
                string processedUrl = uri.Host;
                if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                {
                    string path = uri.AbsolutePath.StartsWith("/") 
                        ? uri.AbsolutePath.Substring(1) 
                        : uri.AbsolutePath;
                    processedUrl += " " + path;
                }
                
                wordTuples[i] = ($"<break time=\"200ms\" />{processedUrl}<break time=\"200ms\" />", pitch, 2.0, style);
            }
            // ALL CAPS detection for shouting style
            else if (word.Any(char.IsLetter) && word.All(c => char.IsUpper(c) || !char.IsLetter(c)))
            {
                wordTuples[i] = (word, pitch, rate, "shouting");
            }
        }

        // Group consecutive words with the same voice settings
        int lastPitch = 0;
        double lastRate = 1.0;
        string lastStyle = string.Empty;
        List<string> currentLine = [];
        List<string> ssmlElements = [];

        foreach ((string word, int pitch, double rate, string style) in wordTuples)
        {
            // Check if voice settings changed
            if (pitch != lastPitch || Math.Abs(rate - lastRate) > 0.01 || style != lastStyle)
            {
                // Process accumulated words with previous settings
                if (currentLine.Count > 0)
                {
                    string groupedText = string.Join(" ", currentLine);
                    
                    if (!string.IsNullOrEmpty(lastStyle))
                    {
                        ssmlElements.Add($"<mstts:express-as style=\"{lastStyle}\">{groupedText}</mstts:express-as>");
                    }
                    else
                    {
                        string pitchValue = lastPitch == 0 ? "0Hz" : $"{(lastPitch > 0 ? "+" : "")}{lastPitch}Hz";
                        string rateValue = lastRate.ToString("F1").Replace(",", ".");
                        ssmlElements.Add($"<prosody volume=\"100\" pitch=\"{pitchValue}\" rate=\"{rateValue}\">{groupedText}</prosody>");
                    }
                }

                // Update settings for new group
                lastPitch = pitch;
                lastRate = rate;
                lastStyle = style;
                currentLine.Clear();
            }

            currentLine.Add(word);
        }

        // Process final group of words
        if (currentLine.Count > 0)
        {
            string groupedText = string.Join(" ", currentLine);
            
            if (!string.IsNullOrEmpty(lastStyle))
            {
                ssmlElements.Add($"<mstts:express-as style=\"{lastStyle}\">{groupedText}</mstts:express-as>");
            }
            else
            {
                string pitchValue = lastPitch == 0 ? "0Hz" : $"{(lastPitch > 0 ? "+" : "")}{lastPitch}Hz";
                string rateValue = lastRate.ToString("F1").Replace(",", ".");
                ssmlElements.Add($"<prosody volume=\"100\" pitch=\"{pitchValue}\" rate=\"{rateValue}\">{groupedText}</prosody>");
            }
        }

        // Build final SSML with Microsoft TTS namespace support
        string ssml = $"""
            <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US" xmlns:mstts="https://www.w3.org/2001/mstts">
                <voice name="{voiceId}">
                    {string.Join("\n ", ssmlElements)}
                </voice>
            </speak>
            """;

        return ssml;
    }

    private List<TtsVoice> GetDefaultAzureVoices()
    {
        return
        [
            // English voices
            new()
            {
                Id = "en-US-JennyNeural", Name = "Jenny", DisplayName = "Jenny (English US)", Locale = "en-US",
                Gender = "Female", Provider = Name, IsDefault = true
            },
            new()
            {
                Id = "en-US-GuyNeural", Name = "Guy", DisplayName = "Guy (English US)", Locale = "en-US",
                Gender = "Male", Provider = Name, IsDefault = false
            },
            new()
            {
                Id = "en-US-AriaNeural", Name = "Aria", DisplayName = "Aria (English US)", Locale = "en-US",
                Gender = "Female", Provider = Name, IsDefault = false
            },
            new()
            {
                Id = "en-US-DavisNeural", Name = "Davis", DisplayName = "Davis (English US)", Locale = "en-US",
                Gender = "Male", Provider = Name, IsDefault = false
            },
            new()
            {
                Id = "en-GB-SoniaNeural", Name = "Sonia", DisplayName = "Sonia (English UK)", Locale = "en-GB",
                Gender = "Female", Provider = Name, IsDefault = false
            },
            new()
            {
                Id = "en-GB-RyanNeural", Name = "Ryan", DisplayName = "Ryan (English UK)", Locale = "en-GB",
                Gender = "Male", Provider = Name, IsDefault = false
            },
            new()
            {
                Id = "en-AU-NatashaNeural", Name = "Natasha", DisplayName = "Natasha (English AU)", Locale = "en-AU",
                Gender = "Female", Provider = Name, IsDefault = false
            },
            new()
            {
                Id = "en-AU-WilliamNeural", Name = "William", DisplayName = "William (English AU)", Locale = "en-AU",
                Gender = "Male", Provider = Name, IsDefault = false
            }
        ];
    }

    public void Dispose()
    {
        _synthesizer?.Dispose();
        _speechConfig = null;
    }
}