<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { useTickRefresh } from '@/composables/useTickRefresh'
import type { PersonAccount } from '@/types/index'

const PERSONAL_STOCK_SALE_TAX_RATE = 0.15

const { t, locale } = useI18n()
const auth = useAuthStore()

const personAccount = ref<PersonAccount | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

const PERSON_ACCOUNT_QUERY = `
  query PersonAccountLedger {
    personAccount {
      playerId
      displayName
      personalCash
      taxReserve
      availableCash
      totalNetWealth
      activeAccountType
      activeCompanyId
      shareholdings {
        companyId
        companyName
        shareCount
        ownershipRatio
        sharePrice
        marketValue
      }
      dividendPayments {
        id
        companyId
        companyName
        shareCount
        amountPerShare
        totalAmount
        gameYear
        recordedAtTick
        recordedAtUtc
        description
      }
      stockTrades {
        id
        companyId
        companyName
        direction
        shareCount
        pricePerShare
        totalValue
        recordedAtTick
        recordedAtUtc
      }
    }
  }
`

const portfolioValue = computed(() =>
  (personAccount.value?.shareholdings ?? []).reduce((sum, h) => sum + h.marketValue, 0),
)

const sellTrades = computed(() =>
  (personAccount.value?.stockTrades ?? []).filter((t) => t.direction === 'SELL'),
)

async function loadData(isRefresh = false) {
  if (!isRefresh) loading.value = true
  error.value = null

  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }
    const data = await gqlRequest<{ personAccount: PersonAccount | null }>(PERSON_ACCOUNT_QUERY)
    personAccount.value = data.personAccount
  } catch (reason: unknown) {
    if (!isRefresh) {
      error.value = reason instanceof Error ? reason.message : t('personalLedger.loadFailed')
    }
  } finally {
    loading.value = false
  }
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value)
}

function formatShares(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
    maximumFractionDigits: Number.isInteger(value) ? 0 : 4,
  }).format(value)
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

onMounted(() => void loadData())

useTickRefresh(() => loadData(true))
</script>

