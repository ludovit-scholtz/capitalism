// Data models for the building-type-specific management panels
// (Apartment/Commercial property, Power Plant, Research & Development,
// Media House) — the non-grid or grid-adjacent panels documented in
// ROADMAP.md items 131-134. GraphQL shapes verified against
// `Api/Types/Query.RentalIncome.cs`, `Api/Types/Query.PowerPlant.cs`,
// `Api/Types/Query.Analytics.cs`, `Api/Types/Query.Rankings.Performance.cs`,
// `Api/Types/Query.Analytics.MediaHouseStats.cs`.

class RentalTickSnapshot {
  const RentalTickSnapshot({required this.tick, required this.revenue, required this.occupancyPercent, required this.rentPerSqm});

  final int tick;
  final double revenue;
  final double occupancyPercent;
  final double rentPerSqm;

  factory RentalTickSnapshot.fromJson(Map<String, dynamic> json) => RentalTickSnapshot(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    revenue: (json['revenue'] as num?)?.toDouble() ?? 0,
    occupancyPercent: (json['occupancyPercent'] as num?)?.toDouble() ?? 0,
    rentPerSqm: (json['rentPerSqm'] as num?)?.toDouble() ?? 0,
  );
}

/// `apartmentBuildingDetail(buildingId)` — serves both APARTMENT and
/// COMMERCIAL building types (one unified query/mutation on both web and
/// backend, confirmed no `commercialBuildingDetail` variant exists).
class ApartmentBuildingDetail {
  const ApartmentBuildingDetail({
    required this.buildingId,
    required this.occupancyPercent,
    required this.totalAreaSqm,
    required this.pricePerSqm,
    required this.pendingPricePerSqm,
    required this.pendingPriceActivationTick,
    required this.cityAverageRentPerSqm,
    required this.adjustedMarketRentPerSqm,
    required this.populationIndex,
    required this.currencyCode,
    required this.revenueHistory,
  });

  final String buildingId;
  final double? occupancyPercent;
  final double? totalAreaSqm;
  final double? pricePerSqm;
  final double? pendingPricePerSqm;
  final int? pendingPriceActivationTick;
  final double cityAverageRentPerSqm;
  final double adjustedMarketRentPerSqm;
  final double populationIndex;
  final String currencyCode;
  final List<RentalTickSnapshot> revenueHistory;

