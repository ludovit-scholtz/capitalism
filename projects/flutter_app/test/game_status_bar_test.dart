import 'package:capitalism_app/core/context/account_context_models.dart';
import 'package:capitalism_app/core/game_state/game_state_model.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/app_harness.dart';
import 'support/fake_account_context_service.dart';
import 'support/fake_game_state_service.dart';

Future<void> _openDrawer(WidgetTester tester) async {
  await tester.tap(find.byIcon(Icons.menu));
  await tester.pumpAndSettle();
}

void main() {
  group('AppShell responsive nav bar', () {
    testWidgets('wide screen keeps the context switcher in the app bar alongside balance/tick chips', (
      tester,
    ) async {
      await pumpCapitalismApp(
        tester,
        authenticated: true,
        surfaceSize: const Size(1280, 900),
        accountContextService: FakeAccountContextService(
          activeAccount: const ActiveAccountInfo(
            playerId: 'player-1',
            displayName: 'Ada',
            availableCash: 4200,
            activeAccountType: 'PERSON',
            activeCompanyId: null,
          ),
        ),
        gameStateService: FakeGameStateService(
          gameState: GameStateModel(
            currentTick: 42,
            lastTickAtUtc: DateTime.now().toUtc(),
            tickIntervalSeconds: 10,
            taxRate: 15,
          ),
        ),
      );

      expect(find.byKey(const Key('context-switcher-trigger')), findsOneWidget);
      expect(find.byKey(const Key('nav-balance-chip')), findsOneWidget);
      expect(find.byKey(const Key('nav-tick-chip')), findsOneWidget);
      expect(find.text('\$4.2K'), findsOneWidget);
      expect(find.text('Jan 2, 2000 18:00'), findsOneWidget);

      // Not duplicated into the drawer on wide screens.
      await _openDrawer(tester);
      expect(find.byKey(const Key('context-switcher-trigger')), findsOneWidget);
    });

    testWidgets('narrow screen shows only balance/tick chips in the app bar and moves the switcher to the drawer', (
      tester,
    ) async {
      await pumpCapitalismApp(
        tester,
        authenticated: true,
        // Default harness width (800) — below AppShell's 1024 breakpoint,
        // so this exercises the narrow layout without hitting unrelated
        // pre-existing overflow issues on phone-sized viewports elsewhere
        // in the app (e.g. HomeScreen's leaderboard row).
        accountContextService: FakeAccountContextService(
          activeAccount: const ActiveAccountInfo(
            playerId: 'player-1',
            displayName: 'Ada',
            availableCash: 1000,
            activeAccountType: 'PERSON',
            activeCompanyId: null,
          ),
        ),
        gameStateService: FakeGameStateService(
          gameState: GameStateModel(
            currentTick: 7,
            lastTickAtUtc: DateTime.now().toUtc(),
            tickIntervalSeconds: 10,
            taxRate: 15,
          ),
        ),
      );

      expect(find.byKey(const Key('context-switcher-trigger')), findsNothing);
      expect(find.byKey(const Key('nav-balance-chip')), findsOneWidget);
      expect(find.byKey(const Key('nav-tick-chip')), findsOneWidget);
      expect(find.text('\$1K'), findsOneWidget);
      expect(find.text('Jan 1, 2000 07:00'), findsOneWidget);

      await _openDrawer(tester);
      expect(find.byKey(const Key('context-switcher-trigger')), findsOneWidget);
    });

    testWidgets('unauthenticated header shows neither the switcher nor the status chips', (tester) async {
      await pumpCapitalismApp(tester, surfaceSize: const Size(1280, 900));

      expect(find.byKey(const Key('context-switcher-trigger')), findsNothing);
      expect(find.byKey(const Key('nav-balance-chip')), findsNothing);
      expect(find.byKey(const Key('nav-tick-chip')), findsNothing);
      expect(find.widgetWithText(AppBar, 'CAPITALISM'), findsOneWidget);
    });

    testWidgets('shows the active company balance instead of the person balance in company mode', (tester) async {
      await pumpCapitalismApp(
        tester,
        authenticated: true,
        surfaceSize: const Size(1280, 900),
        accountContextService: FakeAccountContextService(
          activeAccount: const ActiveAccountInfo(
            playerId: 'player-1',
            displayName: 'Ada',
            availableCash: 999,
            activeAccountType: 'COMPANY',
            activeCompanyId: 'company-1',
          ),
          companies: const [
            ContextCompanyOption(id: 'company-1', name: 'Acme Corp', cash: 7777, buildingCityIds: []),
          ],
        ),
      );

      expect(find.text('\$7.78K'), findsOneWidget);
    });
  });
}
