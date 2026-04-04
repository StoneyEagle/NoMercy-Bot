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
- All `ChatMessages` where `UserId = requestingUserId` -- replace message content with "[deleted]", clear fragments, keep the row for thread integrity
- All `Records` where `UserId = requestingUserId` -- full delete
- All `ChatPresences` where `UserId = requestingUserId` -- full delete
- All `UserTtsVoices` where `UserId = requestingUserId` -- full delete
- All `TtsUsageRecords` where `UserId = requestingUserId` -- full delete  
- All `ChannelEvents` where `UserId = requestingUserId` -- anonymize (replace user ID with "deleted_user", clear user-specific data from JSON)
- All `ChannelModerators` where `UserId = requestingUserId` -- full delete
- All `Shoutouts` where `ShoutedUserId = requestingUserId` -- full delete
- `Users` record -- anonymize (set Username = "deleted_user_{hash}", DisplayName = "Deleted User", clear all other fields)

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
- `Channel` record -- mark as `IsActive = false`, keep for 30 days, then hard delete
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
