<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import type { Building } from '@/types'
import { computeMiningEfficiencyFactor } from '@/lib/miningScarcity'
import MineExtractionHistoryPanel from './MineExtractionHistoryPanel.vue'

const { t } = useI18n()
const router = useRouter()

const props = defineProps<{
  building: Building
  /** Mining rate in units per tick derived from the active mining unit's level. */
  miningRatePerTick?: number | null
}>()

const remaining = computed(() => props.building.lotMaterialQuantity ?? null)
const original = computed(() => props.building.lotOriginalMaterialQuantity ?? null)
const resourceName = computed(() => {
  // Derive name from building name as fallback — composable provides resource name separately
  return props.building.name ?? 'Resource'
})

const isDepleted = computed(() => remaining.value !== null && remaining.value <= 0)

const remainingPercent = computed<number | null>(() => {
  if (remaining.value === null || original.value === null || original.value <= 0) return null
  return Math.round((remaining.value / original.value) * 100)
})

const efficiencyFactor = computed(() => computeMiningEfficiencyFactor(remaining.value, original.value))
const efficiencyPercent = computed(() => Math.round(efficiencyFactor.value * 100))

const barColor = computed(() => {
  const p = remainingPercent.value
  if (p === null) return 'bg-muted'
  if (p <= 0) return 'bg-error'
  if (p < 20) return 'bg-error'
  if (p < 50) return 'bg-warning'
  return 'bg-success'
})

const isDepletionRisk = computed(() => {
  const p = remainingPercent.value
  return p !== null && p > 0 && p < 20
})

const estimatedTicksToDepletion = computed<number | null>(() => {
  if (!props.miningRatePerTick || props.miningRatePerTick <= 0) return null
  if (remaining.value === null || remaining.value <= 0) return null
  const effectiveRate = props.miningRatePerTick * efficiencyFactor.value
  if (effectiveRate <= 0) return null
  return Math.ceil(remaining.value / effectiveRate)
})

const unitSymbol = computed(() => 't')

function navigateToBuyLot() {
  void router.push({ path: '/buy-building', query: { type: 'MINE' } })
}
</script>

<template>
  <div class="mining-resource-status-panel mt-4 rounded-lg border border-divider bg-card p-4">
    <h4 class="mb-3 text-sm font-semibold text-heading">{{ t('mining.resourceStatus') }}</h4>

    <!-- Depleted state -->
    <div v-if="isDepleted" class="depleted-state flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <span class="text-lg">❌</span>
        <span class="text-base font-bold text-error">{{ t('mining.depleted') }}</span>
      </div>
      <p class="text-sm text-muted">{{ t('mining.depletedDescription') }}</p>
      <div v-if="original !== null" class="grid grid-cols-2 gap-2 text-sm">
        <span class="text-muted">{{ t('mining.originalDeposit') }}</span>
        <span class="font-medium text-body">{{ original.toLocaleString() }} {{ unitSymbol }}</span>
        <span class="text-muted">{{ t('mining.extractedTotal') }}</span>
        <span class="font-medium text-body">{{ original.toLocaleString() }} {{ unitSymbol }}</span>
      </div>
      <button
        class="mt-1 w-full rounded-md border border-accent px-3 py-2 text-sm font-medium text-accent hover:bg-accent hover:text-white transition-colors"
        @click="navigateToBuyLot"
      >
        {{ t('mining.viewAvailableLots') }}
      </button>
    </div>

    <!-- Active deposit state -->
    <div v-else-if="remaining !== null && original !== null" class="active-deposit-state flex flex-col gap-3">
      <!-- Progress bar -->
      <div class="deposit-progress">
        <div class="mb-1 flex items-center justify-between text-xs text-muted">
          <span>{{ t('mining.depositProgress', { resource: resourceName }) }}</span>
          <span v-if="remainingPercent !== null" :class="remainingPercent < 20 ? 'text-error font-semibold' : 'text-body'">
            {{ t('mining.remaining', { percent: remainingPercent }) }}
          </span>
        </div>
        <div class="h-3 w-full overflow-hidden rounded-full bg-surface">
          <div
            class="deposit-progress-bar h-full rounded-full transition-all"
            :class="barColor"
            :style="{ width: `${Math.max(remainingPercent ?? 0, 0)}%` }"
          />
        </div>
      </div>

      <!-- Stats grid -->
        <div class="grid grid-cols-2 gap-y-1.5 text-sm">
        <span class="text-muted">{{ t('mining.remainingQuantity', { quantity: '', unit: '' }).trim() }}</span>
        <span class="font-medium text-body">
          {{ remaining.toLocaleString() }} {{ unitSymbol }}
        </span>

          <template v-if="miningRatePerTick">
            <span class="text-muted">{{ t('mining.extractionRate') }}</span>
            <span class="font-medium text-body">
              {{ t('mining.extractionRateValue', { rate: (miningRatePerTick * efficiencyFactor).toLocaleString(undefined, { maximumFractionDigits: 2 }), unit: unitSymbol }) }}
            </span>

            <span class="text-muted">{{ t('mining.efficiency') }}</span>
            <span class="font-medium text-body">
              {{ t('mining.efficiencyValue', { percent: efficiencyPercent }) }}
            </span>

            <template v-if="estimatedTicksToDepletion !== null">
              <span class="text-muted">{{ t('mining.estimatedDepletion') }}</span>
            <span class="font-medium text-body">
              {{ t('mining.estimatedDepletionValue', { ticks: estimatedTicksToDepletion.toLocaleString() }) }}
            </span>
          </template>
        </template>
      </div>

      <!-- Depletion risk warning -->
      <div
        v-if="isDepletionRisk"
        class="depletion-risk-badge flex items-center gap-2 rounded-md bg-warning/10 border border-warning/30 px-3 py-2 text-sm"
      >
        <span>⚠️</span>
        <div>
          <span class="font-semibold text-warning">{{ t('mining.depletionRisk') }}: </span>
          <span class="text-body">{{ t('mining.depletionRiskHint') }}</span>
        </div>
      </div>

      <!-- View lots link -->
      <button
        class="w-full rounded-md border border-divider px-3 py-1.5 text-xs text-muted hover:border-accent hover:text-accent transition-colors"
        @click="navigateToBuyLot"
      >
        {{ t('mining.viewAvailableLots') }}
      </button>

      <!-- Extraction history sparkline + dialog trigger -->
      <MineExtractionHistoryPanel :building="building" />
    </div>

    <!-- No deposit data -->
    <div v-else class="text-sm text-muted">
      {{ t('buildingDetail.mineOutputMissingLotResource') }}
    </div>
  </div>
</template>

<style scoped>
.deposit-progress-bar {
  min-width: 2px;
}
</style>
