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
    required this.productName,
    required this.unitSymbol,
    required this.pricePerUnit,
    required this.remainingQuantity,
    required this.sellerCityName,
    required this.sellerCompanyName,
  });

  final String orderId;
  final String productName;
  final String? unitSymbol;
  final double pricePerUnit;
  final double remainingQuantity;
  final String sellerCityName;
  final String sellerCompanyName;

  factory GlobalExchangeProductListing.fromJson(Map<String, dynamic> json) => GlobalExchangeProductListing(
    orderId: json['orderId'] as String,
    productName: (json['productName'] as String?) ?? '',
    unitSymbol: json['unitSymbol'] as String?,
    pricePerUnit: (json['pricePerUnit'] as num?)?.toDouble() ?? 0,
    remainingQuantity: (json['remainingQuantity'] as num?)?.toDouble() ?? 0,
    sellerCityName: (json['sellerCityName'] as String?) ?? '',
    sellerCompanyName: (json['sellerCompanyName'] as String?) ?? '',
  );
}

class ExchangeTargetUnit {
  const ExchangeTargetUnit({required this.id, required this.buildingName, required this.unitType});

  final String id;
  final String buildingName;
  final String unitType;
}
