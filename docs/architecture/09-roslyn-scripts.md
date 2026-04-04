## 9. Platform Commands and Rewards (Compiled C#, No Roslyn)

### 9.1 Platform Commands (shared across all channels)

Platform commands are **regular C# classes** that implement `IBotCommand`, compiled with the project via `dotnet build`. The Roslyn script loading system (`CommandScriptLoader`) is **removed entirely**.

**Why**: Roslyn script loading was a development convenience in the single-user bot. For a production platform:
- Regular classes have full IDE support (intellisense, refactoring, debugging)
- They're testable with xUnit
- They're type-safe at compile time
- No runtime compilation overhead or security surface
- No file-system dependency for script discovery

**Registration**: Platform commands are registered via DI in `ServiceCollectionExtensions`:
```csharp
services.AddSingleton<IBotCommand, HugCommand>();
services.AddSingleton<IBotCommand, RoastCommand>();
// ... etc
```

When a channel is onboarded, all registered `IBotCommand` instances are added to that channel's `CommandRegistry`.

### 9.2 Per-Channel Custom Commands

User-created commands are stored in the `Command` table with `BroadcasterId`. They use the pipeline action system (section 16) -- no compilation, just JSON interpretation at runtime.

### 9.3 Command Execution Context

The `CommandContext` contains everything a command needs:
- `BroadcasterId` -- which channel this is executing in
- `Channel` -- channel name
- `Message` -- the chat message that triggered it
- `Arguments` -- parsed arguments
- `ServiceProvider` -- DI container for resolving any service
- `TwitchChatService`, `TwitchApiService`, `TtsService` -- commonly used services injected directly

Platform commands access services via the context. Pipeline commands access services via the `ICommandAction` implementations.

### 9.4 Platform Rewards

Same pattern. Platform reward handlers are regular C# classes implementing `IReward`, registered via DI, compiled with the project. The `RewardScriptLoader` is removed.

User-created reward handlers use the same pipeline action system as commands.

### 9.5 Files to Remove

- `src/NoMercyBot.Services/Twitch/Scripting/CommandScriptLoader.cs` -- replaced by DI registration
- `src/NoMercyBot.Services/Twitch/Scripting/RewardScriptLoader.cs` -- replaced by DI registration
- `src/NoMercyBot.Services/Widgets/WidgetScriptLoader.cs` -- replaced by DI registration
- All Roslyn-specific NuGet packages (if not needed elsewhere)

---
