## 11. Implementation Phases

### Phase 2: Schema and Foundation

**Goal**: Database schema supports multi-channel; ChannelRegistry exists; no behavioral changes.

**Files to change**:
- `src/NoMercyBot.Database/Models/Channel.cs` -- Add `IsOnboarded`, `BotJoinedAt`, `SubscriptionTier`
- `src/NoMercyBot.Database/Models/ChannelModerator.cs` -- Add `Role`, `GrantedAt`, `GrantedBy`
- `src/NoMercyBot.Database/Models/Command.cs` -- Add `BroadcasterId`, update index
- `src/NoMercyBot.Database/Models/Reward.cs` -- Add `BroadcasterId`
- `src/NoMercyBot.Database/Models/EventSubscription.cs` -- Add `BroadcasterId`, update index
- `src/NoMercyBot.Database/Models/Widget.cs` -- Add `BroadcasterId`
- `src/NoMercyBot.Database/Models/Service.cs` -- Add `BroadcasterId`, update index
- `src/NoMercyBot.Database/Models/Configuration.cs` -- Add `BroadcasterId`, update index
- `src/NoMercyBot.Database/Models/Storage.cs` -- Add `BroadcasterId`, update index
- `src/NoMercyBot.Database/Models/Record.cs` -- Add `BroadcasterId`, add index
- `src/NoMercyBot.Database/Models/UserTtsVoice.cs` -- Add `BroadcasterId`, update index
- `src/NoMercyBot.Database/Models/TtsUsageRecord.cs` -- Add `BroadcasterId`
- `src/NoMercyBot.Database/AppDbContext.cs` -- Update model building for new columns, indexes, relationships
- New file: `src/NoMercyBot.Database/Models/ChannelBotAuthorization.cs`
- New file: `src/NoMercyBot.Services/ChannelRegistry.cs` -- ChannelRegistry + ChannelContext classes
- EF Core migration file

**Acceptance criteria**:
- Database migration runs successfully against existing data
- All existing data has correct BroadcasterId populated
- Existing single-channel behavior is unchanged
- ChannelRegistry class exists but is not yet wired into services
- All tests pass

### Phase 3: Service Refactor

**Goal**: All services are channel-aware. ChannelRegistry is populated and used.

**Files to change**:
- `src/NoMercyBot.Services/Twitch/TwitchChatService.cs` -- Remove static fields; add broadcasterId parameters
- `src/NoMercyBot.Services/Twitch/TwitchApiService.cs` -- Add accessToken parameters to channel-specific methods
- `src/NoMercyBot.Services/Twitch/TwitchConfig.cs` -- Document that static field is platform-only
- `src/NoMercyBot.Services/Twitch/TwitchCommandService.cs` -- Move Commands dict into ChannelContext; add broadcasterId to methods
- `src/NoMercyBot.Services/Twitch/TwitchEventSubService.cs` -- Add broadcasterId to all methods
- `src/NoMercyBot.Services/Twitch/ShoutoutQueueService.cs` -- Use ChannelRegistry for token lookup; multi-channel startup check
- `src/NoMercyBot.Services/Twitch/ClaudeSessionBridge.cs` -- REMOVE (not part of multi-channel platform)
- `src/NoMercyBot.Services/Twitch/ClaudeIpcService.cs` -- REMOVE (not part of multi-channel platform)
- `src/NoMercyBot.CommandsRewards/commands/Claude.cs` -- REMOVE
- `src/NoMercyBot.Services/Spotify/SpotifyApiService.cs` -- Accept per-channel tokens
- `src/NoMercyBot.Services/Spotify/SpotifyConfig.cs` -- Remove static field usage
- `src/NoMercyBot.Services/Discord/DiscordConfig.cs` -- Remove static field usage
- `src/NoMercyBot.Services/Obs/ObsConfig.cs` -- Remove static field usage
- `src/NoMercyBot.Services/Other/PermissionService.cs` -- Per-channel overrides
- `src/NoMercyBot.Services/Other/TTSService.cs` -- Accept broadcasterId for usage tracking
- `src/NoMercyBot.Services/Widgets/WidgetEventService.cs` -- Channel-scoped event delivery
- `src/NoMercyBot.Services/ServiceResolver.cs` -- Add InitializeChannelServices; populate ChannelRegistry
- `src/NoMercyBot.Services/Twitch/Scripting/CommandScriptLoader.cs` -- Register scripts in ChannelRegistry
- `src/NoMercyBot.Services/Twitch/Scripting/RewardScriptLoader.cs` -- Register rewards in ChannelRegistry
- All event handlers in `src/NoMercyBot.Services/Twitch/EventHandlers/` -- Use ChannelRegistry

