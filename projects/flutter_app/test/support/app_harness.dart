import 'package:capitalism_app/app.dart';
import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/router/app_router.dart';
import 'package:capitalism_app/core/services/url_opener.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'fake_graphql_client.dart';
import 'in_memory_token_storage.dart';

/// Pumps a real [CapitalismApp] with a fresh [AuthState] and a fresh
/// [createAppRouter] instance (never the shared default singleton, which
/// would leak navigation state across tests). Platform-channel-backed
/// dependencies (secure storage, the HomeScreen GraphQL call, external URL
/// launches) are faked by default; pass [urlOpener] to observe/intercept
/// external-link taps.
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

  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(
      value: auth,
      child: CapitalismApp(router: createAppRouter(urlOpener: urlOpener, httpClient: httpClient ?? fakeHomeStatusClient())),
    ),
  );
  await tester.pumpAndSettle();
  return auth;
}
