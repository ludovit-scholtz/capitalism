import '../../core/graphql/graphql_service.dart';
import 'stock_models.dart';

const _listingsQuery = r'''
  query StockExchangeListings {
    stockExchangeListings {
      companyId stockSymbol companyName primaryCityName primaryIndustry totalSharesIssued publicFloatShares
      sharePrice dailyChangePercent marketValue bidPrice askPrice dividendPayoutRatio playerOwnedShares
      controlledCompanyOwnedShares combinedControlledOwnershipRatio canProposeDividend canClaimControl canMerge
    }
  }
''';

const _myBankAccountsQuery = r'''
  query StockMyBankAccounts { myBankAccounts { id currencyCode balance companyId ownerType } }
''';

const _buySharesMutation = r'''
  mutation BuyShares($input: BuySharesInput!) {
    buyShares(input: $input) { companyId companyName shareCount pricePerShare totalValue ownedShareCount }
  }
''';

const _sellSharesMutation = r'''
  mutation SellShares($input: SellSharesInput!) {
    sellShares(input: $input) { companyId companyName shareCount pricePerShare totalValue ownedShareCount }
  }
''';

const _orderBookQuery = r'''
  query OrderBook($stockSymbol: String!) {
    orderBook(stockSymbol: $stockSymbol) { stockSymbol bids { price totalQuantity } asks { price totalQuantity } }
  }
''';

const _tradeHistoryQuery = r'''
  query StockTradeHistory($stockSymbol: String!, $limit: Int!) {
    stockTradeHistory(stockSymbol: $stockSymbol, limit: $limit) { id stockSymbol price quantity executedAtTick executedAtUtc }
  }
''';

const _shareholdersQuery = r'''
  query CompanyShareholders($companyId: UUID!) {
    companyShareholders(companyId: $companyId) {
      companyId companyName totalSharesIssued publicFloatShares shareholderCount
      shareholders { holderName holderType holderPlayerId holderCompanyId shareCount ownershipRatio }
    }
  }
''';

const _myOpenOrdersQuery = r'''
  query MyOpenOrders {
    myOpenOrders {
      id companyId companyName stockSymbol side limitPrice quantity filledQuantity remainingQuantity status createdAtTick updatedAtTick
    }
  }
''';

const _placeLimitOrderMutation = r'''
  mutation PlaceLimitOrder($input: PlaceLimitOrderInput!) {
    placeLimitOrder(input: $input) { id companyId companyName stockSymbol side limitPrice quantity filledQuantity remainingQuantity status createdAtTick updatedAtTick }
  }
''';

const _cancelLimitOrderMutation = r'''
  mutation CancelLimitOrder($orderId: UUID!) {
    cancelLimitOrder(orderId: $orderId) { id status remainingQuantity }
  }
''';

/// GraphQL calls for the Stock Exchange and Stock Trading screens, matching
/// `projects/frontend/src/views/StockExchangeView.vue`/`StockTradingView.vue`'s
/// core buy/sell/order-book contract. Dividend proposals/voting, company
/// mergers, CEO replacement, and hostile takeover are deliberately not
/// ported — see the screens' top-of-file comments.
class StockService {
  const StockService(this._graphQlService);

  final GraphQlService _graphQlService;

  Future<List<StockListing>> fetchListings() async {
    final result = await _graphQlService.request(_listingsQuery);
    final list = result['stockExchangeListings'] as List<dynamic>? ?? const [];
    return list.map((e) => StockListing.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Map<String, String>>> fetchMyBankAccounts() async {
    final result = await _graphQlService.request(_myBankAccountsQuery);
    final accounts = result['myBankAccounts'] as List<dynamic>? ?? const [];
    return accounts
        .map((e) => {'id': (e as Map<String, dynamic>)['id'] as String, 'currencyCode': e['currencyCode'] as String})
        .toList();
  }

  Future<void> buyShares({required String companyId, required double shareCount, String? bankAccountId}) {
    return _graphQlService.request(
      _buySharesMutation,
      variables: {
        'input': {'companyId': companyId, 'shareCount': shareCount, 'bankAccountId': bankAccountId},
      },
    );
  }

  Future<void> sellShares({required String companyId, required double shareCount, String? bankAccountId}) {
    return _graphQlService.request(
      _sellSharesMutation,
      variables: {
        'input': {'companyId': companyId, 'shareCount': shareCount, 'bankAccountId': bankAccountId},
      },
    );
  }

  Future<OrderBook> fetchOrderBook(String stockSymbol) async {
    final result = await _graphQlService.request(_orderBookQuery, variables: {'stockSymbol': stockSymbol});
    return OrderBook.fromJson(result['orderBook'] as Map<String, dynamic>);
  }

  Future<List<StockTradeRecord>> fetchTradeHistory(String stockSymbol, {int limit = 20}) async {
    final result = await _graphQlService.request(_tradeHistoryQuery, variables: {'stockSymbol': stockSymbol, 'limit': limit});
    final list = result['stockTradeHistory'] as List<dynamic>? ?? const [];
    return list.map((e) => StockTradeRecord.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<CompanyShareholders> fetchShareholders(String companyId) async {
    final result = await _graphQlService.request(_shareholdersQuery, variables: {'companyId': companyId});
    return CompanyShareholders.fromJson(result['companyShareholders'] as Map<String, dynamic>);
  }

  Future<List<OpenOrder>> fetchMyOpenOrders() async {
    final result = await _graphQlService.request(_myOpenOrdersQuery);
    final list = result['myOpenOrders'] as List<dynamic>? ?? const [];
    return list.map((e) => OpenOrder.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> placeLimitOrder({
    required String stockSymbol,
    required String side,
    required double limitPrice,
    required int quantity,
  }) {
    return _graphQlService.request(
      _placeLimitOrderMutation,
      variables: {
        'input': {'stockSymbol': stockSymbol, 'side': side, 'limitPrice': limitPrice, 'quantity': quantity},
      },
    );
  }

  Future<void> cancelLimitOrder(String orderId) {
    return _graphQlService.request(_cancelLimitOrderMutation, variables: {'orderId': orderId});
  }
}
