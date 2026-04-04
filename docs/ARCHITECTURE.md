# NoMercyBot Multi-Channel Platform Architecture Specification

## Phases 2 through 6

---

## 1. Overview

### 1.1 Vision

NoMercyBot is evolving from a single-broadcaster Twitch bot into a multi-channel platform. One server instance serves many broadcasters simultaneously. Each broadcaster signs up via Twitch OAuth, connects their own Spotify/Discord/OBS integrations, configures commands, rewards, widgets, and event subscriptions independently.

### 1.2 Core Architecture

- **Deployment model**: One server process, many channels.
- **Database**: Migrate from SQLite to PostgreSQL in Phase 2 alongside the schema restructuring. Multi-channel concurrent writes will break SQLite's single-writer lock.
- **Service model**: Services that currently hold single-broadcaster state in static fields or singletons become channel-aware via a ChannelRegistry pattern.

### 1.3 Naming Conventions

| Context | Term | Example |
|---------|------|---------|
| User-facing (dashboard, UI, docs) | **Channel** | "Your Channel", "Channel Settings" |
| Code / Database columns | **BroadcasterId** | `string BroadcasterId`, FK column names |
| API routes | **channels** | `/api/channels/{channelId}/commands` |
| Twitch API alignment | **Broadcaster** | Matches Twitch Helix terminology |

The word "Tenant" is never used anywhere.

### 1.4 Architectural Principles

These apply to ALL code written for the platform:

**1. Dependency Injection everywhere, but no MediatR.** No `new Service()` or static singletons. Every service is registered in DI and injected. This allows swapping implementations (e.g., `IMusicProvider` has `SpotifyMusicProvider` and future `YouTubeMusicProvider`), testing with mocks, and per-channel resolution via the ChannelRegistry. We do NOT use MediatR/CQRS -- it adds unnecessary indirection and complexity for a bot platform. Services call services directly via interfaces. Domain events use a simple `IEventBus` with `IEventHandler<T>` implementations registered in DI, not MediatR's pipeline behaviors.

**2. Provider/interface pattern for all integrations.** Every external integration is behind an interface:
- `IMusicProvider` -- Spotify, YouTube Music (future)
- `ITtsProvider` -- Azure, Edge, Google (already implemented)
- `IChatProvider` -- Twitch (could support YouTube Live chat in future)
- `IStreamNotificationProvider` -- Discord (could support Telegram, Slack in future)
- `IStreamControlProvider` -- OBS (could support Streamlabs in future)

New providers are added by implementing the interface and registering in DI. Zero changes to consuming code.

**3. Event-driven with hooks for extensibility.** All significant actions publish events through `IWidgetEventService` (or a future `IEventBus`). This is the integration point for the Universe system (post-MVP): when a reward is redeemed, the event bus fires, and Universe handlers can listen without modifying the reward processing code. Design every feature with "what if something else needs to react to this?" in mind.

**4. Universe-ready hooks (baked in, not implemented).** The following extension points must exist in the architecture even though the Universe system is post-MVP:
- Reward redemption pipeline: `OnBeforeRewardProcessed`, `OnAfterRewardProcessed` hooks
- Command execution pipeline: `OnBeforeCommandExecuted`, `OnAfterCommandExecuted` hooks
- A generic `IEventHandler<TEvent>` pattern that additional systems can subscribe to
- Shared state storage pattern (the `UniverseState` table can be added later, but the `Record` table will support arbitrary JSON per-user-per-channel after the BroadcasterId column is added in Phase 2)

These hooks cost nothing to implement (just fire events at the right places) but save a complete rewrite when the Universe system ships.

**5. No `new AppDbContext()`.** Every database access goes through DI-injected `AppDbContext` or `IDbContextFactory<AppDbContext>`. The two existing violations (`ChatMessage.cs` constructor and `DiscordAuthService.cs`) are fixed in Phase 2.

**6. Soft deletes.** Entities are never hard-deleted in normal operation. All deletable entities have a `DeletedAt` (DateTime?) column. A global query filter in `OnModelCreating` automatically excludes soft-deleted rows: `.HasQueryFilter(e => e.DeletedAt == null)`. Hard deletes only happen for GDPR data erasure requests (section 24) and periodic cleanup of rows soft-deleted more than 90 days ago. Benefits: undo mistakes, audit trail, referential integrity preserved, no FK cascade issues.

**7. Query optimization -- minimize joins, no lazy loading.** EF Core lazy loading is DISABLED globally (`UseLazyLoadingProxies()` is not called). All related data is loaded explicitly via:
- `.Include()` only when the related data is actually needed for the response
- Projection via `.Select()` for API responses (never return full entity graphs)
- Separate queries over joins when the related data is optional (N+1 is acceptable for 1-2 optional includes; joins for required includes)
- Navigation properties exist for schema definition but are NOT populated by default
- Chat message processing (hot path) uses raw SQL or Dapper for performance-critical queries
- ChannelRegistry caches frequently-accessed data (channel config, command registry, permissions) to avoid DB reads on every chat message

---

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

#### 2.1.2 Channels (merged with ChannelInfo)

**Current state**: Two separate tables -- `Channel` (config) and `ChannelInfo` (stream state). These represent the same entity and should be merged.

**Current Channel schema** (file: `src/NoMercyBot.Database/Models/Channel.cs`):
- `Id` (string, PK, max 50) -- same as User.Id (broadcaster's Twitch ID)
- `Name` (string, max 25)
- `Enabled` (bool)
- `ShoutoutTemplate` (string, max 450)
- `LastShoutout` (DateTime?)
- `ShoutoutInterval` (int, default 10)
- `UsernamePronunciation` (string?, max 100)
- `CreatedAt`, `UpdatedAt`

**Current ChannelInfo schema** (file: `src/NoMercyBot.Database/Models/ChannelInfo.cs`):
- `Id` (string, PK, max 50)
- `IsLive` (bool)
- `Language` (string, max 50)
- `GameId` (string, max 50)
- `GameName` (string, max 255)
- `Title` (string, max 255)
- `Delay` (int)
- `Tags` (List\<string\>, JSON)
- `ContentLabels` (List\<string\>, JSON)
- `IsBrandedContent` (bool)

**Merged Channel entity**:
```
Channel
  -- Identity
  - Id: string (PK, max 50) -- Twitch user ID
  - Name: string (max 25) -- channel name (login)
  - Enabled: bool
  
  -- Configuration (rarely changes)
  - ShoutoutTemplate: string (max 450)
  - LastShoutout: DateTime?
  - ShoutoutInterval: int (default 10)
  - UsernamePronunciation: string? (max 100)
  - IsOnboarded: bool (default false)
  - BotJoinedAt: DateTime?
  - DeletedAt: DateTime? -- soft delete
  - OverlayToken: string (UUID, unique) -- for widget auth
  
  -- Stream state (changes frequently, updated by EventSub/polling)
  - IsLive: bool
  - Language: string (max 50)
  - GameId: string (max 50)
  - GameName: string (max 255)
  - Title: string (max 255)
  - StreamDelay: int
  - Tags: string[] (JSON)
  - ContentLabels: string[] (JSON)
  - IsBrandedContent: bool
  
  -- Timestamps
  - CreatedAt, UpdatedAt
  
  -- Navigation
  - User (FK to User via Id)
  - UsersInChat, Events, ChannelModerators
```

**Migration**: Merge ChannelInfo columns into Channel, copy data, drop ChannelInfo table. Update all references from `ChannelInfo` to `Channel`.

**Volatile state in memory**: The frequently-changing fields (IsLive, Title, GameName, etc.) are also cached in the `ChannelRegistry`'s `ChannelContext` in-memory to avoid DB reads on every API call. DB is the source of truth; memory is the hot cache.

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
- Add `Role` (string, max 20, NOT NULL, default "moderator") -- "moderator" or "lead_moderator"
- Add `GrantedAt` (DateTime, default CURRENT_TIMESTAMP)
- Add `GrantedBy` (string?, FK to User) -- Who granted dashboard access
- Add `DeletedAt` (DateTime?) -- soft delete

**Why**: The `ChannelModerator` table tracks which users have dashboard access for a channel. The `Role` column distinguishes moderators from lead moderators (who can moderate other moderators). The broadcaster is always implicitly the channel owner (channelId == userId) and doesn't need a row here. Editors are detected via the Twitch API (`Get Channel Editors`) and don't need a row either.

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
- Add `BroadcasterId` (string?, max 50, FK to Channel) -- null means global/platform-level service
- Drop unique index on `Name`, replace with unique index on (`Name`, `BroadcasterId`)

**Why**: The Service table becomes the **single source of truth for all OAuth credentials**. This includes:
- Platform Twitch app credentials (`Name = "Twitch"`, `BroadcasterId = null`)
- Platform bot user token (`Name = "TwitchBot"`, `BroadcasterId = null`) -- migrated from BotAccount table
- Platform bot app token (`Name = "TwitchBotApp"`, `BroadcasterId = null`) -- migrated from BotAccount.AppAccessToken
- Per-channel Twitch grant (`Name = "Twitch"`, `BroadcasterId = channelId`)
- Per-channel Spotify grant (`Name = "Spotify"`, `BroadcasterId = channelId`)
- Per-channel Discord config (`Name = "Discord"`, `BroadcasterId = channelId`)
- Per-channel OBS config (`Name = "OBS"`, `BroadcasterId = channelId`)

The BotAccount table is removed. One table, one entity, one pattern for all credentials.

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

**Why**: Storage is used for per-channel state (e.g., feature flags, temporary data).

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

#### 2.1.18 BotAccount -- MERGED INTO SERVICE

**Current schema** (file: `src/NoMercyBot.Database/Models/BotAccount.cs`):
- `Id` (int, PK, identity)
- `Username` (string, unique index)
- `ClientId`, `ClientSecret` (string, encrypted)
- `AccessToken`, `RefreshToken` (string, encrypted)
- `TokenExpiry` (DateTime?)
- `AppAccessToken` (string, encrypted)
- `AppTokenExpiry` (DateTime?)

**Change**: Remove the `BotAccount` table entirely. The bot account becomes a `Service` row:
- `Name = "TwitchBot"`, `BroadcasterId = null` (platform-level)
- `AccessToken` = the bot's user token (for user:bot scope)
- `RefreshToken` = the bot's refresh token
- `TokenExpiry` = expiry
- The `AppAccessToken` (client credentials token for bot badge) is stored in a second Service row: `Name = "TwitchBotApp"`, `BroadcasterId = null`

**Why**: BotAccount is just another OAuth service credential. It doesn't need its own table. The `Service` table already handles encrypted token storage, refresh, and expiry. This is a proper entity -- one table for all OAuth credentials, differentiated by `Name` and `BroadcasterId`.

**Migration**: Copy BotAccount data into two Service rows, drop BotAccount table. Update all code that queries `BotAccounts` to query `Services WHERE Name = "TwitchBot"`.

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

#### 2.2.3 ChannelFeatures

```
Table: ChannelFeatures
  - Id              int, PK, identity
  - BroadcasterId   string(50), NOT NULL, FK to Channel
  - FeatureKey      string(50), NOT NULL (e.g. "shoutouts", "rewards", "moderation")
  - IsEnabled       bool, default false
  - EnabledAt       DateTime?
  - RequiredScopes  string[] (JSON, the OAuth scopes this feature needs)
  - CreatedAt       DateTime
  - UpdatedAt       DateTime
  - Unique: (BroadcasterId, FeatureKey)
```

**Why**: Tracks which features a broadcaster has enabled via progressive OAuth scope upgrades (section 3.2).

#### 2.2.4 Permissions

```
Table: Permissions
  - Id              int, PK, identity
  - BroadcasterId   string(50), NOT NULL, FK to Channel
  - SubjectType     string(10), NOT NULL ("user" or "role")
  - SubjectId       string(50), NOT NULL (Twitch user ID or role name)
  - ResourceType    string(20), NOT NULL ("command", "reward", "widget", "feature")
  - ResourceId      string? (specific resource ID, null = all of type)
  - Permission      string(5), NOT NULL ("allow" or "deny")
  - DeletedAt       DateTime? -- soft delete
  - CreatedAt       DateTime
  - UpdatedAt       DateTime
  - Index: (BroadcasterId, ResourceType, ResourceId)
  - Index: (BroadcasterId, SubjectType, SubjectId)
```

**Why**: Granular per-command/reward/feature permissions (section 20).

#### 2.2.5 ChannelSubscription

```
Table: ChannelSubscriptions
  - Id                    int, PK, identity
  - BroadcasterId         string(50), FK to Channel, unique
  - Tier                  string(20), NOT NULL ("free", "starter", "pro", "platform")
  - StripeCustomerId      string?
  - StripeSubscriptionId  string?
  - CurrentPeriodEnd      DateTime?
  - Status                string(20) ("active", "past_due", "canceled", "trialing")
  - CreatedAt             DateTime
  - UpdatedAt             DateTime
```

**Why**: Billing state (section 22). Single source of truth for subscription tier.

#### 2.2.6 DeletionAuditLog

```
Table: DeletionAuditLog
  - Id              int, PK, identity
  - RequestType     string(30) ("user_deletion", "channel_deletion", "twitch_revoke")
  - SubjectIdHash   string(64) (SHA256 of deleted user/channel ID)
  - RequestedBy     string(20) ("self", "twitch", "admin")
  - TablesAffected  string[] (JSON)
  - RowsDeleted     int
  - CompletedAt     DateTime
  - CreatedAt       DateTime
```

**Why**: GDPR audit trail (section 24). Contains no PII -- only hashes.

### 2.3 Soft Delete Convention

All entities that can be deleted by users have a `DeletedAt` (DateTime?) column. A global query filter in `AppDbContext.OnModelCreating` excludes soft-deleted rows automatically:

```csharp
modelBuilder.Entity<Command>().HasQueryFilter(e => e.DeletedAt == null);
modelBuilder.Entity<Reward>().HasQueryFilter(e => e.DeletedAt == null);
// ... etc for all soft-deletable entities
```

**Entities with soft delete**: Command, Reward, Widget, Record, Permissions, ChannelModerator, ChatMessage (already has DeletedAt), Shoutout, Channel.

**Entities WITHOUT soft delete** (hard delete only): Service (tokens must be destroyed), TtsCacheEntry (cleanup job), DeletionAuditLog (immutable), ChannelFeatures (toggle, not delete).

**GDPR hard delete**: The `IDataDeletionService` uses `.IgnoreQueryFilters()` to find soft-deleted rows and performs permanent deletion after the retention period (90 days).

---

---

## 3. Authentication and Authorization

### 3.1 Token Ownership Principle -- CRITICAL

Broadcaster OAuth tokens are used legitimately by the platform to provide the features the broadcaster signed up for (EventSub, shoutouts, reward management, Spotify playback control, etc.). This is standard OAuth -- the user explicitly grants scopes via a consent screen and can revoke at any time.

**Rules**:
1. **Tokens are only used by automated server processes** for the features the broadcaster enabled. They are never used for ad-hoc manual access by anyone.
2. **Platform admins have ZERO access to broadcaster resources** via their tokens. No admin API endpoint, no admin dashboard page, no debug tool may use a broadcaster's token to access their Twitch/Spotify/Discord data. Admin access is limited to platform-level operations (user management, system health, etc.).
3. **No undocumented API hacks**. Every token is obtained via official OAuth consent screens. The Discord session token hack is removed.
4. **Tokens are encrypted at rest** and never exposed in API responses, logs, or error messages.
5. **Users can revoke at any time** from their Twitch/Spotify/Discord settings. The platform handles revocation gracefully (disables affected features, notifies the broadcaster to re-authorize).

### 3.2 Progressive OAuth Scopes

**Problem**: Twitch's OAuth consent screen is intimidating. Requesting 15+ scopes upfront makes users think "this bot wants to take over my channel." Most users will bounce.

**Solution**: Start with the absolute minimum to get the user onboarded. Then let them enable features from the dashboard, each clearly explaining WHY the additional scope is needed. The user is in control.

#### Onboarding Scopes (Bare Minimum)

These are the ONLY scopes requested during initial sign-up:

| Scope | Why (shown to user) |
|-------|---------------------|
| `user:read:chat` | Lets the bot read chat messages |
| `moderator:read:chatters` | Lets the bot see who's in chat |

That's it. Two scopes. The consent screen is tiny and non-threatening. The user gets: bot reads chat and responds to commands. Core functionality works.

Note: `channel:bot` is NOT requested here -- that scope is for the BOT's token (platform-owned), not the broadcaster's token. The bot's ability to chat in the channel is handled separately during onboarding (the broadcaster authorizes the bot app with `channel:bot` in a separate step -- see section 6).

#### Feature-Driven Scope Upgrades

From the dashboard, the broadcaster sees a **Features** page listing everything the bot can do. Each feature shows:
- What it does
- What permission it needs and WHY
- An "Enable" button that triggers a re-authorization with the additional scope

| Feature | Additional Scopes | User-Facing Explanation |
|---------|-------------------|----------------------|
| **Shoutouts** | `moderator:manage:shoutouts`, `moderator:read:shoutouts` | "Send shoutouts when a streamer friend visits your channel" |
| **Channel Point Rewards** | `channel:read:redemptions`, `channel:manage:redemptions` | "Let the bot manage custom rewards like song requests and TTS" |
| **Announcements** | `moderator:manage:announcements` | "Post highlighted announcements in your chat" |
| **Moderation Tools** | `channel:moderate`, `moderator:manage:banned_users`, `moderator:manage:blocked_terms` | "Ban/timeout users, manage blocked terms from the dashboard" |
| **Follower Alerts** | `moderator:read:followers` | "Track new followers and show alerts on your overlay" |
| **Sub Tracking** | `channel:read:subscriptions` | "Track subscribers for leaderboards and alerts" |
| **Bits & Cheers** | `bits:read` | "Track bit donations for leaderboards and alerts" |
| **Polls & Predictions** | `channel:read:polls`, `channel:manage:polls`, `channel:read:predictions`, `channel:manage:predictions` | "Create and manage polls and predictions from chat or dashboard" |
| **Hype Train** | `channel:read:hype_train` | "Show hype train progress on your overlay" |
| **Raids** | `channel:manage:raids` | "Start raids from the dashboard or with a chat command" |
| **Ad Management** | `channel:manage:ads`, `channel:read:ads` | "Manage ad schedules and snooze ads from the dashboard" |
| **Clips** | `clips:edit` | "Create clips from chat commands or automatically on highlights" |
| **Chat Settings** | `moderator:manage:chat_settings` | "Control emote-only, slow mode, and sub-only mode from dashboard" |
| **Shield Mode** | `moderator:manage:shield_mode`, `moderator:read:shield_mode` | "Emergency panic button to lock down your chat" |
| **Stream Info** | `channel:manage:broadcast` | "Change your title, game, and tags from the dashboard" |
| **VIP/Mod Management** | `channel:manage:vips`, `channel:manage:moderators` | "Add and remove VIPs and moderators from the dashboard" |
| **Whispers** | `user:manage:whispers` | "Let the bot send whisper notifications" |

#### How Re-Authorization Works

1. User clicks "Enable" on a feature in the dashboard
2. Platform redirects to Twitch OAuth with the **new scope added to existing scopes**
3. Twitch shows a consent screen with ONLY the new scope highlighted (Twitch handles incremental auth)
4. User approves, callback fires with updated token containing all scopes
5. Token is updated in the `Service` table
6. Feature is activated, dashboard refreshes

#### Scope Tracking

```
ChannelFeatures (new table)
  - Id: int (PK, identity)
  - BroadcasterId: string (FK to Channel, NOT NULL)
  - FeatureKey: string (NOT NULL, e.g. "shoutouts", "rewards", "moderation")
  - IsEnabled: bool (default false)
  - EnabledAt: DateTime?
  - RequiredScopes: string[] (JSON, the scopes this feature needs)
  - CreatedAt, UpdatedAt
  - Unique: (BroadcasterId, FeatureKey)
```

On every API call that needs a specific scope, the platform checks:
1. Is the feature enabled for this channel?
2. Does the stored token actually have the required scope? (Twitch returns granted scopes on token validation)
3. If not, return a clear error: "This feature requires the X permission. Enable it from your dashboard."

#### Scope Revocation Handling

If a user revokes scopes from Twitch Settings:
- Token validation returns the remaining scopes
- Platform detects missing scopes, disables affected features
- Dashboard shows a banner: "Some features were disabled because permissions were revoked. Re-enable from Features page."
- No crash, no error -- graceful degradation

### 3.3 Role Model -- All Twitch Roles

The platform uses **every Twitch role exactly as Twitch defines them**. No invented roles.

| Role | Twitch Badge | Source | Chat Permissions | Dashboard Access |
|------|-------------|--------|-----------------|-----------------|
| **Broadcaster** | Channel owner icon | Channel ownership | Everything | Full control of their channel |
| **Editor** | No badge (invisible role) | Twitch Editor assignment | Same as Viewer in chat | Edit stream info (title, game, tags), create clips, manage VODs. Cannot manage chat/commands |
| **Lead Moderator** | Sword with star | Twitch assignment (by broadcaster) | All mod powers + can moderate other moderators | Same as Moderator + can add/remove moderators, view mod action analytics, manage moderator permissions |
| **Moderator** | Sword | Twitch `/mod` command or `ChannelModerator` table | Ban, timeout, delete messages, manage chat modes | Manage commands, rewards, view settings, send bot messages |
| **VIP** | Gem | Twitch `/vip` command | Bypass slow mode, sub-only mode, followers-only mode | View their personal stats (watch time, message count, command usage) |
| **Subscriber** | Sub badge (tiered) | Twitch subscription | Use sub-only commands, sub-only chat modes | View their personal stats |
| **Viewer** | None | Default | Basic chat commands | View their personal stats |

### 3.3.1 Dashboard Access by Role

| Role | What They See |
|------|--------------|
| **Broadcaster** | Full channel dashboard: commands, rewards, widgets, moderation, integrations, settings, permissions, billing |
| **Editor** | Stream info editor (title, game, tags, schedule), clips, VODs. Read-only view of commands/rewards |
| **Lead Moderator** | Same as Moderator + mod action analytics, elevated in mod log |
| **Moderator** | Commands (CRUD), rewards (CRUD), chat settings, mod tools (bans, blocked terms, shield mode), widget demo triggers |
| **VIP** | Personal stats page: watch time, message count, command usage, follow age, sub history |
| **Subscriber** | Personal stats page: same as VIP |
| **Viewer** | Personal stats page: watch time, message count, follow age |

### 3.3.2 How Dashboard Access is Determined

1. User logs in via Twitch OAuth.
2. System checks: are they a **broadcaster** of any channel on the platform? -> Full access to their channel(s).
3. System queries the Twitch API `Get Channel Editors` -> Editor access.
4. System checks `ChannelModerator` table OR Twitch API `Get Moderators` -> Moderator/Lead Moderator access.
5. For VIP/Sub/Viewer: they can log in and see their **personal stats** for any channel they've interacted with. They cannot manage anything.
6. Platform admin list stored in `Configuration(Key = "platform_admins", Value = "comma,separated,twitch,ids", BroadcasterId = null)`.

### 3.3.3 Permission Override System (Chat Only)

The existing `!whitelist` / `!unwhitelist` commands let broadcasters grant someone subscriber/VIP/mod level **for bot commands in chat** without changing their actual Twitch role. This only affects `CommandPermission` checks, not dashboard access.

### 3.3.4 Real-time Permission Propagation

Permission changes (role changes, permission overrides, feature toggles) MUST take effect immediately in both chat and dashboard:

- **Chat**: The `PermissionService` cache is invalidated instantly when a permission is changed via API or chat command. The next command execution uses the new permissions. No restart required.
- **Dashboard**: Permission changes are pushed via SignalR to all connected dashboard sessions for that channel. The frontend reactively updates UI elements (hides/shows buttons, enables/disables forms) without page reload.
- **Implementation**: The `PermissionService` publishes a `PermissionChanged` event through the event bus. Chat command handlers and SignalR hub subscribers listen for this event.

### 3.3.5 API Authorization Mapping

| Endpoint Category | Min Role |
|-------------------|----------|
| View personal stats | Viewer (own stats only) |
| View channel dashboard/analytics | Moderator |
| Manage commands -- text/random/counter (CRUD) | Moderator |
| Manage commands -- pipeline (CRUD) | Broadcaster |
| Manage rewards (CRUD) | Moderator |
| Trigger widget demo events | Moderator |
| Chat settings (emote-only, slow mode) | Moderator |
| Mod tools (bans, blocked terms, shield) | Moderator |
| Add/remove moderators | Lead Moderator |
| View mod action analytics | Lead Moderator |
| Edit stream info (title, game, tags) | Editor |
| Manage clips | Editor |
| View/edit channel settings | Broadcaster |
| Connect integrations (Spotify/Discord/OBS) | Broadcaster |
| Manage EventSub subscriptions | Broadcaster |
| Send bot messages | Moderator |
| Manage permissions | Broadcaster |
| Manage billing | Broadcaster |

### 3.4 Token Validation Flow

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

### 3.5 Token Caching

Twitch `/oauth2/validate` should not be called on every request. Introduce an in-memory cache:
- Key: access token hash (SHA256)
- Value: validated user info + expiry
- TTL: 5 minutes
- Implementation: `IMemoryCache` with sliding expiration
- On token refresh, invalidate the cache entry

### 3.6 Session Management

No change to the stateless Bearer token model. Each request is independently authenticated. The dashboard frontend stores the user's session token (obtained via the platform's OAuth flow) and includes it in API calls. The platform validates this against Twitch on each request (with caching).

