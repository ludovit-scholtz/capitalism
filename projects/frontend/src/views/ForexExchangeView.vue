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

/** Helper — find a bank account in myBankAccounts by ID. */
function findAccountById(id: string): PlayerBankAccountSummary | undefined {
  return myBankAccounts.value.find((a) => a.id === id)
}

/** Resolved source currency code — from bank account when available, otherwise manual picker. */
const resolvedFromCurrency = computed(() => {
  if (hasBankAccounts.value && fromBankAccountId.value) {
    return findAccountById(fromBankAccountId.value)?.currencyCode ?? fromCurrency.value
  }
  return fromCurrency.value
})

/** Resolved destination currency code. */
const resolvedToCurrency = computed(() => {
  if (hasBankAccounts.value && toBankAccountId.value) {
    return findAccountById(toBankAccountId.value)?.currencyCode ?? toCurrency.value
  }
  return toCurrency.value
})

const fromBalance = computed(() => {
  if (hasBankAccounts.value && fromBankAccountId.value) {
    return findAccountById(fromBankAccountId.value)?.balance ?? 0
  }
  const b = balances.value.find((b) => b.currencyCode === fromCurrency.value)
  return b?.balance ?? 0
})

const fromSymbol = computed(() => {
  if (hasBankAccounts.value && fromBankAccountId.value) {
    return findAccountById(fromBankAccountId.value)?.currencySymbol ?? resolvedFromCurrency.value
  }
  if (fromCurrency.value === 'EUR') return '€'
  const r = rates.value.find((r) => r.quoteCurrencyCode === fromCurrency.value)
  return r?.quoteCurrencySymbol ?? fromCurrency.value
})

