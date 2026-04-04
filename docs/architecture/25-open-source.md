## 25. Open Source Strategy and Repository Setup

### 25.1 License

**AGPL-3.0** (GNU Affero General Public License v3). This means:
- Anyone can self-host for free
- Anyone who modifies and runs it as a **service** MUST publish their changes
- Our revenue comes from the managed platform (hosting, convenience, support), not the code
- The managed platform is always ahead of self-hosted (latest features, zero setup)
- Competitors cannot run a closed-source fork as a competing service

### 25.2 GitHub Organization

**Organization**: `NoMercyLabs` (https://github.com/NoMercyLabs)
**Repository**: `nomercybot` (https://github.com/NoMercyLabs/nomercybot)

**Manual step required**: Create the org at https://github.com/account/organizations/new (GitHub API does not support org creation on github.com). Pick the Free plan.

### 25.3 Repository Setup (via CLI after org creation)

```bash
# Create the repo
gh repo create NoMercyLabs/nomercybot --public --description "Open source multi-channel Twitch bot platform" --license agpl-3.0

# Branch protection on main
gh api repos/NoMercyLabs/nomercybot/branches/main/protection -X PUT \
  -f required_pull_request_reviews.required_approving_review_count=1 \
  -f required_status_checks.strict=true \
  -f enforce_admins=false \
  -f restrictions=null

# Labels
gh label create "bug" --repo NoMercyLabs/nomercybot --color d73a4a --description "Something isn't working"
gh label create "feature" --repo NoMercyLabs/nomercybot --color 0075ca --description "New feature or request"
gh label create "docs" --repo NoMercyLabs/nomercybot --color 0075ca --description "Documentation improvements"
gh label create "security" --repo NoMercyLabs/nomercybot --color e4e669 --description "Security related"
gh label create "good first issue" --repo NoMercyLabs/nomercybot --color 7057ff --description "Good for newcomers"
```

### 25.4 CI/CD Pipeline (GitHub Actions)

```yaml
# .github/workflows/ci.yml
name: CI
on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal

  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet tool restore
      - run: dotnet csharpier --check src/

  frontend:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: src/NoMercyBot.Client
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
      - run: npm ci
      - run: npm run build
      - run: npm run lint
```

### 25.5 Branch Strategy

| Branch | Purpose | Protection |
|--------|---------|------------|
| `main` | Production-ready | PR required, CI must pass, 1 approval |
| `develop` | Integration branch | CI must pass |
| `feature/*` | Feature branches | None |
| `fix/*` | Bug fix branches | None |
| `release/*` | Release candidates | CI must pass |

### 25.6 Documentation Requirements for Open Source

The repo must include:
- `README.md` -- Project overview, quick start, screenshots
- `CONTRIBUTING.md` -- How to contribute, code style, PR process
- `CODE_OF_CONDUCT.md` -- Community standards
- `SECURITY.md` -- How to report security vulnerabilities
- `LICENSE` -- AGPL-3.0
- `docs/` -- Architecture spec (this document), API docs, deployment guide
- `docs/self-hosting.md` -- How to self-host (Docker Compose, env vars, database setup)
- `.env.example` -- Example environment variables (no secrets)

### 25.7 Testing Strategy

| Layer | Framework | What's Tested |
|-------|-----------|--------------|
| **Unit tests** | xUnit + Moq | Services, command pipeline engine, permission resolution, template variable substitution |
| **Integration tests** | xUnit + WebApplicationFactory | API endpoints, auth flow, database operations |
| **E2E tests** | Playwright | Dashboard flows, OAuth redirects, widget rendering |

**Coverage target**: 80% for services, 90% for the pipeline action engine (security-critical).

**Test project structure**:
```
tests/
  NoMercyBot.Services.Tests/
  NoMercyBot.Api.Tests/
  NoMercyBot.Database.Tests/
  NoMercyBot.E2E.Tests/
```

### 25.8 Secrets in CI

- `STRIPE_SECRET_KEY` -- For billing integration tests (test mode key)
- `TWITCH_CLIENT_ID` / `TWITCH_CLIENT_SECRET` -- For Twitch API integration tests
- `DATABASE_URL` -- PostgreSQL connection for integration tests
- Stored as GitHub Actions secrets, never in code

### 25.9 User Data Visibility

Every Twitch user is a channel owner of themselves. Any user who logs into the dashboard can:
- See a list of ALL channels they've been detected in (from ChatPresence records)
- View their personal stats per channel (message count, watch time, command usage)
- Request deletion of their data from specific channels or all channels
- Export all their data (GDPR Article 15)

The dashboard "My Data" page shows:
```
Channels you've been seen in:
  - stoney_eagle (245 messages, 12h watch time) [View Stats] [Delete My Data]
  - another_streamer (30 messages, 2h watch time) [View Stats] [Delete My Data]
  
[Export All My Data] [Delete All My Data]
```

---
