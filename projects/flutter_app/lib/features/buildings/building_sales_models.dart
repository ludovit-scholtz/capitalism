// Data models for PUBLIC_SALES-specific tools (ROADMAP 135), mirroring
// `projects/frontend/src/types/analytics.ts`'s `PublicSalesAnalytics` shape
// and the `publicSalesAnalytics` GraphQL query verified against
// `useBuildingDetail.ts`. Trimmed from the web (documented, not an
// oversight — same trim style as other panels in this app): the full
// `seasonalOutlook` quarter-forecast breakdown and `demandDrivers` list are
// not ported — this app surfaces the current demand signal/trend instead,
// which carries the same actionable information in less space. Revenue/
// price/profit history and market share are kept in full since those drive
// the panel's bar-chart visuals directly.

class SalesTickPoint {
  const SalesTickPoint({required this.tick, required this.revenue, required this.quantitySold});

  final int tick;
  final double revenue;
  final double quantitySold;

  factory SalesTickPoint.fromJson(Map<String, dynamic> json) => SalesTickPoint(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
    quantitySold: (json['quantitySold'] as num?)?.toDouble() ?? 0,
  );
}

class PriceTickPoint {
  const PriceTickPoint({required this.tick, required this.pricePerUnit});

  final int tick;
  final double pricePerUnit;

  factory PriceTickPoint.fromJson(Map<String, dynamic> json) =>
      PriceTickPoint(tick: (json['tick'] as num?)?.toInt() ?? 0, pricePerUnit: (json['pricePerUnit'] as num?)?.toDouble() ?? 0);
}

class ProfitTickPoint {
  const ProfitTickPoint({required this.tick, required this.profit});

  final int tick;
  final double profit;

  factory ProfitTickPoint.fromJson(Map<String, dynamic> json) =>
      ProfitTickPoint(tick: (json['tick'] as num?)?.toInt() ?? 0, profit: (json['profit'] as num?)?.toDouble() ?? 0);
}

class MarketShareEntry {
  const MarketShareEntry({required this.label, required this.share, required this.isUnmet});

  final String label;
  final double share;
  final bool isUnmet;

  factory MarketShareEntry.fromJson(Map<String, dynamic> json) => MarketShareEntry(
    label: (json['label'] as String?) ?? '',
    share: (json['share'] as num?)?.toDouble() ?? 0,
    isUnmet: json['isUnmet'] as bool? ?? false,
  );
}

class PublicSalesAnalytics {
  const PublicSalesAnalytics({
    required this.buildingUnitId,
    required this.productName,
    required this.totalRevenue,
    required this.totalProfit,
    required this.totalQuantitySold,
    required this.averagePricePerUnit,
    required this.currentSalesCapacity,
    required this.dataFromTick,
    required this.dataToTick,
    required this.demandSignal,
    required this.actionHint,
    required this.recentUtilization,
    required this.elasticityIndex,
    required this.trendDirection,
    required this.cityCurrencyCode,
    required this.cityMarketClearingPrice,
    required this.revenueHistory,
    required this.priceHistory,
    required this.profitHistory,
    required this.marketShare,
  });

  final String buildingUnitId;
  final String? productName;
  final double totalRevenue;
  final double totalProfit;
  final double totalQuantitySold;
  final double averagePricePerUnit;
  final double currentSalesCapacity;
  final int dataFromTick;
  final int dataToTick;
  final String? demandSignal;
  final String? actionHint;
  final double recentUtilization;
  final double elasticityIndex;
  final String? trendDirection;
  final String? cityCurrencyCode;
  final double? cityMarketClearingPrice;
  final List<SalesTickPoint> revenueHistory;
  final List<PriceTickPoint> priceHistory;
  final List<ProfitTickPoint> profitHistory;
  final List<MarketShareEntry> marketShare;

  factory PublicSalesAnalytics.fromJson(Map<String, dynamic> json) => PublicSalesAnalytics(
    buildingUnitId: json['buildingUnitId'] as String,
    productName: json['productName'] as String?,
    totalRevenue: (json['totalRevenue'] as num?)?.toDouble() ?? 0,
    totalProfit: (json['totalProfit'] as num?)?.toDouble() ?? 0,
    totalQuantitySold: (json['totalQuantitySold'] as num?)?.toDouble() ?? 0,
    averagePricePerUnit: (json['averagePricePerUnit'] as num?)?.toDouble() ?? 0,
    currentSalesCapacity: (json['currentSalesCapacity'] as num?)?.toDouble() ?? 0,
    dataFromTick: (json['dataFromTick'] as num?)?.toInt() ?? 0,
    dataToTick: (json['dataToTick'] as num?)?.toInt() ?? 0,
    demandSignal: json['demandSignal'] as String?,
    actionHint: json['actionHint'] as String?,
    recentUtilization: (json['recentUtilization'] as num?)?.toDouble() ?? 0,
    elasticityIndex: (json['elasticityIndex'] as num?)?.toDouble() ?? 0,
    trendDirection: json['trendDirection'] as String?,
    cityCurrencyCode: json['cityCurrencyCode'] as String?,
    cityMarketClearingPrice: (json['cityMarketClearingPrice'] as num?)?.toDouble(),
    revenueHistory: ((json['revenueHistory'] as List<dynamic>?) ?? const [])
        .map((e) => SalesTickPoint.fromJson(e as Map<String, dynamic>))
        .toList(),
    priceHistory: ((json['priceHistory'] as List<dynamic>?) ?? const [])
        .map((e) => PriceTickPoint.fromJson(e as Map<String, dynamic>))
        .toList(),
    profitHistory: ((json['profitHistory'] as List<dynamic>?) ?? const [])
        .map((e) => ProfitTickPoint.fromJson(e as Map<String, dynamic>))
        .toList(),
    marketShare: ((json['marketShare'] as List<dynamic>?) ?? const [])
        .map((e) => MarketShareEntry.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