---

---

## 4. API Design

### 4.1 API Versioning

All API routes are versioned from day one: `/api/v1/...`. This allows evolving the API without breaking existing integrations.

- **URL prefix**: `/api/v1/` for all endpoints
- **Version negotiation**: URL-based only (not header-based) -- simpler for OBS browser sources and webhooks
- **Breaking changes**: bump to `/api/v2/`, keep v1 alive for a deprecation period (minimum 6 months)
- **Non-breaking changes** (new fields, new optional params): added to current version without bump
- **ASP.NET Core API Versioning**: Use `Asp.Versioning.Http` NuGet package with `[ApiVersion("1.0")]` attributes

### 4.2 Route Structure

All channel-scoped endpoints: `/api/v1/channels/{channelId}/...`. The `channelId` is the broadcaster's Twitch user ID.

### 4.3 Endpoint Groups

#### Global Endpoints (no channel scope)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/status` | None | Health check |
| GET | `/api/v1/oauth/{provider}/login` | None | OAuth redirect |
| GET | `/api/v1/oauth/{provider}/callback` | None | OAuth callback |
| POST | `/api/v1/oauth/{provider}/validate` | Bearer | Validate token |
| GET | `/api/v1/channels` | Bearer | List channels the user has access to |
| POST | `/api/v1/channels/onboard` | Bearer | Start onboarding for the authenticated user's channel |

#### Channel-Scoped Endpoints

| Method | Route | Min Role | Description |
|--------|-------|----------|-------------|
| **Commands** | | | |
| GET | `/api/v1/channels/{channelId}/commands` | Moderator | List commands |
| GET | `/api/v1/channels/{channelId}/commands/{name}` | Moderator | Get command |
| POST | `/api/v1/channels/{channelId}/commands` | Moderator (text/random/counter), Broadcaster (pipeline) | Create command |
| PUT | `/api/v1/channels/{channelId}/commands/{name}` | Moderator (text/random/counter), Broadcaster (pipeline) | Update command |
| DELETE | `/api/v1/channels/{channelId}/commands/{name}` | Moderator | Delete command |
| **Rewards** | | | |
| GET | `/api/v1/channels/{channelId}/rewards` | Moderator | List Twitch rewards |
| POST | `/api/v1/channels/{channelId}/rewards` | Broadcaster | Create reward |
| PATCH | `/api/v1/channels/{channelId}/rewards/{rewardId}/redemptions/{redemptionId}` | Moderator | Update redemption |
| GET | `/api/v1/channels/{channelId}/rewards/bot-rewards` | Moderator | List bot rewards |
| POST | `/api/v1/channels/{channelId}/rewards/bot-rewards` | Moderator | Add/update bot reward |
| DELETE | `/api/v1/channels/{channelId}/rewards/bot-rewards/{identifier}` | Moderator | Remove bot reward |
| **Widgets** | | | |
| GET | `/api/v1/channels/{channelId}/widgets` | Moderator | List widgets |
| GET | `/api/v1/channels/{channelId}/widgets/{id}` | Moderator | Get widget |
| POST | `/api/v1/channels/{channelId}/widgets` | Broadcaster | Create widget |
| PUT | `/api/v1/channels/{channelId}/widgets/{id}` | Moderator | Update widget |
| DELETE | `/api/v1/channels/{channelId}/widgets/{id}` | Broadcaster | Delete widget |
| **Events** | | | |
| GET | `/api/v1/channels/{channelId}/events/{provider}` | Moderator | List event subscriptions |
| POST | `/api/v1/channels/{channelId}/events/{provider}` | Broadcaster | Create subscription |
| PUT | `/api/v1/channels/{channelId}/events/{provider}/{id}` | Broadcaster | Update subscription |
| DELETE | `/api/v1/channels/{channelId}/events/{provider}/{id}` | Broadcaster | Delete subscription |
| **Integrations** | | | |
| GET | `/api/v1/channels/{channelId}/settings/providers` | Moderator | List connected services |
| PUT | `/api/v1/channels/{channelId}/settings/providers/{provider}` | Broadcaster | Update service config |
| **Music** | | | |
| GET | `/api/v1/channels/{channelId}/music/now-playing` | Moderator | Current track |
| GET | `/api/v1/channels/{channelId}/music/queue` | Moderator | Current queue |
| POST | `/api/v1/channels/{channelId}/music/queue` | Moderator | Add to queue |
| POST | `/api/v1/channels/{channelId}/music/skip` | Moderator | Skip track |
| POST | `/api/v1/channels/{channelId}/music/pause` | Moderator | Pause |
| POST | `/api/v1/channels/{channelId}/music/resume` | Moderator | Resume |
| PUT | `/api/v1/channels/{channelId}/music/volume` | Moderator | Set volume |
| GET | `/api/v1/channels/{channelId}/music/search?q=...` | Moderator | Search tracks |
| **Stream Info** | | | |
| GET | `/api/v1/channels/{channelId}/stream` | Moderator | Get stream info |
| PUT | `/api/v1/channels/{channelId}/stream` | Editor | Update title, game, tags |
| **Config** | | | |
| GET | `/api/v1/channels/{channelId}/config` | Moderator | Get channel config |
| PUT | `/api/v1/channels/{channelId}/config` | Broadcaster | Update channel config |
| **TTS** | | | |
| GET | `/api/v1/channels/{channelId}/tts/voices` | Moderator | List TTS voices |
| POST | `/api/v1/channels/{channelId}/tts/speak` | Moderator | Trigger TTS |
| **Bot** | | | |
| GET | `/api/v1/channels/{channelId}/bot/status` | Moderator | Bot auth status for channel |
| POST | `/api/v1/channels/{channelId}/bot/send` | Moderator | Send message in channel |
| **Moderators** | | | |
| GET | `/api/v1/channels/{channelId}/moderators` | Lead Moderator | List channel moderators |
| POST | `/api/v1/channels/{channelId}/moderators` | Lead Moderator | Invite moderator |
| DELETE | `/api/v1/channels/{channelId}/moderators/{userId}` | Lead Moderator | Remove moderator |
| **User Data** | | | |
| GET | `/api/v1/me/channels` | Viewer | List all channels user has been seen in |
| GET | `/api/v1/me/channels/{channelId}/stats` | Viewer | Personal stats for a channel |
| POST | `/api/v1/me/delete-data` | Viewer | Delete all personal data |
| POST | `/api/v1/me/delete-data/{channelId}` | Viewer | Delete personal data from specific channel |
| GET | `/api/v1/me/export-data` | Viewer | Export all personal data (GDPR) |
| **Permissions** | | | |
| GET | `/api/v1/channels/{channelId}/permissions` | Broadcaster | List all permissions |
| POST | `/api/v1/channels/{channelId}/permissions` | Broadcaster | Create permission |
| DELETE | `/api/v1/channels/{channelId}/permissions/{id}` | Broadcaster | Delete permission |
| **Actions Registry** | | | |
| GET | `/api/v1/actions` | Moderator | List all available pipeline actions |

