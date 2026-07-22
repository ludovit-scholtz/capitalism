import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/banking/bank_management_screen.dart';
import 'package:capitalism_app/features/banking/banking_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/banking_fixtures.dart';
import 'support/fake_banking_service.dart';
import 'support/in_memory_token_storage.dart';

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
  group('BankManagementScreen', () {
    testWidgets('owner view shows rate form, liquidity panel, and issued loans', (tester) async {
      final service = FakeBankingService(
        bankInfo: bankInfoOwnerFixture,
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
        bankLoans: const [loanFixture],
      );

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));

      expect(find.text('Manage rates'), findsOneWidget);
      expect(find.text('Issued loans'), findsOneWidget);
      expect(find.text('Status: HEALTHY'), findsOneWidget);
      expect(find.text('Available cash: 15000'), findsOneWidget);
    });

    testWidgets('owner can schedule a deposit rate change', (tester) async {
      final service = FakeBankingService(
        bankInfo: bankInfoOwnerFixture,
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
      );

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));
      await tester.enterText(find.widgetWithText(TextField, 'New deposit rate (%)'), '3.5');
      await tester.tap(find.widgetWithText(OutlinedButton, 'Schedule change'));
      await tester.pumpAndSettle();

      expect(service.lastUpdateDepositRateArgs, {'bankBuildingId': 'bank-1', 'newRatePercent': 3.5});
    });

    testWidgets('owner can expand deposit rate history', (tester) async {
      final service = FakeBankingService(
        bankInfo: bankInfoOwnerFixture,
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
        rateHistory: const [
          BankDepositRateHistoryEntry(id: 'h1', previousRatePercent: 2.0, newRatePercent: 2.5, effectiveTick: 500, isApplied: true),
        ],
      );

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));
      await tester.tap(find.text('View rate history'));
      await tester.pumpAndSettle();

      expect(find.text('2.0% → 2.5%'), findsOneWidget);
      expect(find.text('Hide rate history'), findsOneWidget);
    });

    testWidgets('customer view shows a request-loan CTA', (tester) async {
      final service = FakeBankingService(bankInfo: bankInfoCustomerFixture, myCompanies: const []);

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));

      expect(find.widgetWithText(FilledButton, 'Request a loan'), findsOneWidget);
    });

    testWidgets('customer can repay an overdue loan at this bank', (tester) async {
      final service = FakeBankingService(
        bankInfo: bankInfoCustomerFixture,
        myCompanies: const [],
        myLoans: const [overdueLoanFixture],
      );

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));
      await tester.tap(find.text('Repay now'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Repay'));
      await tester.pumpAndSettle();

      expect(service.lastRepaidLoanId, 'loan-2');
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeBankingService(loadError: Exception('down'));

      await _pump(tester, BankManagementScreen(bankBuildingId: 'bank-1', bankingService: service));

      expect(find.text('Could not load this bank. Please try again.'), findsOneWidget);
    });
  });
}
