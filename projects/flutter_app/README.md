# Capitalism — Flutter App

Mobile client for the Capitalism MMO, providing the same game-playing interface as
`projects/frontend` (the Vue web app) against the same GraphQL game API.

## Status

Most screens are still empty placeholders — see `ROADMAP.md` (`### Flutter mobile
app`) for the one-line-per-screen implementation backlog — but the app shell is real
and working: Home (live tick/tax-rate/leaderboard data), navigation (drawer + bottom
nav, auth/admin-gated visibility), Discord (opens the system browser via
`url_launcher`), Chat (an in-app panel), and Biatec OIDC sign-in are implemented. See
`CLAUDE.md` / `.github/copilot-instructions.md` for architecture and conventions.

Native platform runners (`android/`, `ios/`, `web/`, `windows/`) are generated and
committed. `flutter analyze`/`flutter test` are clean, and `flutter build
web`/`windows`/`apk` all succeed — verified with real runs, not just builds: a built
Windows exe stays open and renders correctly, and a real Android emulator install boots
Home, opens the drawer with correct auth-gated visibility, and tapping Discord genuinely
launches Chrome. iOS cannot be built or verified without a macOS machine running Xcode.

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
flutter run --dart-define=GRAPHQL_URL=http://localhost:44356/graphql
```

`GRAPHQL_URL` defaults to `http://localhost:44356/graphql` (matches the local game API
port) when omitted. `MASTER_GRAPHQL_URL` defaults to `https://localhost:44364/graphql`.
See `lib/core/config/app_config.dart`.

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
  navigating; Chat opens and dismisses its panel; the Biatec sign-in button both
  succeeds (stores the token in `AuthState`) and surfaces a failure via SnackBar.
- `test/biatec_oidc_service_test.dart` — the OIDC client-side validation logic (state,
  nonce, issuer, audience, provider errors, authenticator cancellation), plus a test
  that forces `debugDefaultTargetPlatformOverride` through Android/iOS/Windows to
  assert the redirect URI shape differs correctly per platform **without needing all
  three native toolchains** — see `.github/copilot-instructions.md` for that pattern.
- `test/support/app_harness.dart` — shared `pumpCapitalismApp()` helper: fresh
  `AuthState` + fresh `createAppRouter()` per test (a shared router singleton would
  leak navigation state across tests), `InMemoryTokenStorage`, and a faked
  `HomeScreen` GraphQL response (`fake_graphql_client.dart`) so no test needs a real
  backend.

Once more screens exist and true device-level end-to-end journeys matter (mirroring
the role Playwright plays for `projects/frontend`), add the official `integration_test`
package (bundled with the Flutter SDK) — `flutter_test`/`WidgetTester` alone can't
exercise platform channels, real network calls, or true multi-frame animations.

## Architecture

- `lib/core/config` — build-time GraphQL endpoint configuration (`--dart-define`).
- `lib/core/auth` — `AuthState` (ChangeNotifier) holds the player's Bearer JWT via the
  `TokenStorage` abstraction (`token_storage.dart`): `SecureTokenStorage` wraps
  `flutter_secure_storage` for the real app, `InMemoryTokenStorage` (test-only, under
  `test/support/`) avoids the secure-storage platform channel in widget tests. The
  backend's `login`/`register` GraphQL mutations return the raw JWT in the response
  payload (in addition to setting the browser's HttpOnly session cookie), so this app
  authenticates with `Authorization: Bearer <token>` headers — no backend changes were
  needed. `biatec_oidc_service.dart` + `web_authenticator.dart` are the native
  complement to the web's Biatec OIDC redirect flow, using `flutter_web_auth_2`.
- `lib/core/graphql` — `GraphQlService`, a thin GraphQL POST helper mirroring
  `projects/frontend/src/lib/graphql.ts`'s `gqlRequest()`.
- `lib/core/router` — `createAppRouter()` builds the `go_router` route table
  (`app_router.dart`, mirroring `projects/frontend/src/router/index.ts`) as a factory
  rather than a bare singleton, so tests can get a fresh `GoRouter` (and inject a fake
  `UrlOpener`/`http.Client`) per test instead of leaking navigation state across
  `pumpWidget` calls. The nav drawer's section/item data (`nav_items.dart`) mirrors
  `AppHeader.vue`'s `mobileNavSections`/`desktopNavSections`.
- `lib/core/services/url_opener.dart` — `UrlOpener` abstraction over `url_launcher`
  (real impl `ExternalUrlOpener`), used for the Discord nav link.
- `lib/core/widgets/app_shell.dart` — persistent chrome (app bar, drawer, bottom nav)
  wrapped around every route via a `ShellRoute`, equivalent to `AppHeader.vue`.
- `lib/core/theme` — dark-first `ThemeData` (mirrors the web app's dark-first rule —
  never fall back to `prefers-color-scheme`).
- `lib/features/home/home_screen.dart` — hero + auth-dependent CTA, tick/tax-rate
  status cards, and a top-5 leaderboard preview from one GraphQL query.
- `lib/features/auth/auth_screens.dart` — `LoginScreen` has a working "Sign in with
  Biatec" button; the rest of the auth screens (and most other feature areas) are
  still `PlaceholderScreen` stubs (bare content, no nested `Scaffold`/`AppBar` — it's
  always rendered as `AppShell`'s `Scaffold.body`) naming the Vue view they mirror.
- `lib/features/chat/chat_panel.dart` — the Chat nav item's in-app panel.
- `lib/l10n/app_{en,sk,de}.arb` — source strings for `flutter gen-l10n`, covering the
  app shell/nav/auth chrome today. Mirrors the web app's `en`/`sk`/`de` locale support;
  extend with a screen's own keys as that screen is implemented.

## Known follow-ups

- Wiring `GraphQlService`/`AuthState` into the remaining login/register/gameplay
  screens as each is implemented (see ROADMAP.md).
- `integration_test`-based end-to-end coverage once more screens exist (see Test
  section above).
- The Biatec OIDC flow was only validated against fakes (`FakeWebAuthenticator`) —
  it hasn't been exercised against a real Biatec IdP yet.
- macOS and Linux builds weren't attempted or verified (this scaffold was built and
  tested on Windows only, covering Windows desktop + Android emulator + Web).
