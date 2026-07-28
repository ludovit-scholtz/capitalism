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
    this.priceHistory = const [],
    this.personAccountStockSummary = const PersonAccountStockSummary(
      playerId: 'player-1',
      availableCash: 0,
      shareholdings: [],
      stockTrades: [],
    ),
    this.myCompanies = const [],
    this.dividendProposals = const [],
    this.listingsError,
    this.tradeError,
    this.orderError,
    this.dividendError,
    this.mergeError,
    this.replaceCeoError,
  });

  final List<StockListing> listings;
  final OrderBook orderBook;
  final List<StockTradeRecord> tradeHistory;
  final CompanyShareholders shareholders;
  final List<OpenOrder> openOrders;
  final List<Map<String, String>> bankAccounts;
  final List<StockPriceHistoryPoint> priceHistory;
  final PersonAccountStockSummary personAccountStockSummary;
  final List<Map<String, String>> myCompanies;
  final List<DividendProposal> dividendProposals;
  final Object? listingsError;
  final Object? tradeError;
  final Object? orderError;
  final Object? dividendError;
  final Object? mergeError;
  final Object? replaceCeoError;

  final List<String> calls = [];
  Map<String, dynamic>? lastBuyArgs;
  Map<String, dynamic>? lastSellArgs;
  Map<String, dynamic>? lastLimitOrderArgs;
  String? cancelledOrderId;
  Map<String, dynamic>? lastProposeDividendArgs;
  Map<String, dynamic>? lastVoteArgs;
  Map<String, dynamic>? lastMergeArgs;
  Map<String, dynamic>? lastReplaceCeoArgs;

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
    lastBuyArgs = {'companyId': companyId, 'shareCount': shareCount, 'bankAccountId': bankAccountId};
  }

  @override
  Future<void> sellShares({required String companyId, required double shareCount, String? bankAccountId}) async {
    calls.add('sellShares');
    if (tradeError != null) throw tradeError!;
    lastSellArgs = {'companyId': companyId, 'shareCount': shareCount, 'bankAccountId': bankAccountId};
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

  @override
  Future<List<StockPriceHistoryPoint>> fetchPriceHistory(String companyId) async {
    calls.add('fetchPriceHistory');
    return priceHistory;
  }

  @override
  Future<PersonAccountStockSummary> fetchPersonAccountStockSummary() async {
    calls.add('fetchPersonAccountStockSummary');
    return personAccountStockSummary;
  }

  @override
  Future<List<Map<String, String>>> fetchMyCompanies() async {
    calls.add('fetchMyCompanies');
    return myCompanies;
  }

  @override
  Future<List<DividendProposal>> fetchDividendProposals(String stockSymbol) async {
    calls.add('fetchDividendProposals');
    return dividendProposals;
  }

  @override
  Future<void> proposeDividend({required String stockSymbol, required double dividendPerShare}) async {
    calls.add('proposeDividend');
    if (dividendError != null) throw dividendError!;
    lastProposeDividendArgs = {'stockSymbol': stockSymbol, 'dividendPerShare': dividendPerShare};
  }

  @override
  Future<void> voteDividendProposal({required String proposalId, required String choice}) async {
    calls.add('voteDividendProposal');
    if (dividendError != null) throw dividendError!;
    lastVoteArgs = {'proposalId': proposalId, 'choice': choice};
  }

  @override
  Future<void> mergeCompany({required String targetCompanyId, required String destinationCompanyId}) async {
    calls.add('mergeCompany');
    if (mergeError != null) throw mergeError!;
    lastMergeArgs = {'targetCompanyId': targetCompanyId, 'destinationCompanyId': destinationCompanyId};
  }

  @override
  Future<void> replaceCeo({required String companyId, required String newCeoPlayerId}) async {
    calls.add('replaceCeo');
    if (replaceCeoError != null) throw replaceCeoError!;
    lastReplaceCeoArgs = {'companyId': companyId, 'newCeoPlayerId': newCeoPlayerId};
  }
}
