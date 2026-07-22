// Data models for the Sell Building screen, mirroring
// `projects/frontend/src/views/SellBuildingView.vue`. GraphQL field names
// verified against `Api/Types/Inputs.Building.cs`
// (`SetBuildingForSaleInput`/`DestroyBuildingInput`).

class BuildingMarketValuation {
  const BuildingMarketValuation({required this.totalValue, required this.minimumSalePrice, required this.currencyCode});

  final double totalValue;
  final double minimumSalePrice;
  final String currencyCode;

  factory BuildingMarketValuation.fromJson(Map<String, dynamic> json) => BuildingMarketValuation(
    totalValue: (json['totalValue'] as num?)?.toDouble() ?? 0,
    minimumSalePrice: (json['minimumSalePrice'] as num?)?.toDouble() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
  );
}

class SellableBuilding {
  const SellableBuilding({
    required this.id,
    required this.name,
    required this.type,
    required this.level,
    required this.isForSale,
    required this.askingPrice,
    required this.isCollateralized,
    required this.marketValuation,
  });

  final String id;
  final String name;
  final String type;
  final int level;
  final bool isForSale;
  final double? askingPrice;
  final bool isCollateralized;
  final BuildingMarketValuation? marketValuation;

  factory SellableBuilding.fromJson(Map<String, dynamic> json) => SellableBuilding(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    type: (json['type'] as String?) ?? '',
    level: (json['level'] as num?)?.toInt() ?? 1,
    isForSale: json['isForSale'] as bool? ?? false,
    askingPrice: (json['askingPrice'] as num?)?.toDouble(),
    isCollateralized: json['isCollateralized'] as bool? ?? false,
    marketValuation: json['marketValuation'] == null
        ? null
        : BuildingMarketValuation.fromJson(json['marketValuation'] as Map<String, dynamic>),
  );
}
