import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/core/context/account_context_models.dart';
import 'package:capitalism_app/core/context/account_context_state.dart';
import 'package:capitalism_app/features/banking/bank_loan_request_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/banking_fixtures.dart';
import 'support/fake_banking_service.dart';
import 'support/in_memory_selected_city_storage.dart';
import 'support/in_memory_token_storage.dart';

Future<void> _pump(WidgetTester tester, Widget widget, {String? activeCompanyId}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await auth.setToken('test-token');
  final accountContextState = AccountContextState(storage: InMemorySelectedCityStorage());
  if (activeCompanyId != null) {
    accountContextState.activeAccount = ActiveAccountInfo(
      playerId: 'player-1',
      displayName: 'Player',
      availableCash: 0,
      activeAccountType: 'COMPANY',
      activeCompanyId: activeCompanyId,
    );
  }
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: widget)),
      GoRoute(path: '/banking', builder: (context, state) => const Scaffold(body: Text('Banking Screen'))),
    ],
  );
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthState>.value(value: auth),
        ChangeNotifierProvider<AccountContextState>.value(value: accountContextState),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('BankLoanRequestScreen', () {
    testWidgets('submits an accept-loan request with the default duration', (tester) async {
      final service = FakeBankingService(
        bankInfo: bankInfoOwnerFixture,
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
      expect(service.lastAcceptLoanArgs?['durationTicks'], 8760);
      expect(find.text('Banking Screen'), findsOneWidget);
    });

    testWidgets('defaults the borrowing company to the header\'s active company', (tester) async {
      final service = FakeBankingService(
        bankInfo: bankInfoOwnerFixture,
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
          {'id': 'company-2', 'name': 'Second Company'},
        ],
        companyBankAccounts: const [
          {'id': 'account-1', 'currencyCode': 'EUR'},
        ],
      );

      await _pump(
        tester,
        BankLoanRequestScreen(bankBuildingId: 'bank-1', bankingService: service),
        activeCompanyId: 'company-2',
      );
      await tester.enterText(find.widgetWithText(TextField, 'Principal amount'), '5000');
      await tester.tap(find.widgetWithText(FilledButton, 'Accept loan'));
      await tester.pumpAndSettle();

      expect(service.lastAcceptLoanArgs?['borrowerCompanyId'], 'company-2');
    });

    testWidgets('submits a custom loan duration', (tester) async {
      final service = FakeBankingService(
        bankInfo: bankInfoOwnerFixture,
        myCompanies: const [
          {'id': 'company-1', 'name': 'My Company'},
        ],
        companyBankAccounts: const [
          {'id': 'account-1', 'currencyCode': 'EUR'},
        ],
      );

      await _pump(tester, BankLoanRequestScreen(bankBuildingId: 'bank-1', bankingService: service));
      await tester.enterText(find.widgetWithText(TextField, 'Principal amount'), '5000');
      await tester.enterText(find.widgetWithText(TextField, 'Duration (ticks, 1–87600)'), '2000');
      await tester.tap(find.widgetWithText(FilledButton, 'Accept loan'));
      await tester.pumpAndSettle();

      expect(service.lastAcceptLoanArgs?['durationTicks'], 2000);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeBankingService(loadError: Exception('down'));

      await _pump(tester, BankLoanRequestScreen(bankBuildingId: 'bank-1', bankingService: service));

      expect(find.text('Could not load loan options. Please try again.'), findsOneWidget);
    });
  });
}
