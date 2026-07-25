import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/recent_building_state.dart';
import 'package:capitalism_app/features/buildings/building_detail_models.dart';
import 'package:capitalism_app/features/buildings/building_detail_screen.dart';
import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_building_analytics_service.dart';
import 'support/fake_building_detail_service.dart';
import 'support/fake_building_panel_service.dart';
import 'support/fake_building_sales_service.dart';
import 'support/fake_building_sourcing_service.dart';
import 'support/fake_tutorial_service.dart';
import 'support/in_memory_selected_building_storage.dart';
import 'support/in_memory_token_storage.dart';

const _apartment = BuildingDetail(
  id: 'building-1',
  companyId: 'company-1',
  name: 'Riverside Apartments',
  type: 'APARTMENT',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 72,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
  pricePerSqm: 8.5,
  totalAreaSqm: 2000,
);

const _mediaHouse = BuildingDetail(
  id: 'building-2',
  companyId: 'company-1',
  name: 'Downtown Newspaper',
  type: 'MEDIA_HOUSE',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: null,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
  mediaType: 'NEWSPAPER',
  contentBudgetPerTick: 0,
);

const _researchBuilding = BuildingDetail(
  id: 'building-3',
  companyId: 'company-1',
  name: 'Innovation Lab',
  type: 'RESEARCH_DEVELOPMENT',
  level: 1,
  powerStatus: 'POWERED',
  occupancyPercent: 0,
  isForSale: false,
  units: [],
  pendingConfiguration: null,
);

Future<void> _pumpBuildingDetail(
  WidgetTester tester, {
  required FakeBuildingDetailService service,
  required FakeBuildingPanelService panelService,
  required String buildingId,
}) async {
  await tester.binding.setSurfaceSize(const Size(900, 2400));
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
            buildingPanelService: panelService,
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
  group('BuildingDetailScreen building-type panels', () {
    testWidgets('APARTMENT renders the property panel and schedules a rent change', (tester) async {
      final service = FakeBuildingDetailService(building: _apartment);
      final panelService = FakeBuildingPanelService(
        apartmentDetail: const ApartmentBuildingDetail(
          buildingId: 'building-1',
          occupancyPercent: 72,
          totalAreaSqm: 2000,
          pricePerSqm: 8.5,
          pendingPricePerSqm: null,
          pendingPriceActivationTick: null,
          cityAverageRentPerSqm: 9,
          adjustedMarketRentPerSqm: 9,
          populationIndex: 1,
          currencyCode: 'EUR',
          revenueHistory: [],
        ),
      );
      await _pumpBuildingDetail(tester, service: service, panelService: panelService, buildingId: 'building-1');

      expect(find.text('Property Management'), findsOneWidget);
      expect(panelService.calls, contains('fetchApartmentBuildingDetail'));

      await tester.tap(find.widgetWithText(FilledButton, 'Set Rent'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '10');
      await tester.tap(find.widgetWithText(FilledButton, 'Schedule Change'));
      await tester.pumpAndSettle();

      expect(panelService.lastRentPerSqm, 10);
    });

    testWidgets('MEDIA_HOUSE renders the media house panel and saves the content budget', (tester) async {
      final service = FakeBuildingDetailService(building: _mediaHouse, ownedCompanyNames: {'company-1': 'Acme Media Co'});
      final panelService = FakeBuildingPanelService();
      await _pumpBuildingDetail(tester, service: service, panelService: panelService, buildingId: 'building-2');

      expect(find.text('📡 Media House Management'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextField, 'Content spend per tick'), '400');
      await tester.tap(find.widgetWithText(FilledButton, 'Save Budget'));
      await tester.pumpAndSettle();

      expect(panelService.lastContentBudget, 400);
    });

    testWidgets('RESEARCH_DEVELOPMENT shows the research panel above the grid', (tester) async {
      final service = FakeBuildingDetailService(building: _researchBuilding);
      final panelService = FakeBuildingPanelService(
        companyBrands: const [
          CompanyBrand(
            id: 'brand-1',
            name: 'Acme',
            scope: 'COMPANY',
            awareness: 0.1,
            quality: 0,
            marketingQuality: 0,
            marketingEfficiencyMultiplier: 1,
          ),
        ],
      );
      await _pumpBuildingDetail(tester, service: service, panelService: panelService, buildingId: 'building-3');

      expect(find.text('🔬 Research Progress'), findsOneWidget);
      expect(find.text('Units', skipOffstage: false), findsOneWidget); // grid section still renders below
    });
  });
}
