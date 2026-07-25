// Data models for the building analytics panels (ROADMAP 137): unit
// resource history, the building financial timeline, the recent-activity
// feed, the supply-chain diagram, and per-unit product analytics. Field
// names verified against `useBuildingDetail.ts` and
// `projects/frontend/src/types/building.ts`/`analytics.ts`.

class UnitResourceHistoryPoint {
  const UnitResourceHistoryPoint({
    required this.buildingUnitId,
    required this.tick,
    required this.inflowQuantity,
    required this.outflowQuantity,
    required this.consumedQuantity,
    required this.producedQuantity,
  });

  final String buildingUnitId;
  final int tick;
  final double inflowQuantity;
  final double outflowQuantity;
  final double consumedQuantity;
  final double producedQuantity;

  factory UnitResourceHistoryPoint.fromJson(Map<String, dynamic> json) => UnitResourceHistoryPoint(
    buildingUnitId: json['buildingUnitId'] as String,
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    inflowQuantity: (json['inflowQuantity'] as num?)?.toDouble() ?? 0,
    outflowQuantity: (json['outflowQuantity'] as num?)?.toDouble() ?? 0,
    consumedQuantity: (json['consumedQuantity'] as num?)?.toDouble() ?? 0,
    producedQuantity: (json['producedQuantity'] as num?)?.toDouble() ?? 0,
  );
}

class BuildingFinancialTickSnapshot {
  const BuildingFinancialTickSnapshot({required this.tick, required this.sales, required this.costs, required this.profit});

  final int tick;
  final double sales;
  final double costs;
  final double profit;

  factory BuildingFinancialTickSnapshot.fromJson(Map<String, dynamic> json) => BuildingFinancialTickSnapshot(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    sales: (json['sales'] as num?)?.toDouble() ?? 0,
    costs: (json['costs'] as num?)?.toDouble() ?? 0,
    profit: (json['profit'] as num?)?.toDouble() ?? 0,
  );
}

class BuildingFinancialTimeline {
  const BuildingFinancialTimeline({
    required this.dataFromTick,
    required this.dataToTick,
    required this.totalSales,
    required this.totalCosts,
    required this.totalProfit,
    required this.timeline,
  });

  final int dataFromTick;
  final int dataToTick;
  final double totalSales;
  final double totalCosts;
  final double totalProfit;
  final List<BuildingFinancialTickSnapshot> timeline;

