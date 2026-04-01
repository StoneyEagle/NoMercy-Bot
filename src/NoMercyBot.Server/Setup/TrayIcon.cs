using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using H.NotifyIcon.Core;

namespace NoMercyBot.Server.Setup;

[SupportedOSPlatform("windows10.0.18362")]
public class TrayIcon
{
    private static Icon LoadIcon()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = "NoMercyBot.Server.Assets.icon.ico";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException("Icon resource not found.");
        return new(stream);
    }

    private static readonly Icon Icon = LoadIcon();

    private readonly TrayIconWithContextMenu _trayIcon = new()
    {
        Icon = Icon.Handle,
        ToolTip = "NoMercyBot",
    };

    private TrayIcon()
    {
        _trayIcon.ContextMenu = new()
        {
            Items =
            {
                new PopupMenuItem("Show Console", (_, _) => ToggleConsole()),
                new PopupMenuItem("Hide Console", (_, _) => ToggleConsole()),
                new PopupMenuSeparator(),
                new PopupMenuItem("Pause Server", (_, _) => Pause()),
                new PopupMenuItem("Restart Server", (_, _) => Restart()),
                new PopupMenuItem("Shutdown", (_, _) => Shutdown()),
            },
        };

        if (_trayIcon.ContextMenu?.Items.ElementAt(1) is not null)
        {
            _trayIcon.ContextMenu.Items.ElementAt(0).Visible = true;
            _trayIcon.ContextMenu.Items.ElementAt(1).Visible = false;
        }

        if (_trayIcon.ContextMenu?.Items.ElementAt(3) is not null)
        {
            _trayIcon.ContextMenu.Items.ElementAt(2).Visible = true;
            _trayIcon.ContextMenu.Items.ElementAt(3).Visible = false;
        }

        _trayIcon.Create();
    }

    private static void Pause() { }

    private void ToggleConsole()
    {
        Start.VsConsoleWindow(Start.ConsoleVisible == 1 ? 0 : 1);

        if (Start.ConsoleVisible == 1 && _trayIcon.ContextMenu?.Items.ElementAt(1) is not null)
        {
            _trayIcon.ContextMenu.Items.ElementAt(0).Visible = false;
            _trayIcon.ContextMenu.Items.ElementAt(1).Visible = true;
        }
        else if (_trayIcon.ContextMenu?.Items.ElementAt(1) is not null)
        {
            _trayIcon.ContextMenu.Items.ElementAt(0).Visible = true;
            _trayIcon.ContextMenu.Items.ElementAt(1).Visible = false;
        }
    }

    private static void Restart() { }

    private void Shutdown()
    {
        _trayIcon.Dispose();
        Environment.Exit(0);
    }

    public static Task Make()
    {
        if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362)
        )
        {
            TrayIcon _ = new();
        }

        return Task.CompletedTask;
    }
}