### 4.4 Backward Compatibility

During the transition, keep the old routes functional with a compatibility middleware that:
1. Detects requests to old routes (e.g., `/api/commands`).
2. Resolves the "default channel" from the authenticated user's own broadcaster ID.
3. Internally redirects to `/api/v1/channels/{userId}/commands`.
4. Returns a `Deprecation` header with the sunset date.

This ensures the existing frontend continues working during the migration.

---

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

**Current state**: Background service. Iterates all `Service` rows, refreshing tokens near expiry. (BotAccount is merged into Service -- bot tokens are Service rows with `Name="TwitchBot"`, `BroadcasterId=null`.)

**Changes**: 
- Already iterates all Service rows, so it naturally handles multiple channels.
- After refreshing a channel's OAuth grant token, update the ChannelContext in the registry.
- Remove old BotAccount iteration code.
- Add logging of which channel/service the token belongs to.

#### 5.2.11 PermissionService

**Current state**: Singleton. Loads permission overrides from `Record` table (type = "PermissionOverride"). Static `_overrides` dictionary keyed by userId.

**Changes**:
- Overrides become per-channel: keyed by `"{broadcasterId}:{userId}"`.
- `UserHasMinLevel` accepts `broadcasterId` as a parameter.
- `GrantOverride` and `RevokeOverride` include broadcasterId.
- Also queries the `Permissions` table (section 20) for granular per-resource permissions.
- Cache invalidated immediately on change, publishes `PermissionChanged` event via event bus for real-time dashboard updates (section 3.3.4).
- New method: `CanAccess(broadcasterId, userId, userType, resourceType, resourceId)` -- checks both the Permissions table and the role-based hierarchy.

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

---

## 6. Channel Onboarding Flow

Step-by-step when a new broadcaster signs up:

1. **Twitch OAuth callback** fires. System creates/updates `User` and `Channel` records. Stores the OAuth grant token (explicitly authorized by the user) in `Service(Name="Twitch", BroadcasterId=userId)`. A `ChannelFeatures` record is created with only the onboarding features enabled.

2. **No ChannelModerator row needed for the broadcaster** -- ownership is implicit (channelId == userId).

3. **Channel.IsOnboarded = false**. Dashboard shows the onboarding wizard.

4. **Bot authorization**: The system generates a device code for `channel:bot` scope. The broadcaster authorizes, allowing the bot to chat in their channel with the bot badge. A `ChannelBotAuthorization` record is created.

5. **Default EventSub subscriptions created**: Based on a predefined list of essential events (channel.chat.message, channel.follow, stream.online, stream.offline, channel.subscribe, channel.raid, channel.channel_points_custom_reward_redemption.add, etc.). These are registered with Twitch and stored in `EventSubscription` table.

6. **Bot joins channel**: WatchStreakService IRC client joins the new channel. ChannelRegistry creates a new ChannelContext.

7. **Default commands loaded**: Platform script commands (from `src/NoMercyBot.CommandsRewards/commands/`) are registered in the new channel's command registry. No database rows needed -- these are loaded from disk.

8. **Channel.IsOnboarded = true**. Dashboard shows the full management interface.

9. **Optional integrations**: Broadcaster can later connect Spotify, Discord, and OBS through their Channel Settings.

---

---

## 7. Channel Management

### 7.1 Dashboard Structure

After login, the dashboard shows "Your Channel" as the primary view if the user is a channel owner. A dropdown allows switching between channels the user has access to (as owner, editor, or moderator).

### 7.2 Connecting Integrations

- **Spotify**: Owner clicks "Connect Spotify" which initiates Spotify OAuth (Authorization Code Flow with PKCE). Callback stores access + refresh tokens in `Service(Name="Spotify", BroadcasterId=channelId)`. The SpotifyPollingService starts monitoring playback when the channel goes live.
- **Discord**: Same pattern with Discord OAuth.
- **OBS**: Owner enters OBS WebSocket host and password. Stored in `Service(Name="OBS", BroadcasterId=channelId)`.

### 7.3 Managing Commands

- Dashboard shows commands for the selected channel.
- Platform commands (from script files) are shown as read-only with a "Platform" badge.
- Custom commands (from DB) are editable.
- Changes go to `/api/channels/{channelId}/commands` and only affect that channel's registry.

### 7.4 Managing Rewards

- Similar to commands. Platform reward scripts are read-only.
- Custom rewards are per-channel and managed via the Twitch API using the channel's OAuth grant token (which the broadcaster explicitly authorized with `channel:manage:redemptions` scope).

### 7.5 Inviting Moderators

- Owner goes to Channel Settings > Team.
- Enters a Twitch username.
- System looks up the user (via TwitchApiService), creates a `ChannelModerator(ChannelId, UserId, Role="moderator")` record.
- The moderator can now log in and see the channel in their channel switcher.

---

---

## 8. Background Services

### 8.1 TokenRefreshService

**Current**: Checks all Service rows every 1 minute, refreshes tokens expiring within 5 minutes. (BotAccount is now merged into the Service table -- bot tokens are just Service rows with `Name="TwitchBot"` and `BroadcasterId=null`.)

**Multi-channel change**: No structural change. The service already iterates all Service rows. With multi-channel, there are simply more rows (one per channel per provider). After refreshing, the service updates the ChannelRegistry's cached token. Remove the old BotAccount iteration code.

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

---

## 9. Platform Commands and Rewards (Compiled C#, No Roslyn)

### 9.1 Platform Commands (shared across all channels)

Platform commands are **regular C# classes** that implement `IBotCommand`, compiled with the project via `dotnet build`. The Roslyn script loading system (`CommandScriptLoader`) is **removed entirely**.

**Why**: Roslyn script loading was a development convenience in the single-user bot. For a production platform:
- Regular classes have full IDE support (intellisense, refactoring, debugging)
- They're testable with xUnit
- They're type-safe at compile time
- No runtime compilation overhead or security surface
- No file-system dependency for script discovery

**Registration**: Platform commands are registered via DI in `ServiceCollectionExtensions`:
```csharp
services.AddSingleton<IBotCommand, HugCommand>();
services.AddSingleton<IBotCommand, RoastCommand>();
// ... etc
```

When a channel is onboarded, all registered `IBotCommand` instances are added to that channel's `CommandRegistry`.

### 9.2 Per-Channel Custom Commands

User-created commands are stored in the `Command` table with `BroadcasterId`. They use the pipeline action system (section 16) -- no compilation, just JSON interpretation at runtime.

### 9.3 Command Execution Context

The `CommandContext` contains everything a command needs:
- `BroadcasterId` -- which channel this is executing in
- `Channel` -- channel name
- `Message` -- the chat message that triggered it
- `Arguments` -- parsed arguments
- `ServiceProvider` -- DI container for resolving any service
- `TwitchChatService`, `TwitchApiService`, `TtsService` -- commonly used services injected directly

Platform commands access services via the context. Pipeline commands access services via the `ICommandAction` implementations.

### 9.4 Platform Rewards

Same pattern. Platform reward handlers are regular C# classes implementing `IReward`, registered via DI, compiled with the project. The `RewardScriptLoader` is removed.

User-created reward handlers use the same pipeline action system as commands.

### 9.5 Files to Remove

- `src/NoMercyBot.Services/Twitch/Scripting/CommandScriptLoader.cs` -- replaced by DI registration
- `src/NoMercyBot.Services/Twitch/Scripting/RewardScriptLoader.cs` -- replaced by DI registration
- `src/NoMercyBot.Services/Widgets/WidgetScriptLoader.cs` -- replaced by DI registration
- All Roslyn-specific NuGet packages (if not needed elsewhere)

---

---

## 10. Database Migration Strategy

### 10.0 SQLite to PostgreSQL Migration

This happens FIRST in Phase 2, before any schema changes. SQLite's single-writer lock will not survive multi-channel concurrent writes (chat messages, EventSub events, token refreshes all writing simultaneously).

**Migration steps**:
1. Add PostgreSQL NuGet package: `Npgsql.EntityFrameworkCore.PostgreSQL`
2. Update `AppDbContext.OnConfiguring` to use `UseNpgsql()` with connection string from environment variable `DATABASE_URL`
3. Keep SQLite as fallback for local development: `if (env == "Development" && no DATABASE_URL) UseSqlite()` else `UseNpgsql()`
4. Export existing SQLite data to PostgreSQL using `pgloader` or a custom EF Core script that reads from SQLite context and writes to PostgreSQL context
5. Run `dotnet ef migrations add PostgreSQLInitial` to generate the PostgreSQL-compatible migration
6. Verify all existing data is intact
7. Update Docker/deployment config with PostgreSQL connection string

**Why now and not later**: Doing schema changes (adding BroadcasterId to 13 tables, new indexes, new tables) on SQLite and then migrating to PostgreSQL means doing it twice. One migration, one database engine.

### 10.1 Schema Migration Plan

**Step 1: Schema migration**
Create an EF Core migration that:
1. Adds all new columns (`BroadcasterId` on Command, Reward, EventSubscription, Widget, Configuration, Storage, Record, Service, UserTtsVoice, TtsUsageRecord; `IsOnboarded`, `BotJoinedAt`, `OverlayToken`, `DeletedAt` on Channel; merges ChannelInfo columns into Channel; `Role`, `GrantedAt`, `GrantedBy`, `DeletedAt` on ChannelModerator; `DeletedAt` on soft-deletable entities).
2. Creates new tables: `ChannelBotAuthorizations`, `ChannelFeatures`, `Permissions`, `ChannelSubscriptions`, `DeletionAuditLog`.
3. Merges `ChannelInfo` columns into `Channel` table, drops `ChannelInfo`.
4. Migrates `BotAccount` data into `Service` rows (`TwitchBot`, `TwitchBotApp`), drops `BotAccount` table.
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
3. No ChannelModerator row needed for the broadcaster (ownership is implicit: channelId == userId).
4. Creates `Service(Name = "Twitch", BroadcasterId = existingBroadcasterId)` with the existing tokens, and clears the tokens from the global Service row (keeping only ClientId/ClientSecret on the global row).
5. Similarly splits Spotify/Discord/OBS service rows.
6. Sets `Channel.IsOnboarded = true` for the existing channel.

**Step 3: NOT NULL enforcement**
After data migration populates all values, a subsequent migration makes `BroadcasterId` NOT NULL on tables where it is required.

### 10.2 Rollback Plan

- **Before PostgreSQL migration**: Export full SQLite backup (`cp database.sqlite database.sqlite.backup`).
- **Before schema changes**: `pg_dump` the PostgreSQL database.
- Schema migrations are wrapped in transactions (PostgreSQL supports transactional DDL).
- Each EF Core migration is reversible via `dotnet ef database update <previous-migration>`.
- The data migration script is idempotent -- safe to re-run.

---

---

## 11. Implementation Phases

### Phase 2: Schema and Foundation

**Goal**: Database schema supports multi-channel; ChannelRegistry exists; no behavioral changes.

**Files to change**:
- `src/NoMercyBot.Database/Models/Channel.cs` -- Merge ChannelInfo fields, add `IsOnboarded`, `BotJoinedAt`, `OverlayToken`, `DeletedAt`
- `src/NoMercyBot.Database/Models/ChannelModerator.cs` -- Add `Role` ("moderator"/"lead_moderator"), `GrantedAt`, `GrantedBy`, `DeletedAt`
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

---

## 12. Risk Register

| # | Risk | Probability | Impact | Mitigation |
|---|------|-------------|--------|------------|
| 1 | **PostgreSQL migration complexity.** Moving from SQLite to PostgreSQL in Phase 2 adds risk to the schema restructuring phase. | Medium | Medium | Migrate to PostgreSQL BEFORE schema changes so there's only one set of migrations. Keep SQLite as local dev fallback. Test migration against a copy of production data. |
| 2 | **Twitch EventSub subscription limits.** Twitch allows max 10,000 subscriptions per client ID. Each channel needs ~30+ subscriptions. This limits scaling to ~300 channels. | Medium | High | Monitor subscription count. Prioritize essential events. For large scale, apply for increased limits or use Twitch Conduit (sharding). |
| 3 | **OAuth grant token refresh cascade failure.** If Twitch has an outage, all channel grant token refreshes fail simultaneously, potentially leaving all channels unable to operate. | Low | High | Implement exponential backoff per-channel. Cache last-known-good grant tokens. Alert on refresh failures but do not remove channels. |
| 4 | **Memory growth with many channels.** Each ChannelContext holds command registries, shoutout queues, session chatters, and emote caches. 100+ channels could be significant. | Medium | Medium | Profile memory usage. Implement lazy loading of ChannelContext (only load when channel is live). Evict idle channel contexts after configurable timeout. |
| 5 | **Cross-channel data leakage.** A bug in channel scoping could expose one channel's data to another channel's owner. | Low | Critical | Comprehensive integration tests that verify channel isolation. The ChannelAuthorizationFilter is the single enforcement point -- it must be thoroughly tested. |
| 6 | **Backward compatibility breaks the frontend.** Moving routes could break the existing dashboard. | Medium | Medium | The backward compatibility middleware provides a grace period. The frontend migration can happen incrementally. |
| 7 | **Roslyn script compilation time with many channels.** Scripts are compiled once but registered per-channel. Registration is O(n) per channel. | Low | Low | Scripts are compiled once and the IBotCommand objects are reused. Registration is just adding to a dictionary -- very fast. |
| 8 | **Spotify API polling rate limits.** Spotify allows 180 requests/minute. At 3s polling per channel, each live channel uses ~20 req/min. More than ~9 concurrent live channels hit the limit. | Medium | Medium | Dynamically increase poll interval as live channel count grows (e.g., 5s for 10+ channels, 10s for 20+). Only poll channels that are live. |
| 9 | **IRC connection to many channels.** TwitchLib IRC may struggle with 100+ channel joins on a single connection. | Medium | Medium | Twitch recommends max 20 JOIN/10s and max 500 channels per connection. For scale, open multiple IRC connections. |
| 10 | **Data migration corrupts existing data.** The Phase 2 data migration script has a bug that incorrectly assigns BroadcasterId values. | Low | Critical | Full database backup before migration. Migration is tested against a copy of production data first. Migration script is idempotent. |
| 11 | **Static field residue.** Some code path still references a static field (TwitchConfig._service, SpotifyConfig._service) for a per-channel operation, silently using the wrong channel's credentials. | Medium | High | Comprehensive grep for all static field usages after Phase 3. Add Roslyn analyzer or code review checklist. Consider making static fields `[Obsolete]` with error severity. |
| 12 | **ChatMessage constructor creates raw AppDbContext.** The `ChatMessage` constructor (line 136-141) and `ChatMention` constructor (line 26) create `new AppDbContext()` directly, bypassing DI. | High | Medium | Refactor these constructors to not access the database directly. Pass the required data as parameters instead of looking it up in the constructor. This is a pre-existing tech debt that becomes more dangerous with multi-channel. |

---

---

## 13. Genericization Audit -- Making the Bot Platform-Ready

### 13.1 Spotify Integration -- Replace the Discord Session Token Hack

