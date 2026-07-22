import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_detail_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_building_detail_service.dart';
import 'support/in_memory_token_storage.dart';

const _salesUnit = BuildingUnitDetail(
  id: 'unit-1',
  unitType: 'PUBLIC_SALES',
  level: 1,
  resourceTypeId: null,
  productTypeId: 'product-1',
  minPrice: 10,
);

const _building = BuildingDetail(
  id: 'building-1',
  name: 'Main Factory',
  type: 'FACTORY',
  level: 2,
  powerStatus: 'POWERED',
  occupancyPercent: 80,
  isForSale: false,
  units: [_salesUnit],
  pendingConfiguration: null,
);

Future<GoRouter> _pumpBuildingDetail(WidgetTester tester, {required FakeBuildingDetailService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => Scaffold(body: BuildingDetailScreen(buildingId: 'building-1', buildingDetailService: service)),
      ),
      GoRoute(
        path: '/building/:id/sell',
        builder: (context, state) => Scaffold(body: Text('Sell ${state.pathParameters['id']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('BuildingDetailScreen', () {
    testWidgets('shows building overview and units with resolved product names', (tester) async {
      final service = FakeBuildingDetailService(building: _building, productNames: {'product-1': 'Steel Beams'});

      await _pumpBuildingDetail(tester, service: service);

      expect(find.text('Main Factory'), findsOneWidget);
      expect(find.text('FACTORY · Level 2'), findsOneWidget);
      expect(find.text('POWERED'), findsOneWidget);
      expect(find.text('Steel Beams'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeBuildingDetailService(fetchError: Exception('down'));

      await _pumpBuildingDetail(tester, service: service);

      expect(find.text('Could not load this building. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping upgrade calls scheduleUnitUpgrade', (tester) async {
      final service = FakeBuildingDetailService(building: _building);

      await _pumpBuildingDetail(tester, service: service);
      await tester.tap(find.byTooltip('Upgrade'));
      await tester.pumpAndSettle();

      expect(service.upgradedUnitIds, ['unit-1']);
    });

    testWidgets('updating price on a PUBLIC_SALES unit calls updatePublicSalesPrice', (tester) async {
      final service = FakeBuildingDetailService(building: _building);

      await _pumpBuildingDetail(tester, service: service);
      await tester.tap(find.byTooltip('Update price'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '15');
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(service.lastPriceUpdateArgs?['unitId'], 'unit-1');
      expect(service.lastPriceUpdateArgs?['newMinPrice'], 15);
    });

    testWidgets('sell/destroy link navigates to the sell screen', (tester) async {
      final service = FakeBuildingDetailService(building: _building);

      await _pumpBuildingDetail(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Sell or destroy this building'));
      await tester.pumpAndSettle();

      expect(find.text('Sell building-1'), findsOneWidget);
    });
  });
}
