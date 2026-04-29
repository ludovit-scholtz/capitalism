<template src="./ForexExchangeView.template.html"></template>

<script setup lang="ts">
/* oxlint-disable no-unused-vars */
/* eslint-disable @typescript-eslint/no-unused-vars */
// Split-file SFC: script symbols are consumed by ForexExchangeView.template.html.
 
 
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

// ── City-based FX rate board ────────────────────────────────────────────────

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

/** Map of currencyCode → EUR-based rate (units per 1 EUR). EUR itself = 1. */
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
      const symbol =
        code === 'EUR' ? '€' : (rates.value.find((r) => r.quoteCurrencyCode === code)?.quoteCurrencySymbol ?? code)
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

<style scoped src="./ForexExchangeView.styles.css"></style>