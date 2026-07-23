import 'package:capitalism_app/app.dart';
import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/auth/web_authenticator.dart';
import 'package:capitalism_app/core/config/game_server_state.dart';
import 'package:capitalism_app/core/context/account_context_service.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:capitalism_app/core/router/app_router.dart';
import 'package:capitalism_app/core/services/url_opener.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'fake_account_context_service.dart';
import 'fake_graphql_client.dart';
import 'in_memory_selected_city_storage.dart';
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
/// Uses a taller-than-default virtual screen because the drawer's
/// `ListView` only mounts children within the viewport + cache extent, even
/// for a non-lazy `ListView(children: ...)` — items below the default
/// 800x600 test viewport would otherwise be invisible to `find.text`.
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
  GoRouter? router,
}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));

  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) {
    await auth.setToken('test-token');
  }
  if (admin) {
    auth.setIsAdmin(true);
  }
  final gameServerState = GameServerState(storage: InMemorySelectedGameServerStorage());
  final accountContextState = AccountContextState(storage: InMemorySelectedCityStorage());

  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<GameServerState>.value(value: gameServerState),
        ChangeNotifierProvider<AccountContextState>.value(value: accountContextState),
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
            ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return auth;
}
