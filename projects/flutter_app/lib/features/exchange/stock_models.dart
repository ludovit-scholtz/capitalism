// Data models for the Stock Exchange and Stock Trading screens, mirroring
// `projects/frontend/src/views/StockExchangeView.vue` /
// `StockTradingView.vue`. GraphQL field names verified against
// `Api/Types/Inputs.Stock.cs` and the `stockExchangeListings`/`orderBook`/
// `companyShareholders`/`myOpenOrders` queries.

class StockListing {
  const StockListing({
    required this.companyId,
    required this.stockSymbol,
    required this.companyName,
    required this.primaryCityName,
    required this.primaryIndustry,
    required this.sharePrice,
    required this.dailyChangePercent,
    required this.marketValue,
    required this.playerOwnedShares,
  });

  final String companyId;
  final String stockSymbol;
  final String companyName;
  final String? primaryCityName;
  final String? primaryIndustry;
  final double sharePrice;
  final double dailyChangePercent;
  final double marketValue;
  final double playerOwnedShares;

  factory StockListing.fromJson(Map<String, dynamic> json) => StockListing(
    companyId: json['companyId'] as String,
    stockSymbol: (json['stockSymbol'] as String?) ?? '',
    companyName: (json['companyName'] as String?) ?? '',
    primaryCityName: json['primaryCityName'] as String?,
    primaryIndustry: json['primaryIndustry'] as String?,
    sharePrice: (json['sharePrice'] as num?)?.toDouble() ?? 0,
    dailyChangePercent: (json['dailyChangePercent'] as num?)?.toDouble() ?? 0,
    marketValue: (json['marketValue'] as num?)?.toDouble() ?? 0,
    playerOwnedShares: (json['playerOwnedShares'] as num?)?.toDouble() ?? 0,
  );
}

class OrderBookLevel {
  const OrderBookLevel({required this.price, required this.totalQuantity});

  final double price;
  final double totalQuantity;

  factory OrderBookLevel.fromJson(Map<String, dynamic> json) => OrderBookLevel(
    price: (json['price'] as num?)?.toDouble() ?? 0,
    totalQuantity: (json['totalQuantity'] as num?)?.toDouble() ?? 0,
  );
}

class OrderBook {
  const OrderBook({required this.bids, required this.asks});

  final List<OrderBookLevel> bids;
  final List<OrderBookLevel> asks;

  factory OrderBook.fromJson(Map<String, dynamic> json) => OrderBook(
    bids: ((json['bids'] as List<dynamic>?) ?? const []).map((e) => OrderBookLevel.fromJson(e as Map<String, dynamic>)).toList(),
    asks: ((json['asks'] as List<dynamic>?) ?? const []).map((e) => OrderBookLevel.fromJson(e as Map<String, dynamic>)).toList(),
  );
}

class StockTradeRecord {
  const StockTradeRecord({required this.price, required this.quantity, required this.executedAtTick});

  final double price;
  final double quantity;
  final int executedAtTick;

  factory StockTradeRecord.fromJson(Map<String, dynamic> json) => StockTradeRecord(
    price: (json['price'] as num?)?.toDouble() ?? 0,
    quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
    executedAtTick: (json['executedAtTick'] as num?)?.toInt() ?? 0,
  );
}

class Shareholder {
  const Shareholder({required this.holderName, required this.shareCount, required this.ownershipRatio});

  final String holderName;
  final double shareCount;
  final double ownershipRatio;

  factory Shareholder.fromJson(Map<String, dynamic> json) => Shareholder(
    holderName: (json['holderName'] as String?) ?? '',
    shareCount: (json['shareCount'] as num?)?.toDouble() ?? 0,
    ownershipRatio: (json['ownershipRatio'] as num?)?.toDouble() ?? 0,
  );
}

class CompanyShareholders {
  const CompanyShareholders({required this.totalSharesIssued, required this.shareholders});

  final double totalSharesIssued;
  final List<Shareholder> shareholders;

  factory CompanyShareholders.fromJson(Map<String, dynamic> json) => CompanyShareholders(
    totalSharesIssued: (json['totalSharesIssued'] as num?)?.toDouble() ?? 0,
    shareholders: ((json['shareholders'] as List<dynamic>?) ?? const [])
        .map((e) => Shareholder.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

class OpenOrder {
  const OpenOrder({
    required this.id,
    required this.stockSymbol,
    required this.companyName,
    required this.side,
    required this.limitPrice,
    required this.remainingQuantity,
    required this.status,
  });

  final String id;
  final String stockSymbol;
  final String companyName;

  /// `BUY` or `SELL`.
  final String side;
  final double limitPrice;
  final double remainingQuantity;
  final String status;

  factory OpenOrder.fromJson(Map<String, dynamic> json) => OpenOrder(
    id: json['id'] as String,
    stockSymbol: (json['stockSymbol'] as String?) ?? '',
    companyName: (json['companyName'] as String?) ?? '',
    side: (json['side'] as String?) ?? 'BUY',
    limitPrice: (json['limitPrice'] as num?)?.toDouble() ?? 0,
    remainingQuantity: (json['remainingQuantity'] as num?)?.toDouble() ?? 0,
    status: (json['status'] as String?) ?? 'OPEN',
  );
}
