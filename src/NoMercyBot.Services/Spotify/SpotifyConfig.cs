using NoMercyBot.Database.Models;
using NoMercyBot.Globals.Information;
using NoMercyBot.Services.Interfaces;

namespace NoMercyBot.Services.Spotify;

public class SpotifyConfig : IConfig
{
    internal static Service? _service;

    public static Service Service()
    {
        return _service ??= new();
    }

    public bool IsEnabled => Service().Enabled;

    public static string ApiUrl { get; } = "https://api.spotify.com/v1";
    public static string AuthUrl { get; } = "https://accounts.spotify.com/api";

    public static string RedirectUri =>
        string.IsNullOrEmpty(Config.BaseUrl)
            ? $"http://localhost:{Config.InternalClientPort}/oauth/spotify/callback"
            : $"{Config.BaseUrl}/oauth/spotify/callback";

    public static readonly Dictionary<string, string> AvailableScopes = new()
    {
        { "user-read-playback-state", "Read the current playback state of a user." },
        { "user-modify-playback-state", "Modify the playback state of a user." },
        { "user-read-currently-playing", "Read the currently playing track for a user." },
        { "playlist-read-private", "Read private playlists of a user." },
        { "playlist-modify-public", "Modify public playlists of a user." },
        { "playlist-modify-private", "Modify private playlists of a user." },
        { "playlist-read-collaborative", "Read collaborative playlists of a user." },
        { "user-follow-read", "Read the list of users followed by a user." },
        { "user-follow-modify", "Follow or unfollow users on behalf of a user." },
        { "user-library-read", "Read the saved tracks and albums of a user." },
        { "user-library-modify", "Save or remove tracks and albums from a user's library." },
        { "app-remote-control", "Control playback on Spotify clients remotely." },
        { "streaming", "Stream audio from Spotify clients." },
        { "ugc-image-upload", "Upload images for Spotify content." },
    };
}
