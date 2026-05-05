import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useGameStateStore } from '@/stores/gameState'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { getActiveAccountOption } from '@/lib/accountContext'
import { deepEqual } from '@/lib/utils'
import type { CompanyOwnership, MergeCompanyResult, PlayerBankAccountSummary, PersonAccount, ShareTradeResult, StockExchangeListing, StockExchangePriceHistoryPoint } from '@/types'
import { PERSON_ACCOUNT_QUERY, LISTINGS_QUERY, MY_BANK_ACCOUNTS_QUERY, BUY_MUTATION, SELL_MUTATION, PRICE_HISTORY_QUERY, MERGE_MUTATION, COMPANY_SHAREHOLDERS_QUERY } from '@/components/stock/stockExchangeQueries'

type ControlledCompanyAccount = { id: string; name: string; cash: number | null }
type SortField = 'name' | 'price' | 'marketValue' | 'ownership' | 'dividend'
type SortDir = 'asc' | 'desc'

export function useStockExchange() {
  const { t, locale } = useI18n()
  const auth = useAuthStore()
  const gameStateStore = useGameStateStore()
  const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()
  auth.initFromStorage()

  const currentTick = computed(() => gameStateStore.gameState?.currentTick ?? null)
  const loading = ref(true)
  const error = ref<string | null>(null)
  const actionLoadingKey = ref<string | null>(null)
  const personAccount = ref<PersonAccount | null>(null)
  const listings = ref<StockExchangeListing[]>([])
  const myBankAccounts = ref<PlayerBankAccountSummary[]>([])
  const selectedSettlementBankAccountId = ref<string>('')
  const quantityByCompany = ref<Record<string, number>>({})
  const errorByCompany = ref<Record<string, string | null>>({})
  const successByCompany = ref<Record<string, string | null>>({})
  const expandedCompany = ref<string | null>(null)
  const priceHistoryByCompany = ref<Record<string, StockExchangePriceHistoryPoint[]>>({})
  const priceHistoryLoadingByCompany = ref<Record<string, boolean>>({})
  const priceHistoryErrorByCompany = ref<Record<string, string | null>>({})
  const shareholdersByCompany = ref<Record<string, CompanyOwnership>>({})
  const shareholdersLoadingByCompany = ref<Record<string, boolean>>({})
  const shareholdersErrorByCompany = ref<Record<string, string | null>>({})
  const mergeDialogCompanyId = ref<string | null>(null)
  const mergeDialogOpen = computed({
    get: () => mergeDialogCompanyId.value !== null,
    set: (val: boolean) => { if (!val) closeMergeDialog() },
  })
  const mergeDestinationCompanyId = ref<string>('')
  const mergeLoading = ref(false)
  const mergeError = ref<string | null>(null)
  const mergeSuccess = ref<MergeCompanyResult | null>(null)
  const filterText = ref('')
  const sortField = ref<SortField>('marketValue')
  const sortDir = ref<SortDir>('desc')
  const currentPage = ref(1)
  const pageSize = 10

  const controlledCompanies = computed<ControlledCompanyAccount[]>(() => {
    const directCompanies = (auth.player?.companies ?? []).map((company) => ({ id: company.id, name: company.name, cash: company.cash }))
    const directCompanyIds = new Set(directCompanies.map((company) => company.id))
    const activeCompanyId = personAccount.value?.activeCompanyId ?? null
    const derivedCompanies = listings.value
      .filter((listing) => listing.canClaimControl)
      .filter((listing) => !directCompanyIds.has(listing.companyId))
      .filter((listing) => activeCompanyId === listing.companyId)
      .map((listing) => ({ id: listing.companyId, name: listing.companyName, cash: null }))
    return [...directCompanies, ...derivedCompanies]
  })

  const portfolioValue = computed(() => personAccount.value?.shareholdings.reduce((total, holding) => total + holding.marketValue, 0) ?? 0)
  const recentDividendTotal = computed(() => personAccount.value?.dividendPayments.slice(0, 5).reduce((total, payment) => total + payment.totalAmount, 0) ?? 0)
  const activeTradeAccount = computed(() => getActiveAccountOption(auth.player, auth.player?.companies ?? []))
  const activeTradeAccountName = computed(() => activeTradeAccount.value?.name ?? personAccount.value?.displayName ?? t('stockExchange.personAccount'))
  const activeTradeAccountType = computed(() => activeTradeAccount.value?.accountType ?? 'PERSON')
  const activeTradeAccountCash = computed(() => {
    if (activeTradeAccount.value?.accountType === 'COMPANY') return activeTradeAccount.value.cash
    return personAccount.value?.availableCash ?? null
  })

  const activeSettlementAccounts = computed(() => {
    if (activeTradeAccountType.value === 'COMPANY') {
      const activeCompanyId = activeTradeAccount.value?.companyId ?? auth.player?.activeCompanyId ?? null
      return myBankAccounts.value.filter((account) => account.ownerType === 'COMPANY' && account.companyId === activeCompanyId && account.currencyCode === 'USD')
    }
    return myBankAccounts.value.filter((account) => account.ownerType === 'PERSON' && account.currencyCode === 'USD')
  })

  const filteredAndSortedListings = computed(() => {
    const text = filterText.value.trim().toLowerCase()
    const filtered = text ? listings.value.filter((listing) => listing.companyName.toLowerCase().includes(text)) : listings.value
    return [...filtered].sort((a, b) => {
      let cmp = 0
      if (sortField.value === 'name') cmp = a.companyName.localeCompare(b.companyName)
      else if (sortField.value === 'price') cmp = a.sharePrice - b.sharePrice
      else if (sortField.value === 'marketValue') cmp = a.marketValue - b.marketValue
      else if (sortField.value === 'ownership') cmp = a.combinedControlledOwnershipRatio - b.combinedControlledOwnershipRatio
      else if (sortField.value === 'dividend') cmp = a.dividendPayoutRatio - b.dividendPayoutRatio
      return sortDir.value === 'asc' ? cmp : -cmp
    })
  })

  const totalPages = computed(() => Math.max(1, Math.ceil(filteredAndSortedListings.value.length / pageSize)))
  const paginatedListings = computed(() => {
    const start = (currentPage.value - 1) * pageSize
    return filteredAndSortedListings.value.slice(start, start + pageSize)
  })

  watch([filterText, sortField, sortDir], () => { currentPage.value = 1 })
  watch(totalPages, (value) => { if (currentPage.value > value) currentPage.value = value })
  watch(activeSettlementAccounts, (accounts) => {
    if (!accounts.some((account) => account.id === selectedSettlementBankAccountId.value))
      selectedSettlementBankAccountId.value = accounts[0]?.id ?? ''
  })

  function toggleSort(field: SortField) {
    if (sortField.value === field) sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
    else { sortField.value = field; sortDir.value = 'desc' }
  }

  function sortIcon(field: SortField): string {
    if (sortField.value !== field) return '\u2195'
    return sortDir.value === 'asc' ? '\u2191' : '\u2193'
  }

  function setDefaultQuantities() {
    for (const listing of listings.value) { quantityByCompany.value[listing.companyId] ??= 100 }
  }

  function isControlledCompany(companyId: string): boolean {
    return controlledCompanies.value.some((company) => company.id === companyId)
  }

  function getQuantity(companyId: string): number {
    const value = Number(quantityByCompany.value[companyId] ?? 100)
    if (!Number.isFinite(value)) return 0
    return Math.max(Math.floor(value), 0)
  }

  function updateQuantity(companyId: string, value: number) {
    quantityByCompany.value[companyId] = Math.max(Math.floor(Number.isFinite(value) ? value : 0), 1)
  }

  function estimatedBuyCost(listing: StockExchangeListing): number { return getQuantity(listing.companyId) * listing.askPrice }
  function estimatedSellProceeds(listing: StockExchangeListing): number { return getQuantity(listing.companyId) * listing.bidPrice }

  function resolveTradeAccount(): { tradeAccountType: string; tradeAccountCompanyId: string | null } {
    if (activeTradeAccountType.value === 'COMPANY')
      return { tradeAccountType: 'COMPANY', tradeAccountCompanyId: activeTradeAccount.value?.companyId ?? auth.player?.activeCompanyId ?? null }
    return { tradeAccountType: 'PERSON', tradeAccountCompanyId: null }
  }

  async function loadPriceHistory(companyId: string) {
    priceHistoryLoadingByCompany.value[companyId] = true
    priceHistoryErrorByCompany.value[companyId] = null
    try {
      const data = await gqlRequest<{ stockExchangePriceHistory: StockExchangePriceHistoryPoint[] }>(PRICE_HISTORY_QUERY, { companyId })
      priceHistoryByCompany.value[companyId] = data.stockExchangePriceHistory
    } catch (reason: unknown) {
      priceHistoryErrorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.historyLoadFailed')
    } finally { priceHistoryLoadingByCompany.value[companyId] = false }
  }

  async function loadShareholders(companyId: string) {
    shareholdersLoadingByCompany.value[companyId] = true
    shareholdersErrorByCompany.value[companyId] = null
    try {
      const data = await gqlRequest<{ companyShareholders: CompanyOwnership | null }>(COMPANY_SHAREHOLDERS_QUERY, { companyId })
      if (data.companyShareholders) shareholdersByCompany.value[companyId] = data.companyShareholders
    } catch (reason: unknown) {
      shareholdersErrorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.shareholdersLoadFailed')
    } finally { shareholdersLoadingByCompany.value[companyId] = false }
  }

  async function toggleTradePanel(companyId: string) {
    expandedCompany.value = expandedCompany.value === companyId ? null : companyId
    errorByCompany.value[companyId] = null
    successByCompany.value[companyId] = null
    if (expandedCompany.value === companyId) {
      const loadTasks: Promise<void>[] = []
      if (!priceHistoryByCompany.value[companyId]) loadTasks.push(loadPriceHistory(companyId))
      if (!shareholdersByCompany.value[companyId]) loadTasks.push(loadShareholders(companyId))
      await Promise.all(loadTasks)
    }
  }

  async function loadData(isRefresh = false) {
    if (!isRefresh) loading.value = true
    error.value = null
    try {
      if (auth.isAuthenticated && !auth.player) await auth.fetchMe()
      const listingDataPromise = gqlRequest<{ stockExchangeListings: StockExchangeListing[] }>(LISTINGS_QUERY)
      const accountDataPromise = auth.isAuthenticated
        ? gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY)
        : Promise.resolve({ myBankAccounts: [] as PlayerBankAccountSummary[] })
      let resolvedPersonAccount: PersonAccount | null = null
      try {
        const personData = await gqlRequest<{ personAccount: PersonAccount | null }>(PERSON_ACCOUNT_QUERY)
        resolvedPersonAccount = personData.personAccount
      } catch { resolvedPersonAccount = null }
      const listingData = await listingDataPromise
      const accountData = await accountDataPromise
      if (!deepEqual(personAccount.value, resolvedPersonAccount)) personAccount.value = resolvedPersonAccount
      if (!deepEqual(listings.value, listingData.stockExchangeListings)) listings.value = listingData.stockExchangeListings
      if (!deepEqual(myBankAccounts.value, accountData.myBankAccounts)) myBankAccounts.value = accountData.myBankAccounts
      const hasSelectedSettlement = activeSettlementAccounts.value.some((account) => account.id === selectedSettlementBankAccountId.value)
      if (!hasSelectedSettlement) selectedSettlementBankAccountId.value = activeSettlementAccounts.value[0]?.id ?? ''
      setDefaultQuantities()
    } catch (reason: unknown) {
      if (!isRefresh) error.value = reason instanceof Error ? reason.message : t('stockExchange.loadFailed')
    } finally { loading.value = false }
  }

  async function switchToCompanyAccount(companyId: string) {
    actionLoadingKey.value = `switch-${companyId}`
    errorByCompany.value[companyId] = null
    successByCompany.value[companyId] = null
    const companyName = auth.player?.companies.find((company) => company.id === companyId)?.name ?? listings.value.find((listing) => listing.companyId === companyId)?.companyName ?? t('stockExchange.companyAccount')
    try {
      await auth.switchAccountContext('COMPANY', companyId)
      await loadData(true)
      successByCompany.value[companyId] = t('stockExchange.switchSuccess', { account: companyName })
      expandedCompany.value = companyId
    } catch (reason: unknown) {
      errorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
    } finally { actionLoadingKey.value = null }
  }

  async function executeTrade(kind: 'buy' | 'sell', companyId: string) {
    const shareCount = getQuantity(companyId)
    if (shareCount <= 0) { errorByCompany.value[companyId] = t('stockExchange.invalidQuantity'); successByCompany.value[companyId] = null; return }
    actionLoadingKey.value = `${kind}-${companyId}`
    errorByCompany.value[companyId] = null
    successByCompany.value[companyId] = null
    const { tradeAccountType, tradeAccountCompanyId } = resolveTradeAccount()
    if (!selectedSettlementBankAccountId.value) { errorByCompany.value[companyId] = t('stockExchange.selectSettlementAccount'); successByCompany.value[companyId] = null; return }
    try {
      let result: ShareTradeResult
      if (kind === 'buy') {
        const data = await gqlRequest<{ buyShares: ShareTradeResult }>(BUY_MUTATION, { input: { companyId, shareCount, tradeAccountType, tradeAccountCompanyId, bankAccountId: selectedSettlementBankAccountId.value } })
        result = data.buyShares
        successByCompany.value[companyId] = t('stockExchange.buySuccess', { company: result.companyName, shares: formatShares(result.shareCount) })
      } else {
        const data = await gqlRequest<{ sellShares: ShareTradeResult }>(SELL_MUTATION, { input: { companyId, shareCount, tradeAccountType, tradeAccountCompanyId, bankAccountId: selectedSettlementBankAccountId.value } })
        result = data.sellShares
        if (result.taxReserved > 0) successByCompany.value[companyId] = t('stockExchange.sellSuccessWithTax', { company: result.companyName, shares: formatShares(result.shareCount), tax: formatCurrency(result.taxReserved) })
        else successByCompany.value[companyId] = t('stockExchange.sellSuccess', { company: result.companyName, shares: formatShares(result.shareCount) })
      }
      await Promise.all([loadData(true), auth.fetchMe()])
    } catch (reason: unknown) {
      errorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
    } finally { actionLoadingKey.value = null }
  }

  function openMergeDialog(companyId: string) {
    mergeDialogCompanyId.value = companyId
    mergeDestinationCompanyId.value = controlledCompanies.value[0]?.id ?? ''
    mergeError.value = null
    mergeSuccess.value = null
  }

  function closeMergeDialog() { mergeDialogCompanyId.value = null; mergeError.value = null; mergeSuccess.value = null }

  async function executeMerge() {
    const targetCompanyId = mergeDialogCompanyId.value
    if (!targetCompanyId || !mergeDestinationCompanyId.value) return
    mergeLoading.value = true; mergeError.value = null; mergeSuccess.value = null
    try {
      const data = await gqlRequest<{ mergeCompany: MergeCompanyResult }>(MERGE_MUTATION, { input: { targetCompanyId, destinationCompanyId: mergeDestinationCompanyId.value } })
      mergeSuccess.value = data.mergeCompany
      await Promise.all([loadData(true), auth.fetchMe()])
    } catch (reason: unknown) {
      mergeError.value = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
    } finally { mergeLoading.value = false }
  }

  function formatCurrency(value: number): string {
    return new Intl.NumberFormat(locale.value, { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(value)
  }

  function formatShares(value: number): string {
    return new Intl.NumberFormat(locale.value, { minimumFractionDigits: Number.isInteger(value) ? 0 : 2, maximumFractionDigits: Number.isInteger(value) ? 0 : 4 }).format(value)
  }

  onMounted(() => { void loadData() })

  useTickRefresh(async () => {
    const scrollPos = saveScrollPosition()
    await loadData(true)
    await restoreScrollPosition(scrollPos)
  })

  return { currentTick, loading, error, actionLoadingKey, personAccount, listings, selectedSettlementBankAccountId, quantityByCompany, errorByCompany, successByCompany, expandedCompany, priceHistoryByCompany, priceHistoryLoadingByCompany, priceHistoryErrorByCompany, shareholdersByCompany, shareholdersLoadingByCompany, shareholdersErrorByCompany, mergeDialogOpen, mergeDestinationCompanyId, mergeLoading, mergeError, mergeSuccess, filterText, sortField, sortDir, currentPage, controlledCompanies, portfolioValue, recentDividendTotal, activeTradeAccountName, activeTradeAccountType, activeTradeAccountCash, activeSettlementAccounts, filteredAndSortedListings, totalPages, paginatedListings, toggleSort, sortIcon, isControlledCompany, updateQuantity, estimatedBuyCost, estimatedSellProceeds, toggleTradePanel, loadData, switchToCompanyAccount, executeTrade, openMergeDialog, closeMergeDialog, executeMerge, locale }
}
