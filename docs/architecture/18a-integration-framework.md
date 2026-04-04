## 18a. Integration Framework -- Adding New Streaming Services

### 18a.1 Problem

The current spec defines 4 integrations (Twitch, Spotify, Discord, OBS) each with custom code. Adding a new service (YouTube Music, Kick, Streamlabs, StreamElements, Ko-fi, Patreon, Nightbot import, etc.) requires reading the entire spec to understand the pattern. There should be a clear, repeatable framework.

### 18a.2 Integration Categories

Every streaming-related service falls into one of these categories:

| Category | Interface | Examples | What It Does |
|----------|-----------|----------|-------------|
| **Chat** | `IChatProvider` | Twitch, Kick (future), YouTube Live (future) | Read/send chat messages, moderate |
| **Music** | `IMusicProvider` | Spotify, YouTube Music (future), SoundCloud (future) | Playback control, queue, search |
| **Notifications** | `INotificationProvider` | Discord, Telegram (future), Slack (future) | Send go-live alerts, stream-ended updates |
| **Stream Control** | `IStreamControlProvider` | OBS, Streamlabs (future) | Scene switching, start/stop, source control |
| **Donations** | `IDonationProvider` | Ko-fi (future), Patreon (future), StreamElements (future) | Donation alerts, goal tracking |
| **Emotes** | `IEmoteProvider` | BTTV, FFZ, 7TV, Twitch | Emote resolution, badges |
| **TTS** | `ITtsProvider` | Edge TTS, Azure, Google, ElevenLabs | Voice synthesis |
| **Analytics** | `IAnalyticsProvider` | Twitch (built-in), StreamElements (future) | Viewer stats, stream stats |

### 18a.3 Integration Lifecycle

Every integration follows the same lifecycle:

```
1. REGISTER    -- Provider implements interface, registered in DI
2. CONFIGURE   -- Broadcaster connects via OAuth or API key in dashboard
3. AUTHORIZE   -- Tokens/keys stored in Service table (encrypted)
4. ACTIVATE    -- ChannelRegistry loads provider for the channel
5. OPERATE     -- Provider serves requests (poll, push, or on-demand)
6. REFRESH     -- TokenRefreshService auto-refreshes OAuth tokens
7. DISCONNECT  -- Broadcaster removes integration, tokens destroyed
```

### 18a.4 How to Add a New Integration -- Step by Step

This is the exact checklist for adding any new streaming service:

#### Step 1: Define the category

Which interface does it implement? If none fit, propose a new `I*Provider` interface. The interface must:
- Be async throughout
- Accept `broadcasterId` on every method (multi-channel)
- Return `Result<T>` for error handling (never throw for expected failures)
- Have a `ProviderName` property
- Have `IsConnected(broadcasterId)` check

#### Step 2: Implement the provider

```csharp
public class SpotifyMusicProvider : IMusicProvider
{
    public string ProviderName => "spotify";

    // DI-injected dependencies
    private readonly IChannelRegistry _channels;
    private readonly IServiceTokenStore _tokens;
    private readonly ILogger<SpotifyMusicProvider> _logger;

    public async Task<bool> IsConnectedAsync(string broadcasterId)
    {
        return await _tokens.HasValidTokenAsync(broadcasterId, "Spotify");
    }

    public async Task<Result<MusicTrack>> GetCurrentlyPlayingAsync(string broadcasterId)
    {
        var token = await _tokens.GetTokenAsync(broadcasterId, "Spotify");
        if (token is null) return Result.Fail("Spotify not connected");
        // ... API call using token
    }
}
```

#### Step 3: Define the OAuth/auth flow

| Auth Type | When to Use | How It Works |
|-----------|-------------|-------------|
| **OAuth 2.0 (Authorization Code + PKCE)** | Spotify, Discord, Twitch, YouTube | Redirect flow, stores access+refresh in Service table |
| **OAuth 2.0 (Bot Token)** | Discord bot | Single platform token, not per-user |
| **API Key** | Azure TTS, Google TTS, ElevenLabs, Ko-fi | User provides key in dashboard, stored encrypted in Service table |
| **WebSocket Auth** | OBS | Password/token for local WebSocket connection |
| **Public API** | BTTV, FFZ, 7TV | No auth needed, public endpoints |

For OAuth providers, add to the `AuthController`:
```
GET  /api/v1/oauth/{providerName}/login     -- redirect to provider OAuth
GET  /api/v1/oauth/{providerName}/callback   -- handle callback, store tokens
POST /api/v1/oauth/{providerName}/disconnect -- revoke and delete tokens
```

For API key providers, the key is submitted via:
```
PUT /api/v1/channels/{channelId}/settings/providers/{providerName}
Body: { "apiKey": "..." }
```

#### Step 4: Register in DI

```csharp
// In ServiceCollectionExtensions.cs or a provider-specific extension
services.AddSingleton<IMusicProvider, SpotifyMusicProvider>();
services.AddSingleton<IMusicProvider, YouTubeMusicProvider>(); // future
```

Multiple providers of the same interface can be registered. The `IProviderResolver<T>` selects the active one per channel:

```csharp
public interface IProviderResolver<T> where T : class
{
    Task<T?> GetProviderAsync(string broadcasterId);
    Task<IReadOnlyList<T>> GetAllProvidersAsync(string broadcasterId);
}
```

