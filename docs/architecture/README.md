# NoMercyBot Multi-Channel Platform Architecture Specification

## Table of Contents

### Core Architecture
- [01 - Overview](01-overview.md) -- Vision, core architecture, naming conventions, architectural principles (DI, providers, event hooks)
- [02 - Data Model](02-data-model.md) -- Every table: current schema, required changes, new tables
- [03 - Authentication](03-authentication.md) -- Token ownership, progressive OAuth scopes, role model, token caching

### API & Services
- [04 - API Design](04-api-design.md) -- Route structure, endpoint groups, backward compatibility
- [05 - Service Architecture](05-service-architecture.md) -- ChannelRegistry pattern, service-by-service changes

### Features
- [06 - Channel Onboarding](06-channel-onboarding.md) -- Step-by-step onboarding flow
- [07 - Channel Management](07-channel-management.md) -- Dashboard, integrations, moderator invites
- [08 - Background Services](08-background-services.md) -- Token refresh, Spotify polling, shoutout queue, watch streaks, EventSub
- [09 - Roslyn Scripts](09-roslyn-scripts.md) -- Platform scripts, per-channel registration
- [14 - Music Provider](14-music-provider.md) -- IMusicProvider interface, Spotify implementation, YouTube Music (future)
- [15 - Widget Creator](15-widget-creator.md) -- Template library, settings schema, code editor
- [16 - Command Editor](16-command-editor.md) -- Pipeline action system, conditions, 50+ pre-built actions, visual builder
- [17 - Cross-Channel Universe](17-cross-channel-universe.md) -- (POST-MVP) Shared game/event system across channels

### Security, Auth & Privacy
- [19 - Widget Auth](19-widget-auth.md) -- Overlay token, SignalR authentication, IP whitelist
- [20 - Permissions](20-permissions.md) -- Polymorphic permissions table, feature permissions, granular access control
- [24 - Data Privacy](24-data-privacy.md) -- GDPR compliance, user data deletion, data export, retention policy, Twitch deletion webhooks

### Infrastructure
- [10 - Database Migration](10-database-migration.md) -- SQLite to PostgreSQL, schema migration, data migration
- [11 - Implementation Phases](11-implementation-phases.md) -- Phase 2-6 with files and acceptance criteria
- [12 - Risk Register](12-risk-register.md) -- Risks, probability, impact, mitigation
- [13 - Genericization Audit](13-genericization-audit.md) -- Hardcoded content, Spotify hack, template placeholders
- [18 - Provider Inventory](18-provider-inventory.md) -- Twitch, Spotify, Discord, OBS: full API capability audit
- [23 - Launch Blockers](23-launch-blockers.md) -- Spotify quota, Twitch/Discord verification

### Platform
- [21 - Dashboard Design](21-dashboard-design.md) -- Navigation, page designs, theming, real-time updates, responsive
- [22 - Billing](22-billing.md) -- Tier model, cost analysis, Stripe integration, feature gating, TTS BYOK
- [25 - Open Source](25-open-source.md) -- AGPL-3.0 license, GitHub org setup, CI/CD, branch strategy, testing, user data visibility
