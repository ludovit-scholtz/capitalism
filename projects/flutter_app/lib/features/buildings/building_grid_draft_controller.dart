// Draft/active grid state machine for the Building Detail grid editor —
// port of the editing-related state and functions in
// `projects/frontend/src/composables/useBuildingDetail.ts`. Split into
// `part` files (mirrors this repo's C# `partial class` convention for
// staying under the 500-line-per-file guideline) since the full editing
// surface (draft state, links, save/cancel, summaries) is one cohesive
// state machine that would otherwise be one very large file:
// - this file: lifecycle (start/cancel editing), place/remove unit,
//   starter layouts, cell selection, staged-upgrade bookkeeping.
// - `building_grid_draft_controller_links.dart`: the 8-directional link
//   toggle wrappers over `building_link_helpers.dart`.
// - `building_grid_draft_controller_summary.dart`: draft-vs-baseline
//   diffing (`hasDraftChanges`, `draftLinkChanges`, `draftUnitChanges`,
//   `draftConstructionCost`, `draftTotalTicks`,
//   `projectedCompanyCashAfterApply`).
// - `building_grid_draft_controller_save.dart`: `storeConfiguration`/
//   `cancelPlan` (the actual mutations) and server-error-code mapping.
//
// Trims from the web (documented, not oversights):
// - No confirmation dialog on cancel-with-unsaved-changes — the web itself
//   has none either (confirmed against `BuildingUnitGrid.vue`/the
//   composable), so there is nothing to port here.
// - `draftTotalTicks` does not replicate the web's "editing an
//   already-pending plan" branches of `getDraftTicksForUnit` (reverting a
//   still-in-flight addition/removal back to 10%-rollback timing) — those
//   only matter when a plan is already queued AND the player edits it
//   again before it applies, a narrow edge case. The common case (editing
//   from the live active grid) is fully faithful.

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../../core/graphql/graphql_service.dart';
import 'building_detail_models.dart';
import 'building_detail_service.dart';
import 'building_grid_models.dart';
import 'building_link_helpers.dart';
import 'building_unit_clipboard.dart';
import 'building_unit_economics.dart';

part 'building_grid_draft_controller_links.dart';
part 'building_grid_draft_controller_summary.dart';
part 'building_grid_draft_controller_save.dart';

typedef GridCell = ({int x, int y});

/// Mirrors `allowedUnitsMap` in `useBuildingDetail.ts`, identical to the
/// server's `BuildingConfigurationService.GetAllowedUnitTypes`.
const Map<String, List<String>> allowedUnitsMap = {
  'MINE': ['MINING', 'STORAGE', 'B2B_SALES'],
  'FACTORY': ['PURCHASE', 'MANUFACTURING', 'BRANDING', 'STORAGE', 'B2B_SALES'],
  'SALES_SHOP': ['PURCHASE', 'MARKETING', 'STORAGE', 'PUBLIC_SALES'],
  'RESEARCH_DEVELOPMENT': ['PRODUCT_QUALITY', 'BRAND_QUALITY'],
  'POWER_PLANT': ['POWER_GENERATION', 'BATTERY_STORAGE', 'FUEL_PURCHASE', 'WIND_TURBINE', 'WATER_TURBINE', 'ENERGY_STORAGE', 'ENERGY_PRODUCING'],
};

List<String> allowedUnitTypesFor(String buildingType) => allowedUnitsMap[buildingType] ?? const [];

class BuildingGridDraftController extends ChangeNotifier {
  // Extension methods (the `part` files below) aren't "instance members of
  // a ChangeNotifier subclass" as far as the analyzer's `@protected` check
  // is concerned, even though they share this file's library scope — so
  // they call this public wrapper instead of `notifyListeners()` directly.
  void notify() => notifyListeners();

  BuildingDetail? building;
  BuildingCatalog catalog = const BuildingCatalog(resourceNames: {}, productNames: {}, resourceBasePrices: {}, productBasePrices: {});
  double companyCash = 0;
  bool hasCompanyCash = false;

