## 23. Launch Blockers

### 23.1 Spotify Extended Quota Mode

Spotify Dev Mode limits apps to **5 users**. We cannot launch multi-channel Spotify support without Extended Quota Mode approval. This can take **2-6 weeks**.

**Action**: Apply immediately at the Spotify Developer Dashboard. Required: app description, privacy policy URL, terms of service URL, explanation of how the app promotes artist discovery.

### 23.2 Twitch Application Verification

For more than ~100 concurrent API users, Twitch may require app verification. Not an immediate blocker but apply early.

### 23.3 Discord Bot Verification

Discord requires bot verification when a bot is in 75+ servers. Apply when approaching that threshold.

---

### Critical Files for Implementation
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\Twitch\TwitchChatService.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\Twitch\TwitchCommandService.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Database\AppDbContext.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\ServiceResolver.cs`
- `c:\Projects\StoneyEagle\nomercy-bot\src\NoMercyBot.Services\Twitch\TwitchWebsocketHostedService.cs`
