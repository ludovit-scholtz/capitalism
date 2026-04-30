<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{
  units: Array<{ id?: string; unitType: string }>
  icons: Record<string, string>
}>()

const { t } = useI18n()

function getUnitTypeLabel(unitType: string): string {
  const key = `buildingDetail.unitTypes.${unitType}`
  const translated = t(key)
  return translated === key ? unitType : translated
}
</script>

<template>
  <div class="unit-chain flex items-center flex-wrap gap-1.5">
    <template v-for="(unit, index) in units" :key="unit.id ?? `${unit.unitType}-${index}`">
      <div class="flex min-w-[80px] flex-col items-center gap-1 rounded-lg border border-divider bg-page px-3 py-2">
        <span class="unit-chain-icon text-xl">{{ icons[unit.unitType] ?? '▪️' }}</span>
        <span class="unit-chain-label text-center text-xs font-medium">{{ getUnitTypeLabel(unit.unitType) }}</span>
      </div>
      <span v-if="index < units.length - 1" class="unit-chain-arrow self-center text-muted" aria-hidden="true">→</span>
    </template>
  </div>
</template>
