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

    testWidgets('market sell calls sellShares with entered quantity', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockTrading(tester, service: service);
      await tester.enterText(find.byType(TextField).first, '4');
      await tester.tap(find.widgetWithText(OutlinedButton, 'Sell'));
      await tester.pumpAndSettle();

      expect(service.lastSellArgs?['companyId'], 'company-1');
      expect(service.lastSellArgs?['shareCount'], 4.0);
    });

    testWidgets('shows a price-history sparkline when enough history is available', (tester) async {
      final service = FakeStockService(
        listings: [_listing],
        priceHistory: const [
          StockPriceHistoryPoint(tick: 1, price: 40),
          StockPriceHistoryPoint(tick: 2, price: 41),
          StockPriceHistoryPoint(tick: 3, price: 42.5),
        ],
      );

      await _pumpStockTrading(tester, service: service);

      expect(find.byKey(const Key('stock-price-history-chart')), findsOneWidget);
    });

    testWidgets('shows the position summary with average buy price and unrealized P&L', (tester) async {
      final service = FakeStockService(
        listings: [_listing],
        personAccountStockSummary: const PersonAccountStockSummary(
          playerId: 'player-1',
          availableCash: 5000,
          shareholdings: [PortfolioHolding(companyId: 'company-1', shareCount: 10, marketValue: 425)],
          stockTrades: [
            PersonTradeRecord(companyId: 'company-1', direction: 'BUY', shareCount: 10, pricePerShare: 40, recordedAtTick: 1),
          ],
        ),
      );

      await _pumpStockTrading(tester, service: service);

      expect(find.text('Shares owned: 10'), findsOneWidget);
      expect(find.text('Average buy price: 40.00'), findsOneWidget);
      expect(find.text('Unrealized P&L: +25.00'), findsOneWidget);
      expect(find.text('Available cash: 5000.00'), findsOneWidget);
    });

    testWidgets('shows no average buy price when there is no trade history', (tester) async {
      final service = FakeStockService(listings: [_listing]);

      await _pumpStockTrading(tester, service: service);

      expect(find.text('Shares owned: 0'), findsOneWidget);
      expect(find.textContaining('Average buy price'), findsNothing);
      expect(find.textContaining('Unrealized P&L'), findsNothing);
    });

    testWidgets('shows the empty state for recent trades when there is no trade history', (tester) async {
      final service = FakeStockService(listings: [_listing], tradeHistory: const []);

      await _pumpStockTrading(tester, service: service);

      expect(find.text('No trades yet.'), findsOneWidget);
    });

    testWidgets('renders no shareholder rows when the company has no public shareholders', (tester) async {
      final service = FakeStockService(
        listings: [_listing],
        shareholders: const CompanyShareholders(totalSharesIssued: 1000, shareholders: []),
      );

      await _pumpStockTrading(tester, service: service);

      expect(find.text('Shareholders'), findsOneWidget);
      expect(find.byType(ListTile), findsNothing);
    });

    testWidgets('does not show the price-history sparkline with fewer than 2 points', (tester) async {
      final service = FakeStockService(
        listings: [_listing],
        priceHistory: const [StockPriceHistoryPoint(tick: 1, price: 40)],
      );

      await _pumpStockTrading(tester, service: service);

      expect(find.byKey(const Key('stock-price-history-chart')), findsNothing);
    });
  });
}
