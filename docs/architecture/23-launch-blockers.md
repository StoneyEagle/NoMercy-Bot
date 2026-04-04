## 23. Launch Blockers

### 23.1 Spotify Extended Quota Mode

Spotify Dev Mode limits apps to **5 users**. We cannot launch multi-channel Spotify support without Extended Quota Mode approval. This can take **2-6 weeks**.

**Action**: Apply immediately at the Spotify Developer Dashboard. Required: app description, privacy policy URL, terms of service URL, explanation of how the app promotes artist discovery. Note: Spotify's commercial use policy may require additional compliance for paid applications.

### 23.2 Spotify API Rate Limits

Spotify allows ~180 requests/minute per app. At 3s polling per live channel, each channel uses ~20 req/min. This caps the platform at **~9 concurrent live channels** with Spotify. The Platform tier ($30/mo) allows 10 channels per customer -- a single customer could exhaust the entire budget.

**Action**: Investigate whether rate limits are per-app or per-user-token. If per-app, this is a hard scaling ceiling. Mitigations: increase poll interval (5-10s), only poll when someone is actively viewing the now-playing widget, request rate limit increase from Spotify.

### 23.3 Privacy Policy

Required by Twitch Developer Agreement, Spotify Developer Terms, and Discord Developer Terms before app approval. Also required by GDPR for EU users.

**Action**: Create and publish privacy policy and terms of service at `https://nomercy.tv/privacy` and `https://nomercy.tv/terms`. Must cover all data listed in section 24.

### 23.4 Twitch Application Verification

For more than ~100 concurrent API users, Twitch may require app verification. Not an immediate blocker but apply early.

### 23.5 Discord Bot Verification

Discord requires bot verification when a bot is in 75+ servers. Apply when approaching that threshold.

---
