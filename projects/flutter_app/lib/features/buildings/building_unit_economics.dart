// Pure port of `projects/frontend/src/lib/buildingUnitEconomics.ts` — the
// client-side EUR construction-cost table and tick-cost constants used to
// preview grid-editor changes before saving. Mirrors the server's identical
// table in `Api/Utilities/BuildingConfigurationEconomics.cs` and the tick
// rules in `Api/Utilities/BuildingConfigurationService.Plan.cs`.

import 'building_grid_models.dart';

/// A brand-new unit placement or a same-position type replacement.
const int unitPlanChangeTicks = 3;

/// A link-flag or config-field-only change on an already-placed unit.
const int linkChangeTicks = 1;

/// Base EUR construction cost per unit type (before city FX conversion).
/// Identical table to the web's `unitConstructionCosts` and the server's
/// `BuildingConfigurationEconomics.GetUnitConstructionCost`.
const Map<String, double> unitConstructionCosts = {
  'MINING': 9000,
  'STORAGE': 3500,
  'B2B_SALES': 5000,
  'PURCHASE': 4500,
  'MANUFACTURING': 12000,
  'BRANDING': 7000,
  'MARKETING': 5500,
  'PUBLIC_SALES': 6000,
  'PRODUCT_QUALITY': 8500,
  'BRAND_QUALITY': 8500,
  'POWER_GENERATION': 15000,
  'BATTERY_STORAGE': 12000,
  'FUEL_PURCHASE': 12000,
  'WIND_TURBINE': 18000,
  'WATER_TURBINE': 25000,
  'ENERGY_STORAGE': 14000,
  'ENERGY_PRODUCING': 22000,
};

double getUnitConstructionCost(String unitType) => unitConstructionCosts[unitType] ?? 0;

/// Cost of placing/replacing [plannedUnit] at its grid position, given
/// whichever unit (if any) is currently active there. Same-type
/// reconfiguration at an unchanged position costs nothing to build.
double getPlannedUnitConstructionCost(EditableGridUnit? activeUnit, EditableGridUnit? plannedUnit) {
  if (plannedUnit == null) return 0;
  if (activeUnit == null) return getUnitConstructionCost(plannedUnit.unitType);
  if (activeUnit.unitType != plannedUnit.unitType) return getUnitConstructionCost(plannedUnit.unitType);
  return 0;
}

/// Sums construction cost across every position touched by [plannedUnits]
/// relative to [activeUnits]. Raw EUR, no FX conversion (mirrors the web's
/// `draftConstructionCost`, which is deliberately not FX-converted either —
/// see the file-level trim note in `building_grid_draft_controller.dart`).
double sumPlannedConfigurationCost(List<EditableGridUnit> activeUnits, List<EditableGridUnit> plannedUnits) {
  var total = 0.0;
  for (final planned in plannedUnits) {
    EditableGridUnit? active;
    for (final candidate in activeUnits) {
      if (candidate.gridX == planned.gridX && candidate.gridY == planned.gridY) {
        active = candidate;
        break;
      }
    }
    total += getPlannedUnitConstructionCost(active, planned);
  }
  return total;
}

/// 10%-of-original rollback cost for cancelling an in-progress plan change,
/// matching the server's `CalculateCancelTicks`.
int calculateCancelTicks(int originalTicks) {
  final tenPercent = (originalTicks * 0.1).ceil();
  return tenPercent < 1 ? 1 : tenPercent;
}

/// True when every config field that participates in tick-cost/equivalence
/// comparisons is identical between [a] and [b] (link flags are compared
/// separately by callers). Mirrors the server's `AreEquivalent` config-field
/// comparison used by `CalculateTicksRequired`/`areUnitsEquivalent`.
bool haveEquivalentConfig(EditableGridUnit a, EditableGridUnit b) {
  return a.resourceTypeId == b.resourceTypeId &&
      a.productTypeId == b.productTypeId &&
      a.minPrice == b.minPrice &&
      a.maxPrice == b.maxPrice &&
      a.purchaseSource == b.purchaseSource &&
      a.saleVisibility == b.saleVisibility &&
      a.budget == b.budget &&
      a.mediaHouseBuildingId == b.mediaHouseBuildingId &&
      a.minQuality == b.minQuality &&
      a.brandScope == b.brandScope &&
      a.vendorLockCompanyId == b.vendorLockCompanyId &&
      a.lockedCityId == b.lockedCityId;
}

