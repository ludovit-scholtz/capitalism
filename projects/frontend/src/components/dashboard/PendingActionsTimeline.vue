<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatGameTickTime, formatTickDuration } from '@/lib/gameTime'
import type { ScheduledActionSummary } from '@/types'

const { t, locale } = useI18n()

const props = defineProps<{
  actions: ScheduledActionSummary[]
  loading: boolean
  currentTick: number | null
}>()

const buildingTypeIcons: Record<string, string> = {
  MINE: '⛏️',
  FACTORY: '🏭',
  SALES_SHOP: '🏪',
  RESEARCH_DEVELOPMENT: '🔬',
  APARTMENT: '🏢',
  COMMERCIAL: '🏛️',
  MEDIA_HOUSE: '📺',
  BANK: '🏦',
  EXCHANGE: '📊',
  POWER_PLANT: '⚡',
}

function getBuildingIcon(type: string): string {
  return buildingTypeIcons[type] || '🏗️'
}

function actionLabel(actionType: string): string {
  if (actionType === 'BUILDING_UPGRADE') {
    return t('pendingActions.buildingUpgrade')
  }
  return actionType
}

function formatApplyTime(appliesAtTick: number): string {
  return formatGameTickTime(appliesAtTick, locale.value)
}

function actionDebugTitle(action: ScheduledActionSummary): string {
  return `Tick ${action.appliesAtTick} · ${formatTickDuration(action.ticksRemaining, locale.value)}`
}
</script>

<template>
  <section class="pending-actions-timeline mb-8" aria-labelledby="pending-actions-title">
    <h2 id="pending-actions-title" class="section-title mb-4 text-lg font-bold text-body">{{ t('pendingActions.title') }}</h2>

    <div v-if="loading" class="pending-loading text-[0.9rem] text-muted">{{ t('common.loading') }}</div>

    <div v-else-if="actions.length === 0" class="pending-empty flex items-center gap-3 rounded-md border border-divider bg-white/5 px-5 py-4 text-[0.9rem] text-muted" role="status">
      <span class="empty-icon text-xl">✅</span>
      <p>{{ t('pendingActions.empty') }}</p>
    </div>

    <ol v-else class="actions-list m-0 flex list-none flex-col gap-3 p-0">
      <li v-for="action in actions" :key="action.id" class="action-item flex items-center gap-4 rounded-md border border-divider bg-card-raised px-5 py-4 max-[640px]:flex-wrap">
        <span class="action-building-icon shrink-0 text-2xl" aria-hidden="true">{{ getBuildingIcon(action.buildingType) }}</span>
        <div class="action-body min-w-0 flex-1">
          <div class="action-header-row mb-1 flex flex-wrap items-baseline gap-2">
            <strong class="action-label text-sm font-semibold text-body">{{ actionLabel(action.actionType) }}</strong>
            <span class="action-building-name overflow-hidden text-ellipsis whitespace-nowrap text-[0.8125rem] text-muted">{{ action.buildingName }}</span>
          </div>
          <div class="action-meta mb-2 flex items-center gap-4">
            <span v-if="props.currentTick !== null" class="applies-at text-xs text-muted" role="timer" :title="actionDebugTitle(action)">
              {{ t('pendingActions.appliesAtTime', { time: formatApplyTime(action.appliesAtTick) }) }}
            </span>
          </div>
          <div class="action-progress h-1 overflow-hidden rounded-sm bg-white/10">
            <div
              class="progress-bar h-full rounded-sm bg-brand transition-[width] duration-500 ease-in-out"
              :style="{
                width: action.totalTicksRequired > 0 ? Math.max(0, Math.min(100, ((action.totalTicksRequired - action.ticksRemaining) / action.totalTicksRequired) * 100)) + '%' : '100%',
              }"
              role="progressbar"
              :aria-valuenow="action.totalTicksRequired - action.ticksRemaining"
              :aria-valuemin="0"
              :aria-valuemax="action.totalTicksRequired"
            ></div>
          </div>
        </div>
        <RouterLink
          :to="action.buildingType === 'BANK' ? `/bank/${action.buildingId}` : `/building/${action.buildingId}`"
          class="btn btn-secondary action-link shrink-0 px-3 py-1.5 text-[0.8125rem] max-[640px]:w-full max-[640px]:text-center"
          :aria-label="t('pendingActions.viewBuilding') + ': ' + action.buildingName"
        >
          {{ t('pendingActions.viewBuilding') }}
        </RouterLink>
      </li>
    </ol>
  </section>
</template>
