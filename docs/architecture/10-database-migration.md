## 10. Database Migration Strategy

### 10.0 SQLite to PostgreSQL Migration

This happens FIRST in Phase 2, before any schema changes. SQLite's single-writer lock will not survive multi-channel concurrent writes (chat messages, EventSub events, token refreshes all writing simultaneously).

**Migration steps**:
1. Add PostgreSQL NuGet package: `Npgsql.EntityFrameworkCore.PostgreSQL`
2. Update `AppDbContext.OnConfiguring` to use `UseNpgsql()` with connection string from environment variable `DATABASE_URL`
3. Keep SQLite as fallback for local development: `if (env == "Development" && no DATABASE_URL) UseSqlite()` else `UseNpgsql()`
4. Export existing SQLite data to PostgreSQL using `pgloader` or a custom EF Core script that reads from SQLite context and writes to PostgreSQL context
5. Run `dotnet ef migrations add PostgreSQLInitial` to generate the PostgreSQL-compatible migration
6. Verify all existing data is intact
7. Update Docker/deployment config with PostgreSQL connection string

**Why now and not later**: Doing schema changes (adding BroadcasterId to 13 tables, new indexes, new tables) on SQLite and then migrating to PostgreSQL means doing it twice. One migration, one database engine.

### 10.1 Schema Migration Plan

**Step 1: Schema migration**
Create an EF Core migration that:
1. Adds all new columns (`BroadcasterId` on Command, Reward, EventSubscription, Widget, Configuration, Storage, Record, Service, UserTtsVoice, TtsUsageRecord; `IsOnboarded`, `BotJoinedAt`, `SubscriptionTier` on Channel; `Role`, `GrantedAt`, `GrantedBy` on ChannelModerator).
2. Creates the `ChannelBotAuthorizations` table.
3. Adds new indexes.
4. Drops old unique indexes, creates new composite ones.

**Step 2: Data migration**
A data migration script that:
1. Finds the existing single broadcaster from `Service WHERE Name = 'Twitch' AND BroadcasterId IS NULL`.
2. Sets that broadcaster's Twitch user ID as `BroadcasterId` on all existing rows that need it:
   - All `Command` rows get `BroadcasterId = existingBroadcasterId`
   - All `Reward` rows get `BroadcasterId = existingBroadcasterId`
   - All `EventSubscription` rows get `BroadcasterId = existingBroadcasterId`
   - All `Widget` rows get `BroadcasterId = existingBroadcasterId`
   - All `Record` rows get `BroadcasterId = existingBroadcasterId`
   - `Configuration` rows for channel-specific config get `BroadcasterId = existingBroadcasterId`; platform config stays null.
   - `Storage` rows get `BroadcasterId = existingBroadcasterId`
   - `UserTtsVoice` rows get `BroadcasterId = existingBroadcasterId`
   - `TtsUsageRecord` rows get `BroadcasterId = existingBroadcasterId`
3. Creates `ChannelModerator(ChannelId = existingBroadcasterId, UserId = existingBroadcasterId, Role = "owner")`.
4. Creates `Service(Name = "Twitch", BroadcasterId = existingBroadcasterId)` with the existing tokens, and clears the tokens from the global Service row (keeping only ClientId/ClientSecret on the global row).
5. Similarly splits Spotify/Discord/OBS service rows.
6. Sets `Channel.IsOnboarded = true` for the existing channel.

**Step 3: NOT NULL enforcement**
After data migration populates all values, a subsequent migration makes `BroadcasterId` NOT NULL on tables where it is required.

### 10.2 Rollback Plan

- Before migration, export a full SQLite backup (`cp nomercy.db nomercy.db.backup`).
- Each EF Core migration is reversible via `dotnet ef migrations remove` or `dotnet ef database update <previous-migration>`.
- The data migration script stores the "pre-migration state" flag in a Configuration row, allowing a rollback script to undo the data transformation.

---
