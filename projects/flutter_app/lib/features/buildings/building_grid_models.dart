// Mutable draft-unit model and supporting read models for the grid editor.
// `EditableGridUnit` mirrors `EditableGridUnit` in
// `projects/frontend/src/composables/useBuildingDetail.ts` (lines 132-161)
// — every config/link field is a plain mutable property, matching the
// web's direct in-place mutation pattern (`updateSelectedUnitConfig`)
// rather than an immutable-copyWith style, since the draft grid is edited
// field-by-field as the player interacts with the picker/config sheet.

import 'building_detail_models.dart';

class EditableGridUnit {
  EditableGridUnit({
    required this.id,
    required this.unitType,
    required this.gridX,
    required this.gridY,
    this.level = 1,
    this.linkUp = false,
    this.linkDown = false,
    this.linkLeft = false,
    this.linkRight = false,
    this.linkUpLeft = false,
    this.linkUpRight = false,
    this.linkDownLeft = false,
    this.linkDownRight = false,
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
    this.lowInventoryAlertThreshold,
    this.isReverting = false,
  });

  final String id;
  final String unitType;
  final int gridX;
  final int gridY;
  int level;

  bool linkUp;
  bool linkDown;
  bool linkLeft;
  bool linkRight;
  bool linkUpLeft;
  bool linkUpRight;
  bool linkDownLeft;
  bool linkDownRight;

  String? resourceTypeId;
  String? productTypeId;
  double? minPrice;
  double? maxPrice;
  String? purchaseSource;
  String? saleVisibility;
  double? budget;
  String? mediaHouseBuildingId;
  double? minQuality;
  String? brandScope;
  String? vendorLockCompanyId;
  String? lockedCityId;
  String? industryCategory;
  double? lowInventoryAlertThreshold;
  bool isReverting;

  factory EditableGridUnit.fromActive(BuildingUnitDetail unit) => EditableGridUnit(
    id: unit.id,
    unitType: unit.unitType,
    gridX: unit.gridX,
    gridY: unit.gridY,
    level: unit.level,
    linkUp: unit.linkUp,
    linkDown: unit.linkDown,
    linkLeft: unit.linkLeft,
    linkRight: unit.linkRight,
    linkUpLeft: unit.linkUpLeft,
    linkUpRight: unit.linkUpRight,
    linkDownLeft: unit.linkDownLeft,
    linkDownRight: unit.linkDownRight,
    resourceTypeId: unit.resourceTypeId,
    productTypeId: unit.productTypeId,
    minPrice: unit.minPrice,
    maxPrice: unit.maxPrice,
    purchaseSource: unit.purchaseSource,
    saleVisibility: unit.saleVisibility,
    budget: unit.budget,
    mediaHouseBuildingId: unit.mediaHouseBuildingId,
    minQuality: unit.minQuality,
    brandScope: unit.brandScope,
    vendorLockCompanyId: unit.vendorLockCompanyId,
    lockedCityId: unit.lockedCityId,
    industryCategory: unit.industryCategory,
    lowInventoryAlertThreshold: unit.lowInventoryAlertThreshold,
  );

  factory EditableGridUnit.fromPending(PendingConfigurationUnit unit) => EditableGridUnit(
    id: unit.id,
    unitType: unit.unitType,
    gridX: unit.gridX,
    gridY: unit.gridY,
    level: unit.level,
    linkUp: unit.linkUp,
    linkDown: unit.linkDown,
    linkLeft: unit.linkLeft,
    linkRight: unit.linkRight,
    linkUpLeft: unit.linkUpLeft,
    linkUpRight: unit.linkUpRight,
    linkDownLeft: unit.linkDownLeft,
    linkDownRight: unit.linkDownRight,
    resourceTypeId: unit.resourceTypeId,
    productTypeId: unit.productTypeId,
    minPrice: unit.minPrice,
    maxPrice: unit.maxPrice,
    purchaseSource: unit.purchaseSource,
    saleVisibility: unit.saleVisibility,
    budget: unit.budget,
    mediaHouseBuildingId: unit.mediaHouseBuildingId,
    minQuality: unit.minQuality,
    brandScope: unit.brandScope,
    vendorLockCompanyId: unit.vendorLockCompanyId,
    lockedCityId: unit.lockedCityId,
    industryCategory: unit.industryCategory,
    isReverting: unit.isReverting,
  );

