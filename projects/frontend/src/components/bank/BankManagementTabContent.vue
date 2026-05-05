<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatCurrency, formatPercent } from '@/lib/loanHelpers'
import BankLiquidityPanel from '@/components/bank/BankLiquidityPanel.vue'
import BankManagementTablesPanel from '@/components/bank/BankManagementTablesPanel.vue'
import BankDepositRatePanel from '@/components/bank/BankDepositRatePanel.vue'
import type { LoanSummary, BankDepositSummary, BankInfoSummary, BankDepositRateHistorySummary } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  bankInfo: BankInfoSummary
  bankDeposits: BankDepositSummary[]
  issuedLoans: LoanSummary[]
  totalIssuedCapacity: number
  expectedMonthlyIncome: number
  overdueLoans: LoanSummary[]
  cityCurrency: string
  showRatesForm: boolean
  ratesForm: { depositInterestRatePercent: number; lendingInterestRatePercent: number }
  ratesLoading: boolean
  ratesError: string | null
  ratesSuccess: boolean
  baseDepositLoading: boolean
  baseDepositError: string | null
  baseDepositSuccess: boolean
  // Dynamic deposit rate props
  depositRateHistory: BankDepositRateHistorySummary[]
  depositRateLoading: boolean
  depositRateError: string | null
  depositRateSuccess: boolean
  currentTick: number
}>()

const emit = defineEmits<{
  (e: 'update:showRatesForm', value: boolean): void
  (e: 'update:ratesForm', value: { depositInterestRatePercent: number; lendingInterestRatePercent: number }): void
  (e: 'save-rates'): void
  (e: 'submit-base-deposit'): void
  (e: 'save-deposit-rate', newRate: number): void
}>()

function fmt(amount: number) {
  return formatCurrency(amount, props.cityCurrency)
}

function updateDepositRate(value: number) {
  emit('update:ratesForm', { ...props.ratesForm, depositInterestRatePercent: value })
}

function updateLendingRate(value: number) {
  emit('update:ratesForm', { ...props.ratesForm, lendingInterestRatePercent: value })
}
</script>

