## 9. Roslyn Script System

### 9.1 Platform Scripts (shared across all channels)

Files in `src/NoMercyBot.CommandsRewards/commands/*.cs` are platform commands. They are compiled once at startup and registered in every channel's command registry.

**Current behavior**: `CommandScriptLoader.LoadAllAsync()` loads .cs files, evaluates them via Roslyn, and registers them with the global `TwitchCommandService`.

**Multi-channel change**:
- `CommandScriptLoader` compiles scripts once and stores the resulting `IBotCommand` instances in a list.
- When a new channel is onboarded, these pre-compiled commands are registered in that channel's `CommandRegistry`.
- The `CommandScriptContext` already includes `BroadcasterId`, `Channel`, `DatabaseContext`, `ServiceProvider`, so scripts naturally operate in the correct channel context when invoked.

### 9.2 Per-Channel Custom Commands

Database-backed commands (the `Command` table) are per-channel after adding `BroadcasterId`. Each channel loads only its own commands from the database.

### 9.3 Script Execution Context

The `CommandScriptContext` (file: `src/NoMercyBot.Services/Twitch/Scripting/CommandScriptContext.cs`) already contains:
- `BroadcasterId`
- `Channel` (channel name)
- `DatabaseContext`
- `TwitchChatService`
- `TwitchApiService`
- `ServiceProvider`

No structural changes needed. The caller must populate these correctly per-channel.

### 9.4 Reward Scripts

Same pattern as commands. Platform reward scripts in `src/NoMercyBot.CommandsRewards/rewards/*.cs` are compiled once and registered in every channel. The `RewardScriptContext` already has `BroadcasterId`.

---
