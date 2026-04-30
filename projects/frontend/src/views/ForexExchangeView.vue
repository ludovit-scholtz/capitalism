<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import GoldAmmSection from '@/components/forex/GoldAmmSection.vue'
import BankAccountTransferPanel from '@/components/banking/BankAccountTransferPanel.vue'
import BankAccountSelector from '@/components/banking/BankAccountSelector.vue'
import ForexBankAccountSelector from '@/components/forex/ForexBankAccountSelector.vue'
import type { City, FxRate, ForexQuote, ForexTradeResult, ForexTradeHistoryEntry, CurrencyBalance, PlayerBankAccountSummary } from '@/types'

const { t } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)

const rates = ref<FxRate[]>([])
const balances = ref<CurrencyBalance[]>([])
const history = ref<ForexTradeHistoryEntry[]>([])
const myBankAccounts = ref<PlayerBankAccountSummary[]>([])
const cities = ref<City[]>([])

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

type ForexTab = 'swap' | 'transfer' | 'rates' | 'history' | 'gold'

function parseForexTab(value: unknown): ForexTab {
  return value === 'transfer' || value === 'rates' || value === 'history' || value === 'gold' ? value : 'swap'
}

function getInitialTab(): ForexTab {
  return parseForexTab(route.query.tab)
}

const activeTab = ref<ForexTab>(getInitialTab())

/** Whether the player has bank accounts and should use the bank-account-native swap form. */
const hasBankAccounts = computed(() => myBankAccounts.value.length > 0)

// City-based FX rate board

/** The city currently selected in the navbar. */
const selectedCity = computed<City | null>(() => {
  if (!selectedCityId.value) return null
  return cities.value.find((c) => c.id === selectedCityId.value) ?? null
})

/** ISO 4217 code of the selected city's currency. Defaults to EUR. */
const baseCurrencyCode = computed(() => selectedCity.value?.currencyCode ?? 'EUR')

/** Symbol for the base currency (e.g. "€" for EUR). */
const baseCurrencySymbol = computed(() => {
  if (baseCurrencyCode.value === 'EUR') return '€'
  return rates.value.find((r) => r.quoteCurrencyCode === baseCurrencyCode.value)?.quoteCurrencySymbol ?? baseCurrencyCode.value
})

/** Map of currencyCode -> EUR-based rate (units per 1 EUR). EUR itself = 1. */
const eurRatesMap = computed<Record<string, number>>(() => {
  const map: Record<string, number> = { EUR: 1 }
  rates.value.forEach((r) => {
    if (r.baseCurrencyCode === 'EUR') {
      map[r.quoteCurrencyCode] = r.rate
    }
  })
  return map
})

interface CityRateRow {
  targetCode: string
  targetSymbol: string
  /** How many units of targetCode equal 1 unit of baseCurrencyCode. */
  rate: number
  /** After-fee rate: rate × (1 - 0.01). */
  afterFeeRate: number
  rateDate: string
}

/**
 * Cross-rate board relative to the selected city currency.
 * "1 {baseCurrencyCode} = rate {targetCode}"
 * Formula: crossRate = eurRates[target] / eurRates[base]
 */
const cityRateBoard = computed<CityRateRow[]>(() => {
  const base = baseCurrencyCode.value
  const baseEurRate = eurRatesMap.value[base] ?? 1
  const allCodes = new Set<string>(['EUR'])
  rates.value.forEach((r) => allCodes.add(r.quoteCurrencyCode))

  return Array.from(allCodes)
    .filter((code) => code !== base)
    .map((code) => {
      const targetEurRate = eurRatesMap.value[code] ?? 1
      const crossRate = targetEurRate / baseEurRate
      const symbol = code === 'EUR' ? '€' : (rates.value.find((r) => r.quoteCurrencyCode === code)?.quoteCurrencySymbol ?? code)
      const rateEntry = rates.value.find((r) => r.quoteCurrencyCode === code)
      return {
        targetCode: code,
        targetSymbol: symbol,
        rate: crossRate,
        afterFeeRate: crossRate * 0.99,
        rateDate: rateEntry?.rateDate ?? '',
      }
    })
    .sort((a, b) => a.targetCode.localeCompare(b.targetCode))
})

/** The most recent date from the loaded rate entries. */
const rateUpdateDate = computed<string>(() => {
  const dates = rates.value.map((r) => r.rateDate).filter(Boolean)
  if (dates.length === 0) return ''
  return dates.reduce((latest, d) => (d > latest ? d : latest), dates[0]!)
})

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

