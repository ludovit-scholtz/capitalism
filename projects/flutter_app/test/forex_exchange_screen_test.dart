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

    testWidgets('getting a quote and confirming executes the swap', (tester) async {
      final service = FakeForexService(rates: const [_rate], balances: const [_balance]);

      await _pumpForex(tester, service: service);
      await tester.tap(find.widgetWithText(OutlinedButton, 'Get quote'));
      await tester.pumpAndSettle();

      expect(find.textContaining('You receive:'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Confirm swap'));
      await tester.pumpAndSettle();

      expect(service.lastSwapArgs, isNotNull);
    });
  });
}
