# Capitalism — Flutter App

Mobile client for the Capitalism MMO, providing the same game-playing interface as
`projects/frontend` (the Vue web app) against the same GraphQL game API.

## Status

This is a **base scaffold**: routing, theming, i18n plumbing, a GraphQL client, and
token-based auth state are wired up, but every screen is an empty placeholder. See
`ROADMAP.md` (`### Flutter mobile app`) for the one-line-per-screen implementation
backlog, and `CLAUDE.md` / `.github/copilot-instructions.md` for the architecture and
conventions this app follows.

## One-time setup (requires the Flutter SDK — not available in the environment this
scaffold was generated in)

```bash
cd projects/flutter_app
flutter create . --org io.biatec --project-name capitalism_app --platforms android,ios,web
flutter pub get
```

`flutter create .` fills in the native platform runners (`android/`, `ios/`, `web/`,
etc.) around the existing `lib/`, `pubspec.yaml`, and `test/` — running it in-place
does not overwrite the Dart source already committed here. `flutter pub get` also
regenerates `lib/l10n/generated/app_localizations.dart` from the ARB files under
`lib/l10n/` (both directories are gitignored — regenerate, don't hand-edit).

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

## Architecture

- `lib/core/config` — build-time GraphQL endpoint configuration (`--dart-define`).
- `lib/core/auth` — `AuthState` (ChangeNotifier) holds the player's Bearer JWT in
  `flutter_secure_storage`. The backend's `login`/`register` GraphQL mutations return
  the raw JWT in the response payload (in addition to setting the browser's HttpOnly
  session cookie), so this app authenticates with `Authorization: Bearer <token>`
  headers — no backend changes were needed.
- `lib/core/graphql` — `GraphQlService`, a thin GraphQL POST helper mirroring
  `projects/frontend/src/lib/graphql.ts`'s `gqlRequest()`.
- `lib/core/router` — `go_router` route table (`app_router.dart`) mirroring
  `projects/frontend/src/router/index.ts`, and the nav drawer's section/item data
  (`nav_items.dart`) mirroring `AppHeader.vue`'s `mobileNavSections`/`desktopNavSections`.
- `lib/core/widgets/app_shell.dart` — persistent chrome (app bar, drawer, bottom nav)
  wrapped around every route via a `ShellRoute`, equivalent to `AppHeader.vue`.
- `lib/core/theme` — dark-first `ThemeData` (mirrors the web app's dark-first rule —
  never fall back to `prefers-color-scheme`).
- `lib/features/<area>/*_screens.dart` — one file per feature area grouping the
  (currently empty) screens for that area's routes, each built from the shared
  `PlaceholderScreen` widget and naming the Vue view it mirrors.
- `lib/l10n/app_{en,sk,de}.arb` — source strings for `flutter gen-l10n`, covering the
  app shell/nav/auth chrome today. Mirrors the web app's `en`/`sk`/`de` locale support;
  extend with a screen's own keys as that screen is implemented.

## Known follow-ups (not yet done in this scaffold)

- Native platform folders (`android/`, `ios/`, `web/`, …) — generate via `flutter create .` above.
- `url_launcher` for the Discord nav link and an in-app panel for Chat (both currently no-ops in `AppShell._handleTap`).
- Biatec OIDC sign-in — the web app's redirect flow needs a native OIDC/AppAuth flow or in-app browser tab; out of scope for the Bearer-JWT base auth wired up here.
- Wiring `GraphQlService`/`AuthState` into the actual login/register/gameplay screens as each is implemented (see ROADMAP.md).
