## 7. Channel Management

### 7.1 Dashboard Structure

After login, the dashboard shows "Your Channel" as the primary view if the user is a channel owner. A dropdown allows switching between channels the user has access to (as owner, editor, or moderator).

### 7.2 Connecting Integrations

- **Spotify**: Owner clicks "Connect Spotify" which initiates Spotify OAuth (Authorization Code Flow with PKCE). Callback stores access + refresh tokens in `Service(Name="Spotify", BroadcasterId=channelId)`. The SpotifyPollingService starts monitoring playback when the channel goes live.
- **Discord**: Same pattern with Discord OAuth.
- **OBS**: Owner enters OBS WebSocket host and password. Stored in `Service(Name="OBS", BroadcasterId=channelId)`.

### 7.3 Managing Commands

- Dashboard shows commands for the selected channel.
- Platform commands (from script files) are shown as read-only with a "Platform" badge.
- Custom commands (from DB) are editable.
- Changes go to `/api/channels/{channelId}/commands` and only affect that channel's registry.

### 7.4 Managing Rewards

- Similar to commands. Platform reward scripts are read-only.
- Custom rewards are per-channel and managed via the Twitch API using the channel's OAuth grant token (which the broadcaster explicitly authorized with `channel:manage:redemptions` scope).

### 7.5 Inviting Moderators

- Owner goes to Channel Settings > Team.
- Enters a Twitch username.
- System looks up the user (via TwitchApiService), creates a `ChannelModerator(ChannelId, UserId, Role="moderator")` record.
- The moderator can now log in and see the channel in their channel switcher.

---
