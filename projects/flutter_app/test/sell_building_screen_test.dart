import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/buildings/sell_building_models.dart';
import 'package:capitalism_app/features/buildings/sell_building_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_sell_building_service.dart';
import 'support/in_memory_token_storage.dart';

const _valuation = BuildingMarketValuation(totalValue: 100000, minimumSalePrice: 70000, currencyCode: 'EUR');

const _notListed = SellableBuilding(
  id: 'building-1',
  name: 'Main Factory',
  type: 'FACTORY',
  level: 2,
  isForSale: false,
  askingPrice: null,
  isCollateralized: false,
  marketValuation: _valuation,
);

const _listed = SellableBuilding(
  id: 'building-1',
  name: 'Main Factory',
  type: 'FACTORY',
  level: 2,
  isForSale: true,
  askingPrice: 90000,
  isCollateralized: false,
  marketValuation: _valuation,
);

const _collateralized = SellableBuilding(
  id: 'building-1',
  name: 'Main Factory',
  type: 'FACTORY',
  level: 2,
  isForSale: false,
  askingPrice: null,
  isCollateralized: true,
  marketValuation: _valuation,
);

Future<GoRouter> _pumpSellBuilding(WidgetTester tester, {required FakeSellBuildingService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => Scaffold(body: SellBuildingScreen(buildingId: 'building-1', sellBuildingService: service)),
      ),
      GoRoute(path: '/dashboard', builder: (context, state) => const Scaffold(body: Text('Dashboard Screen'))),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('SellBuildingScreen', () {
    testWidgets('shows valuation and lists the building for sale', (tester) async {
      final service = FakeSellBuildingService(building: _notListed);

      await _pumpSellBuilding(tester, service: service);

      expect(find.text('Estimated value: 100000 EUR'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'List for sale'));
      await tester.pumpAndSettle();

      expect(service.lastSetForSaleArgs?['isForSale'], true);
    });

    testWidgets('shows Cancel listing when already for sale', (tester) async {
      final service = FakeSellBuildingService(building: _listed);

      await _pumpSellBuilding(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel listing'));
      await tester.pumpAndSettle();

      expect(service.lastSetForSaleArgs?['isForSale'], false);
    });

    testWidgets('collateralized buildings cannot be listed or destroyed', (tester) async {
      final service = FakeSellBuildingService(building: _collateralized);

      await _pumpSellBuilding(tester, service: service);

      expect(find.text('This building is locked as loan collateral and cannot be sold or destroyed.'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'List for sale'), findsNothing);
    });

    testWidgets('destroying requires confirmation and navigates to dashboard', (tester) async {
      final service = FakeSellBuildingService(building: _notListed);

      await _pumpSellBuilding(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Destroy building'));
      await tester.pumpAndSettle();
      expect(find.text('Destroy this building?'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Destroy'));
      await tester.pumpAndSettle();

      expect(service.destroyedBuildingId, 'building-1');
      expect(find.text('Dashboard Screen'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeSellBuildingService(fetchError: Exception('down'));

      await _pumpSellBuilding(tester, service: service);

      expect(find.text('Could not load this building. Please try again.'), findsOneWidget);
    });
  });
}
