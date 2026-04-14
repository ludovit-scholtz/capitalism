<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  fmtBuildingAmount,
  fmtBuildingProfit,
  profitClass,
  hasFinancialData,
} from '@/lib/buildingHeaderFormatters'

interface Props {
  revenue: number | null
  costs: number | null
  profit: number | null
  loading: boolean
}

const props = defineProps<Props>()
const { t, locale } = useI18n()

const hasData = computed(() => hasFinancialData(props.revenue, props.costs, props.profit))

function fmt(value: number | null): string {
  return fmtBuildingAmount(value, locale.value)
}

function fmtProfit(value: number | null): string {
  return fmtBuildingProfit(value, locale.value)
}
</script>

<template>
  <div class="building-header-financials" aria-label="Building financial summary">
    <template v-if="loading">
      <div class="bh-metric">
        <span class="bh-label bh-skeleton bh-skeleton-label"></span>
        <span class="bh-value bh-skeleton bh-skeleton-value"></span>
      </div>
      <div class="bh-metric">
        <span class="bh-label bh-skeleton bh-skeleton-label"></span>
        <span class="bh-value bh-skeleton bh-skeleton-value"></span>
      </div>
      <div class="bh-metric bh-metric-profit">
        <span class="bh-label bh-skeleton bh-skeleton-label"></span>
        <span class="bh-value bh-skeleton bh-skeleton-value"></span>
      </div>
    </template>
    <template v-else-if="hasData">
      <div class="bh-metric">
        <span class="bh-label">{{ t('buildingDetail.overview.sales') }}</span>
        <span class="bh-value bh-revenue">{{ fmt(revenue) }}</span>
      </div>
      <div class="bh-metric">
        <span class="bh-label">{{ t('buildingDetail.overview.costs') }}</span>
        <span class="bh-value bh-costs">{{ fmt(costs) }}</span>
      </div>
      <div class="bh-metric bh-metric-profit">
        <span class="bh-label">{{ t('buildingDetail.overview.profit') }}</span>
        <span class="bh-value" :class="profitClass(profit)">{{ fmtProfit(profit) }}</span>
      </div>
    </template>
    <template v-else>
      <span class="bh-no-data">{{ t('buildingDetail.overview.noFinancialData') }}</span>
    </template>
  </div>
</template>

<style scoped>
.building-header-financials {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 1rem;
  background: var(--color-surface-muted, rgba(255, 255, 255, 0.03));
  border: 1px solid var(--color-border);
  border-top: none;
  border-radius: 0 0 var(--radius-md) var(--radius-md);
  flex-wrap: wrap;
  min-height: 2.25rem;
}

.bh-metric {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 4rem;
}

.bh-metric-profit {
  border-left: 1px solid var(--color-border);
  padding-left: 0.75rem;
  margin-left: 0.25rem;
}

.bh-label {
  font-size: 0.625rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-text-secondary);
}

.bh-value {
  font-size: 0.8125rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  color: var(--color-text);
}

.bh-revenue {
  color: var(--color-primary, #2563eb);
}

.bh-costs {
  color: var(--color-danger, #dc2626);
}

.bh-positive {
  color: var(--color-success, #059669);
}

.bh-negative {
  color: var(--color-danger, #dc2626);
}

.bh-neutral {
  color: var(--color-text-secondary);
}

.bh-no-data {
  font-size: 0.6875rem;
  color: var(--color-text-secondary);
  font-style: italic;
}

/* Skeleton loading */
.bh-skeleton {
  display: block;
  border-radius: var(--radius-sm);
  background: linear-gradient(
    90deg,
    var(--color-border) 25%,
    color-mix(in srgb, var(--color-border) 60%, transparent) 50%,
    var(--color-border) 75%
  );
  background-size: 200% 100%;
  animation: bh-shimmer 1.4s ease-in-out infinite;
}

.bh-skeleton-label {
  width: 2.5rem;
  height: 0.5rem;
}

.bh-skeleton-value {
  width: 3.5rem;
  height: 0.75rem;
  margin-top: 0.1rem;
}

@keyframes bh-shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

@media (max-width: 480px) {
  .building-header-financials {
    gap: 0.5rem;
  }

  .bh-metric-profit {
    border-left: none;
    padding-left: 0;
    margin-left: 0;
  }
}
</style>
