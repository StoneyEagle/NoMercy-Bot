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
| `channel:bot` | Lets the bot chat in your channel with the bot badge |
| `user:read:chat` | Lets the bot read chat messages |
| `moderator:read:chatters` | Lets the bot see who's in chat |

That's it. Three scopes. The consent screen is small and non-threatening. The user gets: bot joins their channel, reads chat, responds to commands. Core functionality works.

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

### 3.3 Role Model -- Aligned with Twitch Roles

The platform uses the **same role hierarchy that Twitch uses**. No invented roles. No "Editor" or "Owner" that doesn't exist on Twitch.

| Role | Source | Dashboard Access | API Permissions |
|------|--------|-----------------|-----------------|
| **Platform Admin** | Config flag | All channels, full access | Everything |
| **Broadcaster** | Twitch (channel owner) | Full control of their channel | Commands, rewards, widgets, events, integrations, moderator management |
| **Moderator** | Twitch mod status OR `ChannelModerator` table | Manage commands, rewards, view settings | CRUD commands/rewards, trigger widget events, send bot messages |
| **VIP** | Twitch VIP status | No dashboard access | Chat-only permissions (VIP-level commands) |
| **Subscriber** | Twitch sub status | No dashboard access | Chat-only permissions (sub-only commands) |
| **Viewer** | Default | No dashboard access | Basic chat commands only |

**Dashboard access rule**: Only Broadcaster and Moderator get dashboard access. VIP/Sub/Viewer are chat-only roles, matching Twitch behavior exactly.

**How it's determined**:
1. User logs in via Twitch OAuth.
2. Are they a broadcaster of any channel on the platform? -> They see their channel(s) with Broadcaster access.
3. Are they in the `ChannelModerator` table for any channel? -> They see those channels with Moderator access.
4. Platform admin list stored in `Configuration(Key = "platform_admins", Value = "comma,separated,twitch,ids", BroadcasterId = null)`.

**Permission override system (chat only)**: The existing `!whitelist` / `!unwhitelist` commands let broadcasters grant someone subscriber/VIP/mod level **for bot commands in chat** without changing their Twitch role. This only affects `CommandPermission` checks, not dashboard access.

**API authorization mapping**:

| Endpoint Category | Min Role |
|-------------------|----------|
| View channel dashboard/stats | Moderator |
| Manage commands (CRUD) | Moderator |
| Create/edit script commands | Broadcaster |
| Manage rewards (CRUD) | Moderator |
| Trigger widget demo events | Moderator |
| View/edit channel settings | Broadcaster |
| Connect integrations (Spotify/Discord/OBS) | Broadcaster |
| Manage EventSub subscriptions | Broadcaster |
| Invite/remove moderators | Broadcaster |
| Send bot messages | Moderator |

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

No change to the stateless Bearer token model. Each request is independently authenticated. The dashboard frontend stores the user's session token (obtained via the platform's OAuth flow) and includes it in API calls. The platform validates this against Twitch on each request (with caching).

---
