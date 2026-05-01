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
  if (!props.supplyChain) return 'text-gray-600'
  switch (props.supplyChain.healthScore) {
    case 'GREEN':
      return 'text-green-600'
    case 'YELLOW':
      return 'text-yellow-600'
    case 'RED':
      return 'text-red-600'
    default:
      return 'text-gray-600'
  }
})

const healthBgColor = computed(() => {
  if (!props.supplyChain) return 'bg-gray-100'
  switch (props.supplyChain.healthScore) {
    case 'GREEN':
      return 'bg-green-100'
    case 'YELLOW':
      return 'bg-yellow-100'
    case 'RED':
      return 'bg-red-100'
    default:
      return 'bg-gray-100'
  }
})
</script>

<template>
  <div class="supply-chain-tab flex flex-col gap-4">
    <!-- Loading state -->
    <div v-if="loading" class="flex items-center justify-center py-8">
      <div class="text-sm text-gray-500">
        {{ t('common.loading') }}
      </div>
    </div>

    <!-- Empty state -->
    <div v-else-if="!props.supplyChain" class="flex items-center justify-center py-8">
      <div class="text-sm text-gray-500">
        {{ t('buildingDetail.noData') }}
      </div>
    </div>

    <!-- Supply Chain Content -->
    <template v-else>
      <!-- Health Status Header -->
      <div
        :class="[
          'flex items-center justify-between rounded-lg border px-4 py-3',
          healthBgColor
        ]"
      >
      <div>
        <p class="text-sm font-medium text-gray-700">
          {{ t('buildingDetail.supplyChain.healthStatus') }}
        </p>
        <p :class="['text-lg font-bold', healthColor]">
          {{ t(`buildingDetail.supplyChain.health.${props.supplyChain.healthScore}`) }}
        </p>
        <p class="text-sm text-gray-600">{{ props.supplyChain.healthReason }}</p>
      </div>
      <div class="text-center">
        <div :class="['text-3xl font-bold', healthColor]">
          {{ props.supplyChain.units.length }}
        </div>
        <p class="text-xs text-gray-600">{{ t('buildingDetail.supplyChain.units') }}</p>
      </div>
    </div>

    <!-- Supply Chain Diagram -->
    <div v-if="!loading" class="flex justify-center">
      <SupplyChainDiagram :diagram="props.supplyChain" />
    </div>

    <div v-if="loading" class="flex items-center justify-center py-8">
      <div class="text-sm text-gray-500">
        {{ t('common.loading') }}
      </div>
    </div>

    <!-- Unit Details -->
    <div v-if="!loading && props.supplyChain.units.length > 0" class="mt-4">
      <h4 class="text-sm font-semibold text-gray-700 mb-3">
        {{ t('buildingDetail.supplyChain.unitDetails') }}
      </h4>
      <div class="space-y-2">
        <div
          v-for="unit in props.supplyChain.units"
          :key="unit.buildingUnitId"
          class="flex items-center justify-between rounded border border-gray-200 bg-gray-50 px-3 py-2 text-sm"
        >
          <div>
            <span class="font-medium">{{ unit.unitType }}</span>
            <span class="ml-2 text-gray-600">({{ unit.gridX }}, {{ unit.gridY }})</span>
          </div>
          <div class="flex items-center gap-4">
            <div class="text-right">
              <div class="font-medium">{{ unit.fillPercent.toFixed(0) }}%</div>
              <div class="text-xs text-gray-500">{{ t('buildingDetail.inventory.fill') }}</div>
            </div>
            <div
              :class="[
                'rounded px-2 py-1 text-xs font-semibold',
                unit.status === 'ACTIVE'
                  ? 'bg-green-100 text-green-800'
                  : unit.status === 'BLOCKED'
                    ? 'bg-red-100 text-red-800'
                    : unit.status === 'FULL'
                      ? 'bg-blue-100 text-blue-800'
                      : 'bg-gray-100 text-gray-800'
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
