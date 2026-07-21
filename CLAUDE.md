# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository. It is a condensed, self-contained distillation of `.github/copilot-instructions.md` (the full Copilot rules file, ~1800 lines). Prefer this file over opening that one — it's kept in sync with the durable rules; only open the Copilot file for a topic not covered here.

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
pwsh ./scripts/Remove-AppMigration.ps1                    # undo last unapplied scaffold
dotnet ef migrations list --configuration Release --no-build   # verify EF actually discovered it

# Frontend (game)
cd projects/frontend
npm run dev
npm run build          # runs vue-tsc type-check + build:client + build:ssr — the ONLY command that type-checks;
                        # build:client / build:ssr alone skip type-checking
npm run lint
npm run test:unit
npm run test:e2e                          # full-journey only (Chromium)
npx playwright install chromium           # from inside projects/frontend, not globally
npx playwright test --debug --project=chromium e2e/full-journey
CI=true npm run test:screenshots          # opt-in screenshot specs

# Frontend (master)
cd projects/master-frontend
npm run dev
npm run lint
npm run test:unit
npm run build
npm run test:e2e
```

## Before reporting a change complete

- **Backend**: run the Release pipeline, not just Debug tests: `cd projects/Api && dotnet restore Api.slnx && dotnet build Api.slnx -c Release --no-restore && dotnet test Api.slnx -c Release --no-build`.
- **Any entity/model change**: also validate the upgrade path — migrate/create a DB at the *previous* schema, run `AppDbInitializer.InitializeAsync()` against the new build, confirm new columns/tables/indexes exist after "restart".
- **Frontend**: `npm ci && npm run lint && npm run test:unit && npm run build` from a clean install, not a stale local one. Run `npm run lint` after each edit, not just at the end — unused destructured vars fail CI (`@typescript-eslint/no-unused-vars`, no `argsIgnorePattern: '^_'`, so even `_`-prefixed unused params are flagged).
- **Playwright**: run the specific spec(s) touched, and for onboarding/auth/routing/mock-api changes run the full `npm run test:e2e` (cross-spec regressions are common there).
- **Never push with a known-failing test**, even one that looks "pre-existing" or "unrelated" — a test that breaks under your change means the test data/assertions were invalid under the new rule; fix the data, not the check.
- Rebase/merge `origin/main` before validating locally in a fresh session — CI runs against the real base and main may have moved.
- Distinguish CI infra failures (registry auth, GitHub rate limits) from real code failures; don't "fix" infra by changing code.

## Hard rules (violations break CI or production)

### Backend
- **No SQLite** — ever. No `UseSqlite`, `Microsoft.Data.Sqlite`, or SQLite schema logic.
- **Tests use EF Core InMemory** — unique DB name per test scope. No SQLite, no file-based DBs.
- **No raw SQL outside migration files** — no `FromSqlRaw`, `FromSqlInterpolated`, `ExecuteSql*` in runtime services or tests. Use LINQ.
- **EF migrations via script only** — `pwsh ./scripts/New-AppMigration.ps1`. Never create or edit migration `.cs` files by hand — EF discovers migrations via compiled metadata (`.Designer.cs`, `[Migration]`, `[DbContext]` attributes), not filenames, so a hand-written file is silently never applied and crashes production startup. Never hand-edit `AppDbContextModelSnapshot.cs`. Schema-repair/init code must never pre-create a column/table that a still-pending migration will also create (`42701`/`42P07` errors).
- **Cross-API shared code goes in `projects/Shared`**, not duplicated into Api or MasterApi.
- **Never swallow `MigrateAsync()` failures** — if schema upgrade fails, startup must fail fast.
- **500-line file limit** — split large C# types with `partial class` (e.g., `Query.Building.cs`).
- **2+ `Include`/`ThenInclude` over collection navigations → always add `.AsSplitQuery()`**, otherwise Cartesian-product duplication corrupts navigation collections (has caused real data corruption, not just slowness).
- **GraphQL query resolvers must never write** — no `SaveChangesAsync()` or other mutation in a read path (killed latency for all concurrent users in the past); always `AsNoTracking()` in read resolvers.
- **Tests must not mutate shared singleton state** (`GameState.CurrentTick`, global config) via a shared `IClassFixture` — use an isolated `ApiWebApplicationFactory` per test, or it corrupts unrelated tests in the same suite.
- Non-deterministic ordering: `db.Cities.FirstAsync()` without a filter/order is nondeterministic across providers — filter (`.FirstAsync(c => c.Name == "Bratislava")`) or order explicitly.
- HotChocolate v15: non-nullable input fields must be explicitly provided in GraphQL variables even if the C# class has a default; enum values are `SCREAMING_SNAKE_CASE` strings; a failing authorized field inside a query that also has public fields can make HotChocolate return `data: null` for the whole document — check `ValueKind == JsonValueKind.Object` before indexing into `data`.
- Console apps (e.g. `NPCBot`) must ship a companion `<ProjectName>.Tests/` project in the same PR — extract pure logic out of `Program.cs`/private statics so it's testable.

### Frontend
- **500-line file limit** — extract composables, split view templates into child components.
- **`<script setup lang="ts">`** in every Vue SFC; keep template + script + `<style scoped>` together in one `.vue` file — no sidecar template/style files, and when extracting a child component, the matching `<style scoped>` selectors must move with the template (styles don't follow markup automatically).
- **Font Awesome icons** — register in `src/lib/fontAwesomeIcons.ts` and keep `fontAwesomeIcons.test.ts` green.
- **GraphQL query strings** — add/update unit tests for multiline query strings in stores/composables (a malformed query can leave count-only UI paths working while the main panel silently stays empty).
- **New UI behavior** — add or update Playwright E2E tests in `e2e/full-journey/`.
- **Prettier** — `semi: false`, `singleQuote: true`, `printWidth: 100`.
- **Pure logic → `src/lib/` or composables, with unit tests** (`src/lib/__tests__/`, `src/composables/__tests__/`, Vitest `environment: 'node'`, no browser APIs).
- **The app is dark-first** — never add `prefers-color-scheme` fallback logic to theme detection; CI's headless browser reports `light`, which would silently override the intended dark default.
- Design/spacing conventions (8px rhythm, page-shell padding) are in `projects/frontend/docs/design-patterns.md` — check for new/migrated pages.
- Multi-statement inline `@click` handlers need semicolons/arrow functions; bare newlines aren't statement terminators in attribute expressions.

### Game domain
- **Server controls all economy-sensitive state** — never trust client values for prices, ticks, ownership, balances, cooldowns.
- **Bank accounts are the only money container** — no `Player.cash` or `Company.cash` for spendable funds. Every money movement is a bank-account-to-bank-account transfer visible in the ledger.
- Unconditional per-tick operations (decay, expiry, interest accrual) must never sit behind an early-return guard for an unrelated condition — extract them outside conditional blocks.
- Any financial reserve/block/hold field needs a symmetric release/settlement path added in the *same* PR, or players can get permanently soft-locked.
- Every new GraphQL query/mutation needs an explicit, deliberate auth decision (`[Authorize]` or documented public rationale) plus tests for: unauthenticated rejection, foreign-owner rejection, authorized-owner success.
- Concurrency-sensitive economic mutations need optimistic concurrency tokens plus a test proving a deterministic single winner under simultaneous requests.
- `audits/*.md` feeds a CI gate requiring every Open/In-Progress High/Critical finding to carry a `<!-- issue: #NNN -->` annotation.

## After every meaningful change

1. Add a row to `/CHANGELOG.csv` — guid ID, current timestamp, one sentence in `en`, `sk`, `de`. Format: `"Short prefix - Title: Detailed explanation."` (3–10 word prefix, colon, detail).
2. If it's a player-visible feature or balance change, also publish a `CHANGELOG` news entry via MasterApi so it appears in the in-game feed.

## Authentication (cookie-based — do not use the old localStorage/Bearer model)

Auth uses **server-issued session cookies**, not client-held JWTs:
- Login/register issue an HttpOnly, `SameSite=Strict`, `Secure` (outside Development) cookie named `auth_token`.
- The frontend GraphQL client sends `credentials: 'include'` and does **not** attach `Authorization: Bearer` headers for normal player sessions.
- Any lingering `localStorage` `auth_token`/`auth_expires` values are legacy and are actively purged on load — don't rely on localStorage for auth state.
- Dual login: native GraphQL (`register`/`login`) + Biatec OIDC redirect (callback at `/auth/callback`). Always validate callback `state`/`nonce`; validate `iss`/`aud`/signature server-side for OIDC tokens.

## E2E test pattern (Playwright)

- Directory layout: `e2e/full-journey/**` (canonical CI/default suite), `e2e/docs/**` (screenshot specs, opt-in via `test:screenshots`, excluded from `test:e2e` via `testIgnore`), `e2e/archive/**` (excluded by default).
- Auth setup uses the cookie-based helper, **not manual localStorage before `page.goto()`**:

```ts
import { setupMockApi, makePlayer, loginAs } from './helpers/mock-api'

test('...', async ({ page }) => {
  const player = makePlayer()
  const state = setupMockApi(page, { players: [player] })
  await loginAs(page, state, player)   // sets auth_token/auth_expires cookies + localStorage, submits the login form
  await page.goto('/some-route')
})
```

- Call `setupMockApi` **before** any navigation. Prefer accessible locators (`getByRole`, `getByLabel`) over CSS selectors; scope `getByText` with `.first()`/a container when text may repeat; verify strings against the actual `en.ts` i18n keys, don't guess. Never use `page.waitForTimeout()`.
- Dashboard tests must set `player.onboardingCompletedAtUtc`, or `/dashboard` redirects to `/onboarding`.
- URL assertions must allow for router query params (e.g. `toHaveURL(/\/onboarding/)`, not an exact string), since components can set `?step=...` on mount.
- `v-show`-hidden tab content reports as `hidden` to Playwright — click the tab first.
- Mock-api query matching: `query.includes('x')` substring checks are a recurring bug source — `me` collides with `gameServers`/`mySubscription`, `rankings` collides with `companyRankings`; `includes()` is case-sensitive so a lowercase guard won't exclude a camelCase mutation. Use full, unambiguous operation names, and check for collisions before adding a handler. Mock handlers must return **every** requested top-level field for combined queries — a missing field can silently zero out a Pinia store.
- If you migrate CSS/Tailwind and delete scoped styles, grep specs for `locator('.class')` first — classes used only as test hooks must survive even with no styling left.
- Run installs/specs from inside `projects/frontend` (`npx playwright install chromium`), never globally — version mismatches produce opaque "executable doesn't exist" errors.
- Never leave `[WIP]` PRs or stray screenshot/diff artifacts when reporting complete; review `git status` before pushing.

## i18n quick rules

- All user-visible strings use `t()`. Locale files: `src/i18n/locales/{en,sk,de}.ts` (and the same structure under `projects/master-frontend`). Every new string needs all three locales added in the same change.
- Escape special chars in vue-i18n v11 message strings: `{` `}` → `{'{'}` `{'}'}`, `@` → `{'@'}`, `|` → `{'|'}`. These errors are runtime-only (JIT), not caught at build time, and silently render the whole component as empty (`<!---->`) — validate locale files with the `@intlify/message-compiler` parser after edits.

## Docker Compose local dev

```bash
cd projects
docker-compose down -v --remove-orphans
docker-compose up --build -d
docker-compose ps
docker-compose logs --no-color --tail=200 postgresmaster masterapi game1
```

`postgresmaster` must be `healthy` before APIs start. Containers run HTTP internally (not HTTPS) — don't switch back without mounting real certs. If startup fails, triage in order: container status → Postgres health/log → DB existence (`gamemaster`, `game1`) → API migration logs → GraphQL health endpoints (`/graphql`, `/healthz`).

## Discord bot

Master-server bot lives in `projects/MasterApi/Utilities/Discord/`, gated by `DiscordBot:Enabled` + `BotToken`. One Discord server hosts both environments via command prefixes (`cap5` prod, `cap5stage` staging) with separate bot tokens. The two-way chat bridge (`postBridgedChatMessage` / `forwardInGameChatToDiscord`) uses `BridgedChatMessageTracker` as a loop guard on both sides. Setup: `docs/discord-bot-setup.md`.

## GraphQL endpoints

- Game API: `VITE_GRAPHQL_URL` (default `https://capitalism.de-4.biatec.io/graphql`)
- Master API: `VITE_MASTER_GRAPHQL_URL` (default `https://localhost:44364/graphql`)
