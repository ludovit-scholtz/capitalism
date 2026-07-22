import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/banking/banking_models.dart';
import 'package:capitalism_app/features/banking/banking_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_banking_service.dart';
import 'support/in_memory_token_storage.dart';

const _bank = BankListing(
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  cityName: 'Metropolis',
  depositInterestRatePercent: 2.5,
  lendingInterestRatePercent: 6.0,
  availableLendingCapacity: 100000,
  baseCapitalDeposited: true,
  lenderCompanyId: 'company-owner',
);

const _loan = LoanSummary(
  id: 'loan-1',
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  loanCurrencyCode: 'EUR',
  originalPrincipal: 10000,
  remainingPrincipal: 8000,
  annualInterestRatePercent: 6,
  nextPaymentTick: 100,
  paymentAmount: 500,
  status: 'ACTIVE',
  missedPayments: 0,
);

const _bankInfoOwner = BankInfo(
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  cityCurrencyCode: 'EUR',
  lenderCompanyId: 'company-1',
  depositInterestRatePercent: 2.5,
  lendingInterestRatePercent: 6.0,
  totalDeposits: 50000,
  availableLendingCapacity: 100000,
  baseCapitalDeposited: true,
  baseCapitalRequirement: 20000,
  liquidityStatus: 'HEALTHY',
);

const _bankInfoCustomer = BankInfo(
  bankBuildingId: 'bank-1',
  bankBuildingName: 'First National',
  cityCurrencyCode: 'EUR',
  lenderCompanyId: 'someone-else',
  depositInterestRatePercent: 2.5,
  lendingInterestRatePercent: 6.0,
  totalDeposits: 50000,
  availableLendingCapacity: 100000,
  baseCapitalDeposited: true,
  baseCapitalRequirement: 20000,
  liquidityStatus: 'HEALTHY',
);

const _statement = BankStatementResult(
  companyName: 'Acme Corp',
  currencyCode: 'EUR',
  currentBalance: 5000,
  totalEntries: 1,
  rows: [BankStatementRow(id: 'row-1', description: 'Salary payment', category: 'LABOR', amount: -200, runningBalance: 4800)],
);

Future<void> _pump(WidgetTester tester, Widget widget, {bool authenticated = true}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: widget)),
      GoRoute(path: '/login', builder: (context, state) => const Scaffold(body: Text('Login Screen'))),
      GoRoute(path: '/banking', builder: (context, state) => const Scaffold(body: Text('Banking Screen'))),
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
      final service = FakeBankingService(allBanks: const [_bank], myLoans: const [_loan]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));

      expect(find.text('First National (Metropolis)'), findsOneWidget);
      expect(find.textContaining('8000 EUR'), findsOneWidget);
    });

    testWidgets('Borrow button navigates to the loan request screen', (tester) async {
      final service = FakeBankingService(allBanks: const [_bank]);

      await _pump(tester, LoanMarketplaceScreen(bankingService: service));
      await tester.tap(find.widgetWithText(FilledButton, 'Borrow'));
      await tester.pumpAndSettle();

      expect(find.text('Request Loan bank-1'), findsOneWidget);
    });

    testWidgets('Deposit tab opens the deposit dialog and submits', (tester) async {
      final service = FakeBankingService(
        allBanks: const [_bank],
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
  });

  group('BankManagementScreen', () {
    testWidgets('owner view shows rate form and issued loans', (tester) async {
      final service = FakeBankingService(
        bankInfo: _bankInfoOwner,
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
        bankLoans: const [_loan],
      );

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));

      expect(find.text('Manage rates'), findsOneWidget);
      expect(find.text('Issued loans'), findsOneWidget);
    });

    testWidgets('customer view shows a request-loan CTA', (tester) async {
      final service = FakeBankingService(bankInfo: _bankInfoCustomer, myCompanies: const []);

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));

      expect(find.widgetWithText(FilledButton, 'Request a loan'), findsOneWidget);
    });
  });

  group('BankLoanRequestScreen', () {
    testWidgets('submits an accept-loan request', (tester) async {
      final service = FakeBankingService(
        bankInfo: _bankInfoOwner,
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
        companyBankAccounts: const [
          {'id': 'account-1', 'currencyCode': 'EUR'},
        ],
      );

      await _pump(tester, BankLoanRequestScreen(bankBuildingId: 'bank-1', bankingService: service));
      await tester.enterText(find.widgetWithText(TextField, 'Principal amount'), '5000');
      await tester.tap(find.widgetWithText(FilledButton, 'Accept loan'));
      await tester.pumpAndSettle();

      expect(service.lastAcceptLoanArgs?['borrowerCompanyId'], 'company-1');
      expect(service.lastAcceptLoanArgs?['principalAmount'], 5000.0);
      expect(find.text('Banking Screen'), findsOneWidget);
    });
  });

  group('BankStatementScreen', () {
    testWidgets('redirects unauthenticated users to /login', (tester) async {
      final service = FakeBankingService();

      await _pump(tester, BankStatementScreen(bankingService: service), authenticated: false);

      expect(find.text('Login Screen'), findsOneWidget);
    });

    testWidgets('shows the statement rows and balance', (tester) async {
      final service = FakeBankingService(bankStatement: _statement);

      await _pump(tester, BankStatementScreen(bankingService: service));

      expect(find.textContaining('5000.00 EUR'), findsOneWidget);
      expect(find.text('Salary payment'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeBankingService(loadError: Exception('down'));

      await _pump(tester, BankStatementScreen(bankingService: service));

      expect(find.text('Could not load your bank statement. Please try again.'), findsOneWidget);
    });
  });
}
