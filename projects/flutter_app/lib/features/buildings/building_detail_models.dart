// Data models for the Building Detail screen, mirroring
// `projects/frontend/src/types/building.ts`'s `BuildingUnit`/
// `BuildingConfigurationPlanUnit`/`Building` shapes. GraphQL field names
// verified against `Api/Data/Entities/Building.cs`/`BuildingUnit.cs` and
// `Api/Types/Inputs.Building.cs`.

class BuildingUnitDetail {
  const BuildingUnitDetail({
    required this.id,
    required this.unitType,
    required this.level,
    required this.resourceTypeId,
    required this.productTypeId,
    required this.minPrice,
    required this.gridX,
    required this.gridY,
    this.maxPrice,
    this.purchaseSource,
    this.saleVisibility,
    this.budget,
    this.mediaHouseBuildingId,
    this.minQuality,
    this.brandScope,
    this.vendorLockCompanyId,
    this.lockedCityId,
    this.industryCategory,
    this.lowInventoryAlertThreshold,
    this.linkUp = false,
    this.linkDown = false,
    this.linkLeft = false,
    this.linkRight = false,
    this.linkUpLeft = false,
    this.linkUpRight = false,
    this.linkDownLeft = false,
    this.linkDownRight = false,
  });

  final String id;
  final String unitType;
  final int level;
  final String? resourceTypeId;
  final String? productTypeId;
  final double? minPrice;
  final double? maxPrice;
  final String? purchaseSource;
  final String? saleVisibility;
  final double? budget;
  final String? mediaHouseBuildingId;
  final double? minQuality;
  final String? brandScope;
  final String? vendorLockCompanyId;
  final String? lockedCityId;
  final String? industryCategory;
  final double? lowInventoryAlertThreshold;

  /// Position within the building's 4x4 unit grid, each 0..3. Mirrors
  /// `BuildingUnit.GridX`/`GridY` on the backend and `gridX`/`gridY` in
  /// `projects/frontend/src/types/building.ts` — see `BuildingUnitGrid.vue`.
  final int gridX;
  final int gridY;

  final bool linkUp;
  final bool linkDown;
  final bool linkLeft;
  final bool linkRight;
  final bool linkUpLeft;
  final bool linkUpRight;
  final bool linkDownLeft;
  final bool linkDownRight;

  factory BuildingUnitDetail.fromJson(Map<String, dynamic> json) => BuildingUnitDetail(
    id: json['id'] as String,
    unitType: (json['unitType'] as String?) ?? '',
    level: (json['level'] as num?)?.toInt() ?? 1,
    resourceTypeId: json['resourceTypeId'] as String?,
    productTypeId: json['productTypeId'] as String?,
    minPrice: (json['minPrice'] as num?)?.toDouble(),
    gridX: (json['gridX'] as num?)?.toInt() ?? 0,
    gridY: (json['gridY'] as num?)?.toInt() ?? 0,
    maxPrice: (json['maxPrice'] as num?)?.toDouble(),
    purchaseSource: json['purchaseSource'] as String?,
    saleVisibility: json['saleVisibility'] as String?,
    budget: (json['budget'] as num?)?.toDouble(),
    mediaHouseBuildingId: json['mediaHouseBuildingId'] as String?,
    minQuality: (json['minQuality'] as num?)?.toDouble(),
    brandScope: json['brandScope'] as String?,
    vendorLockCompanyId: json['vendorLockCompanyId'] as String?,
    lockedCityId: json['lockedCityId'] as String?,
    industryCategory: json['industryCategory'] as String?,
    lowInventoryAlertThreshold: (json['lowInventoryAlertThreshold'] as num?)?.toDouble(),
    linkUp: json['linkUp'] as bool? ?? false,
    linkDown: json['linkDown'] as bool? ?? false,
    linkLeft: json['linkLeft'] as bool? ?? false,
    linkRight: json['linkRight'] as bool? ?? false,
    linkUpLeft: json['linkUpLeft'] as bool? ?? false,
    linkUpRight: json['linkUpRight'] as bool? ?? false,
    linkDownLeft: json['linkDownLeft'] as bool? ?? false,
    linkDownRight: json['linkDownRight'] as bool? ?? false,
  );
}

class PendingConfigurationUnit {
  const PendingConfigurationUnit({
    required this.id,
    required this.unitType,
    required this.gridX,
    required this.gridY,
    required this.level,
    required this.appliesAtTick,
    required this.ticksRequired,
    required this.isChanged,
    required this.isReverting,
    this.resourceTypeId,
    this.productTypeId,
    this.minPrice,
    this.maxPrice,
    this.purchaseSource,
    this.saleVisibility,
    this.budget,
    this.mediaHouseBuildingId,
    this.minQuality,
    this.brandScope,
    this.vendorLockCompanyId,
    this.lockedCityId,
    this.industryCategory,
    this.linkUp = false,
    this.linkDown = false,
    this.linkLeft = false,
    this.linkRight = false,
    this.linkUpLeft = false,
    this.linkUpRight = false,
    this.linkDownLeft = false,
    this.linkDownRight = false,
  });

