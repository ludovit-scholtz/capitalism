<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { fmtBuildingAmount, fmtBuildingProfit, profitClass, hasFinancialData } from '@/lib/buildingHeaderFormatters'

interface Props {
  revenue: number | null
  costs: number | null
  profit: number | null
  loading: boolean
  currencyCode?: string
}

const props = defineProps<Props>()
const { t, locale } = useI18n()

const hasData = computed(() => hasFinancialData(props.revenue, props.costs, props.profit))

function fmt(value: number | null): string {
  return fmtBuildingAmount(value, locale.value, props.currencyCode ?? 'EUR')
}

function fmtProfit(value: number | null): string {
  return fmtBuildingProfit(value, locale.value, props.currencyCode ?? 'EUR')
}
</script>

<template>
  <div class="building-header-financials flex min-h-9 flex-wrap items-center gap-3 border border-divider border-t-0 bg-[var(--color-surface-muted,rgba(255,255,255,0.03))] px-4 py-2" :aria-label="t('buildingDetail.accessibility.financialSummary')">
    <template v-if="loading">
      <div class="bh-metric flex min-w-16 flex-col gap-0.5">
        <span class="bh-label bh-skeleton bh-skeleton-label block h-2 w-10 rounded-sm bg-gradient-to-r from-divider via-divider/60 to-divider bg-[length:200%_100%] animate-[bh-shimmer_1.4s_ease-in-out_infinite]"></span>
        <span class="bh-value bh-skeleton bh-skeleton-value mt-0.5 block h-3 w-14 rounded-sm bg-gradient-to-r from-divider via-divider/60 to-divider bg-[length:200%_100%] animate-[bh-shimmer_1.4s_ease-in-out_infinite]"></span>
      </div>
      <div class="bh-metric flex min-w-16 flex-col gap-0.5">
        <span class="bh-label bh-skeleton bh-skeleton-label block h-2 w-10 rounded-sm bg-gradient-to-r from-divider via-divider/60 to-divider bg-[length:200%_100%] animate-[bh-shimmer_1.4s_ease-in-out_infinite]"></span>
        <span class="bh-value bh-skeleton bh-skeleton-value mt-0.5 block h-3 w-14 rounded-sm bg-gradient-to-r from-divider via-divider/60 to-divider bg-[length:200%_100%] animate-[bh-shimmer_1.4s_ease-in-out_infinite]"></span>
      </div>
      <div class="bh-metric bh-metric-profit ml-1 border-l border-divider pl-3 max-[480px]:ml-0 max-[480px]:border-l-0 max-[480px]:pl-0 flex min-w-16 flex-col gap-0.5">
        <span class="bh-label bh-skeleton bh-skeleton-label block h-2 w-10 rounded-sm bg-gradient-to-r from-divider via-divider/60 to-divider bg-[length:200%_100%] animate-[bh-shimmer_1.4s_ease-in-out_infinite]"></span>
        <span class="bh-value bh-skeleton bh-skeleton-value mt-0.5 block h-3 w-14 rounded-sm bg-gradient-to-r from-divider via-divider/60 to-divider bg-[length:200%_100%] animate-[bh-shimmer_1.4s_ease-in-out_infinite]"></span>
      </div>
    </template>
    <template v-else-if="hasData">
      <div class="bh-metric flex min-w-16 flex-col gap-0.5">
        <span class="bh-label text-[0.625rem] font-bold uppercase tracking-[0.05em] text-muted">{{ t('buildingDetail.overview.sales') }}</span>
        <span class="bh-value bh-revenue text-[0.8125rem] font-bold tabular-nums text-brand">{{ fmt(revenue) }}</span>
      </div>
      <div class="bh-metric flex min-w-16 flex-col gap-0.5">
        <span class="bh-label text-[0.625rem] font-bold uppercase tracking-[0.05em] text-muted">{{ t('buildingDetail.overview.costs') }}</span>
        <span class="bh-value bh-costs text-[0.8125rem] font-bold tabular-nums text-danger">{{ fmt(costs) }}</span>
      </div>
      <div class="bh-metric bh-metric-profit ml-1 border-l border-divider pl-3 max-[480px]:ml-0 max-[480px]:border-l-0 max-[480px]:pl-0 flex min-w-16 flex-col gap-0.5">
        <span class="bh-label text-[0.625rem] font-bold uppercase tracking-[0.05em] text-muted">{{ t('buildingDetail.overview.profit') }}</span>
        <span class="bh-value text-[0.8125rem] font-bold tabular-nums" :class="profitClass(profit)">{{ fmtProfit(profit) }}</span>
      </div>
    </template>
    <template v-else>
      <span class="bh-no-data text-[0.6875rem] italic text-muted">{{ t('buildingDetail.overview.noFinancialData') }}</span>
    </template>
  </div>
</template>

<style scoped>
.bh-positive {
  color: var(--color-success, #059669);
}

.bh-negative {
  color: var(--color-danger, #dc2626);
}

.bh-neutral {
  color: var(--color-text-secondary);
}

@keyframes bh-shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

</style>
