## 5. Service Architecture

### 5.1 The ChannelRegistry Pattern

A new `ChannelRegistry` class replaces static singletons. It is registered as a singleton and holds per-channel state:

```
ChannelRegistry
  - ConcurrentDictionary<string, ChannelContext> _channels
  - GetOrCreate(broadcasterId) -> ChannelContext
  - Remove(broadcasterId)
  - GetAll() -> IEnumerable<ChannelContext>
```

`ChannelContext` holds per-channel instances of services and state:

```
ChannelContext
  - BroadcasterId: string
  - ChannelName: string
  - TwitchAccessToken (refreshed by TokenRefreshService)
  - SpotifyApiClient? (if connected)
  - DiscordApiClient? (if connected)
  - CommandRegistry: ConcurrentDictionary<string, ChatCommand>
  - RewardRegistry: ConcurrentDictionary<string/Guid, TwitchReward>
  - ShoutoutQueueState (per-channel cooldowns, session chatters)
  - IsLive: bool
  - CurrentStream: Stream?
```

### 5.2 Service-by-Service Changes

#### 5.2.1 TwitchChatService

**Current state**: Singleton. Static fields: `_userId`, `_userName`, `_accessToken`, `_botUserId`, `_botUserName`, `_botAccessToken`, `_botAppAccessToken`. Loads from a single Twitch service row and a single BotAccount row at construction time. All `SendMessage*` methods hardcode `_userId` as the broadcaster.

**Changes**:
- Remove all static fields.
- Bot identity (`_botUserId`, `_botUserName`, `_botAccessToken`, `_botAppAccessToken`) stays as instance fields on the singleton since the bot account is platform-wide.
- All `SendMessage*` and `SendReply*` methods gain a `string broadcasterId` parameter (replacing the `string channel` parameter which was mostly unused).
- `SendMessageAsUser` is REMOVED. The platform bot sends all messages. The platform never impersonates a user in chat.
- `SendMessageAsBot` and `SendReplyAsBot` use the platform bot token with the target channel's broadcaster ID.

Method signature changes:
- `SendMessageAsBot(string broadcasterId, string message)` -- broadcasterId replaces channel name
- `SendReplyAsBot(string broadcasterId, string message, string replyToMessageId)`
- `SendAnnouncementAsBot(string broadcasterId, string message, string? color)`
- `SendOneOffMessage(string channelId, string message)` -- no change, already uses channelId
- `SendOneOffMessageAsBot(string channelId, string message)` -- takes channelId instead of channel name

#### 5.2.2 TwitchApiService

**Current state**: Singleton. Uses `TwitchConfig.Service()` (a static lazy-loaded Service record) for all API calls. ClientId and AccessToken always come from this single static source.

**Changes**:
- For platform-level operations (fetching user info, channel info), continue using the platform app access token.
- For channel-specific operations (sending shoutouts, managing rewards, creating EventSub subscriptions), look up the OAuth grant token from the `Service` table for that channel's BroadcasterId. This is the token the user explicitly granted to the platform via the OAuth consent screen.
- Methods like `SendShoutoutAsync`, `CreateCustomReward`, `UpdateRedemptionStatus`, `GetCustomRewards`, `UpdateCustomReward` resolve the correct grant token from the ChannelRegistry.
- `RaidAsync` -- uses the channel's OAuth grant token (which includes the raid scope the user authorized).
- **Important**: These are NOT the user's personal tokens. They are OAuth grant tokens issued by Twitch to the platform-user pair, with specific scopes the user approved. The user can revoke them at any time.

#### 5.2.3 TwitchConfig / SpotifyConfig / DiscordConfig / ObsConfig

**Current state**: Each has a `static Service? _service` field loaded once at startup by ServiceResolver.

**Changes**:
- `TwitchConfig._service` remains as the **platform** Twitch app credentials (used for token validation, platform-level API calls).
- Per-channel credentials are stored in the ChannelRegistry / looked up from the `Service` table with the appropriate `BroadcasterId`.
- SpotifyConfig, DiscordConfig, ObsConfig static fields are removed. Their per-channel equivalents live in `ChannelContext`.

#### 5.2.4 TwitchCommandService

**Current state**: Transient. Has a static `ConcurrentDictionary<string, ChatCommand> Commands`. Loads all commands from DB at construction. All channels share the same command registry.

**Changes**:
- Commands registry moves into `ChannelContext.CommandRegistry`.
- `TwitchCommandService` takes a `broadcasterId` parameter to know which channel's commands to use.
- `LoadCommandsFromDatabase()` becomes `LoadCommandsFromDatabase(string broadcasterId)` and only loads commands for that channel.
- `ExecuteCommand` and `ExecuteCommandByName` resolve the command from the channel's registry.
- Platform scripts (from `CommandScriptLoader`) are registered in every channel's registry since they are shared.
- Database-backed custom commands are loaded only into the owning channel's registry.

#### 5.2.5 TwitchEventSubService

**Current state**: Singleton. Manages EventSubscription records in DB without channel scoping.

**Changes**:
- All methods accept a `broadcasterId` parameter.
- `GetAllSubscriptionsAsync(string broadcasterId)` filters by both `ProviderName` and `BroadcasterId`.
- `CreateSubscriptionAsync` associates the subscription with a channel.
- The `OnEventSubscriptionChanged` event includes `broadcasterId` in its signature.

#### 5.2.6 ShoutoutQueueService

**Current state**: Hosted service (singleton). Uses `ConcurrentDictionary` keyed by channelId for queues, cooldowns, session chatters. Already channel-aware in its data structures.

**Changes**: Minimal. The service already uses channel IDs as keys. The main change is:
- `CheckIfStreamIsLiveAsync()` must check ALL active channels, not just one.
- `ExecuteShoutoutAsync` must use the correct channel's OAuth grant token (from ChannelRegistry) for Twitch API calls and the correct channel's shoutout template.

