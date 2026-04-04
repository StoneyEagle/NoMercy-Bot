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
  - SubscriptionTier: string (max 20, default "free")
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
- Add `GrantedAt` (DateTime, default CURRENT_TIMESTAMP)
- Add `GrantedBy` (string?, FK to User) -- Who granted dashboard access

**Why**: The `ChannelModerator` table tracks which users have moderator-level dashboard access for a channel. The broadcaster is always implicitly the channel owner (channelId == userId) and doesn't need a row here. No "Role" column -- presence in this table = moderator access, matching Twitch's binary mod/not-mod model.

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

---
