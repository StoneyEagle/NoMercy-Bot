## 14. Music Provider Architecture

### 14.1 Interface Design

```
IMusicProvider (registered per provider type)
  - string ProviderName { get; }
  - bool IsConnected(string broadcasterId)
  - Task<MusicTrack?> GetCurrentlyPlayingAsync(string broadcasterId)
  - Task<MusicQueue> GetQueueAsync(string broadcasterId)
  - Task<bool> AddToQueueAsync(string broadcasterId, string trackUri)
  - Task<MusicTrack?> SearchTrackAsync(string query)
  - Task<MusicTrack?> GetTrackAsync(string trackId)
  - Task<bool> SkipAsync(string broadcasterId)
  - Task<bool> PauseAsync(string broadcasterId)
  - Task<bool> ResumeAsync(string broadcasterId)
  - Task<bool> SetVolumeAsync(string broadcasterId, int percent)
```

### 14.2 Provider Registration

```
IMusicProviderFactory (singleton)
  - GetProvider(string broadcasterId) -> IMusicProvider?
  // Returns the active music provider for the channel
  // Looks up which provider the broadcaster connected (Spotify, YouTube Music, etc.)
```

### 14.3 API Routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/channels/{channelId}/music/now-playing` | Current track |
| GET | `/api/channels/{channelId}/music/queue` | Current queue |
| POST | `/api/channels/{channelId}/music/queue` | Add to queue (body: `{query}` or `{uri}`) |
| POST | `/api/channels/{channelId}/music/skip` | Skip track |
| POST | `/api/channels/{channelId}/music/pause` | Pause |
| POST | `/api/channels/{channelId}/music/resume` | Resume |
| PUT | `/api/channels/{channelId}/music/volume` | Set volume |
| GET | `/api/channels/{channelId}/music/search?q=...` | Search tracks |

### 14.4 Spotify Implementation

`SpotifyMusicProvider : IMusicProvider` wraps the existing `SpotifyApiService` using official Spotify OAuth tokens (Authorization Code Flow with PKCE). Playback state monitoring uses `SpotifyPollingService` (polling the official REST API), replacing the removed undocumented websocket hack entirely.

### 14.5 YouTube Music (Future)

`YouTubeMusicProvider : IMusicProvider` will be added when YouTube Music support is implemented. The `IMusicProvider` interface is designed to accommodate it without changes to commands or API routes.

---

---
