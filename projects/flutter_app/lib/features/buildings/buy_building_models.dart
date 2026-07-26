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
    required this.buildingId,
    this.latitude = 0,
    this.longitude = 0,
    this.populationIndex = 0,
    this.basePrice = 0,
    this.description,
  });

  final String id;
  final String? name;
  final String? district;
  final double price;
  final List<String> suitableTypes;
  final String? ownerCompanyId;
  final String? buildingId;

  /// Fetched by the `cityLots` query but previously discarded here — needed
  /// for the interactive map (marker position) and distance calculations.
  final double latitude;
  final double longitude;
  final double populationIndex;
  final double basePrice;
  final String? description;

  bool get isAvailable => ownerCompanyId == null;

  factory CityLot.fromJson(Map<String, dynamic> json) => CityLot(
    id: json['id'] as String,
    name: json['name'] as String?,
    district: json['district'] as String?,
    price: (json['price'] as num?)?.toDouble() ?? 0,
    suitableTypes: ((json['suitableTypes'] as List<dynamic>?) ?? const []).cast<String>(),
    ownerCompanyId: json['ownerCompanyId'] as String?,
    buildingId: json['buildingId'] as String?,
    latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
    longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
    populationIndex: (json['populationIndex'] as num?)?.toDouble() ?? 0,
    basePrice: (json['basePrice'] as num?)?.toDouble() ?? 0,
    description: json['description'] as String?,
  );
}

/// One of the player's own buildings' map coordinates, used to compute
/// distance-to-existing-buildings on the Buy Building lot map. Mirrors the
/// `latitude`/`longitude` fields already present on the `Building` entity
/// (`Api/Data/Entities/Building.cs`).
class OwnedBuildingLocation {
  const OwnedBuildingLocation({
    required this.id,
    required this.name,
    required this.type,
    required this.cityId,
    required this.latitude,
    required this.longitude,
  });

  final String id;
  final String name;
  final String type;
  final String cityId;
  final double latitude;
  final double longitude;

  factory OwnedBuildingLocation.fromJson(Map<String, dynamic> json) => OwnedBuildingLocation(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    type: (json['type'] as String?) ?? '',
    cityId: (json['cityId'] as String?) ?? '',
    latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
    longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
  );
}