  factory ApartmentBuildingDetail.fromJson(Map<String, dynamic> json) => ApartmentBuildingDetail(
    buildingId: json['buildingId'] as String,
    occupancyPercent: (json['occupancyPercent'] as num?)?.toDouble(),
    totalAreaSqm: (json['totalAreaSqm'] as num?)?.toDouble(),
    pricePerSqm: (json['pricePerSqm'] as num?)?.toDouble(),
    pendingPricePerSqm: (json['pendingPricePerSqm'] as num?)?.toDouble(),
    pendingPriceActivationTick: (json['pendingPriceActivationTick'] as num?)?.toInt(),
    cityAverageRentPerSqm: (json['cityAverageRentPerSqm'] as num?)?.toDouble() ?? 0,
    adjustedMarketRentPerSqm: (json['adjustedMarketRentPerSqm'] as num?)?.toDouble() ?? 0,
    populationIndex: (json['populationIndex'] as num?)?.toDouble() ?? 1,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    revenueHistory: ((json['revenueHistory'] as List<dynamic>?) ?? const [])
        .map((e) => RentalTickSnapshot.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// `companyBrands(companyId)` — company-wide research/brand progress, not
/// scoped to a single building or unit (multiple R&D units on the same
/// product type all feed the same underlying `Brand`/`ProductResearchBudget`
/// row server-side). `marketingQuality` is included here even though the
/// web's own query omits it (a live bug there producing NaN badges) — see
/// `building_research_panel.dart`'s header comment.
class CompanyBrand {
  const CompanyBrand({
    required this.id,
    required this.name,
    required this.scope,
    required this.awareness,
    required this.quality,
    required this.marketingQuality,
    required this.marketingEfficiencyMultiplier,
    this.productTypeId,
    this.productName,
    this.industryCategory,
    this.accumulatedResearchBudget,
    this.baseResearchBudget,
    this.maxCompetitorBudget,
  });

  final String id;
  final String name;
  final String scope; // PRODUCT | CATEGORY | COMPANY
  final double awareness;
  final double quality;
  final double marketingQuality;
  final double marketingEfficiencyMultiplier;
  final String? productTypeId;
  final String? productName;
  final String? industryCategory;
  final double? accumulatedResearchBudget;
  final double? baseResearchBudget;
  final double? maxCompetitorBudget;

  factory CompanyBrand.fromJson(Map<String, dynamic> json) => CompanyBrand(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    scope: (json['scope'] as String?) ?? 'COMPANY',
    awareness: (json['awareness'] as num?)?.toDouble() ?? 0,
    quality: (json['quality'] as num?)?.toDouble() ?? 0,
    marketingQuality: (json['marketingQuality'] as num?)?.toDouble() ?? 0,
    marketingEfficiencyMultiplier: (json['marketingEfficiencyMultiplier'] as num?)?.toDouble() ?? 1,
    productTypeId: json['productTypeId'] as String?,
    productName: json['productName'] as String?,
    industryCategory: json['industryCategory'] as String?,
    accumulatedResearchBudget: (json['accumulatedResearchBudget'] as num?)?.toDouble(),
    baseResearchBudget: (json['baseResearchBudget'] as num?)?.toDouble(),
    maxCompetitorBudget: (json['maxCompetitorBudget'] as num?)?.toDouble(),
  );
}

/// `buildingBankAccount(buildingId)` — the building's own bank account,
/// distinct from a player's `BANK`-type building (every building carries
/// one operating account). Mirrors `BuildingBankAccountPanel.vue`'s query
/// shape, trimmed to the balance/threshold summary this tab needs (no
/// statement browsing, no cross-account reassignment controls).
class BuildingBankAccountInfo {
  const BuildingBankAccountInfo({
    required this.buildingId,
    required this.buildingName,
    required this.cityName,
    required this.currencyCode,
    required this.hasBankAccount,
    required this.balance,
    required this.isSuspendedForFunds,
    this.bankAccountId,
    this.accountNumber,
    this.alertMinBalanceThreshold,
    this.suspendedReason,
  });

  final String buildingId;
  final String buildingName;
  final String? cityName;
  final String currencyCode;
  final bool hasBankAccount;
  final String? bankAccountId;
  final String? accountNumber;
  final double balance;
  final double? alertMinBalanceThreshold;
  final bool isSuspendedForFunds;
  final String? suspendedReason;

  factory BuildingBankAccountInfo.fromJson(Map<String, dynamic> json) => BuildingBankAccountInfo(
    buildingId: json['buildingId'] as String,
    buildingName: (json['buildingName'] as String?) ?? '',
    cityName: json['cityName'] as String?,
    currencyCode: (json['currencyCode'] as String?) ?? 'EUR',
    hasBankAccount: json['hasBankAccount'] as bool? ?? false,
    bankAccountId: json['bankAccountId'] as String?,
    accountNumber: json['accountNumber'] as String?,
    balance: (json['balance'] as num?)?.toDouble() ?? 0,
    alertMinBalanceThreshold: (json['alertMinBalanceThreshold'] as num?)?.toDouble(),
    isSuspendedForFunds: json['isSuspendedForFunds'] as bool? ?? false,
    suspendedReason: json['suspendedReason'] as String?,
  );
}

class EnergyListing {
  const EnergyListing({
    required this.listingId,
    required this.pricePerKwhLocal,
    required this.capacityKw,
    required this.availableKw,
  });

  final String listingId;
  final double pricePerKwhLocal;
  final double capacityKw;
  final double availableKw;

  factory EnergyListing.fromJson(Map<String, dynamic> json) => EnergyListing(
    listingId: json['listingId'] as String,
    pricePerKwhLocal: (json['pricePerKwhLocal'] as num?)?.toDouble() ?? 0,
    capacityKw: (json['capacityKw'] as num?)?.toDouble() ?? 0,
    availableKw: (json['availableKw'] as num?)?.toDouble() ?? 0,
  );
}

/// `powerPlantAnalytics(buildingId)`. The per-tick `timeline` field is not
/// modeled/queried here — the P&L bar chart is a documented trim (see
/// `building_power_plant_panel.dart`); the aggregate totals below cover the
/// roadmap's "analytics" ask without needing chart-rendering code.
class PowerPlantAnalytics {
  const PowerPlantAnalytics({
    required this.currentOutputMw,
    required this.dispatchTargetPercent,
    required this.fuelReserveMwh,
    required this.maxFuelReserveMwh,
    required this.fuelReservePercent,
    required this.fuelPurchaseCapacityMwhPerTick,
    required this.energyProducingCapacityMw,
    required this.fuelConstrainedOutputMw,
    required this.fuelTypeLabel,
    required this.totalSurplusIncome,
    required this.totalGridFines,
    required this.totalOperatingCosts,
    required this.totalFuelCosts,
    required this.totalSpotMarketRevenue,
    required this.totalNetProfit,
    this.activeListing,
  });

  final double currentOutputMw;
  final int dispatchTargetPercent;
  final double fuelReserveMwh;
  final double maxFuelReserveMwh;
  final double fuelReservePercent;
  final double fuelPurchaseCapacityMwhPerTick;
  final double energyProducingCapacityMw;
  final double fuelConstrainedOutputMw;
  final String? fuelTypeLabel;
  final double totalSurplusIncome;
  final double totalGridFines;
  final double totalOperatingCosts;
  final double totalFuelCosts;
  final double totalSpotMarketRevenue;
  final double totalNetProfit;
  final EnergyListing? activeListing;

  factory PowerPlantAnalytics.fromJson(Map<String, dynamic> json) => PowerPlantAnalytics(
    currentOutputMw: (json['currentOutputMw'] as num?)?.toDouble() ?? 0,
    dispatchTargetPercent: (json['dispatchTargetPercent'] as num?)?.toInt() ?? 100,
    fuelReserveMwh: (json['fuelReserveMwh'] as num?)?.toDouble() ?? 0,
    maxFuelReserveMwh: (json['maxFuelReserveMwh'] as num?)?.toDouble() ?? 0,
    fuelReservePercent: (json['fuelReservePercent'] as num?)?.toDouble() ?? 0,
    fuelPurchaseCapacityMwhPerTick: (json['fuelPurchaseCapacityMwhPerTick'] as num?)?.toDouble() ?? 0,
    energyProducingCapacityMw: (json['energyProducingCapacityMw'] as num?)?.toDouble() ?? 0,
    fuelConstrainedOutputMw: (json['fuelConstrainedOutputMw'] as num?)?.toDouble() ?? 0,
    fuelTypeLabel: json['fuelTypeLabel'] as String?,
    totalSurplusIncome: (json['totalSurplusIncome'] as num?)?.toDouble() ?? 0,
    totalGridFines: (json['totalGridFines'] as num?)?.toDouble() ?? 0,
    totalOperatingCosts: (json['totalOperatingCosts'] as num?)?.toDouble() ?? 0,
    totalFuelCosts: (json['totalFuelCosts'] as num?)?.toDouble() ?? 0,
    totalSpotMarketRevenue: (json['totalSpotMarketRevenue'] as num?)?.toDouble() ?? 0,
    totalNetProfit: (json['totalNetProfit'] as num?)?.toDouble() ?? 0,
    activeListing: json['activeListing'] == null ? null : EnergyListing.fromJson(json['activeListing'] as Map<String, dynamic>),
  );
}

class CityPowerBalance {
  const CityPowerBalance({
    required this.totalSupplyMw,
    required this.totalDemandMw,
    required this.reserveMw,
    required this.reservePercent,
    required this.status,
    this.powerPlantCount = 0,
  });

  final double totalSupplyMw;
  final double totalDemandMw;
  final double reserveMw;
  final double reservePercent;
  final String status; // BALANCED | CONSTRAINED | CRITICAL

  /// `0` means the city has no power plants yet — still running on the
  /// legacy (pre-power-grid) unmetered supply, matching web's "Legacy grid"
  /// badge in `CityPowerPlanningSection.vue`.
  final int powerPlantCount;

  factory CityPowerBalance.fromJson(Map<String, dynamic> json) => CityPowerBalance(
    totalSupplyMw: (json['totalSupplyMw'] as num?)?.toDouble() ?? 0,
    totalDemandMw: (json['totalDemandMw'] as num?)?.toDouble() ?? 0,
    reserveMw: (json['reserveMw'] as num?)?.toDouble() ?? 0,
    reservePercent: (json['reservePercent'] as num?)?.toDouble() ?? 0,
    status: (json['status'] as String?) ?? 'BALANCED',
    powerPlantCount: (json['powerPlantCount'] as num?)?.toInt() ?? 0,
  );
}

class CityMediaHouse {
  const CityMediaHouse({
    required this.id,
    required this.name,
    required this.mediaType,
    required this.ownerCompanyName,
    required this.contentRanking,
    required this.isGovernmentOwned,
    this.effectivenessMultiplier = 1,
    this.powerStatus,
    this.isUnderConstruction = false,
  });

  final String id;
  final String name;
  final String? mediaType;
  final String? ownerCompanyName;
  final double contentRanking;
  final bool isGovernmentOwned;
  final double effectivenessMultiplier;
  final String? powerStatus;
  final bool isUnderConstruction;

  factory CityMediaHouse.fromJson(Map<String, dynamic> json) => CityMediaHouse(
    id: json['id'] as String,
    name: (json['name'] as String?) ?? '',
    mediaType: json['mediaType'] as String?,
    ownerCompanyName: json['ownerCompanyName'] as String?,
    contentRanking: (json['contentRanking'] as num?)?.toDouble() ?? 0,
    isGovernmentOwned: json['isGovernmentOwned'] as bool? ?? false,
    effectivenessMultiplier: (json['effectivenessMultiplier'] as num?)?.toDouble() ?? 1,
    powerStatus: json['powerStatus'] as String?,
    isUnderConstruction: json['isUnderConstruction'] as bool? ?? false,
  );
}

class MediaHouseUnitConfig {
  const MediaHouseUnitConfig({
    required this.id,
    required this.targetCompanyId,
    required this.targetCompanyName,
    required this.mediaType,
    required this.campaignBudgetPerTick,
    required this.isActive,
  });

  final String id;
  final String targetCompanyId;
  final String? targetCompanyName;
  final String mediaType;
  final double campaignBudgetPerTick;
  final bool isActive;

  factory MediaHouseUnitConfig.fromJson(Map<String, dynamic> json) => MediaHouseUnitConfig(
    id: json['id'] as String,
    targetCompanyId: json['targetCompanyId'] as String,
    targetCompanyName: json['targetCompanyName'] as String?,
    mediaType: (json['mediaType'] as String?) ?? 'NEWSPAPER',
    campaignBudgetPerTick: (json['campaignBudgetPerTick'] as num?)?.toDouble() ?? 0,
    isActive: json['isActive'] as bool? ?? true,
  );
}
