# Capitalism NPC Bot

A standalone .NET 10 console application that autonomously manages Capitalism game accounts for testing market dynamics, economic balance, and server stress.

## What it does

- **Registers** NPC bot accounts (or logs in if they already exist — idempotent).
- **Completes the full onboarding flow**: city selection → industry selection → IPO → factory lot → shop lot, with automatic mid-flow resume if interrupted.
- **Selects the cheapest available lot and product**: pure helper logic picks the lowest-priced available lot matching the required building type, and the cheapest non-Pro starter product.
- **Periodically polls** each bot's state to verify onboarding is complete, refresh the net worth, evaluate profitability, and track leaderboard ranking.
- **Classifies profitability**: each bot is rated Profitable / Neutral / Unprofitable / Unknown based on a ±2 % neutral band applied to the net-worth delta since tracking started.
- **Computes an annualised profit rate** (% / yr) and logs it on every tick.
- **Tracks leaderboard rank**: each tick the orchestrator fetches the global rankings and updates `BotAccount.CurrentRank`; the rank is included in the periodic status log. Ranking fetch failures are non-fatal.
- **Produces strategy recommendations**: when a bot has run for the minimum required ticks and is losing money, a `StrategyRecommendation` is generated — a mild 5 % price reduction for small losses, an aggressive 15 % cut for losses ≥ 10 %.
- **State validation**: `BotStateValidator` detects stale bots (no successful operation for N minutes), expired tokens, incomplete onboarding, and at-risk error counts.
- **Error isolation**: each bot tracks consecutive errors independently; skipped after `MaxConsecutiveErrors` without affecting other bots.
- **Graceful shutdown** on `Ctrl+C` or `SIGTERM`.

## Quick start

```bash
cd projects/NPCBot

# Run against the live game server (defaults in appsettings.json)
dotnet run

# Point at a local dev server
dotnet run -- --NpcBot:GraphqlUrl=http://localhost:5095/graphql

# Run 5 bots
dotnet run -- --NpcBot:BotCount=5

# Disable all bots (no-op run)
dotnet run -- --NpcBot:Enabled=false
```

## Configuration

All options live under the `NpcBot` section in `appsettings.json`.  
Override them with environment variables (`NPCBOT_NpcBot__<Key>`) or CLI flags (`--NpcBot:<Key>=<Value>`).

| Key | Default | Description |
|-----|---------|-------------|
| `GraphqlUrl` | `https://capitalism.de-4.biatec.io/graphql` | Game API GraphQL endpoint |
| `BotCount` | `3` | Number of NPC accounts to manage (1–20) |
| `Enabled` | `true` | Master on/off switch |
| `BotNamePrefix` | `NPC` | Prefix for generated display names and e-mails |
| `BotPassword` | *(see below)* | Shared password for all bot accounts |
| `BotEmailDomain` | `npcbot.capitalism.local` | Domain for generated bot e-mails |
| `PollIntervalSeconds` | `60` | How often the orchestrator polls each bot |
| `MaxConsecutiveErrors` | `5` | Errors before a bot is skipped |
| `TokenRefreshBufferMinutes` | `5` | Proactive re-auth before token expiry |
| `AllowedIndustries` | `FURNITURE, FOOD_PROCESSING, HEALTHCARE` | Free-tier industries bots may use |

### Password security

The default password in `appsettings.json` is a development placeholder.  
**Override it in production** using:

```bash
export NPCBOT_NpcBot__BotPassword="<strong-secret>"
dotnet run
```

Or create an `appsettings.Local.json` (already in `.gitignore`):

```json
{ "NpcBot": { "BotPassword": "<strong-secret>" } }
```

## Project layout

```
NPCBot/
├── Configuration/BotOptions.cs      # Configuration model
├── Models/
│   ├── BotAccount.cs                # Per-bot runtime state
│   ├── GameModels.cs                # GraphQL response types
│   ├── ProfitabilityStatus.cs       # Profitability classification enum
│   └── StrategyRecommendation.cs    # Price-adjustment advisory result
├── Services/
│   ├── GameApiClient.cs             # GraphQL HTTP client
│   ├── AccountService.cs            # Auth, profile, game state
│   ├── OnboardingService.cs         # Automated onboarding flow
│   ├── OnboardingHelpers.cs         # Pure lot / product selection helpers
│   ├── BotProfitCalculator.cs       # Net-worth classification, rate, recommendations
│   ├── BotStateValidator.cs         # Token, onboarding, staleness, error-risk checks
│   └── BotOrchestrator.cs           # Main orchestration loop
├── BotRosterFactory.cs              # Creates bot accounts from configuration
├── Program.cs                       # Entry point, DI, graceful shutdown
└── appsettings.json                 # Default configuration
```

## Roadmap

All three ROADMAP items are complete:

- ✅ **Account creation and onboarding** — idempotent register/login, full onboarding flow with mid-flow resume.
- ✅ **Profitability analysis** — `BotProfitCalculator` classifies Profitable / Neutral / Unprofitable / Unknown, computes annualised rate, and produces price-adjustment recommendations when losses exceed thresholds.
- ✅ **State monitoring** — `BotStateValidator` checks token validity, onboarding completion, staleness, and error proximity on every tick.
