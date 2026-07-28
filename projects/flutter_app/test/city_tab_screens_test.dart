import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/core/theme/app_icons.dart';
import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/buildings/buy_building_models.dart';
import 'package:capitalism_app/features/cities/cities_models.dart';
import 'package:capitalism_app/features/city/city_economy_models.dart';
import 'package:capitalism_app/features/city/city_economy_service.dart';
import 'package:capitalism_app/features/city/city_market_models.dart';
import 'package:capitalism_app/features/city/city_tab_models.dart';
import 'package:capitalism_app/features/city/city_tab_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_building_panel_service.dart';
import 'support/fake_city_economy_service.dart';
import 'support/fake_city_market_service.dart';
import 'support/fake_city_tab_service.dart';
import 'support/fake_tile_provider.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _city = City(
  id: 'city-1',
  name: 'Metropolis',
  countryCode: 'US',
  population: 500000,
  currencyCode: 'USD',
  baseSalaryPerManhour: 10,
  resources: [CityResourceAbundance(abundance: 0.7, resourceName: 'Iron Ore', resourceSlug: 'iron-ore')],
);

const _availableLot = CityLot(
  id: 'lot-1',
  name: 'Riverside Plot',
  district: 'Riverside',
  price: 50000,
  suitableTypes: ['FACTORY'],
  ownerCompanyId: null,
  buildingId: null,
  latitude: 40.71,
  longitude: -74.00,
);

const _ownedLot = CityLot(
  id: 'lot-2',
  name: 'Taken Plot',
  district: 'Downtown',
  price: 30000,
  suitableTypes: ['FACTORY'],
  ownerCompanyId: 'someone',
  buildingId: 'building-42',
  latitude: 40.72,
  longitude: -74.01,
);

const _competitor = CityCompetitor(
  companyId: 'company-1',
  companyName: 'Acme Corp',
  isNpc: false,
  buildingCount: 3,
  estimatedRevenueLastTicks: 5000,
  marketSharePercent: 25.5,
  trend: 'UP',
  marketShareByCategory: [],
);

const _contract = GovernmentContractCard(
  id: 'contract-1',
  cityId: 'city-1',
  currencyCode: 'USD',
  title: 'Steel Supply',
  description: 'Supply steel beams.',
  productName: 'Steel Beams',
  quantityRequired: 100,
  minimumQuality: 0.5,
  budgetCap: 10000,
  deadlineTick: 500,
  status: 'OPEN',
  bidCount: 2,
);