#### 5.2.7 WatchStreakService

**Current state**: Hosted service. Creates a single TwitchLib IRC client connected to one channel at startup, running permanently.

**Changes**:
- **Lifecycle-aware**: Do NOT connect at startup. Instead, listen for EventSub `stream.online`/`stream.offline` events.
- On `stream.online`: Join the channel's IRC to start tracking watch streaks.
- On `stream.offline`: Part from the channel to free resources.
- On platform startup: Query Twitch API for currently live channels and join only those.
- Single IRC connection shared across all live channels (TwitchLib supports multi-channel join).
- `JoinChannel(string channelName)` and `PartChannel(string channelName)` methods exposed for the event handlers to call.
- `HandleWatchStreak` already receives channel name from IRC, so it resolves the broadcasterId naturally.

#### 5.2.8 SpotifyApiService

**Current state**: Singleton. Uses `SpotifyConfig.Service()` static field. Stores SpotifyState in instance field.

**Changes**:
- Becomes a factory pattern: `SpotifyApiServiceFactory` that creates per-channel `SpotifyApiClient` instances.
- Each channel's Spotify OAuth grant tokens (explicitly authorized by the broadcaster via Spotify consent screen) come from `Service` table where `Name = "Spotify" AND BroadcasterId = channelId`.
- SpotifyState moves to `ChannelContext`.

#### 5.2.9 SpotifyWebsocketService -- REMOVED

**Current state**: Hosted service. Connects to Spotify's undocumented `dealer.spotify.com` websocket using a token obtained via an undocumented Discord API endpoint. This is a hack that must be completely removed.

**Replacement**: `SpotifyPollingService` -- a background service that polls the official Spotify Web API for playback state changes:
- Polls `GET /me/player` every 3-5 seconds per active channel (only when stream is live)
- Uses official OAuth tokens obtained via Spotify Authorization Code Flow
- Detects track changes, play/pause state, volume changes
- Publishes `spotify.player.state` events to the widget system
- Lifecycle-aware: starts polling on `stream.online`, stops on `stream.offline`
- `ConcurrentDictionary<string, SpotifyPollState>` tracks per-channel polling state

**Files to remove**:
- `src/NoMercyBot.Services/Spotify/SpotifyWebsocketService.cs`
- `src/NoMercyBot.Services/Discord/DiscordApiService.cs` (the `GetSpotifyToken()` hack)

**Files to create**:
- `src/NoMercyBot.Services/Spotify/SpotifyPollingService.cs`

#### 5.2.10 TokenRefreshService

**Current state**: Background service. Iterates all `Service` rows and `BotAccount` rows, refreshing tokens near expiry.

**Changes**: 
- Already iterates all Service rows, so it naturally handles multiple channels.
- After refreshing a channel's token, update the ChannelContext in the registry.
- No structural changes needed, just add logging of which channel the token belongs to.

#### 5.2.11 PermissionService

**Current state**: Singleton. Loads permission overrides from `Record` table (type = "PermissionOverride"). Static `_overrides` dictionary keyed by userId.

**Changes**:
- Overrides become per-channel: keyed by `"{broadcasterId}:{userId}"`.
- `UserHasMinLevel` accepts `broadcasterId` as a parameter.
- `GrantOverride` and `RevokeOverride` include broadcasterId.

#### 5.2.12 WidgetEventService

**Current state**: Singleton. Publishes events to SignalR hub groups by widget ID.

**Changes**:
- `PublishEventAsync` adds an optional `broadcasterId` parameter to scope event delivery.
- Widget hub groups become `"widget-{broadcasterId}-{widgetId}"` to prevent cross-channel event leakage.
- `SubscribeWidgetToEventsAsync` and `UnsubscribeWidgetFromEventsAsync` scope by channel.

#### 5.2.13 Claude Integration -- REMOVED

The Claude command (`!claude`), `ClaudeSessionBridge`, and `ClaudeIpcService` are **removed from the multi-channel platform**. They are inherently single-machine, single-developer tools (spawning a CLI process, reading from named pipes, committing to a local git repo). They have no place in a hosted multi-channel environment.

**Files to remove**:
- `src/NoMercyBot.CommandsRewards/commands/Claude.cs`
- `src/NoMercyBot.Services/Twitch/ClaudeSessionBridge.cs`
- `src/NoMercyBot.Services/Twitch/ClaudeIpcService.cs`

**References to clean up**:
- `ChatEventHandler.cs` -- Remove the Claude thread reply routing logic
- `ServiceCollectionExtensions.cs` -- Remove ClaudeIpcService registration
- `Help.cs` -- Remove `!claude` from help text

#### 5.2.15 ServiceResolver

**Current state**: Initializes all services from DB at startup. Sets static config fields.

**Changes**:
- `InitializeAllServices()` loads the platform-level service configs (BroadcasterId = null).
- New method `InitializeChannelServices(string broadcasterId)` loads per-channel services and populates the ChannelRegistry.
- Called during onboarding and at startup for all active channels.

#### 5.2.16 TwitchWebsocketHostedService

**Current state**: Singleton hosted service. Opens one EventSub websocket connection. Subscribes to events for the single broadcaster.

**Changes**:
- Must subscribe to events for ALL active channels.
- Twitch EventSub websocket supports up to ~300 subscriptions per connection, and the server can open multiple connections.
- At startup, load all active channels and their enabled event subscriptions. Subscribe to all of them.
- On channel onboard: subscribe to that channel's events.
- On channel deactivate: unsubscribe.
- Event handlers already receive `BroadcasterUserId` in the event payload, so routing is automatic.

---
