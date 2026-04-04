## 22. Billing and Sustainability

### 22.1 Philosophy

Keep it as cheap as possible. The bot should be usable for free with reasonable limits. Revenue covers infrastructure costs, not profit maximization. The platform NEVER pays for per-channel costs that scale with usage (TTS, music API calls) -- those are either free-tier providers or BYOK (Bring Your Own Key).

### 22.2 TTS Cost Model -- BYOK

**Problem**: Azure TTS costs ~$16/1M characters. "Unlimited TTS" on any tier would bankrupt the platform. A single abusive user could generate millions of characters.

**Solution**: Two-tier TTS approach:

| Provider | Cost | Quality | Availability |
|----------|------|---------|-------------|
| **Edge TTS** | Free (browser-based synthesis) | Good | Default for ALL tiers. No API key needed. |
| **Azure TTS** | ~$16/1M chars | Excellent | BYOK only. Broadcaster provides their own Azure Speech key in dashboard. |
| **Google TTS** | ~$16/1M chars | Excellent | BYOK only. Same pattern. |
| **ElevenLabs** | Varies | Premium | BYOK only. |

Edge TTS is the **default and only platform-provided TTS**. It's free, decent quality, and already implemented. Any paid TTS provider requires the broadcaster to enter their own API key. The platform never touches Azure/Google billing.

**Dashboard UI**: Integrations > TTS > "Using Edge TTS (free). Want better voices? Add your Azure Speech API key for premium TTS."

### 22.3 Tier Model

| Tier | Price | Limits | Target |
|------|-------|--------|--------|
| **Free** | $0 | 1 channel, basic commands, Edge TTS only, no custom widgets, no Spotify, community support | Small streamers trying it out |
| **Starter** | $5/mo | 1 channel, all commands, Edge TTS + BYOK premium TTS, 3 custom widgets, Spotify, Discord notifications, email support | Growing streamers |
| **Pro** | $15/mo | 3 channels, unlimited widgets, OBS integration, priority support, custom branding (bot name override) | Established streamers |
| **Platform** | $30/mo | 10 channels, everything in Pro, Universe creation (post-MVP), API access, webhook integrations | Multi-channel operators |

### 22.4 Cost Drivers

| Service | Cost Source | Mitigation |
|---------|-----------|------------|
| **TTS** | $0 (Edge TTS is free) | Platform pays nothing. BYOK for premium providers. |
| **PostgreSQL** | ~$15-50/mo managed hosting | Start with a small instance. Scale vertically as needed. |
| **Hosting** | ~$20-50/mo VPS | Single server to start. Horizontal scale later. |
| **Bandwidth** | SignalR + widget overlays | CDN for static assets. SignalR is lightweight text. |
| **Twitch API** | Free (rate limits only) | Respect rate limits, batch where possible. |
| **Spotify API** | Free (rate limits) | Poll interval scales with channel count. |

**Estimated monthly cost at 100 channels**: ~$50-100/mo (no TTS cost!)
**Revenue at 100 channels** (assuming 60% free, 30% Starter, 10% Pro): ~$225/mo

### 22.5 Payment Integration

- **Stripe** for payment processing (standard for SaaS)
- Subscription management via Stripe Customer Portal
- Webhook-driven: Stripe notifies the platform of subscription changes
- **CRITICAL**: All Stripe webhooks MUST validate the webhook signature using the Stripe signing secret. Without this, anyone can forge webhook events and grant themselves paid tiers.
- Grace period: 7 days after failed payment before downgrading
- No data loss on downgrade -- features are disabled, data is preserved

### 22.6 Data Model

Defined in section 2.2.5 (`ChannelSubscriptions` table).

### 22.7 Feature Gating

The `ChannelSubscription.Tier` is checked by a `IFeatureGateService`:

```csharp
public interface IFeatureGateService
{
    bool IsFeatureAvailable(string broadcasterId, string featureKey);
    int GetLimit(string broadcasterId, string limitKey);
}
```

Injected via DI. Every feature that has tier limits checks the gate before executing. Free tier users see "Upgrade to unlock" in the dashboard instead of disabled buttons.

### 22.8 BYOK Key Management

Broadcaster-provided API keys (Azure TTS, Google TTS, ElevenLabs, etc.) are stored in the `Service` table:
- `Name = "AzureTTS"`, `BroadcasterId = channelId`
- `ClientId` = Azure region endpoint
- `ClientSecret` = Azure subscription key (encrypted)

The `ITtsProvider` interface resolves the correct provider and key per-channel via the ChannelRegistry. If no BYOK key exists, Edge TTS is used.

---
