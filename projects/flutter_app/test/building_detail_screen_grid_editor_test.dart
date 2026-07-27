import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/recent_building_state.dart';
import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_detail_screen.dart';
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
import 'support/in_memory_token_storage.dart';

const _emptyFactory = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Empty Factory',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 0,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
);

const _purchaseUnit = BuildingUnitDetail(id: 'unit-1', unitType: 'PURCHASE', level: 1, resourceTypeId: null, productTypeId: null, minPrice: null, gridX: 0, gridY: 0);

const _factoryWithUnit = BuildingDetail(
  id: 'building-2',
  companyId: 'company-1',
  name: 'Factory',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 0,
  isForSale: false,
  units: [_purchaseUnit],
  pendingConfiguration: null,
);

Future<void> _pumpBuildingDetail(WidgetTester tester, {required FakeBuildingDetailService service, required String buildingId}) async {
  await tester.binding.setSurfaceSize(const Size(900, 2200));
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
            buildingId: buildingId,
            buildingDetailService: service,
            tutorialService: FakeTutorialService(),
            buildingSalesService: FakeBuildingSalesService(),
            buildingSourcingService: FakeBuildingSourcingService(),
            buildingAnalyticsService: FakeBuildingAnalyticsService(),
          ),
        ),
      ),
      GoRoute(path: '/building/:id/sell', builder: (context, state) => Scaffold(body: Text('Sell ${state.pathParameters['id']}'))),
    ],
  );
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<RecentBuildingState>.value(value: RecentBuildingState(storage: InMemorySelectedBuildingStorage())),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BuildingDetailScreen grid editor', () {
    testWidgets('shows the starter layout banner for an empty FACTORY and applies it into edit mode', (tester) async {
      final service = FakeBuildingDetailService(building: _emptyFactory);
      await _pumpBuildingDetail(tester, service: service, buildingId: 'building-1');

      expect(find.text('🏭 New Factory — Ready to Set Up'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Apply Starter Layout'));
      await tester.pumpAndSettle();

      // Save/Cancel now live on the outer edit tabs' Layouts tab
      // (`BuildingEditingTabs`), not always-visible.
      await tester.tap(find.byKey(const ValueKey('building-tab-layouts')));
      await tester.pumpAndSettle();

      expect(find.text('Store Configuration'), findsOneWidget);
      expect(find.byKey(const ValueKey('draft-cell-unit-draft-starter-0-0')), findsOneWidget);
      expect(find.byKey(const ValueKey('draft-cell-unit-draft-starter-3-0')), findsOneWidget);
    });

    testWidgets('Edit Building enters edit mode, placing a unit on an empty cell then saving calls storeBuildingConfiguration', (tester) async {
      final service = FakeBuildingDetailService(building: _factoryWithUnit);
      await _pumpBuildingDetail(tester, service: service, buildingId: 'building-2');

      await tester.tap(find.widgetWithText(TextButton, 'Edit Building'));
      await tester.pumpAndSettle();

      // (0,0) is occupied by the seeded PURCHASE unit; place a new unit at (1,0).
      await tester.tap(find.byKey(const ValueKey('draft-cell-1-0')));
      await tester.pumpAndSettle();

      expect(find.text('Select unit type'), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey('picker-MANUFACTURING')));
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('draft-cell-1-0')), findsNothing); // now occupied, different key

      // On this narrow surface width the stacked area below the grid
      // always shows the outer edit tabs (per-unit panes only appear in
      // the tap-to-open sheet here) — switch to Layouts for Save.
      await tester.tap(find.byKey(const ValueKey('building-tab-layouts')));
      await tester.pumpAndSettle();
      expect(find.text('Store Configuration'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Store Configuration'));
      await tester.pumpAndSettle();

      expect(service.calls, contains('storeBuildingConfiguration'));
      expect(service.lastStoredUnits, isNotNull);
      expect(service.lastStoredUnits!.any((u) => u.unitType == 'MANUFACTURING' && u.gridX == 1 && u.gridY == 0), isTrue);
    });

    testWidgets('Cancel in edit mode discards the draft without saving', (tester) async {
      final service = FakeBuildingDetailService(building: _factoryWithUnit);
      await _pumpBuildingDetail(tester, service: service, buildingId: 'building-2');

      await tester.tap(find.widgetWithText(TextButton, 'Edit Building'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('draft-cell-1-0')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('picker-STORAGE')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('building-tab-layouts')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(service.calls, isNot(contains('storeBuildingConfiguration')));
      expect(find.text('Store Configuration'), findsNothing);
    });

    testWidgets('shows config warnings for an unlinked purchase unit', (tester) async {
      final service = FakeBuildingDetailService(building: _factoryWithUnit);
      await _pumpBuildingDetail(tester, service: service, buildingId: 'building-2');

      expect(find.text('⚠ Configuration Warnings'), findsOneWidget);
      expect(find.textContaining('is not linked to a consumer unit'), findsWidgets);
    });
  });
}
