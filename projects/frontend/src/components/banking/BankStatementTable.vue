<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import CurrencyAmount from '@/components/numbers/CurrencyAmount.vue'
import type { BankStatementRow } from '@/types'

const props = defineProps<{
  rows: BankStatementRow[]
  currencyCode: string
  totalEntries: number
  totalShown: number
  page: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}>()

defineEmits<{
  previousPage: []
  nextPage: []
}>()

const { t, locale } = useI18n()

function formatDate(utc: string): string {
  return new Date(utc).toLocaleDateString(locale.value, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function categoryLabel(cat: string): string {
  const map: Record<string, string> = {
    REVENUE: t('bankStatement.category.revenue'),
    PURCHASING_COST: t('bankStatement.category.purchasingCost'),
    LABOR_COST: t('bankStatement.category.laborCost'),
    ENERGY_COST: t('bankStatement.category.energyCost'),
    PROPERTY_PURCHASE: t('bankStatement.category.propertyPurchase'),
    CONSTRUCTION_COST: t('bankStatement.category.constructionCost'),
    UNIT_UPGRADE: t('bankStatement.category.unitUpgrade'),
    MARKETING: t('bankStatement.category.marketing'),
    SHIPPING_COST: t('bankStatement.category.shippingCost'),
    MEDIA_HOUSE_INCOME: t('bankStatement.category.mediaHouseIncome'),
    MEDIA_HOUSE_CONTENT: t('bankStatement.category.mediaHouseContent'),
    TAX: t('bankStatement.category.tax'),
    DIVIDEND: t('bankStatement.category.dividend'),
    RENT_INCOME: t('bankStatement.category.rentIncome'),
    OTHER: t('bankStatement.category.other'),
    LOAN_ORIGINATION: t('bankStatement.category.loanOrigination'),
    LOAN_REPAYMENT_PRINCIPAL: t('bankStatement.category.loanRepayment'),
    LOAN_INTEREST_EXPENSE: t('bankStatement.category.loanInterestExpense'),
    LOAN_INTEREST_INCOME: t('bankStatement.category.loanInterestIncome'),
    LOAN_PENALTY: t('bankStatement.category.loanPenalty'),
    DEPOSIT_MADE: t('bankStatement.category.depositMade'),
    DEPOSIT_WITHDRAWN: t('bankStatement.category.depositWithdrawn'),
    DEPOSIT_INTEREST_PAID: t('bankStatement.category.depositInterestPaid'),
    DEPOSIT_INTEREST_RECEIVED: t('bankStatement.category.depositInterestReceived'),
    CENTRAL_BANK_BORROW: t('bankStatement.category.centralBankBorrow'),
    CENTRAL_BANK_REPAY: t('bankStatement.category.centralBankRepay'),
    STOCK_PURCHASE: t('bankStatement.category.stockPurchase'),
    STOCK_SALE: t('bankStatement.category.stockSale'),
    FOUNDER_CONTRIBUTION: t('bankStatement.category.founderContribution'),
    IPO_RAISE: t('bankStatement.category.ipoRaise'),
  }

  return map[cat] ?? cat
}

function categoryIcon(cat: string): string {
  if (['REVENUE', 'MEDIA_HOUSE_INCOME', 'RENT_INCOME', 'DEPOSIT_INTEREST_RECEIVED', 'LOAN_INTEREST_INCOME', 'STOCK_SALE', 'FOUNDER_CONTRIBUTION', 'IPO_RAISE'].includes(cat)) {
    return '+'
  }

  if (
    [
      'PURCHASING_COST',
      'LABOR_COST',
      'ENERGY_COST',
      'PROPERTY_PURCHASE',
      'CONSTRUCTION_COST',
      'UNIT_UPGRADE',
      'MARKETING',
      'SHIPPING_COST',
      'TAX',
      'DIVIDEND',
      'LOAN_ORIGINATION',
      'LOAN_INTEREST_EXPENSE',
      'LOAN_PENALTY',
      'DEPOSIT_MADE',
      'MEDIA_HOUSE_CONTENT',
      'STOCK_PURCHASE',
    ].includes(cat)
  ) {
    return '−'
  }

  return ''
}
</script>

<template>
  <div class="overflow-x-auto border border-divider rounded-xl">
    <table class="statement-table w-full border-collapse text-sm" aria-label="Bank statement transactions">
      <thead>
        <tr>
          <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card whitespace-nowrap hidden sm:table-cell">
            {{ t('bankStatement.columns.date') }}
          </th>
          <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card whitespace-nowrap hidden sm:table-cell">
            {{ t('bankStatement.columns.tick') }}
          </th>
          <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
            {{ t('bankStatement.columns.description') }}
          </th>
          <th class="text-left px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
            {{ t('bankStatement.columns.category') }}
          </th>
          <th class="text-right px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
            {{ t('bankStatement.columns.debit') }}
          </th>
          <th class="text-right px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card">
            {{ t('bankStatement.columns.credit') }}
          </th>
          <th class="text-right px-3 py-2.5 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider bg-card whitespace-nowrap">
            {{ t('bankStatement.columns.balance') }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="rows.length === 0">
          <td colspan="7" class="text-center text-muted italic py-8 px-4">
            {{ t('bankStatement.noTransactions') }}
          </td>
        </tr>
        <tr v-for="row in rows" :key="row.id" class="statement-row" :class="row.amount >= 0 ? 'row-credit' : 'row-debit'">
          <td class="px-3 py-2.5 text-muted text-xs whitespace-nowrap align-middle hidden sm:table-cell">
            {{ formatDate(row.recordedAtUtc) }}
          </td>
          <td class="px-3 py-2.5 text-muted text-xs tabular-nums align-middle hidden sm:table-cell">
            {{ row.recordedAtTick }}
          </td>
          <td class="px-3 py-2.5 align-middle max-w-[280px]">
            <div class="text-body font-medium">{{ row.description || '—' }}</div>
            <div v-if="row.buildingName" class="description-sub text-xs text-muted mt-0.5">🏭 {{ row.buildingName }}</div>
          </td>
          <td class="px-3 py-2.5 align-middle">
            <span class="inline-block text-xs font-semibold text-muted bg-card-raised border border-divider rounded px-1.5 py-0.5 whitespace-nowrap">
              {{ categoryLabel(row.category) }}
            </span>
          </td>
          <td class="debit-cell px-3 py-2.5 text-right text-bad font-semibold align-middle">
            <CurrencyAmount v-if="row.amount < 0" :amount="Math.abs(row.amount)" :currency="currencyCode" />
            <span v-else class="empty-cell-dash text-muted">—</span>
          </td>
          <td class="credit-cell px-3 py-2.5 text-right text-good font-semibold align-middle">
            <CurrencyAmount v-if="row.amount >= 0" :amount="row.amount" :currency="currencyCode" />
            <span v-else class="empty-cell-dash text-muted">—</span>
          </td>
          <td class="px-3 py-2.5 text-right whitespace-nowrap font-bold tabular-nums align-middle" :class="row.runningBalance >= 0 ? 'text-good' : 'text-bad'">
            <CurrencyAmount :amount="row.runningBalance" :currency="currencyCode" />
            <span class="text-xs text-muted ml-0.5">{{ categoryIcon(row.category) }}</span>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <div class="flex flex-col gap-3 mt-4 sm:flex-row sm:items-center sm:justify-between text-sm text-muted">
    <span>{{ t('bankStatement.showingFirst', { count: totalShown, total: totalEntries }) }}</span>
    <div class="flex items-center gap-3">
      <span>{{ t('bankStatement.pageSummary', { page: props.page, total: totalPages }) }}</span>
      <button class="pagination-btn" :disabled="!hasPreviousPage" @click="$emit('previousPage')">
        {{ t('bankStatement.previousPage') }}
      </button>
      <button class="pagination-btn" :disabled="!hasNextPage" @click="$emit('nextPage')">
        {{ t('bankStatement.nextPage') }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.statement-row td {
  border-bottom: 1px solid var(--color-border-light, rgba(48, 54, 61, 0.5));
}

.statement-row:last-child td {
  border-bottom: none;
}

.row-credit {
  background: rgba(34, 197, 94, 0.02);
}

.row-debit {
  background: rgba(248, 113, 113, 0.02);
}

.statement-row:hover td {
  background: var(--color-surface-raised);
}

.pagination-btn {
  background: var(--color-surface-raised);
  color: var(--color-text);
  border: 1px solid var(--color-border-light, rgba(48, 54, 61, 0.5));
  border-radius: 0.5rem;
  padding: 0.45rem 0.8rem;
  cursor: pointer;
  font-weight: 600;
}

.pagination-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
