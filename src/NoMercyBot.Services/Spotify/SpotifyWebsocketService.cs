using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercyBot.Database;
using NoMercyBot.Globals.NewtonSoftConverters;
using NoMercyBot.Services.Discord;
using NoMercyBot.Services.Spotify.Dto;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Services.Spotify;

public class SpotifyWebsocketService : IHostedService, IDisposable
{
    private readonly ILogger<SpotifyWebsocketService> _logger;
    private readonly SpotifyApiService _spotifyApiService;
    private readonly SpotifyAuthService _spotifyAuthService;
    private readonly IWidgetEventService _widgetEventService;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _dbContext;
    private int _attempts = 0;
    private const int MaxAttempts = 5;
    private Timer? _pingTimer;

    public SpotifyWebsocketService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SpotifyWebsocketService> logger,
        SpotifyApiService spotifyApiService,
        SpotifyAuthService spotifyAuthService,
        IWidgetEventService widgetEventService
    )
    {
        _scope = serviceScopeFactory.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _logger = logger;
        _spotifyApiService = spotifyApiService;
        _spotifyAuthService = spotifyAuthService;
        _widgetEventService = widgetEventService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        DiscordConfig.SessionToken = _dbContext
            .Configurations.FirstOrDefault(config => config.Key == "_DiscordSessionToken")
            ?.SecureValue;

        if (string.IsNullOrWhiteSpace(DiscordConfig.SessionToken))
        {
            _logger.LogInformation("Discord session token is not set. falling back to polling.");

            return;
        }

        await ConnectAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    private async Task ConnectAsync()
    {
        _cts = new();
        _socket = new();

        Uri uri = new(
            $"wss://dealer.spotify.com/?access_token={_spotifyAuthService.Service.AccessToken}"
        );
        try
        {
            await _socket.ConnectAsync(uri, _cts.Token);
            _attempts = 0;
            _logger.LogInformation("Spotify websocket connection opened");
            _pingTimer = new(_ => Ping(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            _ = ReceiveLoop(_cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Spotify websocket");
            ReconnectWithBackoff();
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        while (
            _socket is { State: WebSocketState.Open } && !cancellationToken.IsCancellationRequested
        )
            try
            {
                WebSocketReceiveResult result = await _socket.ReceiveAsync(
                    buffer,
                    cancellationToken
                );
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Spotify websocket connection closed");
                    await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed by client",
                        cancellationToken
                    );
                    ReconnectWithBackoff();
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Spotify websocket receive loop");
                ReconnectWithBackoff();
                break;
            }
    }

    private async Task ProcessMessageAsync(string message)
    {
        try
        {
            SpotifyEventBase? baseEvent = JsonConvert.DeserializeObject<SpotifyEventBase>(message);
            if (baseEvent == null || baseEvent.Type == null)
                return;

            if (baseEvent.Type != "message")
                return;

            SpotifyConnectEvent? connectEvent = JsonConvert.DeserializeObject<SpotifyConnectEvent>(
                message
            );
            if (
                connectEvent?.Headers?.SpotifyConnectionId is { } connectionId
                && !string.IsNullOrEmpty(connectionId)
            )
            {
                await _spotifyApiService.InitializeConnectionAsync(connectionId);
                return;
            }

            try
            {
                SpotifyMessageEvent? messageEvent =
                    JsonConvert.DeserializeObject<SpotifyMessageEvent>(message);
                if (messageEvent?.Payloads != null)
                {
                    foreach (SpotifyMessageEventPayload payload in messageEvent.Payloads)
                    foreach (SpotifyEventElement evt in payload.Events)
                    {
                        if (evt is not { Type: "PLAYER_STATE_CHANGED", Event.State: not null })
                            continue;

                        _spotifyApiService.SpotifyState = evt.Event.State;

                        await _widgetEventService.PublishEventAsync(
                            "spotify.state.changed",
                            evt.Event.State
                        );

                        _logger.LogInformation("Player state changed");
                    }

                    return;
                }
            }
            catch (Exception e)
            {
                //
            }

            try
            {
                SpotifyLikeEvent? likeEvent = JsonConvert.DeserializeObject<SpotifyLikeEvent>(
                    message
                );
                if (likeEvent?.Payloads != null)
                    foreach (string payloadString in likeEvent.Payloads)
                    {
                        SpotifyLikePayload? payload =
                            JsonConvert.DeserializeObject<SpotifyLikePayload>(payloadString);

                        if (payload?.Items == null)
                            continue;

                        List<SpotifyLikeItem> likedItems = payload
                            .Items.Where(item => item is { Type: "track" })
                            .ToList();

                        if (likedItems.Count <= 0)
                            continue;

                        await _widgetEventService.PublishEventAsync(
                            "spotify.track.like",
                            !likedItems.First().Removed
                        );

                        _logger.LogInformation(
                            "Track liked: {Items}",
                            JsonConvert.SerializeObject(likedItems)
                        );
                    }
            }
            catch (Exception)
            {
                //
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Spotify websocket message");
        }
    }

    private void Ping()
    {
        if (_socket is { State: WebSocketState.Open })
            SendMessage(new() { { "type", "ping" } });
        else
            _logger.LogWarning("Spotify websocket is not open. Ping not sent.");
    }

    private void ReconnectWithBackoff()
    {
        if (_attempts < MaxAttempts)
        {
            _attempts++;
            Task.Delay(1000).ContinueWith(_ => ConnectAsync());
        }
        else
        {
            _logger.LogError("Max Spotify websocket reconnect attempts reached.");
        }
    }

    private void SendMessage(Dictionary<string, string> message)
    {
        if (_socket is { State: WebSocketState.Open })
        {
            byte[] msg = Encoding.UTF8.GetBytes(message.ToJson());
            _ = _socket.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        else
        {
            _logger.LogWarning(
                "Spotify websocket is not open. Message not sent: {Message}",
                message
            );
        }
    }

    private void Close()
    {
        if (_socket != null)
        {
            _cts?.Cancel();
            _socket.Dispose();
            _socket = null;
        }

        _pingTimer?.Dispose();
        _pingTimer = null;
    }

    public void Dispose()
    {
        Close();
        _cts?.Dispose();
    }
}
