using System.Runtime.InteropServices;

namespace NoMercyBot.Globals.Information;

public static class Info
{
    public static readonly string ApplicationName = "NoMercy Bot";
    public static string DeviceName { get; set; } = Environment.MachineName;
    public static readonly Guid DeviceId = Software.GetDeviceId();
    public static readonly string Os = RuntimeInformation.OSDescription;
    public static readonly string Platform = Software.GetPlatform();
    public static readonly string Architecture = RuntimeInformation.ProcessArchitecture.ToString();
    public static readonly string? OsVersion = Software.GetSystemVersion();
    public static readonly DateTime BootTime = Software.GetBootTime();
    public static readonly DateTime StartTime = DateTime.UtcNow;
    public static readonly string ExecSuffix = Platform == "windows" ? ".exe" : "";
    public static readonly string IconSuffix =
        Platform == "windows" ? ".ico"
        : Platform == "macos" ? ".icns"
        : ".png";

    public static string UserId { get; set; } = string.Empty;
    public static string UserName { get; set; } = string.Empty;

    public static string BotUserId { get; set; } = string.Empty;
    public static string BotUserName { get; set; } = string.Empty;
}
