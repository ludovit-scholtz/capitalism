import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_unit_view_tabs.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_building_analytics_service.dart';
import 'support/fake_building_detail_service.dart';
import 'support/fake_building_sales_service.dart';
import 'support/fake_building_sourcing_service.dart';

const _manufacturingUnit = BuildingUnitDetail(
  id: 'unit-1',
  unitType: 'MANUFACTURING',
  level: 1,
  resourceTypeId: null,
  productTypeId: null,
  minPrice: null,
  gridX: 0,
  gridY: 0,
);

const _publicSalesUnit = BuildingUnitDetail(
  id: 'unit-2',
  unitType: 'PUBLIC_SALES',
  level: 1,
  resourceTypeId: null,
  productTypeId: null,
  minPrice: 10,
  gridX: 1,
  gridY: 0,
);

const _storageUnit = BuildingUnitDetail(
  id: 'unit-3',
  unitType: 'STORAGE',
  level: 1,
  resourceTypeId: null,
  productTypeId: null,
  minPrice: null,
  gridX: 2,
  gridY: 0,
);

Future<void> _pump(WidgetTester tester, BuildingUnitDetail unit) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: BuildingUnitViewTabs(
          unit: unit,
          buildingId: 'building-1',
          cityId: 'city-1',
          itemNameFor: (u) => '',
          unitResourceHistories: const [],
          service: FakeBuildingDetailService(),
          salesService: FakeBuildingSalesService(),
          sourcingService: FakeBuildingSourcingService(),
          analyticsService: FakeBuildingAnalyticsService(),
          onUpdatePrice: (_) {},
          isPriceUpdating: false,
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BuildingUnitViewTabs', () {
    testWidgets('shows Quick Actions and Market Intelligence only for PUBLIC_SALES', (tester) async {
      await _pump(tester, _publicSalesUnit);

      expect(find.byKey(const ValueKey('building-tab-basicInfo')), findsOneWidget);
      expect(find.byKey(const ValueKey('building-tab-quickActions')), findsOneWidget);
      expect(find.byKey(const ValueKey('building-tab-inventory')), findsOneWidget);
      expect(find.byKey(const ValueKey('building-tab-history')), findsOneWidget);
      expect(find.byKey(const ValueKey('building-tab-marketIntelligence')), findsOneWidget);
    });

    testWidgets('hides Quick Actions and Market Intelligence for a STORAGE unit', (tester) async {
      await _pump(tester, _storageUnit);

      expect(find.byKey(const ValueKey('building-tab-basicInfo')), findsOneWidget);
      expect(find.byKey(const ValueKey('building-tab-quickActions')), findsNothing);
      expect(find.byKey(const ValueKey('building-tab-inventory')), findsOneWidget);
      expect(find.byKey(const ValueKey('building-tab-history')), findsOneWidget);
      expect(find.byKey(const ValueKey('building-tab-marketIntelligence')), findsNothing);
    });

    testWidgets('shows Market Intelligence but not Quick Actions for MANUFACTURING', (tester) async {
      await _pump(tester, _manufacturingUnit);

      expect(find.byKey(const ValueKey('building-tab-quickActions')), findsNothing);
      expect(find.byKey(const ValueKey('building-tab-marketIntelligence')), findsOneWidget);
    });

    testWidgets('no upgrade panel is shown in view mode', (tester) async {
      // Matches web: the upgrade preview/stage/confirm flow lives only in
      // edit mode's Maintenance tab, never in the read-only unit view.
      await _pump(tester, _publicSalesUnit);

      expect(find.textContaining('Stage Upgrade'), findsNothing);
      expect(find.textContaining('Max Level'), findsNothing);
    });
  });
}
