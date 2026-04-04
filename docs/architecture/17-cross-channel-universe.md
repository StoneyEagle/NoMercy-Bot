## 17. Cross-Channel Universe System (POST-MVP)

**This entire section is deferred to post-MVP.** The multi-channel platform ships without the Universe system. The Lucky Feather stays as a hardcoded platform reward initially. The Universe system is designed here so the architecture doesn't prevent it, but implementation happens after the core platform is stable with real users.

### 17.1 Concept

A **Universe** is a shared game or event system that spans multiple channels. There are two levels:

- **Universe Template** -- The blueprint/plugin (e.g., "Lucky Feather"). Created by any user. Published to the marketplace. Defines the game logic, state schema, triggers.
- **Universe Instance** -- A running copy of a template, created by a broadcaster. The broadcaster who creates the instance **controls who can join**. They invite specific channels. It's not "install plugin and you're in a global game" -- it's "install plugin, create YOUR instance, invite YOUR friends."

Example: StoneyEagle installs the Lucky Feather template and creates an instance. He invites Bamo16 and kani_dev to join. Now the feather travels between those 3 channels only. Meanwhile, another group of streamers creates their OWN Lucky Feather instance with their own channels. The two instances are completely independent -- separate state, separate participants.

### 17.2 Design Principles

1. **Generic** -- Not hardcoded to any specific game. The universe system is a framework.
2. **Versioned** -- Universe templates have semver versions. Updates don't break running instances.
3. **Instance-based** -- Each install creates an independent instance. One template, many instances.
4. **Owner-controlled** -- The instance creator controls who can join. Channels must be invited and accept.
5. **User-created** -- Any platform user can create a universe template.
6. **Sandboxed** -- Universe logic cannot access channel-specific data (tokens, settings) outside its scope.

### 17.3 Data Model

#### Universe Template (the blueprint/plugin)

```
UniverseTemplates
  - Id: Ulid (PK)
  - Slug: string (unique, URL-friendly, e.g. "lucky-feather")
  - Name: string ("Lucky Feather")
  - Description: string
  - Version: string (semver, e.g. "1.2.0")
  - CreatorUserId: string (FK to User -- who made this)
  - IsPublished: bool (false = draft, only creator can test)
  - IsApproved: bool (false = pending review, true = visible in marketplace)
  - StateSchema: JSON Schema (defines the shape of the shared state)
  - DefaultState: JSON (initial state when an instance is created)
  - RewardTriggers: JSON (what reward names/actions activate logic)
  - CommandTriggers: JSON (what commands activate logic)
  - EventHandlerScript: string (sandboxed logic)
  - WidgetTemplateId: string? (optional widget for displaying state)
  - CreatedAt, UpdatedAt
```

#### Universe Template Versions

```
UniverseTemplateVersions
  - Id: Ulid (PK)
  - TemplateId: Ulid (FK to UniverseTemplates)
  - Version: string (semver)
  - StateSchema, DefaultState, RewardTriggers, CommandTriggers, EventHandlerScript: (same as template)
  - ChangeLog: string
  - PublishedAt: DateTime
  - CreatedAt
```

#### Universe Instance (a running copy, owned by a broadcaster)

```
UniverseInstances
  - Id: Ulid (PK)
  - TemplateId: Ulid (FK to UniverseTemplates)
  - TemplateVersion: string (pinned version -- owner controls when to upgrade)
  - OwnerBroadcasterId: string (FK to Channel -- who created this instance)
  - Name: string (custom name, e.g. "Stoney & Friends Feather")
  - JoinPolicy: string ("invite_only", "request_to_join", "open")
  - IsActive: bool
  - CreatedAt, UpdatedAt
```

#### Universe Instance Membership (who's in this instance)

