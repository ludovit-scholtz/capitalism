<template>
  <section class="panel rounded-3xl border border-divider bg-card p-6 shadow-sm">
    <div class="section-header mb-5">
      <div>
        <h2 class="text-2xl font-bold text-body">{{ t('stockExchange.portfolioTitle') }}</h2>
        <p class="mt-2 text-sm text-muted">{{ t('stockExchange.portfolioDesc') }}</p>
      </div>
    </div>
    <p v-if="personAccount.shareholdings.length === 0" class="empty-state rounded-2xl border border-dashed border-divider px-5 py-6 text-sm text-muted">
      {{ t('stockExchange.portfolioEmpty') }}
    </p>
    <div v-else class="table-wrapper overflow-x-auto">
      <table class="data-table min-w-full text-left text-sm" :aria-label="t('stockExchange.portfolioTitle')">
        <thead>
          <tr class="border-b border-divider text-xs uppercase tracking-[0.16em] text-muted">
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.company') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.ownedShares') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.holdingOwnership') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.sharePrice') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.holdingMarketValue') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="holding in personAccount.shareholdings" :key="holding.companyId" class="border-b border-divider/70 text-body last:border-b-0">
            <td class="px-4 py-4">{{ holding.companyName }}</td>
            <td class="px-4 py-4">{{ formatShares(holding.shareCount) }}</td>
            <td class="px-4 py-4">{{ formatPercent(holding.ownershipRatio) }}</td>
            <td class="px-4 py-4">{{ formatCurrency(holding.sharePrice) }}</td>
            <td class="px-4 py-4">{{ formatCurrency(holding.marketValue) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>

  <section class="panel rounded-3xl border border-divider bg-card p-6 shadow-sm">
    <div class="section-header mb-5">
      <div>
        <h2 class="text-2xl font-bold text-body">{{ t('stockExchange.dividendHistoryTitle') }}</h2>
        <p class="mt-2 text-sm text-muted">{{ t('stockExchange.dividendHistoryDesc') }}</p>
      </div>
    </div>
    <p v-if="personAccount.dividendPayments.length === 0" class="empty-state rounded-2xl border border-dashed border-divider px-5 py-6 text-sm text-muted">
      {{ t('stockExchange.dividendEmpty') }}
    </p>
    <div v-else class="table-wrapper overflow-x-auto">
      <table class="data-table min-w-full text-left text-sm">
        <thead>
          <tr class="border-b border-divider text-xs uppercase tracking-[0.16em] text-muted">
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.company') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.dividendYear') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.dividendPerShare') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.dividendAmount') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.recordedAt') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="payment in personAccount.dividendPayments" :key="payment.id" class="border-b border-divider/70 text-body last:border-b-0">
            <td class="px-4 py-4">{{ payment.companyName }}</td>
            <td class="px-4 py-4">{{ payment.gameYear }}</td>
            <td class="px-4 py-4">{{ formatCurrency(payment.amountPerShare) }}</td>
            <td class="px-4 py-4">{{ formatCurrency(payment.totalAmount) }}</td>
            <td class="px-4 py-4">{{ formatDateTime(payment.recordedAtUtc) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>

  <section class="panel rounded-3xl border border-divider bg-card p-6 shadow-sm">
    <div class="section-header mb-5">
      <div>
        <h2 class="text-2xl font-bold text-body">{{ t('stockExchange.tradeHistoryTitle') }}</h2>
        <p class="mt-2 text-sm text-muted">{{ t('stockExchange.tradeHistoryDesc') }}</p>
      </div>
    </div>
    <p v-if="personAccount.stockTrades.length === 0" class="empty-state rounded-2xl border border-dashed border-divider px-5 py-6 text-sm text-muted">
      {{ t('stockExchange.tradeHistoryEmpty') }}
    </p>
    <div v-else class="table-wrapper overflow-x-auto">
      <table class="data-table min-w-full text-left text-sm" :aria-label="t('stockExchange.tradeHistoryTitle')">
        <thead>
          <tr class="border-b border-divider text-xs uppercase tracking-[0.16em] text-muted">
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.company') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.tradeDirection') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.tradeQuantity') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.tradePrice') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.tradeTotal') }}</th>
            <th class="px-4 py-4 font-semibold">{{ t('stockExchange.recordedAt') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="trade in personAccount.stockTrades" :key="trade.id" class="trade-history-row border-b border-divider/70 text-body last:border-b-0">
            <td class="px-4 py-4">{{ trade.companyName }}</td>
            <td class="px-4 py-4">
              <span
                class="direction-badge inline-flex items-center rounded-full px-3 py-1 text-xs font-bold"
                :class="trade.direction === 'BUY' ? 'direction-badge--buy bg-emerald-500/15 text-emerald-600' : 'direction-badge--sell bg-rose-500/15 text-rose-600'"
              >
                {{ trade.direction === 'BUY' ? t('stockExchange.tradeBuy') : t('stockExchange.tradeSell') }}
              </span>
            </td>
            <td class="px-4 py-4">{{ formatShares(trade.shareCount) }}</td>
            <td class="px-4 py-4">{{ formatCurrency(trade.pricePerShare) }}</td>
            <td class="px-4 py-4">{{ formatCurrency(trade.totalValue) }}</td>
            <td class="px-4 py-4">{{ formatDateTime(trade.recordedAtUtc) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { PersonAccount } from '@/types'

const props = defineProps<{
  personAccount: PersonAccount
  locale: string
}>()

const { t } = useI18n()

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(props.locale, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value)
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`
}

function formatShares(value: number): string {
  return new Intl.NumberFormat(props.locale, {
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
    maximumFractionDigits: Number.isInteger(value) ? 0 : 4,
  }).format(value)
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(props.locale, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}
</script>
