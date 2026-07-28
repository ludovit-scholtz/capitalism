// Data models for the Market Intelligence, Market Dashboard, Energy
// Market, Global Events, and Marketing Analytics screens, mirroring
// `projects/frontend/src/views/MarketIntelligenceView.vue`/
// `MarketDashboardView.vue`/`EnergyMarketView.vue`/`GlobalEventsPanel.vue`/
// `MarketingAnalyticsView.vue`. GraphQL field names verified against
// `Api/Types/Query.Analytics.MarketIntelligence.cs`, `Query.Market.cs`,
// `Query.EnergyMarket.cs`, `Query.GlobalEvents.cs`,
// `Query.Analytics.Buildings.cs`.

class MarketIntelSeller {
  const MarketIntelSeller({required this.rank, required this.displayName, required this.askingPricePerUnit, required this.marketShare});

  final int rank;
  final String displayName;
  final double askingPricePerUnit;
  final double marketShare;

  factory MarketIntelSeller.fromJson(Map<String, dynamic> json) => MarketIntelSeller(
    rank: (json['rank'] as num?)?.toInt() ?? 0,
    displayName: (json['displayName'] as String?) ?? '',
    askingPricePerUnit: (json['askingPricePerUnit'] as num?)?.toDouble() ?? 0,
    marketShare: (json['marketShare'] as num?)?.toDouble() ?? 0,
  );
}

class MarketIntelProduct {
  const MarketIntelProduct({required this.productName, required this.totalWeeklySalesVolume, required this.sellers});

  final String productName;
  final double totalWeeklySalesVolume;
  final List<MarketIntelSeller> sellers;

  factory MarketIntelProduct.fromJson(Map<String, dynamic> json) => MarketIntelProduct(
    productName: (json['productName'] as String?) ?? '',
    totalWeeklySalesVolume: (json['totalWeeklySalesVolume'] as num?)?.toDouble() ?? 0,
    sellers: ((json['sellers'] as List<dynamic>?) ?? const []).map((e) => MarketIntelSeller.fromJson(e as Map<String, dynamic>)).toList(),
  );
}

class MarketIntelligence {
  const MarketIntelligence({required this.cityName, required this.products});

  final String cityName;
  final List<MarketIntelProduct> products;

  factory MarketIntelligence.fromJson(Map<String, dynamic> json) => MarketIntelligence(
    cityName: (json['cityName'] as String?) ?? '',
    products: ((json['products'] as List<dynamic>?) ?? const []).map((e) => MarketIntelProduct.fromJson(e as Map<String, dynamic>)).toList(),
  );
}

class MarketOverviewProduct {
  const MarketOverviewProduct({
    required this.productTypeId,
    required this.productName,
    required this.totalDemand,
    required this.totalQuantitySold,
    required this.satisfactionRate,
    required this.averageClearingPrice,
    required this.sellerCount,
  });

  final String productTypeId;
  final String productName;
  final double totalDemand;
  final double totalQuantitySold;
  final double satisfactionRate;
  final double averageClearingPrice;
  final int sellerCount;

  factory MarketOverviewProduct.fromJson(Map<String, dynamic> json) => MarketOverviewProduct(
    productTypeId: json['productTypeId'] as String,
    productName: (json['productName'] as String?) ?? '',
    totalDemand: (json['totalDemand'] as num?)?.toDouble() ?? 0,
    totalQuantitySold: (json['totalQuantitySold'] as num?)?.toDouble() ?? 0,
    satisfactionRate: (json['satisfactionRate'] as num?)?.toDouble() ?? 0,
    averageClearingPrice: (json['averageClearingPrice'] as num?)?.toDouble() ?? 0,
    sellerCount: (json['sellerCount'] as num?)?.toInt() ?? 0,
  );
}

class MarketOverview {
  const MarketOverview({required this.cityId, required this.cityName, required this.products});

  final String cityId;
  final String cityName;
  final List<MarketOverviewProduct> products;

  factory MarketOverview.fromJson(Map<String, dynamic> json) => MarketOverview(
    cityId: json['cityId'] as String,
    cityName: (json['cityName'] as String?) ?? '',
    products: ((json['products'] as List<dynamic>?) ?? const []).map((e) => MarketOverviewProduct.fromJson(e as Map<String, dynamic>)).toList(),
  );
}

class MarketPriceHistoryPoint {
  const MarketPriceHistoryPoint({
    required this.tick,
    required this.clearingPrice,
    required this.totalVolume,
    required this.totalRevenue,
    required this.sellerCount,
  });

  final int tick;
  final double clearingPrice;
  final double totalVolume;
  final double totalRevenue;
  final int sellerCount;

  factory MarketPriceHistoryPoint.fromJson(Map<String, dynamic> json) => MarketPriceHistoryPoint(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    clearingPrice: (json['clearingPrice'] as num?)?.toDouble() ?? 0,
    totalVolume: (json['totalVolume'] as num?)?.toDouble() ?? 0,
    totalRevenue: (json['totalRevenue'] as num?)?.toDouble() ?? 0,
    sellerCount: (json['sellerCount'] as num?)?.toInt() ?? 0,
  );
}

