import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/company/company_models.dart';
import 'package:capitalism_app/features/company/ledger_models.dart';
import 'package:capitalism_app/features/company/ledger_screen.dart';
import 'package:capitalism_app/features/company/ledger_service.dart';
import 'package:capitalism_app/features/trade/trade_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_ledger_service.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _ledger = CompanyLedger(
  companyName: 'Acme Corp',
  gameYear: 2026,
  currentCash: 50000,
  primaryCurrencyCode: 'EUR',
  totalRevenue: 100000,
  totalPurchasingCosts: 20000,
  totalShippingCosts: 5000,
  totalLaborCosts: 15000,
  totalEnergyCosts: 3000,
  totalMarketingCosts: 2000,
  totalTaxPaid: 10000,
  totalOtherCosts: 1000,
  netIncome: 44000,
  totalAssets: 200000,
  taxableIncome: 44000,
  estimatedIncomeTax: 8800,
  history: [
    CompanyLedgerHistoryYear(gameYear: 2026, isCurrentGameYear: true, netIncome: 44000, firstRecordedTick: 0, lastRecordedTick: 100),
    CompanyLedgerHistoryYear(gameYear: 2025, isCurrentGameYear: false, netIncome: 30000, firstRecordedTick: -100, lastRecordedTick: -1),
  ],
  buildingSummaries: [
    BuildingLedgerSummary(buildingId: 'building-1', buildingName: 'Steel Mill', buildingType: 'FACTORY', revenue: 60000, costs: 40000, currencyCode: 'EUR'),
  ],
);

const _cityBreakdown = CityFinancialBreakdown(
  cityId: 'city-1',
  cityName: 'Metropolis',
  currencyCode: 'EUR',
  revenue: 80000,
  costs: 40000,
  profit: 40000,
  revenueTrend: [CityRevenueTrendPoint(tick: 1, revenue: 100), CityRevenueTrendPoint(tick: 2, revenue: 200)],
);

const _shipment = TradeRoute(
  id: 'shipment-1',
  sourceBuildingName: 'Mine',
  sourceCityName: 'Metropolis',
  destinationBuildingName: 'Steel Mill',
  destinationCityName: 'Riverside',
  productTypeName: null,
  resourceTypeName: 'Iron Ore',
  quantity: 50,
  expectedArrivalTick: 120,
  status: 'IN_TRANSIT',
  failureReason: null,
  scheduledDepartureTick: 100,
  transitTicks: 20,
);

const _cityUnlock = CityUnlockStatus(
  cityId: 'city-2',
  cityName: 'Riverside',
  countryCode: 'US',
  isUnlocked: false,
  requiredNetWorth: 100000,
  currentNetWorth: 40000,
  currency: 'USD',
  progressPercent: 40,
  estimatedTicksToUnlock: 500,
);

const _pageData = LedgerPageData(
  ledger: _ledger,
  cityFinancialBreakdown: [_cityBreakdown],
  logisticsShipments: [_shipment],
  cityUnlockStatuses: [_cityUnlock],
  currentTick: 110,
);

Future<void> _pump(WidgetTester tester, Widget widget) async {
  await tester.binding.setSurfaceSize(const Size(900, 3000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(initialLocation: '/', routes: [GoRoute(path: '/', builder: (context, state) => widget)]);
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
}

void main() {
  group('LedgerScreen', () {
    testWidgets('shows KPI values, statements, and history year selector', (tester) async {
      final service = FakeLedgerService(page: _pageData);

      await _pump(tester, LedgerScreen(companyId: 'company-1', ledgerService: service));

      expect(find.text('Acme Corp'), findsOneWidget);
      expect(find.textContaining('Y2026'), findsOneWidget);
      expect(find.textContaining('Y2025'), findsOneWidget);
      expect(find.text('Net income'), findsWidgets);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeLedgerService(pageError: Exception('down'));

      await _pump(tester, LedgerScreen(companyId: 'company-1', ledgerService: service));

      expect(find.text('Could not load the ledger. Please try again.'), findsOneWidget);
    });

    testWidgets('selecting a history year re-fetches with that game year', (tester) async {
      final service = FakeLedgerService(page: _pageData);

      await _pump(tester, LedgerScreen(companyId: 'company-1', ledgerService: service));

      await tester.tap(find.byKey(const ValueKey('history-year-2025')));
      await tester.pumpAndSettle();

      expect(service.lastRequestedGameYear, 2025);
    });

    testWidgets('toggling a drill-down button loads and shows entries', (tester) async {
      final service = FakeLedgerService(
        page: _pageData,
        drillEntries: const [
          LedgerEntryResult(id: 'entry-1', category: 'REVENUE', description: 'Sale', amount: 500, recordedAtTick: 42, currencyCode: 'EUR'),
        ],
      );

      await _pump(tester, LedgerScreen(companyId: 'company-1', ledgerService: service));

      await tester.tap(find.byKey(const ValueKey('drill-REVENUE')));
      await tester.pumpAndSettle();

      expect(service.lastDrillCategory, 'REVENUE');
      expect(find.text('Sale'), findsOneWidget);
      expect(find.textContaining('Drill down: REVENUE'), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('drill-REVENUE')));
      await tester.pumpAndSettle();

      expect(find.textContaining('Drill down: REVENUE'), findsNothing);
    });

    testWidgets('shows the cross-city shipment, city breakdown, city unlock, and buildings panels', (tester) async {
      final service = FakeLedgerService(page: _pageData);

      await _pump(tester, LedgerScreen(companyId: 'company-1', ledgerService: service));

      expect(find.textContaining('CROSS-CITY SHIPMENTS'), findsOneWidget);
      expect(find.textContaining('Metropolis → Riverside'), findsOneWidget);
      expect(find.textContaining('FINANCIALS BY CITY'), findsOneWidget);
      expect(find.textContaining('CITY EXPANSION'), findsOneWidget);
      expect(find.text('Riverside'), findsWidgets);
      expect(find.textContaining('BUILDINGS PERFORMANCE'), findsOneWidget);
      expect(find.text('Steel Mill'), findsOneWidget);
    });
  });
}
