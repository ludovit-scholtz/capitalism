<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import GoldAmmSection from '@/components/forex/GoldAmmSection.vue'
import BankAccountSelector from '@/components/banking/BankAccountSelector.vue'
import ForexBankAccountSelector from '@/components/forex/ForexBankAccountSelector.vue'
import type {
  FxRate,
  ForexQuote,
  ForexTradeResult,
  ForexTradeHistoryEntry,
  CurrencyBalance,
  PlayerBankAccountSummary,
} from '@/types'

const { t } = useI18n()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)

const rates = ref<FxRate[]>([])
const balances = ref<CurrencyBalance[]>([])
const history = ref<ForexTradeHistoryEntry[]>([])
const myBankAccounts = ref<PlayerBankAccountSummary[]>([])

const fromCurrency = ref('EUR')
const toCurrency = ref('CZK')
const amount = ref<number | null>(null)

// Bank account selection (used when myBankAccounts is non-empty)
const fromBankAccountId = ref<string>('')
const toBankAccountId = ref<string>('')

const quote = ref<ForexQuote | null>(null)
const quoteError = ref<string | null>(null)
const quoteLoading = ref(false)

const swapResult = ref<ForexTradeResult | null>(null)
const swapError = ref<string | null>(null)
const swapLoading = ref(false)

const showConfirm = ref(false)

type ForexTab = 'swap' | 'rates' | 'history' | 'gold'

function parseForexTab(value: unknown): ForexTab {
  return value === 'rates' || value === 'history' || value === 'gold' ? value : 'swap'
}

function getInitialTab(): ForexTab {
  return parseForexTab(route.query.tab)
}

const activeTab = ref<ForexTab>(getInitialTab())

/** Whether the player has bank accounts and should use the bank-account-native swap form. */
const hasBankAccounts = computed(() => myBankAccounts.value.length > 0)

// Derived list of available currencies (EUR + all quoted currencies)
const availableCurrencies = computed(() => {
  const codes = new Set<string>(['EUR'])
  rates.value.forEach((r) => codes.add(r.quoteCurrencyCode))
  return Array.from(codes).sort()
})

/** All tradeable currencies as CurrencyBalance entries (0 balance when not held). Used for "You receive" selector. */
const toBalances = computed<CurrencyBalance[]>(() => {
  return availableCurrencies.value.map((code) => {
    const existing = balances.value.find((b) => b.currencyCode === code)
    if (existing) return existing
    const rate = rates.value.find((r) => r.quoteCurrencyCode === code)
    const symbol = rate?.quoteCurrencySymbol ?? code
    return { currencyCode: code, currencySymbol: symbol, balance: 0 }
  })
})

/** Resolved source currency code — from bank account when available, otherwise manual picker. */
const resolvedFromCurrency = computed(() => {
  if (hasBankAccounts.value && fromBankAccountId.value) {
    return myBankAccounts.value.find((a) => a.id === fromBankAccountId.value)?.currencyCode ?? fromCurrency.value
  }
  return fromCurrency.value
})

/** Resolved destination currency code. */
const resolvedToCurrency = computed(() => {
  if (hasBankAccounts.value && toBankAccountId.value) {
    return myBankAccounts.value.find((a) => a.id === toBankAccountId.value)?.currencyCode ?? toCurrency.value
  }
  return toCurrency.value
})

const fromBalance = computed(() => {
  if (hasBankAccounts.value && fromBankAccountId.value) {
    return myBankAccounts.value.find((a) => a.id === fromBankAccountId.value)?.balance ?? 0
  }
  const b = balances.value.find((b) => b.currencyCode === fromCurrency.value)
  return b?.balance ?? 0
})

const fromSymbol = computed(() => {
  if (hasBankAccounts.value && fromBankAccountId.value) {
    return myBankAccounts.value.find((a) => a.id === fromBankAccountId.value)?.currencySymbol ?? resolvedFromCurrency.value
  }
  if (fromCurrency.value === 'EUR') return '€'
  const r = rates.value.find((r) => r.quoteCurrencyCode === fromCurrency.value)
  return r?.quoteCurrencySymbol ?? fromCurrency.value
})

