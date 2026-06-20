# CLAUDE.md — Capitalism MMO

Full AI guidelines live in `.github/copilot-instructions.md`. This file adds Claude Code–specific shortcuts and highlights the rules most likely to be violated.

## Project at a glance

Full-stack multiplayer economic strategy game (Capitalism II-style).

| Layer | Location | Stack |
|-------|----------|-------|
| Game API | `projects/Api` | ASP.NET Core 10 + Hot Chocolate GraphQL + EF Core + PostgreSQL |
| Master API | `projects/MasterApi` | ASP.NET Core 10 + Hot Chocolate GraphQL + EF Core |
| Shared | `projects/Shared` | C# class library (constants, pure helpers) |
| Game frontend | `projects/frontend` | Vue 3 + TypeScript + Vite + Tailwind |
| Master frontend | `projects/master-frontend` | Vue 3 + TypeScript + Vite + Tailwind |
| NPC bot | `projects/NPCBot` | .NET console app |

Default dev ports: game frontend `5173`, master frontend `5174`, game API `5095`, master API `44364`.

## Key commands

```powershell
# Backend
dotnet test projects/Api.Tests
dotnet test projects/Api.Tests /p:RUN_ARCHIVE_TEST=true   # include archived tests
dotnet build projects/CapitalismBackend.slnx

# New EF migration (game API only — never create .cs files by hand)
cd projects/Api
pwsh ./scripts/New-AppMigration.ps1 -Name <MigrationName>
pwsh ./scripts/Remove-AppMigration.ps1                    # undo last unnapplied scaffold

# Frontend (game)
cd projects/frontend
npm run dev
npm run build
npm run test:unit
npm run test:e2e                          # full-journey only (Chromium)
npx playwright test --debug --project=chromium e2e/full-journey
CI=true npm run test:screenshots          # opt-in screenshot specs

# Frontend (master)
cd projects/master-frontend
npm run dev
npm run build
npm run test:e2e
```

## Hard rules (violations break CI or production)

### Backend
- **No SQLite** — ever. No `UseSqlite`, `Microsoft.Data.Sqlite`, or SQLite schema logic.
- **Tests use EF Core InMemory** — unique DB name per test scope. No SQLite, no file-based DBs.
- **No raw SQL outside migration files** — no `FromSqlRaw`, `FromSqlInterpolated`, `ExecuteSql*` in runtime services or tests. Use LINQ.
- **EF migrations via script only** — `pwsh ./scripts/New-AppMigration.ps1`. Never create or edit migration `.cs` files by hand. Never hand-edit `AppDbContextModelSnapshot.cs`.
- **Cross-API shared code goes in `projects/Shared`**, not duplicated into Api or MasterApi.
- **Never swallow `MigrateAsync()` failures** — if schema upgrade fails, startup must fail fast.
- **500-line file limit** — split large C# types with `partial class` (e.g., `Query.Building.cs`).

### Frontend
- **500-line file limit** — extract composables, split view templates into child components.
- **`<script setup lang="ts">`** in every Vue SFC.
- **No sidecar template/style files** — keep template + script + style in one `.vue` file.
- **Font Awesome icons** — register in `src/lib/fontAwesomeIcons.ts` and keep `fontAwesomeIcons.test.ts` green.
- **GraphQL query strings** — add/update unit tests for multiline query strings in stores/composables.
- **New UI behavior** — add or update Playwright E2E tests in `e2e/full-journey/`.
- **Prettier** — `semi: false`, `singleQuote: true`, `printWidth: 100`.

### Game domain
- **Server controls all economy-sensitive state** — never trust client values for prices, ticks, ownership, balances, cooldowns.
- **Bank accounts are the only money container** — no `Player.cash` or `Company.cash` for spendable funds.
- **Every money movement** — must be a bank-account-to-bank-account transfer visible in the ledger.

## After every meaningful change

1. Add a row to `/CHANGELOG.csv` — guid ID, current timestamp, one sentence in `en`, `sk`, `de`. Format: `"Short prefix - Title: Detailed explanation."` (3–10 word prefix, colon, detail).
2. If it's a player-visible feature or balance change, also publish a `CHANGELOG` news entry via MasterApi so it appears in the in-game feed.

## i18n quick rules

- All user-visible strings use `t()`. Locale files: `src/i18n/locales/{en,sk,de}.ts`.
- Escape special chars in vue-i18n v11: `{` `}` → `{'{'}` `{'}'}`, `@` → `{'@'}`, `|` → `{'|'}`.

## E2E test pattern (quick reference)

```ts
import { setupMockApi, makePlayer } from './helpers/mock-api'

test('...', async ({ page }) => {
  const player = makePlayer()
  const state = setupMockApi(page, { players: [player] })
  state.currentUserId = player.id
  state.currentToken = `token-${player.id}`
  await page.addInitScript((token) => {
    localStorage.setItem('auth_token', token)
    localStorage.setItem('auth_expires', new Date(Date.now() + 7200000).toISOString())
  }, `token-${player.id}`)
  await page.goto('/some-route')
})
```

Always call `setupMockApi` **before** `page.goto()`. Use accessible locators (`getByRole`, `getByLabel`) over CSS selectors. Never use `page.waitForTimeout()`.

## Authentication

Dual-mode: native GraphQL login (`register`/`login`) + Biatec OIDC redirect (callback at `/auth/callback`). Tokens stored in `localStorage` as `auth_token` / `auth_expires`. JWT TTL: 120 min.

## GraphQL endpoints

- Game API: `VITE_GRAPHQL_URL` (default `https://capitalism.de-4.biatec.io/graphql`)
- Master API: `VITE_MASTER_GRAPHQL_URL` (default `https://localhost:44364/graphql`)