  final String id;
  final String unitType;
  final int gridX;
  final int gridY;
  final int level;
  final int appliesAtTick;
  final int ticksRequired;
  final bool isChanged;
  final bool isReverting;
  final String? resourceTypeId;
  final String? productTypeId;
  final double? minPrice;
  final double? maxPrice;
  final String? purchaseSource;
  final String? saleVisibility;
  final double? budget;
  final String? mediaHouseBuildingId;
  final double? minQuality;
  final String? brandScope;
  final String? vendorLockCompanyId;
  final String? lockedCityId;
  final String? industryCategory;
  final bool linkUp;
  final bool linkDown;
  final bool linkLeft;
  final bool linkRight;
  final bool linkUpLeft;
  final bool linkUpRight;
  final bool linkDownLeft;
  final bool linkDownRight;

  factory PendingConfigurationUnit.fromJson(Map<String, dynamic> json) => PendingConfigurationUnit(
    id: json['id'] as String,
    unitType: (json['unitType'] as String?) ?? '',
    gridX: (json['gridX'] as num?)?.toInt() ?? 0,
    gridY: (json['gridY'] as num?)?.toInt() ?? 0,
    level: (json['level'] as num?)?.toInt() ?? 1,
    appliesAtTick: (json['appliesAtTick'] as num?)?.toInt() ?? 0,
    ticksRequired: (json['ticksRequired'] as num?)?.toInt() ?? 0,
    isChanged: json['isChanged'] as bool? ?? false,
    isReverting: json['isReverting'] as bool? ?? false,
    resourceTypeId: json['resourceTypeId'] as String?,
    productTypeId: json['productTypeId'] as String?,
    minPrice: (json['minPrice'] as num?)?.toDouble(),
    maxPrice: (json['maxPrice'] as num?)?.toDouble(),
    purchaseSource: json['purchaseSource'] as String?,
    saleVisibility: json['saleVisibility'] as String?,
    budget: (json['budget'] as num?)?.toDouble(),
    mediaHouseBuildingId: json['mediaHouseBuildingId'] as String?,
    minQuality: (json['minQuality'] as num?)?.toDouble(),
    brandScope: json['brandScope'] as String?,
    vendorLockCompanyId: json['vendorLockCompanyId'] as String?,
    lockedCityId: json['lockedCityId'] as String?,
    industryCategory: json['industryCategory'] as String?,
    linkUp: json['linkUp'] as bool? ?? false,
    linkDown: json['linkDown'] as bool? ?? false,
    linkLeft: json['linkLeft'] as bool? ?? false,
    linkRight: json['linkRight'] as bool? ?? false,
    linkUpLeft: json['linkUpLeft'] as bool? ?? false,
    linkUpRight: json['linkUpRight'] as bool? ?? false,
    linkDownLeft: json['linkDownLeft'] as bool? ?? false,
    linkDownRight: json['linkDownRight'] as bool? ?? false,
  );
}

class PendingBuildingConfiguration {
  const PendingBuildingConfiguration({
    required this.appliesAtTick,
    required this.totalTicksRequired,
    required this.blockReason,
    this.units = const [],
  });

  final int appliesAtTick;
  final int totalTicksRequired;
  final String? blockReason;
  final List<PendingConfigurationUnit> units;

