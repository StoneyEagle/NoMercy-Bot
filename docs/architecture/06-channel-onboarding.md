## 6. Channel Onboarding Flow

Step-by-step when a new broadcaster signs up:

1. **Twitch OAuth callback** fires. System creates/updates `User` and `Channel` records. Stores the OAuth grant token (explicitly authorized by the user) in `Service(Name="Twitch", BroadcasterId=userId)`. A `ChannelFeatures` record is created with only the onboarding features enabled.

2. **No ChannelModerator row needed for the broadcaster** -- ownership is implicit (channelId == userId).

3. **Channel.IsOnboarded = false**. Dashboard shows the onboarding wizard.

4. **Bot authorization**: The system generates a device code for `channel:bot` scope. The broadcaster authorizes, allowing the bot to chat in their channel with the bot badge. A `ChannelBotAuthorization` record is created.

5. **Default EventSub subscriptions created**: Based on a predefined list of essential events (channel.chat.message, channel.follow, stream.online, stream.offline, channel.subscribe, channel.raid, channel.channel_points_custom_reward_redemption.add, etc.). These are registered with Twitch and stored in `EventSubscription` table.

6. **Bot joins channel**: WatchStreakService IRC client joins the new channel. ChannelRegistry creates a new ChannelContext.

7. **Default commands loaded**: Platform script commands (from `src/NoMercyBot.CommandsRewards/commands/`) are registered in the new channel's command registry. No database rows needed -- these are loaded from disk.

8. **Channel.IsOnboarded = true**. Dashboard shows the full management interface.

9. **Optional integrations**: Broadcaster can later connect Spotify, Discord, and OBS through their Channel Settings.

---
