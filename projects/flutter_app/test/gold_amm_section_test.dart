import 'package:capitalism_app/features/exchange/forex_models.dart';
import 'package:capitalism_app/features/exchange/gold_amm_section.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_forex_service.dart';

const _pool = GoldAmmPool(id: 'pool-1', currencyCode: 'EUR', currencySymbol: '€', fiatReserve: 10000, goldReserve: 100, impliedGoldPrice: 100);

const _poolWithPosition = GoldAmmPool(
  id: 'pool-2',
  currencyCode: 'USD',
  currencySymbol: '\$',
  fiatReserve: 5000,
  goldReserve: 50,
  impliedGoldPrice: 100,
  myPosition: GoldAmmPosition(id: 'position-1', liquidityShares: 10, sharePercent: 5, claimableFiat: 500, claimableGold: 5),
);

Future<void> _pump(WidgetTester tester, {required FakeForexService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(MaterialApp(home: Scaffold(body: SingleChildScrollView(child: GoldAmmSection(forexService: service)))));
  await tester.pumpAndSettle();
}

void main() {
  group('GoldAmmSection', () {
    testWidgets('shows my gold balance and the pool list', (tester) async {
      final service = FakeForexService(
        goldPools: const [_pool],
        goldBalance: const GoldBalance(balance: 12.5, blockedInPools: 2.5, availableBalance: 10),
      );

      await _pump(tester, service: service);

      expect(find.text('Balance: 12.5000'), findsOneWidget);
      expect(find.text('EUR'), findsWidgets);
      expect(find.text('Fiat reserve: 10000.00 · Gold reserve: 100.0000'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeForexService(loadError: Exception('down'));

      await _pump(tester, service: service);

      expect(find.text('Could not load the gold market. Please try again.'), findsOneWidget);
    });

    testWidgets('getting a swap quote and confirming calls executeGoldSwap', (tester) async {
      final service = FakeForexService(goldPools: const [_pool]);

      await _pump(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Get quote'));
      await tester.pumpAndSettle();

      expect(find.textContaining('You receive:'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Confirm swap'));
      await tester.pumpAndSettle();

      expect(service.lastGoldSwapArgs?['direction'], 'FIAT_TO_GOLD');
      expect(service.lastGoldSwapArgs?['currencyCode'], 'EUR');
    });

    testWidgets('shows Remove liquidity only for pools with a position', (tester) async {
      final service = FakeForexService(goldPools: const [_pool, _poolWithPosition]);

      await _pump(tester, service: service);

      expect(find.widgetWithText(OutlinedButton, 'Remove liquidity'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Add liquidity'), findsNWidgets(2));
    });

    testWidgets('removing liquidity confirms and calls removeGoldLiquidity for the full position', (tester) async {
      final service = FakeForexService(goldPools: const [_poolWithPosition]);

      await _pump(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Remove liquidity'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Remove all'));
      await tester.pumpAndSettle();

      expect(service.lastRemoveLiquidityArgs, {'positionId': 'position-1', 'shareFraction': 1.0});
    });

    testWidgets('creating a pool calls createGoldPool with entered values', (tester) async {
      final service = FakeForexService(goldPools: const []);

      await _pump(tester, service: service);
      await tester.tap(find.text('New pool'));
      await tester.pumpAndSettle();
      await tester.enterText(find.widgetWithText(TextField, 'Currency code'), 'gbp');
      await tester.enterText(find.widgetWithText(TextField, 'Fiat amount'), '1000');
      await tester.enterText(find.widgetWithText(TextField, 'Gold amount'), '10');
      await tester.tap(find.widgetWithText(FilledButton, 'Create'));
      await tester.pumpAndSettle();

      expect(service.lastCreatePoolArgs, {'currencyCode': 'GBP', 'fiatAmount': 1000.0, 'goldAmount': 10.0});
    });
  });
}