  factory BuildingFinancialTimeline.fromJson(Map<String, dynamic> json) => BuildingFinancialTimeline(
    dataFromTick: (json['dataFromTick'] as num?)?.toInt() ?? 0,
    dataToTick: (json['dataToTick'] as num?)?.toInt() ?? 0,
    totalSales: (json['totalSales'] as num?)?.toDouble() ?? 0,
    totalCosts: (json['totalCosts'] as num?)?.toDouble() ?? 0,
    totalProfit: (json['totalProfit'] as num?)?.toDouble() ?? 0,
    timeline: ((json['timeline'] as List<dynamic>?) ?? const [])
        .map((e) => BuildingFinancialTickSnapshot.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// `eventType`: `PURCHASED` | `MANUFACTURED` | `MOVED` | `SOLD` | `IDLE` | `BLOCKED`.
class BuildingRecentActivityEvent {
  const BuildingRecentActivityEvent({
    required this.tick,
    required this.eventType,
    required this.description,
    required this.quantity,
    required this.amount,
  });

  final int tick;
  final String eventType;
  final String description;
  final double? quantity;
  final double? amount;

  factory BuildingRecentActivityEvent.fromJson(Map<String, dynamic> json) => BuildingRecentActivityEvent(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    eventType: (json['eventType'] as String?) ?? '',
    description: (json['description'] as String?) ?? '',
    quantity: (json['quantity'] as num?)?.toDouble(),
    amount: (json['amount'] as num?)?.toDouble(),
  );
}

/// `status`: `ACTIVE` | `BLOCKED` | `FULL` | `IDLE` | `UNCONFIGURED`.
class SupplyChainUnitSummary {
  const SupplyChainUnitSummary({
    required this.buildingUnitId,
    required this.unitType,
    required this.gridX,
    required this.gridY,
    required this.status,
    required this.idleTicks,
    required this.fillPercent,
    required this.resourceOrProductName,
  });

  final String buildingUnitId;
  final String unitType;
  final int gridX;
  final int gridY;
  final String status;
  final int idleTicks;
  final double fillPercent;
  final String? resourceOrProductName;

  factory SupplyChainUnitSummary.fromJson(Map<String, dynamic> json) => SupplyChainUnitSummary(
    buildingUnitId: json['buildingUnitId'] as String,
    unitType: (json['unitType'] as String?) ?? '',
    gridX: (json['gridX'] as num?)?.toInt() ?? 0,
    gridY: (json['gridY'] as num?)?.toInt() ?? 0,
    status: (json['status'] as String?) ?? 'UNCONFIGURED',
    idleTicks: (json['idleTicks'] as num?)?.toInt() ?? 0,
    fillPercent: (json['fillPercent'] as num?)?.toDouble() ?? 0,
    resourceOrProductName: json['resourceOrProductName'] as String?,
  );
}

class SupplyChainLink {
  const SupplyChainLink({required this.fromUnitId, required this.toUnitId, required this.estimatedTransitCost});

  final String fromUnitId;
  final String toUnitId;
  final double estimatedTransitCost;

  factory SupplyChainLink.fromJson(Map<String, dynamic> json) => SupplyChainLink(
    fromUnitId: json['fromUnitId'] as String,
    toUnitId: json['toUnitId'] as String,
    estimatedTransitCost: (json['estimatedTransitCost'] as num?)?.toDouble() ?? 0,
  );
}

/// `healthScore`: `GREEN` | `YELLOW` | `RED`.
class BuildingSupplyChainDiagram {
  const BuildingSupplyChainDiagram({
    required this.units,
    required this.links,
    required this.healthScore,
    required this.healthReason,
  });

  final List<SupplyChainUnitSummary> units;
  final List<SupplyChainLink> links;
  final String healthScore;
  final String? healthReason;

  factory BuildingSupplyChainDiagram.fromJson(Map<String, dynamic> json) => BuildingSupplyChainDiagram(
    units: ((json['units'] as List<dynamic>?) ?? const [])
        .map((e) => SupplyChainUnitSummary.fromJson(e as Map<String, dynamic>))
        .toList(),
    links: ((json['links'] as List<dynamic>?) ?? const [])
        .map((e) => SupplyChainLink.fromJson(e as Map<String, dynamic>))
        .toList(),
    healthScore: (json['healthScore'] as String?) ?? 'GREEN',
    healthReason: json['healthReason'] as String?,
  );
}

class UnitProductTickSnapshot {
  const UnitProductTickSnapshot({
    required this.tick,
    required this.totalCost,
    required this.quantityProduced,
    required this.estimatedRevenue,
    required this.estimatedProfit,
  });

  final int tick;
  final double totalCost;
  final double quantityProduced;
  final double estimatedRevenue;
  final double estimatedProfit;

  factory UnitProductTickSnapshot.fromJson(Map<String, dynamic> json) => UnitProductTickSnapshot(
    tick: (json['tick'] as num?)?.toInt() ?? 0,
    totalCost: (json['totalCost'] as num?)?.toDouble() ?? 0,
    quantityProduced: (json['quantityProduced'] as num?)?.toDouble() ?? 0,
    estimatedRevenue: (json['estimatedRevenue'] as num?)?.toDouble() ?? 0,
    estimatedProfit: (json['estimatedProfit'] as num?)?.toDouble() ?? 0,
  );
}

class UnitProductAnalytics {
  const UnitProductAnalytics({
    required this.buildingUnitId,
    required this.productName,
    required this.dataFromTick,
    required this.dataToTick,
    required this.totalCost,
    required this.totalQuantityProduced,
    required this.estimatedRevenue,
    required this.estimatedProfit,
    required this.cityCurrencyCode,
    required this.snapshots,
  });

  final String buildingUnitId;
  final String? productName;
  final int dataFromTick;
  final int dataToTick;
  final double totalCost;
  final double totalQuantityProduced;
  final double estimatedRevenue;
  final double estimatedProfit;
  final String? cityCurrencyCode;
  final List<UnitProductTickSnapshot> snapshots;

  factory UnitProductAnalytics.fromJson(Map<String, dynamic> json) => UnitProductAnalytics(
    buildingUnitId: json['buildingUnitId'] as String,
    productName: json['productName'] as String?,
    dataFromTick: (json['dataFromTick'] as num?)?.toInt() ?? 0,
    dataToTick: (json['dataToTick'] as num?)?.toInt() ?? 0,
    totalCost: (json['totalCost'] as num?)?.toDouble() ?? 0,
    totalQuantityProduced: (json['totalQuantityProduced'] as num?)?.toDouble() ?? 0,
    estimatedRevenue: (json['estimatedRevenue'] as num?)?.toDouble() ?? 0,
    estimatedProfit: (json['estimatedProfit'] as num?)?.toDouble() ?? 0,
    cityCurrencyCode: json['cityCurrencyCode'] as String?,
    snapshots: ((json['snapshots'] as List<dynamic>?) ?? const [])
        .map((e) => UnitProductTickSnapshot.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
