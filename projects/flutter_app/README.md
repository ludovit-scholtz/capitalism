# Capitalism — Flutter App

Mobile client for the Capitalism MMO, providing the same game-playing interface as
`projects/frontend` (the Vue web app) against the same GraphQL game API.

## Status

Most screens are still empty placeholders — see `ROADMAP.md` (`### Flutter mobile
app`) for the one-line-per-screen implementation backlog — but the app shell, auth
flow, onboarding wizard, and dashboard are real and working: Home (live
tick/tax-rate/leaderboard data), navigation (drawer + bottom nav, auth/admin-gated
visibility), Discord (opens the system browser via `url_launcher`), Chat (an in-app
panel), all four auth screens — Sign In (login/register + Biatec OIDC), Forgot
Password, Reset Password, Auth Callback — the full 7-step Onboarding wizard (city →
industry → product → IPO → factory purchase → shop purchase → completion, with guest
mode and backend-driven resume), the Dashboard (company cards, buildings with
status badges, pending actions, the same auth/onboarding redirect guards as the web),
News (filters, pagination, mark-all-read), Notifications (day-grouped, click-to-navigate
by type), Contracts (create-offer form, Pending/Active/History columns with
accept/reject/cancel), Leaderboard (Players/Companies tabs, pagination, endgame
benchmark), Player Profile (stats, industries, hall of fame, achievements, rank
history), Cities (population-sorted directory with resource chips), World Map
(expansion status, unlock progress, list-based city picker), the Encyclopedia
+ Resource Detail (searchable resource/product catalog with recipe cross-links),
and the four building screens — Building Market (listings + offers), Buy Building
(city/type/lot/confirm flow), Sell Building (list/update-price/destroy), and a
heavily trimmed Building Detail (overview, unit list, upgrade/price quick actions
— no grid editor), all 6 City tabs (Overview, Economy, Buildings, Market,
Contracts, Competitors), the 4 exchange screens — Global Exchange (buy resources
from other cities), Stocks (listings), Stock Trading (order book, limit orders,
shareholders), and Forex (swap/rates/history) — Ledger, Company Contracts, Company
Settings, Company Research, Personal Ledger, the four banking screens — Loan
Marketplace, Bank Management, Request Loan, Bank Statement — and the six market/
trade screens — Market Intelligence, Market Dashboard, Energy Market, Global
Events, Marketing Analytics, Trade Routes — are implemented, matching the web
app's fields, validation,
GraphQL/REST endpoints, and error handling. The app now also has a cohesive dark
"22nd century HUD" visual theme (custom Material 3 `ThemeData`, bundled
Orbitron/Inter/Rajdhani fonts, animated starfield background) that applies to every
screen automatically. See `CLAUDE.md` / `.github/copilot-instructions.md` for architecture
and conventions, including several non-obvious behaviors worth knowing before touching
that code (password auth is **disabled by default**, matching the web; login/register
hit the Master API, not the game API; forgot/reset-password are REST, not GraphQL;
onboarding uses `startOnboardingCompany` + `finishOnboarding`, not the legacy one-shot
`completeOnboarding`; any screen redirecting from `initState()` before its first
`await` must defer via `addPostFrameCallback` or it'll throw mid-build — a real bug
the Dashboard's test suite caught, documented in `.github/copilot-instructions.md`).

Native platform runners (`android/`, `ios/`, `web/`, `windows/`) are generated and
committed. `flutter analyze`/`flutter test` are clean, and `flutter build
web`/`windows`/`apk` all succeed — verified with real runs, not just builds: a built
Windows exe stays open and renders the Sign In and Forgot Password screens correctly,
and a real Android emulator install boots Home, opens the drawer with correct
auth-gated visibility, and tapping Discord genuinely launches Chrome. iOS cannot be
built or verified without a macOS machine running Xcode.

Building for Android/Windows requires their native toolchains, which are **not**
covered by a plain Flutter SDK install — see `.github/copilot-instructions.md` →
`### Local toolchain setup` for exact, verified setup steps (Android SDK cmdline-tools
+ JDK 17; Visual Studio "Desktop development with C++" workload + CMake + ATL/MFC
components) and the non-obvious gotchas that come up in each. `flutter doctor` will
tell you what's missing, but its "Visual Studio is missing components" message can
point at the wrong VS install if you have more than one — see that section before
trusting it at face value.

## Setup

```bash
cd projects/flutter_app
flutter pub get
```

`flutter pub get` also regenerates `lib/l10n/generated/app_localizations.dart` from
the ARB files under `lib/l10n/` (both directories are gitignored — regenerate, don't
hand-edit). If you ever need to regenerate the native platform folders from scratch,
`flutter create .` fills them in around the existing `lib/`, `pubspec.yaml`, and
`test/` without overwriting them — verified by checksum before/after when this
scaffold was created.

## Run

```bash
flutter run --dart-define=GRAPHQL_URL=http://localhost:44356/graphql --dart-define=AUTH_PASSWORD_ENABLED=true
```

`GRAPHQL_URL` defaults to `http://localhost:44356/graphql` (matches the local game API
port) when omitted. `MASTER_GRAPHQL_URL` defaults to `https://localhost:44364/graphql`.
`AUTH_PASSWORD_ENABLED` defaults to `false` (matching the web's
`VITE_AUTH_PASSWORD_ENABLED`) — without it, the Sign In screen shows a Biatec-OIDC-only
banner and auto-redirects rather than the email/password form. See
`lib/core/config/app_config.dart`.

## Test

```bash
flutter test
```

Uses the built-in `flutter_test` package (`WidgetTester` + `pumpWidget`) for widget and
navigation tests — no emulator/device or native platform build required, so it's the
right default for CI and for an app where most screens are still placeholders.

- `test/navigation_test.dart` — the app boots to Home; the drawer hides auth/admin-only
  items when signed out; tapping a public drawer item navigates and closes the drawer;
  the bottom nav switches screens and highlights the active tab; signing in reveals
  auth-only items, changes the Home CTA, and reaches Dashboard; an admin session
  reveals the Administration section and reaches Operations.
- `test/feature_actions_test.dart` — Discord opens the (faked) external link without
  navigating; Chat opens and dismisses its panel.
- `test/auth_screens_test.dart` — the OIDC-only default (auto-redirect after a real
  500ms delay); login/register success and every mapped server error code
  (`LOGIN_THROTTLED`, `INVALID_CREDENTIALS`, `REGISTRATION_FAILED`, ...); the
  forgot-password link; the Biatec button navigating to (not signing in from) the
  callback screen; `AuthCallbackScreen` success, failure, provider-error, and
  redirect-param handling; the forgot/reset-password REST flows including the
  client-side missing-token/password-mismatch checks that skip the network call
  entirely, and the live password-strength label.
- `test/biatec_oidc_service_test.dart` — the OIDC client-side validation logic (state,
  nonce, issuer, audience, provider errors, authenticator cancellation), plus a test
  that forces `debugDefaultTargetPlatformOverride` through Android/iOS/Windows to
  assert the redirect URI shape differs correctly per platform **without needing all
  three native toolchains** — see `.github/copilot-instructions.md` for that pattern.
- `test/onboarding_screen_test.dart` — the full authenticated happy path across all 7
  steps with exact mutation-arg assertions (`startOnboardingCompany`/`finishOnboarding`
  called with the precise selected industry/city/lot/product); Pro-only-industry
  gating; `LOT_ALREADY_OWNED` recovery (selection cleared, lots reloaded); backend
  resume landing directly on step 6 when `onboardingCurrentStep == 'SHOP_SELECTION'`;
  redirect to `/dashboard` when onboarding + first sale are already complete;
  milestone completion; and the guest flow (zero mutation calls through step 6, then
  "Save Progress" migrating via both mutations). Uses
  `test/support/fake_onboarding_service.dart`'s `FakeOnboardingService implements
  OnboardingService` — a full in-memory fake, not an HTTP mock, since `OnboardingService`
  has too many distinct operations for a generic fake `http.Client` to stay readable;
  reach for this pattern (implement the concrete service class directly — Dart classes
  are implicitly interfaces) whenever a service has more than 2-3 operations.
- `test/dashboard_screen_test.dart` — both redirect guards (unauthenticated → `/login`,
  onboarding-incomplete → `/onboarding`, the latter verified to skip the dashboard-data
  fetch entirely); loading/error(+Retry)/empty(+"Start Onboarding") states; companies,
  buildings, cash, and status badges (destroyed/loan-default/power-status) rendering
  correctly; per-building-type navigation (`BANK` → `/bank/:id`, else `/building/:id`);
  and pull-to-refresh silently re-fetching (call-count assertion, not just that the UI
  looks the same). Uses `FakeDashboardService implements DashboardService`.
- `test/support/app_harness.dart` — shared `pumpCapitalismApp()` helper: fresh
  `AuthState` + fresh `createAppRouter()` per test (a shared router singleton would
  leak navigation state across tests), `InMemoryTokenStorage`, and a faked
  `HomeScreen` GraphQL response (`fake_graphql_client.dart`) so no test needs a real
  backend. Pass `router:` yourself (built via `createAppRouter(...)`, keeping the
  reference) when a test needs to `router.go(...)` after pumping, or needs to override
  the GraphQL/REST/OIDC fakes — see `fake_auth_graphql_client.dart` and
  `fake_password_reset_client.dart` for the login/register and forgot/reset-password
  fakes respectively.
- **Timing gotchas worth knowing before adding more tests here**: a single `pump()`
  right after `router.go(...)` isn't always enough for go_router's transition to land;
  never wrap `pumpAndSettle()` around a screen with a real `Future.delayed` (it'll fire
  mid-settle and you'll observe the post-timer state); and a purely-microtask-chained
  async flow (fake HTTP + fake authenticator, no real delay anywhere) can fully resolve
  within one `pump()`, so don't rely on catching an intermediate "loading" frame —
  assert on a recording fake having been called, plus the final state, instead.
- **A real bug the Dashboard tests caught**: calling `context.go(...)` synchronously
  from `initState()` — i.e. before the triggering async function's first `await` —
  throws "setState() or markNeedsBuild() called during build", since the Router is
  still mid-build on the first frame. Any screen with an early, no-await redirect check
  in `initState` needs to defer via `WidgetsBinding.instance.addPostFrameCallback`
  rather than calling its bootstrap function directly.

Once more screens exist and true device-level end-to-end journeys matter (mirroring
the role Playwright plays for `projects/frontend`), add the official `integration_test`
package (bundled with the Flutter SDK) — `flutter_test`/`WidgetTester` alone can't
exercise platform channels, real network calls, or true multi-frame animations.

## Architecture

- `lib/core/config` — build-time GraphQL endpoint configuration (`--dart-define`),
  including the Master API base URL and the `authPasswordEnabled` flag.
- `lib/core/auth` — `AuthState` (ChangeNotifier) holds the player's Bearer JWT via the
  `TokenStorage` abstraction (`token_storage.dart`): `SecureTokenStorage` wraps
  `flutter_secure_storage` for the real app, `InMemoryTokenStorage` (test-only, under
  `test/support/`) avoids the secure-storage platform channel in widget tests. The
  backend's `login`/`register` GraphQL mutations return the raw JWT in the response
  payload (in addition to setting the browser's HttpOnly session cookie), so this app
  authenticates with `Authorization: Bearer <token>` headers — no backend changes were
  needed. `biatec_oidc_service.dart` + `web_authenticator.dart` are the native
  complement to the web's Biatec OIDC redirect flow, using `flutter_web_auth_2`.
  `password_reset_service.dart` is a plain REST client for the forgot/reset-password
  endpoints — there are no GraphQL mutations for either.
- `lib/core/graphql` — `GraphQlService`, a thin GraphQL POST helper mirroring
  `projects/frontend/src/lib/graphql.ts`'s `gqlRequest()`; `GraphQlException` carries
  the server's `extensions.code`, mirroring the web's `GraphQLError` class.
- `lib/core/router` — `createAppRouter()` builds the `go_router` route table
  (`app_router.dart`, mirroring `projects/frontend/src/router/index.ts`) as a factory
  rather than a bare singleton, so tests can get a fresh `GoRouter` (and inject fakes
  for `UrlOpener`/`http.Client`/`WebAuthenticator`/`passwordAuthEnabled`) per test
  instead of leaking navigation state across `pumpWidget` calls. The nav drawer's
  section/item data (`nav_items.dart`) mirrors `AppHeader.vue`'s
  `mobileNavSections`/`desktopNavSections`.
- `lib/core/services/url_opener.dart` — `UrlOpener` abstraction over `url_launcher`
  (real impl `ExternalUrlOpener`), used for the Discord nav link.
- `lib/core/widgets/app_shell.dart` — persistent chrome (app bar, drawer, bottom nav)
  wrapped around every route via a `ShellRoute`, equivalent to `AppHeader.vue`.
- `lib/core/theme` — dark-first `ThemeData` (mirrors the web app's dark-first rule —
  never fall back to `prefers-color-scheme`).
- `lib/features/home/home_screen.dart` — hero + auth-dependent CTA, tick/tax-rate
  status cards, and a top-5 leaderboard preview from one GraphQL query.
- `lib/features/auth/auth_screens.dart` — all four auth screens: `LoginScreen`
  (login/register form + Biatec button + OIDC-only default), `ForgotPasswordScreen`,
  `ResetPasswordScreen`, `AuthCallbackScreen` (owns the actual OIDC round trip — see
  the file's doc comment for why that differs from a literal port of the web).
- `lib/features/chat/chat_panel.dart` — the Chat nav item's in-app panel.
- `lib/features/onboarding/` — the full 7-step onboarding wizard: `onboarding_models.dart`
  (data classes + IPO plan/starter-industry constants), `onboarding_service.dart`
  (GraphQL calls, real `startOnboardingCompany`/`finishOnboarding` contract — not the
  legacy `completeOnboarding`), `onboarding_steps.dart` (city/industry/product/IPO/lot
  step widgets), `onboarding_complete_step.dart` (step 7), `onboarding_screen.dart`
  (the orchestrating state machine — guest mode, backend resume, error recovery; see
  its top-of-file comment for the list of things trimmed from the web version).
- `lib/features/dashboard/` — the "company mode" dashboard: `dashboard_models.dart`
  (data classes), `dashboard_service.dart` (the `myCompanies`/`gameState`/
  `myPendingActions` combined query plus the onboarding-guard `me` query),
  `dashboard_widgets.dart` (company card, building tile, pending-actions section),
  `dashboard_screen.dart` (guards, loading/error/empty states, silent pull-to-refresh;
  see its top-of-file comment for the list of things trimmed from the web version).
- `lib/l10n/app_{en,sk,de}.arb` — source strings for `flutter gen-l10n`, covering the
  app shell/nav/auth chrome today. Mirrors the web app's `en`/`sk`/`de` locale support;
  extend with a screen's own keys as that screen is implemented.

## Known follow-ups

- Wiring `GraphQlService`/`AuthState` into the remaining gameplay screens as each is
  implemented (see ROADMAP.md).
- `integration_test`-based end-to-end coverage once more screens exist (see Test
  section above).
- The Biatec OIDC flow was only validated against fakes (`FakeWebAuthenticator`) —
  it hasn't been exercised against a real Biatec IdP yet. Same for the auth screens'
  GraphQL/REST calls — validated against fakes matching the real backend's schema and
  error contracts, not a live Master API.
- macOS and Linux builds weren't attempted or verified (this scaffold was built and
  tested on Windows only, covering Windows desktop + Android emulator + Web).