  List<EditableGridUnit> activeUnits = [];
  List<EditableGridUnit> pendingUnits = [];
  List<EditableGridUnit> draftUnits = [];
  List<EditableGridUnit> editBaselineUnits = [];

  bool isEditing = false;
  GridCell? selectedCell;
  bool showUnitPicker = false;
  String? saveError;
  bool saving = false;
  bool cancellingPlan = false;
  String? cancelPlanError;
  final Set<String> draftUpgradeUnitIds = {};

  /// Resets all draft/edit state from a freshly (re)loaded building —
  /// call after every `fetchBuilding()`.
  void loadFrom(BuildingDetail newBuilding, {required BuildingCatalog catalog, double? companyCash}) {
    building = newBuilding;
    this.catalog = catalog;
    if (companyCash != null) {
      this.companyCash = companyCash;
      hasCompanyCash = true;
    }
    activeUnits = newBuilding.units.map(EditableGridUnit.fromActive).toList();
    pendingUnits = (newBuilding.pendingConfiguration?.units ?? const []).map(EditableGridUnit.fromPending).toList();
    isEditing = false;
    draftUnits = [];
    editBaselineUnits = [];
    selectedCell = null;
    showUnitPicker = false;
    saveError = null;
    cancelPlanError = null;
    draftUpgradeUnitIds.clear();
    notifyListeners();
  }

  double get cityFxRate => building?.cityFxRate ?? 1;

  List<EditableGridUnit> get _editingSourceUnits => pendingUnits.isNotEmpty ? pendingUnits : activeUnits;

  EditableGridUnit? _unitAt(List<EditableGridUnit> units, int x, int y) {
    for (final u in units) {
      if (u.gridX == x && u.gridY == y) return u;
    }
    return null;
  }

  EditableGridUnit? activeUnitAt(int x, int y) => _unitAt(activeUnits, x, y);
  EditableGridUnit? draftUnitAt(int x, int y) => _unitAt(draftUnits, x, y);

  void startEditing() {
    final source = _editingSourceUnits;
    draftUnits = source.map((u) => u.clone()).toList();
    editBaselineUnits = source.map((u) => u.clone()).toList();
    isEditing = true;
    selectedCell = null;
    showUnitPicker = false;
    saveError = null;
    notifyListeners();
  }

  void cancelEditing() {
    final source = _editingSourceUnits;
    draftUnits = source.map((u) => u.clone()).toList();
    editBaselineUnits = source.map((u) => u.clone()).toList();
    isEditing = false;
    selectedCell = null;
    showUnitPicker = false;
    saveError = null;
    draftUpgradeUnitIds.clear();
    notifyListeners();
  }

  /// Clicking a grid cell in edit mode — opens the unit picker for an
  /// empty cell, or just selects an occupied one (for the config sheet).
  void clickDraftCell(int x, int y) {
    if (!isEditing) return;
    final existing = draftUnitAt(x, y);
    selectedCell = (x: x, y: y);
    showUnitPicker = existing == null;
    notifyListeners();
  }

  void closeUnitPicker() {
    showUnitPicker = false;
    notifyListeners();
  }

  List<String> get allowedUnitTypes => building == null ? const [] : allowedUnitTypesFor(building!.type);

  void placeUnit(String unitType) {
    final target = selectedCell;
    if (target == null || !isEditing) return;

    final active = activeUnitAt(target.x, target.y);
    final newUnit = EditableGridUnit(
      id: 'draft-${target.x}-${target.y}-${DateTime.now().microsecondsSinceEpoch}',
      unitType: unitType,
      gridX: target.x,
      gridY: target.y,
      level: active?.level ?? 1,
    );

    if (unitType == 'B2B_SALES') {
      newUnit.saleVisibility = 'GROUP';
      final suggestion = getB2BPriceSource(
        newUnit,
        draftUnits,
        resourceBasePrices: catalog.resourceBasePrices,
        productBasePrices: catalog.productBasePrices,
        resourceNames: catalog.resourceNames,
        productNames: catalog.productNames,
        cityFxRate: cityFxRate,
      );
      if (suggestion != null) newUnit.minPrice = suggestion.price;
    }

    draftUnits = [...draftUnits.where((u) => !(u.gridX == target.x && u.gridY == target.y)), newUnit];
    selectedCell = target;
    showUnitPicker = false;
    notifyListeners();
  }