**Acceptance criteria**:
- Single-channel mode still works (existing broadcaster's ChannelContext is created at startup)
- All services use ChannelRegistry instead of static state
- Chat messages, commands, rewards, shoutouts all work correctly for the existing channel
- No regressions in TTS, Spotify, or widget functionality

### Phase 4: Auth and API Routes

**Goal**: Multi-channel API routes exist; authentication supports channel-scoped authorization.

**Files to change**:
- `src/NoMercyBot.Server/AppConfig/ServiceConfiguration.cs` -- Add token caching (IMemoryCache)
- New file: `src/NoMercyBot.Api/Filters/ChannelAuthorizationFilter.cs`
- `src/NoMercyBot.Api/Controllers/AuthController.cs` -- Onboarding endpoint
- `src/NoMercyBot.Api/Controllers/CommandController.cs` -- Move to channel-scoped routes
- `src/NoMercyBot.Api/Controllers/RewardController.cs` -- Move to channel-scoped routes
- `src/NoMercyBot.Api/Controllers/WidgetController.cs` -- Move to channel-scoped routes
- `src/NoMercyBot.Api/Controllers/EventSubscriptionController.cs` -- Move to channel-scoped routes
- `src/NoMercyBot.Api/Controllers/ServiceController.cs` -- Move to channel-scoped routes
- `src/NoMercyBot.Api/Controllers/SpotifyController.cs` -- Move to channel-scoped routes
- `src/NoMercyBot.Api/Controllers/ConfigController.cs` -- Move to channel-scoped routes
- `src/NoMercyBot.Api/Controllers/BotAuthController.cs` -- Channel-scoped bot auth
- `src/NoMercyBot.Api/Controllers/TTSVoiceController.cs` -- Channel-scoped TTS
- New file: `src/NoMercyBot.Api/Controllers/ChannelController.cs` -- List channels, onboard
- New file: `src/NoMercyBot.Api/Controllers/ModeratorController.cs` -- Manage channel moderators
- New file: `src/NoMercyBot.Api/Middleware/BackwardCompatibilityMiddleware.cs`

**Acceptance criteria**:
- New `/api/channels/{channelId}/...` routes work
- Old routes still work via backward compatibility middleware
- Authorization checks enforce role-based access per channel
- Token validation is cached (verified by reduced calls to Twitch validate endpoint)
- A second user cannot access a channel they are not a moderator/editor/owner of

### Phase 5: Multi-Channel EventSub and Background Services

**Goal**: Multiple channels can be onboarded and serviced simultaneously.

**Files to change**:
- `src/NoMercyBot.Services/Twitch/TwitchWebsocketHostedService.cs` -- Multi-channel subscriptions
- `src/NoMercyBot.Services/Twitch/WatchStreakService.cs` -- Multi-channel IRC
- `src/NoMercyBot.Services/Spotify/SpotifyWebsocketService.cs` -- REMOVE (replace with SpotifyPollingService)
- `src/NoMercyBot.Services/Discord/DiscordApiService.cs` -- REMOVE GetSpotifyToken() hack
- New: `src/NoMercyBot.Services/Spotify/SpotifyPollingService.cs` -- Official API polling
- `src/NoMercyBot.Services/Emotes/BttvService.cs` -- Multi-channel emote fetch
- `src/NoMercyBot.Services/Emotes/FrankerFacezService.cs` -- Multi-channel emote fetch
- `src/NoMercyBot.Services/Emotes/SevenTvService.cs` -- Multi-channel emote fetch
- `src/NoMercyBot.Services/Other/TokenRefreshService.cs` -- ChannelRegistry integration
- New file: `src/NoMercyBot.Services/Onboarding/ChannelOnboardingService.cs` -- Orchestrates the full onboarding flow

**Acceptance criteria**:
- A second broadcaster can sign up and have a fully functional channel
- EventSub events for Channel A do not trigger behaviors in Channel B
- Spotify state for Channel A is independent of Channel B
- Both channels receive chat messages, commands work independently
- Watch streaks are tracked per-channel
- Emotes are fetched per-channel

### Phase 6: Operational Readiness

**Goal**: Production-ready multi-channel deployment with monitoring, graceful lifecycle, and documentation.

**Files to change**:
- `src/NoMercyBot.Api/Controllers/HomeController.cs` -- Add `/status/channels` endpoint showing active channel count, health
- New file: `src/NoMercyBot.Services/Other/ChannelHealthService.cs` -- Periodic health check of all channels (token validity, EventSub status)
- `src/NoMercyBot.Services/Other/GracefulShutdownService.cs` -- Gracefully close all per-channel connections
- Update `src/NoMercyBot.Services/ServiceCollectionExtensions.cs` -- Wire up new services

**Acceptance criteria**:
- Health endpoint reports status of all channels
- Graceful shutdown closes all websocket connections and IRC connections cleanly
- Token refresh failures for one channel do not affect other channels
- Error in one channel's command execution does not crash the server
- Structured logging includes `BroadcasterId` on all log entries for multi-channel operations
- Documentation of the onboarding flow and API routes exists

---
