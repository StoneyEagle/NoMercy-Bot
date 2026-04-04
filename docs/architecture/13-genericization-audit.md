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
