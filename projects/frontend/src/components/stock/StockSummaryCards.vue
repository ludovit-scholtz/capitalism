<template>
  <section class="summary-grid grid gap-4 [grid-template-columns:repeat(auto-fit,minmax(160px,1fr))]">
    <article class="summary-card rounded-3xl border border-divider bg-card p-5 shadow-sm">
      <span class="summary-label text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('stockExchange.totalNetWealth') }}</span>
      <strong class="mt-2 block text-2xl font-bold text-body">{{ formatCurrency(personAccount.totalNetWealth) }}</strong>
    </article>

    <article class="summary-card rounded-3xl border border-divider bg-card p-5 shadow-sm">
      <span class="summary-label text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('stockExchange.availableCash') }}</span>
      <strong class="mt-2 block text-2xl font-bold text-body">{{ formatCurrency(personAccount.availableCash) }}</strong>
    </article>

    <article
      class="summary-card rounded-3xl border p-5 shadow-sm"
      :class="personAccount.taxReserve === 0 ? 'summary-card--inactive border-divider bg-card' : 'summary-card--warning border-amber-500/30 bg-amber-500/10'"
    >
      <span class="summary-label text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('stockExchange.taxReserveLabel') }}</span>
      <strong class="mt-2 block text-2xl font-bold" :class="personAccount.taxReserve === 0 ? 'text-body' : 'text-amber-200'">
        {{ formatCurrency(personAccount.taxReserve) }}
      </strong>
      <span class="summary-hint mt-2 block text-sm text-muted">{{ t('stockExchange.taxReserveHint') }}</span>
    </article>

    <article class="summary-card rounded-3xl border border-divider bg-card p-5 shadow-sm">
      <span class="summary-label text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('stockExchange.portfolioValue') }}</span>
      <strong class="mt-2 block text-2xl font-bold text-body">{{ formatCurrency(portfolioValue) }}</strong>
    </article>

    <article class="summary-card rounded-3xl border border-divider bg-card p-5 shadow-sm">
      <span class="summary-label text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('stockExchange.recentDividends') }}</span>
      <strong class="mt-2 block text-2xl font-bold text-body">{{ formatCurrency(recentDividendTotal) }}</strong>
    </article>

    <article class="summary-card summary-card--link rounded-3xl border border-divider bg-card p-5 shadow-sm">
      <RouterLink to="/personal-ledger" class="ledger-link inline-flex h-full items-center text-sm font-semibold text-brand transition-colors hover:text-brand-strong" :title="t('nav.personalLedger')">
        {{ t('stockExchange.viewPersonalLedger') }}
      </RouterLink>
    </article>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { PersonAccount } from '@/types'

const props = defineProps<{
  personAccount: PersonAccount
  portfolioValue: number
  recentDividendTotal: number
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
</script>