**Current hack**: The bot obtains Spotify access tokens by storing a Discord user's web session token (`_DiscordSessionToken` config key) and calling an **undocumented Discord API endpoint**:
```
GET /api/v9/users/@me/connections/spotify/{spotifyUserId}/access-token
```
This endpoint is not in Discord's public API docs. It could break at any time, it's a security risk (storing Discord session tokens), and it's not portable to other broadcasters.

**Files involved**:
- `src/NoMercyBot.Services/Discord/DiscordApiService.cs` lines 40-73 (`GetSpotifyToken()`)
- `src/NoMercyBot.Services/Spotify/SpotifyWebsocketService.cs` lines 47-49 (reads `_DiscordSessionToken`)
- `src/NoMercyBot.Services/Spotify/SpotifyApiService.cs` lines 320-353 (uses the hacked token)

**Replacement**: Use the official Spotify Authorization Code Flow with PKCE:
1. Broadcaster clicks "Connect Spotify" in the dashboard
2. Redirected to Spotify OAuth with scopes: `user-read-playback-state`, `user-modify-playback-state`, `user-read-currently-playing`, `playlist-modify-public`, `playlist-modify-private`
3. Callback stores access token + refresh token in `Service(Name="Spotify", BroadcasterId=channelId)`
4. `TokenRefreshService` auto-refreshes the Spotify token before expiry
5. Remove all Discord session token hack code

### 13.2 Music Provider Abstraction -- Preparing for YouTube Music

To support multiple music providers (Spotify now, YouTube Music later), introduce an `IMusicProvider` interface:

```
IMusicProvider
  - string Name { get; }  // "spotify", "youtube-music"
  - Task<MusicTrack?> GetCurrentlyPlaying(string broadcasterId)
  - Task<bool> AddToQueue(string broadcasterId, string trackUri)
  - Task<MusicQueue> GetQueue(string broadcasterId)
  - Task<bool> SkipTrack(string broadcasterId)
  - Task<bool> Pause(string broadcasterId)
  - Task<bool> Resume(string broadcasterId)
  - Task<bool> SetVolume(string broadcasterId, int volume)
  - Task<MusicTrack?> SearchTrack(string query)
  - Task<MusicTrack?> GetTrack(string trackId)
```

**Universal music DTOs**:
```
MusicTrack
  - Id: string
  - Name: string
  - Artist: string
  - Album: string
  - DurationMs: int
  - ImageUrl: string?
  - Provider: string
  - ProviderUri: string  // "spotify:track:xxx" or YouTube URL

MusicQueue
  - CurrentlyPlaying: MusicTrack?
  - Queue: List<MusicTrack>
```

**API routes**: `/api/channels/{channelId}/music/currently-playing`, `/api/channels/{channelId}/music/queue`, `/api/channels/{channelId}/music/skip`, etc. The provider is determined by what the broadcaster has connected. Old Spotify-specific routes remain as aliases.

**Command changes**: `!sr`, `!song`, `!skip`, `!volume`, `!banger`, `!playlist`, `!songhistory` all use `IMusicProvider` instead of `SpotifyApiService` directly. The command resolves the active music provider for the channel from the ChannelRegistry.

### 13.3 Channel-Specific Commands That Need Genericization

These commands contain hardcoded references to the current broadcaster and must be made generic:

| Command | File | Issue | Fix |
|---------|------|-------|-----|
| **Raid** | `commands/Raid.cs:112,118` | Hardcoded `stoney90Hmmm` emote and "Big bird raid" text | Make raid message configurable per-channel via `Channel.RaidTemplate` column or use generic "RAID!" messages |
| **Rigged** | `commands/Rigged.cs:91-108,159-162` | Hardcoded "Stoney", "Big Bird" in templates + username check for `stoney_eagle` | Replace hardcoded name with `{streamer}` placeholder. Replace username check with `ctx.Message.Broadcaster.Username` comparison |
| **StoneyAi** | `commands/StoneyAi.cs` (entire file) | Entire command is about "Stoney" | Rename to `!streamerai` or `!botai`. Replace all "Stoney"/"Big Bird" with `{streamer}` placeholder |
| **Trial** | `commands/Trial.cs:22` | "Stoney" in one template | Replace with `{streamer}` placeholder |
| **Hug** | `commands/Hug.cs:86,112,114,141,201-202` | "Big Bird" references + hardcoded `nomercybot`/`nomercy_bot` bot name checks | Replace "Big Bird" with `{streamer}`. Replace hardcoded bot name with dynamic lookup from `TwitchChatService._botUserName` |
| **Auction** | `commands/Auction.cs:11-30` | "NoMercy Auction House", "Big Bird" | Replace with `{streamer}'s Auction House` or make configurable |
| **Detective** | `commands/Detective.cs:11` | "The name's NoMercy" | Replace with `{botname}` placeholder |
| **Project** | `commands/Project.cs:21` | Hardcoded "NoMercy TV" project description | Make configurable per-channel or remove/generalize |

**Template placeholder system**: All templates should support these placeholders:
- `{name}` -- the user who invoked the command (already used)
- `{target}` -- the command target (already used)
- `{streamer}` -- the broadcaster's display name (NEW)
- `{botname}` -- the bot's display name (NEW)
- `{channel}` -- the channel name (NEW)

### 13.4 Claude Command -- REMOVED

The Claude command, ClaudeSessionBridge, and ClaudeIpcService are removed entirely from the multi-channel platform. They are single-machine developer tools that spawn CLI processes and interact with a local git repo. See Section 5.2.13 for details.

### 13.5 Reward GUIDs -- Hardcoded to One Channel

All reward scripts have hardcoded `RewardId` GUIDs that match specific Twitch channel point rewards on one channel:

| Reward | File | GUID |
|--------|------|------|
| Song Request | `rewards/Song.cs:26` | `e67ad9d2-dfe8-4d2f-a15e-30cfded977bd` |
| TTS | `rewards/Tts.cs:24` | `e8168189-8d2c-41fb-b8f4-2785b083a35e` |
| Voice Swap | `rewards/VoiceSwap.cs:19` | `a0aaddc9-36d7-4c30-bd39-5c1044e5f57d` |
| Lucky Feather | `rewards/LuckyFeather.cs:18` | `29c1ea38-96ff-4548-9bbf-ec0b665344c0` |
| DJ Voice | `rewards/DjVoice.cs:20` | `862b9490-b9bc-44f4-b50e-0c454ee3f09d` |
| BSOD | `rewards/Bsod.cs:28` | `67b5638d-e523-4b53-81d7-68812f60889e` |

**Fix**: Rewards should be matched by `RewardTitle` (string) instead of GUID. When a broadcaster creates a channel point reward on Twitch with a matching title (e.g., "Song Request"), the bot automatically links it. The `IReward.RewardId` property becomes `IReward.RewardTitle` and the reward loader matches by title instead of GUID.

Alternatively, provide a dashboard UI where the broadcaster maps their Twitch rewards to bot reward handlers.

### 13.6 Direct Database Context Instantiation

These bypass DI and will cause issues with multi-channel scoping:

| File | Line | Context |
|------|------|---------|
| `Models/ChatMessage/ChatMessage.cs` | 136 | `// TODO: replace this!` -- `using AppDbContext dbContext = new();` in constructor |
| `Services/Discord/DiscordAuthService.cs` | 197 | `AppDbContext dbContext = new();` |

**Fix**: Pass required data as constructor parameters instead of querying the DB inside constructors.

### 13.7 Implementation Priority for Genericization

| Priority | Item | Phase |
|----------|------|-------|
| **CRITICAL** | Replace Spotify Discord hack with official OAuth | Phase 2 (before multi-channel) |
| **HIGH** | Add `{streamer}` and `{botname}` template placeholders | Phase 2 |
| **HIGH** | Fix hardcoded command templates (Rigged, Hug, Raid, etc.) | Phase 2 |
| **HIGH** | Make reward matching title-based instead of GUID-based | Phase 3 |
| **HIGH** | Fix direct AppDbContext instantiation | Phase 2 |
| **MEDIUM** | Music provider abstraction (`IMusicProvider`) | Phase 3 |
| **N/A** | ~~Claude command~~ | REMOVED -- not part of multi-channel platform |
| **LOW** | Rename StoneyAi to generic name | Phase 2 |
| **LOW** | Make Project command configurable | Phase 2 |

---

---

## 14. Music Provider Architecture

### 14.1 Interface Design

```
IMusicProvider (registered per provider type)
  - string ProviderName { get; }
  - bool IsConnected(string broadcasterId)
  - Task<MusicTrack?> GetCurrentlyPlayingAsync(string broadcasterId)
  - Task<MusicQueue> GetQueueAsync(string broadcasterId)
  - Task<bool> AddToQueueAsync(string broadcasterId, string trackUri)
  - Task<MusicTrack?> SearchTrackAsync(string query)
  - Task<MusicTrack?> GetTrackAsync(string trackId)
  - Task<bool> SkipAsync(string broadcasterId)
  - Task<bool> PauseAsync(string broadcasterId)
  - Task<bool> ResumeAsync(string broadcasterId)
  - Task<bool> SetVolumeAsync(string broadcasterId, int percent)
```

### 14.2 Provider Registration

```
IMusicProviderFactory (singleton)
  - GetProvider(string broadcasterId) -> IMusicProvider?
  // Returns the active music provider for the channel
  // Looks up which provider the broadcaster connected (Spotify, YouTube Music, etc.)
```

### 14.3 API Routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/channels/{channelId}/music/now-playing` | Current track |
| GET | `/api/channels/{channelId}/music/queue` | Current queue |
| POST | `/api/channels/{channelId}/music/queue` | Add to queue (body: `{query}` or `{uri}`) |
| POST | `/api/channels/{channelId}/music/skip` | Skip track |
| POST | `/api/channels/{channelId}/music/pause` | Pause |
| POST | `/api/channels/{channelId}/music/resume` | Resume |
| PUT | `/api/channels/{channelId}/music/volume` | Set volume |
| GET | `/api/channels/{channelId}/music/search?q=...` | Search tracks |

### 14.4 Spotify Implementation

`SpotifyMusicProvider : IMusicProvider` wraps the existing `SpotifyApiService` using official Spotify OAuth tokens (Authorization Code Flow with PKCE). Playback state monitoring uses `SpotifyPollingService` (polling the official REST API), replacing the removed undocumented websocket hack entirely.

### 14.5 YouTube Music (Future)

`YouTubeMusicProvider : IMusicProvider` will be added when YouTube Music support is implemented. The `IMusicProvider` interface is designed to accommodate it without changes to commands or API routes.

---

---

---

## 15. Widget Creator System

### 15.1 Current State

Widgets are currently Roslyn scripts (`.cs` files) loaded from disk. They implement `IWidgetScript` and receive events via the `WidgetEventService` pub/sub over SignalR. The overlay is served by `WidgetOverlayController` which injects settings as global JS variables. Widget frameworks supported: Vue, React, Svelte, Angular, vanilla JS.

### 15.2 Vision

A proper widget creator that allows:
- Broadcasters to create widgets from the dashboard (no coding required for simple widgets)
- A template library of pre-built widgets (alerts, chat overlay, now playing, goals, etc.)
- Custom widgets via code editor for advanced users
- Per-channel widget instances with independent settings
- Live preview in the dashboard

### 15.3 Widget Template Library

Pre-built widgets that any broadcaster can add to their channel:

| Template | Events | Description |
|----------|--------|-------------|
| Chat Overlay | `twitch.chat.message` | Displays chat messages with emotes, badges, HTML decorations |
| Alert Box | `channel.subscribe`, `channel.cheer`, `channel.raid`, `channel.follow` | Customizable alerts for channel events |
| Now Playing | `spotify.player.state` | Shows current song with album art |
| Goal Tracker | `channel.subscribe`, `channel.cheer` | Progress bar toward a goal |
| Shoutout Card | `twitch.shoutout` | Animated shoutout display |
| TTS Visualizer | `channel.chat.message.tts` | Audio visualization during TTS |
| Cross-Channel Game | `universe.*` | Displays cross-channel universe game state (see Section 17) |

### 15.4 Widget Settings Schema

Each widget template defines a settings schema (JSON Schema). The dashboard renders a form from this schema. Broadcasters configure without code.

```
WidgetTemplate
  - Id: string (slug, e.g. "chat-overlay")
  - Name: string
  - Description: string
  - Version: string (semver)
  - Framework: string
  - SettingsSchema: JSON Schema (defines what the broadcaster can configure)
  - DefaultSettings: JSON
  - EventSubscriptions: string[] (what events this template needs)
  - SourcePath: string (path to the widget's frontend code)
```

### 15.5 Custom Widget Code Editor

For advanced users, the dashboard includes a code editor (Monaco/CodeMirror) that allows:
- Editing widget HTML/CSS/JS directly
- Live preview with simulated events
- Access to the same event system as template widgets
- Export/import widget bundles

### 15.6 Widget Instance Model

```
WidgetInstance (replaces current Widget table)
  - Id: Ulid (PK)
  - BroadcasterId: string (FK to Channel)
  - TemplateId: string? (null = fully custom widget)
  - Name: string
  - Settings: JSON (broadcaster's configuration)
  - IsEnabled: bool
  - EventSubscriptions: string[] (can override template defaults)
  - CustomCode: string? (null = use template code, non-null = custom override)
  - CreatedAt, UpdatedAt
```

---

---

## 16. Command Editor System

### 16.1 Current State

Commands are either:
- **Platform scripts**: Roslyn-compiled `.cs` files loaded from disk at startup. These implement `IBotCommand` and are shared across all channels. Compiled once, never recompiled. They have full access to all services (Spotify, OBS, TTS, Discord, database, Twitch API).
- **Database commands**: Simple text responses stored in the `Command` table. No logic, just static response strings.

There is no way to create dynamic commands with logic from the dashboard.

### 16.2 Design Principle

Users must be able to create commands that are as powerful as our platform scripts -- accessing Spotify, OBS, TTS, Discord, database, shoutouts, etc. -- without writing C#. Full Roslyn is reserved for platform scripts only (security risk: arbitrary code on our server is trivially exploitable via reflection/dynamic/assembly loading).

The solution: a **trigger + condition + action pipeline** where users compose commands from pre-built building blocks. The blocks are powerful (platform code with full service access). The user only configures and chains them.

### 16.3 Command Types

| Type | Description | Who Can Create |
|------|-------------|---------------|
| **text** | Static response with variable substitution | Moderator+ |
| **random** | Picks randomly from multiple responses | Moderator+ |
| **counter** | Increments and displays a counter | Moderator+ |
| **pipeline** | Condition + action chains with full service access | Broadcaster |

### 16.4 Pipeline Commands -- The Action System

A pipeline command is a list of steps. Each step has an optional **condition** and an **action**. Steps execute in order. If a condition fails, that step is skipped (unless `stop_on_match` is true, which halts the pipeline).

#### Available Conditions

| Condition | Parameters | Description |
|-----------|-----------|-------------|
| `user_role_is` | role (everyone/sub/vip/mod/broadcaster) | Check user's Twitch role |
| `user_role_is_not` | role | Inverse role check |
| `user_is` | username | Specific user check |
| `user_is_not` | username | Not a specific user |
| `target_is_self` | -- | User targeted themselves |
| `target_is_bot` | -- | User targeted the bot |
| `target_exists` | -- | Target user exists on Twitch |
| `args_match` | regex pattern | Command arguments match pattern |
| `args_empty` | -- | No arguments provided |
| `random_chance` | percent (1-100) | Random probability |
| `counter_gt` | counter_name, value | Counter is greater than N |
| `counter_lt` | counter_name, value | Counter is less than N |
| `cooldown_ready` | seconds | Time since last use by this user |
| `stream_is_live` | -- | Stream is currently live |
| `feature_enabled` | feature_key | Channel has feature enabled |
| `spotify_playing` | -- | Spotify is currently playing |

#### Available Actions

These are the building blocks. Each wraps a platform service with full access:

**Chat Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `reply` | message (with variables) | Send a reply in chat |
| `reply_random` | messages[] | Send one of multiple random replies |
| `announce` | message, color? | Send an announcement |
| `send_tts` | message | Send text-to-speech |
| `whisper` | username, message | Send a whisper |

