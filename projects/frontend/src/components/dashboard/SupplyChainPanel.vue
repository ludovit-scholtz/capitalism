<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BuildingUnit, BuildingUnitOperationalStatus } from '@/types'

interface Props {
  units: BuildingUnit[]
  /** Optional per-unit operational statuses fetched from buildingUnitOperationalStatuses query. */
  statuses?: BuildingUnitOperationalStatus[]
}

const props = defineProps<Props>()
const { t } = useI18n()

const UNIT_TYPE_ICONS: Record<string, string> = {
  PURCHASE: '🛒',
  MINING: '⛏️',
  MANUFACTURING: '⚙️',
  STORAGE: '📦',
  B2B_SALES: '🤝',
  PUBLIC_SALES: '🏷️',
  BRANDING: '🎨',
  MARKETING: '📢',
  PRODUCT_QUALITY: '🔬',
  BRAND_QUALITY: '⭐',
}

/** Sort units left to right (by gridX) to form the visual chain. */
const chainUnits = computed<BuildingUnit[]>(() => {
  if (props.units.length === 0) return []
  return [...props.units].sort((a, b) => a.gridX - b.gridX || a.gridY - b.gridY)
})

const statusMap = computed<Record<string, BuildingUnitOperationalStatus>>(() => {
  const map: Record<string, BuildingUnitOperationalStatus> = {}
  for (const s of props.statuses ?? []) {
    map[s.buildingUnitId] = s
  }
  return map
})

function unitIcon(unitType: string): string {
  return UNIT_TYPE_ICONS[unitType] ?? '🔲'
}

function unitLabel(unitType: string): string {
  const key = `supplyChain.unitTypes.${unitType}` as Parameters<typeof t>[0]
  return t(key)
}

function unitStatusClass(unitId: string): string {
  const s = statusMap.value[unitId]
  if (!s) return ''
  return `unit-node--${s.status.toLowerCase()}`
}

function unitStatusBadge(unitId: string): string {
  const s = statusMap.value[unitId]
  if (!s || s.status === 'ACTIVE') return ''
  const badges: Record<string, string> = {
    IDLE: '💤',
    BLOCKED: '⛔',
    FULL: '📊',
    UNCONFIGURED: '❓',
  }
  return badges[s.status] ?? ''
}

function unitStatusTitle(unitId: string): string {
  const s = statusMap.value[unitId]
  if (!s) return ''
  if (s.blockedReason) return s.blockedReason
  return s.status
}
</script>

<template>
  <div class="supply-chain-panel mt-3 rounded-md border border-divider bg-white/5 px-4 py-3" :aria-label="t('supplyChain.title')">
    <h4 class="supply-chain-title mb-2 text-xs font-semibold uppercase tracking-[0.06em] text-muted">{{ t('supplyChain.title') }}</h4>
    <div v-if="chainUnits.length === 0" class="supply-chain-empty text-[0.8125rem] italic text-muted">
      {{ t('supplyChain.empty') }}
    </div>
    <div v-else class="supply-chain-flow flex flex-wrap items-center gap-1 max-[640px]:gap-0.5" role="list">
      <template v-for="(unit, index) in chainUnits" :key="unit.id">
        <div
          class="unit-node relative flex min-w-16 flex-col items-center gap-0.5 rounded-sm border border-divider bg-white/5 px-2 py-1.5 max-[640px]:min-w-12 max-[640px]:px-1.5 max-[640px]:py-1"
          :class="unitStatusClass(unit.id)"
          :title="unitStatusTitle(unit.id)"
          role="listitem"
        >
          <span class="unit-icon text-lg leading-none" :aria-hidden="true">{{ unitIcon(unit.unitType) }}</span>
          <span class="unit-label text-center text-[0.6875rem] font-medium text-muted whitespace-nowrap max-[640px]:text-[0.625rem]">{{ unitLabel(unit.unitType) }}</span>
          <span v-if="unitStatusBadge(unit.id)" class="unit-status-badge text-[0.625rem] leading-none" :aria-label="statusMap[unit.id]?.status">
            {{ unitStatusBadge(unit.id) }}
          </span>
        </div>
        <span v-if="index < chainUnits.length - 1" class="unit-arrow shrink-0 text-base text-muted" aria-hidden="true">→</span>
      </template>
    </div>
  </div>
</template>

<style scoped>
.unit-node--active {
  border-color: var(--color-secondary);
  background: rgba(0, 200, 83, 0.06);
}

.unit-node--blocked {
  border-color: var(--color-danger);
  background: rgba(248, 113, 113, 0.06);
}

.unit-node--full {
  border-color: var(--color-primary);
  background: rgba(0, 71, 255, 0.06);
}

.unit-node--idle {
  opacity: 0.7;
}

.unit-node--unconfigured {
  opacity: 0.5;
  border-style: dashed;
}
</style>
