import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/exchange/stock_exchange_screen.dart';
import 'package:capitalism_app/features/exchange/stock_models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'support/fake_stock_service.dart';
import 'support/in_memory_token_storage.dart';

const _listing = StockListing(
  companyId: 'company-1',
  stockSymbol: 'ACME',
  companyName: 'Acme Corp',
  primaryCityName: 'Metropolis',
  primaryIndustry: 'MANUFACTURING',
  sharePrice: 42.5,
  dailyChangePercent: 3.2,
  marketValue: 1000000,
  playerOwnedShares: 10,
);

Future<GoRouter> _pumpStockExchange(WidgetTester tester, {required FakeStockService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2000));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  final router = GoRouter(
    initialLocation: '/',
    routes: [
      GoRoute(path: '/', builder: (context, state) => Scaffold(body: StockExchangeScreen(stockService: service))),
      GoRoute(
        path: '/stock/trade/:companyId',
        builder: (context, state) => Scaffold(body: Text('Trade ${state.pathParameters['companyId']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(value: auth, child: MaterialApp.router(routerConfig: router)),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  group('StockExchangeScreen', () {
    testWidgets('shows stock listings with price and change', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockExchange(tester, service: service);

      expect(find.text('Acme Corp (ACME)'), findsOneWidget);
      expect(find.text('+3.2%'), findsOneWidget);
    });

    testWidgets('shows error state with Try again on load failure', (tester) async {
      final service = FakeStockService(listingsError: Exception('down'));

      await _pumpStockExchange(tester, service: service);

      expect(find.text('Could not load the stock exchange. Please try again.'), findsOneWidget);
    });

    testWidgets('tapping a listing navigates to stock trading', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockExchange(tester, service: service);
      await tester.tap(find.text('Acme Corp (ACME)'));
      await tester.pumpAndSettle();

      expect(find.text('Trade company-1'), findsOneWidget);
    });
  });
}