/** Helper - find a bank account in myBankAccounts by ID. */
function findAccountById(id: string): PlayerBankAccountSummary | undefined {
  return myBankAccounts.value.find((a) => a.id === id)
}

/** Resolved source currency code - from bank account when available, otherwise manual picker. */
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
    const [ratesResult, citiesResult] = await Promise.all([
      gqlRequest<{ fxRates: FxRate[] }>(`
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
      `),
      gqlRequest<{ cities: City[] }>(`{ cities { id name countryCode currencyCode latitude longitude population } }`),
    ])
    rates.value = ratesResult.fxRates ?? []
    if (citiesResult.cities && citiesResult.cities.length > 0) {
      cities.value = citiesResult.cities
    }

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
              ownerType
              ownerDisplayName
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

/**
 * Silently refresh the player's bank account list (no global loading flag) so
 * the Transfer panel can show updated balances after a successful transfer
 * without unmounting the panel and losing its success state.
 */
async function reloadBankAccountsSilent() {
  if (!auth.isAuthenticated) return
  try {
    const result = await gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(`
      query {
        myBankAccounts {
          id
          accountNumber
          currencyCode
          currencySymbol
          balance
          companyId
          companyName
          ownerType
          ownerDisplayName
        }
      }
    `)
    myBankAccounts.value = result.myBankAccounts ?? []
  } catch {
    // Best-effort refresh; on failure the panel keeps stale balances until the
    // next full reload.
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

/**
 * Format an FX cross rate with adaptive precision:
 * - Very small rates (< 0.01): up to 6 decimal places
 * - All other rates: 2-4 decimal places
 */
function formatRate(val: number): string {
  if (val < 0.01) return val.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 6 })
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
  <main class="container min-h-[calc(100vh-64px)] pb-16 pt-6 lg:pb-20 lg:pt-8">
    <div class="flex flex-col gap-10 lg:gap-12">
      <!-- Hero -->
      <div class="forex-hero rounded-2xl border border-divider bg-card px-6 py-6 shadow-sm sm:px-8 sm:py-7">
        <h1 class="text-3xl font-bold text-body">{{ t('forex.title') }}</h1>
        <p class="text-base text-muted">{{ t('forex.subtitle') }}</p>
      </div>

      <div v-if="loading" class="text-center py-12 text-muted">
        <span>{{ t('common.loading') }}</span>
      </div>

      <div v-else-if="error" class="flex flex-col items-center gap-4 py-12 text-center text-muted">
        <p class="text-bad">{{ error }}</p>
        <button class="btn btn-secondary" @click="loadData">{{ t('common.retry') }}</button>
      </div>

      <template v-else>
        <div class="flex flex-col gap-8">
          <!-- Tabs -->
          <div class="flex flex-wrap gap-4" role="tablist" :aria-label="t('forex.tabsLabel')">
            <button
              role="tab"
              :aria-selected="activeTab === 'swap'"
              class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
              :class="activeTab === 'swap' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
              @click="activeTab = 'swap'"
            >
              {{ t('forex.tabSwap') }}
            </button>
            <button
              role="tab"
              :aria-selected="activeTab === 'transfer'"
              class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
              :class="activeTab === 'transfer' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
              @click="activeTab = 'transfer'"
            >
              {{ t('bankTransfer.tabLabel') }}
            </button>
            <button
              role="tab"
              :aria-selected="activeTab === 'rates'"
              class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
              :class="activeTab === 'rates' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
              @click="activeTab = 'rates'"
            >
              {{ t('forex.tabRateList') }}
            </button>
            <button
              role="tab"
              :aria-selected="activeTab === 'history'"
              class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
              :class="activeTab === 'history' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
              @click="activeTab = 'history'"
            >
              {{ t('forex.tabHistory') }}
            </button>
            <button
              role="tab"
              :aria-selected="activeTab === 'gold'"
              class="border rounded-full px-4 py-2 text-sm font-semibold cursor-pointer transition-colors"
              :class="activeTab === 'gold' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
              @click="activeTab = 'gold'"
            >
              {{ t('forex.tabGold') }}
            </button>
          </div>

          <!-- Swap Tab -->
          <section v-if="activeTab === 'swap'" class="space-y-6 rounded-2xl border border-divider bg-card p-6 shadow-sm sm:p-8" aria-label="Forex Swap">
            <div class="flex flex-wrap items-start justify-between gap-3 border-b border-divider pb-3">
              <h2 class="text-lg font-semibold text-body">
                {{ t('forex.tabSwap') }}
              </h2>
              <!-- City/currency context badge -->
              <div v-if="selectedCity" class="swap-city-badge flex items-center gap-1.5 rounded-lg border border-divider bg-card-raised px-3 py-1.5 text-xs text-muted">
                <span class="font-bold text-brand">{{ baseCurrencySymbol }}</span>
                <span class="font-semibold text-body">{{ baseCurrencyCode }}</span>
                <span class="text-subtle">- {{ selectedCity.name }}</span>
              </div>
            </div>

            <!-- Bank account mode notice -->
            <div v-if="hasBankAccounts" class="ba-notice flex items-center gap-2 rounded-lg border border-divider bg-card-raised px-4 py-2.5 text-sm text-muted" role="note">
              <span class="text-lg">🏦</span>
              <span>{{ t('forex.bankAccountMode') }}</span>
              <RouterLink v-if="auth.player?.companies?.length" :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`" class="ml-1 text-xs font-semibold text-brand hover:underline">
                {{ t('forex.viewBankStatement') }} ->
              </RouterLink>
            </div>

            <!-- Balances summary (non-bank-account mode) -->
            <div v-if="!hasBankAccounts" class="flex flex-col gap-3">
              <h3 class="text-sm font-semibold text-muted">{{ t('forex.balancesTitle') }}</h3>
              <RouterLink
                v-if="auth.player?.companies?.length"
                :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`"
                class="statement-link inline-block text-xs font-semibold text-brand hover:underline"
              >
                {{ t('forex.viewBankStatement') }} ->
              </RouterLink>
              <div v-if="balances.length === 0" class="text-sm italic text-muted">
                {{ t('forex.balancesEmpty') }}
              </div>
              <div v-else class="flex flex-wrap gap-3">
                <div v-for="b in balances" :key="b.currencyCode" class="balance-card flex items-center gap-1.5 rounded-lg border border-divider bg-card-raised px-3 py-2">
                  <span class="font-semibold text-brand">{{ b.currencySymbol }}</span>
                  <span class="text-sm font-semibold text-muted">{{ b.currencyCode }}</span>
                  <span class="text-sm font-bold text-body">{{ formatAmount(b.balance) }}</span>
                </div>
              </div>
            </div>

            <!-- Swap success banner -->
            <div v-if="swapResult" class="swap-result-banner flex flex-col gap-1.5 rounded-lg border border-good bg-good/10 px-4 py-3 font-semibold text-good" role="status">
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
              <div class="flex gap-3 text-xs font-normal text-muted">
                <span class="rounded border border-divider bg-card-raised px-2 py-0.5"> {{ swapResult.fromCurrencyCode }}: {{ formatAmount(swapResult.newFromBalance) }} </span>
                <span class="rounded border border-divider bg-card-raised px-2 py-0.5"> {{ swapResult.toCurrencyCode }}: {{ formatAmount(swapResult.newToBalance) }} </span>
              </div>
            </div>

            <!-- Swap form -->
            <div class="flex flex-col gap-4">
              <!-- From row -->
              <div class="grid grid-cols-[1fr_2fr] gap-4 items-start">
                <div class="flex flex-col gap-1">
                  <template v-if="hasBankAccounts">
                    <ForexBankAccountSelector
                      v-model="fromBankAccountId"
                      :accounts="myBankAccounts"
                      :label="t('forex.sourceAccount')"
                      id="from-bank-account"
                      @update:model-value="
                        () => {
                          quote = null
                          showConfirm = false
                        }
                      "
                    />
                  </template>
                  <template v-else>
                    <BankAccountSelector
                      v-model="fromCurrency"
                      :balances="toBalances"
                      :label="t('forex.sourceCurrency')"
                      id="from-currency"
                      @update:model-value="
                        () => {
                          quote = null
                          showConfirm = false
                        }
                      "
                    />
                  </template>
                </div>

                <!-- Amount input -->
                <div class="flex flex-col gap-1">
                  <label class="text-xs font-semibold text-muted uppercase tracking-wide" for="swap-amount">
                    {{ t('forex.amount') }}
                  </label>
                  <div class="flex items-center border border-divider rounded-lg overflow-hidden bg-page">
                    <span class="px-3 py-2.5 bg-card-raised border-r border-divider font-bold text-brand min-w-10 text-center text-sm">
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
                    />
                  </div>
                  <span class="field-hint text-xs text-muted mt-0.5"> {{ t('forex.availableBalance') }}: {{ fromSymbol }}{{ formatAmount(fromBalance) }} </span>
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
              <div class="grid grid-cols-[1fr_2fr] gap-4 items-start">
                <div class="flex flex-col gap-1">
                  <template v-if="hasBankAccounts">
                    <ForexBankAccountSelector
                      v-model="toBankAccountId"
                      :accounts="myBankAccounts"
                      :label="t('forex.destAccount')"
                      id="to-bank-account"
                      @update:model-value="
                        () => {
                          quote = null
                          showConfirm = false
                        }
                      "
                    />
                  </template>
                  <template v-else>
                    <BankAccountSelector
                      v-model="toCurrency"
                      :balances="toBalances"
                      :label="t('forex.targetCurrency')"
                      id="to-currency"
                      @update:model-value="
                        () => {
                          quote = null
                          showConfirm = false
                        }
                      "
                    />
                  </template>
                </div>

                <!-- You receive -->
                <div class="flex flex-col gap-1">
                  <label class="text-xs font-semibold text-muted uppercase tracking-wide">
                    {{ t('forex.youReceive') }}
                  </label>
                  <div class="flex items-center border border-divider rounded-lg overflow-hidden bg-page">
                    <span class="px-3 py-2.5 bg-card-raised border-r border-divider font-bold text-brand min-w-10 text-center text-sm">
                      {{ toSymbol }}
                    </span>
                    <div class="flex-1 px-3 py-2.5 text-base font-bold text-good">
                      <span v-if="quote">{{ formatAmount(quote.toAmount) }}</span>
                      <span v-else class="text-muted font-normal">-</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Validation / quote errors -->
              <div v-if="validationError && amount" class="validation-error text-sm text-bad px-3 py-2 bg-bad/10 rounded-md" role="alert">
                {{ validationError }}
              </div>
              <div v-if="quoteError" class="text-sm text-bad px-3 py-2 bg-bad/10 rounded-md" role="alert">
                {{ quoteError }}
              </div>

              <!-- Get quote action -->
              <div v-if="!showConfirm">
                <button class="btn btn-primary" :disabled="quoteLoading || !!validationError || !amount" @click="fetchQuote">
                  {{ quoteLoading ? t('common.loading') : t('forex.getQuote') }}
                </button>
              </div>
            </div>

            <!-- Quote confirmation card -->
            <div v-if="showConfirm && quote" class="space-y-4 rounded-2xl border border-brand bg-card-raised p-6 sm:p-7" role="region" aria-label="Exchange Quote">
              <h3 class="text-base font-bold text-body">{{ t('forex.quoteTitle') }}</h3>
              <table class="quote-table w-full border-collapse text-sm">
                <tbody>
                  <tr>
                    <td class="py-1.5 text-muted w-2/5">{{ t('forex.rate') }}</td>
                    <td class="py-1.5 font-semibold text-body text-right">1 {{ quote.fromCurrencyCode }} = {{ formatAmount(quote.rate) }} {{ quote.toCurrencyCode }}</td>
                  </tr>
                  <tr>
                    <td class="py-1.5 text-muted">{{ t('forex.fee') }}</td>
                    <td class="py-1.5 font-semibold text-caution text-right">{{ quote.fromCurrencySymbol }}{{ formatAmount(quote.feeAmount) }}</td>
                  </tr>
                  <tr>
                    <td class="py-1.5 text-muted">{{ t('forex.youReceive') }}</td>
                    <td class="py-1.5 text-right text-base font-bold text-good">{{ quote.toCurrencySymbol }}{{ formatAmount(quote.toAmount) }}</td>
                  </tr>
                </tbody>
              </table>

              <div v-if="swapError" class="rounded-md bg-bad/10 px-3 py-2 text-sm text-bad" role="alert">
                {{ swapError }}
              </div>

              <div class="flex justify-end gap-3">
                <button class="btn btn-secondary" :disabled="swapLoading" @click="cancelQuote">
                  {{ t('forex.cancel') }}
                </button>
                <button class="btn btn-primary" :disabled="swapLoading" @click="executeSwap">
                  {{ swapLoading ? t('common.loading') : t('forex.confirmSwap') }}
                </button>
              </div>
            </div>
          </section>

          <!-- Transfer Tab -->
          <BankAccountTransferPanel v-else-if="activeTab === 'transfer'" :accounts="myBankAccounts" @transferred="reloadBankAccountsSilent" />

          <!-- Rates Tab -->
          <section v-else-if="activeTab === 'rates'" class="space-y-6 rounded-2xl border border-divider bg-card p-6 shadow-sm sm:p-8" aria-label="Rate List">
            <h2 class="border-b border-divider pb-3 text-lg font-semibold text-body">
              {{ t('forex.rateListTitle') }}
            </h2>

            <div v-if="rates.length === 0" class="text-sm italic text-muted">
              {{ t('forex.rateListEmpty') }}
            </div>

            <template v-else>
              <!-- City / base currency context banner -->
              <div class="city-rate-context flex flex-wrap items-center gap-3 rounded-xl border border-brand/30 bg-brand/5 px-4 py-3">
                <div class="flex items-center gap-2">
                  <span class="text-2xl font-bold text-brand">{{ baseCurrencySymbol }}</span>
                  <div class="flex flex-col">
                    <span class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('forex.rateBaseCurrency') }}</span>
                    <span class="font-bold text-body">
                      {{ baseCurrencyCode }}
                      <span v-if="selectedCity" class="ml-1 font-normal text-muted">({{ selectedCity.name }})</span>
                    </span>
                  </div>
                </div>
                <div class="ml-auto text-right text-xs text-muted">
                  <div v-if="rateUpdateDate">{{ t('forex.rateUpdated') }}: {{ rateUpdateDate }}</div>
                  <div class="text-subtle">{{ t('forex.rateSourceNote') }}</div>
                </div>
              </div>

              <!-- Cross-rate table: 1 base -> X target -->
              <div>
                <p class="mb-3 text-sm text-muted">
                  {{ t('forex.rateTableIntro', { base: `1 ${baseCurrencySymbol} ${baseCurrencyCode}` }) }}
                </p>
                <div class="overflow-x-auto">
                  <table class="rates-table w-full border-collapse text-sm">
                    <thead>
                      <tr>
                        <th class="text-left px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                          {{ t('forex.rateTableCurrency') }}
                        </th>
                        <th class="text-right px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                          {{ t('forex.rateTableMidRate') }}
                        </th>
                        <th class="text-right px-3 py-2 text-xs font-semibold text-muted uppercase tracking-wide border-b border-divider">
                          {{ t('forex.rateTableAfterFee') }}
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="row in cityRateBoard" :key="row.targetCode" class="history-row border-b border-divider/40 last:border-0">
                        <td class="px-3 py-3 align-middle">
                          <div class="flex items-center gap-2">
                            <span class="min-w-[2rem] text-base font-bold text-brand">{{ row.targetSymbol }}</span>
                            <div>
                              <span class="font-semibold text-body">{{ row.targetCode }}</span>
                            </div>
                          </div>
                        </td>
                        <td class="px-3 py-3 text-right font-mono font-semibold text-body align-middle">
                          {{ formatRate(row.rate) }}
                        </td>
                        <td class="px-3 py-3 text-right font-mono text-muted align-middle">
                          <span class="text-sm">{{ formatRate(row.afterFeeRate) }}</span>
                          <span class="ml-1 text-xs text-subtle">-1%</span>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <p class="mt-3 text-xs text-subtle">{{ t('forex.rateAfterFeeNote') }}</p>
              </div>
            </template>
          </section>

          <!-- History Tab -->
          <section v-else-if="activeTab === 'history'" class="space-y-4 rounded-2xl border border-divider bg-card p-6 shadow-sm sm:p-8" aria-label="Trade History">
            <h2 class="border-b border-divider pb-3 text-lg font-semibold text-body">
              {{ t('forex.historyTitle') }}
            </h2>
            <div v-if="history.length === 0" class="text-sm italic text-muted">
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
                      <span class="mr-0.5 font-bold text-brand">{{ entry.fromCurrencySymbol }}</span>
                      {{ formatAmount(entry.fromAmount) }}
                      <span class="ml-1 text-xs text-muted">{{ entry.fromCurrencyCode }}</span>
                    </td>
                    <td class="px-3 py-2.5 align-middle">
                      <span class="mr-0.5 font-bold text-brand">{{ entry.toCurrencySymbol }}</span>
                      {{ formatAmount(entry.toAmount) }}
                      <span class="ml-1 text-xs text-muted">{{ entry.toCurrencyCode }}</span>
                    </td>
                    <td class="px-3 py-2.5 text-muted align-middle">{{ formatAmount(entry.rate) }}</td>
                    <td class="px-3 py-2.5 text-caution align-middle">{{ entry.fromCurrencySymbol }}{{ formatAmount(entry.feeAmount) }}</td>
                    <td class="px-3 py-2.5 text-subtle text-xs align-middle">
                      {{ formatTick(entry.executedAtTick) }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <GoldAmmSection v-if="activeTab === 'gold'" :available-currencies="availableCurrencies" :balances="balances" @refresh="loadData" />
        </div>
      </template>
    </div>
  </main>
</template>

<style scoped>
/* Table row hover - cannot target child <td> elements with Tailwind parent-hover */
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
