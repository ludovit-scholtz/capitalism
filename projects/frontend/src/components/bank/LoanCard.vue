<template>
  <div class="loan-card rounded-2xl border border-divider bg-card-raised p-5 shadow-sm" :class="loanStatusClass(loan.status)">
    <div class="loan-card-header mb-3 flex items-start justify-between">
      <span class="lender-name font-semibold text-body">{{ loan.lenderCompanyName }}</span>
      <span class="loan-status-badge" :class="loanStatusClass(loan.status)">
        {{ t(`bank.statusBadge.${loan.status}`) }}
      </span>
    </div>
    <div class="loan-card-body mb-3 grid grid-cols-2 gap-2">
      <div class="loan-stat flex flex-col gap-0.5">
        <span class="stat-label text-[0.7rem] uppercase tracking-wider text-muted">{{ t('bank.remainingPrincipal') }}</span>
        <span class="stat-value text-sm font-semibold text-body">{{ formatCurrency(loan.remainingPrincipal, loan.loanCurrencyCode) }}</span>
      </div>
      <div class="loan-stat flex flex-col gap-0.5">
        <span class="stat-label text-[0.7rem] uppercase tracking-wider text-muted">{{ t('bank.nextPayment') }}</span>
        <span class="stat-value text-sm font-semibold text-body">{{ formatCurrency(loan.paymentAmount, loan.loanCurrencyCode) }}</span>
      </div>
      <div class="loan-stat flex flex-col gap-0.5">
        <span class="stat-label text-[0.7rem] uppercase tracking-wider text-muted">{{ t('bank.paymentsMade') }}</span>
        <span class="stat-value text-sm font-semibold text-body">{{ loan.paymentsMade }} / {{ loan.totalPayments }}</span>
      </div>
      <div class="loan-stat flex flex-col gap-0.5">
        <span class="stat-label text-[0.7rem] uppercase tracking-wider text-muted">{{ t('bank.interestRate') }}</span>
        <span class="stat-value text-sm font-semibold text-body">{{ formatPercent(loan.annualInterestRatePercent) }}</span>
      </div>
    </div>
    <div v-if="loan.missedPayments > 0" class="overdue-warning mt-2 rounded p-2 text-xs">
      ⚠ {{ loan.missedPayments }} missed payment(s) — penalty accumulated: {{ formatCurrency(loan.accumulatedPenalty) }}
    </div>
    <div v-if="loan.collateralBuildingId" class="collateral-badge mt-2 rounded border p-2 text-xs">
      🔒 {{ t('bank.securedLoan') }}: {{ loan.collateralBuildingName }}
      <span v-if="loan.collateralAppraisedValue" class="collateral-badge-value text-muted">
        ({{ t('bank.collateralAppraisedValue') }}: {{ formatCurrency(loan.collateralAppraisedValue, loan.loanCurrencyCode) }})
      </span>
      <div v-if="loan.collateralBuildingId" class="mt-1">
        <RouterLink class="collateral-link text-xs font-semibold" :to="`/building/${loan.collateralBuildingId}`">
          {{ t('bank.openCollateralBuilding') }}
        </RouterLink>
      </div>
      <div
        v-if="loan.collateralListingPrice && loan.collateralListingCurrencyCode"
        class="collateral-badge-listing mt-1 text-muted"
      >
        {{ t('bank.forcedSaleListingPrice') }}:
        {{ formatCurrency(loan.collateralListingPrice, loan.collateralListingCurrencyCode) }}
      </div>
      <div
        v-if="foreclosureTicksRemaining !== null"
        class="collateral-badge-listing mt-1 text-muted"
      >
        {{ t('bank.foreclosureCountdown') }}: {{ t('buildingDetail.loanDefaultDestruction', { ticks: foreclosureTicksRemaining }) }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LoanSummary } from '@/types'
import { formatCurrency, formatPercent, loanStatusClass } from '@/lib/loanHelpers'
import { useGameStateStore } from '@/stores/gameState'

const { t } = useI18n()
const gameStateStore = useGameStateStore()

const props = defineProps<{
  loan: LoanSummary
}>()

const FORECLOSURE_WINDOW_TICKS = 72

const foreclosureTicksRemaining = computed<number | null>(() => {
  if (props.loan.defaultedAtTick === null || props.loan.defaultedAtTick === undefined) return null
  const currentTick = gameStateStore.gameState?.currentTick ?? 0
  return Math.max(0, props.loan.defaultedAtTick + FORECLOSURE_WINDOW_TICKS - currentTick)
})
</script>

<style scoped>
.loan-status-badge {
  font-size: 0.7rem;
  padding: 2px 8px;
  border-radius: 12px;
  font-weight: 600;
  text-transform: uppercase;
}
.loan-status-badge.status-active {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}
.loan-status-badge.status-overdue {
  background: rgba(251, 191, 36, 0.15);
  color: #fbbf24;
}
.loan-status-badge.status-defaulted {
  background: rgba(248, 113, 113, 0.15);
  color: #f87171;
}
.loan-status-badge.status-repaid {
  background: rgba(96, 165, 250, 0.15);
  color: #60a5fa;
}
.loan-card.status-overdue {
  border-color: var(--color-warning, #f59e0b);
}
.loan-card.status-defaulted {
  border-color: var(--color-danger, #ef4444);
}
.overdue-warning {
  background: rgba(251, 191, 36, 0.12);
  color: #fbbf24;
}
.collateral-badge {
  background: rgba(59, 130, 246, 0.1);
  color: var(--color-primary, #3b82f6);
  border-color: rgba(59, 130, 246, 0.2);
}

.collateral-link {
  color: inherit;
}
</style>