  EditableGridUnit clone() => EditableGridUnit(
    id: id,
    unitType: unitType,
    gridX: gridX,
    gridY: gridY,
    level: level,
    linkUp: linkUp,
    linkDown: linkDown,
    linkLeft: linkLeft,
    linkRight: linkRight,
    linkUpLeft: linkUpLeft,
    linkUpRight: linkUpRight,
    linkDownLeft: linkDownLeft,
    linkDownRight: linkDownRight,
    resourceTypeId: resourceTypeId,
    productTypeId: productTypeId,
    minPrice: minPrice,
    maxPrice: maxPrice,
    purchaseSource: purchaseSource,
    saleVisibility: saleVisibility,
    budget: budget,
    mediaHouseBuildingId: mediaHouseBuildingId,
    minQuality: minQuality,
    brandScope: brandScope,
    vendorLockCompanyId: vendorLockCompanyId,
    lockedCityId: lockedCityId,
    industryCategory: industryCategory,
    lowInventoryAlertThreshold: lowInventoryAlertThreshold,
    isReverting: isReverting,
  );

  /// Builds the `BuildingConfigurationUnitInput`-shaped map for the
  /// `storeBuildingConfiguration` mutation. Server-controlled fields (`id`,
  /// `level`) are intentionally omitted, mirroring the web's
  /// `storeConfiguration()` mutation-payload mapper.
  Map<String, dynamic> toMutationInput() => {
    'unitType': unitType,
    'gridX': gridX,
    'gridY': gridY,
    'linkUp': linkUp,
    'linkDown': linkDown,
    'linkLeft': linkLeft,
    'linkRight': linkRight,
    'linkUpLeft': linkUpLeft,
    'linkUpRight': linkUpRight,
    'linkDownLeft': linkDownLeft,
    'linkDownRight': linkDownRight,
    'resourceTypeId': resourceTypeId,
    'productTypeId': productTypeId,
    'minPrice': minPrice,
    'maxPrice': maxPrice,
    'purchaseSource': purchaseSource,
    'saleVisibility': saleVisibility,
    'budget': budget,
    'mediaHouseBuildingId': mediaHouseBuildingId,
    'minQuality': minQuality,
    'brandScope': brandScope,
    'vendorLockCompanyId': vendorLockCompanyId,
    'lockedCityId': lockedCityId,
    'industryCategory': industryCategory,
  };
}

/// Resource/product catalog with base EUR prices, used by the grid editor's
/// item pickers and the B2B suggested-price hint.
class BuildingCatalog {
  const BuildingCatalog({
    required this.resourceNames,
    required this.productNames,
    required this.resourceBasePrices,
    required this.productBasePrices,
  });

  final Map<String, String> resourceNames;
  final Map<String, String> productNames;
  final Map<String, double> resourceBasePrices;
  final Map<String, double> productBasePrices;
}

/// Mirrors `BuildingUnitInventorySummary` from
/// `buildingUnitInventorySummaries(buildingId)` — verified against
/// `Api/Types/Query.Inventory.cs`.
class BuildingUnitInventorySummary {
  const BuildingUnitInventorySummary({
    required this.buildingUnitId,
    required this.quantity,
    required this.capacity,
    required this.fillPercent,
    required this.averageQuality,
    required this.totalSourcingCost,
    required this.sourcingCostPerUnit,
    required this.lastTickInflow,
    required this.lastTickOutflow,
  });

  final String buildingUnitId;
  final double quantity;
  final double capacity;
  final double fillPercent;
  final double? averageQuality;
  final double totalSourcingCost;
  final double sourcingCostPerUnit;
  final double? lastTickInflow;
  final double? lastTickOutflow;

  factory BuildingUnitInventorySummary.fromJson(Map<String, dynamic> json) => BuildingUnitInventorySummary(
    buildingUnitId: json['buildingUnitId'] as String,
    quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
    capacity: (json['capacity'] as num?)?.toDouble() ?? 0,
    fillPercent: (json['fillPercent'] as num?)?.toDouble() ?? 0,
    averageQuality: (json['averageQuality'] as num?)?.toDouble(),
    totalSourcingCost: (json['totalSourcingCost'] as num?)?.toDouble() ?? 0,
    sourcingCostPerUnit: (json['sourcingCostPerUnit'] as num?)?.toDouble() ?? 0,
    lastTickInflow: (json['lastTickInflow'] as num?)?.toDouble(),
    lastTickOutflow: (json['lastTickOutflow'] as num?)?.toDouble(),
  );
}

