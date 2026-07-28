import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/i18n/locale_state.dart';
import 'package:capitalism_app/features/company/personal_ledger_models.dart';
import 'package:capitalism_app/features/company/personal_ledger_screen.dart';
import 'package:capitalism_app/features/leaderboard/leaderboard_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_leaderboard_service.dart';
import 'support/fake_personal_ledger_service.dart';
import 'support/in_memory_selected_locale_storage.dart';
import 'support/in_memory_token_storage.dart';

const _account = PersonAccount(
  displayName: 'Alice',
  personalCash: 10000,
  taxReserve: 500,
  availableCash: 9500,
  totalNetWealth: 50000,
  shareholdings: [PersonalShareholding(companyName: 'Acme Corp', shareCount: 100, ownershipRatio: 0.05, marketValue: 4200)],
  dividendPayments: [
    PersonalDividendPayment(id: 'div-1', companyName: 'Acme Corp', totalAmount: 120, gameYear: 2026, recordedAtTick: 200),
  ],
  stockTrades: [PersonalStockTrade(companyName: 'Acme Corp', direction: 'BUY', shareCount: 100, totalValue: 4000)],
  interestPayments: [
    PersonalInterestPayment(id: 'int-1', companyName: 'Acme Corp', amount: 15, recordedAtTick: 210, recordedAtUtc: '2026-01-01T00:00:00Z', currencyCode: 'EUR', bankBuildingName: 'First Bank'),
  ],
);

const _endgame = EndgameStatus(
  winningThresholdUsd: 100000,
  topRealWorldRichest: [RealWorldWealth(id: 'r1', rank: 1, name: 'Richest Person', wealthUsd: 200000000000)],
);

Future<GoRouter> _pumpPersonalLedger(
  WidgetTester tester, {
  required FakePersonalLedgerService service,
  FakeLeaderboardService? leaderboardService,
  bool authenticated = true,
  bool settle = true,
}) async {
  await tester.binding.setSurfaceSize(const Size(900, 3000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => Scaffold(
          body: PersonalLedgerScreen(personalLedgerService: service, leaderboardService: leaderboardService ?? FakeLeaderboardService()),
        ),
      ),
      GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Text('Login Screen'))),
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
  if (settle) {
    await tester.pumpAndSettle();
  } else {
    // A milestone toast is a SnackBar with its own auto-dismiss timer;
    // pumpAndSettle would run simulated time forward until it dismisses
    // itself again. Pump just enough frames to load data and show it.
    await tester.pump();
    await tester.pump();
    await tester.pump();
  }
  return router;
}

void main() {
  group('PersonalLedgerScreen', () {
    testWidgets('shows a sign-in prompt when unauthenticated, without calling the service', (tester) async {
      final service = FakePersonalLedgerService();

      await _pumpPersonalLedger(tester, service: service, authenticated: false);

      expect(find.text('Sign in to view your personal ledger.'), findsOneWidget);
      expect(service.calls, isEmpty);
    });

    testWidgets('shows wealth summary, shareholdings, dividends, and trades', (tester) async {
      final service = FakePersonalLedgerService(account: _account);

      await _pumpPersonalLedger(tester, service: service);

      expect(find.text('50000'), findsOneWidget);
      expect(find.text('Acme Corp'), findsWidgets);
      expect(find.text('+120.00'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakePersonalLedgerService(fetchError: Exception('down'));

      await _pumpPersonalLedger(tester, service: service);

      expect(find.text('Could not load your personal ledger. Please try again.'), findsOneWidget);
    });

    testWidgets('shows the Race to the Top card with progress and the real-world leaderboard', (tester) async {
      final service = FakePersonalLedgerService(account: _account);
      final leaderboardService = FakeLeaderboardService(endgame: _endgame);

      await _pumpPersonalLedger(tester, service: service, leaderboardService: leaderboardService);

      expect(find.text('Race to the Top'), findsOneWidget);
      expect(find.text('50%'), findsOneWidget);
      expect(find.text('Richest Person'), findsOneWidget);
    });

    testWidgets('shows a milestone toast when net worth crosses a threshold', (tester) async {
      final service = FakePersonalLedgerService(account: _account);
      final leaderboardService = FakeLeaderboardService(endgame: _endgame);

      await _pumpPersonalLedger(tester, service: service, leaderboardService: leaderboardService, settle: false);

      // Multiple milestones (1/10/25/50%) are crossed at once; ScaffoldMessenger
      // shows them one at a time, so only the first-queued toast is visible yet.
      expect(find.textContaining("You've reached 1% of the winning threshold"), findsOneWidget);
    });

    testWidgets('passive income panel lists dividends and interest, filterable by type', (tester) async {
      final service = FakePersonalLedgerService(account: _account);

      await _pumpPersonalLedger(tester, service: service);

      expect(find.textContaining('Acme Corp · First Bank'), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('income-filter-interest')));
      await tester.pumpAndSettle();

      expect(find.textContaining('Acme Corp · First Bank'), findsOneWidget);
      expect(find.text('Tick #200'), findsNothing);
    });
  });
}
