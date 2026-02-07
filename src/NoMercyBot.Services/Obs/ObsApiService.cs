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
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private DateTime _lastConnectionAttempt = DateTime.MinValue;
    private const int ConnectionCooldownMs = 2000;
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 1000;

    public Service Service => ObsConfig.Service();

    public ObsApiService(IConfiguration conf, ILogger<ObsApiService> logger)
    {
        _conf = conf;
        _logger = logger;
    }

    private string GetObsUrl()
    {
        return _conf["Obs:WebSocketUrl"] ?? "ws://192.168.2.201:4456";
    }

    private string GetObsPassword()
    {
        return _conf["Obs:Password"] ?? string.Empty;
    }

    private async Task<OBSWebsocket> GetConnectedClient()
    {
        // Fast path: already connected
        if (_obs is { IsConnected: true })
            return _obs;

        await _connectionLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_obs is { IsConnected: true })
                return _obs;

            // Respect cooldown to avoid hammering OBS
            var timeSinceLastAttempt = DateTime.UtcNow - _lastConnectionAttempt;
            if (timeSinceLastAttempt.TotalMilliseconds < ConnectionCooldownMs)
            {
                await Task.Delay(ConnectionCooldownMs - (int)timeSinceLastAttempt.TotalMilliseconds);
            }

            string url = GetObsUrl();
            string password = GetObsPassword();
            Exception? lastException = null;

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                _lastConnectionAttempt = DateTime.UtcNow;

                try
                {
                    // Dispose old instance if exists
                    if (_obs != null)
                    {
                        try { _obs.Disconnect(); } catch { /* ignore */ }
                        _obs = null;
                    }

                    _obs = new OBSWebsocket();

                    _logger.LogInformation("Connecting to OBS WebSocket at {Url} (attempt {Attempt}/{MaxAttempts})",
                        url, attempt, MaxRetryAttempts);

                    // Use synchronous Connect with timeout - ConnectAsync doesn't properly await
                    var connectTask = Task.Run(() => _obs.ConnectAsync(url, password));

                    // Wait for connection with timeout
                    if (!await WaitForConnection(_obs, TimeSpan.FromSeconds(5)))
                    {
                        throw new TimeoutException("Connection timed out waiting for OBS to respond");
                    }

                    if (!_obs.IsConnected)
                    {
                        throw new InvalidOperationException("OBS WebSocket connection failed - not connected after handshake");
                    }

                    _logger.LogInformation("Successfully connected to OBS WebSocket");
                    return _obs;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "OBS connection attempt {Attempt}/{MaxAttempts} failed", attempt, MaxRetryAttempts);

                    if (attempt < MaxRetryAttempts)
                    {
                        int delay = RetryDelayMs * attempt; // Linear backoff
                        _logger.LogInformation("Retrying OBS connection in {Delay}ms...", delay);
                        await Task.Delay(delay);
                    }
                }
            }

            _logger.LogError(lastException, "Failed to connect to OBS WebSocket after {MaxAttempts} attempts", MaxRetryAttempts);
            throw new InvalidOperationException($"Failed to connect to OBS WebSocket after {MaxRetryAttempts} attempts", lastException);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<bool> WaitForConnection(OBSWebsocket obs, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (obs.IsConnected)
                return true;
            await Task.Delay(100);
        }
        return obs.IsConnected;
    }

    public bool IsConnected => _obs?.IsConnected ?? false;

    public async Task SetCurrentScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("Scene name cannot be null or empty.");

        _logger.LogInformation("Switching to scene: {SceneName}", sceneName);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                OBSWebsocket obs = await GetConnectedClient();

                await Task.Run(() => obs.SetCurrentProgramScene(sceneName));

                // Verify the scene actually changed
                var currentScene = await Task.Run(() => obs.GetCurrentProgramScene());
                if (currentScene != sceneName)
                {
                    throw new InvalidOperationException($"Scene change verification failed. Expected '{sceneName}', got '{currentScene}'");
                }

                _logger.LogInformation("Successfully switched to scene: {SceneName}", sceneName);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to switch scene (attempt {Attempt}/{MaxAttempts}): {SceneName}",
                    attempt, MaxRetryAttempts, sceneName);

                // Force reconnection on next attempt
                if (_obs != null && attempt < MaxRetryAttempts)
                {
                    try { _obs.Disconnect(); } catch { /* ignore */ }
                    _obs = null;
                    await Task.Delay(RetryDelayMs * attempt);
                }
            }
        }

        _logger.LogError(lastException, "Failed to switch to scene after {MaxAttempts} attempts: {SceneName}",
            MaxRetryAttempts, sceneName);
        throw new InvalidOperationException($"Failed to switch to scene: {sceneName}", lastException);
    }

    public async Task StopStreaming()
    {
        _logger.LogInformation("Stopping stream");

        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                OBSWebsocket obs = await GetConnectedClient();

                await Task.Run(() => obs.StopStream());
                _logger.LogInformation("Successfully stopped stream");
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to stop stream (attempt {Attempt}/{MaxAttempts})", attempt, MaxRetryAttempts);

                // Force reconnection on next attempt
                if (_obs != null && attempt < MaxRetryAttempts)
                {
                    try { _obs.Disconnect(); } catch { /* ignore */ }
                    _obs = null;
                    await Task.Delay(RetryDelayMs * attempt);
                }
            }
        }

        _logger.LogError(lastException, "Failed to stop stream after {MaxAttempts} attempts", MaxRetryAttempts);
        throw new InvalidOperationException("Failed to stop stream", lastException);
    }

    public async Task<StreamStatus> GetStreamStatus()
    {
        _logger.LogInformation("Getting stream status");

        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                OBSWebsocket obs = await GetConnectedClient();

                OutputStatus status = await Task.Run(() => obs.GetStreamStatus());
                _logger.LogInformation("Stream is {Status}", status.IsActive ? "active" : "inactive");
                return new()
                {
                    IsActive = status.IsActive,
                    Duration = (int)status.Duration
                };
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to get stream status (attempt {Attempt}/{MaxAttempts})", attempt, MaxRetryAttempts);

                // Force reconnection on next attempt
                if (_obs != null && attempt < MaxRetryAttempts)
                {
                    try { _obs.Disconnect(); } catch { /* ignore */ }
                    _obs = null;
                    await Task.Delay(RetryDelayMs * attempt);
                }
            }
        }

        _logger.LogError(lastException, "Failed to get stream status after {MaxAttempts} attempts", MaxRetryAttempts);
        throw new InvalidOperationException("Failed to get stream status", lastException);
    }

    public async Task<bool> TestConnection()
    {
        try
        {
            await GetConnectedClient();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _connectionLock.Dispose();
        if (_obs != null)
        {
            try { _obs.Disconnect(); } catch { /* ignore */ }
            _obs = null;
        }
    }
}

public class StreamStatus
{
    public bool IsActive { get; set; }
    public int Duration { get; set; }
}