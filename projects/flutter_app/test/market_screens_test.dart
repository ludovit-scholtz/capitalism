import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/market/market_models.dart';
import 'package:capitalism_app/features/market/market_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_market_service.dart';
import 'support/in_memory_token_storage.dart';

const _seller = MarketIntelSeller(rank: 1, displayName: 'Acme Corp', askingPricePerUnit: 12.5, marketShare: 0.4);
const _intelProduct = MarketIntelProduct(productName: 'Steel Beams', totalWeeklySalesVolume: 500, sellers: [_seller]);
const _intel = MarketIntelligence(cityName: 'Metropolis', products: [_intelProduct]);

const _overviewProduct = MarketOverviewProduct(
  productTypeId: 'product-1',
  productName: 'Steel Beams',
  totalDemand: 1000,
  totalQuantitySold: 800,
  satisfactionRate: 0.8,
  averageClearingPrice: 15,
  sellerCount: 3,
);
const _overview = MarketOverview(cityId: 'city-1', cityName: 'Metropolis', products: [_overviewProduct]);

const _energyListing = EnergyListing(
  listingId: 'listing-1',
  buildingId: 'building-1',
  buildingName: 'Solar Plant',
  companyId: 'company-1',
  companyName: 'Acme Corp',
  cityId: 'city-1',
  plantType: 'SOLAR',
  pricePerKwhLocal: 0.12,
  capacityKw: 500,
  availableKw: 300,
);

const _event = GlobalEvent(
  id: 'event-1',
  eventType: 'RECESSION',
  severity: 'HIGH',
  title: 'Economic Recession',
  description: 'Operating costs are elevated.',
  isActive: true,
  operatingCostMultiplier: 1.2,
  tradeRouteMultiplier: null,
);

const _campaignRow = CampaignRow(
  buildingName: 'Downtown Shop',
  productName: 'Steel Beams',
  cityName: 'Metropolis',
  brandAwareness: 0.6,
  brandQuality: 0.7,
  revenueLastTicks: 5000,
  recommendation: null,
);
const _campaignAnalytics = CampaignAnalytics(
  totalRevenue: 20000,
  totalMarketingSpend: 3000,
  bestPerformingCity: 'Metropolis',
  bestPerformingProduct: 'Steel Beams',
  globalRecommendation: 'Increase marketing spend.',
  rows: [_campaignRow],
);

Future<void> _pump(WidgetTester tester, Widget widget) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp(home: Scaffold(body: widget))),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MarketIntelligenceScreen', () {
    testWidgets('shows sellers for the first city by default', (tester) async {
      final service = FakeMarketService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        intelligenceByCity: const {'city-1': _intel},
      );

      await _pump(tester, MarketIntelligenceScreen(marketService: service));

      expect(find.text('Steel Beams'), findsOneWidget);
      expect(find.textContaining('Acme Corp'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeMarketService(loadError: Exception('down'));

      await _pump(tester, MarketIntelligenceScreen(marketService: service));

      expect(find.text('Could not load market intelligence. Please try again.'), findsOneWidget);
    });
  });

  group('MarketDashboardScreen', () {
    testWidgets('shows products and loads competitor data on tap', (tester) async {
      final service = FakeMarketService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        overviewByCity: const {'city-1': _overview},
        competitors: const [CompetitorQuality(companyName: 'Acme Corp', qualityLevel: 0.8, pricePremiumPct: 5, isOwnCompany: true)],
      );

      await _pump(tester, MarketDashboardScreen(marketService: service));
      expect(find.text('Steel Beams'), findsOneWidget);

      await tester.tap(find.text('Steel Beams'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Acme Corp'), findsOneWidget);
    });
  });

  group('EnergyMarketScreen', () {
    testWidgets('shows listings across cities', (tester) async {
      final service = FakeMarketService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        energyListingsByCity: const {'city-1': [_energyListing]},
      );

      await _pump(tester, EnergyMarketScreen(marketService: service));

      expect(find.text('Solar Plant (SOLAR)'), findsOneWidget);
    });

    testWidgets('own listing shows a Cancel action', (tester) async {
      final service = FakeMarketService(
        cities: const [
          {'id': 'city-1', 'name': 'Metropolis'},
        ],
        energyListingsByCity: const {'city-1': [_energyListing]},
        myPowerPlants: const [
          {'id': 'building-1', 'name': 'Solar Plant', 'cityId': 'city-1'},
        ],
      );

      await _pump(tester, EnergyMarketScreen(marketService: service));
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(service.cancelledListingId, 'listing-1');
    });
  });

  group('GlobalEventsScreen', () {
    testWidgets('shows active events by default and switches to history', (tester) async {
      final service = FakeMarketService(activeEvents: const [_event], eventHistory: const []);

      await _pump(tester, GlobalEventsScreen(marketService: service));
      expect(find.text('Economic Recession'), findsOneWidget);

      await tester.tap(find.widgetWithText(ChoiceChip, 'History'));
      await tester.pumpAndSettle();

      expect(find.text('No past events.'), findsOneWidget);
    });
  });

  group('MarketingAnalyticsScreen', () {
    testWidgets('shows campaign summary and rows', (tester) async {
      final service = FakeMarketService(
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
        campaignAnalytics: _campaignAnalytics,
      );

      await _pump(tester, MarketingAnalyticsScreen(marketService: service));

      expect(find.text('Increase marketing spend.'), findsOneWidget);
      expect(find.textContaining('Steel Beams · Downtown Shop'), findsOneWidget);
    });
  });
}
