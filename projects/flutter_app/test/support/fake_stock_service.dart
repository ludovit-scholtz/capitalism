import 'package:capitalism_app/features/exchange/stock_models.dart';
import 'package:capitalism_app/features/exchange/stock_service.dart';

class FakeStockService implements StockService {
  FakeStockService({
    this.listings = const [],
    this.orderBook = const OrderBook(bids: [], asks: []),
    this.tradeHistory = const [],
    this.shareholders = const CompanyShareholders(totalSharesIssued: 0, shareholders: []),
    this.openOrders = const [],
    this.bankAccounts = const [],
    this.listingsError,
    this.tradeError,
    this.orderError,
  });

  final List<StockListing> listings;
  final OrderBook orderBook;
  final List<StockTradeRecord> tradeHistory;
  final CompanyShareholders shareholders;
  final List<OpenOrder> openOrders;
  final List<Map<String, String>> bankAccounts;
  final Object? listingsError;
  final Object? tradeError;
  final Object? orderError;

  final List<String> calls = [];
  Map<String, dynamic>? lastBuyArgs;
  Map<String, dynamic>? lastSellArgs;
  Map<String, dynamic>? lastLimitOrderArgs;
  String? cancelledOrderId;

  @override
  Future<List<StockListing>> fetchListings() async {
    calls.add('fetchListings');
    if (listingsError != null) throw listingsError!;
    return listings;
  }

  @override
  Future<List<Map<String, String>>> fetchMyBankAccounts() async {
    calls.add('fetchMyBankAccounts');
    return bankAccounts;
  }

  @override
  Future<void> buyShares({required String companyId, required double shareCount, String? bankAccountId}) async {
    calls.add('buyShares');
    if (tradeError != null) throw tradeError!;
    lastBuyArgs = {'companyId': companyId, 'shareCount': shareCount};
  }

  @override
  Future<void> sellShares({required String companyId, required double shareCount, String? bankAccountId}) async {
    calls.add('sellShares');
    if (tradeError != null) throw tradeError!;
    lastSellArgs = {'companyId': companyId, 'shareCount': shareCount};
  }

  @override
  Future<OrderBook> fetchOrderBook(String stockSymbol) async {
    calls.add('fetchOrderBook');
    return orderBook;
  }

  @override
  Future<List<StockTradeRecord>> fetchTradeHistory(String stockSymbol, {int limit = 20}) async {
    calls.add('fetchTradeHistory');
    return tradeHistory;
  }

  @override
  Future<CompanyShareholders> fetchShareholders(String companyId) async {
    calls.add('fetchShareholders');
    return shareholders;
  }

  @override
  Future<List<OpenOrder>> fetchMyOpenOrders() async {
    calls.add('fetchMyOpenOrders');
    return openOrders;
  }

  @override
  Future<void> placeLimitOrder({
    required String stockSymbol,
    required String side,
    required double limitPrice,
    required int quantity,
  }) async {
    calls.add('placeLimitOrder');
    if (orderError != null) throw orderError!;
    lastLimitOrderArgs = {'stockSymbol': stockSymbol, 'side': side, 'limitPrice': limitPrice, 'quantity': quantity};
  }

  @override
  Future<void> cancelLimitOrder(String orderId) async {
    calls.add('cancelLimitOrder');
    if (orderError != null) throw orderError!;
    cancelledOrderId = orderId;
  }
}
