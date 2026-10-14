import 'dart:async';

import 'package:capitalism_app/core/widgets/skeleton.dart';
import 'package:capitalism_app/core/widgets/stat_tile.dart';
import 'package:capitalism_app/core/widgets/state_view.dart';
import 'package:capitalism_app/features/dashboard/quick_action_tile.dart';
import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/game_state/game_state_model.dart';
import 'package:capitalism_app/core/game_state/game_state_state.dart';
import 'package:capitalism_app/core/graphql/graphql_service.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/core/services/url_opener.dart';
import 'package:capitalism_app/features/buildings/building_analytics_models.dart';
import 'package:capitalism_app/features/buildings/building_panel_models.dart';
import 'package:capitalism_app/features/company/company_models.dart';
import 'package:capitalism_app/features/dashboard/dashboard_models.dart';
import 'package:capitalism_app/features/dashboard/dashboard_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_dashboard_service.dart';
import 'support/fake_url_opener.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _destroyedBuilding = DashboardBuilding(
  id: 'building-3',
  name: 'Old Warehouse',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  destroyedAtUtc: '2026-01-01T00:00:00Z',
  hasDefaultedCollateralLoan: false,
  unitCount: 0,
);

const _companyWithDestroyed = DashboardCompany(
  id: 'company-2',
  name: 'Rust Belt Holdings',
  cash: 500,
  buildings: [_destroyedBuilding],
);

const _company = DashboardCompany(
  id: 'company-1',
  name: 'Acme Furnishings',
  cash: 12345,
  buildings: [
    DashboardBuilding(
      id: 'building-1',
      name: 'Main Factory',
      type: 'FACTORY',
      level: 2,
      powerStatus: 'POWERED',
      destroyedAtUtc: null,
      hasDefaultedCollateralLoan: false,
      unitCount: 4,
    ),
    DashboardBuilding(
      id: 'building-2',
      name: 'City Bank',
      type: 'BANK',
      level: 1,
      powerStatus: 'OFFLINE',
      destroyedAtUtc: null,
      hasDefaultedCollateralLoan: true,
      unitCount: 0,
    ),
  ],
);

const _buildingWithUnits = DashboardBuilding(
  id: 'building-10',
  name: 'Unit Factory',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  destroyedAtUtc: null,
  hasDefaultedCollateralLoan: false,
  unitCount: 2,
  cityId: 'city-1',
  units: [
    DashboardUnit(id: 'unit-1', unitType: 'PURCHASE', gridX: 0, gridY: 0),
    DashboardUnit(id: 'unit-2', unitType: 'MANUFACTURING', gridX: 1, gridY: 0),
  ],
);

const _companyWithUnits =
    DashboardCompany(id: 'company-9', name: 'Unit Co', cash: 1000, buildings: [_buildingWithUnits]);

const _buildingSameCityA = DashboardBuilding(
  id: 'building-a',
  name: 'Plant A',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  destroyedAtUtc: null,
  hasDefaultedCollateralLoan: false,
  unitCount: 0,
  cityId: 'city-5',
);

const _buildingSameCityB = DashboardBuilding(
  id: 'building-b',
  name: 'Plant B',
  type: 'FACTORY',
  level: 1,
  powerStatus: 'POWERED',
  destroyedAtUtc: null,
  hasDefaultedCollateralLoan: false,
  unitCount: 0,
  cityId: 'city-5',
);

const _companyTwoBuildingsSameCity = DashboardCompany(
  id: 'company-8',
  name: 'Dual Plant Co',
  cash: 2000,
  buildings: [_buildingSameCityA, _buildingSameCityB],
);