Future<GoRouter> _pump(WidgetTester tester, Widget Function(FakeCityTabService) builder, {required FakeCityTabService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: builder(service))),
      GoRoute(
        path: '/building/:id',
        builder: (context, state) => Scaffold(body: Text('Building ${state.pathParameters['id']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('CityOverviewScreen', () {
    testWidgets('shows city stats and lot counts', (tester) async {
      final service = FakeCityTabService(city: _city, lots: [_availableLot, _ownedLot]);

      await _pump(tester, (s) => CityOverviewScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Metropolis'), findsOneWidget);
      expect(find.text('1'), findsOneWidget);
      expect(find.text('2'), findsOneWidget);
    });

    testWidgets('shows error state', (tester) async {
      final service = FakeCityTabService(cityError: Exception('down'));

      await _pump(tester, (s) => CityOverviewScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Could not load this city. Please try again.'), findsOneWidget);
    });
  });

  group('CityEconomyScreen', () {
    testWidgets('shows the economic cycle phase, intensity, active market events, and history', (tester) async {
      final service = FakeCityTabService(city: _city);
      final economyService = FakeCityEconomyService(
        economyData: const CityEconomyData(
          economicCycle: EconomicCycleView(phase: 'EXPANSION', intensityFactor: 1.2, ticksRemaining: 40),
          activeMarketEvents: [
            MarketEventView(id: 'event-1', title: 'Iron Ore Shortage', description: 'Supply disruption.', magnitudeMultiplier: 1.15),
          ],
          economicHistory: [
            EconomicCycleHistoryPoint(tick: 100, phase: 'EXPANSION', intensityFactor: 1.1),
            EconomicCycleHistoryPoint(tick: 124, phase: 'EXPANSION', intensityFactor: 1.2),
          ],
        ),
      );

      await _pump(
        tester,
        (s) => CityEconomyScreen(cityId: 'city-1', cityTabService: s, economyService: economyService),
        service: service,
      );

      expect(find.text('EXPANSION'), findsOneWidget);
      expect(find.text('1.20×'), findsOneWidget);
      expect(find.text('40 ticks remaining in this phase'), findsOneWidget);
      expect(find.text('Iron Ore Shortage'), findsOneWidget);
      expect(find.text('+15%'), findsOneWidget);
    });

    testWidgets('shows weather badges, forecast, and power grid balance', (tester) async {
      final service = FakeCityTabService(city: _city);
      final economyService = FakeCityEconomyService(
        weather: const CityWeatherForecast(
          currentWindPercent: 62,
          currentSolarPercent: 88,
          forecast: [WeatherTickPoint(tick: 1, windPercent: 60, solarPercent: 90)],
        ),
        powerBalance: const CityPowerBalance(
          totalSupplyMw: 120,
          totalDemandMw: 95,
          reserveMw: 25,
          reservePercent: 20.8,
          status: 'BALANCED',
          powerPlantCount: 2,
        ),
      );

      await _pump(
        tester,
        (s) => CityEconomyScreen(cityId: 'city-1', cityTabService: s, economyService: economyService),
        service: service,
      );

      expect(find.text('☀️ 88%'), findsOneWidget);
      expect(find.text('💨 62%'), findsOneWidget);
      expect(find.byKey(const Key('city-power-balance-card')), findsOneWidget);
      expect(find.text('BALANCED'), findsOneWidget);
      expect(find.text('Supply: 120.0 MW'), findsOneWidget);
      expect(find.text('Demand: 95.0 MW'), findsOneWidget);
    });

    testWidgets('shows the economic health index, metrics, and opens the details dialog', (tester) async {
      final service = FakeCityTabService(city: _city);
      final economyService = FakeCityEconomyService(
        economicReport: const CityEconomicReportResult(
          latest: CityEconomicReport(
            id: 'report-1',
            taxCycleEnd: 8760,
            economicIndex: 82,
            totalSalaries: 50000,
            totalPublicRevenue: 120000,
            activeCompanies: 12,
            averageProductQuality: 0.75,
            totalPowerSupply: 100,
            totalPowerConsumption: 80,
          ),
          history: [
            CityEconomicReport(
              id: 'report-0',
              taxCycleEnd: 8000,
              economicIndex: 70,
              totalSalaries: 40000,
              totalPublicRevenue: 100000,
              activeCompanies: 10,
              averageProductQuality: 0.7,
              totalPowerSupply: 90,
              totalPowerConsumption: 70,
            ),
            CityEconomicReport(
              id: 'report-1',
              taxCycleEnd: 8760,
              economicIndex: 82,
              totalSalaries: 50000,
              totalPublicRevenue: 120000,
              activeCompanies: 12,
              averageProductQuality: 0.75,
              totalPowerSupply: 100,
              totalPowerConsumption: 80,
            ),
          ],
        ),
      );

      await _pump(
        tester,
        (s) => CityEconomyScreen(cityId: 'city-1', cityTabService: s, economyService: economyService),
        service: service,
      );

      expect(find.text('82'), findsOneWidget);
      expect(find.text('Thriving'), findsOneWidget);
      expect(find.text('75%'), findsOneWidget);

      await tester.tap(find.text('View details'));
      await tester.pumpAndSettle();

      expect(find.text('City economic health'), findsOneWidget);
      expect(find.textContaining('Economic index: 82.0'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeCityTabService(city: _city);
      final economyService = FakeCityEconomyService(economyError: Exception('down'));

      await _pump(
        tester,
        (s) => CityEconomyScreen(cityId: 'city-1', cityTabService: s, economyService: economyService),
        service: service,
      );

      expect(find.text('Could not load the economy dashboard. Please try again.'), findsOneWidget);
    });
  });

  group('CityBuildingsScreen', () {
    testWidgets('shows lots and toggling filters to available only', (tester) async {
      final service = FakeCityTabService(lots: [_availableLot, _ownedLot]);

      await _pump(
        tester,
        (s) => CityBuildingsScreen(cityId: 'city-1', cityTabService: s, tileProvider: FakeTileProvider()),
        service: service,
      );

      expect(find.text('Riverside Plot'), findsOneWidget);
      expect(find.text('Taken Plot'), findsOneWidget);

      await tester.tap(find.byType(Switch));
      await tester.pumpAndSettle();

      expect(find.text('Riverside Plot'), findsOneWidget);
      expect(find.text('Taken Plot'), findsNothing);
    });

    testWidgets('tapping an owned lot navigates to its building', (tester) async {
      final service = FakeCityTabService(lots: [_ownedLot]);

      await _pump(
        tester,
        (s) => CityBuildingsScreen(cityId: 'city-1', cityTabService: s, tileProvider: FakeTileProvider()),
        service: service,
      );
      await tester.tap(find.byIcon(AppIcons.arrowRight.data));
      await tester.pumpAndSettle();

      expect(find.text('Building building-42'), findsOneWidget);
    });

    testWidgets('renders a map marker per lot and tapping one highlights it in the list', (tester) async {
      final service = FakeCityTabService(lots: [_availableLot, _ownedLot]);

      await _pump(
        tester,
        (s) => CityBuildingsScreen(cityId: 'city-1', cityTabService: s, tileProvider: FakeTileProvider()),
        service: service,
      );

      expect(find.byKey(const Key('map-marker-lot-1')), findsOneWidget);
      expect(find.byKey(const Key('map-marker-lot-2')), findsOneWidget);

      await tester.tap(find.byKey(const Key('map-marker-lot-2')));
      await tester.pumpAndSettle();

      final card = tester.widget<Card>(find.byKey(const Key('city-lot-lot-2')));
      expect(card.color, isNotNull);
    });
  });

  group('CityMarketScreen', () {
    testWidgets('shows local resources sorted by abundance', (tester) async {
      final service = FakeCityTabService(city: _city);

      await _pump(
        tester,
        (s) => CityMarketScreen(
          cityId: 'city-1',
          cityTabService: s,
          marketService: FakeCityMarketService(),
          buildingPanelService: FakeBuildingPanelService(),
        ),
        service: service,
      );

      expect(find.text('Iron Ore'), findsOneWidget);
      expect(find.text('70%'), findsOneWidget);
    });

    testWidgets('shows the top-selling-products demand panel', (tester) async {
      final service = FakeCityTabService(city: _city);
      final marketService = FakeCityMarketService(
        demandSummary: const CityDemandSummary(
          cityId: 'city-1',
          cityName: 'Metropolis',
          currencyCode: 'USD',
          products: [
            ProductDemandEntry(
              productTypeId: 'pt-1',
              productName: 'Steel Beams',
              industry: 'Manufacturing',
              totalDemand: 100,
              totalQuantitySold: 90,
              satisfactionRate: 0.9,
              averageClearingPrice: 42.5,
              sellerCount: 3,
            ),
            ProductDemandEntry(
              productTypeId: 'pt-2',
              productName: 'Canned Food',
              industry: 'Food',
              totalDemand: 200,
              totalQuantitySold: 40,
              satisfactionRate: 0.2,
              averageClearingPrice: 3.1,
              sellerCount: 1,
            ),
          ],
        ),
      );

      await _pump(
        tester,
        (s) => CityMarketScreen(
          cityId: 'city-1',
          cityTabService: s,
          marketService: marketService,
          buildingPanelService: FakeBuildingPanelService(),
        ),
        service: service,
      );

      expect(find.text('TOP-SELLING PRODUCTS'), findsOneWidget);
      expect(find.text('Steel Beams'), findsOneWidget);
      expect(find.text('90%'), findsOneWidget);
      expect(find.text('Canned Food'), findsOneWidget);
      expect(find.text('20%'), findsOneWidget);
      expect(find.text('Sellers: 3  ·  Sold: 90'), findsOneWidget);
    });

    testWidgets('shows an empty state when there is no demand data', (tester) async {
      final service = FakeCityTabService(city: _city);

      await _pump(
        tester,
        (s) => CityMarketScreen(
          cityId: 'city-1',
          cityTabService: s,
          marketService: FakeCityMarketService(),
          buildingPanelService: FakeBuildingPanelService(),
        ),
        service: service,
      );

      expect(find.text('No demand data yet for this city.'), findsOneWidget);
    });

    testWidgets('shows the city media-houses section with badges', (tester) async {
      final service = FakeCityTabService(city: _city);
      final buildingPanelService = FakeBuildingPanelService(
        cityMediaHouses: const [
          CityMediaHouse(
            id: 'mh-1',
            name: 'Channel One',
            mediaType: 'TV',
            ownerCompanyName: 'Acme Media',
            contentRanking: 82,
            isGovernmentOwned: false,
            effectivenessMultiplier: 1.4,
            powerStatus: 'ONLINE',
            isUnderConstruction: false,
          ),
          CityMediaHouse(
            id: 'mh-2',
            name: 'State Radio',
            mediaType: 'RADIO',
            ownerCompanyName: 'Government',
            contentRanking: 55,
            isGovernmentOwned: true,
            effectivenessMultiplier: 1.0,
            powerStatus: 'OFFLINE',
            isUnderConstruction: false,
          ),
        ],
      );

      await _pump(
        tester,
        (s) => CityMarketScreen(
          cityId: 'city-1',
          cityTabService: s,
          marketService: FakeCityMarketService(),
          buildingPanelService: buildingPanelService,
        ),
        service: service,
      );

      expect(find.text('Media Houses'), findsOneWidget);
      expect(find.text('Channel One'), findsOneWidget);
      expect(find.textContaining('×1.4'), findsOneWidget);
      expect(find.textContaining('82%'), findsOneWidget);
      expect(find.text('GOV'), findsOneWidget);
      expect(find.text('OFFLINE'), findsOneWidget);
    });

    testWidgets('shows an empty state when there are no media houses', (tester) async {
      final service = FakeCityTabService(city: _city);

      await _pump(
        tester,
        (s) => CityMarketScreen(
          cityId: 'city-1',
          cityTabService: s,
          marketService: FakeCityMarketService(),
          buildingPanelService: FakeBuildingPanelService(),
        ),
        service: service,
      );

      expect(find.text('No media houses have been built in this city yet.'), findsOneWidget);
    });

    testWidgets('still shows local resources when the demand and media-houses fetches fail', (tester) async {
      final service = FakeCityTabService(city: _city);

      await _pump(
        tester,
        (s) => CityMarketScreen(
          cityId: 'city-1',
          cityTabService: s,
          marketService: FakeCityMarketService(demandError: Exception('boom')),
          buildingPanelService: FakeBuildingPanelService(actionError: Exception('boom')),
        ),
        service: service,
      );

      expect(find.text('Iron Ore'), findsOneWidget);
      expect(find.text('No demand data yet for this city.'), findsOneWidget);
    });
  });

  group('CityContractsScreen', () {
    testWidgets('shows open contracts and submits a bid', (tester) async {
      final service = FakeCityTabService(
        contracts: [_contract],
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
        eligibility: const ContractEligibility(isEligible: true, reasonMessage: null),
      );

      await _pump(tester, (s) => CityContractsScreen(cityId: 'city-1', cityTabService: s), service: service);
      expect(find.text('Steel Supply'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Bid'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '15');
      await tester.tap(find.widgetWithText(FilledButton, 'Submit bid'));
      await tester.pumpAndSettle();

      expect(service.lastBidArgs?['contractId'], 'contract-1');
      expect(service.lastBidArgs?['companyId'], 'company-1');
    });

    testWidgets('shows error state', (tester) async {
      final service = FakeCityTabService(contractsError: Exception('down'));

      await _pump(tester, (s) => CityContractsScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Could not load contracts. Please try again.'), findsOneWidget);
    });
  });

  group('CityCompetitorsScreen', () {
    testWidgets('shows competitors with market share', (tester) async {
      final service = FakeCityTabService(competitors: [_competitor]);

      await _pump(tester, (s) => CityCompetitorsScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Acme Corp'), findsOneWidget);
      expect(find.textContaining('25.5%'), findsOneWidget);
    });

    testWidgets('shows error state', (tester) async {
      final service = FakeCityTabService(competitorsError: Exception('down'));

      await _pump(tester, (s) => CityCompetitorsScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Could not load competitors. Please try again.'), findsOneWidget);
    });
  });
}