The resolver checks the Service table for which provider the broadcaster has connected. If a broadcaster has both Spotify and YouTube Music connected, the resolver returns the one marked as primary.

#### Step 5: Add to the Features system

If the integration requires OAuth scopes (Twitch-side), add it to the `ChannelFeatures` progressive OAuth system (section 3.2).

If it's a standalone integration (Spotify, Discord, OBS), add an entry to the Integrations dashboard page with:
- Provider name and icon
- What features it enables (bullet list)
- Connect/disconnect button
- Connection status indicator
- Per-channel configuration (if needed)

#### Step 6: Add background service (if needed)

| Pattern | When | Example |
|---------|------|---------|
| **Polling** | API doesn't support push, need periodic state | Spotify playback state |
| **WebSocket** | Service offers real-time push | 7TV emote updates, OBS state changes |
| **Webhook** | Service sends HTTP callbacks | Ko-fi donations, Twitch EventSub |
| **On-demand** | Only called when user triggers | TTS synthesis, Discord message send |

Background services must be:
- Lifecycle-aware (start on `stream.online`, stop on `stream.offline` where applicable)
- Per-channel (dictionary of connections/poll states keyed by broadcasterId)
- Fault-tolerant (one channel's failure doesn't affect others)
- Registered as `IHostedService` or `BackgroundService`

#### Step 7: Add pipeline actions (if applicable)

If the integration exposes actions that should be available in the command pipeline builder (section 16), implement `ICommandAction`:

```csharp
public class MusicSkipAction : ICommandAction
{
    public string Type => "music_skip";
    public string Category => "Music";
    public string Description => "Skip the current track";
    public ActionParameterSchema[] Parameters => Array.Empty<ActionParameterSchema>();

    public async Task<ActionResult> ExecuteAsync(ActionContext context)
    {
        var provider = await _resolver.GetProviderAsync(context.BroadcasterId);
        if (provider is null) return ActionResult.Fail("No music provider connected");
        var result = await provider.SkipAsync(context.BroadcasterId);
        return result.IsSuccess ? ActionResult.Ok() : ActionResult.Fail(result.Error);
    }
}
```

Pipeline actions are auto-discovered by the dashboard via `GET /api/v1/actions`.

#### Step 8: Add widget events (if applicable)

If the integration produces real-time state that widgets should display, define event types:

| Integration | Event Type | Payload | When |
|-------------|-----------|---------|------|
| Spotify | `music.track.changed` | `{ track, artist, album, imageUrl }` | Track changes |
| Spotify | `music.state.changed` | `{ isPlaying, volume, progress }` | Play/pause/volume |
| Discord | `discord.notification.sent` | `{ guild, channel, messageId }` | Go-live sent |
| OBS | `obs.scene.changed` | `{ sceneName }` | Scene switches |
| Ko-fi | `donation.received` | `{ donor, amount, message }` | New donation |
| 7TV | `emote.set.changed` | `{ added[], removed[] }` | Emote set updated |

### 18a.5 Integration Checklist Template

When adding a new integration, copy this checklist:

```markdown
## New Integration: [Provider Name]

### Category: [Chat/Music/Notifications/StreamControl/Donations/Emotes/TTS/Analytics]
### Interface: [I*Provider]

- [ ] Provider class implements interface
- [ ] Auth flow defined (OAuth/API Key/Public/WebSocket)
- [ ] Auth endpoints added to AuthController (if OAuth)
- [ ] Provider registered in DI
- [ ] Service table row schema documented
- [ ] Added to Integrations dashboard page
- [ ] Added to Features system (if requires OAuth scopes)
- [ ] Background service created (if polling/websocket/webhook)
- [ ] Background service is lifecycle-aware (if applicable)
- [ ] Pipeline actions implemented (if applicable)
- [ ] Widget events defined (if applicable)
- [ ] Rate limits documented
- [ ] Error handling for API failures
- [ ] Token refresh flow (if OAuth)
- [ ] Disconnect/cleanup flow
- [ ] Unit tests for provider
- [ ] Integration tests for auth flow
- [ ] Dashboard UI for configuration
- [ ] Documentation in provider-apis.md
```

### 18a.6 Planned Future Integrations

| Service | Category | Priority | Auth | Notes |
|---------|----------|----------|------|-------|
| **YouTube Music** | Music | High | OAuth 2.0 | Needs Google API key + OAuth consent |
| **Kick** | Chat | Medium | TBD (Kick API is limited) | Read-only chat initially |
| **YouTube Live Chat** | Chat | Medium | OAuth 2.0 | YouTube Data API v3 |
| **Ko-fi** | Donations | Medium | Webhook + API key | Donation alerts |
| **StreamElements** | Donations/Analytics | Low | OAuth 2.0 | SE API for tips, overlays |
| **Streamlabs** | Donations/StreamControl | Low | OAuth 2.0 | SL API |
| **Patreon** | Donations | Low | OAuth 2.0 | Member/tier sync |
| **Telegram** | Notifications | Low | Bot token | Go-live notifications |
| **Nightbot** | Import | Low | API key | One-time command import tool |
| **StreamElements** | Import | Low | OAuth 2.0 | One-time command/loyalty import |

---
