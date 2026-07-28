// Models for the Ledger screen's drill-down, cross-city shipment,
// city-unlock, and per-city breakdown sections, mirroring
// `projects/frontend/src/types/ledger.ts` and `world.ts`'s
// `CityUnlockStatus`. GraphQL field names verified against the web's
// `LedgerView.vue`/`LedgerMainContent.vue`/`CityExpansionPanel.vue` query
// strings.

class CityRevenueTrendPoint {
  const CityRevenueTrendPoint({required this.tick, required this.revenue});

  final int tick;
  final double revenue;

  factory CityRevenueTrendPoint.fromJson(Map<String, dynamic> json) =>
      CityRevenueTrendPoint(tick: (json['tick'] as num?)?.toInt() ?? 0, revenue: (json['revenue'] as num?)?.toDouble() ?? 0);
}

class CityFinancialBreakdown {
  const CityFinancialBreakdown({
    required this.cityName,
    required this.currencyCode,
    required this.revenue,
    required this.costs,
    required this.profit,
    this.cityId = '',
    this.revenueTrend = const [],
  });

  final String cityId;
  final String cityName;
  final String currencyCode;
  final double revenue;
  final double costs;
  final double profit;
  final List<CityRevenueTrendPoint> revenueTrend;

  factory CityFinancialBreakdown.fromJson(Map<String, dynamic> json) => CityFinancialBreakdown(
    cityId: (json['cityId'] as String?) ?? '',
    cityName: (json['cityName'] as String?) ?? '',
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
    costs: (json['costs'] as num?)?.toDouble() ?? 0,
    profit: (json['profit'] as num?)?.toDouble() ?? 0,
    revenueTrend: ((json['revenueTrend'] as List<dynamic>?) ?? const [])
        .map((e) => CityRevenueTrendPoint.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

class LedgerEntryResult {
  const LedgerEntryResult({
    required this.id,
    required this.category,
    required this.description,
    required this.amount,
    required this.recordedAtTick,
    required this.currencyCode,
    this.buildingId,
    this.buildingName,
    this.buildingType,
    this.productName,
    this.resourceName,
    this.eventTag,
    this.eventDescription,
  });

  final String id;
  final String category;
  final String description;
  final double amount;
  final int recordedAtTick;
  final String currencyCode;
  final String? buildingId;
  final String? buildingName;
  final String? buildingType;
  final String? productName;
  final String? resourceName;
  final String? eventTag;
  final String? eventDescription;

  factory LedgerEntryResult.fromJson(Map<String, dynamic> json) => LedgerEntryResult(
    id: json['id'] as String,
    category: (json['category'] as String?) ?? '',
    description: (json['description'] as String?) ?? '',
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    recordedAtTick: (json['recordedAtTick'] as num?)?.toInt() ?? 0,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    buildingId: json['buildingId'] as String?,
    buildingName: json['buildingName'] as String?,
    buildingType: json['buildingType'] as String?,
    productName: json['productName'] as String?,
    resourceName: json['resourceName'] as String?,
    eventTag: json['eventTag'] as String?,
    eventDescription: json['eventDescription'] as String?,
  );
}

class CityUnlockStatus {
  const CityUnlockStatus({
    required this.cityId,
    required this.cityName,
    required this.countryCode,
    required this.isUnlocked,
    required this.requiredNetWorth,
    required this.currentNetWorth,
    required this.currency,
    required this.progressPercent,
    this.estimatedTicksToUnlock,
  });

  final String cityId;
  final String cityName;
  final String countryCode;
  final bool isUnlocked;
  final double requiredNetWorth;
  final double currentNetWorth;
  final String currency;
  final double progressPercent;
  final double? estimatedTicksToUnlock;

  /// Ported from `computeCityUnlockProgress` in
  /// `projects/frontend/src/lib/cityExpansion.ts`.
  int get progressPercentClamped {
    if (isUnlocked) return 100;
    if (requiredNetWorth <= 0) return 100;
    if (progressPercent.isFinite && progressPercent > 0) {
      return progressPercent.round().clamp(0, 100);
    }
    return ((currentNetWorth / requiredNetWorth) * 100).round().clamp(0, 99);
  }

  factory CityUnlockStatus.fromJson(Map<String, dynamic> json) => CityUnlockStatus(
    cityId: json['cityId'] as String,
    cityName: (json['cityName'] as String?) ?? '',
    countryCode: (json['countryCode'] as String?) ?? '',
    isUnlocked: json['isUnlocked'] as bool? ?? false,
    requiredNetWorth: (json['requiredNetWorth'] as num?)?.toDouble() ?? 0,
    currentNetWorth: (json['currentNetWorth'] as num?)?.toDouble() ?? 0,
    currency: (json['currency'] as String?) ?? 'EUR',
    progressPercent: (json['progressPercent'] as num?)?.toDouble() ?? 0,
    estimatedTicksToUnlock: (json['estimatedTicksToUnlock'] as num?)?.toDouble(),
  );
}
