<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatCurrency, formatPercent, loanStatusClass } from '@/lib/loanHelpers'
import UiStateEmpty from '@/components/ui/UiStateEmpty.vue'
import type { LoanSummary, BankDepositSummary } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  bankDeposits: BankDepositSummary[]
  issuedLoans: LoanSummary[]
  totalIssuedCapacity: number
  expectedMonthlyIncome: number
  overdueLoans: LoanSummary[]
  cityCurrency: string
  baseCapitalDeposited: boolean
}>()

function fmt(amount: number) {
  return formatCurrency(amount, props.cityCurrency)
}
</script>

<template>
  <!-- Depositors section -->
  <section v-if="baseCapitalDeposited" class="depositors-section">
    <h2 class="section-title">{{ t('bank.bankDepositors') }}</h2>
    <UiStateEmpty v-if="bankDeposits.length === 0">
      {{ t('bank.noBankDepositors') }}
    </UiStateEmpty>
    <div v-else class="depositors-table">
      <table>
        <thead>
          <tr>
            <th>{{ t('common.company') }}</th>
            <th>{{ t('bank.depositAmount') }}</th>
            <th>{{ t('bank.depositInterestRate') }}</th>
            <th>{{ t('bank.depositInterestEarned') }}</th>
            <th>Type</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="dep in bankDeposits" :key="dep.id">
            <td>{{ dep.depositorCompanyName }}</td>
            <td>{{ formatCurrency(dep.amount, dep.cityCurrencyCode || cityCurrency) }}</td>
            <td>{{ formatPercent(dep.depositInterestRatePercent) }}</td>
            <td>{{ formatCurrency(dep.totalInterestPaid, dep.cityCurrencyCode || cityCurrency) }}</td>
            <td>
              <span v-if="dep.isBaseCapital" class="badge badge-info">{{ t('bank.baseCapital') }}</span>
              <span v-else class="badge badge-success">Depositor</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>

  <!-- Overview stats -->
  <div v-if="baseCapitalDeposited" class="stats-row">
    <div class="stat-card">
      <span class="stat-label">Active Loans</span>
      <span class="stat-value">{{ issuedLoans.filter((l) => l.status === 'ACTIVE').length }}</span>
    </div>
    <div class="stat-card">
      <span class="stat-label">Capital Outstanding</span>
      <span class="stat-value">{{ fmt(totalIssuedCapacity) }}</span>
    </div>
    <div class="stat-card" :class="{ 'stat-card-warning': overdueLoans.length > 0 }">
      <span class="stat-label">Overdue/Defaulted</span>
      <span class="stat-value">{{ overdueLoans.length }}</span>
    </div>
    <div class="stat-card">
      <span class="stat-label">Expected Income/Payment</span>
      <span class="stat-value">{{ fmt(expectedMonthlyIncome) }}</span>
    </div>
  </div>

  <!-- Issued Loans -->
  <section v-if="baseCapitalDeposited" class="loans-section">
    <h2 class="section-title">{{ t('bank.issuedLoans') }}</h2>
    <UiStateEmpty v-if="issuedLoans.length === 0">
      {{ t('bank.noIssuedLoans') }}
    </UiStateEmpty>
    <div v-else class="loans-table">
      <table>
        <thead>
          <tr>
            <th>{{ t('bank.borrower') }}</th>
            <th>{{ t('bank.originalPrincipal') }}</th>
            <th>{{ t('bank.remainingPrincipal') }}</th>
            <th>{{ t('bank.paymentAmount') }}</th>
            <th>Payments</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="loan in issuedLoans" :key="loan.id" :class="loanStatusClass(loan.status)">
            <td>{{ loan.borrowerCompanyName }}</td>
            <td>{{ fmt(loan.originalPrincipal) }}</td>
            <td>{{ fmt(loan.remainingPrincipal) }}</td>
            <td>{{ fmt(loan.paymentAmount) }}</td>
            <td>{{ loan.paymentsMade }} / {{ loan.totalPayments }}</td>
            <td>
              <span class="loan-status-badge" :class="loanStatusClass(loan.status)">
                {{ t(`bank.statusBadge.${loan.status}`) }}
              </span>
              <div v-if="loan.missedPayments > 0" class="missed-hint">{{ loan.missedPayments }} missed</div>
              <div v-if="loan.collateralBuildingId" class="collateral-inline">
                <span aria-hidden="true">🔒</span> {{ loan.collateralBuildingName }}
                <span v-if="loan.collateralAppraisedValue" class="collateral-inline-value"> ({{ fmt(loan.collateralAppraisedValue) }}) </span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.stats-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-xl);
}

.stat-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
}

.stat-card-warning {
  border-color: var(--color-warning, #f59e0b);
}

.stat-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  color: var(--color-text-secondary);
  letter-spacing: 0.05em;
}

.stat-value {
  font-size: 1.4rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.section-title {
  font-size: 1.2rem;
  font-weight: 600;
}

.loans-section {
  margin-bottom: var(--spacing-xl);
}

.loans-table {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

th,
td {
  padding: var(--spacing-sm);
  text-align: left;
  border-bottom: 1px solid var(--color-border);
}

th {
  font-weight: 600;
  color: var(--color-text-secondary);
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.loan-status-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
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

.missed-hint {
  font-size: 0.7rem;
  color: #fbbf24;
  margin-top: 2px;
}

.collateral-inline {
  font-size: 0.7rem;
  color: var(--color-primary, #3b82f6);
  margin-top: 2px;
}

.collateral-inline-value {
  color: var(--color-text-secondary);
}

/* Depositors section */
.depositors-section {
  margin-bottom: 1.5rem;
}

.depositors-table table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.depositors-table th,
.depositors-table td {
  padding: 0.5rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
}

.depositors-table th {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 600;
}

.badge-info {
  background: var(--color-primary-bg, rgba(99, 102, 241, 0.15));
  color: var(--color-primary, #6366f1);
}

.badge-success {
  background: rgba(34, 197, 94, 0.15);
  color: #22c55e;
}
</style>
