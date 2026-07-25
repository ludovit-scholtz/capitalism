part of 'building_grid_draft_controller.dart';

/// `storeConfiguration()`/`cancelPlan()` — the actual save/cancel mutations,
/// plus the server error-code mapping. Mirrors `useBuildingDetail.ts`'s
/// `storeConfiguration`/`cancelPlan`: only `INSUFFICIENT_FUNDS`/
/// `MAX_CONCURRENT_UPGRADES`/`UNIT_ALREADY_UPGRADING` (all three raised by
/// the staged-upgrade `scheduleUnitUpgrade` calls in step 1, not by
/// `storeBuildingConfiguration` itself) get a translated message — every
/// other code (grid/link/product/media-house/pricing validation errors from
/// `BuildingConfigurationService.Validation.cs`) falls through to the raw
/// server message, exactly as on web.
extension BuildingGridDraftControllerSave on BuildingGridDraftController {
  /// Returns true on success (caller should reload the building). On
  /// failure, [saveError] is set and this returns false.
  Future<bool> storeConfiguration(BuildingDetailService service) async {
    final activeBuilding = building;
    if (activeBuilding == null || saving || !hasDraftChanges) return false;

    saving = true;
    saveError = null;
    notify();
    try {
      for (final unitId in draftUpgradeUnitIds.toList()) {
        await service.scheduleUnitUpgrade(unitId);
      }
      draftUpgradeUnitIds.clear();

      final hasStructuralChanges = !areUnitCollectionsEqual(draftUnits, editBaselineUnits);
      if (hasStructuralChanges) {
        await service.storeBuildingConfiguration(buildingId: activeBuilding.id, units: draftUnits);
      }

      isEditing = false;
      return true;
    } catch (error) {
      saveError = _mapSaveError(error);
      return false;
    } finally {
      saving = false;
      notify();
    }
  }

  Future<bool> cancelPlan(BuildingDetailService service) async {
    final activeBuilding = building;
    if (activeBuilding == null || cancellingPlan || activeBuilding.pendingConfiguration == null) return false;

    cancellingPlan = true;
    cancelPlanError = null;
    notify();
    try {
      await service.cancelBuildingConfiguration(activeBuilding.id);
      return true;
    } catch (error) {
      cancelPlanError = error is GraphQlException ? error.message : error.toString();
      return false;
    } finally {
      cancellingPlan = false;
      notify();
    }
  }

  String _mapSaveError(Object error) {
    if (error is GraphQlException) {
      switch (error.code) {
        case 'INSUFFICIENT_FUNDS':
          return 'Insufficient funds to complete the staged unit upgrade.';
        case 'MAX_CONCURRENT_UPGRADES':
          return 'This building already has 2 unit upgrades in progress. Wait for one to finish before starting another.';
        case 'UNIT_ALREADY_UPGRADING':
          return 'One of the staged units already has an upgrade in progress.';
        default:
          return error.message;
      }
    }
    return error.toString();
  }
}
