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
