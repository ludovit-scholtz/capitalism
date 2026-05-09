<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { EconomicCycleHistoryPoint, EconomicCycleView, MarketEventView } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  economicCycle: EconomicCycleView | null
  activeMarketEvents: MarketEventView[]
  economicHistory: EconomicCycleHistoryPoint[]
}>()

const phaseMeta = computed(() => {
  switch (props.economicCycle?.phase) {
    case 'EXPANSION':
      return { emoji: '🟢', className: 'text-emerald-500 border-emerald-500/30 bg-emerald-500/10' }
    case 'PEAK':
      return { emoji: '🟡', className: 'text-amber-400 border-amber-400/30 bg-amber-400/10' }
    case 'RECESSION':
      return { emoji: '🔴', className: 'text-red-500 border-red-500/30 bg-red-500/10' }
    case 'TROUGH':
      return { emoji: '⚪', className: 'text-slate-300 border-slate-400/30 bg-slate-500/10' }
    default:
      return { emoji: '⚪', className: 'text-muted border-divider bg-card-raised' }
  }
})

const chartPoints = computed(() => {
  if (!props.economicHistory || props.economicHistory.length === 0) return []
  const maxIntensity = 1.5
  return props.economicHistory.slice(-24).map((point) => ({
    ...point,
    heightPct: Math.max(12, Math.min(100, (point.intensityFactor / maxIntensity) * 100)),
  }))
})

function formatPctDelta(multiplier: number): string {
  const pct = (multiplier - 1) * 100
  return `${pct >= 0 ? '+' : ''}${pct.toFixed(0)}%`
}
</script>

<template>
  <section class="economy-cycle-widget rounded-xl border border-divider bg-card p-4" :aria-label="t('dashboard.economyWidget.title')">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 class="text-sm font-bold uppercase tracking-wide text-muted">{{ t('dashboard.economyWidget.title') }}</h2>
      <span v-if="economicCycle" class="inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs font-bold" :class="phaseMeta.className">
        {{ phaseMeta.emoji }} {{ t(`dashboard.economyWidget.phase.${economicCycle.phase}`) }}
      </span>
    </div>

    <p v-if="!economicCycle" class="mt-3 text-sm text-muted">{{ t('dashboard.economyWidget.noData') }}</p>

    <template v-else>
      <div class="mt-3 grid gap-3 md:grid-cols-[minmax(0,1fr)_220px]">
        <div>
          <div class="flex items-center justify-between text-xs text-muted">
            <span>{{ t('dashboard.economyWidget.intensity') }}</span>
            <strong class="text-body">{{ economicCycle.intensityFactor.toFixed(2) }}×</strong>
          </div>
          <div class="mt-1 h-2 rounded-full bg-card-raised">
            <div class="h-full rounded-full bg-brand" :style="{ width: `${Math.max(0, Math.min(100, (economicCycle.intensityFactor / 1.5) * 100))}%` }" />
          </div>
          <p class="mt-2 text-xs text-muted">{{ t('dashboard.economyWidget.ticksRemaining', { ticks: economicCycle.ticksRemaining }) }}</p>

          <div v-if="activeMarketEvents.length > 0" class="mt-3 flex flex-col gap-2">
            <div v-for="event in activeMarketEvents.slice(0, 3)" :key="event.id" class="rounded-lg border border-divider bg-card-raised px-3 py-2">
              <div class="flex items-center justify-between gap-2">
                <strong class="text-xs text-body">{{ event.title }}</strong>
                <span class="text-[0.6875rem] font-semibold text-brand">{{ formatPctDelta(event.magnitudeMultiplier) }}</span>
              </div>
              <p class="mt-1 text-xs text-muted">{{ event.description }}</p>
            </div>
          </div>
        </div>

        <div class="rounded-lg border border-divider bg-card-raised p-3">
          <p class="text-[0.6875rem] font-semibold uppercase tracking-wide text-muted">{{ t('dashboard.economyWidget.history') }}</p>
          <div class="mt-2 flex h-20 items-end gap-1" aria-label="Economic cycle history">
            <span
              v-for="point in chartPoints"
              :key="point.tick"
              class="w-1.5 rounded-sm bg-brand/80"
              :style="{ height: `${point.heightPct}%` }"
              :title="`${point.phase} ${point.intensityFactor.toFixed(2)}x`"
            />
          </div>
        </div>
      </div>
    </template>
  </section>
</template>
