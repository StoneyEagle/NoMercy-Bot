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
