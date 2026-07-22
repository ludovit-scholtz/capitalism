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
| Flutter app | `projects/flutter_app` | Flutter/Dart mobile client mirroring `projects/frontend` |
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

## Flutter app (`projects/flutter_app`)

Mobile client mirroring `projects/frontend`'s screens/nav against the same game GraphQL API — see `projects/flutter_app/README.md` and `.github/copilot-instructions.md` → `## Flutter mobile app` for full detail. Key points:
- **Most screens are still empty placeholders**; Home, all four auth screens (Sign In, Forgot Password, Reset Password, Auth Callback), Onboarding, Dashboard, News, Notifications, Contracts, Leaderboard, Player Profile, Cities, World Map, Encyclopedia, Resource Detail, Building Market, Buy Building, Sell Building, Building Detail (trimmed), all 6 City tabs, and Discord/Chat nav actions are implemented. Implement the rest by porting the matching Vue view; `ROADMAP.md` → `### Flutter mobile app` has the per-screen backlog with exact file/view names.
- **`lib/features/onboarding/`** ports the full 7-step wizard (`OnboardingView.vue`), including guest mode (zero backend calls through step 6, then a "Save Progress" button that signs in and migrates via the real mutations) and backend-driven resume (`onboardingCurrentStep`/`onboardingIndustry`/etc. from the `me` query — lands directly on step 6 mid-flow, matching the web's "resume after factory purchase" behavior). Uses the real `startOnboardingCompany` + `finishOnboarding` two-mutation contract, not the legacy one-shot `completeOnboarding`. Trimmed from the web: list-based lot picker (no map), simplified recommended-lot heuristic, no FX conversion, no auto-polled first-sale celebration — see the file's top-of-file comment and `ROADMAP.md` for the full trim list before assuming parity on something not covered here.
- **`lib/features/dashboard/`** ports the web's "company mode" dashboard: `myCompanies`/`gameState`/`myPendingActions` combined query, company cards with action buttons, a building list with status badges, and the same two redirect guards as the web (unauthenticated → `/login`, onboarding-incomplete → `/onboarding`). **Any screen redirecting from `initState()` must defer past the current build** (`WidgetsBinding.instance.addPostFrameCallback`) if the redirect can fire before the first `await` — calling `context.go()` synchronously that early fights with the Router mid-build ("setState() or markNeedsBuild() called during build"); this is a real bug the dashboard's own test suite caught, not a hypothetical one, so apply the same pattern to any future screen with an early, no-await redirect check. Trimmed from the web: no person-account mode / multi-company switching, no 5-tab layout (buildings + pending actions just share one scroll), no Pro panel, no power-grid summary, no per-building ledger/supply-chain panels, no currency-aware cash formatting, no live tick-polling refresh (pull-to-refresh instead).
- **Auth is Bearer-JWT, not cookies** — the backend already returns the raw token from `login`/`register` (`projects/Api/Types/Mutation.Auth.cs`) and accepts it via `Authorization: Bearer` (`Program.cs`'s `TryReadRequestToken`), so no backend changes were needed. Token lives behind the `TokenStorage` abstraction (`lib/core/auth/token_storage.dart`) — `SecureTokenStorage` (real) vs. test-only `InMemoryTokenStorage` — so widget tests don't hit the secure-storage platform channel. `BiatecOidcService` (`lib/core/auth/biatec_oidc_service.dart`, via `flutter_web_auth_2`) is the native complement to the web's OIDC redirect flow — same implicit-flow protocol and client-side state/nonce/issuer/audience checks as `projects/frontend/src/stores/auth.ts`, but the redirect URI shape differs by platform (custom `io.biatec.capitalism://` scheme on Android/iOS vs. `http://localhost:<port>` loopback on Windows/Linux) — see the service's doc comment before changing it. `AuthCallbackScreen` (not `LoginScreen`) owns the actual `signIn()` call and its loading/error UI — native has no server-redirect round trip to resume after, so `LoginScreen`'s Biatec button navigates to `/auth/callback` rather than signing in directly, unlike a literal port.
- **Login/Register are Master API GraphQL mutations, not game API ones** — `LoginScreen` overrides `endpoint: AppConfig.masterGraphqlUrl` per-call; forgot/reset-password are plain REST endpoints on the Master API (`PasswordResetService`, `AppConfig.masterApiBaseUrl`), not GraphQL — there are no such mutations. `AppConfig.authPasswordEnabled` mirrors the web's `VITE_AUTH_PASSWORD_ENABLED`, which **defaults to false** — don't assume the password form is the default UI when testing/screenshotting; override with `--dart-define=AUTH_PASSWORD_ENABLED=true` or `LoginScreen(passwordAuthEnabled: true)`.
- Routing (`lib/core/router/app_router.dart`, `createAppRouter()` factory) and the nav drawer (`lib/core/router/nav_items.dart`) must be kept in sync with `projects/frontend/src/router/index.ts` and `AppHeader.vue`'s nav sections when either changes.
- Native platform folders (`android/`, `ios/`, `web/`, `windows/`) are generated and committed; `flutter analyze`/`flutter test`/`flutter build web`/`flutter build windows`/`flutter build apk` are all verified working (real Windows-exe launch and real Android-emulator run, not just build) — see `.github/copilot-instructions.md` for the exact local toolchain setup (Android SDK + JDK 17, VS "Desktop development with C++" workload + CMake + ATL components) this required.
- **Test with `flutter_test`/`WidgetTester`** (no device/emulator needed for most coverage) — `test/navigation_test.dart` (drawer visibility, nav taps, bottom-nav switching), `test/feature_actions_test.dart` (Discord/Chat wiring), `test/auth_screens_test.dart` (all four auth screens: login/register success and every server error code, forgot/reset password REST flows, the OIDC-only default, callback success/error/redirect), `test/onboarding_screen_test.dart` (full authenticated happy path with exact mutation-arg assertions, Pro-industry gating, `LOT_ALREADY_OWNED` recovery, backend resume into step 6, completed-onboarding dashboard redirect, milestone completion, guest no-mutations-until-save-then-migrates — uses `FakeOnboardingService implements OnboardingService`, a full in-memory fake rather than an HTTP-level mock, since `OnboardingService`'s GraphQL contract has many distinct query/mutation shapes; prefer this `implements`-a-concrete-class pattern over a bigger fake HTTP client when a service has more than 2-3 distinct operations), `test/dashboard_screen_test.dart` (both redirect guards, loading/error/empty states, per-building-type navigation, silent pull-to-refresh), `test/biatec_oidc_service_test.dart` (OIDC validation logic, plus per-platform redirect-URI shape via `debugDefaultTargetPlatformOverride` — this is how you unit-test Android/iOS/Windows-specific branches without needing all three toolchains). Drawer assertions need `tester.binding.setSurfaceSize(const Size(800, 2400))` first, since `ListView` only mounts children within the viewport even for a non-lazy list — items below the default 600px test height silently fail `find.text`. Screens making real HTTP calls (`HomeScreen`/`LoginScreen` GraphQL, `ForgotPasswordScreen`/`ResetPasswordScreen` REST) need fake `http.Client`s threaded through `createAppRouter(httpClient: ..., passwordResetHttpClient: ...)` — the shared `pumpCapitalismApp()` harness (`test/support/app_harness.dart`) does the GraphQL one by default; build a router yourself (`createAppRouter(...)`, keep the reference, pass it to `pumpCapitalismApp(tester, router: ...)`) when a test needs to navigate afterward or override any of these. **Don't assume a single `pump()` after `router.go(...)` shows the destination screen** — go_router's transition needs more than one frame; and **don't use `pumpAndSettle()` around a screen with a real `Future.delayed` timer** (e.g. `LoginScreen`'s OIDC-only auto-redirect) — real timers fire mid-settle, so you'll observe the post-timer state, not the one you meant to assert on. Add `integration_test` later for real device-level e2e once more screens are implemented (the mobile analogue of Playwright).

## GraphQL endpoints

- Game API: `VITE_GRAPHQL_URL` (default `https://capitalism.de-4.biatec.io/graphql`)
- Master API: `VITE_MASTER_GRAPHQL_URL` (default `https://localhost:44364/graphql`)