**Spotify/Music Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `music_add_to_queue` | track_uri or search_query | Add a song to queue |
| `music_skip` | -- | Skip current track |
| `music_pause` | -- | Pause playback |
| `music_resume` | -- | Resume playback |
| `music_set_volume` | percent | Set volume |
| `music_get_current` | -> {track_name}, {artist}, {album} | Get current track (sets variables for next steps) |
| `music_add_to_playlist` | playlist_id | Add current track to playlist |
| `music_search` | query -> {track_name}, {artist}, {track_uri} | Search for a track |

**OBS Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `obs_switch_scene` | scene_name | Switch to a scene |
| `obs_toggle_source` | source_name, visible | Show/hide a source |
| `obs_mute` | input_name, muted | Mute/unmute an input |
| `obs_set_volume` | input_name, percent | Set input volume |
| `obs_start_stream` | -- | Start streaming |
| `obs_stop_stream` | -- | Stop streaming |

**Discord Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `discord_send` | channel_id, message | Send a message to Discord |
| `discord_assign_role` | guild_id, user_id, role_id | Assign a role |
| `discord_remove_role` | guild_id, user_id, role_id | Remove a role |

**Twitch Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `twitch_shoutout` | username | Give a shoutout |
| `twitch_raid` | username | Start a raid |
| `twitch_create_clip` | -- | Create a clip |
| `twitch_create_poll` | title, options[], duration | Create a poll |
| `twitch_create_prediction` | title, outcomes[], duration | Create a prediction |
| `twitch_timeout` | username, duration, reason? | Timeout a user |
| `twitch_ban` | username, reason? | Ban a user |
| `twitch_set_title` | title | Change stream title |
| `twitch_set_game` | game_name | Change stream game/category |
| `twitch_set_chat_mode` | mode (emote/sub/slow/follower), enabled | Toggle chat mode |

**TTS Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `tts_speak` | message, voice? | Speak text with optional voice override |
| `tts_speak_as` | message, user_id | Speak with a user's configured voice |

**Data Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `counter_increment` | counter_name, amount? | Increment a counter |
| `counter_decrement` | counter_name, amount? | Decrement a counter |
| `counter_set` | counter_name, value | Set counter to value |
| `counter_reset` | counter_name | Reset counter to 0 |
| `store_value` | key, value | Store a value in channel storage |
| `get_value` | key -> variable | Retrieve a stored value |
| `lookup_user` | username -> {user_display}, {user_id}, {follow_age}, {account_age} | Look up a Twitch user |

**Widget Actions**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `widget_event` | event_type, data | Publish event to widgets |
| `widget_alert` | message, type? | Trigger an alert overlay |
| `play_sound` | sound_url | Play a sound through widget |

**Flow Control**
| Action | Parameters | What It Does |
|--------|-----------|-------------|
| `delay` | milliseconds (max 300000) | Wait before next step |
| `stop` | -- | Stop pipeline execution |
| `refund_reward` | -- | Refund the channel point redemption (for reward pipelines) |

### 16.5 Template Variables

Available in all message strings:

| Variable | Value |
|----------|-------|
| `{user}` | Display name of who triggered the command |
| `{user_id}` | Twitch user ID of who triggered |
| `{target}` | First argument (with @ stripped) |
| `{args}` | All arguments as a string |
| `{streamer}` | Channel broadcaster's display name |
| `{botname}` | Bot's display name |
| `{channel}` | Channel name |
| `{count}` | Counter value (if counter action was used) |
| `{track_name}` | Current music track (after `music_get_current`) |
| `{artist}` | Current artist (after `music_get_current`) |
| `{track_uri}` | Track URI (after `music_search`) |
| `{random_user}` | Random active chatter |
| `{time}` | Current time |
| `{uptime}` | Stream uptime |
| `{viewers}` | Current viewer count |
| `{followers}` | Follower count |
| `{user_display}` | Looked up user's display name (after `lookup_user`) |
| `{follow_age}` | How long the looked up user has followed (after `lookup_user`) |

### 16.6 Pipeline Storage Format

Pipelines are stored as JSON in `Command.PipelineJson`:

```json
{
  "steps": [
    {
      "condition": {"type": "args_empty"},
      "action": {"type": "reply", "message": "Usage: !sr <spotify url or song name>"},
      "stop_on_match": true
    },
    {
      "action": {"type": "music_add_to_queue", "query": "{args}"}
    },
    {
      "action": {"type": "reply", "message": "Added to queue!"}
    }
  ]
}
```

### 16.7 Example: Rebuilding `!raid` as a Pipeline

```json
{
  "steps": [
    {"condition": {"type": "args_empty"}, "action": {"type": "reply", "message": "Usage: !raid <username>"}, "stop_on_match": true},
    {"action": {"type": "obs_switch_scene", "scene_name": "Ending"}},
    {"action": {"type": "twitch_raid", "username": "{target}"}},
    {"action": {"type": "announce", "message": "RAID INCOMING to {target}! Raiding in 60 seconds..."}},
    {"action": {"type": "delay", "milliseconds": 45000}},
    {"action": {"type": "reply", "message": "Raid in 15 seconds..."}},
    {"action": {"type": "delay", "milliseconds": 10000}},
    {"action": {"type": "reply", "message": "Raid in 5 seconds..."}},
    {"action": {"type": "delay", "milliseconds": 5000}},
    {"action": {"type": "reply", "message": "RAID LIVE! We're heading to {target}! Let's go!"}},
    {"action": {"type": "obs_stop_stream"}},
    {"action": {"type": "music_pause"}}
  ]
}
```

This does everything the current Roslyn `Raid.cs` does, without a single line of C#.

### 16.8 Command Model (Enhanced)

```
Command (updated)
  - Id: int (PK)
  - BroadcasterId: string (FK to Channel)
  - Name: string (max 100)
  - Permission: string (default "everyone")
  - Type: string ("text", "random", "counter", "pipeline")
  - Response: string (for text type)
  - Responses: string[] (for random type, JSON)
  - PipelineJson: string? (for pipeline type, JSON)
  - IsEnabled: bool
  - Description: string?
  - CooldownSeconds: int (default 0, 0 = no cooldown)
  - CooldownPerUser: bool (default false)
  - Aliases: string[] (alternative command names, JSON)
  - IsPlatform: bool (default false, true = shipped with bot, read-only in dashboard)
  - CreatedAt, UpdatedAt
```

### 16.9 Dashboard Command Editor

- List view: Name, Type, Permission, Cooldown, Enabled, Usage Count
- Inline enable/disable toggle
- Click to expand inline editor (not a modal)
- **For text/random/counter**: Simple form inputs with variable autocomplete
- **For pipeline**: Visual step builder
  - Each step is a card with condition (optional) and action
  - Drag-and-drop reordering
  - "Add Step" button with action picker (categorized: Chat, Music, OBS, Discord, Twitch, TTS, Data, Widget, Flow)
  - Each action shows its parameters as form fields
  - Conditions are dropdowns with parameter inputs
  - "Test Run" simulates the pipeline and shows what would happen at each step
  - "Raw JSON" toggle for advanced users
- Platform commands shown with lock icon + "Duplicate as Pipeline" button
- Search, filter by type/permission
- Bulk import/export (JSON)

### 16.10 Platform Commands -- Regular C# Classes (No Roslyn)

Platform commands are **regular C# classes** compiled with the project. No Roslyn script loading.

- Implement `IBotCommand` interface, registered via DI at startup
- Full IDE support (intellisense, refactoring, debugging, testing)
- Compiled with `dotnet build`, not at runtime
- Registered in all channels' command registries during onboarding
- Shown as read-only in the dashboard with a "Platform" badge
- Cannot be edited by broadcasters
- CAN be "duplicated" into a pipeline command that approximates the behavior

**Why not Roslyn**: Roslyn script loading was a development convenience. For a production platform, regular compiled classes are faster, testable, type-safe, and don't have the security surface of runtime compilation. The `CommandScriptLoader` Roslyn system is removed entirely.

### 16.11 Reward Handlers Use the Same Pipeline System

The reward system uses the same action pipeline. When a channel point reward is redeemed, the pipeline executes. This replaces hardcoded Roslyn reward scripts for user-created rewards.

Platform reward scripts (Song, TTS, Lucky Feather, etc.) remain as Roslyn for now. Users can create custom reward handlers as pipelines with all the same actions available.

### 16.12 Action Registry -- Extensibility via DI

All actions are registered via DI through an `ICommandActionRegistry`:

```csharp
public interface ICommandAction
{
    string Type { get; } // e.g., "music_skip"
    string Category { get; } // e.g., "Music"
    string Description { get; }
    ActionParameterSchema[] Parameters { get; }
    Task<ActionResult> ExecuteAsync(ActionContext context);
}
```

New actions are added by implementing `ICommandAction` and registering in DI. The dashboard auto-discovers available actions from the registry via an API endpoint (`GET /api/actions`). This follows the same provider pattern as `ITtsProvider` and `IMusicProvider`.

When the Universe system ships (post-MVP), it adds Universe-specific actions (`universe_update_state`, `universe_broadcast`, `universe_get_state`) to the registry. Existing pipeline commands can use them immediately -- no changes to the pipeline engine.

Actions that require specific OAuth scopes (e.g., `twitch_raid` needs `channel:manage:raids`) check the `ChannelFeatures` table before executing. If the feature isn't enabled, the action returns an error message telling the user to enable the feature from the dashboard.

---

## 17. Cross-Channel Universe System (POST-MVP)

**This entire section is deferred to post-MVP.** The multi-channel platform ships without the Universe system. The Lucky Feather stays as a hardcoded platform reward initially. The Universe system is designed here so the architecture doesn't prevent it, but implementation happens after the core platform is stable with real users.

### 17.1 Concept

A **Universe** is a shared game or event system that spans multiple channels. There are two levels:

- **Universe Template** -- The blueprint/plugin (e.g., "Lucky Feather"). Created by any user. Published to the marketplace. Defines the game logic, state schema, triggers.
- **Universe Instance** -- A running copy of a template, created by a broadcaster. The broadcaster who creates the instance **controls who can join**. They invite specific channels. It's not "install plugin and you're in a global game" -- it's "install plugin, create YOUR instance, invite YOUR friends."

Example: StoneyEagle installs the Lucky Feather template and creates an instance. He invites Bamo16 and kani_dev to join. Now the feather travels between those 3 channels only. Meanwhile, another group of streamers creates their OWN Lucky Feather instance with their own channels. The two instances are completely independent -- separate state, separate participants.

### 17.2 Design Principles

1. **Generic** -- Not hardcoded to any specific game. The universe system is a framework.
2. **Versioned** -- Universe templates have semver versions. Updates don't break running instances.
3. **Instance-based** -- Each install creates an independent instance. One template, many instances.
4. **Owner-controlled** -- The instance creator controls who can join. Channels must be invited and accept.
5. **User-created** -- Any platform user can create a universe template.
6. **Sandboxed** -- Universe logic cannot access channel-specific data (tokens, settings) outside its scope.

### 17.3 Data Model

#### Universe Template (the blueprint/plugin)

```
UniverseTemplates
  - Id: Ulid (PK)
  - Slug: string (unique, URL-friendly, e.g. "lucky-feather")
  - Name: string ("Lucky Feather")
  - Description: string
  - Version: string (semver, e.g. "1.2.0")
  - CreatorUserId: string (FK to User -- who made this)
  - IsPublished: bool (false = draft, only creator can test)
  - IsApproved: bool (false = pending review, true = visible in marketplace)
  - StateSchema: JSON Schema (defines the shape of the shared state)
  - DefaultState: JSON (initial state when an instance is created)
  - RewardTriggers: JSON (what reward names/actions activate logic)
  - CommandTriggers: JSON (what commands activate logic)
  - EventHandlerScript: string (sandboxed logic)
  - WidgetTemplateId: string? (optional widget for displaying state)
  - CreatedAt, UpdatedAt
```

#### Universe Template Versions

```
UniverseTemplateVersions
  - Id: Ulid (PK)
  - TemplateId: Ulid (FK to UniverseTemplates)
  - Version: string (semver)
  - StateSchema, DefaultState, RewardTriggers, CommandTriggers, EventHandlerScript: (same as template)
  - ChangeLog: string
  - PublishedAt: DateTime
  - CreatedAt
```

#### Universe Instance (a running copy, owned by a broadcaster)

```
UniverseInstances
  - Id: Ulid (PK)
  - TemplateId: Ulid (FK to UniverseTemplates)
  - TemplateVersion: string (pinned version -- owner controls when to upgrade)
  - OwnerBroadcasterId: string (FK to Channel -- who created this instance)
  - Name: string (custom name, e.g. "Stoney & Friends Feather")
  - JoinPolicy: string ("invite_only", "request_to_join", "open")
  - IsActive: bool
  - CreatedAt, UpdatedAt
```

#### Universe Instance Membership (who's in this instance)

```
UniverseInstanceMembers
  - Id: Ulid (PK)
  - InstanceId: Ulid (FK to UniverseInstances)
  - BroadcasterId: string (FK to Channel)
  - Status: string ("invited", "accepted", "declined", "removed")
  - InvitedBy: string (FK to User)
  - InvitedAt: DateTime
  - AcceptedAt: DateTime?
  - ChannelState: JSON (this channel's local state within the instance)
  - CreatedAt, UpdatedAt
  - Unique: (InstanceId, BroadcasterId)
```

#### Universe Instance State (shared across all members)

```
UniverseInstanceState
  - Id: Ulid (PK)
  - InstanceId: Ulid (FK to UniverseInstances, unique)
  - State: JSON (the shared state -- e.g., who holds the feather)
  - LastModifiedBy: string (broadcasterId of last channel that changed state)
  - LastModifiedAt: DateTime
  - UpdatedAt
```

#### Universe Event Log

```
UniverseEvents
  - Id: Ulid (PK)
  - InstanceId: Ulid (FK to UniverseInstances)
  - BroadcasterId: string (FK to Channel -- which channel triggered this)
  - UserId: string (FK to User -- which user triggered this)
  - EventType: string (e.g. "steal", "transfer", "reset")
  - EventData: JSON
  - CreatedAt
  - Index: (InstanceId, CreatedAt)
```

### 17.4 How It Works -- Flow

1. **Template creator publishes "Lucky Feather v1.0"** to the marketplace:
   - Defines state schema, default state, reward triggers, event handler script
   - Gets approved by platform moderators

2. **StoneyEagle installs the template**:
   - Browses marketplace, clicks "Install" on Lucky Feather
   - Creates a **new instance** named "Stoney & Friends Feather"
   - Chooses join policy: "Invite Only"
   - StoneyEagle is automatically the first member (status: "accepted")

3. **StoneyEagle invites Bamo16 and kani_dev**:
   - From dashboard: Universes > "Stoney & Friends Feather" > Members > Invite
   - Searches for Bamo16, sends invite
   - Bamo16 sees the invite in their dashboard, accepts
   - kani_dev accepts too
   - Now 3 channels are members of this instance

4. **Viewer in StoneyEagle's channel redeems "Lucky Feather"**:
   - Bot detects reward redemption
   - Checks: is this reward name a trigger for any universe instance this channel is a member of?
   - Yes -- "Stoney & Friends Feather" instance, trigger: `onRewardRedeemed`
   - Loads the instance's shared state from `UniverseInstanceState`
   - Executes the sandboxed event handler with context
   - Bot atomically updates `UniverseInstanceState`
   - Bot sends chat message in StoneyEagle's channel
   - Bot publishes widget event to ALL members of this instance (Stoney, Bamo, kani)
   - Overlays in all three channels update

5. **Viewer in Bamo16's channel steals it next**: Same flow, same instance.

6. **Meanwhile**: Another group of streamers can install the same Lucky Feather template and create their OWN instance with their OWN members. Completely independent state.

### 17.5 Sandboxed Event Handler Scripts

