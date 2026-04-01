using System.Drawing;
using Newtonsoft.Json;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.LogEnrichers;
using NoMercyBot.Globals.NewtonSoftConverters;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace NoMercyBot.Globals.SystemCalls;

public static class Logger
{
    private static Serilog.Core.Logger ConsoleLog { get; set; }
    private static Serilog.Core.Logger FileLog { get; set; }
    private static LogEventLevel _maxLogLevel = LogEventLevel.Debug;
    private const string ConsoleTemplate =
        "{Time} {ConsoleType} | {@Message:lj}{NewLine}{Exception}";

    public class LogType
    {
        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("display_name")]
        public string DisplayName { get; }

        [JsonProperty("color")]
        public Color Color { get; }

        [JsonProperty("colorHex")]
        public string ColorHex { get; }

        [JsonProperty("type")]
        public string Type { get; }

        [JsonProperty("level")]
        public LogEventLevel DefaultLevel { get; }

        public LogType(
            string name,
            string displayName,
            Color color,
            string type,
            LogEventLevel defaultLevel = LogEventLevel.Information
        )
        {
            Name = name;
            DisplayName = displayName;
            Color = color;
            ColorHex = ToHexString(color);
            Type = type;
            DefaultLevel = defaultLevel;
        }
    }

    public static readonly Dictionary<string, LogType> LogTypes = new()
    {
        // System category
        { "_", new("_", "System", Color.DimGray, "spacer") },
        { "app", new("app", "App", Color.MediumPurple, "System") },
        { "access", new("access", "Access", Color.MediumPurple, "System") },
        { "configuration", new("configuration", "Configuration", Color.MediumPurple, "System") },
        { "setup", new("setup", "Setup", Color.CornflowerBlue, "System") },
        { "system", new("system", "System", Color.CornflowerBlue, "System") },
        { "service", new("service", "Service", Color.CornflowerBlue, "System") },
        { "debug", new("debug", "Debug", Color.Gray, "System", LogEventLevel.Debug) },
        { "info", new("info", "Info", Color.White, "System") },
        { "warning", new("warning", "Warning", Color.Yellow, "System", LogEventLevel.Warning) },
        { "error", new("error", "Error", Color.Red, "System", LogEventLevel.Error) },
        // Workers category
        { "__", new("__", "Workers", Color.DimGray, "spacer") },
        { "queue", new("queue", "Queue", Color.Chocolate, "Workers", LogEventLevel.Debug) },
        // Networking category
        { "___", new("___", "Networking", Color.DimGray, "spacer") },
        { "http", new("http", "Http", Color.Orange, "Networking") },
        { "notify", new("notify", "Notify", Color.Orange, "Networking") },
        { "ping", new("ping", "Ping", Color.Orange, "Networking") },
        { "socket", new("socket", "Socket", Color.Orange, "Networking") },
        { "request", new("request", "Request", Color.Orange, "Networking", LogEventLevel.Debug) },
        // Providers category
        { "____", new("____", "Providers", Color.DimGray, "spacer") },
        { "youtube", new("youtube", "YouTube", Color.DodgerBlue, "Providers") },
        // Notifications category
        { "_____", new("_____", "Notifications", Color.DimGray, "spacer") },
        { "discord", new("discord", "Discord", Color.Green, "Notifications") },
        { "twitch", new("twitch", "Twitch", Color.Green, "Notifications") },
        { "spotify", new("spotify", "Spotify", Color.Green, "Notifications") },
        { "twitter", new("twitter", "Twitter", Color.Green, "Notifications") },
        { "webhook", new("webhook", "Webhook", Color.Green, "Notifications") },
    };

    static Logger()
    {
        ConsoleLog = CreateConsoleConfiguration().CreateLogger();
        FileLog = CreateFileConfiguration().CreateLogger();
    }

    private static LoggerConfiguration DefaultEnrich(this LoggerConfiguration lc)
    {
        return lc.Enrich.FromLogContext().Enrich.With<WithThreadIdEnricher>();
    }

    private static void SinkFile(this LoggerConfiguration lc, string filePath)
    {
        lc.Enrich.With<FileTypeEnricher>()
            .Enrich.With<FileTimestampEnricher>()
            .Enrich.With<FileMessageEnricher>()
            .WriteTo.File(
                new CompactJsonFormatter(),
                filePath,
                rollingInterval: RollingInterval.Day
            );
    }

    private static SystemConsoleTheme Literate { get; } =
        new(
            new Dictionary<ConsoleThemeStyle, SystemConsoleThemeStyle>
            {
                [ConsoleThemeStyle.Text] = new() { Foreground = ConsoleColor.White },
                [ConsoleThemeStyle.SecondaryText] = new() { Foreground = ConsoleColor.Gray },
                [ConsoleThemeStyle.TertiaryText] = new() { Foreground = ConsoleColor.Cyan },
                [ConsoleThemeStyle.Invalid] = new() { Foreground = ConsoleColor.Yellow },
                [ConsoleThemeStyle.Null] = new() { Foreground = ConsoleColor.Blue },
                [ConsoleThemeStyle.Name] = new() { Foreground = ConsoleColor.Gray },
                [ConsoleThemeStyle.String] = new() { Foreground = ConsoleColor.White },
                [ConsoleThemeStyle.Number] = new() { Foreground = ConsoleColor.Magenta },
                [ConsoleThemeStyle.Boolean] = new() { Foreground = ConsoleColor.DarkYellow },
                [ConsoleThemeStyle.Scalar] = new() { Foreground = ConsoleColor.Green },
                [ConsoleThemeStyle.LevelVerbose] = new() { Foreground = ConsoleColor.Gray },
                [ConsoleThemeStyle.LevelDebug] = new() { Foreground = ConsoleColor.Gray },
                [ConsoleThemeStyle.LevelInformation] = new() { Foreground = ConsoleColor.White },
                [ConsoleThemeStyle.LevelWarning] = new() { Foreground = ConsoleColor.Yellow },
                [ConsoleThemeStyle.LevelError] = new()
                {
                    Foreground = ConsoleColor.White,
                    Background = ConsoleColor.Red,
                },
                [ConsoleThemeStyle.LevelFatal] = new()
                {
                    Foreground = ConsoleColor.White,
                    Background = ConsoleColor.Red,
                },
            }
        );

    private static void SinkConsole(this LoggerConfiguration lc)
    {
        lc.Enrich.With<ConsoleTimestampEnricher>()
            .Enrich.With<ConsoleTypeEnricher>()
            .WriteTo.Console(
                applyThemeToRedirectedOutput: true,
                theme: Literate,
                outputTemplate: ConsoleTemplate
            );
    }

    private static LoggerConfiguration CreateConsoleConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .DefaultEnrich()
            .WriteTo.Logger(lc =>
            {
                if (!Console.IsOutputRedirected)
                    lc.SinkConsole();
            });
    }

    private static LoggerConfiguration CreateFileConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .DefaultEnrich()
            .WriteTo.Logger(lc => lc.SinkFile(Path.Join(AppFiles.LogPath, "log.txt")));
    }

    private static bool ShouldLog(LogEventLevel level)
    {
        return level >= _maxLogLevel;
    }

    private static string ToHexString(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static void SetLogLevel(LogEventLevel level)
    {
        _maxLogLevel = level;
    }

    private static void Log<T>(string logType, T message, LogEventLevel? level = null)
        where T : class
    {
        if (!LogTypes.TryGetValue(logType, out LogType? type))
            type = new(logType, logType, Color.White, "Unknown");

        LogEventLevel logLevel = level ?? type.DefaultLevel;

        if (!ShouldLog(logLevel))
            return;

        string colorHex = type.ColorHex;

        ConsoleLog
            .ForContext("Type", logType)
            .ForContext("Color", colorHex)
            .ForContext("Message", message)
            .ForContext("Level", logLevel)
            .ForContext("ConsoleType", type.Name)
            .Write(logLevel, "{@Message}", message);

        FileLog
            .ForContext("Type", logType)
            .ForContext("Color", colorHex)
            .ForContext("Message", message.ToJson())
            .ForContext("Level", logLevel)
            .ForContext("ConsoleType", type.Name)
            .Write(logLevel, "{@Message}", message.ToJson());
    }

    // Generic entry point
    public static void Write<T>(string logType, T message, LogEventLevel? level = null)
        where T : class
    {
        Log(logType, message, level);
    }

    public static void Write(string logType, string message, LogEventLevel? level = null)
    {
        Log(logType, message, level);
    }

    internal static Color GetColor(string type)
    {
        return LogTypes.TryGetValue(type, out LogType? color) ? color.Color : Color.Red;
    }

    // Standard logging methods with simplified implementation
    public static void Debug(string message)
    {
        Log("debug", message);
    }

    public static void Debug<T>(T message, LogEventLevel? level = null)
        where T : class
    {
        Log("debug", message, level ?? LogEventLevel.Debug);
    }

    public static void Info(string message)
    {
        Log("info", message);
    }

    public static void Info<T>(T message, LogEventLevel? level = null)
        where T : class
    {
        Log("info", message, level ?? LogEventLevel.Information);
    }

    public static void Warning(string message)
    {
        Log("warning", message);
    }

    public static void Warning<T>(T message, LogEventLevel? level = null)
        where T : class
    {
        Log("warning", message, level ?? LogEventLevel.Warning);
    }

    public static void Error(string message)
    {
        Log("error", message);
    }

    public static void Error<T>(T message, LogEventLevel? level = null)
        where T : class
    {
        Log("error", message, level ?? LogEventLevel.Error);
    }

    public static void Access<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("access", message, level);
    }

    public static void App<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("app", message, level);
    }

    public static void Configuration<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("configuration", message, level);
    }

    public static void Discord<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("discord", message, level);
    }

    public static void Http<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("http", message, level);
    }

    public static void Notify<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("notify", message, level);
    }

    public static void Ping<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("ping", message, level);
    }

    public static void Queue<T>(T message, LogEventLevel level = LogEventLevel.Debug)
        where T : class
    {
        Log("queue", message, level);
    }

    public static void Request<T>(T message, LogEventLevel level = LogEventLevel.Debug)
        where T : class
    {
        Log("request", message, level);
    }

    public static void Request<T>(T message, LogEventLevel? level = null)
        where T : class
    {
        Log("request", message, level);
    }

    public static void Service<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("service", message, level);
    }

    public static void Service<T>(T message, LogEventLevel? level = null)
        where T : class
    {
        Log("service", message, level);
    }

    public static void Setup<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("setup", message, level);
    }

    public static void Socket<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("socket", message, level);
    }

    public static void Spotify<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("spotify", message, level);
    }

    public static void System<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("system", message, level);
    }

    public static void System<T>(T message, LogEventLevel? level = null)
        where T : class
    {
        Log("system", message, level);
    }

    public static void Twitch<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("twitch", message, level);
    }

    public static void Twitter<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("twitter", message, level);
    }

    public static void Webhook<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("webhook", message, level);
    }

    public static void Youtube<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class
    {
        Log("youtube", message, level);
    }
}
