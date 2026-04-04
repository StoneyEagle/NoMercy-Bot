## 17. Cross-Channel Universe System (POST-MVP)

**This entire section is deferred to post-MVP.** The multi-channel platform ships without the Universe system. The Lucky Feather stays as a hardcoded platform reward initially. The Universe system is designed here so the architecture doesn't prevent it, but implementation happens after the core platform is stable with real users.

### 17.1 Concept

A **Universe** is a shared game or event system that spans multiple channels. Any user can create a universe. Streamers opt in to participate. When a viewer interacts with the universe in any participating channel, it affects the shared state.

Example: The Lucky Feather game. A feather exists in a "universe". Three streamers opt in. A viewer in Channel A steals the feather. Viewers in Channel B and C see the theft on their overlays. A viewer in Channel B can steal it next. The feather travels across channels.

### 17.2 Design Principles

1. **Generic** -- Not hardcoded to any specific game. The universe system is a framework.
2. **Versioned** -- Universe definitions have semver versions. Updates don't break running instances.
3. **Opt-in** -- Streamers explicitly join a universe. They can leave at any time.
4. **User-created** -- Any platform user can create a universe definition.
5. **Sandboxed** -- Universe logic cannot access channel-specific data (tokens, settings) outside its scope.

### 17.3 Data Model

#### Universe Definition (the template/blueprint)

```
Universes
  - Id: Ulid (PK)
  - Slug: string (unique, URL-friendly, e.g. "lucky-feather")
  - Name: string ("Lucky Feather")
  - Description: string
  - Version: string (semver, e.g. "1.2.0")
  - CreatorUserId: string (FK to User -- who made this)
  - IsPublished: bool (false = draft, only creator can test)
  - IsApproved: bool (false = pending review, true = visible in marketplace)
  - StateSchema: JSON Schema (defines the shape of the universe's shared state)
  - DefaultState: JSON (initial state when a channel joins)
  - RewardTriggers: JSON (what reward names/actions activate universe logic)
  - CommandTriggers: JSON (what commands activate universe logic)
  - EventHandlerScript: string (sandboxed logic script -- see 18.5)
  - WidgetTemplateId: string? (optional widget for displaying universe state)
  - CreatedAt, UpdatedAt
```

#### Universe Version History

```
UniverseVersions
  - Id: Ulid (PK)
  - UniverseId: Ulid (FK to Universes)
  - Version: string (semver)
  - StateSchema: JSON Schema
  - DefaultState: JSON
  - RewardTriggers: JSON
  - CommandTriggers: JSON
  - EventHandlerScript: string
  - ChangeLog: string
  - PublishedAt: DateTime
  - CreatedAt
```

#### Channel Universe Participation

```
ChannelUniverses
  - Id: Ulid (PK)
  - BroadcasterId: string (FK to Channel)
  - UniverseId: Ulid (FK to Universes)
  - UniverseVersion: string (pinned version -- broadcaster controls when to upgrade)
  - IsActive: bool
  - ChannelState: JSON (this channel's local state within the universe, if any)
  - JoinedAt: DateTime
  - CreatedAt, UpdatedAt
  - Unique: (BroadcasterId, UniverseId)
```

#### Universe Shared State

```
UniverseState
  - Id: Ulid (PK)
  - UniverseId: Ulid (FK to Universes, unique)
  - State: JSON (the global shared state -- e.g., who holds the feather)
  - LastModifiedBy: string (broadcasterId of last channel that changed state)
  - LastModifiedAt: DateTime
  - UpdatedAt
```

#### Universe Event Log

```
UniverseEvents
  - Id: Ulid (PK)
  - UniverseId: Ulid (FK to Universes)
  - BroadcasterId: string (FK to Channel -- which channel triggered this)
  - UserId: string (FK to User -- which user triggered this)
  - EventType: string (e.g. "steal", "transfer", "reset")
  - EventData: JSON
  - CreatedAt
  - Index: (UniverseId, CreatedAt)
```

### 17.4 How It Works -- Flow

1. **Creator publishes a universe** (e.g., "Lucky Feather v1.0"):
   - Defines state schema: `{ "holderId": "string", "holderName": "string", "cost": "int", "stealCount": "int" }`
   - Defines default state: `{ "holderId": null, "holderName": null, "cost": 100, "stealCount": 0 }`
   - Defines reward trigger: reward title matching "Lucky Feather" activates the `onRewardRedeemed` handler
   - Writes the event handler script (sandboxed -- see 18.5)
   - Optionally creates a widget template for overlays

