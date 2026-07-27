// Covers the responsive two-column Building Detail layout added to
// `building_detail_screen.dart`: at >=1024px (matching `app_shell.dart`'s
// `_wideScreenBreakpoint` and the web's `min-[1024px]` collapse point) the
// grid and its contextual sidebar sit side by side; below that they stack
// in one column, as they already did before this feature.

import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/recent_building_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
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
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _unit = BuildingUnitDetail(
  id: 'unit-1',
  unitType: 'MANUFACTURING',
  level: 1,
  resourceTypeId: null,
  productTypeId: null,
  minPrice: null,
  gridX: 0,
  gridY: 0,
);

const _building = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Main Factory',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 100,
  isForSale: false,
  units: [_unit],
  pendingConfiguration: null,
);

Future<void> _pumpAtWidth(WidgetTester tester, double width) async {
  final size = Size(width, 2200);
  await tester.binding.setSurfaceSize(size);
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
            buildingDetailService: FakeBuildingDetailService(building: _building),
            tutorialService: FakeTutorialService(),
            buildingSalesService: FakeBuildingSalesService(),
            buildingSourcingService: FakeBuildingSourcingService(),
            buildingAnalyticsService: FakeBuildingAnalyticsService(),
          ),
        ),
      ),
    ],
  );
  await tester.pumpWidget(
    MediaQuery(
      data: MediaQueryData(size: size),
      child: MultiProvider(
        providers: [
          ChangeNotifierProvider<AuthState>.value(value: auth),
          ChangeNotifierProvider<RecentBuildingState>.value(value: RecentBuildingState(storage: InMemorySelectedBuildingStorage())),
          ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
        ],
        child: MaterialApp.router(routerConfig: router),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BuildingDetailScreen responsive layout', () {
    testWidgets('renders the grid and sidebar side by side at >=1024px', (tester) async {
      await _pumpAtWidth(tester, 1200);

      final gridCellTop = tester.getTopLeft(find.byKey(const ValueKey('cell-unit-unit-1')));
      final sidebarTabTop = tester.getTopLeft(find.byKey(const ValueKey('building-tab-overview')));

      // Side by side: roughly the same row (small vertical delta), but at
      // very different horizontal positions (sidebar well to the right).
      expect((gridCellTop.dy - sidebarTabTop.dy).abs(), lessThan(100));
      expect(sidebarTabTop.dx, greaterThan(gridCellTop.dx + 200));
    });

    testWidgets('stacks the grid above the sidebar below 1024px', (tester) async {
      await _pumpAtWidth(tester, 800);

      final gridCellBottom = tester.getBottomLeft(find.byKey(const ValueKey('cell-unit-unit-1'))).dy;
      final sidebarTabTop = tester.getTopLeft(find.byKey(const ValueKey('building-tab-overview'))).dy;

      // Stacked: the sidebar's tab strip sits below the grid, not beside it.
      expect(sidebarTabTop, greaterThanOrEqualTo(gridCellBottom));
    });
  });
}