<template>
  <div class="ledger-view">
    <section class="ledger-hero">
      <div class="container">
        <p class="ledger-eyebrow">{{ t('personalLedger.eyebrow') }}</p>
        <h1 class="ledger-title">{{ t('personalLedger.title') }}</h1>
        <p class="ledger-subtitle">{{ t('personalLedger.subtitle') }}</p>
        <RouterLink to="/stocks" class="back-link">{{ t('personalLedger.backToExchange') }}</RouterLink>
      </div>
    </section>

    <div class="container ledger-body">
      <div v-if="loading" class="state-box">
        <p>{{ t('common.loading') }}</p>
      </div>

      <div v-else-if="error" class="state-box state-error" role="alert">
        <p>{{ error }}</p>
        <button class="btn btn-secondary" @click="() => void loadData()">{{ t('common.tryAgain') }}</button>
      </div>

      <div v-else-if="!personAccount" class="state-box">
        <p>{{ t('personalLedger.loadFailed') }}</p>
        <RouterLink to="/login" class="btn btn-primary">{{ t('common.login') }}</RouterLink>
      </div>

      <template v-else>
        <!-- Wealth summary panel -->
        <section class="panel wealth-panel">
          <h2 class="panel-title">{{ t('personalLedger.wealthBreakdownTitle') }}</h2>

          <div class="wealth-grid">
            <article class="wealth-card wealth-card--primary">
              <span class="wealth-label">{{ t('personalLedger.netWealth') }}</span>
              <strong class="wealth-amount wealth-amount--hero">{{ formatCurrency(personAccount.totalNetWealth) }}</strong>
              <span class="wealth-desc">{{ t('personalLedger.netWealthDesc') }}</span>
            </article>

            <article class="wealth-card">
              <span class="wealth-label">{{ t('personalLedger.availableCash') }}</span>
              <strong class="wealth-amount">{{ formatCurrency(personAccount.availableCash) }}</strong>
              <span class="wealth-desc">{{ t('personalLedger.availableCashDesc') }}</span>
            </article>

            <article class="wealth-card" :class="{ 'wealth-card--blocked': personAccount.taxReserve > 0 }">
              <span class="wealth-label">{{ t('personalLedger.taxReserve') }}</span>
              <strong class="wealth-amount wealth-amount--blocked">{{ formatCurrency(personAccount.taxReserve) }}</strong>
              <span class="wealth-desc">{{ t('personalLedger.taxReserveDesc') }}</span>
            </article>

            <article class="wealth-card">
              <span class="wealth-label">{{ t('personalLedger.portfolioValue') }}</span>
              <strong class="wealth-amount">{{ formatCurrency(portfolioValue) }}</strong>
              <span class="wealth-desc">{{ t('personalLedger.portfolioValueDesc') }}</span>
            </article>
          </div>

          <p v-if="personAccount.taxReserve > 0" class="tax-note" role="note">
            {{ t('personalLedger.taxNote') }}
          </p>
        </section>

        <!-- Tax reserve history: sell trades with reserved tax -->
        <section class="panel">
          <div class="section-header">
            <div>
              <h2>{{ t('personalLedger.taxHistoryTitle') }}</h2>
              <p>{{ t('personalLedger.taxHistoryDesc') }}</p>
            </div>
            <span v-if="personAccount.taxReserve > 0" class="reserve-badge">
              {{ t('personalLedger.totalReserved') }}: <strong>{{ formatCurrency(personAccount.taxReserve) }}</strong>
            </span>
          </div>

          <p v-if="sellTrades.length === 0" class="empty-state">
            {{ t('personalLedger.taxHistoryEmpty') }}
          </p>
          <div v-else class="table-wrapper">
            <table class="data-table" :aria-label="t('personalLedger.taxHistoryTitle')">
              <thead>
                <tr>
                  <th>{{ t('personalLedger.company') }}</th>
                  <th>{{ t('personalLedger.shares') }}</th>
                  <th>{{ t('personalLedger.price') }}</th>
                  <th>{{ t('personalLedger.total') }}</th>
                  <th>{{ t('personalLedger.taxReservedCol') }}</th>
                  <th>{{ t('personalLedger.recordedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="trade in sellTrades" :key="trade.id">
                  <td>{{ trade.companyName }}</td>
                  <td class="num-cell">{{ formatShares(trade.shareCount) }}</td>
                  <td class="num-cell">{{ formatCurrency(trade.pricePerShare) }}</td>
                  <td class="num-cell">{{ formatCurrency(trade.totalValue) }}</td>
                  <td class="num-cell tax-cell">{{ formatCurrency(trade.totalValue * PERSONAL_STOCK_SALE_TAX_RATE) }}</td>
                  <td>{{ formatDateTime(trade.recordedAtUtc) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Full trade history -->
        <section class="panel">
          <div class="section-header">
            <div>
              <h2>{{ t('personalLedger.tradeHistoryTitle') }}</h2>
              <p>{{ t('personalLedger.tradeHistoryDesc') }}</p>
            </div>
          </div>

          <p v-if="personAccount.stockTrades.length === 0" class="empty-state">
            {{ t('personalLedger.tradeHistoryEmpty') }}
          </p>
          <div v-else class="table-wrapper">
            <table class="data-table" :aria-label="t('personalLedger.tradeHistoryTitle')">
              <thead>
                <tr>
                  <th>{{ t('personalLedger.company') }}</th>
                  <th>{{ t('personalLedger.direction') }}</th>
                  <th>{{ t('personalLedger.shares') }}</th>
                  <th>{{ t('personalLedger.price') }}</th>
                  <th>{{ t('personalLedger.total') }}</th>
                  <th>{{ t('personalLedger.recordedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="trade in personAccount.stockTrades" :key="trade.id">
                  <td>{{ trade.companyName }}</td>
                  <td>
                    <span
                      class="direction-badge"
                      :class="trade.direction === 'BUY' ? 'direction-badge--buy' : 'direction-badge--sell'"
                    >
                      {{ trade.direction === 'BUY' ? t('personalLedger.buy') : t('personalLedger.sell') }}
                    </span>
                  </td>
                  <td class="num-cell">{{ formatShares(trade.shareCount) }}</td>
                  <td class="num-cell">{{ formatCurrency(trade.pricePerShare) }}</td>
                  <td class="num-cell">{{ formatCurrency(trade.totalValue) }}</td>
                  <td>{{ formatDateTime(trade.recordedAtUtc) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Dividend history -->
        <section class="panel">
          <div class="section-header">
            <div>
              <h2>{{ t('personalLedger.dividendHistoryTitle') }}</h2>
              <p>{{ t('personalLedger.dividendHistoryDesc') }}</p>
            </div>
          </div>

          <p v-if="personAccount.dividendPayments.length === 0" class="empty-state">
            {{ t('personalLedger.dividendHistoryEmpty') }}
          </p>
          <div v-else class="table-wrapper">
            <table class="data-table" :aria-label="t('personalLedger.dividendHistoryTitle')">
              <thead>
                <tr>
                  <th>{{ t('personalLedger.company') }}</th>
                  <th>{{ t('personalLedger.gameYear') }}</th>
                  <th>{{ t('personalLedger.shares') }}</th>
                  <th>{{ t('personalLedger.perShare') }}</th>
                  <th>{{ t('personalLedger.amount') }}</th>
                  <th>{{ t('personalLedger.recordedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="div in personAccount.dividendPayments" :key="div.id">
                  <td>{{ div.companyName }}</td>
                  <td>{{ div.gameYear }}</td>
                  <td class="num-cell">{{ formatShares(div.shareCount) }}</td>
                  <td class="num-cell">{{ formatCurrency(div.amountPerShare) }}</td>
                  <td class="num-cell">{{ formatCurrency(div.totalAmount) }}</td>
                  <td>{{ formatDateTime(div.recordedAtUtc) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
    </div>
  </div>
</template>

<style scoped>
.ledger-view {
  min-height: 100vh;
  background:
    radial-gradient(circle at top, color-mix(in srgb, var(--color-primary) 10%, transparent), transparent 35%),
    var(--color-background);
}

.ledger-hero {
  padding: 3.5rem 0 2rem;
}

.ledger-eyebrow {
  margin: 0 0 0.5rem;
  color: var(--color-primary);
  font-size: 0.8rem;
  font-weight: 700;
  letter-spacing: 0.14em;
  text-transform: uppercase;
}

.ledger-title {
  margin: 0;
}

.ledger-subtitle {
  margin: 0.75rem 0 0.5rem;
  max-width: 56rem;
  color: var(--color-text-secondary);
}

.back-link {
  display: inline-block;
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  text-decoration: none;
}

.back-link:hover {
  color: var(--color-primary);
  text-decoration: underline;
}

.ledger-body {
  display: grid;
  gap: 1.5rem;
  padding-bottom: 3rem;
}

.panel,
.state-box {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 18px;
  box-shadow: var(--shadow-sm);
  padding: 1.4rem;
}

.state-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  padding: 2.5rem;
  text-align: center;
}

.state-error {
  border-color: var(--color-danger, #ef4444);
}

.panel-title {
  margin: 0 0 1.25rem;
  font-size: 1.15rem;
}

.section-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.section-header h2 {
  margin: 0;
  font-size: 1.1rem;
}

.section-header p {
  margin: 0.3rem 0 0;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.wealth-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.wealth-card {
  background: color-mix(in srgb, var(--color-surface) 60%, var(--color-background));
  border: 1px solid var(--color-border);
  border-radius: 14px;
  padding: 1.1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.wealth-card--primary {
  border-color: color-mix(in srgb, var(--color-primary) 40%, var(--color-border));
  background: color-mix(in srgb, var(--color-primary) 6%, var(--color-surface));
}

.wealth-card--blocked {
  border-color: color-mix(in srgb, #f59e0b 40%, var(--color-border));
  background: color-mix(in srgb, #f59e0b 6%, var(--color-surface));
}

.wealth-label {
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-text-secondary);
}

.wealth-amount {
  font-size: 1.35rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  color: var(--color-text-primary);
}

.wealth-amount--hero {
  font-size: 1.8rem;
  color: var(--color-primary);
}

.wealth-amount--blocked {
  color: #b45309;
}

.wealth-desc {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  line-height: 1.4;
}

.tax-note {
  margin: 1rem 0 0;
  padding: 0.75rem 1rem;
  background: color-mix(in srgb, #f59e0b 12%, var(--color-surface));
  border: 1px solid color-mix(in srgb, #f59e0b 35%, var(--color-border));
  border-radius: 10px;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.reserve-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.3rem 0.75rem;
  background: color-mix(in srgb, #f59e0b 15%, var(--color-surface));
  border: 1px solid color-mix(in srgb, #f59e0b 40%, var(--color-border));
  border-radius: 999px;
  font-size: 0.82rem;
  color: #92400e;
  white-space: nowrap;
}

.empty-state {
  padding: 1.5rem 0;
  text-align: center;
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.table-wrapper {
  overflow-x: auto;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.data-table th,
.data-table td {
  padding: 0.55rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
}

.data-table th {
  font-size: 0.78rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-text-secondary);
}

.num-cell {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.tax-cell {
  color: #b45309;
  font-weight: 600;
}

.direction-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
  white-space: nowrap;
}

.direction-badge--buy {
  background: color-mix(in srgb, #22c55e 18%, transparent);
  color: #16a34a;
}

.direction-badge--sell {
  background: color-mix(in srgb, #ef4444 18%, transparent);
  color: #dc2626;
}

.wealth-panel {
  padding: 1.6rem;
}
</style>
