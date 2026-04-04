## 8. Background Services

### 8.1 TokenRefreshService

**Current**: Checks all Service and BotAccount rows every 1 minute, refreshes tokens expiring within 5 minutes.

**Multi-channel change**: No structural change. The service already iterates all rows. With multi-channel, there are simply more Service rows (one per channel per provider). After refreshing, the service should update the ChannelRegistry's cached token.

### 8.2 SpotifyPollingService (replaces SpotifyWebsocketService)

**Current**: One undocumented websocket to `dealer.spotify.com` using a Discord session token hack. REMOVED.

**Replacement**: `SpotifyPollingService` polls the official Spotify Web API:
- `ConcurrentDictionary<string, SpotifyPollState>` tracks per-channel state
- Polls `GET /me/player` using the channel's official Spotify OAuth token
- Poll interval: 3s when stream is live, disabled when offline
- On track change: publish `spotify.player.state` event to widgets
- On play/pause change: publish state update
- Lifecycle: starts on `stream.online`, stops on `stream.offline`
- Rate limit aware: Spotify allows 180 requests/minute. With 3s polling, one channel uses 20 req/min. Supports ~9 concurrent live channels per rate limit window. For more, increase poll interval dynamically.

### 8.3 ShoutoutQueueService

**Current**: Already uses per-channel dictionaries for queues and cooldowns.

**Multi-channel change**: The processing loop already iterates all channel IDs. On startup, `CheckIfStreamIsLiveAsync` must iterate all active channels. Access tokens for API calls come from each channel's Service record.

### 8.4 WatchStreakService

**Current**: One IRC connection to one channel, running permanently from bot startup.

**Multi-channel change**: IRC connections are expensive and unnecessary when a channel is offline. Instead of connecting at startup:
- **Connect on `stream.online` event**: When EventSub fires `stream.online` for a channel, `WatchStreakService` joins that channel's IRC.
- **Disconnect on `stream.offline` event**: When the stream ends, part from the channel and close the connection.
- **No idle connections**: Channels that are offline have zero IRC overhead.
- TwitchLib IRC client supports joining multiple channels on one connection. Use a single connection and dynamically join/part channels as they go live/offline.
- Expose `JoinChannel(string channelName)` and `PartChannel(string channelName)` methods called from the EventSub event handlers.
- On platform startup, check which channels are currently live and join only those.

### 8.5 TwitchWebsocketHostedService

**Current**: One EventSub websocket session subscribing to events for one broadcaster.

**Multi-channel change**: 
- At startup, load all active channels and their enabled EventSubscriptions.
- Twitch EventSub websocket allows subscribing to events for different broadcasters on the same connection (up to the subscription limit).
- EventSub subscriptions are created using the channel's OAuth grant token (which the broadcaster explicitly authorized with the required scopes). Twitch EventSub requires the token to have the appropriate scopes for each subscription type.
- The platform uses a single websocket connection. Each subscription is created using the grant token for that channel. These are NOT personal tokens -- they are tokens the user explicitly issued to the platform via the OAuth consent screen.

### 8.6 Emote Services (BTTV, FrankerFaceZ, SevenTV)

**Current**: Singleton hosted services that fetch emotes for one channel.

**Multi-channel change**: Fetch emotes for all active channels. Store per-channel emote sets in a dictionary. Refresh periodically.

### 8.7 ClaudeIpcService -- REMOVED

Removed from the multi-channel platform. See Section 5.2.13.

---
