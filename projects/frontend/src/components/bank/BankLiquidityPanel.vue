<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatCurrency, formatPercent } from '@/lib/loanHelpers'
import type { BankInfoSummary } from '@/types'

const props = defineProps<{
  bankInfo: BankInfoSummary
  currencyCode: string
}>()

const { t } = useI18n()

const fmt = computed(() => (amount: number) => formatCurrency(amount, props.currencyCode))
</script>

<template>
  <section class="liquidity-section">
    <h2 class="section-title text-lg font-semibold text-body">{{ t('bank.liquidityHealth') }}</h2>
    <div class="liquidity-status-banner" :class="`liquidity-${bankInfo.liquidityStatus.toLowerCase()}`">
      <span class="liquidity-status-label">{{ t(`bank.liquidityStatus.${bankInfo.liquidityStatus}`) }}</span>
      <span class="liquidity-status-hint">{{ t(`bank.liquidityStatusHint.${bankInfo.liquidityStatus}`) }}</span>
    </div>

    <div class="liquidity-grid">
      <div class="liquidity-stat">
        <span class="liquidity-stat-label">{{ t('bank.availableCash') }}</span>
        <span
          class="liquidity-stat-value"
          :class="(bankInfo.availableCash ?? 0) >= (bankInfo.reserveRequirement ?? 0) ? 'positive' : 'negative'"
        >
          {{ fmt(bankInfo.availableCash ?? 0) }}
        </span>
      </div>
      <div class="liquidity-stat">
        <span class="liquidity-stat-label">{{ t('bank.reserveRequirement') }}</span>
        <span class="liquidity-stat-value">{{ fmt(bankInfo.reserveRequirement ?? 0) }}</span>
        <span class="liquidity-stat-hint">{{ t('bank.reserveInfo') }}</span>
      </div>
      <div class="liquidity-stat">
        <span class="liquidity-stat-label">{{ t('bank.reserveShortfall') }}</span>
        <span class="liquidity-stat-value" :class="(bankInfo.reserveShortfall ?? 0) > 0 ? 'negative' : 'positive'">
          {{ (bankInfo.reserveShortfall ?? 0) > 0 ? fmt(bankInfo.reserveShortfall) : t('bank.noReserveShortfall') }}
        </span>
      </div>
      <div class="liquidity-stat" :class="{ 'liquidity-stat-warning': (bankInfo.centralBankDebt ?? 0) > 0 }">
        <span class="liquidity-stat-label">{{ t('bank.centralBankDebt') }}</span>
        <span class="liquidity-stat-value" :class="(bankInfo.centralBankDebt ?? 0) > 0 ? 'negative' : 'positive'">
          {{ (bankInfo.centralBankDebt ?? 0) > 0 ? fmt(bankInfo.centralBankDebt) : fmt(0) }}
        </span>
        <span v-if="(bankInfo.centralBankDebt ?? 0) > 0" class="liquidity-stat-hint">
          {{ t('bank.centralBankRate') }}: {{ formatPercent(bankInfo.centralBankInterestRatePercent ?? 2) }} p.a.
        </span>
      </div>
    </div>

    <!-- Central-bank debt context -->
    <div v-if="(bankInfo.centralBankDebt ?? 0) > 0" class="central-bank-notice">
      <div class="notice-icon">⚠</div>
      <div class="notice-body">
        <strong>{{ t('bank.centralBankDebt') }}</strong>
        <p>{{ t('bank.centralBankDebtHint', { rate: (bankInfo.centralBankInterestRatePercent ?? 2).toFixed(2) }) }}</p>
      </div>
    </div>

    <!-- Recommended actions when under pressure -->
    <div v-if="bankInfo.liquidityStatus !== 'HEALTHY'" class="recommended-actions">
      <h3 class="actions-title">{{ t('bank.recommendedActions') }}</h3>
      <ul class="actions-list">
        <li v-if="(bankInfo.reserveShortfall ?? 0) > 0">{{ t('bank.actionAddDeposits') }}</li>
        <li v-if="bankInfo.outstandingLoanPrincipal > 0">{{ t('bank.actionReduceLending') }}</li>
        <li v-if="(bankInfo.centralBankDebt ?? 0) > 0">{{ t('bank.actionRecapitalize') }}</li>
      </ul>
    </div>

    <p class="capitalization-info">{{ t('bank.capitalRequirementInfo') }}</p>
  </section>
</template>

<style scoped>
.liquidity-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--spacing-lg);
  margin-bottom: var(--spacing-xl);
}

.liquidity-status-banner {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
  padding: var(--spacing-sm) var(--spacing-md);
  border-radius: var(--radius-sm);
  margin-bottom: var(--spacing-md);
  font-size: 0.9rem;
}

.liquidity-healthy {
  background: rgba(34, 197, 94, 0.1);
  border: 1px solid rgba(34, 197, 94, 0.3);
  color: #16a34a;
}

.liquidity-pressured {
  background: rgba(245, 158, 11, 0.1);
  border: 1px solid rgba(245, 158, 11, 0.3);
  color: #b45309;
}

.liquidity-critical {
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  color: #dc2626;
}

.liquidity-status-label {
  font-weight: 700;
  white-space: nowrap;
}

.liquidity-status-hint {
  font-size: 0.82rem;
}

.liquidity-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

.liquidity-stat {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  background: var(--color-surface-secondary, rgba(0, 0, 0, 0.03));
}

.liquidity-stat-warning {
  border: 1px solid rgba(239, 68, 68, 0.3);
  background: rgba(239, 68, 68, 0.05);
}

.liquidity-stat-label {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
}

.liquidity-stat-value {
  font-size: 1.1rem;
  font-weight: 600;
}

.liquidity-stat-value.positive {
  color: var(--color-success, #22c55e);
}

.liquidity-stat-value.negative {
  color: var(--color-error, #ef4444);
}

.liquidity-stat-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.central-bank-notice {
  display: flex;
  gap: var(--spacing-sm);
  background: rgba(239, 68, 68, 0.07);
  border: 1px solid rgba(239, 68, 68, 0.25);
  border-radius: var(--radius-sm);
  padding: var(--spacing-md);
  margin-bottom: var(--spacing-md);
  font-size: 0.85rem;
}

.notice-icon {
  font-size: 1.1rem;
  flex-shrink: 0;
}

.notice-body p {
  margin: 0.25rem 0 0;
  color: var(--color-text-secondary);
}

.recommended-actions {
  margin-top: var(--spacing-md);
  padding: var(--spacing-md);
  background: rgba(245, 158, 11, 0.06);
  border: 1px solid rgba(245, 158, 11, 0.2);
  border-radius: var(--radius-sm);
}

.actions-title {
  font-size: 0.85rem;
  font-weight: 600;
  margin: 0 0 var(--spacing-xs);
}

.actions-list {
  margin: 0;
  padding-left: 1.25rem;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  line-height: 1.7;
}

.capitalization-info {
  margin-top: var(--spacing-md);
  font-size: 0.78rem;
  color: var(--color-text-muted);
  font-style: italic;
}
</style>