  void removeDraftUnit(int x, int y) {
    if (!isEditing) return;
    clearConnectionsAround(draftUnits, x, y);
    final removed = draftUnitAt(x, y);
    draftUnits = draftUnits.where((u) => !(u.gridX == x && u.gridY == y)).toList();
    // Deliberate improvement over the web: removing a unit that had a
    // staged level-upgrade also drops it from the staged-upgrade set,
    // rather than silently firing `scheduleUnitUpgrade` on save for a unit
    // that's about to be deleted from the plan.
    if (removed != null) draftUpgradeUnitIds.remove(removed.id);
    selectedCell = null;
    showUnitPicker = false;
    notifyListeners();
  }

  void toggleStagedUpgrade(String unitId) {
    if (draftUpgradeUnitIds.contains(unitId)) {
      draftUpgradeUnitIds.remove(unitId);
    } else {
      draftUpgradeUnitIds.add(unitId);
    }
    notifyListeners();
  }

  /// FACTORY starter layout: Purchase(0,0) -> Manufacturing(1,0) ->
  /// Storage(2,0) -> B2B Sales(3,0). Populates the draft only — the player
  /// still saves explicitly.
  void applyStarterLayout() {
    startEditing();
    draftUnits = [
      EditableGridUnit(id: 'draft-starter-0-0', unitType: 'PURCHASE', gridX: 0, gridY: 0, linkRight: true),
      EditableGridUnit(id: 'draft-starter-1-0', unitType: 'MANUFACTURING', gridX: 1, gridY: 0, linkRight: true),
      EditableGridUnit(id: 'draft-starter-2-0', unitType: 'STORAGE', gridX: 2, gridY: 0, linkRight: true),
      EditableGridUnit(id: 'draft-starter-3-0', unitType: 'B2B_SALES', gridX: 3, gridY: 0, saleVisibility: 'GROUP'),
    ];
    editBaselineUnits = [];
    notifyListeners();
  }

  /// SALES_SHOP starter layout: Purchase(0,0) -> Public Sales(1,0).
  void applyShopStarterLayout() {
    startEditing();
    draftUnits = [
      EditableGridUnit(id: 'draft-shop-starter-0-0', unitType: 'PURCHASE', gridX: 0, gridY: 0, linkRight: true),
      EditableGridUnit(id: 'draft-shop-starter-1-0', unitType: 'PUBLIC_SALES', gridX: 1, gridY: 0),
    ];
    editBaselineUnits = [];
    notifyListeners();
  }

  Future<void> copySelectedUnit() async {
    if (!isEditing || selectedCell == null) return;
    final unit = draftUnitAt(selectedCell!.x, selectedCell!.y);
    if (unit == null) return;
    await Clipboard.setData(ClipboardData(text: serializeUnitConfig(unit)));
  }

  /// Returns a [ClipboardParseError] on failure, or `null` on success.
  Future<ClipboardParseError?> pasteToSelectedUnit() async {
    if (!isEditing || selectedCell == null) return null;
    final data = await Clipboard.getData(Clipboard.kTextPlain);
    final target = draftUnitAt(selectedCell!.x, selectedCell!.y);

    if (target == null) {
      final result = deserializeUnitConfig(data?.text);
      if (!result.isOk) return result.error;
      if (!allowedUnitTypes.contains(result.config!.unitType)) return ClipboardParseError.incompatibleType;
      placeUnit(result.config!.unitType);
      final placed = draftUnitAt(selectedCell!.x, selectedCell!.y);
      if (placed != null) applyConfigToUnit(placed, result.config!);
      notifyListeners();
      return null;
    }

    final result = deserializeUnitConfig(data?.text, targetUnitType: target.unitType);
    if (!result.isOk) return result.error;
    applyConfigToUnit(target, result.config!);
    notifyListeners();
    return null;
  }
}
