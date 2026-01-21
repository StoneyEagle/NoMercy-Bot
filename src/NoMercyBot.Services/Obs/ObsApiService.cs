using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NoMercyBot.Database.Models;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;

namespace NoMercyBot.Services.Obs;

public class ObsApiService
{
    private readonly IConfiguration _conf;
    private readonly ILogger<ObsApiService> _logger;
    private OBSWebsocket? _obs;

    public Service Service => ObsConfig.Service();
    
    public ObsApiService(IConfiguration conf, ILogger<ObsApiService> logger)
    {
        _conf = conf;
        _logger = logger;
    }

    private async Task<OBSWebsocket> GetConnectedClient()
    {
        if (_obs is { IsConnected: true })
            return _obs;

        _obs = new();

        string url = "ws://192.168.2.201:4456";

        try
        {
            _logger.LogInformation("Connecting to OBS WebSocket at {Url}", url);
            _obs.ConnectAsync(url, string.Empty);
            _logger.LogInformation("Successfully connected to OBS WebSocket");
            return _obs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to OBS WebSocket");
            throw new InvalidOperationException("Failed to connect to OBS WebSocket", ex);
        }
    }

    public async Task SetCurrentScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("Scene name cannot be null or empty.");

        _logger.LogInformation("Switching to scene: {SceneName}", sceneName);

        OBSWebsocket obs = await GetConnectedClient();

        try
        {
            await Task.Run(() => obs.SetCurrentProgramScene(sceneName));
            _logger.LogInformation("Successfully switched to scene: {SceneName}", sceneName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to scene: {SceneName}", sceneName);
            throw new InvalidOperationException($"Failed to switch to scene: {sceneName}", ex);
        }
    }

    public async Task StopStreaming()
    {
        _logger.LogInformation("Stopping stream");

        OBSWebsocket obs = await GetConnectedClient();

        try
        {
            obs.StopStream();
            _logger.LogInformation("Successfully stopped stream");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop stream");
            throw new InvalidOperationException("Failed to stop stream", ex);
        }
    }

    public async Task<StreamStatus> GetStreamStatus()
    {
        _logger.LogInformation("Getting stream status");

        OBSWebsocket obs = await GetConnectedClient();

        try
        {
            OutputStatus status = obs.GetStreamStatus();
            _logger.LogInformation("Stream is {Status}", status.IsActive ? "active" : "inactive");
            return new()
            {
                IsActive = status.IsActive,
                Duration = (int)status.Duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stream status");
            throw new InvalidOperationException("Failed to get stream status", ex);
        }
    }

    public void Dispose()
    {
        if (_obs != null && _obs.IsConnected)
        {
            _obs.Disconnect();
        }
    }
}

public class StreamStatus
{
    public bool IsActive { get; set; }
    public int Duration { get; set; }
}