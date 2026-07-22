// Data models for the Building Market screen, mirroring
// `projects/frontend/src/views/BuildingMarketView.vue`. GraphQL field
// names verified against `Api/Types/Query.BuildingMarket.cs` /
// `Api/Types/Mutation.BuildingMarket.cs`.

class MarketBuildingCity {
  const MarketBuildingCity({required this.id, required this.name, required this.currencyCode});

  final String id;
  final String name;
  final String currencyCode;

  factory MarketBuildingCity.fromJson(Map<String, dynamic> json) => MarketBuildingCity(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
  );
}

class MarketBuildingCompany {
  const MarketBuildingCompany({required this.id, required this.name, required this.ownerDisplayName});

  final String id;
  final String name;
  final String? ownerDisplayName;

  factory MarketBuildingCompany.fromJson(Map<String, dynamic> json) {
    final player = json['player'] as Map<String, dynamic>?;
    return MarketBuildingCompany(
      id: json['id'] as String,
      name: (json['name'] as String?) ?? '',
      ownerDisplayName: player?['displayName'] as String?,
    );
  }
}

class MarketBuilding {
  const MarketBuilding({
    required this.id,
    required this.name,
    required this.type,
    required this.isForSale,
    required this.askingPrice,
    required this.level,
    required this.isCollateralized,
    required this.city,
    required this.company,
  });

  final String id;
  final String name;
  final String type;
  final bool isForSale;
  final double? askingPrice;
  final int level;
  final bool isCollateralized;
  final MarketBuildingCity city;
  final MarketBuildingCompany company;

  factory MarketBuilding.fromJson(Map<String, dynamic> json) => MarketBuilding(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    type: (json['type'] as String?) ?? '',
    isForSale: json['isForSale'] as bool? ?? false,
    askingPrice: (json['askingPrice'] as num?)?.toDouble(),
    level: (json['level'] as num?)?.toInt() ?? 1,
    isCollateralized: json['isCollateralized'] as bool? ?? false,
    city: MarketBuildingCity.fromJson((json['city'] as Map<String, dynamic>?) ?? const {}),
    company: MarketBuildingCompany.fromJson((json['company'] as Map<String, dynamic>?) ?? const {}),
  );
}

class BuildingOffer {
  const BuildingOffer({
    required this.id,
    required this.offerVersion,
    required this.offeredPrice,
    required this.status,
    required this.buyerCompanyName,
    required this.buyerDisplayName,
  });

  final String id;
  final int offerVersion;
  final double offeredPrice;

  /// `PENDING`, `ACCEPTED`, `REJECTED`, or `CANCELLED`.
  final String status;
  final String? buyerCompanyName;
  final String? buyerDisplayName;

  factory BuildingOffer.fromJson(Map<String, dynamic> json) {
    final buyerCompany = json['buyerCompany'] as Map<String, dynamic>?;
    final buyerPlayer = json['buyerPlayer'] as Map<String, dynamic>?;
    return BuildingOffer(
      id: json['id'] as String,
      offerVersion: (json['offerVersion'] as num?)?.toInt() ?? 0,
      offeredPrice: (json['offeredPrice'] as num?)?.toDouble() ?? 0,
      status: (json['status'] as String?) ?? 'PENDING',
      buyerCompanyName: buyerCompany?['name'] as String?,
      buyerDisplayName: buyerPlayer?['displayName'] as String?,
    );
  }
}

class MyBuildingListing {
  const MyBuildingListing({required this.building, required this.offers});

  final MarketBuilding building;
  final List<BuildingOffer> offers;

  factory MyBuildingListing.fromJson(Map<String, dynamic> json) => MyBuildingListing(
    building: MarketBuilding.fromJson(json['building'] as Map<String, dynamic>),
    offers: ((json['offers'] as List<dynamic>?) ?? const [])
        .map((e) => BuildingOffer.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