bool haveEquivalentLinks(EditableGridUnit a, EditableGridUnit b) {
  return a.linkUp == b.linkUp &&
      a.linkDown == b.linkDown &&
      a.linkLeft == b.linkLeft &&
      a.linkRight == b.linkRight &&
      a.linkUpLeft == b.linkUpLeft &&
      a.linkUpRight == b.linkUpRight &&
      a.linkDownLeft == b.linkDownLeft &&
      a.linkDownRight == b.linkDownRight;
}

/// Client-side re-derivation of the server's `CalculateTicksRequired`, used
/// to preview the plan's total tick count before saving.
int calculateTicksRequired(EditableGridUnit? activeUnit, EditableGridUnit desiredUnit) {
  if (activeUnit == null) return unitPlanChangeTicks;
  if (activeUnit.unitType != desiredUnit.unitType) return unitPlanChangeTicks;
  if (!haveEquivalentLinks(activeUnit, desiredUnit)) return linkChangeTicks;
  if (!haveEquivalentConfig(activeUnit, desiredUnit)) return linkChangeTicks;
  return 0;
}

/// Port of `getB2BPriceSource` — suggests a competitive `minPrice` for a new
/// B2B_SALES unit from an orthogonally-adjacent (preferred) or any-in-draft
/// (fallback) MANUFACTURING/MINING unit's configured item base price.
/// Priority: adjacent-manufacturing > any-manufacturing > adjacent-mining >
/// any-mining > null.
class B2BPriceSource {
  const B2BPriceSource({required this.price, required this.sourceType, required this.itemName});
  final double price;
  final String sourceType;
  final String? itemName;
}

B2BPriceSource? getB2BPriceSource(
  EditableGridUnit unit,
  List<EditableGridUnit> draftUnits, {
  required Map<String, double> resourceBasePrices,
  required Map<String, double> productBasePrices,
  required Map<String, String> resourceNames,
  required Map<String, String> productNames,
  required double cityFxRate,
}) {
  EditableGridUnit? at(int x, int y) {
    for (final u in draftUnits) {
      if (u.gridX == x && u.gridY == y) return u;
    }
    return null;
  }

  double round2(double value) => (value * 100).round() / 100;

  final neighbors = [at(unit.gridX - 1, unit.gridY), at(unit.gridX + 1, unit.gridY), at(unit.gridX, unit.gridY - 1), at(unit.gridX, unit.gridY + 1)];

  for (final n in neighbors) {
    if (n != null && n.unitType == 'MANUFACTURING' && n.productTypeId != null) {
      final base = productBasePrices[n.productTypeId];
      if (base != null) {
        return B2BPriceSource(price: round2(base * cityFxRate), sourceType: 'manufacturing', itemName: productNames[n.productTypeId]);
      }
    }
  }
  for (final n in draftUnits) {
    if (n.unitType == 'MANUFACTURING' && n.productTypeId != null) {
      final base = productBasePrices[n.productTypeId];
      if (base != null) {
        return B2BPriceSource(price: round2(base * cityFxRate), sourceType: 'manufacturing', itemName: productNames[n.productTypeId]);
      }
    }
  }
  for (final n in neighbors) {
    if (n != null && n.unitType == 'MINING' && n.resourceTypeId != null) {
      final base = resourceBasePrices[n.resourceTypeId];
      if (base != null) {
        return B2BPriceSource(price: round2(base * cityFxRate), sourceType: 'mining', itemName: resourceNames[n.resourceTypeId]);
      }
    }
  }
  for (final n in draftUnits) {
    if (n.unitType == 'MINING' && n.resourceTypeId != null) {
      final base = resourceBasePrices[n.resourceTypeId];
      if (base != null) {
        return B2BPriceSource(price: round2(base * cityFxRate), sourceType: 'mining', itemName: resourceNames[n.resourceTypeId]);
      }
    }
  }
  return null;
}