<template>
  <!-- ── Base Capital Deposit Required ────────────────────────── -->
  <div v-if="!bankInfo.baseCapitalDeposited" class="base-deposit-required">
    <div class="base-deposit-icon" aria-hidden="true">🏦</div>
    <div class="base-deposit-body">
      <h2 class="base-deposit-title">{{ t('bank.baseDepositRequired') }}</h2>
      <p class="base-deposit-description">
        {{
          t('bank.baseDepositRequiredBody', {
            amount: fmt(bankInfo.baseCapitalRequirement ?? 10_000_000),
          })
        }}
      </p>
      <p class="base-deposit-hint">
        {{
          t('bank.baseCapitalRequired', {
            amount: fmt(bankInfo.baseCapitalRequirement ?? 10_000_000),
            currency: bankInfo.cityCurrencyCode ?? 'EUR',
          })
        }}
      </p>
      <p v-if="bankInfo.cityCurrencyCode && bankInfo.cityCurrencyCode !== 'EUR'" class="base-deposit-currency-note">
        <span class="currency-badge">{{ bankInfo.cityCurrencyCode }}</span>
        {{ bankInfo.cityCurrencySymbol }}{{ (bankInfo.baseCapitalRequirement ?? 0).toLocaleString() }}
        {{ t('bank.localCurrencyNote') }}
      </p>
    </div>
    <div v-if="baseDepositError" class="error-message">{{ baseDepositError }}</div>
    <div v-if="baseDepositSuccess" class="success-message">{{ t('bank.baseDepositSuccess') }}</div>
    <button class="btn btn-primary base-deposit-btn" :disabled="baseDepositLoading" @click="emit('submit-base-deposit')">
      {{ baseDepositLoading ? t('common.loading') : t('bank.makeBaseDeposit') }}
    </button>
  </div>

  <!-- Bank Info & Rate Configuration -->
  <div class="bank-info-section">
    <div class="bank-info-header">
      <h2>{{ t('bank.bankRates') }}</h2>
      <button class="btn btn-secondary btn-sm" @click="emit('update:showRatesForm', !showRatesForm)">
        {{ showRatesForm ? t('common.cancel') : t('bank.setBankRates') }}
      </button>
    </div>

    <!-- Rates form -->
    <div v-if="showRatesForm" class="rates-form">
      <div class="form-grid">
        <div class="form-group">
          <label for="deposit-rate">{{ t('bank.depositInterestRate') }} (%)</label>
          <input
            id="deposit-rate"
            :value="ratesForm.depositInterestRatePercent"
            type="number"
            min="0"
            max="100"
            step="0.1"
            class="form-input"
            @input="updateDepositRate(+($event.target as HTMLInputElement).value)"
          />
        </div>
        <div class="form-group">
          <label for="lending-rate">{{ t('bank.lendingInterestRate') }} (%)</label>
          <input
            id="lending-rate"
            :value="ratesForm.lendingInterestRatePercent"
            type="number"
            min="0.1"
            max="200"
            step="0.1"
            class="form-input"
            @input="updateLendingRate(+($event.target as HTMLInputElement).value)"
          />
        </div>
      </div>
      <div v-if="ratesError" class="error-message">{{ ratesError }}</div>
      <button class="btn btn-primary" :disabled="ratesLoading" @click="emit('save-rates')">
        {{ ratesLoading ? t('common.loading') : t('bank.setBankRates') }}
      </button>
    </div>
    <!-- Success message shown outside the form so it persists after the form closes -->
    <div v-if="ratesSuccess && !showRatesForm" class="success-message rates-success">
      {{ t('bank.ratesUpdated') }}
    </div>

    <!-- Bank stats panel -->
    <div class="bank-stats-grid">
      <div class="bank-stat">
        <span class="bank-stat-label">{{ t('bank.depositInterestRate') }}</span>
        <span class="bank-stat-value deposit-rate">{{ formatPercent(bankInfo.depositInterestRatePercent) }}</span>
      </div>
      <div class="bank-stat">
        <span class="bank-stat-label">{{ t('bank.lendingInterestRate') }}</span>
        <span class="bank-stat-value lending-rate">{{ formatPercent(bankInfo.lendingInterestRatePercent) }}</span>
      </div>
      <div class="bank-stat">
        <span class="bank-stat-label">{{ t('bank.totalDeposits') }}</span>
        <span class="bank-stat-value">{{ fmt(bankInfo.totalDeposits) }}</span>
      </div>
      <div class="bank-stat">
        <span class="bank-stat-label">{{ t('bank.lendableCapacity') }}</span>
        <span class="bank-stat-value">{{ fmt(bankInfo.lendableCapacity) }}</span>
        <span class="bank-stat-hint">{{ t('bank.reserveInfo') }}</span>
      </div>
      <div class="bank-stat">
        <span class="bank-stat-label">{{ t('bank.availableLendingCapacity') }}</span>
        <span class="bank-stat-value" :class="bankInfo.availableLendingCapacity > 0 ? 'positive' : 'negative'">
          {{ fmt(bankInfo.availableLendingCapacity) }}
        </span>
      </div>
    </div>
  </div>

  <!-- Liquidity Health Panel -->
  <BankLiquidityPanel v-if="bankInfo.baseCapitalDeposited && bankInfo.liquidityStatus" :bank-info="bankInfo" :currency-code="cityCurrency" />

  <!-- Dynamic Deposit Rate Panel (owner only, bank activated) -->
  <BankDepositRatePanel
    v-if="bankInfo.baseCapitalDeposited"
    :current-rate-percent="bankInfo.depositInterestRatePercent"
    :pending-rate-percent="bankInfo.pendingDepositInterestRatePercent ?? null"
    :pending-rate-effective-tick="bankInfo.pendingDepositRateEffectiveTick ?? null"
    :current-tick="currentTick"
    :rate-history="depositRateHistory"
    :loading="depositRateLoading"
    :error="depositRateError"
    :success="depositRateSuccess"
    :deposit-count="bankDeposits.length"
    @save-deposit-rate="(rate) => emit('save-deposit-rate', rate)"
  />

  <BankManagementTablesPanel
    v-if="bankInfo.baseCapitalDeposited"
    :bank-deposits="bankDeposits"
    :issued-loans="issuedLoans"
    :total-issued-capacity="totalIssuedCapacity"
    :expected-monthly-income="expectedMonthlyIncome"
    :overdue-loans="overdueLoans"
    :city-currency="cityCurrency"
    :base-capital-deposited="bankInfo.baseCapitalDeposited"
  />
