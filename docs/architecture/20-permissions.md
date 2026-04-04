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
