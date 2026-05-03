<script setup lang="ts">
import type { SeasonalOutlook } from '@/types'
import { useI18n } from 'vue-i18n'
import { computed } from 'vue'

const props = defineProps<{
  seasonalOutlook: SeasonalOutlook
}>()

const { t } = useI18n()

const demandLevelClass = computed(() => {
  switch (props.seasonalOutlook.demandLevel) {
    case 'HIGH':
      return 'seasonal-badge-high text-emerald-500 bg-emerald-500/10 border-emerald-500/30'
    case 'MODERATE':
      return 'seasonal-badge-moderate text-amber-400 bg-amber-400/10 border-amber-400/30'
    case 'BELOW_AVERAGE':
      return 'seasonal-badge-below text-orange-400 bg-orange-400/10 border-orange-400/30'
    case 'LOW':
      return 'seasonal-badge-low text-red-500 bg-red-500/10 border-red-500/30'
    default:
      return 'text-muted bg-card border-divider'
  }
})

function barColorClass(colorCode: string): string {
  switch (colorCode) {
    case 'GREEN':
      return 'seasonal-bar-green bg-emerald-500'
    case 'YELLOW':
      return 'seasonal-bar-yellow bg-amber-400'
    case 'ORANGE':
      return 'seasonal-bar-orange bg-orange-400'
    case 'RED':
      return 'seasonal-bar-red bg-red-500'
    default:
      return 'bg-muted'
  }
}

/** Converts a multiplier (e.g. 1.5) into a bar height percentage [10%, 100%]. */
function barHeightPct(multiplier: number): string {
  const pct = Math.max(10, Math.min(100, Math.round((multiplier / 2.0) * 100)))
  return `${pct}%`
}
</script>

<template>
  <section class="seasonal-outlook mt-4" :aria-label="t('buildingDetail.seasonalOutlook.sectionLabel')">
    <!-- Header row -->
    <div class="flex items-center justify-between mb-2">
      <span class="text-xs font-bold uppercase tracking-wide text-muted">
        {{ t('buildingDetail.seasonalOutlook.title') }}
      </span>
      <span
        class="seasonal-demand-level text-[0.65rem] font-bold uppercase px-2 py-0.5 rounded border"
        :class="demandLevelClass"
      >
        {{ t(`buildingDetail.seasonalOutlook.demandLevel.${seasonalOutlook.demandLevel}`) }}
      </span>
    </div>

    <!-- Current quarter + multiplier -->
    <div class="flex items-baseline gap-2 mb-3">
      <span class="seasonal-current-quarter text-sm font-semibold text-foreground">
        {{ seasonalOutlook.currentQuarterLabel }}
      </span>
      <span class="seasonal-current-multiplier text-xl font-bold" :class="{
        'text-emerald-500': seasonalOutlook.currentMultiplier >= 1.3,
        'text-amber-400': seasonalOutlook.currentMultiplier >= 1.0 && seasonalOutlook.currentMultiplier < 1.3,
        'text-orange-400': seasonalOutlook.currentMultiplier >= 0.7 && seasonalOutlook.currentMultiplier < 1.0,
        'text-red-500': seasonalOutlook.currentMultiplier < 0.7,
      }">
        {{ seasonalOutlook.currentMultiplier.toFixed(1) }}×
      </span>
      <span class="text-xs text-muted">{{ t('buildingDetail.seasonalOutlook.demandMultiplier') }}</span>
    </div>

    <!-- 4-quarter forecast bar chart -->
    <div
      class="seasonal-forecast-chart flex items-end gap-2 mb-3"
      :aria-label="t('buildingDetail.seasonalOutlook.forecastChartLabel')"
      role="img"
    >
      <div
        v-for="qf in seasonalOutlook.quarterForecasts"
        :key="qf.quarterIndex"
        class="seasonal-forecast-bar-col flex flex-col items-center gap-1 flex-1"
      >
        <span class="seasonal-forecast-multiplier-label text-[0.6rem] text-muted font-medium">
          {{ qf.multiplier.toFixed(1) }}×
        </span>
        <div class="seasonal-bar-track w-full relative rounded-sm overflow-hidden" style="height: 48px">
          <div
            class="seasonal-forecast-bar absolute bottom-0 left-0 right-0 rounded-sm transition-all"
            :class="[barColorClass(qf.colorCode), qf.isCurrent ? 'opacity-100' : 'opacity-60']"
            :style="{ height: barHeightPct(qf.multiplier) }"
          />
        </div>
        <span
          class="seasonal-quarter-label text-[0.65rem] font-semibold"
          :class="qf.isCurrent ? 'text-foreground' : 'text-muted'"
        >
          {{ qf.label }}
        </span>
        <span v-if="qf.isCurrent" class="seasonal-current-indicator text-[0.55rem] text-primary font-bold uppercase tracking-wide">
          {{ t('buildingDetail.seasonalOutlook.now') }}
        </span>
      </div>
    </div>

    <!-- Contextual callout -->
    <div
      v-if="seasonalOutlook.callout"
      class="seasonal-callout text-[0.72rem] text-muted leading-snug rounded-md bg-card border border-divider px-3 py-2"
    >
      📌 {{ seasonalOutlook.callout }}
    </div>
  </section>
</template>

<style scoped>
.seasonal-bar-track {
  background: color-mix(in srgb, var(--color-divider) 60%, transparent);
}
</style>
