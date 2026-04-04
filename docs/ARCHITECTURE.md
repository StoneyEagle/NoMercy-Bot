# NoMercyBot Multi-Channel Platform Architecture Specification

## Phases 2 through 6

---

## 1. Overview

### 1.1 Vision

NoMercyBot is evolving from a single-broadcaster Twitch bot into a multi-channel platform. One server instance serves many broadcasters simultaneously. Each broadcaster signs up via Twitch OAuth, connects their own Spotify/Discord/OBS integrations, configures commands, rewards, widgets, and event subscriptions independently.

### 1.2 Core Architecture

- **Deployment model**: One server process, many channels.
- **Database**: SQLite today (will need migration path to PostgreSQL for production multi-channel, but out of scope for Phases 2-6; all schema work is SQLite-compatible).
- **Service model**: Services that currently hold single-broadcaster state in static fields or singletons become channel-aware via a ChannelRegistry pattern.

### 1.3 Naming Conventions

| Context | Term | Example |
|---------|------|---------|
| User-facing (dashboard, UI, docs) | **Channel** | "Your Channel", "Channel Settings" |
| Code / Database columns | **BroadcasterId** | `string BroadcasterId`, FK column names |
| API routes | **channels** | `/api/channels/{channelId}/commands` |
| Twitch API alignment | **Broadcaster** | Matches Twitch Helix terminology |

The word "Tenant" is never used anywhere.

---

## 2. Data Model

### 2.1 Existing Tables -- Current Schema and Required Changes

#### 2.1.1 Users

**Current schema** (file: `src/NoMercyBot.Database/Models/User.cs`):
- `Id` (string, PK, max 50) -- Twitch user ID
- `Username` (string, max 255)
- `DisplayName` (string, max 255)
- `NickName` (string, max 255)
- `Timezone` (string?, max 50)
- `Description` (string, max 255)
- `ProfileImageUrl` (string, max 2048)
- `OfflineImageUrl` (string, max 2048)
- `Color` (string?, max 7)
- `BroadcasterType` (string, max 255)
- `Enabled` (bool)
- `Pronoun` (Pronoun?, stored as JSON in column `PronounData`)
- `PronounManualOverride` (bool)
- `CreatedAt`, `UpdatedAt` (DateTime, from Timestamps base)
- Nav: `Channel` (virtual Channel)

**Changes**: None. Users are global entities that exist across channels. A Twitch user is the same person regardless of which channel they chat in.

#### 2.1.2 Channels

