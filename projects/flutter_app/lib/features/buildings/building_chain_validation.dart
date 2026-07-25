// Pure port of `projects/frontend/src/lib/buildingChainValidation.ts` plus
// the `configWarnings` computed from `useBuildingDetail.ts`. Two distinct,
// independent mechanisms, kept separate here exactly as on web:
//
// - [configWarnings]: real BFS link-graph reachability + per-field
//   "not configured yet" checks, shown as a flat warning list in both view
//   and edit mode (never dismissible).
// - [getProductionChainStatus]/[getShopChainStatus]: simple presence-only
//   checks (ignores link connectivity entirely) feeding the dismissible
//   "chain ready" status panel, shown only in view mode.

import 'building_grid_models.dart';

class ChainWarning {
  const ChainWarning(this.message);
  final String message;
}

List<EditableGridUnit> _linkedUnits(EditableGridUnit unit, List<EditableGridUnit> units) {
  EditableGridUnit? at(int x, int y) {
    for (final u in units) {
      if (u.gridX == x && u.gridY == y) return u;
    }
    return null;
  }

  final linked = <EditableGridUnit>[];
  void addIfLinked(bool flag, int dx, int dy) {
    if (!flag) return;
    final neighbor = at(unit.gridX + dx, unit.gridY + dy);
    if (neighbor != null) linked.add(neighbor);
  }

  addIfLinked(unit.linkUp, 0, -1);
  addIfLinked(unit.linkDown, 0, 1);
  addIfLinked(unit.linkLeft, -1, 0);
  addIfLinked(unit.linkRight, 1, 0);
  addIfLinked(unit.linkUpLeft, -1, -1);
  addIfLinked(unit.linkUpRight, 1, -1);
  addIfLinked(unit.linkDownLeft, -1, 1);
  addIfLinked(unit.linkDownRight, 1, 1);
  return linked;
}

/// BFS from [sourceUnit] over its outgoing link flags; true as soon as a
/// [terminalUnitTypes] member is reached. Traversal only continues through
/// [passthroughUnitTypes] members — everything else is a dead end even if
/// linked.
bool hasReachableOutputPath(
  EditableGridUnit sourceUnit,
  List<EditableGridUnit> units, {
  List<String> passthroughUnitTypes = const [],
  required List<String> terminalUnitTypes,
}) {
  final passthrough = passthroughUnitTypes.toSet();
  final terminal = terminalUnitTypes.toSet();
  final queue = <EditableGridUnit>[sourceUnit];
  final visited = <String>{};
  var readIndex = 0;
  while (readIndex < queue.length) {
    final current = queue[readIndex++];
    if (visited.contains(current.id)) continue;
    visited.add(current.id);
    for (final next in _linkedUnits(current, units)) {
      if (terminal.contains(next.unitType)) return true;
      if (!visited.contains(next.id) && passthrough.contains(next.unitType)) queue.add(next);
    }
  }
  return false;
}

