import 'package:capitalism_app/app.dart';
import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/router/app_router.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/in_memory_token_storage.dart';

Future<AuthState> _pumpApp(WidgetTester tester, {bool authenticated = false, bool admin = false}) async {
  // The default 800x600 test surface is too short to mount every drawer
  // section's ListTiles (ListView only mounts children within the viewport
  // + cache extent, even for a non-lazy `ListView(children: ...)`) — use a
  // tall virtual screen so `find.text`/`find.widgetWithText` can see them
  // all without needing to scroll the drawer during the test.
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
      // A fresh router per pump — the app's default `createAppRouter()`
      // singleton would otherwise leak navigation state across tests.
      child: CapitalismApp(router: createAppRouter()),
    ),
  );
  await tester.pumpAndSettle();
  return auth;
}

Future<void> _openDrawer(WidgetTester tester) async {
  await tester.tap(find.byIcon(Icons.menu));
  await tester.pumpAndSettle();
}

Finder _bottomNavLabel(String label) =>
    find.descendant(of: find.byType(NavigationBar), matching: find.text(label));

Finder _placeholderBodyFor(String sourceView) =>
    find.text('Not implemented yet. Mirrors $sourceView in the web frontend.');

void main() {
  group('basic navigation', () {
    testWidgets('boots to the Home screen with app bar and bottom nav visible', (tester) async {
      await _pumpApp(tester);

      expect(find.widgetWithText(AppBar, 'Capitalism'), findsOneWidget);
      expect(find.byType(NavigationBar), findsOneWidget);
      expect(_placeholderBodyFor('HomeView.vue'), findsOneWidget);
    });

    testWidgets('drawer hides auth-only items and the Administration section when signed out', (tester) async {
      await _pumpApp(tester);
      await _openDrawer(tester);

      expect(find.text('Main'), findsOneWidget);
      expect(find.text('Economy'), findsOneWidget);
      expect(find.text('Build'), findsOneWidget);
      expect(find.text('Social'), findsOneWidget);
      expect(find.text('Administration'), findsNothing);

      expect(find.widgetWithText(ListTile, 'Dashboard'), findsNothing);
      expect(find.widgetWithText(ListTile, 'Forex'), findsNothing);
      expect(find.widgetWithText(ListTile, 'Operations'), findsNothing);

      expect(find.widgetWithText(ListTile, 'Leaderboard'), findsOneWidget);
      expect(find.widgetWithText(ListTile, 'Discord'), findsOneWidget);
    });

    testWidgets('tapping a public drawer item navigates and closes the drawer', (tester) async {
      await _pumpApp(tester);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Leaderboard'));
      await tester.pumpAndSettle();

      expect(_placeholderBodyFor('LeaderboardView.vue'), findsOneWidget);
      expect(find.byType(Drawer), findsNothing);
    });

    testWidgets('bottom nav switches between Home, Exchange and News and highlights the active tab', (
      tester,
    ) async {
      await _pumpApp(tester);

      await tester.tap(_bottomNavLabel('News'));
      await tester.pumpAndSettle();
      expect(_placeholderBodyFor('NewsView.vue'), findsOneWidget);
      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 3);

      await tester.tap(_bottomNavLabel('Exchange'));
      await tester.pumpAndSettle();
      expect(_placeholderBodyFor('GlobalExchangeView.vue'), findsOneWidget);
      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 2);

      await tester.tap(_bottomNavLabel('Home'));
      await tester.pumpAndSettle();
      expect(_placeholderBodyFor('HomeView.vue'), findsOneWidget);
      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 0);
    });

    testWidgets('signing in reveals auth-only nav items and reaches the Dashboard', (tester) async {
      await _pumpApp(tester, authenticated: true);
      await _openDrawer(tester);

      expect(find.widgetWithText(ListTile, 'Dashboard'), findsOneWidget);
      expect(find.widgetWithText(ListTile, 'Forex'), findsOneWidget);
      expect(find.widgetWithText(ListTile, 'Operations'), findsNothing);

      await tester.tap(find.widgetWithText(ListTile, 'Dashboard'));
      await tester.pumpAndSettle();

      expect(_placeholderBodyFor('DashboardView.vue'), findsOneWidget);
    });

    testWidgets('admin players see the Administration section and reach Operations', (tester) async {
      await _pumpApp(tester, authenticated: true, admin: true);
      await _openDrawer(tester);

      expect(find.text('Administration'), findsOneWidget);

      await tester.tap(find.widgetWithText(ListTile, 'Operations'));
      await tester.pumpAndSettle();

      expect(_placeholderBodyFor('OperationsOverviewView.vue'), findsOneWidget);
    });
  });
}