  factory PendingBuildingConfiguration.fromJson(Map<String, dynamic> json) => PendingBuildingConfiguration(
    appliesAtTick: (json['appliesAtTick'] as num?)?.toInt() ?? 0,
    totalTicksRequired: (json['totalTicksRequired'] as num?)?.toInt() ?? 0,
    blockReason: json['blockReason'] as String?,
    units: ((json['units'] as List<dynamic>?) ?? const [])
        .map((e) => PendingConfigurationUnit.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

class BuildingDetail {
  const BuildingDetail({
    required this.id,
    required this.companyId,
    required this.name,
    required this.type,
    required this.level,
    required this.powerStatus,
    required this.occupancyPercent,
    required this.isForSale,
    required this.units,
    required this.pendingConfiguration,
    this.cityId,
    this.cityFxRate = 1,
    // APARTMENT/COMMERCIAL property fields.
    this.pricePerSqm,
    this.pendingPricePerSqm,
    this.pendingPriceActivationTick,
    this.totalAreaSqm,
    this.cityReferenceRentPerSqm,
    this.adjustedMarketRentPerSqm,
    this.populationIndex,
    // MEDIA_HOUSE fields.
    this.mediaType,
    this.contentValue,
    this.contentBudgetPerTick,
    this.isGovernmentOwned = false,
    // POWER_PLANT fields.
    this.powerPlantType,
    this.powerOutput,
    this.dispatchTargetPercent,
    this.powerPriority,
    this.maxEnergyBidPrice,
    this.fuelReserveMwh,
  });

  final String id;
  final String companyId;
  final String name;
  final String type;
  final int level;
  final String? powerStatus;
  final double? occupancyPercent;
  final bool isForSale;
  final List<BuildingUnitDetail> units;
  final PendingBuildingConfiguration? pendingConfiguration;
  final String? cityId;

  /// City's EUR foreign-exchange rate — multiply a EUR base cost by this to
  /// get the local-currency amount, mirroring `cityFxRate` in
  /// `useBuildingDetail.ts`.
  final double cityFxRate;

  final double? pricePerSqm;
  final double? pendingPricePerSqm;
  final int? pendingPriceActivationTick;
  final double? totalAreaSqm;
  final double? cityReferenceRentPerSqm;
  final double? adjustedMarketRentPerSqm;
  final double? populationIndex;

  final String? mediaType;
  final double? contentValue;
  final double? contentBudgetPerTick;
  final bool isGovernmentOwned;

  final String? powerPlantType;
  final double? powerOutput;
  final int? dispatchTargetPercent;
  final int? powerPriority;
  final double? maxEnergyBidPrice;
  final double? fuelReserveMwh;

  factory BuildingDetail.fromJson(Map<String, dynamic> json, {required String companyId}) => BuildingDetail(
    id: json['id'] as String,
    companyId: companyId,
    name: (json['name'] as String?) ?? '',
    type: (json['type'] as String?) ?? '',
    level: (json['level'] as num?)?.toInt() ?? 1,
    powerStatus: json['powerStatus'] as String?,
    occupancyPercent: (json['occupancyPercent'] as num?)?.toDouble(),
    isForSale: json['isForSale'] as bool? ?? false,
    units: ((json['units'] as List<dynamic>?) ?? const [])
        .map((e) => BuildingUnitDetail.fromJson(e as Map<String, dynamic>))
        .toList(),
    pendingConfiguration: json['pendingConfiguration'] == null
        ? null
        : PendingBuildingConfiguration.fromJson(json['pendingConfiguration'] as Map<String, dynamic>),
    cityId: json['cityId'] as String?,
    cityFxRate: (json['cityFxRate'] as num?)?.toDouble() ?? 1,
    pricePerSqm: (json['pricePerSqm'] as num?)?.toDouble(),
    pendingPricePerSqm: (json['pendingPricePerSqm'] as num?)?.toDouble(),
    pendingPriceActivationTick: (json['pendingPriceActivationTick'] as num?)?.toInt(),
    totalAreaSqm: (json['totalAreaSqm'] as num?)?.toDouble(),
    cityReferenceRentPerSqm: (json['cityReferenceRentPerSqm'] as num?)?.toDouble(),
    adjustedMarketRentPerSqm: (json['adjustedMarketRentPerSqm'] as num?)?.toDouble(),
    populationIndex: (json['populationIndex'] as num?)?.toDouble(),
    mediaType: json['mediaType'] as String?,
    contentValue: (json['contentValue'] as num?)?.toDouble(),
    contentBudgetPerTick: (json['contentBudgetPerTick'] as num?)?.toDouble(),
    isGovernmentOwned: json['isGovernmentOwned'] as bool? ?? false,
    powerPlantType: json['powerPlantType'] as String?,
    powerOutput: (json['powerOutput'] as num?)?.toDouble(),
    dispatchTargetPercent: (json['dispatchTargetPercent'] as num?)?.toInt(),
    powerPriority: (json['powerPriority'] as num?)?.toInt(),
    maxEnergyBidPrice: (json['maxEnergyBidPrice'] as num?)?.toDouble(),
    fuelReserveMwh: (json['fuelReserveMwh'] as num?)?.toDouble(),
  );

  /// `cityFxRate` isn't part of the `Building` GraphQL type — it's resolved
  /// separately via [BuildingDetailService.fetchCityFxRate] and folded in
  /// here after the fact.
  BuildingDetail withCityFxRate(double rate) => BuildingDetail(
    id: id,
    companyId: companyId,
    name: name,
    type: type,
    level: level,
    powerStatus: powerStatus,
    occupancyPercent: occupancyPercent,
    isForSale: isForSale,
    units: units,
    pendingConfiguration: pendingConfiguration,
    cityId: cityId,
    cityFxRate: rate,
    pricePerSqm: pricePerSqm,
    pendingPricePerSqm: pendingPricePerSqm,
    pendingPriceActivationTick: pendingPriceActivationTick,
    totalAreaSqm: totalAreaSqm,
    cityReferenceRentPerSqm: cityReferenceRentPerSqm,
    adjustedMarketRentPerSqm: adjustedMarketRentPerSqm,
    populationIndex: populationIndex,
    mediaType: mediaType,
    contentValue: contentValue,
    contentBudgetPerTick: contentBudgetPerTick,
    isGovernmentOwned: isGovernmentOwned,
    powerPlantType: powerPlantType,
    powerOutput: powerOutput,
    dispatchTargetPercent: dispatchTargetPercent,
    powerPriority: powerPriority,
    maxEnergyBidPrice: maxEnergyBidPrice,
    fuelReserveMwh: fuelReserveMwh,
  );
}
