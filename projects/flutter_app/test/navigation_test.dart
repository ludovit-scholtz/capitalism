import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';

Finder _bottomNavLabel(String label) =>
    find.descendant(of: find.byType(NavigationBar), matching: find.text(label));

Finder _placeholderBodyFor(String sourceView) =>
    find.text('Not implemented yet. Mirrors $sourceView in the web frontend.');

Future<void> _openDrawer(WidgetTester tester) async {
  await tester.tap(find.byIcon(Icons.menu));
  await tester.pumpAndSettle();
}

void main() {
  group('basic navigation', () {
    testWidgets('boots to the Home screen with app bar, hero CTA and bottom nav visible', (tester) async {
      await pumpCapitalismApp(tester);

      expect(find.widgetWithText(AppBar, 'CAPITALISM'), findsOneWidget);
      expect(find.byType(NavigationBar), findsOneWidget);
      expect(find.text('Get Started'), findsOneWidget);
      expect(find.text('Tick'), findsOneWidget);
    });

    testWidgets('drawer hides auth-only items and the Administration section when signed out', (tester) async {
      await pumpCapitalismApp(tester);
      await _openDrawer(tester);

      // Section headers are rendered upper-cased (HUD styling) — see AppShell.
      expect(find.text('MAIN'), findsOneWidget);
      expect(find.text('ECONOMY'), findsOneWidget);
      expect(find.text('BUILD'), findsOneWidget);
      expect(find.text('SOCIAL'), findsOneWidget);
      expect(find.text('ADMINISTRATION'), findsNothing);

      expect(find.widgetWithText(ListTile, 'Dashboard'), findsNothing);
      expect(find.widgetWithText(ListTile, 'Forex'), findsNothing);
      expect(find.widgetWithText(ListTile, 'Operations'), findsNothing);

      expect(find.widgetWithText(ListTile, 'Leaderboard'), findsOneWidget);
      expect(find.widgetWithText(ListTile, 'Discord'), findsOneWidget);
    });

    testWidgets('tapping a public drawer item navigates and closes the drawer', (tester) async {
      await pumpCapitalismApp(tester);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Leaderboard'));
      await tester.pumpAndSettle();

      // LeaderboardScreen is real and GraphQL-backed now (not a
      // placeholder); the harness's generic fake HTTP client doesn't shape a
      // `rankings` response with a `playerId` field, so it lands on the
      // screen's own error state — still proves the navigation itself works.
      expect(find.text('Could not load the leaderboard. Please try again.'), findsOneWidget);
      expect(find.byType(Drawer), findsNothing);
    });

    testWidgets('bottom nav switches between Home, Stocks and News and highlights the active tab', (
      tester,
    ) async {
      await pumpCapitalismApp(tester);

      await tester.tap(_bottomNavLabel('News'));
      await tester.pumpAndSettle();
      // NewsScreen is a real GraphQL-backed screen now (not a placeholder);
      // the harness's generic fake HTTP client doesn't shape a
      // `gameNewsFeed` response, so it lands on the screen's own error state.
      expect(find.text('Could not load the news feed. Please try again.'), findsOneWidget);
      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 6);

      await tester.tap(_bottomNavLabel('Stocks'));
      await tester.pumpAndSettle();
      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 5);

      await tester.tap(_bottomNavLabel('Home'));
      await tester.pumpAndSettle();
      expect(find.text('Get Started'), findsOneWidget);
      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 0);
    });

    testWidgets('bottom nav Last Building tab falls back to the building market when nothing was visited yet', (
      tester,
    ) async {
      await pumpCapitalismApp(tester);

      await tester.tap(_bottomNavLabel('Last Building'));
      await tester.pumpAndSettle();

      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 2);
    });

    testWidgets('signing in reveals auth-only nav items, changes the Home CTA, and reaches the Dashboard', (
      tester,
    ) async {
      await pumpCapitalismApp(tester, authenticated: true);

      expect(find.text('Go to Dashboard'), findsOneWidget);
      expect(find.text('Get Started'), findsNothing);

      await _openDrawer(tester);
      expect(find.widgetWithText(ListTile, 'Dashboard'), findsOneWidget);
      expect(find.widgetWithText(ListTile, 'Forex'), findsOneWidget);
      expect(find.widgetWithText(ListTile, 'Operations'), findsNothing);

      await tester.tap(find.widgetWithText(ListTile, 'Dashboard'));
      await tester.pumpAndSettle();

      // DashboardScreen is implemented (no longer a placeholder); the shared
      // fake GraphQL client used by this harness answers every query with
      // the Home status shape, so `myCompanies` resolves empty and the
      // screen lands on its real empty state rather than placeholder text.
      // See test/dashboard_screen_test.dart for full DashboardScreen coverage.
      expect(find.text('You do not have a company yet.'), findsOneWidget);
    });

    testWidgets('admin players see the Administration section and reach Operations', (tester) async {
      await pumpCapitalismApp(tester, authenticated: true, admin: true);
      await _openDrawer(tester);

      expect(find.text('ADMINISTRATION'), findsOneWidget);

      await tester.tap(find.widgetWithText(ListTile, 'Operations'));
      await tester.pumpAndSettle();

      // OperationsOverviewScreen is real and GraphQL-backed now; it gates
      // on the server's `gameAdminSession.canAccessAdminDashboard` (not
      // just the drawer's locally-set `admin` flag used to show the nav
      // item), and the harness's generic fake client doesn't shape that
      // field as true, so it correctly lands on the "admins only" state —
      // still proves the navigation itself reached the Operations screen.
      expect(find.text('Administrators only.'), findsOneWidget);
    });
  });
}