Future<GoRouter> _pumpDashboard(
  WidgetTester tester, {
  required AuthState auth,
  required FakeDashboardService service,
  GameStateState? gameStateState,
  UrlOpener? urlOpener,
  bool settle = true,
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) =>
            Scaffold(body: DashboardScreen(dashboardService: service, urlOpener: urlOpener ?? FakeUrlOpener())),
      ),
      GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Center(child: Text('Login Screen')))),
      GoRoute(
        path: '/onboarding',
        builder: (context, state) => const Scaffold(body: Center(child: Text('Onboarding Screen'))),
      ),
      GoRoute(
        path: '/building/:id',
        builder: (context, state) =>
            Scaffold(body: Center(child: Text('Building Detail ${state.pathParameters['id']}'))),
      ),
      GoRoute(
        path: '/bank/:id',
        builder: (context, state) => Scaffold(body: Center(child: Text('Bank Detail ${state.pathParameters['id']}'))),
      ),
      GoRoute(
        path: '/buy-building/:companyId',
        builder: (context, state) =>
            Scaffold(body: Center(child: Text('Buy Building ${state.pathParameters['companyId']}'))),
      ),
    ],
  );

  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<GameStateState>.value(value: gameStateState ?? GameStateState()),
        ChangeNotifierProvider<LocaleState>.value(value: LocaleState(storage: InMemorySelectedLocaleStorage())),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  if (settle) await tester.pumpAndSettle();
  return router;
}

/// Taps a tab by its label and settles the resulting `TabBarView` animation.
Future<void> _selectTab(WidgetTester tester, String label) async {
  await tester.tap(find.text(label));
  await tester.pumpAndSettle();
}

