<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import CurrencyAmount from '@/components/numbers/CurrencyAmount.vue'
import type { CompanyLedgerSummary } from '@/types'

interface Props {
  ledger: CompanyLedgerSummary | null
  loading: boolean
}

const props = defineProps<Props>()
const { t } = useI18n()

const totalCosts = computed(() => {
  if (!props.ledger) return 0
  const l = props.ledger
  return (l.totalPurchasingCosts ?? 0) + (l.totalLaborCosts ?? 0) + (l.totalEnergyCosts ?? 0) + (l.totalMarketingCosts ?? 0) + (l.totalOtherCosts ?? 0) + (l.totalTaxPaid ?? 0)
})

/** Use the backend-authoritative netIncome (includes taxes) as the single source of truth. */
const netProfit = computed(() => {
  if (!props.ledger) return 0
  return props.ledger.netIncome ?? 0
})

const currencyCode = computed(() => props.ledger?.primaryCurrencyCode ?? 'EUR')

function profitClass(value: number): string {
  if (value > 0) return 'text-secondary'
  if (value < 0) return 'text-danger'
  return 'text-muted'
}
</script>

<template>
  <div class="financial-summary-card rounded-md border border-divider bg-white/5 px-5 py-4" aria-labelledby="financial-summary-title">
    <h3 id="financial-summary-title" class="financial-summary-title mb-3 text-[0.8125rem] font-bold uppercase tracking-[0.06em] text-muted">
      {{ t('financialSummary.title') }}
    </h3>
    <div v-if="loading" class="financial-summary-metrics flex flex-wrap gap-4" aria-busy="true" aria-label="Loading financial data">
      <div class="metric flex min-w-24 flex-col gap-1">
        <span class="metric-label text-[0.6875rem] font-semibold uppercase tracking-[0.04em] text-muted">{{ t('financialSummary.revenue') }}</span>
        <span class="metric-value skeleton-line inline-block h-5 w-[4.5rem] animate-pulse rounded bg-white/10" aria-hidden="true"></span>
      </div>
      <div class="metric flex min-w-24 flex-col gap-1">
        <span class="metric-label text-[0.6875rem] font-semibold uppercase tracking-[0.04em] text-muted">{{ t('financialSummary.costs') }}</span>
        <span class="metric-value skeleton-line inline-block h-5 w-[4.5rem] animate-pulse rounded bg-white/10" aria-hidden="true"></span>
      </div>
      <div class="metric metric--profit ml-1 flex min-w-24 flex-col gap-1 border-l border-divider pl-4">
        <span class="metric-label text-[0.6875rem] font-semibold uppercase tracking-[0.04em] text-muted">{{ t('financialSummary.netProfit') }}</span>
        <span class="metric-value skeleton-line inline-block h-5 w-[4.5rem] animate-pulse rounded bg-white/10" aria-hidden="true"></span>
      </div>
    </div>
    <div v-else-if="!ledger" class="financial-summary-empty text-sm italic text-muted">
      {{ t('financialSummary.noData') }}
    </div>
    <div v-else class="financial-summary-metrics flex flex-wrap gap-4">
      <div class="metric flex min-w-24 flex-col gap-1">
        <span class="metric-label text-[0.6875rem] font-semibold uppercase tracking-[0.04em] text-muted">{{ t('financialSummary.revenue') }}</span>
        <span class="metric-value text-[1.0625rem] font-bold tabular-nums text-secondary">
          <CurrencyAmount :amount="ledger.totalRevenue" :currency="currencyCode" :max-chars="12" />
        </span>
      </div>
      <div class="metric flex min-w-24 flex-col gap-1">
        <span class="metric-label text-[0.6875rem] font-semibold uppercase tracking-[0.04em] text-muted">{{ t('financialSummary.costs') }}</span>
        <span class="metric-value text-[1.0625rem] font-bold tabular-nums text-danger">
          <CurrencyAmount :amount="totalCosts" :currency="currencyCode" :max-chars="12" />
        </span>
      </div>
      <div class="metric metric--profit ml-1 flex min-w-24 flex-col gap-1 border-l border-divider pl-4">
        <span class="metric-label text-[0.6875rem] font-semibold uppercase tracking-[0.04em] text-muted">{{ t('financialSummary.netProfit') }}</span>
        <span class="metric-value text-[1.0625rem] font-bold tabular-nums" :class="profitClass(netProfit)">
          <CurrencyAmount :amount="netProfit" :currency="currencyCode" :max-chars="12" :sign="true" />
        </span>
      </div>
    </div>
    <RouterLink v-if="ledger" :to="`/ledger/${ledger.companyId}`" class="financial-summary-link mt-2.5 inline-block text-[0.8125rem] text-brand hover:underline">
      {{ t('financialSummary.viewFullLedger') }}
    </RouterLink>
  </div>
</template>
