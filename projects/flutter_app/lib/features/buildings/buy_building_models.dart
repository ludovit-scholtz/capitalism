// Data models for the Buy Building screen, mirroring
// `projects/frontend/src/views/BuyBuildingView.vue` +
// `BuyBuildingSteps.vue`/`CityLotDetailPanel.vue` (the latter is the source
// for the POWER_PLANT subtype picker, which `BuyBuildingSteps.vue` itself
// doesn't have — see `buy_building_screen.dart`'s top-of-file comment).
// GraphQL field names verified against `Api/Types/Inputs.Building.cs`
// (`PurchaseLotInput`) and the `cityLots`/`purchaseLot` operations.

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

/// Parses a GraphQL field that may arrive as a comma-separated `String`
/// (`BuildingLot.SuitableTypes` on the backend is a plain `string`, not a
/// list type — see `Api/Data/Entities/BuildingLot.cs`) or, defensively, as a
/// real JSON list. Mirrors `onboarding_models.dart`'s `_stringList`.
List<String> _stringList(Object? raw) {
  if (raw is List) return raw.map((e) => e.toString()).toList();
  if (raw is String) {
    return raw.split(',').map((s) => s.trim()).where((s) => s.isNotEmpty).toList();
  }
  return const [];
}

/// A city as shown on the Buy Building screen's city-picker step —
/// includes [currencyCode] (needed for the BANK base-capital requirement
/// and rate display, added alongside that feature) on top of the bare
/// id/name previously kept.
class BuyBuildingCity {
  const BuyBuildingCity({required this.id, required this.name, required this.currencyCode});

  final String id;
  final String name;
  final String currencyCode;

  factory BuyBuildingCity.fromJson(Map<String, dynamic> json) => BuyBuildingCity(
    id: json['id'] as String,
    name: json['name'] as String,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
  );
}

/// One selectable media house channel type — mirrors the inline
/// `<option>` list in `BuyBuildingSteps.vue`/`CityLotDetailPanel.vue`
/// (icon, label, revenue multiplier).
class MediaHouseChannelType {
  const MediaHouseChannelType({required this.code, required this.icon, required this.label, required this.multiplierLabel});

  final String code;
  final String icon;
  final String label;
  final String multiplierLabel;
}

const List<MediaHouseChannelType> mediaHouseChannelTypes = [
  MediaHouseChannelType(code: 'NEWSPAPER', icon: '📰', label: 'Newspaper', multiplierLabel: '×1.0'),
  MediaHouseChannelType(code: 'RADIO', icon: '📻', label: 'Radio', multiplierLabel: '×1.5'),
  MediaHouseChannelType(code: 'TV', icon: '📺', label: 'TV', multiplierLabel: '×2.0'),
];

/// One selectable power plant subtype — mirrors `POWER_PLANT_TYPES` in
/// `CityLotDetailPanel.vue` (output capacity, renewable/fuel badge).
class PowerPlantTypeOption {
  const PowerPlantTypeOption({required this.code, required this.label, required this.outputMw, required this.isRenewable, required this.description});

  final String code;
  final String label;
  final int outputMw;
  final bool isRenewable;
  final String description;
}

const List<PowerPlantTypeOption> powerPlantTypeOptions = [
  PowerPlantTypeOption(
    code: 'COAL',
    label: 'Coal',
    outputMw: 50,
    isRenewable: false,
    description: 'High, steady output. Needs a continuous coal fuel supply.',
  ),
  PowerPlantTypeOption(
    code: 'GAS',
    label: 'Natural Gas',
    outputMw: 40,
    isRenewable: false,
    description: 'Fast-ramping output fuelled by natural gas.',
  ),
  PowerPlantTypeOption(
    code: 'SOLAR',
    label: 'Solar',
    outputMw: 20,
    isRenewable: true,
    description: 'No fuel cost, but output varies with sunlight.',
  ),
  PowerPlantTypeOption(
    code: 'WIND',
    label: 'Wind',
    outputMw: 25,
    isRenewable: true,
    description: 'No fuel cost, but output varies with wind conditions.',
  ),
  PowerPlantTypeOption(
    code: 'NUCLEAR',
    label: 'Nuclear',
    outputMw: 200,
    isRenewable: false,
    description: 'Very high, steady output at a high construction cost.',
  ),
];

/// The company bank capital a BANK building must hold before it can start
/// operating, denominated in the lot's city currency — mirrors
/// `bankBaseCapitalRequired` in `BuyBuildingSteps.vue`.
double bankBaseCapitalRequired(String currencyCode) {
  switch (currencyCode.toUpperCase()) {
    case 'CZK':
      return 240000000;
    case 'GBP':
      return 8600000;
    case 'CNY':
      return 72000000;
    case 'INR':
      return 835000000;
    default:
      return 10000000;
  }
}

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
    suitableTypes: _stringList(json['suitableTypes']),
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
