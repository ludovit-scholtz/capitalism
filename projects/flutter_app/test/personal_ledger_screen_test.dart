import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/company/personal_ledger_models.dart';
import 'package:capitalism_app/features/company/personal_ledger_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_personal_ledger_service.dart';
import 'support/in_memory_token_storage.dart';

const _account = PersonAccount(
  displayName: 'Alice',
  personalCash: 10000,
  taxReserve: 500,
  availableCash: 9500,
  totalNetWealth: 50000,
  shareholdings: [PersonalShareholding(companyName: 'Acme Corp', shareCount: 100, ownershipRatio: 0.05, marketValue: 4200)],
  dividendPayments: [PersonalDividendPayment(companyName: 'Acme Corp', totalAmount: 120, gameYear: 2026)],
  stockTrades: [PersonalStockTrade(companyName: 'Acme Corp', direction: 'BUY', shareCount: 100, totalValue: 4000)],
);

Future<GoRouter> _pumpPersonalLedger(WidgetTester tester, {required FakePersonalLedgerService service, bool authenticated = true}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: PersonalLedgerScreen(personalLedgerService: service))),
      GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Text('Login Screen'))),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
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
  });
}
