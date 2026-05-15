<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import SupplyChainDiagram from './SupplyChainDiagram.vue'
import type { BuildingSupplyChainDiagram } from '@/types'

const props = defineProps<{
  supplyChain: BuildingSupplyChainDiagram | null
  loading: boolean
}>()

const { t } = useI18n()

const healthColor = computed(() => {
  if (!props.supplyChain) return 'text-foreground'
  switch (props.supplyChain.healthScore) {
    case 'GREEN':
      return 'text-green-700 dark:text-green-300'
    case 'YELLOW':
      return 'text-yellow-700 dark:text-yellow-300'
    case 'RED':
      return 'text-red-700 dark:text-red-300'
    default:
      return 'text-foreground'
  }
})

const healthBgColor = computed(() => {
  if (!props.supplyChain) return 'border-divider bg-surface'
  switch (props.supplyChain.healthScore) {
    case 'GREEN':
      return 'border-green-200 bg-green-50 dark:border-green-400/30 dark:bg-green-500/10'
    case 'YELLOW':
      return 'border-yellow-200 bg-yellow-50 dark:border-yellow-400/30 dark:bg-yellow-500/10'
    case 'RED':
      return 'border-red-200 bg-red-50 dark:border-red-400/30 dark:bg-red-500/10'
    default:
      return 'border-divider bg-surface'
  }
})
</script>

<template>
  <div class="supply-chain-tab flex flex-col gap-4">
    <!-- Loading state -->
    <div v-if="loading" class="flex items-center justify-center py-8">
      <div class="text-sm text-muted">
        {{ t('common.loading') }}
      </div>
    </div>

    <!-- Empty state -->
    <div v-else-if="!props.supplyChain" class="flex items-center justify-center py-8">
      <div class="text-sm text-muted">
        {{ t('buildingDetail.noData') }}
      </div>
    </div>

    <!-- Supply Chain Content -->
    <template v-else>
      <!-- Health Status Header -->
      <div :class="['flex items-center justify-between rounded-lg border px-4 py-3', healthBgColor]">
        <div>
          <p class="text-sm font-medium text-foreground">
            {{ t('buildingDetail.supplyChain.healthStatus') }}
          </p>
          <p :class="['text-lg font-bold', healthColor]">
            {{ t(`buildingDetail.supplyChain.health.${props.supplyChain.healthScore}`) }}
          </p>
          <p class="text-sm text-muted">{{ props.supplyChain.healthReason }}</p>
        </div>
        <div class="text-center">
          <div :class="['text-3xl font-bold', healthColor]">
            {{ props.supplyChain.units.length }}
          </div>
          <p class="text-xs text-muted">{{ t('buildingDetail.supplyChain.units') }}</p>
        </div>
      </div>

      <!-- Supply Chain Diagram -->
      <div v-if="!loading" class="flex justify-center">
        <SupplyChainDiagram :diagram="props.supplyChain" />
      </div>

      <div v-if="loading" class="flex items-center justify-center py-8">
        <div class="text-sm text-muted">
          {{ t('common.loading') }}
        </div>
      </div>

      <!-- Unit Details -->
      <div v-if="!loading && props.supplyChain.units.length > 0" class="mt-4">
        <h4 class="mb-3 text-sm font-semibold text-foreground">
          {{ t('buildingDetail.supplyChain.unitDetails') }}
        </h4>
        <div class="space-y-2">
          <div
            v-for="unit in props.supplyChain.units"
            :key="unit.buildingUnitId"
            class="flex items-center justify-between rounded border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-900 dark:border-gray-700 dark:bg-gray-800/80 dark:text-gray-100"
          >
            <div>
              <span class="font-medium">{{ unit.unitType }}</span>
              <span class="ml-2 text-gray-600 dark:text-gray-300">({{ unit.gridX }}, {{ unit.gridY }})</span>
            </div>
            <div class="flex items-center gap-4">
              <div class="text-right">
                <div class="font-medium">{{ unit.fillPercent.toFixed(0) }}%</div>
                <div class="text-xs text-gray-500 dark:text-gray-400">{{ t('buildingDetail.inventory.fill') }}</div>
              </div>
              <div
                :class="[
                  'rounded px-2 py-1 text-xs font-semibold',
                  unit.status === 'ACTIVE'
                    ? 'bg-green-100 text-green-800 dark:bg-green-500/15 dark:text-green-300'
                    : unit.status === 'BLOCKED'
                      ? 'bg-red-100 text-red-800 dark:bg-red-500/15 dark:text-red-300'
                      : unit.status === 'FULL'
                        ? 'bg-blue-100 text-blue-800 dark:bg-blue-500/15 dark:text-blue-300'
                        : 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-200',
                ]"
              >
                {{ unit.status }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.supply-chain-tab {
  max-height: 600px;
  overflow-y: auto;
}
</style>
