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
    this.bidPrice = 0,
    this.askPrice = 0,
    this.canProposeDividend = false,
    this.canClaimControl = false,
    this.canMerge = false,
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

  /// The inline trade panel buys at [askPrice] and sells at [bidPrice] —
  /// matches web's `StockMarketListingRow.vue` (`buyAt`/`sellAt` labels).
  final double bidPrice;
  final double askPrice;

  /// Combined ownership (own + controlled-company shares) `> 50%` or the
  /// caller already runs the company — matches `canProposeDividend` on
  /// `StockExchangeListingResult` (`Api/Types/AccountExchangeTypes.cs`).
  final bool canProposeDividend;

  /// Combined ownership `>= 50%` and the caller doesn't already run the
  /// company — enables the CEO-replacement ("claim control") action.
  final bool canClaimControl;

  /// Combined ownership `>= 90%` and the caller doesn't already run the
  /// company — enables the merge-into-my-company action.
  final bool canMerge;

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
    bidPrice: (json['bidPrice'] as num?)?.toDouble() ?? 0,
    askPrice: (json['askPrice'] as num?)?.toDouble() ?? 0,
    canProposeDividend: json['canProposeDividend'] as bool? ?? false,
    canClaimControl: json['canClaimControl'] as bool? ?? false,
    canMerge: json['canMerge'] as bool? ?? false,
  );
}

class StockPriceHistoryPoint {
  const StockPriceHistoryPoint({required this.tick, required this.price});

  final int tick;
  final double price;

  factory StockPriceHistoryPoint.fromJson(Map<String, dynamic> json) => StockPriceHistoryPoint(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    price: (json['price'] as num?)?.toDouble() ?? 0,
  );
}

/// Mirrors `DividendProposalResult` (`Api/Types/StockDividendGovernanceTypes.cs`).
class DividendProposal {
  const DividendProposal({
    required this.id,
    required this.stockSymbol,
    required this.dividendPerShare,
    required this.totalPayout,
    required this.status,
    required this.ticksRemaining,
    required this.forVotes,
    required this.againstVotes,
    required this.myVoteChoice,
  });

  final String id;
  final String stockSymbol;
  final double dividendPerShare;
  final double totalPayout;

  /// `PENDING` | `VOTING` | `APPROVED` | `REJECTED` | `SETTLED` | `CANCELLED`.
  final String status;
  final int ticksRemaining;
  final double forVotes;
  final double againstVotes;

  /// `FOR` | `AGAINST` | `null` (not yet voted).
  final String? myVoteChoice;

  bool get isOpenForVoting => status == 'VOTING' && ticksRemaining > 0;

  factory DividendProposal.fromJson(Map<String, dynamic> json) => DividendProposal(
    id: json['id'] as String,
    stockSymbol: (json['stockSymbol'] as String?) ?? '',
    dividendPerShare: (json['dividendPerShare'] as num?)?.toDouble() ?? 0,
    totalPayout: (json['totalPayout'] as num?)?.toDouble() ?? 0,
    status: (json['status'] as String?) ?? '',
    ticksRemaining: (json['ticksRemaining'] as num?)?.toInt() ?? 0,
    forVotes: (json['forVotes'] as num?)?.toDouble() ?? 0,
    againstVotes: (json['againstVotes'] as num?)?.toDouble() ?? 0,
    myVoteChoice: json['myVoteChoice'] as String?,
  );
}

class PersonTradeRecord {
  const PersonTradeRecord({
    required this.companyId,
    required this.direction,
    required this.shareCount,
    required this.pricePerShare,
    required this.recordedAtTick,
  });

  final String companyId;

  /// `BUY` or `SELL`.
  final String direction;
  final double shareCount;
  final double pricePerShare;
  final int recordedAtTick;

  factory PersonTradeRecord.fromJson(Map<String, dynamic> json) => PersonTradeRecord(
    companyId: json['companyId'] as String,
    direction: (json['direction'] as String?) ?? 'BUY',
    shareCount: (json['shareCount'] as num?)?.toDouble() ?? 0,
    pricePerShare: (json['pricePerShare'] as num?)?.toDouble() ?? 0,
    recordedAtTick: (json['recordedAtTick'] as num?)?.toInt() ?? 0,
  );
}

