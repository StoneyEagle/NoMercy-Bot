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