const toSymbol = computed(() => {
  if (hasBankAccounts.value && toBankAccountId.value) {
    return findAccountById(toBankAccountId.value)?.currencySymbol ?? resolvedToCurrency.value
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
  <main class="container py-8 pb-16 min-h-[calc(100vh-64px)]">
    <!-- Hero -->
    <div class="forex-hero mb-8">
      <h1 class="text-3xl font-bold text-body mb-1">{{ t('forex.title') }}</h1>
      <p class="text-muted text-base">{{ t('forex.subtitle') }}</p>
    </div>

    <div v-if="loading" class="text-center py-12 text-muted">
      <span>{{ t('common.loading') }}</span>
    </div>

    <div v-else-if="error" class="text-center py-12 text-muted">
      <p class="text-bad mb-4">{{ error }}</p>
      <button class="btn btn-secondary" @click="loadData">{{ t('common.retry') }}</button>
    </div>

    <template v-else>
      <!-- Tabs -->
      <div class="flex flex-wrap gap-3 mb-6" role="tablist" :aria-label="t('forex.tabsLabel')">
        <button
          role="tab"
          :aria-selected="activeTab === 'swap'"
          class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
          :class="activeTab === 'swap'
            ? 'bg-brand border-brand text-white'
            : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
          @click="activeTab = 'swap'"
        >
          {{ t('forex.tabSwap') }}
        </button>
        <button
          role="tab"
          :aria-selected="activeTab === 'rates'"
          class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
          :class="activeTab === 'rates'
            ? 'bg-brand border-brand text-white'
            : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
          @click="activeTab = 'rates'"
        >
          {{ t('forex.tabRateList') }}
        </button>
        <button
          role="tab"
          :aria-selected="activeTab === 'history'"
          class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
          :class="activeTab === 'history'
            ? 'bg-brand border-brand text-white'
            : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
          @click="activeTab = 'history'"
        >
          {{ t('forex.tabHistory') }}
        </button>
        <button
          role="tab"
          :aria-selected="activeTab === 'gold'"
          class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
          :class="activeTab === 'gold'
            ? 'bg-brand border-brand text-white'
            : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
          @click="activeTab = 'gold'"
        >
          {{ t('forex.tabGold') }}
        </button>
      </div>

      <!-- Swap Tab -->
      <section
        v-if="activeTab === 'swap'"
        class="bg-card border border-divider rounded-xl p-6 mb-6"
        aria-label="Forex Swap"
      >
        <h2 class="text-lg font-semibold text-body mb-4 pb-3 border-b border-divider">
          {{ t('forex.tabSwap') }}
        </h2>

        <!-- Bank account mode notice -->
        <div
          v-if="hasBankAccounts"
          class="ba-notice flex items-center gap-2 bg-card-raised border border-divider rounded-lg px-4 py-2.5 mb-5 text-sm text-muted"
          role="note"
        >
          <span class="text-lg">🏦</span>
          <span>{{ t('forex.bankAccountMode') }}</span>
          <RouterLink
            v-if="auth.player?.companies?.length"
            :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`"
            class="ml-1 text-xs font-semibold text-brand hover:underline"
          >
            {{ t('forex.viewBankStatement') }} →
          </RouterLink>
        </div>

        <!-- Balances summary (non-bank-account mode) -->
        <div v-if="!hasBankAccounts" class="mb-6">
          <h3 class="text-sm font-semibold text-muted mb-2">{{ t('forex.balancesTitle') }}</h3>
          <RouterLink
            v-if="auth.player?.companies?.length"
            :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`"
            class="statement-link inline-block text-xs font-semibold text-brand hover:underline mb-3"
          >
            {{ t('forex.viewBankStatement') }} →
          </RouterLink>
          <div v-if="balances.length === 0" class="text-sm text-muted italic">
            {{ t('forex.balancesEmpty') }}
          </div>
          <div v-else class="flex flex-wrap gap-3">
            <div
              v-for="b in balances"
              :key="b.currencyCode"
              class="balance-card flex items-center gap-1.5 bg-card-raised border border-divider rounded-lg px-3 py-2"
            >
              <span class="text-brand font-semibold">{{ b.currencySymbol }}</span>
              <span class="text-sm font-semibold text-muted">{{ b.currencyCode }}</span>
              <span class="text-sm font-bold text-body">{{ formatAmount(b.balance) }}</span>
            </div>
          </div>
        </div>

        <!-- Swap success banner -->
        <div
          v-if="swapResult"
          class="swap-result-banner flex flex-col gap-1.5 px-4 py-3 bg-good/10 border border-good rounded-lg mb-4 text-good font-semibold"
          role="status"
        >
          <div class="flex items-center gap-2">
            <span>✓</span>
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
          </div>
          <div class="flex gap-3 text-xs text-muted font-normal">
            <span class="bg-card-raised border border-divider rounded px-2 py-0.5">
              {{ swapResult.fromCurrencyCode }}: {{ formatAmount(swapResult.newFromBalance) }}
            </span>
            <span class="bg-card-raised border border-divider rounded px-2 py-0.5">
              {{ swapResult.toCurrencyCode }}: {{ formatAmount(swapResult.newToBalance) }}
            </span>
          </div>
        </div>

        <!-- Swap form -->
        <div class="flex flex-col gap-4">
          <!-- From row -->
          <div class="grid grid-cols-1 sm:grid-cols-[1fr_2fr] gap-4 items-end">
            <div class="flex flex-col gap-1">
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
                <BankAccountSelector
                  v-model="fromCurrency"
                  :balances="toBalances"
                  :label="t('forex.sourceCurrency')"
                  id="from-currency"
                  @update:model-value="() => { quote = null; showConfirm = false }"
                />
              </template>
            </div>

            <!-- Amount input -->
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-muted uppercase tracking-wide" for="swap-amount">
                {{ t('forex.amount') }}
              </label>
              <div class="flex items-center border border-divider rounded-lg overflow-hidden bg-page">
                <span class="px-3 py-2.5 bg-card-raised border-r border-divider font-bold text-brand min-w-[2.5rem] text-center text-sm">
                  {{ fromSymbol }}
                </span>
                <input
                  id="swap-amount"
                  v-model.number="amount"
                  type="number"
                  min="0"
                  step="any"
                  :placeholder="t('forex.amountPlaceholder')"
                  class="flex-1 bg-transparent border-none px-3 py-2.5 text-body text-base font-semibold focus:outline-none"
                  @input="quote = null; showConfirm = false"
                />
              </div>
              <span class="field-hint text-xs text-muted mt-0.5">
                {{ t('forex.availableBalance') }}: {{ fromSymbol }}{{ formatAmount(fromBalance) }}
              </span>
            </div>
          </div>

          <!-- Swap direction button -->
          <div class="flex justify-center">
            <button
              class="w-9 h-9 rounded-full bg-card-raised border border-divider text-muted text-lg flex items-center justify-center transition-colors hover:bg-brand hover:text-white hover:border-brand"
              :title="'Swap currencies'"
              aria-label="Swap currencies"
              @click="swapCurrencies"
            >
              ⇅
            </button>
          </div>

          <!-- To row -->
          <div class="grid grid-cols-1 sm:grid-cols-[1fr_2fr] gap-4 items-end">
            <div class="flex flex-col gap-1">
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

            <!-- You receive -->
            <div class="flex flex-col gap-1">
              <label class="text-xs font-semibold text-muted uppercase tracking-wide">
                {{ t('forex.youReceive') }}
              </label>
              <div class="flex items-center border border-divider rounded-lg overflow-hidden bg-page">
                <span class="px-3 py-2.5 bg-card-raised border-r border-divider font-bold text-brand min-w-[2.5rem] text-center text-sm">
                  {{ toSymbol }}
                </span>
                <div class="flex-1 px-3 py-2.5 text-base font-bold text-good">
                  <span v-if="quote">{{ formatAmount(quote.toAmount) }}</span>
                  <span v-else class="text-muted font-normal">—</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Validation / quote errors -->
          <div
            v-if="validationError && amount"
            class="validation-error text-sm text-bad px-3 py-2 bg-bad/10 rounded-md"
            role="alert"
          >
            {{ validationError }}
          </div>
          <div
            v-if="quoteError"
            class="text-sm text-bad px-3 py-2 bg-bad/10 rounded-md"
            role="alert"
          >
            {{ quoteError }}
          </div>

          <!-- Get quote action -->
          <div v-if="!showConfirm">
            <button
              class="btn btn-primary"
              :disabled="quoteLoading || !!validationError || !amount"
              @click="fetchQuote"
            >
              {{ quoteLoading ? t('common.loading') : t('forex.getQuote') }}
            </button>
          </div>
        </div>

        <!-- Quote confirmation card -->
        <div
          v-if="showConfirm && quote"
          class="mt-5 border border-brand rounded-xl p-5 bg-card-raised"
          role="region"
          aria-label="Exchange Quote"
        >
          <h3 class="text-base font-bold text-body mb-3">{{ t('forex.quoteTitle') }}</h3>
          <table class="quote-table w-full border-collapse mb-4 text-sm">
            <tbody>
              <tr>
                <td class="py-1.5 text-muted w-2/5">{{ t('forex.rate') }}</td>
                <td class="py-1.5 font-semibold text-body text-right">
                  1 {{ quote.fromCurrencyCode }} = {{ formatAmount(quote.rate) }} {{ quote.toCurrencyCode }}
                </td>
              </tr>
              <tr>
                <td class="py-1.5 text-muted">{{ t('forex.fee') }}</td>
                <td class="py-1.5 font-semibold text-caution text-right">
                  {{ quote.fromCurrencySymbol }}{{ formatAmount(quote.feeAmount) }}
                </td>
              </tr>
              <tr>
                <td class="py-1.5 text-muted">{{ t('forex.youReceive') }}</td>
                <td class="py-1.5 font-bold text-good text-right text-base">
                  {{ quote.toCurrencySymbol }}{{ formatAmount(quote.toAmount) }}
                </td>
              </tr>
            </tbody>
          </table>

          <div
            v-if="swapError"
            class="text-sm text-bad px-3 py-2 bg-bad/10 rounded-md mb-3"
            role="alert"
          >
            {{ swapError }}
          </div>

          <div class="flex gap-3 justify-end">
            <button class="btn btn-secondary" :disabled="swapLoading" @click="cancelQuote">
              {{ t('forex.cancel') }}
            </button>
            <button class="btn btn-primary" :disabled="swapLoading" @click="executeSwap">
              {{ swapLoading ? t('common.loading') : t('forex.confirmSwap') }}
            </button>
          </div>
        </div>
      </section>

      <!-- Rates Tab -->
      <section
        v-else-if="activeTab === 'rates'"
        class="bg-card border border-divider rounded-xl p-6 mb-6"
        aria-label="Rate List"
      >
        <h2 class="text-lg font-semibold text-body mb-4 pb-3 border-b border-divider">
          {{ t('forex.rateListTitle') }}
        </h2>
        <div v-if="rates.length === 0" class="text-sm text-muted italic">
          {{ t('forex.rateListEmpty') }}
        </div>
        <div v-else class="overflow-x-auto">
          <table class="w-full border-collapse text-sm rates-table">
            <thead>
              <tr>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.rateListPair') }}
                </th>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.rate') }}
                </th>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.executedAt') }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="rateEntry in rates"
                :key="`${rateEntry.baseCurrencyCode}-${rateEntry.quoteCurrencyCode}`"
                class="history-row"
              >
                <td class="px-3 py-2.5 font-semibold text-body align-middle">
                  {{ rateEntry.baseCurrencyCode }}/{{ rateEntry.quoteCurrencyCode }}
                </td>
                <td class="px-3 py-2.5 text-muted align-middle">{{ formatAmount(rateEntry.rate) }}</td>
                <td class="px-3 py-2.5 text-subtle text-xs align-middle">{{ rateEntry.rateDate }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- History Tab -->
      <section
        v-else-if="activeTab === 'history'"
        class="bg-card border border-divider rounded-xl p-6 mb-6"
        aria-label="Trade History"
      >
        <h2 class="text-lg font-semibold text-body mb-4 pb-3 border-b border-divider">
          {{ t('forex.historyTitle') }}
        </h2>
        <div v-if="history.length === 0" class="text-sm text-muted italic">
          {{ t('forex.historyEmpty') }}
        </div>
        <div v-else class="overflow-x-auto">
          <table class="w-full border-collapse text-sm">
            <thead>
              <tr>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.fromAmount') }}
                </th>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.toAmount') }}
                </th>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.rate') }}
                </th>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.feeAmount') }}
                </th>
                <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                  {{ t('forex.executedAt') }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="entry in history" :key="entry.id" class="history-row">
                <td class="px-3 py-2.5 align-middle">
                  <span class="font-bold text-brand mr-0.5">{{ entry.fromCurrencySymbol }}</span>
                  {{ formatAmount(entry.fromAmount) }}
                  <span class="text-xs text-muted ml-1">{{ entry.fromCurrencyCode }}</span>
                </td>
                <td class="px-3 py-2.5 align-middle">
                  <span class="font-bold text-brand mr-0.5">{{ entry.toCurrencySymbol }}</span>
                  {{ formatAmount(entry.toAmount) }}
                  <span class="text-xs text-muted ml-1">{{ entry.toCurrencyCode }}</span>
                </td>
                <td class="px-3 py-2.5 text-muted align-middle">{{ formatAmount(entry.rate) }}</td>
                <td class="px-3 py-2.5 text-caution align-middle">
                  {{ entry.fromCurrencySymbol }}{{ formatAmount(entry.feeAmount) }}
                </td>
                <td class="px-3 py-2.5 text-subtle text-xs align-middle">
                  {{ formatTick(entry.executedAtTick) }}
                </td>
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
  </main>
</template>

<style scoped>
/* Table row hover — cannot target child <td> elements with Tailwind parent-hover */
.history-row td {
  border-bottom: 1px solid var(--color-border-light, rgba(48, 54, 61, 0.5));
}
.history-row:last-child td {
  border-bottom: none;
}
.history-row:hover td {
  background: var(--color-surface-raised);
}
</style>
