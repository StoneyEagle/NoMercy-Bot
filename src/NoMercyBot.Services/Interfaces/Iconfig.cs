using NoMercyBot.Database.Models;

namespace NoMercyBot.Services.Interfaces;

public interface IConfig
{
    static Service _service { get; }

    static Service Service()
    {
        return _service;
    }

    static bool IsEnabled => true;
    static string ApiUrl { get; }
    static string AuthUrl { get; }
    static string RedirectUri { get; }
    static Dictionary<string, string> AvailableScopes { get; }
    static Dictionary<string, string> Scopes { get; }
}
