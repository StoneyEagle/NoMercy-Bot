using CommandLine;
using NoMercyBot.Globals.Extensions;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.SystemCalls;
using Serilog.Events;

namespace NoMercyBot.Server;

public class StartupOptions
{
    [Option(
        'i',
        "internal-port",
        Required = false,
        HelpText = "Internal port to use for the server."
    )]
    public int InternalPort { get; set; }

    [Option('l', "loglevel", Required = false, HelpText = "Run the server in development mode.")]
    public string LogLevel { get; set; } = nameof(LogEventLevel.Information);

    public void ApplySettings()
    {
        if (!string.IsNullOrEmpty(LogLevel))
        {
            Logger.Configuration($"Setting log level to: {LogLevel}.");
            Logger.SetLogLevel(Enum.Parse<LogEventLevel>(LogLevel.ToTitleCase()));
        }

        if (InternalPort != 0)
        {
            Logger.Configuration("Setting internal port to " + InternalPort);
            Config.InternalServerPort = InternalPort;
        }
    }
}
