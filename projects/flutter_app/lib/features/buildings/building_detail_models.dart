// Data models for the (heavily trimmed) Building Detail screen, mirroring
// the read-only + quick-action subset of
// `projects/frontend/src/views/BuildingDetailView.vue`. GraphQL field names
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
  });

  final String id;
  final String unitType;
  final int level;
  final String? resourceTypeId;
  final String? productTypeId;
  final double? minPrice;

  /// Position within the building's 4x4 unit grid, each 0..3. Mirrors
  /// `BuildingUnit.GridX`/`GridY` on the backend and `gridX`/`gridY` in
  /// `projects/frontend/src/types/building.ts` — see `BuildingUnitGrid.vue`.
  final int gridX;
  final int gridY;

  factory BuildingUnitDetail.fromJson(Map<String, dynamic> json) => BuildingUnitDetail(
    id: json['id'] as String,
    unitType: (json['unitType'] as String?) ?? '',
    level: (json['level'] as num?)?.toInt() ?? 1,
    resourceTypeId: json['resourceTypeId'] as String?,
    productTypeId: json['productTypeId'] as String?,
    minPrice: (json['minPrice'] as num?)?.toDouble(),
    gridX: (json['gridX'] as num?)?.toInt() ?? 0,
    gridY: (json['gridY'] as num?)?.toInt() ?? 0,
  );
}

class PendingBuildingConfiguration {
  const PendingBuildingConfiguration({required this.appliesAtTick, required this.totalTicksRequired, required this.blockReason});

  final int appliesAtTick;
  final int totalTicksRequired;
  final String? blockReason;

  factory PendingBuildingConfiguration.fromJson(Map<String, dynamic> json) => PendingBuildingConfiguration(
    appliesAtTick: (json['appliesAtTick'] as num?)?.toInt() ?? 0,
    totalTicksRequired: (json['totalTicksRequired'] as num?)?.toInt() ?? 0,
    blockReason: json['blockReason'] as String?,
  );
}

class BuildingDetail {
  const BuildingDetail({
    required this.id,
    required this.name,
    required this.type,
    required this.level,
    required this.powerStatus,
    required this.occupancyPercent,
    required this.isForSale,
    required this.units,
    required this.pendingConfiguration,
  });

  final String id;
  final String name;
  final String type;
  final int level;
  final String? powerStatus;
  final double? occupancyPercent;
  final bool isForSale;
  final List<BuildingUnitDetail> units;
  final PendingBuildingConfiguration? pendingConfiguration;

  factory BuildingDetail.fromJson(Map<String, dynamic> json) => BuildingDetail(
    id: json['id'] as String,
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
  );
}
