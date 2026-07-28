import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/banking/banking_models.dart';
import 'package:capitalism_app/features/banking/loan_marketplace_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/banking_fixtures.dart';
import 'support/fake_banking_service.dart';
import 'support/in_memory_token_storage.dart';

const _pendingBank = BankListing(
  bankBuildingId: 'bank-2',
  bankBuildingName: 'Second Trust',
  cityName: 'Riverside',
  depositInterestRatePercent: 1.0,
  lendingInterestRatePercent: 9.0,
  availableLendingCapacity: 5000,
  baseCapitalDeposited: false,
  lenderCompanyId: 'other-owner',
);

const _myAccount = PlayerBankAccount(
  id: 'account-1',
  accountNumber: '001',
  currencyCode: 'EUR',
  balance: 500,
  companyId: 'company-1',
  companyName: 'My Company',
  ownerType: 'COMPANY',
  bankBuildingId: 'bank-1',
  isDepositAccount: true,
);

Future<void> _pump(WidgetTester tester, Widget widget) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: widget)),
      GoRoute(
        path: '/bank/:buildingId/request-loan',
        builder: (context, state) => Scaffold(body: Text('Request Loan ${state.pathParameters['buildingId']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('LoanMarketplaceScreen', () {
    testWidgets('shows banks and my loans on Borrow tab', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture], myLoans: const [loanFixture]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));

      expect(find.text('First National (Metropolis)'), findsOneWidget);
      expect(find.textContaining('8000 EUR'), findsOneWidget);
    });

    testWidgets('Borrow tab only shows banks with base capital deposited', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture, _pendingBank]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));

      expect(find.text('First National (Metropolis)'), findsOneWidget);
      expect(find.text('Second Trust (Riverside)'), findsNothing);
    });

    testWidgets('Borrow button navigates to the loan request screen', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));
      await tester.tap(find.widgetWithText(FilledButton, 'Borrow'));
      await tester.pumpAndSettle();

      expect(find.text('Request Loan bank-1'), findsOneWidget);
    });

    testWidgets('repaying an overdue loan calls repayLoanDebt', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture], myLoans: const [overdueLoanFixture]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));
      expect(find.text('Repay now'), findsOneWidget);

      await tester.tap(find.text('Repay now'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Repay'));
      await tester.pumpAndSettle();

      expect(service.lastRepaidLoanId, 'loan-2');
    });

    testWidgets('an active (non-overdue) loan has no repay action', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture], myLoans: const [loanFixture]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));

      expect(find.text('Repay now'), findsNothing);
    });

    testWidgets('Deposit tab opens the deposit dialog and submits', (tester) async {
      final service = FakeBankingService(
        allBanks: const [bankFixture],
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
      );

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));
      await tester.tap(find.widgetWithText(ChoiceChip, 'Deposit'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Deposit'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Deposit').last);
      await tester.pumpAndSettle();

      expect(service.lastOpenAccountArgs?['bankBuildingId'], 'bank-1');
    });

    testWidgets('Deposit tab city filter narrows the bank list', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture, _pendingBank]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));
      await tester.tap(find.widgetWithText(ChoiceChip, 'Deposit'));
      await tester.pumpAndSettle();
      expect(find.text('First National (Metropolis)'), findsOneWidget);
      expect(find.text('Second Trust (Riverside)'), findsOneWidget);

      await tester.tap(find.byKey(const Key('deposit-city-filter')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Metropolis').last);
      await tester.pumpAndSettle();

      expect(find.text('First National (Metropolis)'), findsOneWidget);
      expect(find.text('Second Trust (Riverside)'), findsNothing);
    });

    testWidgets('Deposit tab available-only checkbox narrows the bank list', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture, _pendingBank]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));
      await tester.tap(find.widgetWithText(ChoiceChip, 'Deposit'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('deposit-available-only')));
      await tester.pumpAndSettle();

      expect(find.text('First National (Metropolis)'), findsOneWidget);
      expect(find.text('Second Trust (Riverside)'), findsNothing);
    });

    testWidgets('withdrawing from an account calls closeBankAccount with the entered amount', (tester) async {
      final service = FakeBankingService(allBanks: const [bankFixture], myBankAccounts: const [_myAccount]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));
      await tester.tap(find.widgetWithText(ChoiceChip, 'Deposit'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Withdraw'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '200');
      await tester.tap(find.widgetWithText(FilledButton, 'Withdraw'));
      await tester.pumpAndSettle();

      expect(service.closedAccountId, 'account-1');
      expect(service.lastCloseAmount, 200.0);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeBankingService(loadError: Exception('down'));

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));

      expect(find.text('Could not load banking data. Please try again.'), findsOneWidget);
    });
  });
}
