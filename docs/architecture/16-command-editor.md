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
