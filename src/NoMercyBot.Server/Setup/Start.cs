using System.Runtime.InteropServices;
using NoMercyBot.Globals.Information;

namespace NoMercyBot.Server.Setup;

public class Start
{
    [DllImport("Kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("User32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

    public static int ConsoleVisible { get; set; } = 1;

    public static void VsConsoleWindow(int i)
    {
        IntPtr hWnd = GetConsoleWindow();
        if (hWnd != IntPtr.Zero)
        {
            ConsoleVisible = i;
            ShowWindow(hWnd, i);
        }
    }

    public static async Task Init(List<TaskDelegate> tasks)
    {
        if (UserSettings.TryGetUserSettings(out Dictionary<string, (string, string)> settings))
            UserSettings.ApplySettings(settings);

        List<TaskDelegate> startupTasks =
        [
            new(AppFiles.CreateAppFolders),
            .. tasks,
            new(
                delegate
                {
                    if (
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362)
                    )
                        return TrayIcon.Make();
                    return Task.CompletedTask;
                }
            ),
            new(
                delegate
                {
                    DesktopIconCreator.CreateDesktopIcon(
                        Info.ApplicationName,
                        AppFiles.ServerExePath,
                        AppFiles.AppIcon
                    );
                    return Task.CompletedTask;
                }
            ),
        ];

        await RunStartup(startupTasks);

        // Thread queues = new(new Task(() => QueueRunner.Initialize().Wait()).Start)
        // {
        //     Name = "Queue workers",
        //     Priority = ThreadPriority.Lowest,
        //     IsBackground = true
        // };
        // queues.Start();
    }

    public static void MinimizeConsole()
    {
        if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362)
        )
            VsConsoleWindow(0);
    }

    private static async Task RunStartup(List<TaskDelegate> startupTasks)
    {
        foreach (TaskDelegate task in startupTasks)
            await task.Invoke();
    }
}
