using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using SpotifyAPI.Web;
using Newtonsoft.Json;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Http;
using NoMercyBot.Services.Spotify.Dto;
using RestSharp;

namespace NoMercyBot.Services.Spotify;

public class SpotifyApiService
{
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _conf;
    private readonly ILogger<SpotifyApiService> _logger;

    private SpotifyClient SpotifyClient => new(Service.AccessToken ??
                                               throw new InvalidOperationException(
                                                   "Spotify AccessToken is not set. Please authenticate first."));

    private readonly DiscordApiService _discordApiService;
    private readonly ResilientApiClient _apiClient;

    public Service Service => SpotifyConfig.Service();

    public SpotifyState? SpotifyState { get; set; }

    public string ClientId => Service.ClientId ?? throw new InvalidOperationException("Spotify ClientId is not set.");

    public SpotifyApiService(
        IServiceScopeFactory serviceScopeFactory,
        DiscordApiService discordApiService,
        IConfiguration conf,
        ILogger<SpotifyApiService> logger,
        ResilientApiClientFactory apiClientFactory)
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _conf = conf;
        _logger = logger;
        _discordApiService = discordApiService;
        _apiClient = apiClientFactory.GetClient(SpotifyConfig.ApiUrl);

        SpotifyState = GetPlayerState().Result;
    }

    public async Task<bool> SetVolume(PlayerVolumeRequest request)
    {
        if (request.VolumePercent is < 0 or > 100)
            throw new InvalidOperationException($"Volume must be between 0 and 100. Received: {request.VolumePercent}");

        if (await SpotifyClient.Player.SetVolume(request))
        {
            _logger.LogInformation("Volume set to {Volume} successfully.", request.VolumePercent);
            return true;
        }

        throw new InvalidOperationException("Cannot set volume on Spotify.");
    }

    public async Task<int> GetVolume()
    {
        _logger.LogInformation("Fetching current volume from Spotify...");

        RestRequest request = new("/me/player");
        request.AddHeader("Authorization", $"Bearer {Service.AccessToken}");
        request.AddHeader("Content-Type", "application/json");

        RestResponse response = await _apiClient.ExecuteAsync(request);
        if (!response.IsSuccessful)
        {
            _logger.LogError("Failed to fetch player state: {StatusCode} - {Content}", response.StatusCode,
                response.Content);
            throw new InvalidOperationException("Could not retrieve player state from Spotify.");
        }

        SpotifyState? playerState = response.Content?.FromJson<SpotifyState>();
        if (playerState?.Device?.VolumePercent is null)
            throw new InvalidOperationException("Volume percent is not available in the player state.");

        return playerState.Device.VolumePercent;
    }

    public async Task<bool> ResumePlayback()
    {
        if (await SpotifyClient.Player.ResumePlayback())
        {
            _logger.LogInformation("Playback resumed successfully.");
            return true;
        }

        throw new InvalidOperationException("Cannot resume playback on Spotify.");
    }

    public async Task<bool> Pause()
    {
        if (await SpotifyClient.Player.PausePlayback())
        {
            _logger.LogInformation("Playback paused successfully.");
            return true;
        }

        throw new InvalidOperationException("Cannot pause playback on Spotify.");
    }

    public async Task<bool> PreviousTrack()
    {
        if (await SpotifyClient.Player.SkipPrevious())
        {
            _logger.LogInformation("Successfully skipped to previous track.");
            return true;
        }

        throw new InvalidOperationException("Could not skip previous");
    }

    public async Task<bool> NextTrack()
    {
        if (await SpotifyClient.Player.SkipNext())
        {
            _logger.LogInformation("Successfully skipped to next track.");
            return true;
        }

        throw new InvalidOperationException("Could not skip next");
    }

    public async Task<SnapshotResponse> AddToPlaylist(string playlistId, PlaylistAddItemsRequest request)
    {
        if (string.IsNullOrEmpty(playlistId))
            throw new ArgumentException("Playlist ID cannot be null or empty.", nameof(playlistId));

        if (request?.Uris == null || !request.Uris.Any())
            throw new ArgumentException("Request must contain at least one URI to add to the playlist.",
                nameof(request));

        _logger.LogInformation("Adding items to Spotify playlist: {PlaylistId}", playlistId);
        return await SpotifyClient.Playlists.AddItems(playlistId, request);
    }

    public async Task<Dto.CurrentlyPlaying?> GetCurrentlyPlaying()
    {
        _logger.LogInformation("Fetching currently playing track from Spotify...");

        RestRequest request = new("/me/player/currently-playing");
        request.AddHeader("Authorization", $"Bearer {Service.AccessToken}");
        request.AddHeader("Content-Type", "application/json");

        RestResponse response = await _apiClient.ExecuteAsync(request);
        if (!response.IsSuccessful)
        {
            _logger.LogError("Failed to fetch currently playing track: {StatusCode} - {Content}", response.StatusCode,
                response.Content);
            throw new InvalidOperationException("Could not retrieve currently playing track from Spotify.");
        }

        Dto.CurrentlyPlaying? currentlyPlaying = response.Content?.FromJson<Dto.CurrentlyPlaying>();

        return currentlyPlaying;
    }

    public async Task<SpotifyState?> GetPlayerState()
    {
        RestRequest request = new("/me/player");
        request.AddHeader("Authorization", $"Bearer {Service.AccessToken}");
        request.AddHeader("Content-Type", "application/json");

        RestResponse response = await _apiClient.ExecuteAsync(request);

        return response.Content.FromJson<SpotifyState>();
    }

    public async Task<FullTrack?> GetTrack(string trackId)
    {
        if (string.IsNullOrEmpty(trackId))
            throw new ArgumentException("Track ID cannot be null or empty.", nameof(trackId));

        _logger.LogInformation("Fetching track information for track ID: {TrackId}", trackId);

        try
        {
            FullTrack track = await SpotifyClient.Tracks.Get(trackId);
            return track;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch track information for track ID: {TrackId}", trackId);
            return null;
        }
    }

    public async Task<FullEpisode?> GetEpisode(string episodeId)
    {
        if (string.IsNullOrEmpty(episodeId))
            throw new ArgumentException("Track ID cannot be null or empty.", nameof(episodeId));

        _logger.LogInformation("Fetching episode information for episode ID: {TrackId}", episodeId);

        try
        {
            FullEpisode episode = await SpotifyClient.Episodes.Get(episodeId);
            return episode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch episode information for episode ID: {TrackId}", episodeId);
            return null;
        }
    }

    public async Task<bool> AddToQueue(PlayerAddToQueueRequest request)
    {
        if (request?.Uri == null)
            throw new ArgumentException("Request must contain a valid URI to add to the queue.", nameof(request));

        if (await SpotifyClient.Player.AddToQueue(request))
        {
            _logger.LogInformation("Successfully added item to queue: {Uri}", request.Uri);
            return true;
        }

        throw new InvalidOperationException("Could not add item to queue on Spotify.");
    }

    public async Task<SpotifyQueueResponse?> GetQueue()
    {
        _logger.LogInformation("Fetching current playlist from Spotify...");
        _logger.LogInformation("Fetching currently playing track from Spotify...");

        RestRequest request = new("/me/player/queue");
        request.AddHeader("Authorization", $"Bearer {Service.AccessToken}");
        request.AddHeader("Content-Type", "application/json");

        RestResponse response = await _apiClient.ExecuteAsync(request);
        if (!response.IsSuccessful)
        {
            _logger.LogError("Failed to fetch currently playing track: {StatusCode} - {Content}", response.StatusCode,
                response.Content);
            throw new InvalidOperationException("Could not retrieve currently playing track from Spotify.");
        }

        SpotifyQueueResponse? queue = response.Content?.FromJson<SpotifyQueueResponse>();

        return queue;
    }

    public async Task<object> GetDevices()
    {
        _logger.LogInformation("Fetching devices from Spotify...");

        DeviceResponse? devices = await SpotifyClient.Player.GetAvailableDevices();

        if (devices?.Devices is null || !devices.Devices.Any())
            throw new InvalidOperationException("No devices found on Spotify.");

        return devices.Devices;
    }

    public async Task<bool> TransferPlayback(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty.", nameof(deviceId));

        _logger.LogInformation("Transferring playback to device: {DeviceId}", deviceId);

        if (await SpotifyClient.Player.TransferPlayback(new(new List<string> { deviceId })))
        {
            _logger.LogInformation("Playback transferred successfully.");
            return true;
        }

        throw new InvalidOperationException("Could not transfer playback on Spotify.");
    }

    public async Task InitializeConnectionAsync(string connectionId)
    {
        try
        {
            string? accessToken = await _discordApiService.GetSpotifyToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Discord session token is not set. Cannot retrieve Spotify token.");
                return;
            }

            RestRequest request = new("/me/notifications/player", Method.Put);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("Content-Type", "application/json");
            request.Resource += $"?connection_id={Uri.EscapeDataString(connectionId)}";

            RestResponse response = await _apiClient.ExecuteAsync(request);
            string json = response.Content ?? string.Empty;
            dynamic? data = JsonConvert.DeserializeObject(json);

            if (data?.message != "Subscription created")
                throw new InvalidOperationException($"Error creating subscription: {data?.message}");

            _logger.LogInformation("Connection initialized: {Data}", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing connection");
        }
    }

    public async Task<SpotifyMeResponse> GetSpotifyMe()
    {
        RestRequest request = new("me");
        request.AddHeader("Authorization", $"Bearer {Service.AccessToken}");

        RestResponse response = await _apiClient.ExecuteAsync(request);

        if (!response.IsSuccessful)
            throw new("Invalid access token");

        SpotifyMeResponse? meResponse = response.Content?.FromJson<SpotifyMeResponse>();
        if (meResponse == null)
            throw new("Invalid response from Spotify.");

        return meResponse;
    }

    public async Task<FullPlaylist?> GetPlaylist(string playlistId)
    {
        if (string.IsNullOrEmpty(playlistId))
            throw new ArgumentException("Playlist ID cannot be null or empty.", nameof(playlistId));

        _logger.LogInformation("Fetching playlist information for playlist ID: {PlaylistId}", playlistId);

        try
        {
            FullPlaylist playlist = await SpotifyClient.Playlists.Get(playlistId);
            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch playlist information for playlist ID: {PlaylistId}", playlistId);
            return null;
        }
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