```
UniverseInstanceMembers
  - Id: Ulid (PK)
  - InstanceId: Ulid (FK to UniverseInstances)
  - BroadcasterId: string (FK to Channel)
  - Status: string ("invited", "accepted", "declined", "removed")
  - InvitedBy: string (FK to User)
  - InvitedAt: DateTime
  - AcceptedAt: DateTime?
  - ChannelState: JSON (this channel's local state within the instance)
  - CreatedAt, UpdatedAt
  - Unique: (InstanceId, BroadcasterId)
```

#### Universe Instance State (shared across all members)

```
UniverseInstanceState
  - Id: Ulid (PK)
  - InstanceId: Ulid (FK to UniverseInstances, unique)
  - State: JSON (the shared state -- e.g., who holds the feather)
  - LastModifiedBy: string (broadcasterId of last channel that changed state)
  - LastModifiedAt: DateTime
  - UpdatedAt
```

#### Universe Event Log

```
UniverseEvents
  - Id: Ulid (PK)
  - InstanceId: Ulid (FK to UniverseInstances)
  - BroadcasterId: string (FK to Channel -- which channel triggered this)
  - UserId: string (FK to User -- which user triggered this)
  - EventType: string (e.g. "steal", "transfer", "reset")
  - EventData: JSON
  - CreatedAt
  - Index: (InstanceId, CreatedAt)
```

### 17.4 How It Works -- Flow

1. **Template creator publishes "Lucky Feather v1.0"** to the marketplace:
   - Defines state schema, default state, reward triggers, event handler script
   - Gets approved by platform moderators

2. **StoneyEagle installs the template**:
   - Browses marketplace, clicks "Install" on Lucky Feather
   - Creates a **new instance** named "Stoney & Friends Feather"
   - Chooses join policy: "Invite Only"
   - StoneyEagle is automatically the first member (status: "accepted")

3. **StoneyEagle invites Bamo16 and kani_dev**:
   - From dashboard: Universes > "Stoney & Friends Feather" > Members > Invite
   - Searches for Bamo16, sends invite
   - Bamo16 sees the invite in their dashboard, accepts
   - kani_dev accepts too
   - Now 3 channels are members of this instance

4. **Viewer in StoneyEagle's channel redeems "Lucky Feather"**:
   - Bot detects reward redemption
   - Checks: is this reward name a trigger for any universe instance this channel is a member of?
   - Yes -- "Stoney & Friends Feather" instance, trigger: `onRewardRedeemed`
   - Loads the instance's shared state from `UniverseInstanceState`
   - Executes the sandboxed event handler with context
   - Bot atomically updates `UniverseInstanceState`
   - Bot sends chat message in StoneyEagle's channel
   - Bot publishes widget event to ALL members of this instance (Stoney, Bamo, kani)
   - Overlays in all three channels update

5. **Viewer in Bamo16's channel steals it next**: Same flow, same instance.

6. **Meanwhile**: Another group of streamers can install the same Lucky Feather template and create their OWN instance with their OWN members. Completely independent state.

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

- Browse published & approved templates
- Search by name, tags, popularity, active instance count
- View: description, version history, changelog, how many instances are running
- "Install" button creates a new instance (not joining someone else's)
- Version management: see current version, available upgrades, changelog per instance
- Creator tools: create template, edit, publish, view analytics

### 17.7 Template Lifecycle

| State | Visibility | Can Install |
|-------|-----------|-------------|
| **Draft** | Creator only | Creator only (for testing) |
| **Published** | Everyone can see | Nobody (awaiting approval) |
| **Approved** | Everyone can see | Anyone can install and create instances |
| **Deprecated** | Existing instances only | Nobody new |
| **Archived** | Nobody | Nobody (frozen, existing instances keep running) |

### 17.7.1 Instance Access Control

| Join Policy | How Channels Join |
|-------------|-------------------|
| **Invite Only** | Instance owner sends invites. Channel must accept. Default. |
| **Request to Join** | Any channel can request. Instance owner approves/denies. |
| **Open** | Any channel can join immediately. Owner can still kick. |

Instance owner can always:
- Invite channels
- Remove channels
- Transfer ownership to another member
- Close the instance (soft delete, state preserved for 90 days)

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
