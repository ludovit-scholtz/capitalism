import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/buildings/buy_building_models.dart';
import 'package:capitalism_app/features/cities/cities_models.dart';
import 'package:capitalism_app/features/city/city_tab_models.dart';
import 'package:capitalism_app/features/city/city_tab_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_city_tab_service.dart';
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
);

const _ownedLot = CityLot(
  id: 'lot-2',
  name: 'Taken Plot',
  district: 'Downtown',
  price: 30000,
  suitableTypes: ['FACTORY'],
  ownerCompanyId: 'someone',
  buildingId: 'building-42',
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
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
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
    testWidgets('shows basic economic stats', (tester) async {
      final service = FakeCityTabService(city: _city);

      await _pump(tester, (s) => CityEconomyScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Currency: USD'), findsOneWidget);
    });
  });

  group('CityBuildingsScreen', () {
    testWidgets('shows lots and toggling filters to available only', (tester) async {
      final service = FakeCityTabService(lots: [_availableLot, _ownedLot]);

      await _pump(tester, (s) => CityBuildingsScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Riverside Plot'), findsOneWidget);
      expect(find.text('Taken Plot'), findsOneWidget);

      await tester.tap(find.byType(Switch));
      await tester.pumpAndSettle();

      expect(find.text('Riverside Plot'), findsOneWidget);
      expect(find.text('Taken Plot'), findsNothing);
    });

    testWidgets('tapping an owned lot navigates to its building', (tester) async {
      final service = FakeCityTabService(lots: [_ownedLot]);

      await _pump(tester, (s) => CityBuildingsScreen(cityId: 'city-1', cityTabService: s), service: service);
      await tester.tap(find.byIcon(Icons.arrow_forward));
      await tester.pumpAndSettle();

      expect(find.text('Building building-42'), findsOneWidget);
    });
  });

  group('CityMarketScreen', () {
    testWidgets('shows local resources sorted by abundance', (tester) async {
      final service = FakeCityTabService(city: _city);

      await _pump(tester, (s) => CityMarketScreen(cityId: 'city-1', cityTabService: s), service: service);

      expect(find.text('Iron Ore'), findsOneWidget);
      expect(find.text('70%'), findsOneWidget);
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