Universe logic runs in a **sandboxed environment** with NO access to:
- Database directly
- Service credentials / tokens
- Other channels' private data
- File system
- Network

The script receives a **context object** and returns an **action result**:

**Input context:**
```
UniverseEventContext
  - SharedState: dynamic (the universe's global state)
  - ChannelState: dynamic (this channel's local state)
  - TriggerType: string ("reward", "command", "timer")
  - User: { Id, DisplayName, Color }
  - Channel: { Id, Name, DisplayName }
  - Input: string? (reward user input or command arguments)
  - AllParticipatingChannels: { Id, Name, DisplayName }[] (read-only list)
```

**Output result:**
```
UniverseActionResult
  - SharedState: dynamic (updated global state, or null for no change)
  - ChannelState: dynamic (updated channel state, or null for no change)
  - ChatMessage: string? (message to send in the triggering channel)
  - BroadcastChatMessage: string? (message to send in ALL participating channels)
  - WidgetEvent: { type, data }? (event to publish to all universe widgets)
  - RefundReward: bool (if true, refund the channel point redemption)
  - LogEvent: { type, data }? (event to record in UniverseEvents)
```

**Execution environment:** Either:
- A restricted C# Roslyn script with limited imports (no System.IO, no System.Net, no reflection)
- Or a lightweight expression/rules engine (simpler but less flexible)

Recommendation: Start with Roslyn with a strict allowlist of namespaces. The script is compiled once per version and cached.

### 17.6 Universe Marketplace

