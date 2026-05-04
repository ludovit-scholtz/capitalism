# Capitalism NPC Bot

A standalone .NET 10 console application that autonomously manages Capitalism game accounts for testing market dynamics, economic balance, and server stress.

## What it does

- **Registers** NPC bot accounts (or logs in if they already exist — idempotent).
- **Completes the full onboarding flow**: city selection → industry selection → IPO → factory lot → shop lot, with automatic mid-flow resume if interrupted.
- **Periodically polls** each bot's state to verify onboarding is complete, track company net worth, and log profitability deltas.
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
│   └── GameModels.cs                # GraphQL response types
├── Services/
│   ├── GameApiClient.cs             # GraphQL HTTP client
│   ├── AccountService.cs            # Auth, profile, game state
│   ├── OnboardingService.cs         # Automated onboarding flow
│   └── BotOrchestrator.cs           # Main orchestration loop
├── Program.cs                       # Entry point, DI, graceful shutdown
└── appsettings.json                 # Default configuration
```

## Roadmap

- **Phase 1 (done):** Account creation, onboarding, and net-worth tracking.
- **Phase 2 (planned):** Price optimisation (undercut competitors by 5–10%), inventory restocking, advanced strategy profiles.
