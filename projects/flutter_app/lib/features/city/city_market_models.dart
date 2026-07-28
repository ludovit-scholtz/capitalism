// Models for the City Market tab's demand panel, ported from
// `projects/frontend/src/types/analytics.ts` (`CityDemandSummaryResult`,
// `ProductDemandEntry`).

class ProductDemandEntry {
  const ProductDemandEntry({
    required this.productTypeId,
    required this.productName,
    required this.industry,
    required this.totalDemand,
    required this.totalQuantitySold,
    required this.satisfactionRate,
    required this.averageClearingPrice,
    required this.sellerCount,
  });

  final String productTypeId;
  final String productName;
  final String industry;
  final double totalDemand;
  final double totalQuantitySold;
  final double satisfactionRate;
  final double averageClearingPrice;
  final int sellerCount;

  factory ProductDemandEntry.fromJson(Map<String, dynamic> json) => ProductDemandEntry(
    productTypeId: json['productTypeId'] as String,
    productName: (json['productName'] as String?) ?? '',
    industry: (json['industry'] as String?) ?? '',
    totalDemand: (json['totalDemand'] as num?)?.toDouble() ?? 0,
    totalQuantitySold: (json['totalQuantitySold'] as num?)?.toDouble() ?? 0,
    satisfactionRate: (json['satisfactionRate'] as num?)?.toDouble() ?? 0,
    averageClearingPrice: (json['averageClearingPrice'] as num?)?.toDouble() ?? 0,
    sellerCount: (json['sellerCount'] as num?)?.toInt() ?? 0,
  );
}

class CityDemandSummary {
  const CityDemandSummary({
    required this.cityId,
    required this.cityName,
    required this.currencyCode,
    required this.products,
  });

  final String cityId;
  final String cityName;
  final String currencyCode;
  final List<ProductDemandEntry> products;

  factory CityDemandSummary.fromJson(Map<String, dynamic> json) => CityDemandSummary(
    cityId: json['cityId'] as String,
    cityName: (json['cityName'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    products: ((json['products'] as List<dynamic>?) ?? const [])
        .map((e) => ProductDemandEntry.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
