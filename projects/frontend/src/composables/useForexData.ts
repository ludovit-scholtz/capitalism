import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import type { City, FxRate, ForexTradeHistoryEntry, CurrencyBalance, PlayerBankAccountSummary } from '@/types'

interface CityRateRow {
  targetCode: string
  targetSymbol: string
  rate: number
  afterFeeRate: number
  rateDate: string
}

export function useForexData() {
  const auth = useAuthStore()
  const { selectedCityId } = storeToRefs(auth)

  const loading = ref(true)
  const error = ref<string | null>(null)
  const rates = ref<FxRate[]>([])
  const balances = ref<CurrencyBalance[]>([])
  const history = ref<ForexTradeHistoryEntry[]>([])
  const myBankAccounts = ref<PlayerBankAccountSummary[]>([])
  const cities = ref<City[]>([])

  const isCompanyContext = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!auth.player?.activeCompanyId)
  const contextScopedBankAccounts = computed(() => {
    if (isCompanyContext.value) {
      const activeCompanyId = auth.player?.activeCompanyId
      return myBankAccounts.value.filter((account) => account.ownerType === 'COMPANY' && account.companyId === activeCompanyId)
    }
    return myBankAccounts.value.filter((account) => account.ownerType === 'PERSON')
  })
  const hasBankAccounts = computed(() => isCompanyContext.value || contextScopedBankAccounts.value.length > 0)

  const selectedCity = computed<City | null>(() => {
    if (!selectedCityId.value) return null
    return cities.value.find((c) => c.id === selectedCityId.value) ?? null
  })
  const baseCurrencyCode = computed(() => selectedCity.value?.currencyCode ?? 'EUR')
  const baseCurrencySymbol = computed(() => {
    if (baseCurrencyCode.value === 'EUR') return '€'
    return rates.value.find((r) => r.quoteCurrencyCode === baseCurrencyCode.value)?.quoteCurrencySymbol ?? baseCurrencyCode.value
  })

  const eurRatesMap = computed<Record<string, number>>(() => {
    const map: Record<string, number> = { EUR: 1 }
    rates.value.forEach((r) => { if (r.baseCurrencyCode === 'EUR') map[r.quoteCurrencyCode] = r.rate })
    return map
  })

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
        return { targetCode: code, targetSymbol: symbol, rate: crossRate, afterFeeRate: crossRate * 0.99, rateDate: rateEntry?.rateDate ?? '' }
      })
      .sort((a, b) => a.targetCode.localeCompare(b.targetCode))
  })

  const rateUpdateDate = computed<string>(() => {
    const dates = rates.value.map((r) => r.rateDate).filter(Boolean)
    if (dates.length === 0) return ''
    return dates.reduce((latest, d) => (d > latest ? d : latest), dates[0]!)
  })

  const availableCurrencies = computed(() => {
    const codes = new Set<string>(['EUR'])
    rates.value.forEach((r) => codes.add(r.quoteCurrencyCode))
    return Array.from(codes).sort()
  })

  const toBalances = computed<CurrencyBalance[]>(() => {
    return availableCurrencies.value.map((code) => {
      const existing = balances.value.find((b) => b.currencyCode === code)
      if (existing) return existing
      const rate = rates.value.find((r) => r.quoteCurrencyCode === code)
      const symbol = rate?.quoteCurrencySymbol ?? code
      return { currencyCode: code, currencySymbol: symbol, balance: 0 }
    })
  })

  async function loadData() {
    loading.value = true
    error.value = null
    try {
      const [ratesResult, citiesResult] = await Promise.all([
        gqlRequest<{ fxRates: FxRate[] }>(`query { fxRates { baseCurrencyCode quoteCurrencyCode rate rateDate source quoteCurrencySymbol } }`),
        gqlRequest<{ cities: City[] }>(`{ cities { id name countryCode currencyCode latitude longitude population } }`),
      ])
      rates.value = ratesResult.fxRates ?? []
      if (citiesResult.cities && citiesResult.cities.length > 0) cities.value = citiesResult.cities

      if (auth.isAuthenticated) {
        const [balancesResult, historyResult, bankAccountsResult] = await Promise.all([
          gqlRequest<{ playerCurrencyBalances: CurrencyBalance[] }>(`query { playerCurrencyBalances { currencyCode currencySymbol balance } }`),
          gqlRequest<{ forexTradeHistory: ForexTradeHistoryEntry[] }>(`
            query {
              forexTradeHistory {
                id fromCurrencyCode toCurrencyCode fromAmount toAmount feeAmount rate
                executedAtTick executedAtUtc fromCurrencySymbol toCurrencySymbol
              }
            }
          `),
          gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(`
            query {
              myBankAccounts {
                id accountNumber currencyCode currencySymbol balance
                companyId companyName ownerType ownerDisplayName
              }
            }
          `),
        ])
        balances.value = balancesResult.playerCurrencyBalances ?? []
        history.value = historyResult.forexTradeHistory ?? []
        myBankAccounts.value = bankAccountsResult.myBankAccounts ?? []
      }
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : String(e)
    } finally {
      loading.value = false
    }
  }

  async function reloadBankAccountsSilent() {
    if (!auth.isAuthenticated) return
    try {
      const result = await gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(`
        query { myBankAccounts { id accountNumber currencyCode currencySymbol balance companyId companyName ownerType ownerDisplayName } }
      `)
      myBankAccounts.value = result.myBankAccounts ?? []
    } catch { /* best-effort */ }
  }

  /** Reload user-specific data (balances, history, bank accounts) silently without triggering the loading spinner. */
  async function reloadAfterSwap() {
    if (!auth.isAuthenticated) return
    try {
      const [balancesResult, historyResult, bankAccountsResult] = await Promise.all([
        gqlRequest<{ playerCurrencyBalances: CurrencyBalance[] }>(`query { playerCurrencyBalances { currencyCode currencySymbol balance } }`),
        gqlRequest<{ forexTradeHistory: ForexTradeHistoryEntry[] }>(`
          query {
            forexTradeHistory {
              id fromCurrencyCode toCurrencyCode fromAmount toAmount feeAmount rate
              executedAtTick executedAtUtc fromCurrencySymbol toCurrencySymbol
            }
          }
        `),
        gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(`
          query {
            myBankAccounts {
              id accountNumber currencyCode currencySymbol balance
              companyId companyName ownerType ownerDisplayName
            }
          }
        `),
      ])
      balances.value = balancesResult.playerCurrencyBalances ?? []
      history.value = historyResult.forexTradeHistory ?? []
      myBankAccounts.value = bankAccountsResult.myBankAccounts ?? []
    } catch { /* best-effort */ }
  }

  function formatAmount(val: number): string {
    return val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })
  }

  function formatRate(val: number): string {
    if (val < 0.01) return val.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 6 })
    return val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })
  }

  function formatTick(tick: number): string { return tick.toLocaleString() }

  return { auth, loading, error, rates, balances, history, myBankAccounts, cities, contextScopedBankAccounts, hasBankAccounts, selectedCity, baseCurrencyCode, baseCurrencySymbol, cityRateBoard, rateUpdateDate, availableCurrencies, toBalances, loadData, reloadBankAccountsSilent, reloadAfterSwap, formatAmount, formatRate, formatTick }
}
