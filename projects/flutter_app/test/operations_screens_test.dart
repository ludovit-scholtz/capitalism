import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/operations/operations_models.dart';
import 'package:capitalism_app/features/operations/operations_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/fake_operations_service.dart';
import 'support/in_memory_token_storage.dart';

const _player = GameAdminPlayer(
  id: 'player-1',
  displayName: 'Alice',
  email: 'alice@example.com',
  role: 'PLAYER',
  personalCash: 5000,
  totalCompanyCash: 20000,
  companyCount: 2,
  cityNames: ['Metropolis'],
  lastLoginAtUtc: '2026-07-01T00:00:00Z',
);

const _dashboard = GameAdminDashboard(
  moneySupply: 1000000,
  totalPersonalCash: 400000,
  totalCompanyCash: 600000,
  externalMoneyInflowLast100Ticks: 5000,
  totalShippingCostsLast100Ticks: 1000,
  players: [_player],
);

const _statistics = OperationsStatistics(
  totalInflow: 10000,
  totalOutflow: 8000,
  netFlow: 2000,
  totalPlayerCount: 10,
  totalCompanyCount: 15,
  inflowItems: [MoneyFlowItem(label: 'Sales', amount: 8000, percentage: 80)],
  outflowItems: [MoneyFlowItem(label: 'Taxes', amount: 2000, percentage: 20)],
);

const _productRow = ProductAnalyticsRow(productName: 'Steel Beams', industry: 'HEAVY_INDUSTRY', totalSold: 500, totalRevenue: 10000, avgSellingPrice: 20, activeSellerCount: 5);

const _newsEntry = AdminNewsEntry(
  id: 'news-1',
  entryType: 'NEWS',
  status: 'DRAFT',
  localizations: [AdminNewsLocalization(locale: 'en', title: 'New Feature', summary: null)],
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
  group('OperationsOverviewScreen', () {
    testWidgets('shows dashboard metrics for admins', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard);

      await _pump(tester, OperationsOverviewScreen(operationsService: service));

      expect(find.text('1000000'), findsOneWidget);
    });

    testWidgets('shows Administrators only for non-admins', (tester) async {
      final service = FakeOperationsService(canAccess: false);

      await _pump(tester, OperationsOverviewScreen(operationsService: service));

      expect(find.text('Administrators only.'), findsOneWidget);
      expect(service.calls.contains('fetchDashboard'), isFalse);
    });
  });

  group('OperationsMoneyFlowScreen', () {
    testWidgets('shows inflow/outflow items', (tester) async {
      final service = FakeOperationsService(statistics: _statistics);

      await _pump(tester, OperationsMoneyFlowScreen(operationsService: service));

      expect(find.text('Sales'), findsOneWidget);
      expect(find.text('Taxes'), findsOneWidget);
    });
  });

  group('OperationsProductAnalyticsScreen', () {
    testWidgets('shows product rows', (tester) async {
      final service = FakeOperationsService(productAnalytics: const [_productRow]);

      await _pump(tester, OperationsProductAnalyticsScreen(operationsService: service));

      expect(find.text('Steel Beams'), findsOneWidget);
    });
  });

  group('OperationsNewsScreen', () {
    testWidgets('shows news entries with status', (tester) async {
      final service = FakeOperationsService(newsFeed: const [_newsEntry]);

      await _pump(tester, OperationsNewsScreen(operationsService: service));

      expect(find.text('New Feature'), findsOneWidget);
      expect(find.text('DRAFT'), findsOneWidget);
    });
  });

  group('OperationsPlayersScreen', () {
    testWidgets('shows players and filters by search', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard);

      await _pump(tester, OperationsPlayersScreen(operationsService: service));
      expect(find.text('Alice'), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'zzz');
      await tester.pumpAndSettle();

      expect(find.text('Alice'), findsNothing);
    });
  });

  group('OperationsPlayerDetailScreen', () {
    testWidgets('shows the matching player detail', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard);

      await _pump(tester, OperationsPlayerDetailScreen(playerId: 'player-1', operationsService: service));

      expect(find.text('Alice'), findsOneWidget);
      expect(find.text('alice@example.com'), findsOneWidget);
    });

    testWidgets('shows not-found for an unknown player id', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard);

      await _pump(tester, OperationsPlayerDetailScreen(playerId: 'unknown', operationsService: service));

      expect(find.text('Player not found.'), findsOneWidget);
    });
  });
}
