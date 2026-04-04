## 21. Dashboard Design

### 21.1 Design Principles

- **Modern but not AI-looking** -- Clean, purposeful design. No gradient blobs, no generative art patterns
- **User's chat color as theme** -- The broadcaster's Twitch chat color becomes the accent/primary color throughout the dashboard. Buttons, links, active states, highlights all use this color
- **Dark mode first** -- Streamers work in dark environments. Dark background with the chat color as accent
- **Better than Twitch's own tools** -- Every management task should be faster and clearer than doing it on Twitch directly
- **Information density** -- Show more data without clutter. Tables, not cards-for-everything

### 21.2 Navigation Structure

```
Sidebar:
  [Channel Switcher dropdown]
  
  Dashboard (home/overview)
  
  Chat
    ├── Live Chat (real-time view)
    ├── Chat Settings (emote-only, slow mode, etc.)
    └── Chat Logs (searchable history)
  
  Commands
    ├── All Commands (list + editor)
    ├── Create Command
    └── Cooldowns & Aliases
  
  Rewards
    ├── Channel Point Rewards
    ├── Bot Reward Handlers
    └── Redemption Queue
  
  Moderation
    ├── Banned Users
    ├── Blocked Terms
    ├── AutoMod Settings
    ├── Suspicious Users
    ├── Mod Actions Log
    └── Shield Mode
  
  Widgets
    ├── My Widgets
    ├── Widget Templates
    ├── Create Widget
    └── Widget Preview
  
  Music
    ├── Now Playing
    ├── Queue
    ├── Song Requests Settings
    ├── Banned Songs
    └── Playlists
  
  Stream
    ├── Stream Info (title, game, tags)
    ├── Schedule
    ├── Raids
    ├── Clips
    ├── Polls & Predictions
    └── Ads
  
  Community
    ├── Followers
    ├── Subscribers
    ├── VIPs
    ├── Moderators
    ├── Shoutout Templates
    └── Watch Streaks
  
  Integrations
    ├── Spotify
    ├── Discord
    ├── OBS
    └── TTS
  
  Universes
    ├── Joined Universes
    ├── Universe Marketplace
    └── Create Universe
  
  Permissions
    ├── Role Permissions
    ├── User Overrides
    └── Feature Access
  
  Settings
    ├── Channel Settings
    ├── Bot Account
    └── Danger Zone (delete channel, revoke tokens)
```

### 21.3 Key Dashboard Pages

#### Overview / Home
- Stream status indicator (LIVE / OFFLINE) with uptime
- Current viewer count, follower count, sub count
- Recent events timeline (follows, subs, raids, cheers)
- Quick actions: Change title, change game, run ad, toggle shield mode
- Active alerts / warnings (token expiring, integration disconnected)

#### Commands Page
- Table view: Name, Type (text/random/counter/script), Permission, Cooldown, Enabled, Usage Count
- Inline enable/disable toggle
- Click to expand: edit form slides in (not a modal -- keeps context)
- Script commands show Monaco editor inline
- "Test" button: simulates the command and shows what would happen
- Search and filter by name, type, permission level
- Drag-and-drop reordering for priority
- Platform commands shown with lock icon, "Duplicate" button to create editable copy

#### Rewards Page
- Two sections: "Twitch Rewards" (synced from Twitch API) and "Bot Handlers" (what the bot does on redemption)
- Twitch Rewards: create, edit, pause/unpause, set cost -- all without leaving the dashboard
- Pending redemptions queue with bulk fulfill/refund
- Bot Handler mapping: drag a bot handler onto a Twitch reward to connect them

#### Moderation Page
- **Banned Users**: searchable table with ban reason, date, unban button
- **Blocked Terms**: add/remove with regex support indicator
- **AutoMod**: slider controls for each AutoMod category (same as Twitch but in one place)
- **Mod Actions Log**: real-time log of all mod actions in the channel
- **Shield Mode**: big toggle button with current status

#### Chat Settings
- Emote-only mode toggle
- Subscriber-only mode toggle  
- Slow mode with duration slider
- Follower-only mode with duration
- All changes apply immediately via Twitch API
- Current settings shown as live indicators

#### Music Page
- Now playing card with album art, progress bar, controls (play/pause/skip/volume)
- Queue list with drag-to-reorder and remove
- Song request settings: enabled/disabled, max duration, banned songs list
- Search Spotify inline and add to queue directly
- Playlist manager: view bangers playlist, remove songs

#### Stream Info Page
- Edit title and game with autocomplete (search Twitch categories)
- Tags editor
- Schedule viewer/editor
- Content classification labels
- All saved with one click, applied via Twitch API

#### Permissions Page
- Three tabs: Roles, Users, Features
- **Roles tab**: matrix view -- rows are roles (Everyone/Sub/VIP/Mod), columns are resources (commands/rewards/features). Click cells to toggle allow/deny
- **Users tab**: search for a user, see all their overrides, add/remove
- **Features tab**: toggle features per role with clear descriptions of what each feature does

### 21.4 Theming System

```
Theme Generation from Chat Color:
  1. User logs in, we fetch their Twitch chat color (e.g., #FF6B35)
  2. Generate a full color palette:
     - Primary: user's chat color
     - Primary light/dark: HSL shifted variants
     - Background: dark neutral (not tinted -- keep it clean)
     - Surface: slightly lighter dark neutral
     - Text: white/light gray
     - Accent: complement or analogous of chat color
  3. CSS custom properties set on :root
  4. All components use var(--color-primary), var(--color-surface), etc.
  5. User can override in settings if they don't like the auto-generated theme
```

### 21.5 Real-time Updates

- Dashboard connects to SignalR hub for live updates
- Chat messages stream in real-time on the Chat page
- Stream status changes update the header indicator immediately
- Reward redemptions appear in the queue instantly
- Mod actions appear in the log as they happen
- Music track changes update the Now Playing card
- No polling -- everything is push via SignalR

### 21.6 Responsive Design

- Desktop: full sidebar, multi-column layouts
- Tablet: collapsible sidebar, single-column content
- Mobile: bottom navigation, stacked layouts, touch-friendly controls
- Critical actions (change title, toggle shield mode) accessible in 2 taps on mobile

### 21.7 Tools Inspired by twitch-tools.rootonline.de

Features to integrate that Twitch doesn't provide natively:

| Tool | Implementation |
|------|---------------|
| **Follower management** | Bulk follower viewer with search, filter by follow date, remove bot followers |
| **Chat log search** | Full-text search across all chat messages, filter by user/date/content |
| **Emote browser** | View all channel emotes (Twitch + BTTV + FFZ + 7TV) in one place |
| **User lookup** | Click any username -> see account age, follow date, message count, ban history, all in a sidebar panel |
| **Mod action history** | Searchable log of all bans, timeouts, message deletions with who did what and when |
| **Blocked terms manager** | Add/remove/test blocked terms with regex preview |
| **Bot detection** | Flag suspicious followers/chatters based on account age, username patterns |
| **Clip browser** | View, search, and manage clips without leaving the dashboard |

---
