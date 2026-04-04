## 22. Billing and Sustainability

### 22.1 Philosophy

Keep it as cheap as possible. The bot should be usable for free with reasonable limits. Revenue covers infrastructure costs, not profit maximization.

### 22.2 Tier Model

| Tier | Price | Limits | Target |
|------|-------|--------|--------|
| **Free** | $0 | 1 channel, basic commands, 500 TTS chars/stream, no custom widgets, no Spotify, community support | Small streamers trying it out |
| **Starter** | $5/mo | 1 channel, all commands, 5000 TTS chars/stream, 3 custom widgets, Spotify, Discord notifications, email support | Growing streamers |
| **Pro** | $15/mo | 3 channels, unlimited TTS, unlimited widgets, OBS integration, priority support, custom branding (bot name override) | Established streamers |
| **Platform** | $30/mo | 10 channels, everything in Pro, Universe creation, API access, webhook integrations | Multi-channel operators |

### 22.3 Cost Drivers

| Service | Cost Source | Mitigation |
|---------|-----------|------------|
| **TTS (Azure)** | ~$16/1M characters | Character limits per tier. Cache aggressively (TtsCacheEntries). Same text+voice = cached audio. |
| **PostgreSQL** | ~$15-50/mo managed hosting | Start with a small instance. Scale vertically as needed. |
| **Hosting** | ~$20-50/mo VPS | Single server to start. Horizontal scale later. |
| **Bandwidth** | SignalR + widget overlays | CDN for static assets. SignalR is lightweight text. |
| **Twitch API** | Free (rate limits only) | Respect rate limits, batch where possible. |
| **Spotify API** | Free (rate limits) | Poll interval scales with channel count. |

**Estimated monthly cost at 100 channels**: ~$100-150/mo
**Revenue at 100 channels** (assuming 60% free, 30% Starter, 10% Pro): ~$225/mo

### 22.4 Payment Integration

- **Stripe** for payment processing (standard for SaaS)
- Subscription management via Stripe Customer Portal
- Webhook-driven: Stripe notifies the platform of subscription changes
- Grace period: 7 days after failed payment before downgrading
- No data loss on downgrade -- features are disabled, data is preserved

### 22.5 Data Model

```
ChannelSubscription (new table)
  - Id: int (PK, identity)
  - BroadcasterId: string (FK to Channel, unique)
  - Tier: string ("free", "starter", "pro", "platform")
  - StripeCustomerId: string?
  - StripeSubscriptionId: string?
  - CurrentPeriodEnd: DateTime?
  - Status: string ("active", "past_due", "canceled", "trialing")
  - CreatedAt, UpdatedAt
```

### 22.6 Feature Gating

The `ChannelSubscription.Tier` is checked by a `IFeatureGateService`:

```csharp
public interface IFeatureGateService
{
    bool IsFeatureAvailable(string broadcasterId, string featureKey);
    int GetLimit(string broadcasterId, string limitKey); // e.g., "tts_chars_per_stream"
}
```

Injected via DI. Every feature that has tier limits checks the gate before executing. Free tier users see "Upgrade to unlock" in the dashboard instead of disabled buttons.

---
