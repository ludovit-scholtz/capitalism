// Data models for the six City tabs, mirroring
// `projects/frontend/src/components/cityTabs/*.vue`. GraphQL field names
// verified against `Api/Types/GovernmentContractTypes.cs` and
// `Api/Types/Query.Npc.cs` (`cityCompetitors`).

class GovernmentContractCard {
  const GovernmentContractCard({
    required this.id,
    required this.cityId,
    required this.currencyCode,
    required this.title,
    required this.description,
    required this.productName,
    required this.quantityRequired,
    required this.minimumQuality,
    required this.budgetCap,
    required this.deadlineTick,
    required this.status,
    required this.bidCount,
  });

  final String id;
  final String cityId;
  final String currencyCode;
  final String title;
  final String description;
  final String productName;
  final double quantityRequired;
  final double minimumQuality;
  final double budgetCap;
  final int deadlineTick;

  /// `OPEN`, `AWARDED`, `FULFILLED`, or `EXPIRED`.
  final String status;
  final int bidCount;

  factory GovernmentContractCard.fromJson(Map<String, dynamic> json) => GovernmentContractCard(
    id: json['id'] as String,
    cityId: json['cityId'] as String,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    title: (json['title'] as String?) ?? '',
    description: (json['description'] as String?) ?? '',
    productName: (json['productName'] as String?) ?? '',
    quantityRequired: (json['quantityRequired'] as num?)?.toDouble() ?? 0,
    minimumQuality: (json['minimumQuality'] as num?)?.toDouble() ?? 0,
    budgetCap: (json['budgetCap'] as num?)?.toDouble() ?? 0,
    deadlineTick: (json['deadlineTick'] as num?)?.toInt() ?? 0,
    status: (json['status'] as String?) ?? 'OPEN',
    bidCount: (json['bidCount'] as num?)?.toInt() ?? 0,
  );
}

class ContractEligibility {
  const ContractEligibility({required this.isEligible, required this.reasonMessage});

  final bool isEligible;
  final String? reasonMessage;

  factory ContractEligibility.fromJson(Map<String, dynamic> json) => ContractEligibility(
    isEligible: json['isEligible'] as bool? ?? false,
    reasonMessage: json['reasonMessage'] as String?,
  );
}

class CompetitorShare {
  const CompetitorShare({required this.category, required this.sharePercent});

  final String category;
  final double sharePercent;

  factory CompetitorShare.fromJson(Map<String, dynamic> json) => CompetitorShare(
    category: (json['category'] as String?) ?? '',
    sharePercent: (json['sharePercent'] as num?)?.toDouble() ?? 0,
  );
}

class CityCompetitor {
  const CityCompetitor({
    required this.companyId,
    required this.companyName,
    required this.isNpc,
    required this.buildingCount,
    required this.estimatedRevenueLastTicks,
    required this.marketSharePercent,
    required this.trend,
    required this.marketShareByCategory,
  });

  final String companyId;
  final String companyName;
  final bool isNpc;
  final int buildingCount;
  final double estimatedRevenueLastTicks;
  final double marketSharePercent;

  /// `UP`, `DOWN`, or `FLAT`.
  final String trend;
  final List<CompetitorShare> marketShareByCategory;

  factory CityCompetitor.fromJson(Map<String, dynamic> json) => CityCompetitor(
    companyId: json['companyId'] as String,
    companyName: (json['companyName'] as String?) ?? '',
    isNpc: json['isNpc'] as bool? ?? false,
    buildingCount: (json['buildingCount'] as num?)?.toInt() ?? 0,
    estimatedRevenueLastTicks: (json['estimatedRevenueLastTicks'] as num?)?.toDouble() ?? 0,
    marketSharePercent: (json['marketSharePercent'] as num?)?.toDouble() ?? 0,
    trend: (json['trend'] as String?) ?? 'FLAT',
    marketShareByCategory: ((json['marketShareByCategory'] as List<dynamic>?) ?? const [])
        .map((e) => CompetitorShare.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