2. **Streamers A, B, C opt in**:
   - From their dashboard, they browse the Universe Marketplace
   - Click "Join" on Lucky Feather
   - Pin to version 1.0
   - The bot auto-creates a Twitch channel point reward named "Lucky Feather" in their channel (or the streamer creates one manually with that name)

3. **Viewer in Channel A redeems "Lucky Feather"**:
   - Bot detects reward redemption
   - Checks: is this reward name a trigger for any universe this channel participates in?
   - Yes -- Lucky Feather universe, trigger: `onRewardRedeemed`
   - Loads the universe's shared state from `UniverseState`
   - Executes the sandboxed event handler script with context: `{ sharedState, channelState, user, channel, rewardInput }`
   - Script returns: `{ newSharedState, newChannelState, events: [{ type: "steal", ... }], chatMessage: "...", widgetEvent: { ... } }`
   - Bot atomically updates `UniverseState`
   - Bot logs to `UniverseEvents`
   - Bot sends chat message in Channel A
   - Bot publishes widget event to ALL channels in this universe (A, B, C)
   - Overlays in all three channels update

4. **Viewer in Channel B steals it next**: Same flow, different channel.

### 17.5 Sandboxed Event Handler Scripts

Universe logic runs in a **sandboxed environment** with NO access to:
- Database directly
- Service credentials / tokens
- Other channels' private data
- File system
- Network

The script receives a **context object** and returns an **action result**:

**Input context:**
```
UniverseEventContext
  - SharedState: dynamic (the universe's global state)
  - ChannelState: dynamic (this channel's local state)
  - TriggerType: string ("reward", "command", "timer")
  - User: { Id, DisplayName, Color }
  - Channel: { Id, Name, DisplayName }
  - Input: string? (reward user input or command arguments)
  - AllParticipatingChannels: { Id, Name, DisplayName }[] (read-only list)
```

**Output result:**
```
UniverseActionResult
  - SharedState: dynamic (updated global state, or null for no change)
  - ChannelState: dynamic (updated channel state, or null for no change)
  - ChatMessage: string? (message to send in the triggering channel)
  - BroadcastChatMessage: string? (message to send in ALL participating channels)
  - WidgetEvent: { type, data }? (event to publish to all universe widgets)
  - RefundReward: bool (if true, refund the channel point redemption)
  - LogEvent: { type, data }? (event to record in UniverseEvents)
```

**Execution environment:** Either:
- A restricted C# Roslyn script with limited imports (no System.IO, no System.Net, no reflection)
- Or a lightweight expression/rules engine (simpler but less flexible)

Recommendation: Start with Roslyn with a strict allowlist of namespaces. The script is compiled once per version and cached.

### 17.6 Universe Marketplace

- Browse published & approved universes
- Search by name, tags, popularity
- View: description, version history, changelog, participating channels count
- "Join" button to opt in
- Version management: see current version, available upgrades, changelog
- Creator tools: create, edit, publish, view analytics

### 17.7 Universe Lifecycle

| State | Visibility | Who Can Join |
|-------|-----------|-------------|
| **Draft** | Creator only | Creator only (for testing) |
| **Published** | Everyone can see | Nobody (awaiting approval) |
| **Approved** | Everyone can see | Anyone can join |
| **Deprecated** | Existing participants only | Nobody new |
| **Archived** | Nobody | Nobody (frozen) |

### 17.8 Lucky Feather Migration

The current hardcoded Lucky Feather system (`LuckyFeather.cs`, `LuckyFeatherChange.cs`, `LuckyFeatherTimerService.cs`, `LuckyFeatherWidget.cs`) becomes the **first published universe**:
- Extract the logic into a universe event handler script
- Extract the widget into a universe widget template
- Remove the hardcoded reward GUID -- match by reward title instead
- Remove `LuckyFeatherTimerService` -- timer logic becomes part of the universe framework (universes can define timer-based state transitions)
- Existing feather state migrates to `UniverseState`

### 17.9 Implementation Phase

The Universe system is a **Phase 5+** feature. It depends on:
- Multi-channel being fully operational (Phase 5)
- Widget creator system (Section 16)
- Reward title-based matching (Section 13.5)

---
