import { getCurrentScope, onScopeDispose, ref, type Ref } from 'vue'
import { deserializeUnitConfig, serializeUnitConfig, type UnitClipboardConfig } from '@/lib/unitClipboard'
import type { EditableGridUnit } from '@/composables/useBuildingDetail'

/**
 * Copy / paste of a draft unit's configuration through the system clipboard (Ctrl+C / Ctrl+V).
 * Extracted from useBuildingDetail (#596).
 */
export function useBuildingClipboard(deps: {
  isEditing: Ref<boolean>
  selectedCell: Ref<{ x: number; y: number } | null>
  getDraftUnitAt: (x: number, y: number) => EditableGridUnit | null | undefined
  allowedUnits: Ref<string[]>
  placeUnit: (unitType: string) => void
  t: (key: string, params?: Record<string, unknown>) => string
}) {
  const { isEditing, selectedCell, getDraftUnitAt, allowedUnits, placeUnit, t } = deps
  const clipboardMessage = ref<string | null>(null)
  const clipboardMessageType = ref<'success' | 'error'>('success')
  let _clipboardMessageTimer: ReturnType<typeof setTimeout> | null = null

  // ---------------------------------------------------------------------------
  // Unit copy/paste (Ctrl+C / Ctrl+V)
  // ---------------------------------------------------------------------------

  if (getCurrentScope()) {
    onScopeDispose(() => {
      if (_clipboardMessageTimer !== null) clearTimeout(_clipboardMessageTimer)
    })
  }

  function showClipboardMessage(message: string, type: 'success' | 'error', durationMs = 3000) {
    if (_clipboardMessageTimer !== null) {
      clearTimeout(_clipboardMessageTimer)
    }
    clipboardMessage.value = message
    clipboardMessageType.value = type
    _clipboardMessageTimer = setTimeout(() => {
      clipboardMessage.value = null
      _clipboardMessageTimer = null
    }, durationMs)
  }

  /**
   * Copy the selected draft unit's configuration to the system clipboard as JSON.
   * Only configurable (non-structural) fields are copied.
   * No-op when not in editing mode or no cell is selected.
   */
  async function copySelectedUnit(): Promise<void> {
    if (!isEditing.value || !selectedCell.value) return
    const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
    if (!unit) {
      showClipboardMessage(t('buildingDetail.clipboard.noUnitToCopy'), 'error')
      return
    }
    try {
      const json = serializeUnitConfig(unit)
      await navigator.clipboard.writeText(json)
      showClipboardMessage(t('buildingDetail.clipboard.copySuccess'), 'success')
    } catch {
      showClipboardMessage(t('buildingDetail.clipboard.copyFailed'), 'error')
    }
  }

  /**
   * Paste a previously copied unit configuration onto the selected draft unit.
   * When pasting onto an occupied unit, the target type must match the copied type.
   * When pasting onto an empty slot, a new unit of the copied type is placed first
   * (provided that unit type is allowed in this building), then the config is applied.
   * No-op when not in editing mode or no cell is selected.
   */
  async function pasteToSelectedUnit(): Promise<void> {
    if (!isEditing.value || !selectedCell.value) return
    const { x, y } = selectedCell.value

    let text: string
    try {
      text = await navigator.clipboard.readText()
    } catch {
      showClipboardMessage(t('buildingDetail.clipboard.readFailed'), 'error')
      return
    }

    // The read can be slow (permission prompt); make sure the user is still on the same cell in edit mode.
    if (!isEditing.value || selectedCell.value?.x !== x || selectedCell.value?.y !== y) return

    const targetUnit = getDraftUnitAt(x, y)

    if (!targetUnit) {
      // Paste to empty slot: deserialize without type restriction, then place a new unit
      const result = deserializeUnitConfig(text)
      if (!result.ok) {
        switch (result.error) {
          case 'EMPTY':
            showClipboardMessage(t('buildingDetail.clipboard.empty'), 'error')
            break
          case 'INVALID_JSON':
            showClipboardMessage(t('buildingDetail.clipboard.invalidFormat'), 'error')
            break
          default:
            showClipboardMessage(t('buildingDetail.clipboard.schemaMismatch'), 'error')
            break
        }
        return
      }

      // Check whether the clipboard unit type is allowed in this building
      if (!allowedUnits.value.includes(result.config.unitType)) {
        showClipboardMessage(
          t('buildingDetail.clipboard.unitTypeNotAllowed', {
            type: t(`buildingDetail.unitTypes.${result.config.unitType}`),
          }),
          'error',
        )
        return
      }

      // Place a new unit of the correct type, then apply the copied config to it
      placeUnit(result.config.unitType)
      const newUnit = getDraftUnitAt(x, y)
      if (newUnit) {
        applyConfigToUnit(newUnit, result.config)
      }
      showClipboardMessage(
        t('buildingDetail.clipboard.pasteEmptySuccess', {
          type: t(`buildingDetail.unitTypes.${result.config.unitType}`),
        }),
        'success',
      )
      return
    }

    // Paste to occupied unit: the clipboard type must match the target type
    const result = deserializeUnitConfig(text, targetUnit.unitType)

    if (!result.ok) {
      switch (result.error) {
        case 'EMPTY':
          showClipboardMessage(t('buildingDetail.clipboard.empty'), 'error')
          break
        case 'INVALID_JSON':
          showClipboardMessage(t('buildingDetail.clipboard.invalidFormat'), 'error')
          break
        case 'SCHEMA_MISMATCH':
          showClipboardMessage(t('buildingDetail.clipboard.schemaMismatch'), 'error')
          break
        case 'INCOMPATIBLE_TYPE': {
          // Safely extract the source unitType from the raw JSON for a better error message
          let sourceUnitType = ''
          try {
            const raw = JSON.parse(text) as Record<string, unknown>
            if (typeof raw['unitType'] === 'string') sourceUnitType = raw['unitType']
          } catch {
            // ignore
          }
          const fromLabel = sourceUnitType ? t(`buildingDetail.unitTypes.${sourceUnitType}`) : ''
          const toLabel = t(`buildingDetail.unitTypes.${targetUnit.unitType}`)
          showClipboardMessage(fromLabel ? t('buildingDetail.clipboard.incompatibleType', { from: fromLabel, to: toLabel }) : t('buildingDetail.clipboard.schemaMismatch'), 'error')
          break
        }
      }
      return
    }

    applyConfigToUnit(targetUnit, result.config)
    showClipboardMessage(t('buildingDetail.clipboard.pasteSuccess'), 'success')
  }

  /** Apply all clipboard-configurable fields from `config` onto `unit`. */
  function applyConfigToUnit(unit: EditableGridUnit, config: UnitClipboardConfig): void {
    unit.resourceTypeId = config.resourceTypeId
    unit.productTypeId = config.productTypeId
    unit.minPrice = config.minPrice
    unit.maxPrice = config.maxPrice
    unit.purchaseSource = config.purchaseSource
    unit.saleVisibility = config.saleVisibility
    unit.budget = config.budget
    unit.mediaHouseBuildingId = config.mediaHouseBuildingId
    unit.minQuality = config.minQuality
    unit.brandScope = config.brandScope
    unit.vendorLockCompanyId = config.vendorLockCompanyId
    unit.lockedCityId = config.lockedCityId
    unit.industryCategory = config.industryCategory
    unit.lowInventoryAlertThreshold = config.lowInventoryAlertThreshold
  }

  return {
    clipboardMessage,
    clipboardMessageType,
    copySelectedUnit,
    pasteToSelectedUnit,
  }
}
