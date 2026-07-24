import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/theme/app_icons.dart';
import 'package:capitalism_app/features/banking/bank_statement_screen.dart';
import 'package:capitalism_app/features/banking/banking_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/banking_fixtures.dart';
import 'support/fake_banking_service.dart';
import 'support/in_memory_token_storage.dart';

const _account = PlayerBankAccount(
  id: 'account-1',
  accountNumber: '001',
  currencyCode: 'EUR',
  balance: 5000,
  companyName: 'Acme Corp',
  bankBuildingId: 'bank-1',
  isDepositAccount: false,
);

Future<void> _pump(WidgetTester tester, Widget widget, {bool authenticated = true}) async {
  // Tall enough that the pagination controls stay within ListView's build
  // cache extent even on the 50-row-per-page test fixture below.
  await tester.binding.setSurfaceSize(const Size(800, 6000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: widget)),
      GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Text('Login Screen'))),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BankStatementScreen', () {
    testWidgets('redirects unauthenticated users to /login', (tester) async {
      final service = FakeBankingService();

      await _pump(tester, BankStatementScreen(bankingService: service), authenticated: false);

      expect(find.text('Login Screen'), findsOneWidget);
    });

    testWidgets('shows the statement rows and balance', (tester) async {
      final service = FakeBankingService(bankStatement: statementFixture);

      await _pump(tester, BankStatementScreen(bankingService: service));

      expect(find.textContaining('5000.00 EUR'), findsOneWidget);
      expect(find.text('Salary payment'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeBankingService(loadError: Exception('down'));

      await _pump(tester, BankStatementScreen(bankingService: service));

      expect(find.text('Could not load your bank statement. Please try again.'), findsOneWidget);
    });

    testWidgets('selecting an account reloads the statement with the accountId filter', (tester) async {
      final service = FakeBankingService(bankStatement: statementFixture, myBankAccounts: const [_account]);

      await _pump(tester, BankStatementScreen(bankingService: service));
      await tester.tap(find.byKey(const Key('statement-account-selector')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Acme Corp · EUR').last);
      await tester.pumpAndSettle();

      expect(service.lastBankStatementArgs?['accountId'], 'account-1');
    });

    testWidgets('changing the page size reloads the statement with the new limit', (tester) async {
      final service = FakeBankingService(bankStatement: statementFixture);

      await _pump(tester, BankStatementScreen(bankingService: service));
      await tester.tap(find.byKey(const Key('statement-page-size')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('100').last);
      await tester.pumpAndSettle();

      expect(service.lastBankStatementArgs?['limit'], 100);
    });

    testWidgets('applying a tick range filter reloads with fromTick/toTick and resets to page 1', (tester) async {
      final service = FakeBankingService(bankStatement: statementFixture);

      await _pump(tester, BankStatementScreen(bankingService: service));
      await tester.enterText(find.widgetWithText(TextField, 'From tick'), '100');
      await tester.enterText(find.widgetWithText(TextField, 'To tick'), '500');
      await tester.tap(find.widgetWithText(OutlinedButton, 'Apply filters'));
      await tester.pumpAndSettle();

      expect(service.lastBankStatementArgs?['fromTick'], 100);
      expect(service.lastBankStatementArgs?['toTick'], 500);
      expect(service.lastBankStatementArgs?['offset'], 0);
    });

    testWidgets('next page button advances the offset when a full page is returned', (tester) async {
      final fullPage = BankStatementResult(
        companyName: 'Acme Corp',
        currencyCode: 'EUR',
        currentBalance: 5000,
        totalEntries: 120,
        rows: List.generate(50, (i) => BankStatementRow(id: 'row-$i', description: 'Entry $i', category: null, amount: -10, runningBalance: 4800)),
      );
      final service = FakeBankingService(bankStatement: fullPage);

      await _pump(tester, BankStatementScreen(bankingService: service));
      await tester.tap(find.byKey(const Key('statement-page-size')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('50').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byIcon(AppIcons.chevronRight.data));
      await tester.pumpAndSettle();

      expect(service.lastBankStatementArgs?['offset'], 50);
    });
  });
}
