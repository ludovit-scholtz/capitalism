// Data models for the Buy Building screen, mirroring
// `projects/frontend/src/views/BuyBuildingView.vue`. GraphQL field names
// verified against `Api/Types/Inputs.Building.cs` (`PurchaseLotInput`) and
// the `cityLots`/`purchaseLot` operations.

const List<String> buildingTypes = [
  'MINE',
  'FACTORY',
  'SALES_SHOP',
  'RESEARCH_DEVELOPMENT',
  'APARTMENT',
  'COMMERCIAL',
  'MEDIA_HOUSE',
  'BANK',
  'EXCHANGE',
  'POWER_PLANT',
];

class CityLot {
  const CityLot({
    required this.id,
    required this.name,
    required this.district,
    required this.price,
    required this.suitableTypes,
    required this.ownerCompanyId,
  });

  final String id;
  final String? name;
  final String? district;
  final double price;
  final List<String> suitableTypes;
  final String? ownerCompanyId;

  bool get isAvailable => ownerCompanyId == null;

  factory CityLot.fromJson(Map<String, dynamic> json) => CityLot(
    id: json['id'] as String,
    name: json['name'] as String?,
    district: json['district'] as String?,
    price: (json['price'] as num?)?.toDouble() ?? 0,
    suitableTypes: ((json['suitableTypes'] as List<dynamic>?) ?? const []).cast<String>(),
    ownerCompanyId: json['ownerCompanyId'] as String?,
  );
}