const toSymbol = computed(() => {
  if (hasBankAccounts.value && toBankAccountId.value) {
    return myBankAccounts.value.find((a) => a.id === toBankAccountId.value)?.currencySymbol ?? resolvedToCurrency.value
  }
  if (toCurrency.value === 'EUR') return '€'
  const r = rates.value.find((r) => r.quoteCurrencyCode === toCurrency.value)
  return r?.quoteCurrencySymbol ?? toCurrency.value
})

const validationError = computed<string | null>(() => {
  if (hasBankAccounts.value) {
    if (!fromBankAccountId.value) return t('forex.selectSourceAccount')
    if (!toBankAccountId.value) return t('forex.selectDestAccount')
    if (fromBankAccountId.value === toBankAccountId.value) return t('forex.sameAccount')
    if (resolvedFromCurrency.value === resolvedToCurrency.value) return t('forex.sameCurrency')
    if (!amount.value || amount.value <= 0) return t('forex.invalidAmount')
    if (amount.value > fromBalance.value) return t('forex.insufficientFunds')
    return null
  }
  if (fromCurrency.value === toCurrency.value) return t('forex.sameCurrency')
  if (!amount.value || amount.value <= 0) return t('forex.invalidAmount')
  if (amount.value > fromBalance.value) return t('forex.insufficientFunds')
  return null
})

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const ratesResult = await gqlRequest<{ fxRates: FxRate[] }>(`
      query {
        fxRates {
          baseCurrencyCode
          quoteCurrencyCode
          rate
          rateDate
          source
          quoteCurrencySymbol
        }
      }
    `)
    rates.value = ratesResult.fxRates ?? []

    if (auth.isAuthenticated) {
      const [balancesResult, historyResult, bankAccountsResult] = await Promise.all([
        gqlRequest<{ playerCurrencyBalances: CurrencyBalance[] }>(`
          query {
            playerCurrencyBalances {
              currencyCode
              currencySymbol
              balance
            }
          }
        `),
        gqlRequest<{ forexTradeHistory: ForexTradeHistoryEntry[] }>(`
          query {
            forexTradeHistory {
              id
              fromCurrencyCode
              toCurrencyCode
              fromAmount
              toAmount
              feeAmount
              rate
              executedAtTick
              executedAtUtc
              fromCurrencySymbol
              toCurrencySymbol
            }
          }
        `),
        gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(`
          query {
            myBankAccounts {
              id
              accountNumber
              currencyCode
              currencySymbol
              balance
              companyId
              companyName
            }
          }
        `),
      ])
      balances.value = balancesResult.playerCurrencyBalances ?? []
      history.value = historyResult.forexTradeHistory ?? []
      myBankAccounts.value = bankAccountsResult.myBankAccounts ?? []

      // Pre-select default bank accounts when available.
      if (myBankAccounts.value.length > 0 && !fromBankAccountId.value) {
        const firstEur = myBankAccounts.value.find((a) => a.currencyCode === 'EUR')
        fromBankAccountId.value = firstEur?.id ?? myBankAccounts.value[0]?.id ?? ''
      }
      if (myBankAccounts.value.length > 1 && !toBankAccountId.value) {
        const firstCzk = myBankAccounts.value.find((a) => a.currencyCode === 'CZK')
        const firstDifferent = myBankAccounts.value.find((a) => a.id !== fromBankAccountId.value)
        toBankAccountId.value = firstCzk?.id ?? firstDifferent?.id ?? ''
      }
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

async function fetchQuote() {
  if (validationError.value) return
  quoteLoading.value = true
  quoteError.value = null
  quote.value = null
  showConfirm.value = false
  swapResult.value = null
  swapError.value = null

  try {
    const inputVars: Record<string, unknown> = {
      fromCurrencyCode: resolvedFromCurrency.value,
      toCurrencyCode: resolvedToCurrency.value,
      amount: amount.value,
    }
    if (hasBankAccounts.value && fromBankAccountId.value) {
      inputVars.fromBankAccountId = fromBankAccountId.value
    }
    if (hasBankAccounts.value && toBankAccountId.value) {
      inputVars.toBankAccountId = toBankAccountId.value
    }

    const result = await gqlRequest<{ forexQuote: ForexQuote }>(
      `
      query ForexQuote($input: GetForexQuoteInput!) {
        forexQuote(input: $input) {
          fromCurrencyCode
          toCurrencyCode
          fromAmount
          toAmount
          feeAmount
          feePercent
          rate
          availableFromBalance
          fromCurrencySymbol
          toCurrencySymbol
        }
      }
    `,
      { input: inputVars },
    )
    quote.value = result.forexQuote
    showConfirm.value = true
  } catch (e: unknown) {
    quoteError.value = e instanceof Error ? e.message : t('forex.swapFailed')
  } finally {
    quoteLoading.value = false
  }
}

async function executeSwap() {
  if (!quote.value) return
  swapLoading.value = true
  swapError.value = null
  swapResult.value = null

  try {
    const inputVars: Record<string, unknown> = {
      fromCurrencyCode: resolvedFromCurrency.value,
      toCurrencyCode: resolvedToCurrency.value,
      amount: amount.value,
    }
    if (hasBankAccounts.value && fromBankAccountId.value) {
      inputVars.fromBankAccountId = fromBankAccountId.value
    }
    if (hasBankAccounts.value && toBankAccountId.value) {
      inputVars.toBankAccountId = toBankAccountId.value
    }

    const result = await gqlRequest<{ executeForexSwap: ForexTradeResult }>(
      `
      mutation ExecuteForexSwap($input: ExecuteForexSwapInput!) {
        executeForexSwap(input: $input) {
          tradeId
          fromCurrencyCode
          toCurrencyCode
          fromAmount
          toAmount
          feeAmount
          rate
          newFromBalance
          newToBalance
          fromCurrencySymbol
          toCurrencySymbol
        }
      }
    `,
      { input: inputVars },
    )
    swapResult.value = result.executeForexSwap
    showConfirm.value = false
    quote.value = null
    amount.value = null
    // Refresh balances and history
    await loadData()
  } catch (e: unknown) {
    swapError.value = e instanceof Error ? e.message : t('forex.swapFailed')
  } finally {
    swapLoading.value = false
  }
}

function cancelQuote() {
  showConfirm.value = false
  quote.value = null
}

function swapCurrencies() {
  if (hasBankAccounts.value) {
    const tmp = fromBankAccountId.value
    fromBankAccountId.value = toBankAccountId.value
    toBankAccountId.value = tmp
  } else {
    const tmp = fromCurrency.value
    fromCurrency.value = toCurrency.value
    toCurrency.value = tmp
  }
  quote.value = null
  showConfirm.value = false
}

function formatAmount(val: number): string {
  return val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })
}

