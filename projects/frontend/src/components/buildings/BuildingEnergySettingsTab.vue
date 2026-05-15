<script setup lang="ts">
import { computed, inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import BuildingEnergyPanel from '@/components/buildings/BuildingEnergyPanel.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, selectedCell, plannedUnits, getUnitAtFrom } = bd

const selectedUnit = computed(() => {
  const cell = selectedCell.value
  if (!cell) return null

  return getUnitAtFrom(plannedUnits.value, cell.x, cell.y)
})

const selectedUnitLabel = computed(() => {
  if (!selectedUnit.value) return null
  return t(`buildingDetail.unitTypes.${selectedUnit.value.unitType}`)
})
</script>

<template>
  <div class="energy-settings-tab mt-3 space-y-4">
    <div class="energy-settings-context rounded-xl border border-divider bg-card p-4" role="note">
      <div class="flex flex-wrap items-center gap-2">
        <span v-if="building?.name" class="inline-flex items-center rounded-full border border-divider bg-surface px-3 py-1 text-xs font-semibold uppercase tracking-wide text-muted">
          {{ building.name }}
        </span>
        <span v-if="selectedUnitLabel" class="inline-flex items-center rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-primary">
          {{ selectedUnitLabel }}
        </span>
        <span v-if="selectedCell" class="inline-flex items-center rounded-full border border-divider bg-surface px-3 py-1 text-xs font-semibold uppercase tracking-wide text-muted">
          {{ t('buildingDetail.gridPosition', { x: selectedCell.x, y: selectedCell.y }) }}
        </span>
      </div>
    </div>

    <BuildingEnergyPanel />
  </div>
</template>
