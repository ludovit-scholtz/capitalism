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

const _npc = NpcCompanySummary(id: 'npc-1', name: 'Acme NPC', archetype: 'CONGLOMERATE', difficultyLevel: 2, homeCityName: 'Metropolis', isActive: true, buildingCount: 3);

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

    testWidgets('shows an impersonation banner and can stop impersonating', (tester) async {
      final service = FakeOperationsService(
        dashboard: _dashboard,
        isImpersonating: true,
        adminActorDisplayName: 'AdminUser',
        effectivePlayerDisplayName: 'Alice',
      );

      await _pump(tester, OperationsOverviewScreen(operationsService: service));

      expect(find.textContaining('Impersonating Alice'), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('stop-impersonating-button')));
      await tester.pumpAndSettle();

      expect(service.calls.contains('stopImpersonation'), isTrue);
    });

    testWidgets('shows NPC companies and can pause/resume them', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard, npcCompanies: const [_npc]);

      await _pump(tester, OperationsOverviewScreen(operationsService: service));

      expect(find.text('Acme NPC'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Pause'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Pause'));
      await tester.pumpAndSettle();

      expect(service.lastPausedNpcId, 'npc-1');
    });

    testWidgets('end shard requires confirmation before calling the mutation', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard);

      await _pump(tester, OperationsOverviewScreen(operationsService: service));

      await tester.tap(find.byKey(const ValueKey('end-shard-button')));
      await tester.pumpAndSettle();

      expect(service.calls.contains('endShardManually'), isFalse);

      await tester.enterText(find.byKey(const ValueKey('end-shard-reason-field')), 'Testing');
      await tester.tap(find.byKey(const ValueKey('end-shard-confirm-button')));
      await tester.pumpAndSettle();

      expect(service.lastEndShardReason, 'Testing');
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

    testWidgets('composing a new entry calls upsertGameNewsEntry with the entered title', (tester) async {
      final service = FakeOperationsService(newsFeed: const [_newsEntry]);

      await _pump(tester, OperationsNewsScreen(operationsService: service));

      await tester.tap(find.byKey(const ValueKey('new-news-entry-button')));
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const ValueKey('news-title-en')), 'Big Announcement');
      await tester.tap(find.byKey(const ValueKey('save-news-entry-button')));
      await tester.pumpAndSettle();

      expect(service.lastNewsEntry?['entryId'], isNull);
      final localizations = service.lastNewsEntry?['localizations'] as List<Map<String, String>>?;
      expect(localizations?.first['title'], 'Big Announcement');
    });

    testWidgets('tapping an existing entry pre-fills the editor for updating', (tester) async {
      final service = FakeOperationsService(newsFeed: const [_newsEntry]);

      await _pump(tester, OperationsNewsScreen(operationsService: service));

      await tester.tap(find.text('New Feature'));
      await tester.pumpAndSettle();

      expect(find.text('Edit news entry'), findsOneWidget);
      final titleField = tester.widget<TextField>(find.byKey(const ValueKey('news-title-en')));
      expect(titleField.controller?.text, 'New Feature');

      await tester.tap(find.byKey(const ValueKey('save-news-entry-button')));
      await tester.pumpAndSettle();

      expect(service.lastNewsEntry?['entryId'], 'news-1');
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

    testWidgets('hides root-administrator actions for a non-root admin', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard);

      await _pump(tester, OperationsPlayerDetailScreen(playerId: 'player-1', operationsService: service));

      expect(find.byKey(const ValueKey('impersonate-button')), findsOneWidget);
      expect(find.byKey(const ValueKey('invisible-in-chat-switch')), findsOneWidget);
      expect(find.byKey(const ValueKey('local-admin-switch')), findsNothing);
      expect(find.byKey(const ValueKey('grant-global-admin-button')), findsNothing);
    });

    testWidgets('impersonating a player applies the returned token', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard, impersonationToken: 'impersonation-token');

      await _pump(tester, OperationsPlayerDetailScreen(playerId: 'player-1', operationsService: service));

      await tester.tap(find.byKey(const ValueKey('impersonate-button')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Impersonate'));
      await tester.pumpAndSettle();

      expect(service.calls.contains('startImpersonation'), isTrue);
    });

    testWidgets('toggling chat visibility calls setPlayerInvisibleInChat', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard);

      await _pump(tester, OperationsPlayerDetailScreen(playerId: 'player-1', operationsService: service));

      await tester.tap(find.byKey(const ValueKey('invisible-in-chat-switch')));
      await tester.pumpAndSettle();

      expect(service.lastInvisibleInChatArgs, {'playerId': 'player-1', 'isInvisible': true});
    });

    testWidgets('root administrators see and can use admin-role actions', (tester) async {
      final service = FakeOperationsService(dashboard: _dashboard, isRootAdministrator: true);

      await _pump(tester, OperationsPlayerDetailScreen(playerId: 'player-1', operationsService: service));

      expect(find.byKey(const ValueKey('local-admin-switch')), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('local-admin-switch')));
      await tester.pumpAndSettle();
      expect(service.lastLocalAdminArgs, {'playerId': 'player-1', 'isAdmin': true});

      await tester.tap(find.byKey(const ValueKey('grant-global-admin-button')));
      await tester.pumpAndSettle();
      expect(service.lastGrantedGlobalAdminEmail, 'alice@example.com');

      await tester.tap(find.byKey(const ValueKey('revoke-global-admin-button')));
      await tester.pumpAndSettle();
      expect(service.lastRevokedGlobalAdminEmail, 'alice@example.com');
    });
  });
}
