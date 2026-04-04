## 19. Widget & Overlay Authentication

### 19.1 The Problem

OBS browser sources and overlay URLs cannot perform OAuth login flows. They just load a URL. We need a way to authenticate widget SignalR connections without requiring the user to log in from inside OBS.

### 19.2 Channel Secret Token

Each channel gets a unique secret token generated on onboarding:

```
Channel (additional column)
  - OverlayToken: string (UUID v4, unique, indexed)
```

The overlay URL includes this token: `https://bot.nomercy.tv/overlay/widgets/{widgetId}?token={overlayToken}`

### 19.3 Authentication Flow

1. Broadcaster copies their overlay URL from the dashboard (includes the token)
2. OBS browser source loads the URL
3. Widget frontend connects to the SignalR hub with the token as a query parameter
4. `WidgetHub.JoinWidgetGroup()` validates the token:
   - Look up `Channel` by `OverlayToken`
   - Verify the widget belongs to that channel
   - If valid, join the SignalR group
   - If invalid, reject the connection
5. No OAuth, no login, no cookies -- just the URL token

### 19.4 Security Properties

- **Per-channel**: Each channel has its own token. Knowing Channel A's token gives zero access to Channel B.
- **Rotatable**: Broadcaster can regenerate from dashboard Settings > Danger Zone. All existing OBS sources need the new URL.
- **No API access**: The overlay token ONLY authenticates SignalR widget connections. It cannot be used to call any API endpoint.
- **Scope-limited**: The token only allows receiving events for widgets belonging to that channel. It cannot send commands, modify settings, or access any data.
- **IP whitelist (optional)**: Broadcaster can optionally restrict overlay connections to specific IPs from dashboard Settings. If set, both the token AND the IP must match. Useful for extra security but not required.

### 19.5 Dashboard UI

In Channel Settings:
- "Overlay Token" section showing the current token (masked, with copy button)
- "Regenerate Token" button with confirmation warning
- Optional "Allowed IPs" field (comma-separated, empty = any IP)
- "Copy Overlay URL" buttons next to each widget that include the token automatically

---
