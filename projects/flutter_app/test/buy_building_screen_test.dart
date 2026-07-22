import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/buildings/buy_building_models.dart';
import 'package:capitalism_app/features/buildings/buy_building_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_buy_building_service.dart';
import 'support/in_memory_token_storage.dart';

const _availableLot = CityLot(
  id: 'lot-1',
  name: 'Riverside Plot',
  district: 'Riverside',
  price: 50000,
  suitableTypes: ['FACTORY'],
  ownerCompanyId: null,
);

const _ownedLot = CityLot(
  id: 'lot-2',
  name: 'Taken Plot',
  district: 'Downtown',
  price: 30000,
  suitableTypes: ['FACTORY'],
  ownerCompanyId: 'someone-else',
);

Future<GoRouter> _pumpBuyBuilding(WidgetTester tester, {required FakeBuyBuildingService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => Scaffold(body: BuyBuildingScreen(companyId: 'company-1', buyBuildingService: service)),
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
  group('BuyBuildingScreen', () {
    testWidgets('walks through city, type, lot, and confirm steps to purchase', (tester) async {
      final service = FakeBuyBuildingService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        lotsByCity: {
          'city-1': [_availableLot, _ownedLot],
        },
      );

      await _pumpBuyBuilding(tester, service: service);

      // Step 1: city
      await tester.tap(find.text('Metropolis'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      // Step 2: building type
      await tester.tap(find.text('FACTORY'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      // Step 3: lot (only the available, suitable lot should show)
      expect(find.text('Riverside Plot'), findsOneWidget);
      expect(find.text('Taken Plot'), findsNothing);
      await tester.tap(find.text('Riverside Plot'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Next'));
      await tester.pumpAndSettle();

      // Step 4: confirm
      await tester.enterText(find.byType(TextField), 'My Factory');
      await tester.tap(find.widgetWithText(FilledButton, 'Purchase'));
      await tester.pumpAndSettle();

      expect(service.lastPurchaseArgs?['companyId'], 'company-1');
      expect(service.lastPurchaseArgs?['lotId'], 'lot-1');
      expect(service.lastPurchaseArgs?['buildingType'], 'FACTORY');
      expect(service.lastPurchaseArgs?['buildingName'], 'My Factory');
      expect(find.text('Dashboard Screen'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on city load failure', (tester) async {
      final service = FakeBuyBuildingService(citiesError: Exception('down'));

      await _pumpBuyBuilding(tester, service: service);

      expect(find.text('Could not load cities. Please try again.'), findsOneWidget);
    });
  });
}
