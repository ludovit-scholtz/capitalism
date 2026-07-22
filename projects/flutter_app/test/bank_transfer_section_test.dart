import 'package:capitalism_app/features/exchange/bank_transfer_section.dart';
import 'package:capitalism_app/features/exchange/forex_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_forex_service.dart';

const _accountOne = BankAccountOption(id: 'acc-1', accountNumber: '001', currencyCode: 'EUR', currencySymbol: '€', balance: 500, ownerDisplayName: 'Me');
const _accountTwo = BankAccountOption(id: 'acc-2', accountNumber: '002', currencyCode: 'EUR', currencySymbol: '€', balance: 100, ownerDisplayName: 'Me');
const _accounts = [_accountOne, _accountTwo];

Future<void> _pump(WidgetTester tester, {required FakeForexService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 1600));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(MaterialApp(home: Scaffold(body: BankTransferSection(forexService: service))));
  await tester.pumpAndSettle();
}

void main() {
  group('BankTransferSection', () {
    testWidgets('prompts for a second account when only one exists', (tester) async {
      final service = FakeForexService(bankAccounts: const [_accountOne]);

      await _pump(tester, service: service);

      expect(find.text('You need at least two bank accounts to transfer funds.'), findsOneWidget);
    });

    testWidgets('shows an error state with Try again on load failure', (tester) async {
      final service = FakeForexService(loadError: Exception('down'));

      await _pump(tester, service: service);

      expect(find.text('Could not load your bank accounts. Please try again.'), findsOneWidget);
    });

    testWidgets('submitting a transfer calls transferFunds with the entered amount', (tester) async {
      final service = FakeForexService(bankAccounts: _accounts);

      await _pump(tester, service: service);
      await tester.enterText(find.widgetWithText(TextField, 'Amount'), '50');
      await tester.tap(find.widgetWithText(FilledButton, 'Transfer'));
      await tester.pumpAndSettle();

      expect(service.lastTransferArgs?['fromBankAccountId'], 'acc-1');
      expect(service.lastTransferArgs?['toBankAccountId'], 'acc-2');
      expect(service.lastTransferArgs?['amount'], 50.0);
      expect(find.text('Transferred 50.00 EUR.'), findsOneWidget);
    });

    testWidgets('shows a snack bar on transfer failure', (tester) async {
      final service = FakeForexService(bankAccounts: _accounts, transferError: Exception('failed'));

      await _pump(tester, service: service);
      await tester.enterText(find.widgetWithText(TextField, 'Amount'), '50');
      await tester.tap(find.widgetWithText(FilledButton, 'Transfer'));
      await tester.pumpAndSettle();

      expect(find.text('Transfer failed. Please try again.'), findsOneWidget);
    });
  });
}