**Current schema** (file: `src/NoMercyBot.Database/Models/Channel.cs`):
- `Id` (string, PK, max 50) -- same as User.Id (broadcaster's Twitch ID)
- `Name` (string, max 25)
- `Enabled` (bool)
- `ShoutoutTemplate` (string, max 450)
- `LastShoutout` (DateTime?)
- `ShoutoutInterval` (int, default 10)
- `UsernamePronunciation` (string?, max 100)
- `CreatedAt`, `UpdatedAt`
- Nav: `User` (FK to User via Id), `Info` (FK to ChannelInfo via Id), `UsersInChat`, `Events`, `ChannelModerators`

**Changes**:
- Add `IsOnboarded` (bool, default false) -- Whether the channel has completed the onboarding flow
- Add `BotJoinedAt` (DateTime?) -- When the bot first joined this channel
- Add `SubscriptionTier` (string, max 20, default "free") -- For future rate limiting / feature gating
- Add index on `Enabled` for fast lookup of active channels

**Why**: The Channel table already exists and is keyed by broadcaster Twitch ID. Adding `IsOnboarded` tracks the onboarding workflow. `BotJoinedAt` is useful for operational monitoring.

#### 2.1.3 ChannelInfo

**Current schema** (file: `src/NoMercyBot.Database/Models/ChannelInfo.cs`):
- `Id` (string, PK, max 50) -- broadcaster_id
- `IsLive` (bool)
- `Language` (string, max 50)
- `GameId` (string, max 50)
- `GameName` (string, max 255)
- `Title` (string, max 255)
- `Delay` (int)
- `Tags` (List\<string\>, JSON)
- `ContentLabels` (List\<string\>, JSON)
- `IsBrandedContent` (bool)
- `CreatedAt`, `UpdatedAt`

**Changes**: None. Already keyed by broadcaster ID.

#### 2.1.4 ChannelEvent

**Current schema** (file: `src/NoMercyBot.Database/Models/ChannelEvent.cs`):
- `Id` (string, PK, max 50)
- `Type` (string, max 50)
- `Data` (object?, JSON)
- `ChannelId` (string?, FK to Channel)
- `UserId` (string?, FK to User)
- `CreatedAt`, `UpdatedAt`

**Changes**: None. Already has `ChannelId` FK.

#### 2.1.5 ChannelModerator

**Current schema** (file: `src/NoMercyBot.Database/Models/ChannelModerator.cs`):
- Composite PK: (`ChannelId`, `UserId`)
- `ChannelId` (string, FK to Channel)
- `UserId` (string, FK to User)
- `CreatedAt`, `UpdatedAt`
- Indexes on `UserId` and `ChannelId`

**Changes**: 
- Add `Role` (string, max 20, default "moderator") -- Allows distinguishing "owner", "moderator", "editor" roles
- Add `GrantedAt` (DateTime, default CURRENT_TIMESTAMP)
- Add `GrantedBy` (string?, FK to User) -- Who granted this role

**Why**: The multi-channel system needs an explicit role system. The broadcaster is always implicitly "owner" of their own channel, but moderators and editors need explicit grants.

#### 2.1.6 ChatMessage

**Current schema** (file: `src/NoMercyBot.Database/Models/ChatMessage/ChatMessage.cs`):
- `Id` (string, PK, max 255)
- `IsCommand`, `IsCheer`, `IsHighlighted`, `IsGigantified`, `IsDecorated` (bool)
- `BitsAmount` (int?)
- `MessageType`, `DecorationStyle` (string)
- `ColorHex` (string)
- `Badges` (List\<ChatBadge\>, JSON)
- `UserId` (string, max 50, FK to User)
- `Username`, `DisplayName`, `UserType` (string)
- `BroadcasterId` (string?) -- JSON property name is `channel_id`
- `Message` (string)
- `Fragments` (List\<ChatMessageFragment\>, JSON)
- `MessageNode` (MessageNode?, NotMapped at DB level)
- `TmiSentTs` (string)
- `SuccessfulReply` (string?)
- `ReplyToMessageId` (string?)
- `DeletedAt` (DateTime?)
- `StreamId` (string?, FK to Stream)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add index on `BroadcasterId` -- Critical for per-channel message queries
- Add composite index on (`BroadcasterId`, `CreatedAt`) -- For time-range queries per channel
- Make `BroadcasterId` non-nullable (string, not string?)

**Why**: Currently `BroadcasterId` is nullable and unindexed. For multi-channel, every message must be associated with a channel, and queries will always filter by channel.

#### 2.1.7 ChatPresence

**Current schema** (file: `src/NoMercyBot.Database/Models/ChatPresence.cs`):
- `Id` (int, PK, identity)
- `IsPresent` (bool)
- `ChannelId` (string, max 50, FK to User)
- `UserId` (string, max 50, FK to User)
- `CreatedAt`, `UpdatedAt`
- Unique index on (`ChannelId`, `UserId`)

**Changes**: None. Already channel-scoped.

#### 2.1.8 Service

**Current schema** (file: `src/NoMercyBot.Database/Models/Service.cs`):
- `Id` (Ulid, PK)
- `Name` (string, unique index)
- `Enabled` (bool)
- `ClientId`, `ClientSecret` (string?, encrypted)
- `UserName`, `UserId` (string)
- `Scopes` (string[])
- `AccessToken`, `RefreshToken` (string?, encrypted, JsonIgnore)
- `TokenExpiry` (DateTime?)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add `BroadcasterId` (string?, max 50, FK to Channel) -- null means global/platform-level service (the platform's own Twitch app)
- Drop unique index on `Name`, replace with unique index on (`Name`, `BroadcasterId`)

**Why**: Currently there is one Service row per provider (Twitch, Spotify, Discord, OBS). In multi-channel, each broadcaster has their own Spotify, Discord, and OBS tokens. The platform Twitch app credentials remain global (BroadcasterId = null), while per-channel services have the broadcaster's ID set.

#### 2.1.9 Command

**Current schema** (file: `src/NoMercyBot.Database/Models/Command.cs`):
- `Id` (int, PK, identity)
- `Name` (string, max 100, unique index)
- `Permission` (string, default "everyone")
- `Type` (string, default "command")
- `Response` (string)
- `IsEnabled` (bool, default true)
- `Description` (string?)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add `BroadcasterId` (string, max 50, FK to Channel, NOT NULL)
- Drop unique index on `Name`, replace with unique index on (`Name`, `BroadcasterId`)

**Why**: Commands are per-channel. Two different channels can have a `!hello` command with different responses.

#### 2.1.10 Reward

**Current schema** (file: `src/NoMercyBot.Database/Models/Reward.cs`):
- `Id` (Guid, PK)
- `Title` (string)
- `Response` (string)
- `Permission` (string, default "everyone")
- `IsEnabled` (bool, default true)
- `Description` (string?)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add `BroadcasterId` (string, max 50, FK to Channel, NOT NULL)

**Why**: Channel point rewards are per-channel on Twitch. Each broadcaster creates rewards on their own channel.

#### 2.1.11 EventSubscription

**Current schema** (file: `src/NoMercyBot.Database/Models/EventSubscription.cs`):
- `Id` (string, PK, Ulid)
- `Provider` (string)
- `EventType` (string)
- `Description` (string)
- `Enabled` (bool, default true)
- `Version` (string?)
- `SubscriptionId` (string?)
- `SessionId` (string?)
- `ExpiresAt` (DateTime?)
- `Metadata` (Dictionary\<string,string\>, JSON)
- `Condition` (string[], JSON)
- `CreatedAt`, `UpdatedAt`
- Unique index on (`Provider`, `EventType`, `Condition`)

**Changes**:
- Add `BroadcasterId` (string, max 50, FK to Channel, NOT NULL)
- Update unique index to (`Provider`, `EventType`, `Condition`, `BroadcasterId`)

**Why**: Each channel has its own set of EventSub subscriptions with Twitch.

#### 2.1.12 Stream

**Current schema** (file: `src/NoMercyBot.Database/Models/Stream.cs`):
- `Id` (string, PK, max 50)
- `Language`, `GameId`, `GameName`, `Title` (string)
- `Delay` (int)
- `Tags`, `ContentLabels` (List\<string\>)
- `IsBrandedContent` (bool)
- `ChannelId` (string, FK to Channel)
- `CreatedAt`, `UpdatedAt`

**Changes**: None. Already has `ChannelId` FK.

#### 2.1.13 Widget

**Current schema** (file: `src/NoMercyBot.Database/Models/Widget.cs`):
- `Id` (Ulid, PK)
- `Name`, `Description`, `Version`, `Framework` (string)
- `IsEnabled` (bool)
- `EventSubscriptionsJson` (TEXT)
- `SettingsJson` (TEXT)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add `BroadcasterId` (string, max 50, FK to Channel, NOT NULL)

**Why**: Widgets are per-channel. Each broadcaster has their own OBS browser sources.

#### 2.1.14 Configuration

**Current schema** (file: `src/NoMercyBot.Database/Models/Configuration.cs`):
- `Id` (int, PK, identity)
- `Key` (string, unique index)
- `Value` (string)
- `SecureValue` (string, encrypted)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add `BroadcasterId` (string?, max 50, FK to Channel) -- null means global/platform config
- Drop unique index on `Key`, replace with unique index on (`Key`, `BroadcasterId`)

**Why**: Some configs are global (Azure TTS key, platform settings). Others are per-channel (TTS character limit for their channel, billing settings).

#### 2.1.15 Storage

**Current schema** (file: `src/NoMercyBot.Database/Models/Storage.cs`):
- `Id` (int, PK, identity)
- `Key` (string, unique index)
- `Value` (string)
- `SecureValue` (string, encrypted)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add `BroadcasterId` (string?, max 50, FK to Channel) -- null means global
- Drop unique index on `Key`, replace with unique index on (`Key`, `BroadcasterId`)

**Why**: Storage is used for per-channel state (e.g., Claude session bridge data is per-channel).

#### 2.1.16 Record

**Current schema** (file: `src/NoMercyBot.Database/Models/Record.cs`):
- `Id` (int, PK, identity)
- `RecordType` (string)
- `Data` (string)
- `UserId` (string, max 50, FK to User)
- `CreatedAt`, `UpdatedAt`

**Changes**:
- Add `BroadcasterId` (string, max 50, FK to Channel, NOT NULL)
- Add index on (`BroadcasterId`, `RecordType`)

**Why**: Records track command usage, watch streaks, permission overrides -- all per-channel.

#### 2.1.17 Shoutout

**Current schema** (file: `src/NoMercyBot.Database/Models/Shoutout.cs`):
- `Id` (string, PK, identity)
- `Enabled` (bool)
- `MessageTemplate` (string)
- `LastShoutout` (DateTime?)
- `ChannelId` (string, FK to Channel)
- `ShoutedUserId` (string, FK to User)
- `CreatedAt`, `UpdatedAt`
- Unique index on (`ChannelId`, `ShoutedUserId`)

**Changes**: None. Already has `ChannelId` FK.

#### 2.1.18 BotAccount

**Current schema** (file: `src/NoMercyBot.Database/Models/BotAccount.cs`):
- `Id` (int, PK, identity)
- `Username` (string, unique index)
- `ClientId`, `ClientSecret` (string, encrypted)
- `AccessToken`, `RefreshToken` (string, encrypted)
- `TokenExpiry` (DateTime?)
- `AppAccessToken` (string, encrypted)
- `AppTokenExpiry` (DateTime?)

**Changes**: None for now. The bot account is a platform-level singleton -- one bot identity sends messages in all channels. In a future phase, we could support per-channel bot accounts, but for MVP, the platform bot account is used everywhere.

#### 2.1.19 TTS Tables (TtsVoice, UserTtsVoice, TtsProvider, TtsUsageRecord, TtsCacheEntry)

**TtsVoice**: No changes. Voices are global.

**UserTtsVoice**: 
- Add `BroadcasterId` (string, max 50, FK to Channel, NOT NULL)
- Change unique index from `UserId` to (`UserId`, `BroadcasterId`)

**Why**: A user can pick different voices in different channels.

**TtsProvider**: No changes. Providers are platform-level.

**TtsUsageRecord**: 
- Add `BroadcasterId` (string, max 50, FK to Channel, NOT NULL)

**Why**: Usage tracking needs to be per-channel for billing.

**TtsCacheEntry**: No changes. Cache is global (same text/voice combo produces the same audio regardless of channel).

#### 2.1.20 Pronoun

**Current schema**: Id (int, PK), Name, Subject, Object, Singular. No changes. Pronouns are global.

### 2.2 New Tables

#### 2.2.1 ChannelService (junction for per-channel OAuth connections)

This is handled by the existing `Service` table with the added `BroadcasterId` column. No new table needed.

#### 2.2.2 ChannelBotAuthorization

```
Table: ChannelBotAuthorizations
- Id              int, PK, identity
- BroadcasterId   string(50), NOT NULL, FK to Channel, unique index
- AuthorizedAt    DateTime, NOT NULL
- AuthorizedBy    string(50), FK to User  -- who ran the /channel-auth flow
- IsActive        bool, default true
- CreatedAt       DateTime
- UpdatedAt       DateTime
```

**Why**: Tracks which channels have completed the `channel:bot` authorization flow, allowing the bot to chat in their channel with the bot badge. Currently this is done ad-hoc; multi-channel needs to track it explicitly.

---

## 3. Authentication and Authorization

### 3.1 Broadcaster Sign-up Flow

1. Broadcaster visits the NoMercyBot landing page and clicks "Add NoMercyBot to your channel."
2. Redirected to Twitch OAuth with scopes: `channel:bot`, `user:read:chat`, `user:write:chat`, `moderator:read:chatters`, `moderator:read:followers`, `channel:read:redemptions`, `channel:manage:redemptions`, `channel:read:subscriptions`, `bits:read`, `channel:read:hype_train`, `channel:read:polls`, `channel:read:predictions`, `channel:moderate`, `moderator:manage:shoutouts`, `moderator:read:shoutouts`, `moderator:manage:announcements`.
3. On callback, the system creates/updates: `User` record, `Channel` record (with `IsOnboarded = false`), `ChannelInfo` record, `Service` record (BroadcasterId = their ID, Name = "Twitch"), and a `ChannelModerator` record (UserId = BroadcasterId, Role = "owner").
4. Broadcaster token is stored in the `Service` table (encrypted) tied to their BroadcasterId.
5. Onboarding wizard runs (see Section 6).

### 3.2 Role Model

| Role | Scope | Permissions |
|------|-------|-------------|
| **Platform Admin** | Global | All operations on all channels. Identified via a platform-level config flag. |
| **Owner** | Per-channel | Full control of their channel: commands, rewards, widgets, events, integrations, moderator management. |
| **Editor** | Per-channel | Manage commands, rewards, widgets. Cannot manage integrations or moderators. |
| **Moderator** | Per-channel | View dashboard, manage commands (limited). Read-only for integrations. |

Stored in `ChannelModerator.Role` column. The platform admin list is stored in a `Configuration` row with `Key = "platform_admins"`, `Value = "comma,separated,twitch,ids"`, `BroadcasterId = null`.

### 3.3 Token Validation Flow

The current auth scheme (file: `src/NoMercyBot.Server/AppConfig/ServiceConfiguration.cs`, lines 160-196) validates a Bearer token against Twitch's `/oauth2/validate` endpoint. This returns the `UserId` and `Login`.

**Changes for multi-channel**:
1. The validation response gives us the authenticated user's Twitch ID.
2. For routes under `/api/channels/{channelId}/...`, we resolve the user's role for that specific channel by querying `ChannelModerator` where `ChannelId = channelId AND UserId = authenticatedUserId`.
3. If the user IS the channel owner (channelId == userId), they implicitly have "owner" role.
4. A middleware or action filter `ChannelAuthorizationFilter` is added that:
   - Extracts `channelId` from the route.
   - Checks the user's role for that channel.
   - Sets `HttpContext.Items["ChannelRole"]` for downstream use.
   - Returns 403 if the user has no role for that channel.

### 3.4 Token Caching

Twitch `/oauth2/validate` should not be called on every request. Introduce an in-memory cache:
- Key: access token hash (SHA256)
- Value: validated user info + expiry
- TTL: 5 minutes
- Implementation: `IMemoryCache` with sliding expiration
- On token refresh, invalidate the cache entry

### 3.5 Session Management

No change to the stateless Bearer token model. Each request is independently authenticated. The dashboard frontend stores the Twitch access token and includes it in API calls.

---

## 4. API Design

### 4.1 New Route Structure

All channel-scoped endpoints move under `/api/channels/{channelId}/...`. The `channelId` is the broadcaster's Twitch user ID.

### 4.2 Endpoint Groups

#### Global Endpoints (no channel scope)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/status` | None | Health check |
| GET | `/api/oauth/{provider}/login` | None | OAuth redirect |
| GET | `/api/oauth/{provider}/callback` | None | OAuth callback |
| POST | `/api/oauth/{provider}/validate` | Bearer | Validate token |
| GET | `/api/channels` | Bearer | List channels the user has access to |
| POST | `/api/channels/onboard` | Bearer | Start onboarding for the authenticated user's channel |

#### Channel-Scoped Endpoints

| Method | Route | Min Role | Description |
|--------|-------|----------|-------------|
| **Commands** | | | |
| GET | `/api/channels/{channelId}/commands` | Moderator | List commands |
| GET | `/api/channels/{channelId}/commands/{name}` | Moderator | Get command |
| POST | `/api/channels/{channelId}/commands` | Editor | Create command |
| PUT | `/api/channels/{channelId}/commands/{name}` | Editor | Update command |
| DELETE | `/api/channels/{channelId}/commands/{name}` | Editor | Delete command |
| **Rewards** | | | |
| GET | `/api/channels/{channelId}/rewards` | Moderator | List Twitch rewards |
| POST | `/api/channels/{channelId}/rewards` | Owner | Create reward |
| PATCH | `/api/channels/{channelId}/rewards/{rewardId}/redemptions/{redemptionId}` | Editor | Update redemption |
| GET | `/api/channels/{channelId}/rewards/bot-rewards` | Moderator | List bot rewards |
| POST | `/api/channels/{channelId}/rewards/bot-rewards` | Editor | Add/update bot reward |
| DELETE | `/api/channels/{channelId}/rewards/bot-rewards/{identifier}` | Editor | Remove bot reward |
| **Widgets** | | | |
| GET | `/api/channels/{channelId}/widgets` | Moderator | List widgets |
| GET | `/api/channels/{channelId}/widgets/{id}` | Moderator | Get widget |
| POST | `/api/channels/{channelId}/widgets` | Editor | Create widget |
| PUT | `/api/channels/{channelId}/widgets/{id}` | Editor | Update widget |
| DELETE | `/api/channels/{channelId}/widgets/{id}` | Editor | Delete widget |
| **Events** | | | |
| GET | `/api/channels/{channelId}/events/{provider}` | Moderator | List event subscriptions |
| POST | `/api/channels/{channelId}/events/{provider}` | Owner | Create subscription |
| PUT | `/api/channels/{channelId}/events/{provider}/{id}` | Owner | Update subscription |
| DELETE | `/api/channels/{channelId}/events/{provider}/{id}` | Owner | Delete subscription |
| **Integrations** | | | |
| GET | `/api/channels/{channelId}/settings/providers` | Moderator | List connected services |
| PUT | `/api/channels/{channelId}/settings/providers/{provider}` | Owner | Update service config |
| **Spotify** | | | |
| GET | `/api/channels/{channelId}/spotify/currently-playing` | Moderator | Current track |
| POST | `/api/channels/{channelId}/spotify/set-volume` | Owner | Set volume |
| POST | `/api/channels/{channelId}/spotify/next` | Editor | Skip track |
| POST | `/api/channels/{channelId}/spotify/pause` | Editor | Pause |
| POST | `/api/channels/{channelId}/spotify/resume` | Editor | Resume |
| **Config** | | | |
| GET | `/api/channels/{channelId}/config` | Moderator | Get channel config |
| PUT | `/api/channels/{channelId}/config` | Owner | Update channel config |
| **TTS** | | | |
| GET | `/api/channels/{channelId}/tts/voices` | Moderator | List TTS voices |
| POST | `/api/channels/{channelId}/tts/speak` | Editor | Trigger TTS |
| **Bot** | | | |
| GET | `/api/channels/{channelId}/bot/status` | Moderator | Bot auth status for channel |
| POST | `/api/channels/{channelId}/bot/send` | Editor | Send message in channel |
| **Moderators** | | | |
| GET | `/api/channels/{channelId}/moderators` | Owner | List channel moderators |
| POST | `/api/channels/{channelId}/moderators` | Owner | Invite moderator |
| DELETE | `/api/channels/{channelId}/moderators/{userId}` | Owner | Remove moderator |

### 4.3 Backward Compatibility

During the transition, keep the old routes functional with a compatibility middleware that:
1. Detects requests to old routes (e.g., `/api/commands`).
2. Resolves the "default channel" from the authenticated user's own broadcaster ID.
3. Internally redirects to `/api/channels/{userId}/commands`.
4. Returns a `Deprecation` header with the sunset date.

This ensures the existing frontend continues working during the migration.

---

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
- `SendMessageAsUser` becomes only callable for the channel owner -- it uses that channel's own access token from the ChannelRegistry.
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
- For platform-level operations (fetching user info, channel info), continue using the platform Twitch app credentials.
- For channel-specific operations (sending shoutouts, managing rewards, creating EventSub subscriptions), accept the broadcaster's access token as a parameter.
- Methods like `SendShoutoutAsync`, `CreateCustomReward`, `UpdateRedemptionStatus`, `GetCustomRewards`, `UpdateCustomReward` should accept an `accessToken` parameter with fallback to `TwitchConfig.Service().AccessToken`.
- `RaidAsync` -- only the channel owner can raid; must use their token.

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
- `ExecuteShoutoutAsync` must use the correct channel's access token for Twitch API calls and the correct channel's shoutout template.

#### 5.2.7 WatchStreakService

**Current state**: Hosted service. Creates a single TwitchLib IRC client connected to one channel.

**Changes**:
- Connect to ALL active channels. TwitchLib IRC client supports joining multiple channels.
- `ConnectIrcClient` loads all active channels from DB and joins each.
- `HandleWatchStreak` already receives channel name from IRC, so it can resolve the broadcasterId.
- When a new channel is onboarded, join it dynamically.
- When a channel is deactivated, part from it.

#### 5.2.8 SpotifyApiService

**Current state**: Singleton. Uses `SpotifyConfig.Service()` static field. Stores SpotifyState in instance field.

**Changes**:
- Becomes a factory pattern: `SpotifyApiServiceFactory` that creates per-channel `SpotifyApiClient` instances.
- Each channel's Spotify tokens come from `Service` table where `Name = "Spotify" AND BroadcasterId = channelId`.
- SpotifyState moves to `ChannelContext`.

#### 5.2.9 SpotifyWebsocketService

**Current state**: Hosted service. Connects one Spotify websocket for the single broadcaster.

**Changes**:
- Manages one websocket connection PER channel that has Spotify connected.
- Uses a `ConcurrentDictionary<string, SpotifyChannelConnection>` to track connections.
- On channel onboard/Spotify connect: create websocket.
- On channel deactivate/Spotify disconnect: close websocket.

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

#### 5.2.13 ClaudeSessionBridge

**Current state**: Static class. Stores one session per bot instance via static fields backed by `Storage` table.

**Changes**:
- Remove static fields. Store session data in `Storage` with `BroadcasterId` set.
- Becomes a non-static service registered as singleton with a `ConcurrentDictionary<string, ClaudeSession>` keyed by broadcasterId.

#### 5.2.14 ClaudeIpcService

**Current state**: Background service. Reads from named pipe `"nomercy-bot-claude-ipc"`. Sends replies to the channel stored in `ClaudeSessionBridge.BroadcasterId`.

**Changes**:
- Named pipe protocol gains a channel prefix: messages are `"{broadcasterId}|{message}"`.
- Routes replies to the correct channel based on the broadcasterId prefix.

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

## 6. Channel Onboarding Flow

Step-by-step when a new broadcaster signs up:

1. **Twitch OAuth callback** fires. System creates/updates `User`, `Channel`, `ChannelInfo` records. Stores Twitch tokens in `Service(Name="Twitch", BroadcasterId=userId)`.

2. **ChannelModerator record created** with `(ChannelId=userId, UserId=userId, Role="owner")`.

3. **Channel.IsOnboarded = false**. Dashboard shows the onboarding wizard.

4. **Bot authorization**: The system generates a device code for `channel:bot` scope. The broadcaster authorizes, allowing the bot to chat in their channel with the bot badge. A `ChannelBotAuthorization` record is created.

5. **Default EventSub subscriptions created**: Based on a predefined list of essential events (channel.chat.message, channel.follow, stream.online, stream.offline, channel.subscribe, channel.raid, channel.channel_points_custom_reward_redemption.add, etc.). These are registered with Twitch and stored in `EventSubscription` table.

6. **Bot joins channel**: WatchStreakService IRC client joins the new channel. ChannelRegistry creates a new ChannelContext.

7. **Default commands loaded**: Platform script commands (from `src/NoMercyBot.CommandsRewards/commands/`) are registered in the new channel's command registry. No database rows needed -- these are loaded from disk.

8. **Channel.IsOnboarded = true**. Dashboard shows the full management interface.

9. **Optional integrations**: Broadcaster can later connect Spotify, Discord, and OBS through their Channel Settings.

---

## 7. Channel Management

### 7.1 Dashboard Structure

After login, the dashboard shows "Your Channel" as the primary view if the user is a channel owner. A dropdown allows switching between channels the user has access to (as owner, editor, or moderator).

### 7.2 Connecting Integrations

- **Spotify**: Owner clicks "Connect Spotify" which initiates Spotify OAuth. Callback stores tokens in `Service(Name="Spotify", BroadcasterId=channelId)`. The SpotifyWebsocketService creates a connection for this channel.
- **Discord**: Same pattern with Discord OAuth.
- **OBS**: Owner enters OBS WebSocket host and password. Stored in `Service(Name="OBS", BroadcasterId=channelId)`.

### 7.3 Managing Commands

- Dashboard shows commands for the selected channel.
- Platform commands (from script files) are shown as read-only with a "Platform" badge.
- Custom commands (from DB) are editable.
- Changes go to `/api/channels/{channelId}/commands` and only affect that channel's registry.

### 7.4 Managing Rewards

- Similar to commands. Platform reward scripts are read-only.
- Custom rewards are per-channel and managed via the Twitch API using the channel owner's access token.

### 7.5 Inviting Moderators

- Owner goes to Channel Settings > Team.
- Enters a Twitch username.
- System looks up the user (via TwitchApiService), creates a `ChannelModerator(ChannelId, UserId, Role="moderator")` record.
- The moderator can now log in and see the channel in their channel switcher.

---

## 8. Background Services

### 8.1 TokenRefreshService

**Current**: Checks all Service and BotAccount rows every 1 minute, refreshes tokens expiring within 5 minutes.

**Multi-channel change**: No structural change. The service already iterates all rows. With multi-channel, there are simply more Service rows (one per channel per provider). After refreshing, the service should update the ChannelRegistry's cached token.

### 8.2 SpotifyWebsocketService

**Current**: One websocket to Spotify dealer.

**Multi-channel change**: Manages a dictionary of websocket connections. A `SpotifyConnectionManager` class holds `ConcurrentDictionary<string, SpotifyChannelConnection>`. Each connection has its own receive loop and ping timer. Connections are created when a channel connects Spotify and destroyed when disconnected.

Connection limit consideration: Spotify may rate-limit multiple websocket connections from the same IP. Fallback to polling if the websocket fails.

### 8.3 ShoutoutQueueService

**Current**: Already uses per-channel dictionaries for queues and cooldowns.

**Multi-channel change**: The processing loop already iterates all channel IDs. On startup, `CheckIfStreamIsLiveAsync` must iterate all active channels. Access tokens for API calls come from each channel's Service record.

### 8.4 WatchStreakService

**Current**: One IRC connection to one channel.

**Multi-channel change**: The TwitchLib IRC client supports joining multiple channels. On startup, join all active channels. Expose `JoinChannel(string channelName)` and `PartChannel(string channelName)` methods for dynamic add/remove.

### 8.5 TwitchWebsocketHostedService

**Current**: One EventSub websocket session subscribing to events for one broadcaster.

**Multi-channel change**: 
- At startup, load all active channels and their enabled EventSubscriptions.
- Twitch EventSub websocket allows subscribing to events for different broadcasters on the same connection (up to the subscription limit).
- Create subscriptions using the platform app's access token (for webhook-based) or the appropriate user token (for websocket-based, which requires the user's token).
- Note: Twitch EventSub websocket requires the token to have the appropriate scopes for each subscription. For multi-channel, the platform uses a single websocket connection, and each channel's own token is used to create the subscription.

### 8.6 Emote Services (BTTV, FrankerFaceZ, SevenTV)

**Current**: Singleton hosted services that fetch emotes for one channel.

**Multi-channel change**: Fetch emotes for all active channels. Store per-channel emote sets in a dictionary. Refresh periodically.

### 8.7 ClaudeIpcService

**Current**: Single named pipe server.

**Multi-channel change**: The pipe protocol adds a broadcasterId prefix to messages so responses route to the correct channel.

---

## 9. Roslyn Script System

### 9.1 Platform Scripts (shared across all channels)

Files in `src/NoMercyBot.CommandsRewards/commands/*.cs` are platform commands. They are compiled once at startup and registered in every channel's command registry.

**Current behavior**: `CommandScriptLoader.LoadAllAsync()` loads .cs files, evaluates them via Roslyn, and registers them with the global `TwitchCommandService`.

**Multi-channel change**:
- `CommandScriptLoader` compiles scripts once and stores the resulting `IBotCommand` instances in a list.
- When a new channel is onboarded, these pre-compiled commands are registered in that channel's `CommandRegistry`.
- The `CommandScriptContext` already includes `BroadcasterId`, `Channel`, `DatabaseContext`, `ServiceProvider`, so scripts naturally operate in the correct channel context when invoked.

### 9.2 Per-Channel Custom Commands

Database-backed commands (the `Command` table) are per-channel after adding `BroadcasterId`. Each channel loads only its own commands from the database.

### 9.3 Script Execution Context

The `CommandScriptContext` (file: `src/NoMercyBot.Services/Twitch/Scripting/CommandScriptContext.cs`) already contains:
- `BroadcasterId`
- `Channel` (channel name)
- `DatabaseContext`
- `TwitchChatService`
- `TwitchApiService`
- `ServiceProvider`

No structural changes needed. The caller must populate these correctly per-channel.

### 9.4 Reward Scripts

Same pattern as commands. Platform reward scripts in `src/NoMercyBot.CommandsRewards/rewards/*.cs` are compiled once and registered in every channel. The `RewardScriptContext` already has `BroadcasterId`.

---

## 10. Database Migration Strategy

### 10.1 Migration Plan

**Step 1: Schema migration**
Create an EF Core migration that:
1. Adds all new columns (`BroadcasterId` on Command, Reward, EventSubscription, Widget, Configuration, Storage, Record, Service, UserTtsVoice, TtsUsageRecord; `IsOnboarded`, `BotJoinedAt`, `SubscriptionTier` on Channel; `Role`, `GrantedAt`, `GrantedBy` on ChannelModerator).
2. Creates the `ChannelBotAuthorizations` table.
3. Adds new indexes.
4. Drops old unique indexes, creates new composite ones.

**Step 2: Data migration**
A data migration script that:
1. Finds the existing single broadcaster from `Service WHERE Name = 'Twitch' AND BroadcasterId IS NULL`.
2. Sets that broadcaster's Twitch user ID as `BroadcasterId` on all existing rows that need it:
   - All `Command` rows get `BroadcasterId = existingBroadcasterId`
   - All `Reward` rows get `BroadcasterId = existingBroadcasterId`
   - All `EventSubscription` rows get `BroadcasterId = existingBroadcasterId`
   - All `Widget` rows get `BroadcasterId = existingBroadcasterId`
   - All `Record` rows get `BroadcasterId = existingBroadcasterId`
   - `Configuration` rows for channel-specific config get `BroadcasterId = existingBroadcasterId`; platform config stays null.
   - `Storage` rows get `BroadcasterId = existingBroadcasterId`
   - `UserTtsVoice` rows get `BroadcasterId = existingBroadcasterId`
   - `TtsUsageRecord` rows get `BroadcasterId = existingBroadcasterId`
3. Creates `ChannelModerator(ChannelId = existingBroadcasterId, UserId = existingBroadcasterId, Role = "owner")`.
4. Creates `Service(Name = "Twitch", BroadcasterId = existingBroadcasterId)` with the existing tokens, and clears the tokens from the global Service row (keeping only ClientId/ClientSecret on the global row).
5. Similarly splits Spotify/Discord/OBS service rows.
6. Sets `Channel.IsOnboarded = true` for the existing channel.

**Step 3: NOT NULL enforcement**
After data migration populates all values, a subsequent migration makes `BroadcasterId` NOT NULL on tables where it is required.

### 10.2 Rollback Plan

- Before migration, export a full SQLite backup (`cp nomercy.db nomercy.db.backup`).
- Each EF Core migration is reversible via `dotnet ef migrations remove` or `dotnet ef database update <previous-migration>`.
- The data migration script stores the "pre-migration state" flag in a Configuration row, allowing a rollback script to undo the data transformation.

---

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
- `src/NoMercyBot.Services/Twitch/ClaudeSessionBridge.cs` -- Convert from static to instance; per-channel sessions
- `src/NoMercyBot.Services/Twitch/ClaudeIpcService.cs` -- Channel prefix in pipe protocol
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
- `src/NoMercyBot.Services/Spotify/SpotifyWebsocketService.cs` -- Multi-channel websocket connections
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

## 12. Risk Register

| # | Risk | Probability | Impact | Mitigation |
|---|------|-------------|--------|------------|
| 1 | **SQLite concurrency under multi-channel load.** SQLite uses file-level locking; concurrent writes from multiple channels may cause contention. | High | High | Use WAL mode (already likely). For Phase 6+, plan PostgreSQL migration. In the interim, ensure all DB writes use scoped DbContext instances and keep transactions short. |
| 2 | **Twitch EventSub subscription limits.** Twitch allows max 10,000 subscriptions per client ID. Each channel needs ~30+ subscriptions. This limits scaling to ~300 channels. | Medium | High | Monitor subscription count. Prioritize essential events. For large scale, apply for increased limits or use Twitch Conduit (sharding). |
| 3 | **Token refresh cascade failure.** If Twitch has an outage, all channel token refreshes fail simultaneously, potentially leaving all channels unable to operate. | Low | High | Implement exponential backoff per-channel. Cache last-known-good tokens. Alert on refresh failures but do not remove channels. |
| 4 | **Memory growth with many channels.** Each ChannelContext holds command registries, shoutout queues, session chatters, and emote caches. 100+ channels could be significant. | Medium | Medium | Profile memory usage. Implement lazy loading of ChannelContext (only load when channel is live). Evict idle channel contexts after configurable timeout. |
| 5 | **Cross-channel data leakage.** A bug in channel scoping could expose one channel's data to another channel's owner. | Low | Critical | Comprehensive integration tests that verify channel isolation. The ChannelAuthorizationFilter is the single enforcement point -- it must be thoroughly tested. |
| 6 | **Backward compatibility breaks the frontend.** Moving routes could break the existing dashboard. | Medium | Medium | The backward compatibility middleware provides a grace period. The frontend migration can happen incrementally. |
| 7 | **Roslyn script compilation time with many channels.** Scripts are compiled once but registered per-channel. Registration is O(n) per channel. | Low | Low | Scripts are compiled once and the IBotCommand objects are reused. Registration is just adding to a dictionary -- very fast. |
| 8 | **Spotify websocket connection limits.** Spotify may not support dozens of concurrent websocket connections from one IP. | Medium | Medium | Implement connection pooling and fallback to polling. Start with a conservative limit (e.g., 10 concurrent Spotify connections) and test scaling. |
| 9 | **IRC connection to many channels.** TwitchLib IRC may struggle with 100+ channel joins on a single connection. | Medium | Medium | Twitch recommends max 20 JOIN/10s and max 500 channels per connection. For scale, open multiple IRC connections. |
| 10 | **Data migration corrupts existing data.** The Phase 2 data migration script has a bug that incorrectly assigns BroadcasterId values. | Low | Critical | Full database backup before migration. Migration is tested against a copy of production data first. Migration script is idempotent. |
| 11 | **Static field residue.** Some code path still references a static field (TwitchConfig._service, SpotifyConfig._service) for a per-channel operation, silently using the wrong channel's credentials. | Medium | High | Comprehensive grep for all static field usages after Phase 3. Add Roslyn analyzer or code review checklist. Consider making static fields `[Obsolete]` with error severity. |
| 12 | **ChatMessage constructor creates raw AppDbContext.** The `ChatMessage` constructor (line 136-141) and `ChatMention` constructor (line 26) create `new AppDbContext()` directly, bypassing DI. | High | Medium | Refactor these constructors to not access the database directly. Pass the required data as parameters instead of looking it up in the constructor. This is a pre-existing tech debt that becomes more dangerous with multi-channel. |

---

### Critical Files for Implementation
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\Twitch\TwitchChatService.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\Twitch\TwitchCommandService.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Database\AppDbContext.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\ServiceResolver.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\Twitch\TwitchWebsocketHostedService.cs`