function formatTick(tick: number): string {
  return tick.toLocaleString()
}

function applyQueryDefaults() {
  activeTab.value = parseForexTab(route.query.tab)

  const queryToCurrency = typeof route.query.toCurrency === 'string' ? route.query.toCurrency.toUpperCase() : null
  if (queryToCurrency && availableCurrencies.value.includes(queryToCurrency) && !hasBankAccounts.value) {
    toCurrency.value = queryToCurrency
    if (fromCurrency.value === queryToCurrency) {
      const preferredSourceCurrency = availableCurrencies.value.find((code) => code !== queryToCurrency)
      if (preferredSourceCurrency) {
        fromCurrency.value = preferredSourceCurrency
      }
    }
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }
  await loadData()
  applyQueryDefaults()
})

watch(activeTab, async (tab) => {
  const nextQuery = { ...route.query }
  if (tab === 'swap') {
    delete nextQuery.tab
  } else {
    nextQuery.tab = tab
  }
  await router.replace({ query: nextQuery })
})
</script>

<template>
  <main class="forex-page">
    <div class="container">
      <div class="forex-hero">
        <h1 class="forex-title">{{ t('forex.title') }}</h1>
        <p class="forex-subtitle">{{ t('forex.subtitle') }}</p>
      </div>

      <div v-if="loading" class="forex-loading">
        <span>{{ t('common.loading') }}</span>
      </div>

      <div v-else-if="error" class="forex-error">
        <p>{{ error }}</p>
        <button class="btn btn-secondary" @click="loadData">{{ t('common.retry') }}</button>
      </div>

      <template v-else>
        <div class="forex-tabs" role="tablist" :aria-label="t('forex.tabsLabel')">
          <button
            role="tab"
            class="forex-tab"
            :class="{ active: activeTab === 'swap' }"
            :aria-selected="activeTab === 'swap'"
            @click="activeTab = 'swap'"
          >
            {{ t('forex.tabSwap') }}
          </button>
          <button
            role="tab"
            class="forex-tab"
            :class="{ active: activeTab === 'rates' }"
            :aria-selected="activeTab === 'rates'"
            @click="activeTab = 'rates'"
          >
            {{ t('forex.tabRateList') }}
          </button>
          <button
            role="tab"
            class="forex-tab"
            :class="{ active: activeTab === 'history' }"
            :aria-selected="activeTab === 'history'"
            @click="activeTab = 'history'"
          >
            {{ t('forex.tabHistory') }}
          </button>
          <button
            role="tab"
            class="forex-tab"
            :class="{ active: activeTab === 'gold' }"
            :aria-selected="activeTab === 'gold'"
            @click="activeTab = 'gold'"
          >
            {{ t('forex.tabGold') }}
          </button>
        </div>

        <section v-if="activeTab === 'swap'" class="forex-section" aria-label="Forex Swap">
          <h2 class="section-title">{{ t('forex.tabSwap') }}</h2>

          <div v-if="hasBankAccounts" class="ba-notice" role="note">
            <span class="ba-notice-icon">🏦</span>
            {{ t('forex.bankAccountMode') }}
            <RouterLink v-if="auth.player?.companies?.length" :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`" class="statement-link-inline">
              {{ t('forex.viewBankStatement') }} →
            </RouterLink>
          </div>

          <div v-if="!hasBankAccounts" class="balances-summary">
            <h3 class="subsection-title">{{ t('forex.balancesTitle') }}</h3>
            <RouterLink
              v-if="auth.player?.companies?.length"
              :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`"
              class="statement-link"
            >
              {{ t('forex.viewBankStatement') }} →
            </RouterLink>
            <div v-if="balances.length === 0" class="balances-empty">{{ t('forex.balancesEmpty') }}</div>
            <div v-else class="balances-grid">
              <div v-for="b in balances" :key="b.currencyCode" class="balance-card">
                <span class="balance-symbol">{{ b.currencySymbol }}</span>
                <span class="balance-code">{{ b.currencyCode }}</span>
                <span class="balance-amount">{{ formatAmount(b.balance) }}</span>
              </div>
            </div>
          </div>

          <div v-if="swapResult" class="swap-result-banner" role="status">
            <span class="result-icon">✓</span>
            <span>
              {{
                t('forex.swapResultDetail', {
                  fromAmount: formatAmount(swapResult.fromAmount),
                  fromSymbol: swapResult.fromCurrencySymbol,
                  toAmount: formatAmount(swapResult.toAmount),
                  toSymbol: swapResult.toCurrencySymbol,
                })
              }}
            </span>
            <div class="swap-result-balances">
              <span class="balance-tag">{{ swapResult.fromCurrencyCode }}: {{ formatAmount(swapResult.newFromBalance) }}</span>
              <span class="balance-tag">{{ swapResult.toCurrencyCode }}: {{ formatAmount(swapResult.newToBalance) }}</span>
            </div>
          </div>

          <div class="swap-form">
            <div class="swap-row">
              <div class="swap-field">
                <template v-if="hasBankAccounts">
                  <ForexBankAccountSelector
                    v-model="fromBankAccountId"
                    :accounts="myBankAccounts"
                    :label="t('forex.sourceAccount')"
                    id="from-bank-account"
                    @update:model-value="() => { quote = null; showConfirm = false }"
                  />
                </template>
                <template v-else>
                  <!--
                    Both selectors intentionally use `toBalances` (all tradeable currencies,
                    including 0-balance entries). This is required so the ⇅ swap button can
                    reverse EUR→CZK to CZK→EUR even before the player holds any CZK.
                    The affordability validation (`validationError`) catches a 0-balance source
                    and shows "Insufficient balance for this swap." before the swap proceeds.
                  -->
                  <BankAccountSelector
                    v-model="fromCurrency"
                    :balances="toBalances"
                    :label="t('forex.sourceCurrency')"
                    id="from-currency"
                    @update:model-value="() => { quote = null; showConfirm = false }"
                  />
                </template>
              </div>
              <div class="swap-field amount-field">
                <label class="field-label" for="swap-amount">{{ t('forex.amount') }}</label>
                <div class="input-with-symbol">
                  <span class="currency-symbol-badge">{{ fromSymbol }}</span>
                  <input
                    id="swap-amount"
                    v-model.number="amount"
                    type="number"
                    min="0"
                    step="any"
                    :placeholder="t('forex.amountPlaceholder')"
                    class="amount-input"
                    @input="quote = null; showConfirm = false"
                  />
                </div>
                <span class="field-hint">{{ t('forex.availableBalance') }}: {{ fromSymbol }}{{ formatAmount(fromBalance) }}</span>
              </div>
            </div>

            <div class="swap-arrow-row">
              <button class="swap-arrow-btn" :title="'Swap currencies'" @click="swapCurrencies" aria-label="Swap currencies">
                ⇅
              </button>
            </div>

            <div class="swap-row">
              <div class="swap-field">
                <template v-if="hasBankAccounts">
                  <ForexBankAccountSelector
                    v-model="toBankAccountId"
                    :accounts="myBankAccounts"
                    :label="t('forex.destAccount')"
                    id="to-bank-account"
                    @update:model-value="() => { quote = null; showConfirm = false }"
                  />
                </template>
                <template v-else>
                  <BankAccountSelector
                    v-model="toCurrency"
                    :balances="toBalances"
                    :label="t('forex.targetCurrency')"
                    id="to-currency"
                    @update:model-value="() => { quote = null; showConfirm = false }"
                  />
                </template>
              </div>
              <div class="swap-field amount-field">
                <label class="field-label">{{ t('forex.youReceive') }}</label>
                <div class="input-with-symbol">
                  <span class="currency-symbol-badge">{{ toSymbol }}</span>
                  <div class="receive-amount">
                    <span v-if="quote">{{ formatAmount(quote.toAmount) }}</span>
                    <span v-else class="receive-placeholder">—</span>
                  </div>
                </div>
              </div>
            </div>

            <div v-if="validationError && amount" class="validation-error" role="alert">
              {{ validationError }}
            </div>

            <div v-if="quoteError" class="swap-error" role="alert">{{ quoteError }}</div>

            <div v-if="!showConfirm" class="swap-actions">
              <button
                class="btn btn-primary"
                :disabled="quoteLoading || !!validationError || !amount"
                @click="fetchQuote"
              >
                {{ quoteLoading ? t('common.loading') : t('forex.getQuote') }}
              </button>
            </div>
          </div>

          <div v-if="showConfirm && quote" class="quote-card" role="region" aria-label="Exchange Quote">
            <h3 class="quote-title">{{ t('forex.quoteTitle') }}</h3>
            <table class="quote-table">
              <tbody>
                <tr>
                  <td class="quote-label">{{ t('forex.rate') }}</td>
                  <td class="quote-value">
                    1 {{ quote.fromCurrencyCode }} = {{ formatAmount(quote.rate) }} {{ quote.toCurrencyCode }}
                  </td>
                </tr>
                <tr>
                  <td class="quote-label">{{ t('forex.fee') }}</td>
                  <td class="quote-value fee-value">
                    {{ quote.fromCurrencySymbol }}{{ formatAmount(quote.feeAmount) }}
                  </td>
                </tr>
                <tr class="quote-total-row">
                  <td class="quote-label">{{ t('forex.youReceive') }}</td>
                  <td class="quote-value receive-value">
                    {{ quote.toCurrencySymbol }}{{ formatAmount(quote.toAmount) }}
                  </td>
                </tr>
              </tbody>
            </table>

            <div v-if="swapError" class="swap-error" role="alert">{{ swapError }}</div>

            <div class="confirm-actions">
              <button class="btn btn-secondary" :disabled="swapLoading" @click="cancelQuote">
                {{ t('forex.cancel') }}
              </button>
              <button class="btn btn-primary" :disabled="swapLoading" @click="executeSwap">
                {{ swapLoading ? t('common.loading') : t('forex.confirmSwap') }}
              </button>
            </div>
          </div>
        </section>

        <section v-else-if="activeTab === 'rates'" class="forex-section" aria-label="Rate List">
          <h2 class="section-title">{{ t('forex.rateListTitle') }}</h2>
          <div v-if="rates.length === 0" class="history-empty">{{ t('forex.rateListEmpty') }}</div>
          <div v-else class="history-table-wrap">
            <table class="history-table rates-table">
              <thead>
                <tr>
                  <th>{{ t('forex.rateListPair') }}</th>
                  <th>{{ t('forex.rate') }}</th>
                  <th>{{ t('forex.executedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="rateEntry in rates" :key="`${rateEntry.baseCurrencyCode}-${rateEntry.quoteCurrencyCode}`" class="history-row">
                  <td>{{ rateEntry.baseCurrencyCode }}/{{ rateEntry.quoteCurrencyCode }}</td>
                  <td class="rate-cell">{{ formatAmount(rateEntry.rate) }}</td>
                  <td class="tick-cell">{{ rateEntry.rateDate }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section v-else-if="activeTab === 'history'" class="forex-section" aria-label="Trade History">
          <h2 class="section-title">{{ t('forex.historyTitle') }}</h2>
          <div v-if="history.length === 0" class="history-empty">{{ t('forex.historyEmpty') }}</div>
          <div v-else class="history-table-wrap">
            <table class="history-table">
              <thead>
                <tr>
                  <th>{{ t('forex.fromAmount') }}</th>
                  <th>{{ t('forex.toAmount') }}</th>
                  <th>{{ t('forex.rate') }}</th>
                  <th>{{ t('forex.feeAmount') }}</th>
                  <th>{{ t('forex.executedAt') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="entry in history" :key="entry.id" class="history-row">
                  <td>
                    <span class="currency-badge">{{ entry.fromCurrencySymbol }}</span>
                    {{ formatAmount(entry.fromAmount) }}
                    <span class="currency-code">{{ entry.fromCurrencyCode }}</span>
                  </td>
                  <td>
                    <span class="currency-badge">{{ entry.toCurrencySymbol }}</span>
                    {{ formatAmount(entry.toAmount) }}
                    <span class="currency-code">{{ entry.toCurrencyCode }}</span>
                  </td>
                  <td class="rate-cell">{{ formatAmount(entry.rate) }}</td>
                  <td class="fee-cell">{{ entry.fromCurrencySymbol }}{{ formatAmount(entry.feeAmount) }}</td>
                  <td class="tick-cell">{{ formatTick(entry.executedAtTick) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>

      <GoldAmmSection
        v-if="!loading && !error && activeTab === 'gold'"
        :available-currencies="availableCurrencies"
        :balances="balances"
        @refresh="loadData"
      />
    </div>
  </main>
</template>

<style scoped>
.forex-page {
  padding: 2rem 0 4rem;
  min-height: calc(100vh - 64px);
}

.forex-hero {
  margin-bottom: 2rem;
}

.forex-title {
  font-size: 2rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 0.5rem;
}

.forex-subtitle {
  color: var(--color-text-muted);
  font-size: 1.05rem;
}

.forex-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1.5rem;
}

.forex-tab {
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  border-radius: 999px;
  padding: 0.55rem 1rem;
  font-weight: 600;
  cursor: pointer;
  transition:
    background 0.15s ease,
    color 0.15s ease,
    border-color 0.15s ease;
}

.forex-tab.active,
.forex-tab:hover {
  background: var(--color-accent, #4f8ef7);
  border-color: var(--color-accent, #4f8ef7);
  color: #fff;
}

.forex-loading,
.forex-error {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-muted);
}

.forex-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg, 12px);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.section-title {
  font-size: 1.1rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--color-border);
}

.subsection-title {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin-bottom: 0.75rem;
}

.balances-summary {
  margin-bottom: 1.5rem;
}

.statement-link {
  display: inline-block;
  font-size: 0.82rem;
  font-weight: 600;
  color: var(--color-accent, #4f8ef7);
  text-decoration: none;
  margin-bottom: 0.6rem;
}

.statement-link:hover {
  text-decoration: underline;
}

.ba-notice {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: var(--color-surface-alt, #1e2a3a);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 0.65rem 1rem;
  margin-bottom: 1.25rem;
  font-size: 0.9rem;
  color: var(--color-text-secondary);
}

.ba-notice-icon {
  font-size: 1.1rem;
}

.statement-link-inline {
  font-size: 0.82rem;
  font-weight: 600;
  color: var(--color-accent, #4f8ef7);
  text-decoration: none;
  margin-left: 0.25rem;
}

.statement-link-inline:hover {
  text-decoration: underline;
}

/* Balances */
.balances-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.balance-card {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  background: var(--color-surface-alt, var(--color-surface));
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 0.6rem 1rem;
}

.balance-symbol {
  font-size: 1.1rem;
  color: var(--color-accent, #4f8ef7);
}

.balance-code {
  font-weight: 600;
  color: var(--color-text-secondary);
}

.balance-amount {
  font-weight: 700;
  color: var(--color-text-primary);
}

.balances-empty,
.history-empty {
  color: var(--color-text-muted);
  font-style: italic;
}

/* Swap form */
.swap-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.swap-row {
  display: grid;
  grid-template-columns: 1fr 2fr;
  gap: 1rem;
  align-items: end;
}

.swap-field {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.field-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.currency-select {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 0.6rem 0.75rem;
  color: var(--color-text-primary);
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
}

.currency-select:focus {
  outline: 2px solid var(--color-accent, #4f8ef7);
}

.input-with-symbol {
  display: flex;
  align-items: center;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  overflow: hidden;
  background: var(--color-bg);
}

.currency-symbol-badge {
  padding: 0.6rem 0.75rem;
  background: var(--color-surface-alt, #1e2a3a);
  font-weight: 700;
  color: var(--color-accent, #4f8ef7);
  border-right: 1px solid var(--color-border);
  min-width: 2.5rem;
  text-align: center;
}

.amount-input {
  flex: 1;
  background: transparent;
  border: none;
  padding: 0.6rem 0.75rem;
  color: var(--color-text-primary);
  font-size: 1rem;
  font-weight: 600;
}

.amount-input:focus {
  outline: none;
}

.receive-amount {
  flex: 1;
  padding: 0.6rem 0.75rem;
  font-size: 1rem;
  font-weight: 700;
  color: var(--color-success, #28c76f);
}

.receive-placeholder {
  color: var(--color-text-muted);
  font-weight: 400;
}

.field-hint {
  font-size: 0.78rem;
  color: var(--color-text-muted);
}

.swap-arrow-row {
  display: flex;
  justify-content: center;
}

.swap-arrow-btn {
  background: var(--color-surface-alt, #1e2a3a);
  border: 1px solid var(--color-border);
  border-radius: 50%;
  width: 2.2rem;
  height: 2.2rem;
  font-size: 1.2rem;
  cursor: pointer;
  color: var(--color-text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.15s;
}

.swap-arrow-btn:hover {
  background: var(--color-accent, #4f8ef7);
  color: #fff;
}

.swap-actions {
  display: flex;
  justify-content: flex-start;
  margin-top: 0.5rem;
}

.validation-error {
  color: var(--color-error, #ea5455);
  font-size: 0.88rem;
  padding: 0.5rem;
  background: rgba(234, 84, 85, 0.08);
  border-radius: 6px;
}

.swap-error {
  color: var(--color-error, #ea5455);
  font-size: 0.88rem;
  padding: 0.5rem;
  background: rgba(234, 84, 85, 0.08);
  border-radius: 6px;
  margin-top: 0.5rem;
}

/* Quote card */
.quote-card {
  margin-top: 1.25rem;
  border: 1px solid var(--color-accent, #4f8ef7);
  border-radius: 10px;
  padding: 1.25rem;
  background: var(--color-surface-alt, #1a2332);
}

.quote-title {
  font-size: 1rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 0.75rem;
}

.quote-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1rem;
}

.quote-table td {
  padding: 0.4rem 0.5rem;
  font-size: 0.95rem;
}

.quote-label {
  color: var(--color-text-secondary);
  width: 40%;
}

.quote-value {
  font-weight: 600;
  color: var(--color-text-primary);
  text-align: right;
}

.fee-value {
  color: var(--color-warning, #ff9f43);
}

.quote-total-row .quote-value.receive-value {
  font-size: 1.1rem;
  color: var(--color-success, #28c76f);
}

.confirm-actions {
  display: flex;
  gap: 0.75rem;
  justify-content: flex-end;
  margin-top: 0.5rem;
}

/* Swap result */
.swap-result-banner {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  padding: 0.85rem 1rem;
  background: rgba(40, 199, 111, 0.1);
  border: 1px solid var(--color-success, #28c76f);
  border-radius: 8px;
  margin-bottom: 1rem;
  color: var(--color-success, #28c76f);
  font-weight: 600;
}

.result-icon {
  font-size: 1.1rem;
}

.swap-result-balances {
  display: flex;
  gap: 1rem;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  font-weight: 400;
}

.balance-tag {
  background: var(--color-surface-alt, #1e2a3a);
  border: 1px solid var(--color-border);
  border-radius: 4px;
  padding: 0.15rem 0.5rem;
}

/* Trade history */
.history-table-wrap {
  overflow-x: auto;
}

.history-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.history-table th {
  text-align: left;
  padding: 0.5rem 0.75rem;
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-muted);
  border-bottom: 1px solid var(--color-border);
}

.history-row td {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  vertical-align: middle;
}

.history-row:last-child td {
  border-bottom: none;
}

.history-row:hover td {
  background: var(--color-surface-alt, #1e2a3a);
}

.currency-badge {
  font-weight: 700;
  color: var(--color-accent, #4f8ef7);
  margin-right: 0.15rem;
}

.currency-code {
  font-size: 0.8rem;
  color: var(--color-text-muted);
  margin-left: 0.25rem;
}

.rate-cell {
  color: var(--color-text-secondary);
}

.fee-cell {
  color: var(--color-warning, #ff9f43);
}

.tick-cell {
  color: var(--color-text-muted);
  font-size: 0.85rem;
}

@media (max-width: 640px) {
  .forex-tabs {
    gap: 0.5rem;
  }

  .forex-tab {
    flex: 1 1 calc(50% - 0.25rem);
    justify-content: center;
  }

  .swap-row {
    grid-template-columns: 1fr;
  }

  .balances-grid {
    gap: 0.5rem;
  }
}
</style>