/// Mirrors the `UnitUpgradeInfo` GraphQL type returned by
/// `unitUpgradeInfo(unitId)` — verified against `Api/Types/Query.Types.Building.cs`
/// and the resolver in `Api/Types/Query.Lending.cs`.
class UnitUpgradeInfo {
  const UnitUpgradeInfo({
    required this.unitId,
    required this.unitType,
    required this.currentLevel,
    required this.nextLevel,
    required this.isMaxLevel,
    required this.isUpgradable,
    required this.upgradeCost,
    required this.upgradeTicks,
    required this.currentStat,
    required this.nextStat,
    required this.statLabel,
    required this.currentLaborHoursPerTick,
    required this.nextLaborHoursPerTick,
    required this.currentEnergyMwhPerTick,
    required this.nextEnergyMwhPerTick,
    required this.currentLaborCostPerTick,
    required this.nextLaborCostPerTick,
    required this.currentEnergyCostPerTick,
    required this.nextEnergyCostPerTick,
    required this.currentStorageCapacity,
    required this.nextStorageCapacity,
  });

  final String unitId;
  final String unitType;
  final int currentLevel;
  final int nextLevel;
  final bool isMaxLevel;
  final bool isUpgradable;
  final double upgradeCost;
  final int upgradeTicks;
  final double currentStat;
  final double nextStat;
  final String statLabel;
  final double currentLaborHoursPerTick;
  final double nextLaborHoursPerTick;
  final double currentEnergyMwhPerTick;
  final double nextEnergyMwhPerTick;
  final double currentLaborCostPerTick;
  final double nextLaborCostPerTick;
  final double currentEnergyCostPerTick;
  final double nextEnergyCostPerTick;
  final double currentStorageCapacity;
  final double nextStorageCapacity;

  factory UnitUpgradeInfo.fromJson(Map<String, dynamic> json) => UnitUpgradeInfo(
    unitId: json['unitId'] as String,
    unitType: (json['unitType'] as String?) ?? '',
    currentLevel: (json['currentLevel'] as num?)?.toInt() ?? 1,
    nextLevel: (json['nextLevel'] as num?)?.toInt() ?? 1,
    isMaxLevel: json['isMaxLevel'] as bool? ?? false,
    isUpgradable: json['isUpgradable'] as bool? ?? false,
    upgradeCost: (json['upgradeCost'] as num?)?.toDouble() ?? 0,
    upgradeTicks: (json['upgradeTicks'] as num?)?.toInt() ?? 0,
    currentStat: (json['currentStat'] as num?)?.toDouble() ?? 0,
    nextStat: (json['nextStat'] as num?)?.toDouble() ?? 0,
    statLabel: (json['statLabel'] as String?) ?? '',
    currentLaborHoursPerTick: (json['currentLaborHoursPerTick'] as num?)?.toDouble() ?? 0,
    nextLaborHoursPerTick: (json['nextLaborHoursPerTick'] as num?)?.toDouble() ?? 0,
    currentEnergyMwhPerTick: (json['currentEnergyMwhPerTick'] as num?)?.toDouble() ?? 0,
    nextEnergyMwhPerTick: (json['nextEnergyMwhPerTick'] as num?)?.toDouble() ?? 0,
    currentLaborCostPerTick: (json['currentLaborCostPerTick'] as num?)?.toDouble() ?? 0,
    nextLaborCostPerTick: (json['nextLaborCostPerTick'] as num?)?.toDouble() ?? 0,
    currentEnergyCostPerTick: (json['currentEnergyCostPerTick'] as num?)?.toDouble() ?? 0,
    nextEnergyCostPerTick: (json['nextEnergyCostPerTick'] as num?)?.toDouble() ?? 0,
    currentStorageCapacity: (json['currentStorageCapacity'] as num?)?.toDouble() ?? 0,
    nextStorageCapacity: (json['nextStorageCapacity'] as num?)?.toDouble() ?? 0,
  );
}