</template>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
}

.form-group label {
  font-size: 0.85rem;
  font-weight: 500;
}

.form-input {
  padding: 6px var(--spacing-sm);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-primary);
}

.error-message {
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  font-size: 0.85rem;
  margin-bottom: var(--spacing-sm);
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--spacing-xs) var(--spacing-md);
  border-radius: var(--radius-sm);
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: background-color 0.2s;
}

.btn-sm {
  padding: 4px var(--spacing-sm);
  font-size: 0.8rem;
}

.btn-primary {
  background: var(--color-primary, #3b82f6);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--color-primary-hover, #2563eb);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--color-surface);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border);
}

/* Bank info and rate configuration */
.bank-info-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.bank-info-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.bank-info-header h2 {
  font-size: 1.1rem;
  font-weight: 600;
  margin: 0;
}

.bank-stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 1rem;
  margin-top: 1rem;
}

.bank-stat {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 0.75rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.bank-stat-label {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.bank-stat-value {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--color-text);
}

.bank-stat-value.deposit-rate {
  color: var(--color-success, #22c55e);
}

.bank-stat-value.lending-rate {
  color: var(--color-warning, #f59e0b);
}

.bank-stat-value.positive {
  color: var(--color-success, #22c55e);
}

.bank-stat-value.negative {
  color: var(--color-error, #ef4444);
}

.bank-stat-hint {
  font-size: 0.72rem;
  color: var(--color-text-muted);
}

.rates-form {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 1rem;
  margin-bottom: 1rem;
}

.success-message {
  color: var(--color-success, #22c55e);
  font-size: 0.875rem;
  padding: 0.5rem 0;
}

/* ── Base Capital Deposit Required ───────────────────────────────────────── */
.base-deposit-required {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--spacing-md);
  padding: var(--spacing-xl);
  background: var(--color-surface-secondary, rgba(0, 0, 0, 0.03));
  border: 2px dashed var(--color-border, rgba(0, 0, 0, 0.15));
  border-radius: var(--radius-md);
  text-align: center;
  margin-bottom: var(--spacing-lg);
}

.base-deposit-icon {
  font-size: 3rem;
}

.base-deposit-title {
  font-size: 1.25rem;
  font-weight: 700;
  margin: 0;
}

.base-deposit-description {
  color: var(--color-text-secondary);
  margin: 0;
  max-width: 480px;
}

.base-deposit-hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin: 0;
}

.base-deposit-btn {
  min-width: 260px;
  font-size: 1rem;
  padding: var(--spacing-sm) var(--spacing-lg);
}

.currency-badge {
  display: inline-block;
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  padding: 0.1em 0.45em;
  border-radius: var(--radius-sm, 4px);
  background: var(--color-accent-alpha, rgba(59, 130, 246, 0.15));
  color: var(--color-accent, #3b82f6);
  vertical-align: middle;
}

.base-deposit-currency-note {
  display: flex;
  align-items: center;
  gap: var(--spacing-xs, 4px);
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  margin-top: var(--spacing-xs, 4px);
}
</style>
