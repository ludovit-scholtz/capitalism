# Capitalism — Flutter App

Mobile client for the Capitalism MMO, providing the same game-playing interface as
`projects/frontend` (the Vue web app) against the same GraphQL game API.

## Status

This is a **base scaffold**: routing, theming, i18n plumbing, a GraphQL client, and
token-based auth state are wired up, but every screen is an empty placeholder. See
`ROADMAP.md` (`### Flutter mobile app`) for the one-line-per-screen implementation
backlog, and `CLAUDE.md` / `.github/copilot-instructions.md` for the architecture and
conventions this app follows.

Native platform runners (`android/`, `ios/`, `web/`, `windows/`) have been generated
and are committed. `flutter analyze` is clean, `flutter test` passes (navigation
coverage included), and `flutter build web` succeeds. Building for Android/iOS/Windows
additionally requires their native toolchains on your machine (Android SDK; Visual
Studio "Desktop development with C++" workload) — install those separately, `flutter
doctor` will tell you what's missing.

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
right default for CI and for a scaffold where most screens are still placeholders.
`test/navigation_test.dart` drives the real `CapitalismApp` (fresh `createAppRouter()`
per test, `InMemoryTokenStorage` standing in for the secure-storage platform channel)
and asserts: the app boots to Home; the drawer hides auth/admin-only items when signed
out; tapping a public drawer item navigates and closes the drawer; the bottom nav
switches screens and highlights the active tab; signing in reveals auth-only items and
reaches Dashboard; an admin session reveals the Administration section and reaches
Operations.

Once real screens exist and end-to-end user journeys matter (mirroring the role
Playwright plays for `projects/frontend`), add the official `integration_test` package
(bundled with the Flutter SDK) to drive the app on a real device/emulator/Chrome —
`flutter_test`/`WidgetTester` alone can't exercise platform channels, real network
calls, or true multi-frame animations the way `integration_test` can.

## Architecture

- `lib/core/config` — build-time GraphQL endpoint configuration (`--dart-define`).
- `lib/core/auth` — `AuthState` (ChangeNotifier) holds the player's Bearer JWT via the
  `TokenStorage` abstraction (`token_storage.dart`): `SecureTokenStorage` wraps
  `flutter_secure_storage` for the real app, `InMemoryTokenStorage` (test-only, under
  `test/support/`) avoids the secure-storage platform channel in widget tests. The
  backend's `login`/`register` GraphQL mutations return the raw JWT in the response
  payload (in addition to setting the browser's HttpOnly session cookie), so this app
  authenticates with `Authorization: Bearer <token>` headers — no backend changes were
  needed.
- `lib/core/graphql` — `GraphQlService`, a thin GraphQL POST helper mirroring
  `projects/frontend/src/lib/graphql.ts`'s `gqlRequest()`.
- `lib/core/router` — `createAppRouter()` builds the `go_router` route table
  (`app_router.dart`, mirroring `projects/frontend/src/router/index.ts`) as a factory
  rather than a bare singleton, so tests can get a fresh `GoRouter` per test instead of
  leaking navigation state across `pumpWidget` calls. The nav drawer's section/item
  data (`nav_items.dart`) mirrors `AppHeader.vue`'s
  `mobileNavSections`/`desktopNavSections`.
- `lib/core/widgets/app_shell.dart` — persistent chrome (app bar, drawer, bottom nav)
  wrapped around every route via a `ShellRoute`, equivalent to `AppHeader.vue`.
- `lib/core/theme` — dark-first `ThemeData` (mirrors the web app's dark-first rule —
  never fall back to `prefers-color-scheme`).
- `lib/features/<area>/*_screens.dart` — one file per feature area grouping the
  (currently empty) screens for that area's routes, each built from the shared
  `PlaceholderScreen` widget (bare content, no nested `Scaffold`/`AppBar` — it's always
  rendered as `AppShell`'s `Scaffold.body`) and naming the Vue view it mirrors.
- `lib/l10n/app_{en,sk,de}.arb` — source strings for `flutter gen-l10n`, covering the
  app shell/nav/auth chrome today. Mirrors the web app's `en`/`sk`/`de` locale support;
  extend with a screen's own keys as that screen is implemented.

## Known follow-ups (not yet done in this scaffold)

- `url_launcher` for the Discord nav link and an in-app panel for Chat (both currently no-ops in `AppShell._handleTap`).
- Biatec OIDC sign-in — the web app's redirect flow needs a native OIDC/AppAuth flow or in-app browser tab; out of scope for the Bearer-JWT base auth wired up here.
- Wiring `GraphQlService`/`AuthState` into the actual login/register/gameplay screens as each is implemented (see ROADMAP.md).
- `integration_test`-based end-to-end coverage once real screens exist (see Test section above).