- Browse published & approved templates
- Search by name, tags, popularity, active instance count
- View: description, version history, changelog, how many instances are running
- "Install" button creates a new instance (not joining someone else's)
- Version management: see current version, available upgrades, changelog per instance
- Creator tools: create template, edit, publish, view analytics

### 17.7 Template Lifecycle

| State | Visibility | Can Install |
|-------|-----------|-------------|
| **Draft** | Creator only | Creator only (for testing) |
| **Published** | Everyone can see | Nobody (awaiting approval) |
| **Approved** | Everyone can see | Anyone can install and create instances |
| **Deprecated** | Existing instances only | Nobody new |
| **Archived** | Nobody | Nobody (frozen, existing instances keep running) |

### 17.7.1 Instance Access Control

| Join Policy | How Channels Join |
|-------------|-------------------|
| **Invite Only** | Instance owner sends invites. Channel must accept. Default. |
| **Request to Join** | Any channel can request. Instance owner approves/denies. |
| **Open** | Any channel can join immediately. Owner can still kick. |

Instance owner can always:
- Invite channels
- Remove channels
- Transfer ownership to another member
- Close the instance (soft delete, state preserved for 90 days)

### 17.8 Lucky Feather Migration

The current hardcoded Lucky Feather system (`LuckyFeather.cs`, `LuckyFeatherChange.cs`, `LuckyFeatherTimerService.cs`, `LuckyFeatherWidget.cs`) becomes the **first published universe**:
- Extract the logic into a universe event handler script
- Extract the widget into a universe widget template
- Remove the hardcoded reward GUID -- match by reward title instead
- Remove `LuckyFeatherTimerService` -- timer logic becomes part of the universe framework (universes can define timer-based state transitions)
- Existing feather state migrates to `UniverseState`

### 17.9 Implementation Phase

The Universe system is a **Phase 5+** feature. It depends on:
- Multi-channel being fully operational (Phase 5)
- Widget creator system (Section 16)
- Reward title-based matching (Section 13.5)

---

---

## 18. Provider Integration Inventory -- What Each Service Can Actually Do

### 18.1 Twitch Helix API -- Full Integration Plan

The Twitch API is the core of the platform. We currently use a subset. Here's what we should integrate:

#### Currently Integrated
| Category | Endpoints | Status |
|----------|-----------|--------|
| Chat | Send Chat Message, Send Announcement, Send Shoutout | Working |
| Channel Points | Get/Create/Update Custom Rewards, Get/Update Redemption Status | Working |
| Users | Get Users | Working |
| Channels | Get Channel Information | Working |
| Streams | Get Streams | Working |
| Raids | Start a Raid | Working |
| Moderation | Get Moderators, Get VIPs, Ban/Unban, Delete Chat Messages | Partial |
| EventSub | Create/Delete/Get Subscriptions | Working |

#### Should Integrate
| Category | Endpoints | Why |
|----------|-----------|-----|
| **Ads** | Start Commercial, Get/Snooze Ad Schedule | Let broadcasters manage ads from dashboard |
| **Bits** | Get Bits Leaderboard, Get Cheermotes | Leaderboard widget, cheer alerts |
| **Clips** | Create Clip, Get Clips | Auto-clip highlights, clip commands |
| **Polls** | Create/Get/End Poll | Chat-driven polls, `!poll` command |
| **Predictions** | Create/Get/End Prediction | Chat-driven predictions, `!predict` command |
| **Schedule** | Get/Update Stream Schedule | Display schedule in dashboard/widget |
| **Goals** | Get Creator Goals | Goal progress widget |
| **Hype Train** | Get Hype Train Status | Hype train alerts and widget |
| **Subscriptions** | Get Broadcaster Subscriptions | Sub count widget, sub alerts |
| **Chat Settings** | Get/Update Chat Settings | Dashboard control (emote-only, slow mode, etc.) |
| **Shield Mode** | Get/Update Shield Mode | Emergency dashboard toggle |
| **Whispers** | Send Whisper | Bot DM notifications |
| **Chat Color** | Get/Update User Chat Color | Bot appearance customization |
| **Channel Editors** | Get Channel Editors | Dashboard access auto-detection |
| **Followers** | Get Channel Followers | Follower alerts, follower count widget |
| **Moderation** | Warn User, AutoMod Settings, Blocked Terms, Suspicious Users | Full mod dashboard |
| **Search** | Search Channels, Search Categories | Channel discovery for cross-channel features |

#### Not Integrating
| Category | Why Not |
|----------|---------|
| Analytics | Extension-only, not relevant for a chat bot |
| Conduits | Complex EventSub transport, not needed with websocket |
| Extensions | Building a bot, not a Twitch extension |
| Guest Star | Niche feature, low priority |
| Entitlements/Drops | Game developer feature, not relevant |
| Teams | Read-only, low value |
| Videos/VODs | Out of scope for a live bot |
| Tags | Deprecated by Twitch |

### 18.2 Spotify Web API -- Legitimate Integration (Replacing the Hack)

**CRITICAL: The current integration uses an undocumented Discord API to steal Spotify tokens. This is completely replaced.**

#### Authentication
- **Flow**: Spotify Authorization Code Flow with PKCE
- **Scopes needed**: `user-read-playback-state`, `user-modify-playback-state`, `user-read-currently-playing`, `playlist-modify-public`, `playlist-modify-private`, `user-library-read`
- **Token storage**: `Service(Name="Spotify", BroadcasterId=channelId)` with access + refresh tokens
- **Auto-refresh**: `TokenRefreshService` handles Spotify token refresh

#### February 2026 API Changes Impact
Spotify made breaking changes in Feb 2026 that affect us:
- **Search limit reduced** from 50 to 10 results -- `!sr` search needs pagination for better results
- **Batch endpoints removed** -- Must fetch tracks individually (minor perf impact)
- **Dev Mode restrictions**: Premium required for app owner, max 5 users per app in dev mode
- **IMPORTANT**: We need **Extended Quota Mode** approval from Spotify to support more than 5 broadcasters. Apply at Spotify Developer Dashboard.

#### Endpoints to Integrate
| Category | Endpoint | Usage |
|----------|----------|-------|
| **Player** | Get Playback State | Polling service: track changes, play/pause state |
| **Player** | Get Currently Playing Track | `!song` command, now-playing widget |
| **Player** | Pause/Resume Playback | `!pause`/`!resume` commands, raid auto-pause |
| **Player** | Skip to Next | `!skip` command |
| **Player** | Set Playback Volume | `!volume` command |
| **Player** | Add Item to Queue | `!sr` command |
| **Player** | Get Queue | Duplicate song detection, `!queue` command |
| **Player** | Get Recently Played | `!songhistory` command |
| **Tracks** | Get Track | Track metadata for `!sr`, `!song` |
| **Search** | Search for Item | `!sr` song name search |
| **Playlists** | Add Items to Playlist | `!banger` command (add to playlist) |
| **Playlists** | Get Playlist | `!playlist` command |
| **Library** | Check Saved | Check if user has track saved |

#### Playback Monitoring -- Polling, Not Websocket
- **REMOVE**: `SpotifyWebsocketService` (uses undocumented `dealer.spotify.com`)
- **REMOVE**: `DiscordApiService.GetSpotifyToken()` (undocumented Discord endpoint)
- **REPLACE WITH**: `SpotifyPollingService` -- polls `GET /me/player` every 3-5 seconds
- Only polls when stream is live (lifecycle-aware via EventSub `stream.online`/`stream.offline`)
- Publishes `spotify.player.state` events to widgets on state change
- Rate limit: Spotify allows 180 req/min. At 3s polling, one channel = ~20 req/min. Supports ~9 concurrent live channels. For more, dynamically increase poll interval.

#### Files to Remove
- `src/NoMercyBot.Services/Spotify/SpotifyWebsocketService.cs`
- `src/NoMercyBot.Services/Discord/DiscordApiService.cs` (`GetSpotifyToken()` method)
- All references to `_DiscordSessionToken`

### 18.3 Discord API -- Bot Integration

**Current state**: Discord is only used as a hack to get Spotify tokens. The actual Discord bot features are not implemented.

**New integration**: A proper Discord bot that provides value to streamers' Discord servers.

#### Authentication
- **Flow**: Discord Bot Token (platform-managed bot, not per-user)
- **Bot invite URL**: Generated with required permissions, broadcaster adds the bot to their server
- **Permissions needed**: `SEND_MESSAGES`, `MANAGE_ROLES`, `EMBED_LINKS`, `VIEW_CHANNEL`
- **Gateway Intents**: `GUILDS`, `GUILD_MEMBERS` (for role management)

#### Features to Integrate

**Live Stream Notifications**
- When `stream.online` EventSub fires for a channel:
  - Bot sends a rich embed to the configured Discord announcement channel
  - Embed includes: stream title, game, thumbnail, go-live link
  - Configurable per-server: which channel to post in, custom message template, @mention role
- When `stream.offline` fires:
  - Optionally update the embed to show "Stream ended" with duration
- Broadcaster configures this in dashboard: pick server -> pick channel -> set template -> enable

**Server Access Control**

A Discord server may not want random broadcasters spamming live notifications. Access requires BOTH:
1. The broadcaster must be a **member** of the Discord server (verified via `Get Guild Member`)
2. The server must have the bot added (server admin adds it via invite link)
3. A **server admin or moderator** must **approve** the broadcaster to post in their server

**Approval flow**:
1. Broadcaster clicks "Add Server" in their Discord integration dashboard
2. They select a server they're a member of (bot must also be in it)
3. The platform sends a permission request to the server's designated approval channel (or the server owner via DM)
4. A server admin reacts with ✅ to approve, or uses a `/nomercybot approve @user` slash command
5. Only after approval can the broadcaster configure notification channel, role, and template

**Configuration Model** includes approval state:

```
DiscordServerAuthorization (new table)
  - Id: int (PK, identity)
  - BroadcasterId: string (FK to Channel) -- the streamer requesting access
  - GuildId: string -- Discord server ID
  - GuildName: string
  - Status: string ("pending", "approved", "denied", "revoked")
  - ApprovedBy: string? -- Discord user ID of who approved
  - ApprovedAt: DateTime?
  - CreatedAt, UpdatedAt
  - Unique: (BroadcasterId, GuildId)
```

**Live Role Assignment/Removal**
- When broadcaster goes live:
  - Bot assigns a configurable role to the broadcaster's Discord member in ALL **approved** servers
  - Role name is configurable per-server (e.g., "LIVE", "Streaming", "On Air")
- When broadcaster goes offline:
  - Bot removes the role
- Requirements: bot has `MANAGE_ROLES` permission, the role is below the bot's highest role, AND the broadcaster is approved for that server
- Dashboard: broadcaster picks which role to use (dropdown of server roles) -- only for approved servers

**Discord Endpoints Used**
| Feature | Discord Endpoint | Description |
|---------|-----------------|-------------|
| Send notification | Create Message (with embed) | Rich embed with stream info in announcement channel |
| Assign live role | Add Guild Member Role | On `stream.online` |
| Remove live role | Remove Guild Member Role | On `stream.offline` |
| Get server info | Get Guild | Verify bot permissions |
| List roles | Get Guild Roles | Dashboard role picker |
| List channels | Get Guild Channels | Dashboard channel picker |
| Get member | Get Guild Member | Find broadcaster in server |
| List servers | Get Current User Guilds | Show which servers bot is in |

**Per-Server Configuration** (only for approved servers):
```
DiscordServerConfig (stored in Configuration table as JSON, BroadcasterId set)
  - Servers: [
      {
        GuildId: string,
        GuildName: string,
        AuthorizationId: int (FK to DiscordServerAuthorization),
        NotificationChannelId: string?,
        NotificationTemplate: string?,
        MentionRoleId: string?,
        LiveRoleId: string?,
        LiveRoleMemberId: string? (broadcaster's Discord user ID in this server),
        IsEnabled: bool
      }
    ]
```

#### Not Integrating (Discord)
| Feature | Why Not |
|---------|---------|
| Full moderation (bans, kicks) | Out of scope -- we're a Twitch bot |
| Voice channels | Not relevant |
| Scheduled Events | Low priority, could add later |
| Forum/thread management | Not relevant |
| DM functionality | Privacy concerns |

### 18.4 OBS WebSocket -- Remote Control Integration

**Problem**: OBS runs locally on the broadcaster's machine. A hosted platform cannot directly connect to each broadcaster's OBS instance.

#### Architecture: Relay via Dashboard

The dashboard (running in the broadcaster's browser) acts as a bridge:
1. Platform API sends OBS commands via SignalR to the broadcaster's dashboard
2. Dashboard connects to OBS WebSocket locally (`ws://localhost:4455`)
3. Dashboard relays the command and returns the response
4. Flow: `Platform API -> SignalR -> Dashboard -> OBS WebSocket -> OBS`
5. **No direct server-to-OBS connection** -- works because the browser has localhost access

This means OBS features only work when the broadcaster's dashboard is open. This is acceptable because:
- Broadcasters typically have the dashboard open while streaming
- OBS control from the dashboard is the primary use case
- Automated OBS actions (raid scene switch) trigger via SignalR to the open dashboard

#### Features to Integrate

**Scene Management**
| Feature | OBS Request | Usage |
|---------|-------------|-------|
| Switch scene | SetCurrentProgramScene | Raid ending scene, BRB, starting soon |
| Get current scene | GetCurrentProgramScene | Dashboard indicator |
| List scenes | GetSceneList | Dashboard scene picker |

**Stream/Record Control**
| Feature | OBS Request | Usage |
|---------|-------------|-------|
| Start/Stop stream | StartStream / StopStream | Dashboard control, auto-stop after raid |
| Get stream status | GetStreamStatus | Dashboard indicator |
| Start/Stop recording | StartRecord / StopRecord | Dashboard control |

**Source Control**
| Feature | OBS Request | Usage |
|---------|-------------|-------|
| Show/hide source | SetSceneItemEnabled | Toggle overlays, alerts, camera |
| List sources | GetSceneItemList | Dashboard source manager |
| Mute/unmute audio | SetInputMute / ToggleInputMute | Audio control |
| Set volume | SetInputVolume | Audio mixing |

**Media Playback**
| Feature | OBS Request | Usage |
|---------|-------------|-------|
| Play/stop media | TriggerMediaInputAction | Sound alerts, video playback |

**Screenshot/Preview**
| Feature | OBS Request | Usage |
|---------|-------------|-------|
| Take screenshot | GetSourceScreenshot | Dashboard live preview thumbnail |

#### OBS Events to Forward to Platform
| Event | Usage |
|-------|-------|
| StreamStateChanged | Update dashboard, trigger Discord notifications |
| RecordStateChanged | Update dashboard |
| CurrentProgramSceneChanged | Update dashboard, log scene changes |

#### Not Integrating (OBS)
| Feature | Why Not |
|---------|---------|
| Profile/collection management | Dangerous, could break OBS setup |
| Filter management | Too granular, low value |
| Hotkey triggering | Security risk |
| Virtual camera | Niche |
| Video settings changes | Dangerous |

#### Phase 6+ Enhancement: OBS Plugin Relay

The dashboard relay approach has a weakness: if the broadcaster closes their browser, OBS automation stops (raid scene switch, auto-stop stream). For Phase 6+, build a lightweight OBS plugin:

1. **OBS Lua/Python plugin** that connects outbound to the platform's WebSocket endpoint
2. The plugin authenticates using the channel's overlay token (same as widget auth)
3. Platform sends OBS commands through this reverse tunnel
4. Plugin executes them locally and returns results
5. Works even when the dashboard is closed -- as long as OBS is running

This is a separate installable that the broadcaster downloads from the dashboard. The dashboard relay remains as the zero-install fallback.

---

---

## 19. Widget & Overlay Authentication

### 19.1 The Problem

OBS browser sources and overlay URLs cannot perform OAuth login flows. They just load a URL. We need a way to authenticate widget SignalR connections without requiring the user to log in from inside OBS.

### 19.2 Channel Secret Token

Each channel gets a unique secret token generated on onboarding:

```
Channel (additional column)
  - OverlayToken: string (UUID v4, unique, indexed)
```

The overlay URL includes this token: `https://bot.nomercy.tv/overlay/widgets/{widgetId}?token={overlayToken}`

### 19.3 Authentication Flow

1. Broadcaster copies their overlay URL from the dashboard (includes the token)
2. OBS browser source loads the URL
3. Widget frontend connects to the SignalR hub with the token as a query parameter
4. `WidgetHub.JoinWidgetGroup()` validates the token:
   - Look up `Channel` by `OverlayToken`
   - Verify the widget belongs to that channel
   - If valid, join the SignalR group
   - If invalid, reject the connection
5. No OAuth, no login, no cookies -- just the URL token

### 19.4 Security Properties

- **Per-channel**: Each channel has its own token. Knowing Channel A's token gives zero access to Channel B.
- **Rotatable**: Broadcaster can regenerate from dashboard Settings > Danger Zone. All existing OBS sources need the new URL.
- **No API access**: The overlay token ONLY authenticates SignalR widget connections. It cannot be used to call any API endpoint.
- **Scope-limited**: The token only allows receiving events for widgets belonging to that channel. It cannot send commands, modify settings, or access any data.
- **IP whitelist (optional)**: Broadcaster can optionally restrict overlay connections to specific IPs from dashboard Settings. If set, both the token AND the IP must match. Useful for extra security but not required.

### 19.5 Dashboard UI

In Channel Settings:
- "Overlay Token" section showing the current token (masked, with copy button)
- "Regenerate Token" button with confirmation warning
- Optional "Allowed IPs" field (comma-separated, empty = any IP)
- "Copy Overlay URL" buttons next to each widget that include the token automatically

---

---

## 20. Granular Permissions System

### 20.1 Problem

The current system has a flat role hierarchy (Viewer -> Subscriber -> VIP -> Moderator -> Broadcaster). This means you either have access to everything at your level or nothing. Real needs are more nuanced:
- "I want this viewer to be able to use HTML rendering in chat but not others"
- "I want my VIPs to use `!sr` but not `!skip`"
- "This command should be usable by everyone but only in slow mode"
- "Moderator X should manage commands but not rewards"

### 20.2 Polymorphic Permission Model

A single `Permissions` table using polymorphic relations to handle permissions for any entity type:

```
Permissions
  - Id: int (PK, identity)
  - BroadcasterId: string (FK to Channel, NOT NULL)
  - SubjectType: string (NOT NULL) -- "user", "role"
  - SubjectId: string (NOT NULL) -- Twitch user ID or role name ("subscriber", "vip", "moderator", "everyone")
  - ResourceType: string (NOT NULL) -- "command", "reward", "widget", "feature"
  - ResourceId: string? -- Specific resource ID (null = all resources of this type)
  - Permission: string (NOT NULL) -- "allow", "deny"
  - CreatedAt, UpdatedAt
  - Index: (BroadcasterId, ResourceType, ResourceId)
  - Index: (BroadcasterId, SubjectType, SubjectId)
```

### 20.3 How It Works

**Resolution order** (first match wins):
1. Check explicit user deny -> DENIED
2. Check explicit user allow -> ALLOWED
3. Check explicit role deny (for user's Twitch role) -> DENIED
4. Check explicit role allow (for user's Twitch role) -> ALLOWED
5. Fall back to the command/reward's default `Permission` level (the existing `CommandPermission` enum)

**Examples**:

| Scenario | SubjectType | SubjectId | ResourceType | ResourceId | Permission |
|----------|-------------|-----------|-------------|------------|-----------|
| Let specific user use HTML rendering | user | 132799162 | feature | html_rendering | allow |
| Block a user from song requests | user | 999999 | command | sr | deny |
| Let VIPs skip songs | role | vip | command | skip | allow |
| Deny everyone from a specific reward | role | everyone | reward | lucky-feather | deny |
| Let subscriber role use all commands | role | subscriber | command | (null) | allow |

### 20.4 Feature Permissions (Built-in Resource Types)

These are the `feature` resource type IDs:

| Feature ID | Default Access | Description |
|------------|---------------|-------------|
| `html_rendering` | subscriber | HTML tags in chat rendered in overlay |
| `og_preview` | subscriber | URL previews with images in overlay |
| `tts` | everyone (via reward) | Text-to-speech |
| `song_request` | everyone | `!sr` command access |
| `custom_voice` | subscriber | Custom TTS voice selection |

### 20.5 Dashboard Permission Manager

The dashboard provides a visual permission editor per-channel:

- **Per-command permissions**: Click a command -> set who can use it (dropdown: Everyone/Sub/VIP/Mod/Broadcaster + specific user allow/deny list)
- **Per-reward permissions**: Same pattern for rewards
- **Feature permissions**: Toggle features for roles/specific users
- **User permission overrides**: Search for a user -> see/edit all their specific permissions across commands, rewards, features
- **Bulk operations**: "Set all commands to Moderator only" with one click

### 20.6 Permission Check Flow (Updated)

The current `PermissionService.UserHasMinLevel()` becomes:

```
PermissionService.CanAccess(broadcasterId, userId, userType, resourceType, resourceId?) -> bool

1. Query Permissions table for (broadcasterId, resourceType, resourceId)
2. Check user-specific deny -> return false
3. Check user-specific allow -> return true  
4. Check role-specific deny (for user's Twitch role) -> return false
5. Check role-specific allow (for user's Twitch role) -> return true
6. Fall back to resource's default permission level (CommandPermission enum)
```

Cache the permissions per-channel in a `ConcurrentDictionary` to avoid DB hits on every command. Invalidate on permission change via API.

---

---

## 21. Dashboard Design

### 21.1 Design Principles

- **Modern but not AI-looking** -- Clean, purposeful design. No gradient blobs, no generative art patterns
- **User's chat color as theme** -- The broadcaster's Twitch chat color becomes the accent/primary color throughout the dashboard. Buttons, links, active states, highlights all use this color
- **Dark mode first** -- Streamers work in dark environments. Dark background with the chat color as accent
- **Better than Twitch's own tools** -- Every management task should be faster and clearer than doing it on Twitch directly
- **Information density** -- Show more data without clutter. Tables, not cards-for-everything

### 21.2 Navigation Structure

```
Sidebar:
  [Channel Switcher dropdown]
  
  Dashboard (home/overview)
  
  Chat
    ├── Live Chat (real-time view)
    ├── Chat Settings (emote-only, slow mode, etc.)
    └── Chat Logs (searchable history)
  
  Commands
    ├── All Commands (list + editor)
    ├── Create Command
    └── Cooldowns & Aliases
  
  Rewards
    ├── Channel Point Rewards
    ├── Bot Reward Handlers
    └── Redemption Queue
  
  Moderation
    ├── Banned Users
    ├── Blocked Terms
    ├── AutoMod Settings
    ├── Suspicious Users
    ├── Mod Actions Log
    └── Shield Mode
  
  Widgets
    ├── My Widgets
    ├── Widget Templates
    ├── Create Widget
    └── Widget Preview
  
  Music
    ├── Now Playing
    ├── Queue
    ├── Song Requests Settings
    ├── Banned Songs
    └── Playlists
  
  Stream
    ├── Stream Info (title, game, tags)
    ├── Schedule
    ├── Raids
    ├── Clips
    ├── Polls & Predictions
    └── Ads
  
  Community
    ├── Followers
    ├── Subscribers
    ├── VIPs
    ├── Moderators
    ├── Shoutout Templates
    └── Watch Streaks
  
  Integrations
    ├── Spotify
    ├── Discord
    ├── OBS
    └── TTS
  
  Universes
    ├── Joined Universes
    ├── Universe Marketplace
    └── Create Universe
  
  Permissions
    ├── Role Permissions
    ├── User Overrides
    └── Feature Access
  
  Settings
    ├── Channel Settings
    ├── Bot Account
    └── Danger Zone (delete channel, revoke tokens)
```

### 21.3 Key Dashboard Pages

#### Overview / Home
- Stream status indicator (LIVE / OFFLINE) with uptime
- Current viewer count, follower count, sub count
- Recent events timeline (follows, subs, raids, cheers)
- Quick actions: Change title, change game, run ad, toggle shield mode
- Active alerts / warnings (token expiring, integration disconnected)

#### Commands Page
- Table view: Name, Type (text/random/counter/script), Permission, Cooldown, Enabled, Usage Count
- Inline enable/disable toggle
- Click to expand: edit form slides in (not a modal -- keeps context)
- Script commands show Monaco editor inline
- "Test" button: simulates the command and shows what would happen
- Search and filter by name, type, permission level
- Drag-and-drop reordering for priority
- Platform commands shown with lock icon, "Duplicate" button to create editable copy

#### Rewards Page
- Two sections: "Twitch Rewards" (synced from Twitch API) and "Bot Handlers" (what the bot does on redemption)
- Twitch Rewards: create, edit, pause/unpause, set cost -- all without leaving the dashboard
- Pending redemptions queue with bulk fulfill/refund
- Bot Handler mapping: drag a bot handler onto a Twitch reward to connect them

#### Moderation Page
- **Banned Users**: searchable table with ban reason, date, unban button
- **Blocked Terms**: add/remove with regex support indicator
- **AutoMod**: slider controls for each AutoMod category (same as Twitch but in one place)
- **Mod Actions Log**: real-time log of all mod actions in the channel
- **Shield Mode**: big toggle button with current status

#### Chat Settings
- Emote-only mode toggle
- Subscriber-only mode toggle  
- Slow mode with duration slider
- Follower-only mode with duration
- All changes apply immediately via Twitch API
- Current settings shown as live indicators

#### Music Page
- Now playing card with album art, progress bar, controls (play/pause/skip/volume)
- Queue list with drag-to-reorder and remove
- Song request settings: enabled/disabled, max duration, banned songs list
- Search Spotify inline and add to queue directly
- Playlist manager: view bangers playlist, remove songs

#### Stream Info Page
- Edit title and game with autocomplete (search Twitch categories)
- Tags editor
- Schedule viewer/editor
- Content classification labels
- All saved with one click, applied via Twitch API

#### Permissions Page
- Three tabs: Roles, Users, Features
- **Roles tab**: matrix view -- rows are roles (Everyone/Sub/VIP/Mod), columns are resources (commands/rewards/features). Click cells to toggle allow/deny
- **Users tab**: search for a user, see all their overrides, add/remove
- **Features tab**: toggle features per role with clear descriptions of what each feature does

### 21.4 Theming System

```
Theme Generation from Chat Color:
  1. User logs in, we fetch their Twitch chat color (e.g., #FF6B35)
  2. Generate a full color palette:
     - Primary: user's chat color
     - Primary light/dark: HSL shifted variants
     - Background: dark neutral (not tinted -- keep it clean)
     - Surface: slightly lighter dark neutral
     - Text: white/light gray
     - Accent: complement or analogous of chat color
  3. CSS custom properties set on :root
  4. All components use var(--color-primary), var(--color-surface), etc.
  5. User can override in settings if they don't like the auto-generated theme
```

### 21.5 Real-time Updates

- Dashboard connects to SignalR hub for live updates
- Chat messages stream in real-time on the Chat page
- Stream status changes update the header indicator immediately
- Reward redemptions appear in the queue instantly
- Mod actions appear in the log as they happen
- Music track changes update the Now Playing card
- No polling -- everything is push via SignalR

### 21.6 Responsive Design

- Desktop: full sidebar, multi-column layouts
- Tablet: collapsible sidebar, single-column content
- Mobile: bottom navigation, stacked layouts, touch-friendly controls
- Critical actions (change title, toggle shield mode) accessible in 2 taps on mobile

### 21.7 Tools Inspired by twitch-tools.rootonline.de

Features to integrate that Twitch doesn't provide natively:

| Tool | Implementation |
|------|---------------|
| **Follower management** | Bulk follower viewer with search, filter by follow date, remove bot followers |
| **Chat log search** | Full-text search across all chat messages, filter by user/date/content |
| **Emote browser** | View all channel emotes (Twitch + BTTV + FFZ + 7TV) in one place |
| **User lookup** | Click any username -> see account age, follow date, message count, ban history, all in a sidebar panel |
| **Mod action history** | Searchable log of all bans, timeouts, message deletions with who did what and when |
| **Blocked terms manager** | Add/remove/test blocked terms with regex preview |
| **Bot detection** | Flag suspicious followers/chatters based on account age, username patterns |
| **Clip browser** | View, search, and manage clips without leaving the dashboard |

---

---

## 22. Billing and Sustainability

### 22.1 Philosophy

Keep it as cheap as possible. The bot should be usable for free with reasonable limits. Revenue covers infrastructure costs, not profit maximization. The platform NEVER pays for per-channel costs that scale with usage (TTS, music API calls) -- those are either free-tier providers or BYOK (Bring Your Own Key).

### 22.2 TTS Cost Model -- BYOK

**Problem**: Azure TTS costs ~$16/1M characters. "Unlimited TTS" on any tier would bankrupt the platform. A single abusive user could generate millions of characters.

**Solution**: Two-tier TTS approach:

| Provider | Cost | Quality | Availability |
|----------|------|---------|-------------|
| **Edge TTS** | Free (browser-based synthesis) | Good | Default for ALL tiers. No API key needed. |
| **Azure TTS** | ~$16/1M chars | Excellent | BYOK only. Broadcaster provides their own Azure Speech key in dashboard. |
| **Google TTS** | ~$16/1M chars | Excellent | BYOK only. Same pattern. |
| **ElevenLabs** | Varies | Premium | BYOK only. |

Edge TTS is the **default and only platform-provided TTS**. It's free, decent quality, and already implemented. Any paid TTS provider requires the broadcaster to enter their own API key. The platform never touches Azure/Google billing.

**Dashboard UI**: Integrations > TTS > "Using Edge TTS (free). Want better voices? Add your Azure Speech API key for premium TTS."

### 22.3 Tier Model

| Tier | Price | Limits | Target |
|------|-------|--------|--------|
| **Free** | $0 | 1 channel, basic commands, Edge TTS only, no custom widgets, no Spotify, community support | Small streamers trying it out |
| **Starter** | $5/mo | 1 channel, all commands, Edge TTS + BYOK premium TTS, 3 custom widgets, Spotify, Discord notifications, email support | Growing streamers |
| **Pro** | $15/mo | 3 channels, unlimited widgets, OBS integration, priority support, custom branding (bot name override) | Established streamers |
| **Platform** | $30/mo | 10 channels, everything in Pro, Universe creation (post-MVP), API access, webhook integrations | Multi-channel operators |

### 22.4 Cost Drivers

| Service | Cost Source | Mitigation |
|---------|-----------|------------|
| **TTS** | $0 (Edge TTS is free) | Platform pays nothing. BYOK for premium providers. |
| **PostgreSQL** | ~$15-50/mo managed hosting | Start with a small instance. Scale vertically as needed. |
| **Hosting** | ~$20-50/mo VPS | Single server to start. Horizontal scale later. |
| **Bandwidth** | SignalR + widget overlays | CDN for static assets. SignalR is lightweight text. |
| **Twitch API** | Free (rate limits only) | Respect rate limits, batch where possible. |
| **Spotify API** | Free (rate limits) | Poll interval scales with channel count. |

**Estimated monthly cost at 100 channels**: ~$50-100/mo (no TTS cost!)
**Revenue at 100 channels** (assuming 60% free, 30% Starter, 10% Pro): ~$225/mo

### 22.5 Payment Integration

- **Stripe** for payment processing (standard for SaaS)
- Subscription management via Stripe Customer Portal
- Webhook-driven: Stripe notifies the platform of subscription changes
- **CRITICAL**: All Stripe webhooks MUST validate the webhook signature using the Stripe signing secret. Without this, anyone can forge webhook events and grant themselves paid tiers.
- Grace period: 7 days after failed payment before downgrading
- No data loss on downgrade -- features are disabled, data is preserved

### 22.6 Data Model

Defined in section 2.2.5 (`ChannelSubscriptions` table).

### 22.7 Feature Gating

The `ChannelSubscription.Tier` is checked by a `IFeatureGateService`:

```csharp
public interface IFeatureGateService
{
    bool IsFeatureAvailable(string broadcasterId, string featureKey);
    int GetLimit(string broadcasterId, string limitKey);
}
```

Injected via DI. Every feature that has tier limits checks the gate before executing. Free tier users see "Upgrade to unlock" in the dashboard instead of disabled buttons.

### 22.8 BYOK Key Management

Broadcaster-provided API keys (Azure TTS, Google TTS, ElevenLabs, etc.) are stored in the `Service` table:
- `Name = "AzureTTS"`, `BroadcasterId = channelId`
- `ClientId` = Azure region endpoint
- `ClientSecret` = Azure subscription key (encrypted)

The `ITtsProvider` interface resolves the correct provider and key per-channel via the ChannelRegistry. If no BYOK key exists, Edge TTS is used.

---

---

## 23. Launch Blockers

### 23.1 Spotify Extended Quota Mode

Spotify Dev Mode limits apps to **5 users**. We cannot launch multi-channel Spotify support without Extended Quota Mode approval. This can take **2-6 weeks**.

**Action**: Apply immediately at the Spotify Developer Dashboard. Required: app description, privacy policy URL, terms of service URL, explanation of how the app promotes artist discovery. Note: Spotify's commercial use policy may require additional compliance for paid applications.

### 23.2 Spotify API Rate Limits

Spotify allows ~180 requests/minute per app. At 3s polling per live channel, each channel uses ~20 req/min. This caps the platform at **~9 concurrent live channels** with Spotify. The Platform tier ($30/mo) allows 10 channels per customer -- a single customer could exhaust the entire budget.

**Action**: Investigate whether rate limits are per-app or per-user-token. If per-app, this is a hard scaling ceiling. Mitigations: increase poll interval (5-10s), only poll when someone is actively viewing the now-playing widget, request rate limit increase from Spotify.

### 23.3 Privacy Policy

Required by Twitch Developer Agreement, Spotify Developer Terms, and Discord Developer Terms before app approval. Also required by GDPR for EU users.

**Action**: Create and publish privacy policy and terms of service at `https://nomercy.tv/privacy` and `https://nomercy.tv/terms`. Must cover all data listed in section 24.

### 23.4 Twitch Application Verification

For more than ~100 concurrent API users, Twitch may require app verification. Not an immediate blocker but apply early.

### 23.5 Discord Bot Verification

Discord requires bot verification when a bot is in 75+ servers. Apply when approaching that threshold.

---

---

## 24. Data Privacy and Deletion (GDPR / Twitch Compliance)

### 24.1 What Data We Store Per User

| Data | Table | Retention | Purpose |
|------|-------|-----------|---------|
| Twitch user ID, username, display name, profile image | `Users` | Until deletion request | User identity |
| Chat messages (full text, fragments, badges) | `ChatMessages` | Indefinite (for chat logs, replay) | Chat history, moderation, analytics |
| Chat presence (join/leave) | `ChatPresences` | Indefinite | Watch streak tracking |
| Command usage records | `Records` (type: CommandUsage) | Indefinite | Stats, leaderboards |
| Watch streak data | `Records` (type: WatchStreak) | Indefinite | Watch streak feature |
| Song request history | `Records` (type: Spotify) | Indefinite | Song history |
| Permission overrides | `Records` (type: PermissionOverride) | Until revoked | Bot permission system |
| Banned song records | `Records` (type: BannedSong) | Until unbanned | Song request moderation |
| TTS voice preference | `UserTtsVoices` | Until changed | TTS customization |
| TTS usage records | `TtsUsageRecords` | Indefinite | Usage tracking, billing |
| User pronouns | `Users.PronounData` | Until changed | Pronoun display |
| Channel moderator grants | `ChannelModerators` | Until removed | Dashboard access |
| Shoutout records | `Shoutouts` | Indefinite | Shoutout cooldowns |
| Channel events (follows, subs, raids, cheers) | `ChannelEvents` | Indefinite | Event replay, analytics |

### 24.2 Deletion Request Types

#### User Data Deletion (GDPR Article 17 -- Right to Erasure)

A user (any chatter, not just broadcasters) can request deletion of ALL their personal data. This includes:

**Must delete**:
- All `ChatMessages` where `UserId = requestingUserId` -- replace message content with "[deleted]", clear fragments, clear Username, DisplayName, ColorHex, Badges. Keep the row for thread integrity (soft delete via DeletedAt)
- All `Records` where `UserId = requestingUserId` -- full delete
- All `ChatPresences` where `UserId = requestingUserId` -- full delete
- All `UserTtsVoices` where `UserId = requestingUserId` -- full delete
- All `TtsUsageRecords` where `UserId = requestingUserId` -- full delete  
- All `ChannelEvents` where `UserId = requestingUserId` -- anonymize (replace user ID with "deleted_user", clear user-specific data from JSON)
- All `ChannelModerators` where `UserId = requestingUserId` -- hard delete
- All `Shoutouts` where `ShoutedUserId = requestingUserId` -- hard delete
- All `Permissions` where `SubjectType = "user" AND SubjectId = requestingUserId` -- hard delete
- `ChannelSubscription` where user is broadcaster -- cancel subscription, anonymize Stripe references
- `Users` record -- anonymize (set Username = "deleted_user_{hash}", DisplayName = "Deleted User", clear all other fields, set DeletedAt)
- If the user is also a broadcaster: trigger Channel Data Deletion flow below for their channel(s)

**Must NOT delete** (legitimate interest / legal obligation):
- Ban records (moderation actions are retained for channel safety, but the banned user's display name is anonymized)
- The `Users` row itself (anonymized, not deleted, to prevent FK violations)

#### Channel Data Deletion (Broadcaster leaves the platform)

When a broadcaster disconnects their channel:
- All `Service` records for their `BroadcasterId` -- delete (tokens are destroyed)
- All `Commands` for their channel -- delete
- All `Rewards` for their channel -- delete
- All `Widgets` for their channel -- delete
- All `EventSubscriptions` -- delete (also unsubscribe from Twitch EventSub)
- All `Configurations` for their channel -- delete
- All `Storages` for their channel -- delete
- All `Permissions` for their channel -- delete
- `Channel` record -- soft delete (set `DeletedAt`), keep for 30 days, then hard delete
- Chat messages in their channel -- retained (they belong to the individual chatters, not the broadcaster)

#### Twitch Compliance (User Deletion Webhook)

Twitch can send a **User Data Deletion** webhook when a user deletes their Twitch account or requests data removal. The platform must:
1. Subscribe to the `user.authorization.revoke` EventSub event
2. When received, execute the full user data deletion flow above
3. Log the deletion request (without personal data) for audit trail
4. Respond within 30 days (GDPR requirement)

### 24.3 Implementation

#### API Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/me/delete-data` | Bearer (the user themselves) | User requests deletion of all their data |
| GET | `/api/me/export-data` | Bearer | User requests an export of all their data (GDPR Article 15) |
| POST | `/api/channels/{channelId}/delete` | Broadcaster | Broadcaster removes their channel from the platform |
| POST | `/api/admin/delete-user/{userId}` | Platform Admin | Admin-initiated deletion (Twitch compliance) |

#### Data Export (GDPR Article 15 -- Right of Access)

Users can download all their data in JSON format:
```json
{
  "user": { "id": "...", "username": "...", "display_name": "..." },
  "chat_messages": [ { "channel": "...", "message": "...", "timestamp": "..." }, ... ],
  "command_usage": [ ... ],
  "watch_streaks": [ ... ],
  "song_requests": [ ... ],
  "tts_preferences": { ... },
  "permissions": [ ... ]
}
```

#### Deletion Service

```csharp
public interface IDataDeletionService
{
    Task<DeletionResult> DeleteUserDataAsync(string userId, string reason);
    Task<DeletionResult> DeleteChannelDataAsync(string broadcasterId, string reason);
    Task<byte[]> ExportUserDataAsync(string userId);
}
```

Registered via DI. Called by API endpoints and by the Twitch `user.authorization.revoke` EventSub handler.

#### Deletion Audit Log

Every deletion is logged (without personal data):

```
DeletionAuditLog (new table)
  - Id: int (PK, identity)
  - RequestType: string ("user_deletion", "channel_deletion", "twitch_revoke")
  - SubjectIdHash: string (SHA256 of the deleted user/channel ID -- for audit, not re-identification)
  - RequestedBy: string ("self", "twitch", "admin")
  - TablesAffected: string[] (JSON list of table names)
  - RowsDeleted: int
  - CompletedAt: DateTime
  - CreatedAt: DateTime
```

### 24.4 Retention Policy

| Data Type | Default Retention | Configurable |
|-----------|------------------|-------------|
| Chat messages | Indefinite | Yes -- broadcaster can set auto-delete after N days |
| Channel events | Indefinite | Yes -- broadcaster can set auto-delete after N days |
| Command usage stats | Indefinite | No (aggregated, low PII) |
| TTS cache (audio files) | 30 days | No |
| OAuth tokens | Until revoked | No |
| Deletion audit log | 7 years | No (legal requirement) |

### 24.5 Privacy Policy Requirements

The platform must have a published privacy policy that covers:
- What data is collected and why
- How long data is retained
- How users can request deletion or export
- Third-party data sharing (Twitch API, Spotify API, Discord API, Azure TTS)
- Data processing location (server hosting region)
- Contact information for privacy requests

This is required by:
- GDPR (EU users)
- Twitch Developer Agreement (required for app approval)
- Spotify Developer Terms (required for Extended Quota Mode)
- Discord Developer Terms

---

## 25. Open Source Strategy and Repository Setup

### 25.1 License

**AGPL-3.0** (GNU Affero General Public License v3). This means:
- Anyone can self-host for free
- Anyone who modifies and runs it as a **service** MUST publish their changes
- Our revenue comes from the managed platform (hosting, convenience, support), not the code
- The managed platform is always ahead of self-hosted (latest features, zero setup)
- Competitors cannot run a closed-source fork as a competing service

### 25.2 GitHub Organization

**Organization**: `NoMercyLabs` (https://github.com/NoMercyLabs)
**Repository**: `nomercybot` (https://github.com/NoMercyLabs/nomercybot)

**Manual step required**: Create the org at https://github.com/account/organizations/new (GitHub API does not support org creation on github.com). Pick the Free plan.

### 25.3 Repository Setup (via CLI after org creation)

```bash
# Create the repo
gh repo create NoMercyLabs/nomercybot --public --description "Open source multi-channel Twitch bot platform" --license agpl-3.0

# Branch protection on main
gh api repos/NoMercyLabs/nomercybot/branches/main/protection -X PUT \
  -f required_pull_request_reviews.required_approving_review_count=1 \
  -f required_status_checks.strict=true \
  -f enforce_admins=false \
  -f restrictions=null

# Labels
gh label create "bug" --repo NoMercyLabs/nomercybot --color d73a4a --description "Something isn't working"
gh label create "feature" --repo NoMercyLabs/nomercybot --color 0075ca --description "New feature or request"
gh label create "docs" --repo NoMercyLabs/nomercybot --color 0075ca --description "Documentation improvements"
gh label create "security" --repo NoMercyLabs/nomercybot --color e4e669 --description "Security related"
gh label create "good first issue" --repo NoMercyLabs/nomercybot --color 7057ff --description "Good for newcomers"
```

### 25.4 CI/CD Pipeline (GitHub Actions)

```yaml
# .github/workflows/ci.yml
name: CI
on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal

  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet tool restore
      - run: dotnet csharpier --check src/

  frontend:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: src/NoMercyBot.Client
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
      - run: npm ci
      - run: npm run build
      - run: npm run lint
```

### 25.5 Branch Strategy

| Branch | Purpose | Protection |
|--------|---------|------------|
| `main` | Production-ready | PR required, CI must pass, 1 approval |
| `develop` | Integration branch | CI must pass |
| `feature/*` | Feature branches | None |
| `fix/*` | Bug fix branches | None |
| `release/*` | Release candidates | CI must pass |

### 25.6 Documentation Requirements for Open Source

The repo must include:
- `README.md` -- Project overview, quick start, screenshots
- `CONTRIBUTING.md` -- How to contribute, code style, PR process
- `CODE_OF_CONDUCT.md` -- Community standards
- `SECURITY.md` -- How to report security vulnerabilities
- `LICENSE` -- AGPL-3.0
- `docs/` -- Architecture spec (this document), API docs, deployment guide
- `docs/self-hosting.md` -- How to self-host (Docker Compose, env vars, database setup)
- `.env.example` -- Example environment variables (no secrets)

### 25.7 Testing Strategy

| Layer | Framework | What's Tested |
|-------|-----------|--------------|
| **Unit tests** | xUnit + Moq | Services, command pipeline engine, permission resolution, template variable substitution |
| **Integration tests** | xUnit + WebApplicationFactory | API endpoints, auth flow, database operations |
| **E2E tests** | Playwright | Dashboard flows, OAuth redirects, widget rendering |

**Coverage target**: 80% for services, 90% for the pipeline action engine (security-critical).

**Test project structure**:
```
tests/
  NoMercyBot.Services.Tests/
  NoMercyBot.Api.Tests/
  NoMercyBot.Database.Tests/
  NoMercyBot.E2E.Tests/
```

### 25.8 Secrets in CI

- `STRIPE_SECRET_KEY` -- For billing integration tests (test mode key)
- `TWITCH_CLIENT_ID` / `TWITCH_CLIENT_SECRET` -- For Twitch API integration tests
- `DATABASE_URL` -- PostgreSQL connection for integration tests
- Stored as GitHub Actions secrets, never in code

### 25.9 User Data Visibility

Every Twitch user is a channel owner of themselves. Any user who logs into the dashboard can:
- See a list of ALL channels they've been detected in (from ChatPresence records)
- View their personal stats per channel (message count, watch time, command usage)
- Request deletion of their data from specific channels or all channels
- Export all their data (GDPR Article 15)

The dashboard "My Data" page shows:
```
Channels you've been seen in:
  - stoney_eagle (245 messages, 12h watch time) [View Stats] [Delete My Data]
  - another_streamer (30 messages, 2h watch time) [View Stats] [Delete My Data]
  
[Export All My Data] [Delete All My Data]
```

---