void main() {
  group('DashboardScreen', () {
    testWidgets('redirects to /login when not authenticated, without calling the service', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      final service = FakeDashboardService();

      await _pumpDashboard(tester, auth: auth, service: service);

      expect(find.text('Login Screen'), findsOneWidget);
      expect(service.calls, isEmpty);
    });

    testWidgets('redirects to /onboarding when onboarding is not completed', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(onboardingCompleted: false);

      await _pumpDashboard(tester, auth: auth, service: service);

      expect(find.text('Onboarding Screen'), findsOneWidget);
      expect(service.calls, ['fetchOnboardingCompleted']);
      expect(service.fetchDashboardDataCallCount, 0);
    });

    testWidgets('shows the empty state with a link to onboarding when there are no companies', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [], currentTick: 42, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);

      expect(find.text('You do not have a company yet.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Start Onboarding'));
      await tester.pumpAndSettle();
      expect(find.text('Onboarding Screen'), findsOneWidget);
    });

    testWidgets('settled company cash keeps its cents', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(
          companies: [DashboardCompany(id: 'company-c', name: 'Cents Co', cash: 12.5, buildings: [])],
          currentTick: 1,
          taxRate: 15,
          pendingActions: [],
        ),
      );
      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');
      expect(find.text(r'$12.50'), findsOneWidget);
    });

    testWidgets('shows skeleton placeholders (not a spinner) while loading', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service, settle: false);

      expect(find.byType(SkeletonBox), findsWidgets);
      expect(find.byType(CircularProgressIndicator), findsNothing);
      await tester.pump(const Duration(milliseconds: 50));
    });

    testWidgets('Buildings tab shows icon quick actions that navigate', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      for (final label in ['Buy Building', 'Ledger', 'Contracts', 'Research', 'Settings']) {
        expect(find.widgetWithText(QuickActionTile, label), findsOneWidget, reason: label);
      }
      final handle = tester.ensureSemantics();
      expect(
        tester.getSemantics(
            find.descendant(of: find.widgetWithText(QuickActionTile, 'Ledger'), matching: find.text('Ledger'))),
        matchesSemantics(label: 'Ledger', isButton: true, hasTapAction: true, hasFocusAction: true, isFocusable: true),
      );
      handle.dispose();
      await tester.tap(find.widgetWithText(QuickActionTile, 'Buy Building'));
      await tester.pumpAndSettle();
      expect(find.text('Buy Building company-1'), findsOneWidget);
    });

    testWidgets('shows the error state with Try Again on load failure', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(fetchDataError: Exception('network down'));

      await _pumpDashboard(tester, auth: auth, service: service);

      expect(find.text('Could not load your dashboard. Please try again.'), findsOneWidget);
      expect(find.text('Try Again'), findsOneWidget);
      expect(find.byType(StateView), findsOneWidget);
    });

    testWidgets('renders all 5 tabs with Overview active by default', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);

      expect(find.text('Overview'), findsOneWidget);
      expect(find.text('Buildings'), findsOneWidget);
      expect(find.text('Activity'), findsOneWidget);
      expect(find.text('Chat'), findsOneWidget);
      expect(find.text('Pro'), findsOneWidget);
      expect(find.text('Financial summary'), findsOneWidget); // Overview content visible by default
    });

    testWidgets('Overview tab shows a financial summary card per company', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
        ledgersByCompanyId: const {
          'company-1': CompanyLedger(
            companyName: 'Acme Furnishings',
            gameYear: 5,
            currentCash: 12345,
            primaryCurrencyCode: 'USD',
            totalRevenue: 10000,
            totalPurchasingCosts: 2000,
            totalShippingCosts: 0,
            totalLaborCosts: 1000,
            totalEnergyCosts: 500,
            totalMarketingCosts: 0,
            totalTaxPaid: 500,
            totalOtherCosts: 0,
            netIncome: 6000,
            totalAssets: 50000,
          ),
        },
      );

      await _pumpDashboard(tester, auth: auth, service: service);

      final overviewCard = find.byKey(const Key('overview-ledger-company-1'));
      expect(overviewCard, findsOneWidget);
      expect(find.descendant(of: overviewCard, matching: find.text('Acme Furnishings')), findsOneWidget);
      expect(find.descendant(of: overviewCard, matching: find.textContaining('10,000')), findsOneWidget);
      expect(find.descendant(of: overviewCard, matching: find.textContaining('6,000')), findsOneWidget);
      expect(find.text('Next steps'), findsOneWidget);
    });

    testWidgets('Overview loads every company ledger in parallel and shows each card as soon as it arrives', (
      tester,
    ) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(
          companies: [_company, _companyWithUnits],
          currentTick: 1,
          taxRate: 15,
          pendingActions: [],
        ),
        ledgersByCompanyId: const {
          'company-9': CompanyLedger(
            companyName: 'Unit Co',
            gameYear: 5,
            currentCash: 1000,
            primaryCurrencyCode: 'USD',
            totalRevenue: 7777,
            totalPurchasingCosts: 0,
            totalShippingCosts: 0,
            totalLaborCosts: 0,
            totalEnergyCosts: 0,
            totalMarketingCosts: 0,
            totalTaxPaid: 0,
            totalOtherCosts: 0,
            netIncome: 7000,
            totalAssets: 1000,
          ),
        },
      );
      service.ledgerGates['company-1'] = Completer<void>(); // company-1 stays slow
      service.ledgerGates['company-9'] = Completer<void>();

      await _pumpDashboard(tester, auth: auth, service: service, settle: false);
      await tester.pump(const Duration(milliseconds: 50));

      // Both requests are in flight at once (not one after the other).
      expect(service.fetchCompanyOverviewLedgerCallCount, 2);

      service.ledgerGates['company-9']!.complete();
      await tester.pump(const Duration(milliseconds: 50));

      // The finished company shows its numbers while the slow one still shows a skeleton.
      final fast = find.byKey(const Key('overview-ledger-company-9'));
      final slow = find.byKey(const Key('overview-ledger-company-1'));
      expect(find.descendant(of: fast, matching: find.textContaining('7,777')), findsOneWidget);
      expect(find.descendant(of: slow, matching: find.byType(SkeletonBox)), findsWidgets);
      expect(find.descendant(of: slow, matching: find.text('No financial data yet.')), findsNothing);

      service.ledgerGates['company-1']!.complete();
      await tester.pumpAndSettle();
    });

    testWidgets('an older slow ledger response never overwrites a newer one', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      CompanyLedger ledger(double revenue) => CompanyLedger(
        companyName: 'Acme Furnishings',
        gameYear: 5,
        currentCash: 1,
        primaryCurrencyCode: 'USD',
        totalRevenue: revenue,
        totalPurchasingCosts: 0,
        totalShippingCosts: 0,
        totalLaborCosts: 0,
        totalEnergyCosts: 0,
        totalMarketingCosts: 0,
        totalTaxPaid: 0,
        totalOtherCosts: 0,
        netIncome: 1,
        totalAssets: 1,
      );
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );
      service.ledgerQueues['company-1'] = [ledger(1111), ledger(2222)];
      final slowFirstCall = Completer<void>();
      service.ledgerGates['company-1'] = slowFirstCall;

      await _pumpDashboard(tester, auth: auth, service: service, settle: false);
      await tester.pump(const Duration(milliseconds: 50));
      expect(service.fetchCompanyOverviewLedgerCallCount, 1);

      // The second request (pull-to-refresh) is not gated and finishes first.
      service.ledgerGates.remove('company-1');
      await tester.fling(find.text('Financial summary'), const Offset(0, 300), 1000);
      // Skeletons animate forever, so settle with bounded pumps.
      for (var i = 0; i < 30; i++) {
        await tester.pump(const Duration(milliseconds: 100));
      }
      expect(service.fetchCompanyOverviewLedgerCallCount, 2);
      final card = find.byKey(const Key('overview-ledger-company-1'));
      expect(find.descendant(of: card, matching: find.textContaining('2,222')), findsOneWidget);

      // Now the stale first response arrives; it must be ignored.
      slowFirstCall.complete();
      await tester.pumpAndSettle();
      expect(find.descendant(of: card, matching: find.textContaining('2,222')), findsOneWidget);
      expect(find.descendant(of: card, matching: find.textContaining('1,111')), findsNothing);
    });

    testWidgets('a ledger that fails to load shows an error with Retry instead of an endless spinner', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
        ledgersByCompanyId: const {
          'company-1': CompanyLedger(
            companyName: 'Acme Furnishings',
            gameYear: 5,
            currentCash: 12345,
            primaryCurrencyCode: 'USD',
            totalRevenue: 4242,
            totalPurchasingCosts: 0,
            totalShippingCosts: 0,
            totalLaborCosts: 0,
            totalEnergyCosts: 0,
            totalMarketingCosts: 0,
            totalTaxPaid: 0,
            totalOtherCosts: 0,
            netIncome: 4000,
            totalAssets: 1,
          ),
        },
      );
      service.ledgerErrors['company-1'] = Exception('timed out');

      await _pumpDashboard(tester, auth: auth, service: service);

      final card = find.byKey(const Key('overview-ledger-company-1'));
      expect(find.descendant(of: card, matching: find.text('Could not load financial data.')), findsOneWidget);
      expect(find.descendant(of: card, matching: find.text('No financial data yet.')), findsNothing);

      service.ledgerErrors.clear();
      await tester.tap(find.descendant(of: card, matching: find.text('Retry')));
      await tester.pumpAndSettle();

      expect(find.descendant(of: card, matching: find.textContaining('4,242')), findsOneWidget);
      expect(find.text('Could not load financial data.'), findsNothing);
    });

    testWidgets('Overview tab shows the Launch New Company checklist with the CTA disabled when unmet', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
        newCompanyPrerequisites: const AdditionalCompanyPrerequisites(
          companyCount: 1,
          underMaxCap: true,
          hasExistingCompany: true,
          companyAgeRequirementMet: false,
          ticksUntilAgeRequirementMet: 500,
          profitabilityRequirementMet: true,
          balanceRequirementMet: false,
          allRequirementsMet: false,
        ),
      );

      await _pumpDashboard(tester, auth: auth, service: service);

      expect(find.text('Launch a new company'), findsOneWidget);
      expect(find.text('Oldest company is at least 1 game year old'), findsOneWidget);
      final launchButton = tester.widget<FilledButton>(
        find.ancestor(of: find.text('Launch New Company'), matching: find.byType(FilledButton)),
      );
      expect(launchButton.onPressed, isNull);
    });

    testWidgets(
      'Overview tab enables Launch New Company once eligible, and the wizard launches into /buy-building/:id',
      (tester) async {
        final auth = AuthState(storage: InMemoryTokenStorage());
        await auth.setToken('test-token');
        final service = FakeDashboardService(
          data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
          newCompanyPrerequisites: const AdditionalCompanyPrerequisites(
            companyCount: 1,
            underMaxCap: true,
            hasExistingCompany: true,
            companyAgeRequirementMet: true,
            ticksUntilAgeRequirementMet: 0,
            profitabilityRequirementMet: true,
            balanceRequirementMet: true,
            allRequirementsMet: true,
          ),
          newCompanyCities: const [NewCompanyCity(id: 'city-9', name: 'New Frontier', currencyCode: 'EUR')],
          startAdditionalCompanyResult: const NewCompanyResult(id: 'company-77', name: 'Second Co'),
        );

        await _pumpDashboard(tester, auth: auth, service: service);

        final launchButtonFinder = find.widgetWithText(FilledButton, 'Launch New Company');
        await tester.ensureVisible(launchButtonFinder);
        await tester.pumpAndSettle();
        await tester.tap(launchButtonFinder);
        await tester.pumpAndSettle();

        expect(find.text('Company details'), findsOneWidget);
        await tester.enterText(find.byType(TextField), 'Second Co');
        await tester.tap(find.widgetWithText(FilledButton, 'Next'));
        await tester.pumpAndSettle();

        expect(find.text('Choose IPO plan'), findsOneWidget);
        await tester.tap(find.widgetWithText(FilledButton, 'Launch'));
        await tester.pumpAndSettle();

        expect(service.lastStartAdditionalCompanyArgs?['companyName'], 'Second Co');
        expect(service.lastStartAdditionalCompanyArgs?['cityId'], 'city-9');
        expect(find.text('Buy Building company-77'), findsOneWidget);
      },
    );

    testWidgets('Buildings tab shows companies, buildings, badges, and cash', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 500, taxRate: 12.5, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      final companyCard = find.byKey(const Key('company-card-company-1'));
      expect(find.descendant(of: companyCard, matching: find.text('Acme Furnishings')), findsOneWidget);
      expect(find.descendant(of: companyCard, matching: find.text(r'$12,345')), findsOneWidget);
      expect(find.descendant(of: companyCard, matching: find.byType(StatTile)), findsWidgets);
      expect(find.text('Main Factory'), findsOneWidget);
      expect(find.text('City Bank'), findsOneWidget);
      expect(find.text('Loan default'), findsOneWidget);
      expect(find.text('OFFLINE'), findsOneWidget);
      expect(find.text('Destroyed'), findsNothing);
    });

    testWidgets('Activity tab shows pending actions', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(
          companies: [_company],
          currentTick: 500,
          taxRate: 12.5,
          pendingActions: [
            ScheduledAction(id: 'action-1', actionType: 'UPGRADE', buildingName: 'Main Factory', ticksRemaining: 3),
          ],
        ),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Activity');

      expect(find.text('UPGRADE · Main Factory'), findsOneWidget);
      expect(find.text('3 ticks remaining'), findsOneWidget);
    });

    testWidgets('tapping a regular building navigates to /building/:id', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
          data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []));

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      await tester.tap(find.text('Main Factory'));
      await tester.pumpAndSettle();

      expect(find.text('Building Detail building-1'), findsOneWidget);
    });

    testWidgets('tapping a BANK building navigates to /bank/:id instead', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
          data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []));

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      await tester.tap(find.text('City Bank'));
      await tester.pumpAndSettle();

      expect(find.text('Bank Detail building-2'), findsOneWidget);
    });

    testWidgets('Buildings tab shows the per-building financial strip and supply-chain strip', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_companyWithUnits], currentTick: 1, taxRate: 15, pendingActions: []),
        financialsByBuildingId: const {
          'building-10': BuildingFinancialTimeline(
            dataFromTick: 1,
            dataToTick: 100,
            totalSales: 5000,
            totalCosts: 2000,
            totalProfit: 3000,
            timeline: [],
          ),
        },
        unitStatusesByBuildingId: const {
          'building-10': [
            BuildingUnitOperationalStatus(
                buildingUnitId: 'unit-1', status: 'ACTIVE', blockedCode: null, blockedReason: null, idleTicks: 0),
            BuildingUnitOperationalStatus(
              buildingUnitId: 'unit-2',
              status: 'BLOCKED',
              blockedCode: 'NO_INPUTS',
              blockedReason: 'Waiting on inputs',
              idleTicks: 25,
            ),
          ],
        },
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');
      await tester.pumpAndSettle();

      expect(find.text(r'$5K'), findsOneWidget);
      expect(find.text(r'$3K'), findsOneWidget);
      expect(find.byKey(const Key('supply-chain-unit-unit-1')), findsOneWidget);
      expect(find.byKey(const Key('supply-chain-unit-unit-2')), findsOneWidget);
      // idleTicks=25 on unit-2 pushes health to RED.
      final badge = tester.widget<Text>(
        find.descendant(of: find.byKey(const Key('supply-chain-health-badge')), matching: find.byType(Text)),
      );
      expect(badge.data, 'RED');
    });

    testWidgets('Buildings tab shows one power-balance chip per distinct city, deduped across buildings',
        (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(
            companies: [_companyTwoBuildingsSameCity], currentTick: 1, taxRate: 15, pendingActions: []),
        powerBalanceByCityId: const {
          'city-5': CityPowerBalance(
              totalSupplyMw: 100, totalDemandMw: 80, reserveMw: 20, reservePercent: 25, status: 'BALANCED'),
        },
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('power-balance-city-5')), findsOneWidget);
      expect(service.fetchCityPowerBalanceCallCount, 1);
    });

    testWidgets('a city power-balance fetch failure does not crash the Buildings tab', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(
            companies: [_companyTwoBuildingsSameCity], currentTick: 1, taxRate: 15, pendingActions: []),
        cityPowerBalanceError: Exception('city power service down'),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');
      await tester.pumpAndSettle();

      expect(find.text('Plant A'), findsOneWidget);
      expect(find.text('Plant B'), findsOneWidget);
      expect(find.byKey(const Key('power-balance-city-5')), findsNothing);
    });

    testWidgets('Chat tab shows the placeholder content', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Chat');

      expect(find.text('Not implemented yet. Mirrors the chat side panel in AppHeader.vue.'), findsOneWidget);
    });

    testWidgets('Pro tab shows Inactive when there is no subscription', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Pro');

      expect(find.text('Inactive'), findsOneWidget);
      expect(find.text('You do not have an active Pro subscription.'), findsOneWidget);
      expect(find.text('What you unlock with Pro'), findsOneWidget);
      expect(find.text('Products'), findsOneWidget);
    });

    testWidgets('Pro tab shows Active with the expiry date when the subscription is in the future', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final futureDate = DateTime.now().toUtc().add(const Duration(days: 30));
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
        proSubscriptionEndsAtUtc: futureDate.toIso8601String(),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Pro');

      expect(find.text('Active'), findsOneWidget);
      expect(find.textContaining('Pro is active on your account until'), findsOneWidget);
    });

    testWidgets('Pro tab treats a lapsed subscription as Inactive', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final pastDate = DateTime.now().toUtc().subtract(const Duration(days: 1));
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
        proSubscriptionEndsAtUtc: pastDate.toIso8601String(),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Pro');

      expect(find.text('Inactive'), findsOneWidget);
    });

    testWidgets('Pro tab Open Portal button opens the master web URL', (tester) async {
      // Taller surface so the Pro tab's benefit cards + Open Portal button
      // (well below the default 600px test viewport) are actually mounted
      // into the element tree rather than lazily culled by the sliver.
      await tester.binding.setSurfaceSize(const Size(800, 1400));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );
      final urlOpener = FakeUrlOpener();

      await _pumpDashboard(tester, auth: auth, service: service, urlOpener: urlOpener);
      await _selectTab(tester, 'Pro');

      final openPortalFinder = find.widgetWithText(OutlinedButton, 'Open Portal');
      await tester.ensureVisible(openPortalFinder);
      await tester.pumpAndSettle();
      await tester.tap(openPortalFinder);
      await tester.pumpAndSettle();

      expect(urlOpener.openedUrls, hasLength(1));
      expect(urlOpener.openedUrls.single, isNotEmpty);
    });

    testWidgets('pull-to-refresh on the Overview tab silently re-fetches dashboard data', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      expect(service.fetchDashboardDataCallCount, 1);

      await tester.fling(find.text('Financial summary'), const Offset(0, 300), 1000);
      await tester.pumpAndSettle();

      expect(service.fetchDashboardDataCallCount, 2);
      // Still showing dashboard content, not a full-screen loading spinner.
      expect(find.text('Financial summary'), findsOneWidget);
    });

    testWidgets('a server tick change triggers a silent refresh without a full-screen spinner', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );
      final gameStateState = GameStateState();

      await _pumpDashboard(tester, auth: auth, service: service, gameStateState: gameStateState);
      expect(service.fetchDashboardDataCallCount, 1);

      gameStateState.gameState =
          const GameStateModel(currentTick: 2, lastTickAtUtc: null, tickIntervalSeconds: 10, taxRate: 15);
      gameStateState.notifyListeners();
      await tester.pump();
      expect(find.byType(CircularProgressIndicator), findsNothing);
      await tester.pumpAndSettle();

      expect(service.fetchDashboardDataCallCount, 2);
      expect(service.fetchCompanyOverviewLedgerCallCount, greaterThanOrEqualTo(2));
    });

    testWidgets('a destroyed building shows a remove action; a healthy one does not', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(
            companies: [_company, _companyWithDestroyed], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      await tester.scrollUntilVisible(
        find.text('Old Warehouse'),
        300,
        scrollable: find.byWidgetPredicate((w) => w is Scrollable && w.axis == Axis.vertical).first,
      );
      expect(find.text('Old Warehouse'), findsOneWidget);
      expect(find.text('Destroyed'), findsOneWidget);
      expect(find.byTooltip('Remove from dashboard'), findsOneWidget);
    });

    testWidgets('confirming removal calls the service and drops the tile from the list', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_companyWithDestroyed], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      await tester.tap(find.byTooltip('Remove from dashboard'));
      await tester.pumpAndSettle();
      expect(find.text('Remove destroyed building?'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Remove'));
      await tester.pumpAndSettle();

      expect(service.calls, contains('removeDestroyedBuilding'));
      expect(service.lastRemovedBuildingId, 'building-3');
      expect(find.text('Old Warehouse'), findsNothing);
    });

    testWidgets('cancelling the confirm dialog does not call the service or remove the tile', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_companyWithDestroyed], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      await tester.tap(find.byTooltip('Remove from dashboard'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(service.calls, isNot(contains('removeDestroyedBuilding')));
      expect(find.text('Old Warehouse'), findsOneWidget);
    });

    testWidgets('a server error while removing shows a SnackBar and keeps the tile', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_companyWithDestroyed], currentTick: 1, taxRate: 15, pendingActions: []),
        removeDestroyedBuildingError: GraphQlException('Building is still under review.', 'CANNOT_REMOVE'),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      await _selectTab(tester, 'Buildings');

      await tester.tap(find.byTooltip('Remove from dashboard'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Remove'));
      await tester.pumpAndSettle();

      expect(find.text('Building is still under review.'), findsOneWidget);
      expect(find.text('Old Warehouse'), findsOneWidget);
    });
  });
}
