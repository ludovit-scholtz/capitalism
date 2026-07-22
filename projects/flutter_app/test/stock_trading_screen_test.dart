import 'package:capitalism_app/core/auth/auth_state.dart';
import 'package:capitalism_app/features/exchange/stock_models.dart';
import 'package:capitalism_app/features/exchange/stock_trading_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
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

const _openOrder = OpenOrder(
  id: 'order-1',
  stockSymbol: 'ACME',
  companyName: 'Acme Corp',
  side: 'BUY',
  limitPrice: 40,
  remainingQuantity: 5,
  status: 'OPEN',
);

Future<void> _pumpStockTrading(WidgetTester tester, {required FakeStockService service}) async {
  await tester.binding.setSurfaceSize(const Size(800, 2400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final auth = AuthState(storage: InMemoryTokenStorage());
  await tester.pumpWidget(
    ChangeNotifierProvider<AuthState>.value(
      value: auth,
      child: MaterialApp(home: Scaffold(body: StockTradingScreen(companyId: 'company-1', stockService: service))),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('StockTradingScreen', () {
    testWidgets('shows the stock header, order book, and shareholders', (tester) async {
      final service = FakeStockService(
        listings: [_listing],
        orderBook: const OrderBook(bids: [OrderBookLevel(price: 42, totalQuantity: 100)], asks: [OrderBookLevel(price: 43, totalQuantity: 50)]),
        shareholders: const CompanyShareholders(totalSharesIssued: 1000, shareholders: [Shareholder(holderName: 'Alice', shareCount: 100, ownershipRatio: 0.1)]),
      );

      await _pumpStockTrading(tester, service: service);

      expect(find.text('Acme Corp (ACME)'), findsOneWidget);
      expect(find.text('42.00 × 100'), findsOneWidget);
      expect(find.text('Alice'), findsOneWidget);
    });

    testWidgets('shows not-traded error for a company with no listing', (tester) async {
      final service = FakeStockService(listings: const []);

      await _pumpStockTrading(tester, service: service);

      expect(find.text('This company is not publicly traded.'), findsOneWidget);
    });

    testWidgets('market buy calls buyShares with entered quantity', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockTrading(tester, service: service);
      await tester.enterText(find.byType(TextField).first, '5');
      await tester.tap(find.widgetWithText(FilledButton, 'Buy'));
      await tester.pumpAndSettle();

      expect(service.lastBuyArgs?['companyId'], 'company-1');
      expect(service.lastBuyArgs?['shareCount'], 5.0);
    });

    testWidgets('placing a limit order calls placeLimitOrder', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockTrading(tester, service: service);
      final textFields = find.byType(TextField);
      await tester.enterText(textFields.at(0), '3');
      await tester.enterText(textFields.at(1), '41.5');
      await tester.tap(find.widgetWithText(FilledButton, 'Place order'));
      await tester.pumpAndSettle();

      expect(service.lastLimitOrderArgs?['stockSymbol'], 'ACME');
      expect(service.lastLimitOrderArgs?['side'], 'BUY');
      expect(service.lastLimitOrderArgs?['limitPrice'], 41.5);
      expect(service.lastLimitOrderArgs?['quantity'], 3);
    });

    testWidgets('cancelling an open order calls cancelLimitOrder', (tester) async {
      final service = FakeStockService(listings: [_listing], openOrders: [_openOrder]);

      await _pumpStockTrading(tester, service: service);
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(service.cancelledOrderId, 'order-1');
    });
  });
}
