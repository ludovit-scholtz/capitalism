import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/auth/biatec_oidc_service.dart';
import 'package:capitalism_app/features/auth/auth_screens.dart';
import 'package:capitalism_app/features/chat/chat_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/app_harness.dart';
import 'support/fake_id_token.dart';
import 'support/fake_url_opener.dart';
import 'support/fake_web_authenticator.dart';
import 'support/in_memory_token_storage.dart';

Future<void> _openDrawer(WidgetTester tester) async {
  await tester.tap(find.byIcon(Icons.menu));
  await tester.pumpAndSettle();
}

void main() {
  group('Discord nav item', () {
    testWidgets('opens the external link instead of navigating', (tester) async {
      final fakeOpener = FakeUrlOpener();
      await pumpCapitalismApp(tester, urlOpener: fakeOpener);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Discord'));
      await tester.pumpAndSettle();

      expect(fakeOpener.openedUrls, ['https://discord.gg/PhHSxJvDn6']);
      expect(find.text('Get Started'), findsOneWidget); // still on Home, nothing navigated
      expect(find.byType(Drawer), findsNothing); // drawer still closed itself
    });
  });

  group('Chat panel', () {
    testWidgets('opens from the drawer and can be dismissed', (tester) async {
      await pumpCapitalismApp(tester, authenticated: true);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Chat'));
      await tester.pumpAndSettle();

      expect(find.byType(ChatPanel), findsOneWidget);
      expect(find.text('Not implemented yet. Mirrors the chat side panel in AppHeader.vue.'), findsOneWidget);

      await tester.tap(find.byTooltip('Close'));
      await tester.pumpAndSettle();

      expect(find.byType(ChatPanel), findsNothing);
    });
  });

  group('Biatec sign-in button', () {
    testWidgets('succeeds and stores the returned token in AuthState', (tester) async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        final state = authorizeUrl.queryParameters['state']!;
        final nonce = authorizeUrl.queryParameters['nonce']!;
        final token = buildFakeIdToken({
          'nonce': nonce,
          'iss': 'https://google.biatec.io',
          'aud': 'capitalism',
          'exp': DateTime.now().add(const Duration(hours: 1)).millisecondsSinceEpoch ~/ 1000,
        });
        return '${authorizeUrl.queryParameters['redirect_uri']}?state=$state&id_token=$token';
      });
      final auth = AuthState(storage: InMemoryTokenStorage());

      await tester.pumpWidget(
        ChangeNotifierProvider<AuthState>.value(
          value: auth,
          // LoginScreen has no Scaffold of its own (it's shown inside
          // AppShell's Scaffold via the router) but ScaffoldMessenger needs
          // one to actually present the SnackBar, so provide one here too.
          child: MaterialApp(
            home: Scaffold(body: LoginScreen(oidcService: BiatecOidcService(authenticator: authenticator))),
          ),
        ),
      );

      expect(auth.isAuthenticated, isFalse);

      await tester.tap(find.text('Sign in with Biatec'));
      await tester.pumpAndSettle();

      expect(auth.isAuthenticated, isTrue);
      expect(find.text('Signed in with Biatec.'), findsOneWidget);
    });

    testWidgets('surfaces a failure via snackbar without authenticating', (tester) async {
      final authenticator = FakeWebAuthenticator((authorizeUrl) {
        return '${authorizeUrl.queryParameters['redirect_uri']}?error=access_denied&error_description=User+declined';
      });
      final auth = AuthState(storage: InMemoryTokenStorage());

      await tester.pumpWidget(
        ChangeNotifierProvider<AuthState>.value(
          value: auth,
          // LoginScreen has no Scaffold of its own (it's shown inside
          // AppShell's Scaffold via the router) but ScaffoldMessenger needs
          // one to actually present the SnackBar, so provide one here too.
          child: MaterialApp(
            home: Scaffold(body: LoginScreen(oidcService: BiatecOidcService(authenticator: authenticator))),
          ),
        ),
      );

      await tester.tap(find.text('Sign in with Biatec'));
      await tester.pumpAndSettle();

      expect(auth.isAuthenticated, isFalse);
      expect(find.text('User declined'), findsOneWidget);
    });
  });
}
