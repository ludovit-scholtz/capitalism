import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/cities/cities_models.dart';
import 'package:capitalism_app/features/cities/cities_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_cities_service.dart';
import 'support/in_memory_token_storage.dart';

const _bigCity = City(
  id: 'city-1',
  name: 'Metropolis',
  countryCode: 'US',
  population: 5200000,
  currencyCode: 'USD',
  baseSalaryPerManhour: 12.5,
  resources: [
    CityResourceAbundance(abundance: 0.8, resourceName: 'Iron Ore', resourceSlug: 'iron-ore'),
    CityResourceAbundance(abundance: 0.3, resourceName: 'Wood', resourceSlug: 'wood'),
  ],
);

const _smallCity = City(
  id: 'city-2',
  name: 'Smallville',
  countryCode: 'SK',
  population: 15000,
  currencyCode: 'EUR',
  baseSalaryPerManhour: 8.0,
  resources: [],
);

Future<GoRouter> _pumpCities(WidgetTester tester, {required FakeCitiesService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: CitiesScreen(citiesService: service))),
      GoRoute(
        path: '/city/:cityId',
        builder: (context, state) => Scaffold(body: Text('City ${state.pathParameters['cityId']}')),
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
  group('CitiesScreen', () {
    testWidgets('shows cities sorted by population with top resources', (tester) async {
      final service = FakeCitiesService(cities: [_bigCity, _smallCity]);

      await _pumpCities(tester, service: service);

      expect(find.text('Metropolis'), findsOneWidget);
      expect(find.text('Smallville'), findsOneWidget);
      expect(find.text('⛓️ 80%'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeCitiesService(citiesError: Exception('down'));

      await _pumpCities(tester, service: service);

      expect(find.text('Could not load cities. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping View city map navigates to /city/:id', (tester) async {
      final service = FakeCitiesService(cities: [_bigCity]);

      await _pumpCities(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, '🗺️ View city map'));
      await tester.pumpAndSettle();

      expect(find.text('City city-1'), findsOneWidget);
    });
  });

  group('WorldMapScreen', () {
    const unlockedCity = ExpansionCity(
      id: 'city-1',
      name: 'Metropolis',
      countryCode: 'US',
      currencyCode: 'USD',
      population: 5200000,
      isUnlocked: true,
      availableLandPlots: 10,
      activeCompanyCount: 3,
      topResourceName: 'Iron Ore',
      requiredNetWorth: 0,
      currentNetWorth: 0,
      progressPercent: 100,
    );

    const lockedCity = ExpansionCity(
      id: 'city-2',
      name: 'Newtown',
      countryCode: 'DE',
      currencyCode: 'EUR',
      population: 800000,
      isUnlocked: false,
      availableLandPlots: 4,
      activeCompanyCount: 1,
      topResourceName: 'Coal',
      requiredNetWorth: 100000,
      currentNetWorth: 40000,
      progressPercent: 40,
    );

    Future<GoRouter> pumpWorldMap(WidgetTester tester, {required FakeCitiesService service, bool authenticated = false}) async {
      await tester.binding.setSurfaceSize(const Size(800, 2400));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final auth = AuthState(storage: InMemoryTokenStorage());
      if (authenticated) await auth.setToken('test-token');
      final router = GoRouter(
        initialLocation: '/',
        routes: [
          GoRoute(path: '/', builder: (context, state) => Scaffold(body: WorldMapScreen(citiesService: service))),
          GoRoute(
            path: '/city/:cityId',
            builder: (context, state) => Scaffold(body: Text('City ${state.pathParameters['cityId']}')),
          ),
          GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Text('Login Screen'))),
          GoRoute(path: '/dashboard', builder: (context, state) => const Scaffold(body: Text('Dashboard Screen'))),
          GoRoute(path: '/ledger/:companyId', builder: (context, state) => Scaffold(body: Text('Ledger ${state.pathParameters['companyId']}'))),
        ],
      );
      await tester.pumpWidget(
        ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
      );
      await tester.pumpAndSettle();
      return router;
    }

    testWidgets('selects the first city by default and shows its detail', (tester) async {
      final service = FakeCitiesService(expansionCities: [unlockedCity, lockedCity]);

      await pumpWorldMap(tester, service: service);

      expect(find.text('Population: 5.2M'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Go to city'), findsOneWidget);
    });

    testWidgets('tapping a locked city shows the unlock progress and Start expanding button', (tester) async {
      final service = FakeCitiesService(expansionCities: [unlockedCity, lockedCity]);

      await pumpWorldMap(tester, service: service);
      await tester.tap(find.text('Newtown'));
      await tester.pumpAndSettle();

      expect(find.text('40% toward unlocking'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Start expanding'), findsOneWidget);
    });

    testWidgets('Go to city navigates to /city/:id', (tester) async {
      final service = FakeCitiesService(expansionCities: [unlockedCity]);

      await pumpWorldMap(tester, service: service);
      await tester.tap(find.widgetWithText(FilledButton, 'Go to city'));
      await tester.pumpAndSettle();

      expect(find.text('City city-1'), findsOneWidget);
    });

    testWidgets('Start expanding redirects unauthenticated users to /login', (tester) async {
      final service = FakeCitiesService(expansionCities: [lockedCity]);

      await pumpWorldMap(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Start expanding'));
      await tester.pumpAndSettle();

      expect(find.text('Login Screen'), findsOneWidget);
    });

    testWidgets('Start expanding navigates authenticated users to their ledger', (tester) async {
      final service = FakeCitiesService(expansionCities: [lockedCity], firstCompanyId: 'company-1');

      await pumpWorldMap(tester, service: service, authenticated: true);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Start expanding'));
      await tester.pumpAndSettle();

      expect(find.text('Ledger company-1'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeCitiesService(expansionError: Exception('down'));

      await pumpWorldMap(tester, service: service);

      expect(find.text('Could not load the world map. Please try again.'), findsOneWidget);
    });
  });
}
