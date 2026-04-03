using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CommandLine;
using Microsoft.AspNetCore;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services;

namespace NoMercyBot.Server;

public static class Program
{
    private static readonly CancellationTokenSource CancellationTokenSource = new();

    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Exception exception = (Exception)eventArgs.ExceptionObject;
        };

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Logger.App("Shutting down gracefully...");
            CancellationTokenSource.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            CancellationTokenSource.Cancel();
        };

        await Parser
            .Default.ParseArguments<StartupOptions>(args)
            .MapResult(Start, ErrorParsingArguments);

        static Task ErrorParsingArguments(IEnumerable<Error> errors)
        {
            Environment.ExitCode = 1;
            return Task.CompletedTask;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(
        IntPtr hWnd,
        int x,
        int y,
        int width,
        int height,
        bool repaint
    );

    private static async Task Start(StartupOptions options)
    {
        Console.Clear();
        Console.Title = "NoMercyBot Server";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            IntPtr handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
                MoveWindow(handle, 0, 0, 1920, 1080, true);
        }

        options.ApplySettings();

        Version version = Assembly.GetExecutingAssembly().GetName().Version!;
        Software.Version = version;
        Logger.App($"NoMercyBot version: v{version.Major}.{version.Minor}.{version.Build}");

        IWebHost app = CreateWebHostBuilder(options).Build();

        try
        {
            await app.RunAsync(CancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.App("Application shutdown completed.");
        }
    }

    private static IWebHostBuilder CreateWebHostBuilder(StartupOptions options)
    {
        UriBuilder localhostIPv4Url = new()
        {
            Host = IPAddress.Any.ToString(),
            Port = 6037,
            Scheme = Uri.UriSchemeHttp,
        };

        List<string> urls = [localhostIPv4Url.ToString()];

        return WebHost
            .CreateDefaultBuilder([])
            .ConfigureServices(services =>
            {
                services.AddSingleton<StartupOptions>(options);
                services.AddSingleton<
                    IApiVersionDescriptionProvider,
                    DefaultApiVersionDescriptionProvider
                >();
                services.AddSingleton<ISunsetPolicyManager, DefaultSunsetPolicyManager>();
                // Add custom logging here to ensure it's available during startup
                services.AddSingleton(typeof(ILogger<>), typeof(CustomLogger<>));
            })
            .UseUrls(urls.ToArray())
            .UseKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
                options.Limits.MaxRequestBufferSize = 1024 * 1024; // 1 MB
                options.Limits.MaxConcurrentConnections = 1000;
                options.Limits.MaxConcurrentUpgradedConnections = 200;
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
            })
            .UseQuic()
            .UseSockets()
            .UseStartup<Startup>();
    }
}
