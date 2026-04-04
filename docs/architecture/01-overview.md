## 1. Overview

### 1.1 Vision

NoMercyBot is evolving from a single-broadcaster Twitch bot into a multi-channel platform. One server instance serves many broadcasters simultaneously. Each broadcaster signs up via Twitch OAuth, connects their own Spotify/Discord/OBS integrations, configures commands, rewards, widgets, and event subscriptions independently.

### 1.2 Core Architecture

- **Deployment model**: One server process, many channels.
- **Database**: Migrate from SQLite to PostgreSQL in Phase 2 alongside the schema restructuring. Multi-channel concurrent writes will break SQLite's single-writer lock.
- **Service model**: Services that currently hold single-broadcaster state in static fields or singletons become channel-aware via a ChannelRegistry pattern.

### 1.3 Naming Conventions

| Context | Term | Example |
|---------|------|---------|
| User-facing (dashboard, UI, docs) | **Channel** | "Your Channel", "Channel Settings" |
| Code / Database columns | **BroadcasterId** | `string BroadcasterId`, FK column names |
| API routes | **channels** | `/api/channels/{channelId}/commands` |
| Twitch API alignment | **Broadcaster** | Matches Twitch Helix terminology |

The word "Tenant" is never used anywhere.

### 1.4 Architectural Principles

These apply to ALL code written for the platform:

**1. Dependency Injection everywhere, but no MediatR.** No `new Service()` or static singletons. Every service is registered in DI and injected. This allows swapping implementations (e.g., `IMusicProvider` has `SpotifyMusicProvider` and future `YouTubeMusicProvider`), testing with mocks, and per-channel resolution via the ChannelRegistry. We do NOT use MediatR/CQRS -- it adds unnecessary indirection and complexity for a bot platform. Services call services directly via interfaces. Domain events use a simple `IEventBus` with `IEventHandler<T>` implementations registered in DI, not MediatR's pipeline behaviors.

**2. Provider/interface pattern for all integrations.** Every external integration is behind an interface:
- `IMusicProvider` -- Spotify, YouTube Music (future)
- `ITtsProvider` -- Azure, Edge, Google (already implemented)
- `IChatProvider` -- Twitch (could support YouTube Live chat in future)
- `IStreamNotificationProvider` -- Discord (could support Telegram, Slack in future)
- `IStreamControlProvider` -- OBS (could support Streamlabs in future)

New providers are added by implementing the interface and registering in DI. Zero changes to consuming code.

**3. Event-driven with hooks for extensibility.** All significant actions publish events through `IWidgetEventService` (or a future `IEventBus`). This is the integration point for the Universe system (post-MVP): when a reward is redeemed, the event bus fires, and Universe handlers can listen without modifying the reward processing code. Design every feature with "what if something else needs to react to this?" in mind.

**4. Universe-ready hooks (baked in, not implemented).** The following extension points must exist in the architecture even though the Universe system is post-MVP:
- Reward redemption pipeline: `OnBeforeRewardProcessed`, `OnAfterRewardProcessed` hooks
- Command execution pipeline: `OnBeforeCommandExecuted`, `OnAfterCommandExecuted` hooks
- A generic `IEventHandler<TEvent>` pattern that additional systems can subscribe to
- Shared state storage pattern (the `UniverseState` table can be added later, but the `Record` table will support arbitrary JSON per-user-per-channel after the BroadcasterId column is added in Phase 2)

These hooks cost nothing to implement (just fire events at the right places) but save a complete rewrite when the Universe system ships.

**5. No `new AppDbContext()`.** Every database access goes through DI-injected `AppDbContext` or `IDbContextFactory<AppDbContext>`. The two existing violations (`ChatMessage.cs` constructor and `DiscordAuthService.cs`) are fixed in Phase 2.

**6. Soft deletes.** Entities are never hard-deleted in normal operation. All deletable entities have a `DeletedAt` (DateTime?) column. A global query filter in `OnModelCreating` automatically excludes soft-deleted rows: `.HasQueryFilter(e => e.DeletedAt == null)`. Hard deletes only happen for GDPR data erasure requests (section 24) and periodic cleanup of rows soft-deleted more than 90 days ago. Benefits: undo mistakes, audit trail, referential integrity preserved, no FK cascade issues.

**7. Query optimization -- minimize joins, no lazy loading.** EF Core lazy loading is DISABLED globally (`UseLazyLoadingProxies()` is not called). All related data is loaded explicitly via:
- `.Include()` only when the related data is actually needed for the response
- Projection via `.Select()` for API responses (never return full entity graphs)
- Separate queries over joins when the related data is optional (N+1 is acceptable for 1-2 optional includes; joins for required includes)
- Navigation properties exist for schema definition but are NOT populated by default
- Chat message processing (hot path) uses raw SQL or Dapper for performance-critical queries
- ChannelRegistry caches frequently-accessed data (channel config, command registry, permissions) to avoid DB reads on every chat message

---
