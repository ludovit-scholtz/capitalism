// Data models for the Global Exchange screen, mirroring
// `projects/frontend/src/views/GlobalExchangeView.vue`. GraphQL field
// names verified against `Api/Types/Inputs.cs` (`BuyFromExchangeInput`).

class GlobalExchangeOffer {
  const GlobalExchangeOffer({
    required this.cityId,
    required this.cityName,
    required this.resourceTypeId,
    required this.resourceName,
    required this.unitSymbol,
    required this.exchangePricePerUnit,
    required this.deliveredPricePerUnit,
    required this.estimatedQuality,
  });

  final String cityId;
  final String cityName;
  final String resourceTypeId;
  final String resourceName;
  final String? unitSymbol;
  final double exchangePricePerUnit;
  final double deliveredPricePerUnit;
  final double estimatedQuality;

  factory GlobalExchangeOffer.fromJson(Map<String, dynamic> json) => GlobalExchangeOffer(
    cityId: json['cityId'] as String,
    cityName: (json['cityName'] as String?) ?? '',
    resourceTypeId: json['resourceTypeId'] as String,
    resourceName: (json['resourceName'] as String?) ?? '',
    unitSymbol: json['unitSymbol'] as String?,
    exchangePricePerUnit: (json['exchangePricePerUnit'] as num?)?.toDouble() ?? 0,
    deliveredPricePerUnit: (json['deliveredPricePerUnit'] as num?)?.toDouble() ?? 0,
    estimatedQuality: (json['estimatedQuality'] as num?)?.toDouble() ?? 0,
  );
}

class GlobalExchangeProductListing {
  const GlobalExchangeProductListing({
    required this.orderId,
    required this.productTypeId,
    required this.productName,
    required this.productIndustry,
    required this.unitSymbol,
    required this.pricePerUnit,
    required this.remainingQuantity,
    required this.sellerCityName,
    required this.sellerCompanyName,
  });

  final String orderId;
  final String productTypeId;
  final String productName;
  final String productIndustry;
  final String? unitSymbol;
  final double pricePerUnit;
  final double remainingQuantity;
  final String sellerCityName;
  final String sellerCompanyName;

  factory GlobalExchangeProductListing.fromJson(Map<String, dynamic> json) => GlobalExchangeProductListing(
    orderId: json['orderId'] as String,
    productTypeId: (json['productTypeId'] as String?) ?? '',
    productName: (json['productName'] as String?) ?? '',
    productIndustry: (json['productIndustry'] as String?) ?? '',
    unitSymbol: json['unitSymbol'] as String?,
    pricePerUnit: (json['pricePerUnit'] as num?)?.toDouble() ?? 0,
    remainingQuantity: (json['remainingQuantity'] as num?)?.toDouble() ?? 0,
    sellerCityName: (json['sellerCityName'] as String?) ?? '',
    sellerCompanyName: (json['sellerCompanyName'] as String?) ?? '',
  );
}

/// Minimal projection of `resourceTypes`/`productTypes`, used only to build
/// the category/industry filter dropdowns — mirrors
/// `GlobalExchangeView.vue`'s `RESOURCES_QUERY`/`PRODUCTS_QUERY` usage.
class ExchangeCatalogEntry {
  const ExchangeCatalogEntry({required this.id, required this.name, required this.category});

  final String id;
  final String name;
  final String category;

  factory ExchangeCatalogEntry.fromResourceJson(Map<String, dynamic> json) => ExchangeCatalogEntry(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    category: (json['category'] as String?) ?? '',
  );

  factory ExchangeCatalogEntry.fromProductJson(Map<String, dynamic> json) => ExchangeCatalogEntry(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    category: (json['industry'] as String?) ?? '',
  );
}

class ExchangeTargetUnit {
  const ExchangeTargetUnit({required this.id, required this.buildingName, required this.unitType});

  final String id;
  final String buildingName;
  final String unitType;
}
