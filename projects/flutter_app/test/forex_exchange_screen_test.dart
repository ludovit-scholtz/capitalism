import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/exchange/forex_exchange_screen.dart';
import 'package:capitalism_app/features/exchange/forex_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_forex_service.dart';
import 'support/in_memory_token_storage.dart';

const _rate = FxRate(baseCurrencyCode: 'EUR', quoteCurrencyCode: 'USD', rate: 1.08);
const _balance = CurrencyBalance(currencyCode: 'EUR', currencySymbol: '€', balance: 5000);
const _trade = ForexTrade(fromCurrencyCode: 'EUR', toCurrencyCode: 'USD', fromAmount: 100, toAmount: 108, rate: 1.08, executedAtUtc: '2026-07-01T00:00:00Z');

Future<GoRouter> _pumpForex(WidgetTester tester, {required FakeForexService service, bool authenticated = true}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  if (authenticated) await auth.setToken('test-token');
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: ForexExchangeScreen(forexService: service))),
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
  group('ForexExchangeScreen', () {
    testWidgets('redirects to /login when not authenticated', (tester) async {
      final service = FakeForexService();

      await _pumpForex(tester, service: service, authenticated: false);

      expect(find.text('Login Screen'), findsOneWidget);
      expect(service.calls, isEmpty);
    });

    testWidgets('shows balances on the Swap tab by default', (tester) async {
      final service = FakeForexService(rates: const [_rate], balances: const [_balance]);

      await _pumpForex(tester, service: service);

      expect(find.text('€5000.00'), findsOneWidget);
    });

    testWidgets('Rates tab shows fx rates', (tester) async {
      final service = FakeForexService(rates: const [_rate], balances: const [_balance]);

      await _pumpForex(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Rates'));
      await tester.pumpAndSettle();

      expect(find.text('EUR → USD'), findsOneWidget);
    });

    testWidgets('History tab shows trade history', (tester) async {
      final service = FakeForexService(rates: const [_rate], balances: const [_balance], history: const [_trade]);

      await _pumpForex(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'History'));
      await tester.pumpAndSettle();

      expect(find.textContaining('100.00 EUR'), findsOneWidget);
    });

    testWidgets('getting a quote and confirming executes the swap with the default slippage', (tester) async {
      final service = FakeForexService(rates: const [_rate], balances: const [_balance]);

      await _pumpForex(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Get quote'));
      await tester.pumpAndSettle();

      expect(find.textContaining('You receive:'), findsOneWidget);
      expect(find.text('Quote expires in 30s'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Confirm swap'));
      await tester.pumpAndSettle();

      expect(service.lastSwapArgs?['quoteNonce'], 'nonce-1');
      expect(service.lastSwapArgs?['acceptedSlippageBps'], 100);
    });

    testWidgets('selecting a slippage preset is passed through to the swap', (tester) async {
      final service = FakeForexService(rates: const [_rate], balances: const [_balance]);

      await _pumpForex(tester, service: service);
      await tester.tap(find.byKey(const Key('slippage-200')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(OutlinedButton, 'Get quote'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm swap'));
      await tester.pumpAndSettle();

      expect(service.lastSwapArgs?['acceptedSlippageBps'], 200);
    });

    testWidgets('shows a commodity-shock market event banner when active', (tester) async {
      final service = FakeForexService(
        rates: const [_rate],
        balances: const [_balance],
        activeMarketEvents: const [
          MarketEvent(
            id: 'event-1',
            title: 'Oil shock',
            description: 'Oil prices are spiking.',
            magnitudeMultiplier: 1.5,
            ticksRemaining: 20,
            affectedResourceName: 'Crude Oil',
          ),
        ],
      );

      await _pumpForex(tester, service: service);

      expect(find.text('Oil shock'), findsOneWidget);
      expect(find.text('20 ticks remaining'), findsOneWidget);
    });

    testWidgets('Rates tab shows a rate-history chart once a currency is selected', (tester) async {
      final service = FakeForexService(
        rates: const [_rate],
        balances: const [_balance],
        rateHistory: const [
          FxRateHistoryPoint(gameTick: 1, midRate: 1.05),
          FxRateHistoryPoint(gameTick: 2, midRate: 1.08),
        ],
      );

      await _pumpForex(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Rates'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('rate-history-chart')), findsOneWidget);
    });

    testWidgets('Transfer tab shows the bank-transfer section', (tester) async {
      final service = FakeForexService(
        rates: const [_rate],
        balances: const [_balance],
        bankAccounts: const [
          BankAccountOption(id: 'acc-1', accountNumber: '001', currencyCode: 'EUR', currencySymbol: '€', balance: 100, ownerDisplayName: 'Me'),
          BankAccountOption(id: 'acc-2', accountNumber: '002', currencyCode: 'EUR', currencySymbol: '€', balance: 50, ownerDisplayName: 'Me'),
        ],
      );

      await _pumpForex(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Transfer'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('transfer-from-account')), findsOneWidget);
    });

    testWidgets('Gold tab shows the gold AMM section', (tester) async {
      final service = FakeForexService(rates: const [_rate], balances: const [_balance]);

      await _pumpForex(tester, service: service);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Gold'));
      await tester.pumpAndSettle();

      expect(find.text('My gold'), findsOneWidget);
    });
  });
}
