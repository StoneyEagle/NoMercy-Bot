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
