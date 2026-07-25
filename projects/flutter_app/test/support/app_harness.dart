import 'package:capitalism_app/app.dart';
import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/auth/web_authenticator.dart';
import 'package:capitalism_app/core/config/app_config.dart';
import 'package:capitalism_app/core/config/app_environment.dart';
import 'package:capitalism_app/core/config/app_environment_state.dart';
import 'package:capitalism_app/core/config/game_server_state.dart';
import 'package:capitalism_app/core/context/account_context_service.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:capitalism_app/core/context/recent_building_state.dart';
import 'package:capitalism_app/core/game_state/game_state_service.dart';
import 'package:capitalism_app/core/game_state/game_state_state.dart';
import 'package:capitalism_app/core/router/app_router.dart';
import 'package:capitalism_app/core/services/url_opener.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'fake_account_context_service.dart';
import 'fake_game_state_service.dart';
import 'fake_graphql_client.dart';
import 'in_memory_selected_building_storage.dart';
import 'in_memory_selected_city_storage.dart';
import 'in_memory_selected_environment_storage.dart';
import 'in_memory_selected_game_server_storage.dart';
import 'in_memory_token_storage.dart';

/// Pumps a real [CapitalismApp] with a fresh [AuthState] and a fresh
/// [createAppRouter] instance (never the shared default singleton, which
/// would leak navigation state across tests). Platform-channel-backed
/// dependencies (secure storage, GraphQL calls, external URL launches, the
/// Biatec OIDC round trip) are faked by default; pass the corresponding
/// parameter to observe/intercept/customize them, or pass a fully-built
/// [router] directly (e.g. via [createAppRouter]) when a test needs to keep
/// a reference to it for `router.go(...)` navigation afterwards.
///
/// Defaults to a taller-than-default virtual screen because the drawer's
/// `ListView` only mounts children within the viewport + cache extent, even
/// for a non-lazy `ListView(children: ...)` — items below the default
/// 800x600 test viewport would otherwise be invisible to `find.text`. The
/// default width (800) is below [AppShell]'s wide-screen breakpoint
/// (1024) — pass a wider [surfaceSize] to exercise the wide-screen layout.
Future<AuthState> pumpCapitalismApp(
  WidgetTester tester, {
  bool authenticated = false,
  bool admin = false,
  UrlOpener urlOpener = const ExternalUrlOpener(),
  http.Client? httpClient,
  http.Client? passwordResetHttpClient,
  WebAuthenticator? webAuthenticator,
  bool? passwordAuthEnabled,
  AccountContextService? accountContextService,
  GameStateService? gameStateService,
  GoRouter? router,
  Size surfaceSize = const Size(800, 2400),
}) async {
  await tester.binding.setSurfaceSize(surfaceSize);
  addTearDown(() => tester.binding.setSurfaceSize(null));

  // AppConfig's environment/graphqlUrl are process-wide static state (see
  // `app_config.dart`) rather than something threaded through the widget
  // tree, so every test run resets them to a known-good baseline instead of
  // inheriting whatever an earlier test in the same file left behind (e.g.
  // an environment-switch test). Stage has no fixed default game endpoint
  // by design (see `AppEnvironment.defaultGameGraphqlUrl`) — tests need a
  // non-empty one so `GraphQlService` doesn't short-circuit with "no game
  // server selected" before ever reaching the injected fake `httpClient`.
  AppConfig.setEnvironment(AppEnvironment.stage);
  AppConfig.setGraphqlUrl('https://example.test/graphql');

  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) {
    await auth.setToken('test-token');
  }
  if (admin) {
    auth.setIsAdmin(true);
  }
  final environmentState = AppEnvironmentState(storage: InMemorySelectedEnvironmentStorage());
  final gameServerState = GameServerState(storage: InMemorySelectedGameServerStorage());
  final accountContextState = AccountContextState(storage: InMemorySelectedCityStorage());
  final recentBuildingState = RecentBuildingState(storage: InMemorySelectedBuildingStorage());
  final gameStateState = GameStateState();
  addTearDown(gameStateState.dispose);

  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<AppEnvironmentState>.value(value: environmentState),
        ChangeNotifierProvider<GameServerState>.value(value: gameServerState),
        ChangeNotifierProvider<AccountContextState>.value(value: accountContextState),
        ChangeNotifierProvider<RecentBuildingState>.value(value: recentBuildingState),
        ChangeNotifierProvider<GameStateState>.value(value: gameStateState),
      ],
      child: CapitalismApp(
        router:
            router ??
            createAppRouter(
              urlOpener: urlOpener,
              httpClient: httpClient ?? fakeHomeStatusClient(),
              passwordResetHttpClient: passwordResetHttpClient,
              webAuthenticator: webAuthenticator,
              passwordAuthEnabled: passwordAuthEnabled,
              accountContextService: accountContextService ?? FakeAccountContextService(),
              gameStateService: gameStateService ?? FakeGameStateService(),
            ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return auth;
}
