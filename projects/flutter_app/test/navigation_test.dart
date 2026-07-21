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

      expect(find.widgetWithText(AppBar, 'Capitalism'), findsOneWidget);
      expect(find.byType(NavigationBar), findsOneWidget);
      expect(find.text('Get Started'), findsOneWidget);
      expect(find.text('Tick'), findsOneWidget);
    });

    testWidgets('drawer hides auth-only items and the Administration section when signed out', (tester) async {
      await pumpCapitalismApp(tester);
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
      await pumpCapitalismApp(tester);
      await _openDrawer(tester);

      await tester.tap(find.widgetWithText(ListTile, 'Leaderboard'));
      await tester.pumpAndSettle();

      expect(_placeholderBodyFor('LeaderboardView.vue'), findsOneWidget);
      expect(find.byType(Drawer), findsNothing);
    });

    testWidgets('bottom nav switches between Home, Exchange and News and highlights the active tab', (
      tester,
    ) async {
      await pumpCapitalismApp(tester);

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
      expect(find.text('Get Started'), findsOneWidget);
      expect(tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex, 0);
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

      expect(_placeholderBodyFor('DashboardView.vue'), findsOneWidget);
    });

    testWidgets('admin players see the Administration section and reach Operations', (tester) async {
      await pumpCapitalismApp(tester, authenticated: true, admin: true);
      await _openDrawer(tester);

      expect(find.text('Administration'), findsOneWidget);

      await tester.tap(find.widgetWithText(ListTile, 'Operations'));
      await tester.pumpAndSettle();

      expect(_placeholderBodyFor('OperationsOverviewView.vue'), findsOneWidget);
    });
  });
}