class CompetitorQuality {
  const CompetitorQuality({required this.companyName, required this.qualityLevel, required this.pricePremiumPct, required this.isOwnCompany});

  final String companyName;
  final double qualityLevel;
  final double pricePremiumPct;
  final bool isOwnCompany;

  factory CompetitorQuality.fromJson(Map<String, dynamic> json) => CompetitorQuality(
    companyName: (json['companyName'] as String?) ?? '',
    qualityLevel: (json['qualityLevel'] as num?)?.toDouble() ?? 0,
    pricePremiumPct: (json['pricePremiumPct'] as num?)?.toDouble() ?? 0,
    isOwnCompany: json['isOwnCompany'] as bool? ?? false,
  );
}

class EnergyListing {
  const EnergyListing({
    required this.listingId,
    required this.buildingId,
    required this.buildingName,
    required this.companyId,
    required this.companyName,
    required this.cityId,
    required this.plantType,
    required this.pricePerKwhLocal,
    required this.capacityKw,
    required this.availableKw,
  });

  final String listingId;
  final String buildingId;
  final String buildingName;
  final String companyId;
  final String? companyName;
  final String cityId;
  final String? plantType;
  final double pricePerKwhLocal;
  final double capacityKw;
  final double availableKw;

  factory EnergyListing.fromJson(Map<String, dynamic> json) => EnergyListing(
    listingId: json['listingId'] as String,
    buildingId: json['buildingId'] as String,
    buildingName: (json['buildingName'] as String?) ?? '',
    companyId: json['companyId'] as String,
    companyName: json['companyName'] as String?,
    cityId: json['cityId'] as String,
    plantType: json['plantType'] as String?,
    pricePerKwhLocal: (json['pricePerKwhLocal'] as num?)?.toDouble() ?? 0,
    capacityKw: (json['capacityKw'] as num?)?.toDouble() ?? 0,
    availableKw: (json['availableKw'] as num?)?.toDouble() ?? 0,
  );
}

class GlobalEvent {
  const GlobalEvent({
    required this.id,
    required this.eventType,
    required this.severity,
    required this.title,
    required this.description,
    required this.isActive,
    required this.operatingCostMultiplier,
    required this.tradeRouteMultiplier,
  });

  final String id;
  final String eventType;
  final String severity;
  final String title;
  final String? description;
  final bool isActive;
  final double? operatingCostMultiplier;
  final double? tradeRouteMultiplier;

  factory GlobalEvent.fromJson(Map<String, dynamic> json) => GlobalEvent(
    id: json['id'] as String,
    eventType: (json['eventType'] as String?) ?? '',
    severity: (json['severity'] as String?) ?? 'LOW',
    title: (json['title'] as String?) ?? '',
    description: json['description'] as String?,
    isActive: json['isActive'] as bool? ?? false,
    operatingCostMultiplier: (json['operatingCostMultiplier'] as num?)?.toDouble(),
    tradeRouteMultiplier: (json['tradeRouteMultiplier'] as num?)?.toDouble(),
  );
}

class CampaignRow {
  const CampaignRow({
    required this.buildingName,
    required this.productName,
    required this.cityName,
    required this.brandAwareness,
    required this.brandQuality,
    required this.revenueLastTicks,
    required this.recommendation,
  });

  final String buildingName;
  final String productName;
  final String cityName;
  final double brandAwareness;
  final double brandQuality;
  final double revenueLastTicks;
  final String? recommendation;

  factory CampaignRow.fromJson(Map<String, dynamic> json) => CampaignRow(
    buildingName: (json['buildingName'] as String?) ?? '',
    productName: (json['productName'] as String?) ?? '',
    cityName: (json['cityName'] as String?) ?? '',
    brandAwareness: (json['brandAwareness'] as num?)?.toDouble() ?? 0,
    brandQuality: (json['brandQuality'] as num?)?.toDouble() ?? 0,
    revenueLastTicks: (json['revenueLastTicks'] as num?)?.toDouble() ?? 0,
    recommendation: json['recommendation'] as String?,
  );
}

class CampaignAnalytics {
  const CampaignAnalytics({
    required this.totalRevenue,
    required this.totalMarketingSpend,
    required this.bestPerformingCity,
    required this.bestPerformingProduct,
    required this.globalRecommendation,
    required this.rows,
  });

  final double totalRevenue;
  final double totalMarketingSpend;
  final String? bestPerformingCity;
  final String? bestPerformingProduct;
  final String? globalRecommendation;
  final List<CampaignRow> rows;

  factory CampaignAnalytics.fromJson(Map<String, dynamic> json) => CampaignAnalytics(
    totalRevenue: (json['totalRevenue'] as num?)?.toDouble() ?? 0,
    totalMarketingSpend: (json['totalMarketingSpend'] as num?)?.toDouble() ?? 0,
    bestPerformingCity: json['bestPerformingCity'] as String?,
    bestPerformingProduct: json['bestPerformingProduct'] as String?,
    globalRecommendation: json['globalRecommendation'] as String?,
    rows: ((json['rows'] as List<dynamic>?) ?? const []).map((e) => CampaignRow.fromJson(e as Map<String, dynamic>)).toList(),
  );
}
