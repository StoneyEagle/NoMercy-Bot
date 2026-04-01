using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Obs;

public class ObsConfig : IConfig
{
    internal static Service? _service;

    public static Service Service()
    {
        return _service ??= new();
    }

    public bool IsEnabled => Service().Enabled;

    public string ApiUrl { get; } = "http://localhost:4456";
    public string AuthUrl { get; } = $"http://localhost:4456/oauth2/token";

    public string RedirectUri => $"http://localhost:{Config.InternalClientPort}/oauth/obs/callback";

    public static readonly Dictionary<string, string> AvailableScopes = new()
    {
        { "obs:read", "Read OBS data" },
        { "obs:write", "Write OBS data" },
        { "obs:streaming", "Control streaming in OBS" },
        { "obs:recording", "Control recording in OBS" },
    };
}