class PortfolioHolding {
  const PortfolioHolding({required this.companyId, required this.shareCount, required this.marketValue});

  final String companyId;
  final double shareCount;
  final double marketValue;

  factory PortfolioHolding.fromJson(Map<String, dynamic> json) => PortfolioHolding(
    companyId: json['companyId'] as String,
    shareCount: (json['shareCount'] as num?)?.toDouble() ?? 0,
    marketValue: (json['marketValue'] as num?)?.toDouble() ?? 0,
  );
}

/// Slice of `PersonAccountResult` (`Api/Types/Query.Auth.cs`) needed to
/// compute the stock-trading position panel (owned shares, average buy
/// price, unrealized P&L, available cash) — see [computeStockPositionSummary].
class PersonAccountStockSummary {
  const PersonAccountStockSummary({
    required this.playerId,
    required this.availableCash,
    required this.shareholdings,
    required this.stockTrades,
  });

  final String playerId;
  final double availableCash;
  final List<PortfolioHolding> shareholdings;
  final List<PersonTradeRecord> stockTrades;

  factory PersonAccountStockSummary.fromJson(Map<String, dynamic> json) => PersonAccountStockSummary(
    playerId: json['playerId'] as String,
    availableCash: (json['availableCash'] as num?)?.toDouble() ?? 0,
    shareholdings: ((json['shareholdings'] as List<dynamic>?) ?? const [])
        .map((e) => PortfolioHolding.fromJson(e as Map<String, dynamic>))
        .toList(),
    stockTrades: ((json['stockTrades'] as List<dynamic>?) ?? const [])
        .map((e) => PersonTradeRecord.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// Client-computed stock position summary for one company — the backend has
/// no `averageBuyPrice`/`unrealizedPnl` field; this mirrors the moving-
/// average-cost reducer in `projects/frontend/src/lib/stockTrading.ts`
/// (`computeStockPositionSummary`).
class StockPositionSummary {
  const StockPositionSummary({
    required this.sharesOwned,
    required this.marketValue,
    required this.averageBuyPrice,
    required this.unrealizedPnl,
  });

  final double sharesOwned;
  final double marketValue;

  /// `null` when there's no trade history to derive a cost basis from.
  final double? averageBuyPrice;

  /// `null` when [averageBuyPrice] is unavailable or no shares are owned.
  final double? unrealizedPnl;

  factory StockPositionSummary.compute({
    required String companyId,
    required double currentSharePrice,
    required List<PortfolioHolding> shareholdings,
    required List<PersonTradeRecord> stockTrades,
  }) {
    final holding = shareholdings.where((h) => h.companyId == companyId).firstOrNull;
    final sharesOwned = holding?.shareCount ?? 0;
    final marketValue = holding?.marketValue ?? (sharesOwned * currentSharePrice);

    final trades = stockTrades.where((t) => t.companyId == companyId).toList()
      ..sort((a, b) => a.recordedAtTick.compareTo(b.recordedAtTick));

    var trackedShares = 0.0;
    var trackedCost = 0.0;
    for (final trade in trades) {
      if (trade.direction == 'BUY') {
        trackedShares += trade.shareCount;
        trackedCost += trade.shareCount * trade.pricePerShare;
      } else {
        final soldShares = trade.shareCount < trackedShares ? trade.shareCount : trackedShares;
        if (trackedShares > 0) {
          final avgCost = trackedCost / trackedShares;
          trackedCost -= avgCost * soldShares;
          trackedShares -= soldShares;
        }
      }
    }

    final averageBuyPrice = trackedShares > 0 ? trackedCost / trackedShares : null;
    final unrealizedPnl = (sharesOwned > 0 && averageBuyPrice != null)
        ? (currentSharePrice - averageBuyPrice) * sharesOwned
        : null;

    return StockPositionSummary(
      sharesOwned: sharesOwned,
      marketValue: marketValue,
      averageBuyPrice: averageBuyPrice,
      unrealizedPnl: unrealizedPnl,
    );
  }
}

extension StockFirstOrNull<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
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
