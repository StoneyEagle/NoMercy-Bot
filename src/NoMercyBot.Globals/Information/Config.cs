namespace NoMercyBot.Globals.Information;

public static class Config
{
    public static readonly string DnsServer = "1.1.1.1";
    public static object? ProxyServer { get; set; }

    public static string UserAgent => $"NoMercyBot/{Software.Version} ( admin@nomercy.tv )";

    public static int InternalServerPort { get; set; } = 6037;
    public static int InternalClientPort { get; set; } = 6038;

    /// <summary>
    /// Public base URL for OAuth redirect URIs. When set, replaces localhost URLs.
    /// Set via NOMERCY_BASE_URL environment variable for production.
    /// Example: "https://bot.nomercy.tv"
    /// </summary>
    public static string? BaseUrl { get; set; } = Environment.GetEnvironmentVariable("NOMERCY_BASE_URL");
    public static object InternalTtsPort { get; set; } = 6040;

    public static bool Swagger { get; set; } = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static KeyValuePair<string, int> QueueWorkers { get; set; } = new("queue", 1);
    public static KeyValuePair<string, int> CronWorkers { get; set; } = new("cron", 1);

    public static bool UseTts { get; set; }
    public static bool SaveTtsToDisk { get; set; } = true;
    public static bool PlayTtsLocally { get; set; } = false; // New option to control local audio playback

    public static bool UseFrankerfacezEmotes { get; set; } = true;
    public static bool UseBttvEmotes { get; set; } = true;
    public static bool UseSevenTvEmotes { get; set; } = true;
    public static bool UseChatCodeSnippets { get; set; } = true;
    public static bool UseChatHtmlParser { get; set; } = true;
    public static bool UseChatOgParser { get; set; } = true;
}
