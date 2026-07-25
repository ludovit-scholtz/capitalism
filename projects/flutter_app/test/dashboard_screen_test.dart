import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/graphql/graphql_service.dart';
import 'package:capitalism_app/features/dashboard/dashboard_models.dart';
import 'package:capitalism_app/features/dashboard/dashboard_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_dashboard_service.dart';
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

Future<GoRouter> _pumpDashboard(
  WidgetTester tester, {
  required AuthState auth,
  required FakeDashboardService service,
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: DashboardScreen(dashboardService: service))),
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
    ],
  );

  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
  return router;
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

    testWidgets('shows the error state with Try Again on load failure', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(fetchDataError: Exception('network down'));

      await _pumpDashboard(tester, auth: auth, service: service);

      expect(find.text('Could not load your dashboard. Please try again.'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Try Again'), findsOneWidget);
    });

    testWidgets('shows companies, buildings, badges, cash and pending actions', (tester) async {
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

      expect(find.text('Acme Furnishings'), findsOneWidget);
      expect(find.textContaining('12345'), findsOneWidget);
      expect(find.text('Main Factory'), findsOneWidget);
      expect(find.text('City Bank'), findsOneWidget);
      expect(find.text('Loan default'), findsOneWidget);
      expect(find.text('OFFLINE'), findsOneWidget);
      expect(find.text('Destroyed'), findsNothing);
      expect(find.text('UPGRADE · Main Factory'), findsOneWidget);
      expect(find.text('3 ticks remaining'), findsOneWidget);
    });

    testWidgets('tapping a regular building navigates to /building/:id', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []));

      await _pumpDashboard(tester, auth: auth, service: service);

      await tester.tap(find.text('Main Factory'));
      await tester.pumpAndSettle();

      expect(find.text('Building Detail building-1'), findsOneWidget);
    });

    testWidgets('tapping a BANK building navigates to /bank/:id instead', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []));

      await _pumpDashboard(tester, auth: auth, service: service);

      await tester.tap(find.text('City Bank'));
      await tester.pumpAndSettle();

      expect(find.text('Bank Detail building-2'), findsOneWidget);
    });

    testWidgets('pull-to-refresh silently re-fetches dashboard data', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);
      expect(service.fetchDashboardDataCallCount, 1);

      await tester.fling(find.text('Dashboard'), const Offset(0, 300), 1000);
      await tester.pumpAndSettle();

      expect(service.fetchDashboardDataCallCount, 2);
      // Still showing the dashboard content, not a full-screen loading spinner.
      expect(find.text('Acme Furnishings'), findsOneWidget);
    });

    testWidgets('a destroyed building shows a remove action; a healthy one does not', (tester) async {
      final auth = AuthState(storage: InMemoryTokenStorage());
      await auth.setToken('test-token');
      final service = FakeDashboardService(
        data: const DashboardData(companies: [_company, _companyWithDestroyed], currentTick: 1, taxRate: 15, pendingActions: []),
      );

      await _pumpDashboard(tester, auth: auth, service: service);

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

      await tester.tap(find.byTooltip('Remove from dashboard'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Remove'));
      await tester.pumpAndSettle();

      expect(find.text('Building is still under review.'), findsOneWidget);
      expect(find.text('Old Warehouse'), findsOneWidget);
    });
  });
}
