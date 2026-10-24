import { computed, ref, type Ref } from 'vue'
import {
  isMasterConnected,
  fetchMasterLayouts,
  saveMasterLayout,
  deleteMasterLayout,
  saveLocalLayout,
  deleteLocalLayout,
  getLocalLayoutsForType,
  type BuildingLayoutTemplate,
  type LayoutUnit,
} from '@/lib/masterLayoutApi'
import type { Building } from '@/types'
import { createBuildingScope } from '@/composables/buildingScope'
import type { EditableGridUnit } from '@/composables/useBuildingDetail'

/**
 * Layout templates (cloud and local) for the building edit grid: save the draft, list, apply and
 * delete. Extracted from useBuildingDetail (#596).
 */
export function useBuildingLayouts(deps: {
  building: Ref<Building | null>
  draftUnits: Ref<EditableGridUnit[]>
  t: (key: string, params?: Record<string, unknown>) => string
  /** When given, state is reset synchronously when the id changes and stale responses are dropped. */
  buildingId?: () => string | null | undefined
}) {
  const { building, draftUnits, t } = deps
  const scope = createBuildingScope(deps.buildingId, () => resetState())
  const layoutName = ref('')
  const layoutDescription = ref('')
  // Master-API layout state
  const masterLayouts = ref<BuildingLayoutTemplate[]>([])
  const masterLayoutsLoading = ref(false)
  const masterLayoutsError = ref<string | null>(null)
  const localLayouts = ref<BuildingLayoutTemplate[]>([])
  const layoutSaving = ref(false)
  const layoutSaveError = ref<string | null>(null)
  const layoutSaveSuccess = ref(false)
  const layoutDeleteError = ref<string | null>(null)
  const overwriteConfirmPending = ref<BuildingLayoutTemplate | null>(null)
  const masterConnected = computed(() => isMasterConnected())

  /** Extract serialisable unit data from the draft list. */
  function getDraftLayoutUnits(): LayoutUnit[] {
    return draftUnits.value.map((u) => ({
      unitType: u.unitType,
      gridX: u.gridX,
      gridY: u.gridY,
      linkUp: u.linkUp,
      linkDown: u.linkDown,
      linkLeft: u.linkLeft,
      linkRight: u.linkRight,
      linkUpLeft: u.linkUpLeft,
      linkUpRight: u.linkUpRight,
      linkDownLeft: u.linkDownLeft,
      linkDownRight: u.linkDownRight,
      resourceTypeId: u.resourceTypeId,
      productTypeId: u.productTypeId,
      minPrice: u.minPrice,
      maxPrice: u.maxPrice,
      purchaseSource: u.purchaseSource,
      saleVisibility: u.saleVisibility,
      budget: u.budget,
      mediaHouseBuildingId: u.mediaHouseBuildingId,
      minQuality: u.minQuality,
      brandScope: u.brandScope,
      vendorLockCompanyId: u.vendorLockCompanyId,
      lockedCityId: u.lockedCityId,
    }))
  }

  /** Refresh local layouts from localStorage into the reactive ref. */
  function refreshLocalLayouts(): void {
    if (!building.value) return
    localLayouts.value = getLocalLayoutsForType(building.value.type)
  }

  /** Fetch cloud layouts from master API (no-op when not connected). */
  async function refreshMasterLayouts(): Promise<void> {
    if (!masterConnected.value) {
      masterLayouts.value = []
      return
    }
    const gen = scope.token()
    masterLayoutsLoading.value = true
    masterLayoutsError.value = null
    try {
      const all = await scope.settle(gen, fetchMasterLayouts())
      masterLayouts.value = building.value ? all.filter((l) => l.buildingType === building.value!.type) : all
    } catch (err: unknown) {
      if (scope.isStaleError(err)) return
      masterLayoutsError.value = err instanceof Error ? err.message : String(err)
    } finally {
      if (!scope.isStale(gen)) masterLayoutsLoading.value = false
    }
  }

  /** Save current draft as a named layout template. */
  async function saveLayout(): Promise<void> {
    if (!building.value || !layoutName.value.trim()) return
    if (draftUnits.value.length === 0) {
      layoutSaveError.value = t('buildingDetail.layouts.noUnits')
      return
    }

    const gen = scope.token()
    layoutSaving.value = true
    layoutSaveError.value = null
    layoutSaveSuccess.value = false

    const name = layoutName.value.trim()
    const description = layoutDescription.value.trim() || null
    const buildingType = building.value.type
    const units = getDraftLayoutUnits()

    try {
      if (masterConnected.value) {
        // Find if an existing cloud layout with the same name exists so we can update it
        const existing = masterLayouts.value.find((l) => l.name === name)
        const saved = await scope.settle(gen, saveMasterLayout(name, description, buildingType, units, existing?.id))
        // Update local cache
        const idx = masterLayouts.value.findIndex((l) => l.id === saved.id)
        if (idx >= 0) {
          masterLayouts.value.splice(idx, 1, saved)
        } else {
          masterLayouts.value = [saved, ...masterLayouts.value]
        }
      } else {
        // Fallback: localStorage
        saveLocalLayout(name, description, buildingType, units)
        refreshLocalLayouts()
      }
      layoutName.value = ''
      layoutDescription.value = ''
      layoutSaveSuccess.value = true
      setTimeout(() => {
        if (!scope.isStale(gen)) layoutSaveSuccess.value = false
      }, 3000)
    } catch (err: unknown) {
      if (scope.isStaleError(err)) return
      // If cloud failed, persist locally and surface a fallback warning
      if (masterConnected.value) {
        try {
          saveLocalLayout(name, description, buildingType, units)
          refreshLocalLayouts()
          layoutSaveError.value = t('buildingDetail.layouts.masterError')
          layoutSaveSuccess.value = true
          setTimeout(() => {
            if (scope.isStale(gen)) return
            layoutSaveSuccess.value = false
            layoutSaveError.value = null
          }, 4000)
        } catch {
          layoutSaveError.value = err instanceof Error ? err.message : String(err)
        }
      } else {
        layoutSaveError.value = err instanceof Error ? err.message : String(err)
      }
    } finally {
      if (!scope.isStale(gen)) layoutSaving.value = false
    }
  }

  /** Confirm and apply a layout template to the current draft. */
  function requestLoadLayout(layout: BuildingLayoutTemplate): void {
    // Compatibility check: building types must match
    if (layout.buildingType !== building.value?.type) {
      layoutSaveError.value = t('buildingDetail.layouts.incompatible', {
        type: layout.buildingType,
        buildingType: building.value?.type ?? '?',
      })
      return
    }
    // If the draft is non-empty, ask for overwrite confirmation
    if (draftUnits.value.length > 0) {
      overwriteConfirmPending.value = layout
    } else {
      applyLayout(layout)
    }
  }

  function confirmOverwrite(): void {
    if (overwriteConfirmPending.value) {
      applyLayout(overwriteConfirmPending.value)
      overwriteConfirmPending.value = null
    }
  }

  function cancelOverwrite(): void {
    overwriteConfirmPending.value = null
  }

  function applyLayout(layout: BuildingLayoutTemplate): void {
    draftUnits.value = layout.units.map((u, i) => ({
      id: `layout-${i}-${Date.now()}`,
      ...u,
      lockedCityId: u.lockedCityId ?? null,
      industryCategory: u.industryCategory ?? null,
      level: 1,
    }))
  }

  /** Delete a layout template. */
  async function deleteLayout(layout: BuildingLayoutTemplate): Promise<void> {
    const gen = scope.token()
    layoutDeleteError.value = null
    try {
      if (!layout.isLocal && layout.id) {
        await scope.settle(gen, deleteMasterLayout(layout.id))
        masterLayouts.value = masterLayouts.value.filter((l) => l.id !== layout.id)
      } else {
        deleteLocalLayout(layout.name, layout.buildingType)
        refreshLocalLayouts()
      }
    } catch (err: unknown) {
      if (scope.isStaleError(err)) return
      layoutDeleteError.value = err instanceof Error ? err.message : String(err)
    }
  }

  /** Clears everything tied to one building; runs when the viewed building changes. */
  function resetState() {
    layoutName.value = ''
    layoutDescription.value = ''
    layoutSaving.value = false
    layoutSaveError.value = null
    layoutSaveSuccess.value = false
    layoutDeleteError.value = null
    overwriteConfirmPending.value = null
    masterLayouts.value = []
    masterLayoutsLoading.value = false
    masterLayoutsError.value = null
    localLayouts.value = []
  }

  return {
    layoutName,
    layoutDescription,
    masterLayouts,
    masterLayoutsLoading,
    masterLayoutsError,
    localLayouts,
    layoutSaving,
    layoutSaveError,
    layoutSaveSuccess,
    layoutDeleteError,
    overwriteConfirmPending,
    masterConnected,
    getDraftLayoutUnits,
    refreshLocalLayouts,
    refreshMasterLayouts,
    saveLayout,
    requestLoadLayout,
    confirmOverwrite,
    cancelOverwrite,
    applyLayout,
    deleteLayout,
  }
}
