---
name: flutter-app-testing
description: "Use when: running or writing tests for projects/flutter_app (the Flutter mobile client), including `flutter test`/`flutter analyze` invocation, locating the Flutter SDK when it isn't on PATH, and the project's widget-test conventions (fakes, MultiProvider requirements, viewport sizing, timer/pumpAndSettle gotchas)."
---

# Flutter App Testing

Use this skill when running, debugging, or writing tests for `projects/flutter_app`.
This is a shared skill — both Claude Code and GitHub Copilot read `.github/skills/`,
so keep this file as the single source of truth rather than duplicating it elsewhere.

## Finding the Flutter SDK

`flutter` is not always on `PATH` in this environment. If a bare `flutter` command
fails, locate it first rather than assuming it's missing:

```powershell
where.exe flutter 2>$null
Get-ChildItem -Path C:\ -Filter "flutter.bat" -Recurse -ErrorAction SilentlyContinue -Depth 3
```

It has been found at `C:\tools\flutter\bin` in this environment. Add it to `PATH` for
the current PowerShell session before running any `flutter` command:

```powershell
$env:Path += ";C:\tools\flutter\bin"
```

Do this once per shell session (each `PowerShell`/`Bash` tool call may start a fresh
shell, so it may need repeating per call unless commands are chained with `;`/`&&`).

## Running tests

Always run from `projects/flutter_app`:

```powershell
cd projects/flutter_app
flutter test                              # full suite
flutter test test/foo_test.dart           # one file
flutter test test/foo_test.dart test/bar_test.dart   # a few files together
```

Run `flutter analyze` too — a clean analyze is required alongside a clean test run
before reporting any change to this app complete (per `CLAUDE.md`):

```powershell
flutter analyze
```

For a large run, redirect to a file and grep the summary line rather than scrolling
raw output — `flutter test` output is verbose and the useful signal is the final
`All tests passed!` / `Some tests failed` / `[E]`-tagged failure blocks:

```powershell
flutter test 2>&1 | Out-File -FilePath test_output.txt -Encoding utf8
Select-String -Path test_output.txt -Pattern "All tests passed|Some tests failed"
```

Delete the temp file afterward — don't leave scratch output files in the repo.

## What this test suite is

Pure `flutter_test`/`WidgetTester` widget and unit tests — **no emulator, device, or
native platform build required**. This is the right default for this app; don't reach
for `integration_test` or a real device unless specifically asked.

## Project conventions to know before writing tests

- **Fakes, not mocks.** Every service has a hand-written `Fake<Name>Service implements
  <Name>Service` under `test/support/` (e.g. `FakeBankingService`,
  `FakeAccountContextService`) that records calls and lets you inject canned
  responses/errors via constructor params. Follow this pattern for any new service
  rather than introducing a mocking package.
- **`pumpCapitalismApp` is the preferred harness** (`test/support/app_harness.dart`)
  for anything that needs the full app shell/router. It already wires up `AuthState`,
  `GameServerState`, and `AccountContextState` with in-memory fakes, and accepts
  `authenticated`/`admin`/`router`/service-fake overrides. Prefer it over hand-rolling
  a `MultiProvider` + `MaterialApp.router` unless the screen under test is deliberately
  pumped standalone (common for a single screen/widget test — see almost any
  `test/*_screen_test.dart` for the pattern of a local `_pump` helper).
- **Any widget test that renders `AppShell`** (directly, or via `pumpCapitalismApp`,
  or via a router built with `createAppRouter`) **needs all three providers**:
  `AuthState`, `GameServerState`, `AccountContextState` — `AppShell`'s app bar mounts
  `ContextSwitcher`, which reads `AccountContextState` unconditionally. Missing one
  throws a `ProviderNotFoundException` at pump time. When a screen is pumped standalone
  (not through `AppShell`), it only needs the providers its own `context.read<...>()`
  calls require — check the screen's `initState`/build for which ones that is.
- **Never let a widget test hit the real network.** `GraphQlService`/screen-level
  services default to real HTTP if not given a fake. `createAppRouter(...)` accepts
  `httpClient`/`accountContextService`/etc. for exactly this reason —
  `pumpCapitalismApp` already defaults `accountContextService` to
  `FakeAccountContextService()`. If you add a new app-root `ChangeNotifier` that fetches
  over GraphQL (following the `GameServerState`/`AccountContextState` pattern), thread a
  fake through `createAppRouter` → `AppShell` → the widget that owns the fetch, and wire
  it into `app_harness.dart`, the same way those two were wired.
- **Viewport size matters for anything using `Drawer`/long `ListView`s.** A `ListView`
  only builds children within the viewport + cache extent, even for a non-lazy
  `ListView(children: ...)`. Tests that need to find text/widgets far down a list
  (the nav drawer, a long paginated table) must call
  `tester.binding.setSurfaceSize(const Size(800, <tall enough>))` before pumping —
  `pumpCapitalismApp` already uses `2400`; bump it higher locally (e.g. `6000`) for
  screens with unusually long generated content, and always `addTearDown(() =>
  tester.binding.setSurfaceSize(null))`.
- **`Timer`/`Timer.periodic` in a widget is safe under `flutter_test`'s fake-async
  zone** — real wall-clock time does not pass unless you call `tester.pump(Duration)`,
  so a periodic countdown (e.g. a quote-expiry timer) will not fire mid-test unless you
  explicitly advance it. Just make sure the `State.dispose()` path cancels the timer
  (e.g. via a mutation that succeeds and closes the screen, or an explicit
  `tester.pumpWidget(const SizedBox())` to force disposal) — an uncancelled `Timer`
  left running past a test's end can be flagged as a leaked timer.
- **`find.byIcon(AppIcons.xxx)` works for `FaIcon`s too**, not just Material `Icon`
  widgets — this app uses `font_awesome_flutter`'s `FaIcon` everywhere via the central
  `AppIcons` mapping (`lib/core/theme/app_icons.dart`); don't assume you need a
  different finder for FontAwesome icons.
- **Find a labeled `TextField`/dropdown via `find.widgetWithText(TextField, 'Label
  Text')`**, not by index/type alone, when a screen has more than one text field —
  indices break silently as fields are added. Use `.first`/`.at(n)` only when the
  screen genuinely has multiple identically-labeled fields (e.g. a dialog's field vs.
  a background screen's field with the same label).
- **`ValueKey`/`Key` for anything looped (`for (final x in list) Widget(key:
  ValueKey('prefix-${x.id}'))`)** — this is already the convention throughout the app;
  keep using it so tests can target a specific list item without relying on text
  content or position.

## Fixing common failures

- `ProviderNotFoundException` → a required `ChangeNotifier` provider is missing from
  the pumped widget tree; check what the screen/widget under test actually reads via
  `context.read<...>()`/`context.watch<...>()` and add it.
- `Found 0 widgets with icon/text ...` where you expected a match → usually a viewport
  size issue (see above) or a stale label after an unrelated copy change — re-read the
  current widget source rather than trusting an old assertion.
- `Bad state: Too many elements` on a finder that used to be unique → a new widget with
  matching text/type was added elsewhere on screen (e.g. a new search field also using
  a generic `TextField`); scope the finder more precisely
  (`find.widgetWithText`, a `Key`, or `find.descendant`).
- A test hangs / times out on `pumpAndSettle()` → check for a widget that keeps
  scheduling frames (an uncancelled animation, a `Timer.periodic` whose callback keeps
  calling `setState`) — `pumpAndSettle` will not return while frames keep being
  scheduled real-time-unbounded within its retry budget.