/// Flat list of configuration/reachability problems for [units] (already
/// resolved by the caller to whichever set is "current" — draft while
/// editing, else pending-plan-if-any, else active).
List<ChainWarning> getConfigWarnings(String buildingType, List<EditableGridUnit> units) {
  final warnings = <ChainWarning>[];

  Iterable<EditableGridUnit> ofType(String type) => units.where((u) => u.unitType == type);

  for (final u in ofType('PURCHASE')) {
    if (u.resourceTypeId == null && u.productTypeId == null) {
      warnings.add(ChainWarning('Purchase unit at (${u.gridX}, ${u.gridY}) has no resource or product selected.'));
    }
  }
  for (final u in ofType('MANUFACTURING')) {
    if (u.productTypeId == null) {
      warnings.add(ChainWarning('Manufacturing unit at (${u.gridX}, ${u.gridY}) has no product type set.'));
    }
  }
  for (final u in ofType('PUBLIC_SALES')) {
    if (u.resourceTypeId == null && u.productTypeId == null) {
      warnings.add(ChainWarning('Public Sales unit at (${u.gridX}, ${u.gridY}) has no item configured.'));
    }
  }
  for (final u in ofType('MARKETING')) {
    if (u.budget == null) {
      warnings.add(ChainWarning('Marketing unit at (${u.gridX}, ${u.gridY}) has no budget set.'));
    }
    if (u.mediaHouseBuildingId == null) {
      warnings.add(ChainWarning('Marketing unit at (${u.gridX}, ${u.gridY}) has no media house selected.'));
    }
  }
  for (final u in ofType('BRANDING')) {
    if (u.brandScope == null) {
      warnings.add(ChainWarning('Branding unit at (${u.gridX}, ${u.gridY}) has no brand scope set.'));
    }
  }
  for (final u in ofType('PRODUCT_QUALITY')) {
    if (u.productTypeId == null) {
      warnings.add(ChainWarning('Product Quality unit at (${u.gridX}, ${u.gridY}) has no researched product selected.'));
    }
  }
  for (final u in ofType('BRAND_QUALITY')) {
    if (u.brandScope == null) {
      warnings.add(ChainWarning('Brand Quality unit at (${u.gridX}, ${u.gridY}) has no research scope set.'));
    } else if (u.brandScope != 'COMPANY' && u.productTypeId == null && u.industryCategory == null) {
      warnings.add(ChainWarning('Brand Quality unit at (${u.gridX}, ${u.gridY}) needs an anchor product for product/category research.'));
    }
  }

  if (buildingType == 'FACTORY') {
    for (final pu in ofType('PURCHASE')) {
      final linked = _linkedUnits(pu, units).map((u) => u.unitType);
      if (!linked.any((t) => t == 'MANUFACTURING' || t == 'STORAGE')) {
        warnings.add(ChainWarning('Purchase unit at (${pu.gridX}, ${pu.gridY}) is not linked to a consumer unit.'));
      }
    }
    for (final mu in ofType('MANUFACTURING')) {
      final hasOutput = hasReachableOutputPath(
        mu,
        units,
        passthroughUnitTypes: const ['MANUFACTURING'],
        terminalUnitTypes: const ['STORAGE', 'B2B_SALES', 'PUBLIC_SALES'],
      );
      if (!hasOutput) {
        warnings.add(ChainWarning('Manufacturing unit at (${mu.gridX}, ${mu.gridY}) is not linked to a storage or sales output.'));
      }
    }
  }

  if (buildingType == 'MINE') {
    for (final mu in ofType('MINING')) {
      final hasOutput = hasReachableOutputPath(mu, units, terminalUnitTypes: const ['STORAGE', 'B2B_SALES', 'PUBLIC_SALES']);
      if (!hasOutput) {
        warnings.add(ChainWarning('Mining unit at (${mu.gridX}, ${mu.gridY}) is not linked to a storage or sales output.'));
      }
    }
  }

  if (buildingType == 'SALES_SHOP') {
    for (final pu in ofType('PURCHASE')) {
      final linked = _linkedUnits(pu, units).map((u) => u.unitType);
      if (!linked.any((t) => t == 'PUBLIC_SALES' || t == 'MARKETING' || t == 'STORAGE')) {
        warnings.add(ChainWarning('Purchase unit at (${pu.gridX}, ${pu.gridY}) is not linked to a consumer unit.'));
      }
    }
  }

  for (final su in ofType('STORAGE')) {
    if (_linkedUnits(su, units).isEmpty) {
      warnings.add(ChainWarning('Storage unit at (${su.gridX}, ${su.gridY}) is not linked to any other unit.'));
    }
  }

  return warnings;
}

/// Simple presence-only "is a starter production chain configured" status —
/// ignores link connectivity and storage entirely, matching the web's
/// `getProductionChainStatus`.
class ProductionChainStatus {
  const ProductionChainStatus({
    required this.purchase,
    required this.manufacturing,
    required this.storage,
    required this.isPurchaseConfigured,
    required this.isManufacturingConfigured,
    required this.isChainComplete,
  });

  final EditableGridUnit? purchase;
  final EditableGridUnit? manufacturing;
  final EditableGridUnit? storage;
  final bool isPurchaseConfigured;
  final bool isManufacturingConfigured;
  final bool isChainComplete;
}

ProductionChainStatus getProductionChainStatus(List<EditableGridUnit> units) {
  EditableGridUnit? find(String type) {
    for (final u in units) {
      if (u.unitType == type) return u;
    }
    return null;
  }

  final purchase = find('PURCHASE');
  final manufacturing = find('MANUFACTURING');
  final storage = find('STORAGE');
  final isPurchaseConfigured = purchase != null && (purchase.resourceTypeId != null || purchase.productTypeId != null);
  final isManufacturingConfigured = manufacturing != null && manufacturing.productTypeId != null;
  return ProductionChainStatus(
    purchase: purchase,
    manufacturing: manufacturing,
    storage: storage,
    isPurchaseConfigured: isPurchaseConfigured,
    isManufacturingConfigured: isManufacturingConfigured,
    isChainComplete: isPurchaseConfigured && isManufacturingConfigured,
  );
}

class ShopChainStatus {
  const ShopChainStatus({
    required this.purchase,
    required this.publicSales,
    required this.isPurchaseConfigured,
    required this.isPublicSalesConfigured,
    required this.isChainComplete,
  });

  final EditableGridUnit? purchase;
  final EditableGridUnit? publicSales;
  final bool isPurchaseConfigured;
  final bool isPublicSalesConfigured;
  final bool isChainComplete;
}

ShopChainStatus getShopChainStatus(List<EditableGridUnit> units) {
  EditableGridUnit? find(String type) {
    for (final u in units) {
      if (u.unitType == type) return u;
    }
    return null;
  }

  final purchase = find('PURCHASE');
  final publicSales = find('PUBLIC_SALES');
  final isPurchaseConfigured = purchase != null && (purchase.resourceTypeId != null || purchase.productTypeId != null);
  final isPublicSalesConfigured = publicSales != null && publicSales.productTypeId != null && publicSales.minPrice != null;
  return ShopChainStatus(
    purchase: purchase,
    publicSales: publicSales,
    isPurchaseConfigured: isPurchaseConfigured,
    isPublicSalesConfigured: isPublicSalesConfigured,
    isChainComplete: isPurchaseConfigured && isPublicSalesConfigured,
  );
}
