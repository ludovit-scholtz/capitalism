import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/recent_building_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_detail_screen.dart';
import 'package:capitalism_app/features/buildings/building_grid_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_building_analytics_service.dart';
import 'support/fake_building_detail_service.dart';
import 'support/fake_building_sales_service.dart';
import 'support/fake_building_sourcing_service.dart';
import 'support/fake_tutorial_service.dart';
import 'support/in_memory_selected_building_storage.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _salesUnit = BuildingUnitDetail(
  id: 'unit-1',
  unitType: 'PUBLIC_SALES',
  level: 1,
  resourceTypeId: null,
  productTypeId: 'product-1',
  minPrice: 10,
  gridX: 0,
  gridY: 0,
);

const _building = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
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
        builder: (context, state) => Scaffold(
          body: BuildingDetailScreen(
            buildingId: 'building-1',
            buildingDetailService: service,
            tutorialService: FakeTutorialService(),
            buildingSalesService: FakeBuildingSalesService(),
            buildingSourcingService: FakeBuildingSourcingService(),
            buildingAnalyticsService: FakeBuildingAnalyticsService(),
          ),
        ),
      ),
      GoRoute(
        path: '/building/:id/sell',
        builder: (context, state) => Scaffold(body: Text('Sell ${state.pathParameters['id']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<RecentBuildingState>.value(
          value: RecentBuildingState(storage: InMemorySelectedBuildingStorage()),
        ),
        ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
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

    testWidgets('staging then storing an upgrade in edit mode calls scheduleUnitUpgrade', (tester) async {
      // Upgrade now lives only in edit mode's per-unit Maintenance tab
      // (matching web — "Unit Upgrade Panel removed from read-only view; it
      // now lives in edit mode only"), applied via the outer Layouts tab's
      // Save button rather than a one-shot button on the unit itself.
      const upgradeInfo = UnitUpgradeInfo(
        unitId: 'unit-1',
        unitType: 'PUBLIC_SALES',
        currentLevel: 1,
        nextLevel: 2,
        isMaxLevel: false,
        isUpgradable: true,
        upgradeCost: 100,
        upgradeTicks: 5,
        currentStat: 1,
        nextStat: 2,
        statLabel: 'Throughput',
        currentLaborHoursPerTick: 1,
        nextLaborHoursPerTick: 1,
        currentEnergyMwhPerTick: 1,
        nextEnergyMwhPerTick: 1,
        currentLaborCostPerTick: 1,
        nextLaborCostPerTick: 1,
        currentEnergyCostPerTick: 1,
        nextEnergyCostPerTick: 1,
        currentStorageCapacity: 10,
        nextStorageCapacity: 20,
      );
      final service = FakeBuildingDetailService(building: _building, productNames: {'product-1': 'Steel Beams'}, unitUpgradeInfo: upgradeInfo);

      await _pumpBuildingDetail(tester, service: service);
      await tester.tap(find.widgetWithText(TextButton, 'Edit Building'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('draft-cell-unit-unit-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('building-tab-maintenance')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(OutlinedButton, 'Stage Upgrade'));
      await tester.pumpAndSettle();

      // Close the per-unit pane to get back to the outer edit tabs, then
      // switch to Layouts to reach the Save button.
      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('building-tab-layouts')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Store Upgrade (1 queued)'));
      await tester.pumpAndSettle();

      expect(service.upgradedUnitIds, ['unit-1']);
    });

    testWidgets('updating price on a PUBLIC_SALES unit calls updatePublicSalesPrice', (tester) async {
      final service = FakeBuildingDetailService(building: _building);

      await _pumpBuildingDetail(tester, service: service);
      await tester.tap(find.byKey(const ValueKey('cell-unit-unit-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('building-tab-quickActions')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Update price'));
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
