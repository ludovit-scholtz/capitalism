import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { GraphQLError, gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useGameStateStore } from '@/stores/gameState'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { getActiveAccountOption } from '@/lib/accountContext'
import { deepEqual } from '@/lib/utils'
import type { CompanyOwnership, DividendProposal, DividendVoteResult, MergeCompanyResult, OpenLimitOrder, PlayerBankAccountSummary, PersonAccount, ReplaceCeoResult, ShareTradeResult, StockExchangeListing, StockExchangePriceHistoryPoint, StockOrderBook, StockTradeExecution } from '@/types'
import {
  PERSON_ACCOUNT_QUERY,
  LISTINGS_QUERY,
  MY_BANK_ACCOUNTS_QUERY,
  BUY_MUTATION,
  SELL_MUTATION,
  PRICE_HISTORY_QUERY,
  MERGE_MUTATION,
  REPLACE_CEO_MUTATION,
  COMPANY_SHAREHOLDERS_QUERY,
  OPEN_ORDERS_QUERY,
  ORDER_BOOK_QUERY,
  STOCK_TRADE_HISTORY_QUERY,
  PLACE_LIMIT_ORDER_MUTATION,
  CANCEL_LIMIT_ORDER_MUTATION,
  DIVIDEND_PROPOSALS_QUERY,
  PROPOSE_DIVIDEND_MUTATION,
  VOTE_DIVIDEND_PROPOSAL_MUTATION,
} from '@/components/stock/stockExchangeQueries'

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
  const openOrders = ref<OpenLimitOrder[]>([])
  const selectedOrderBookSymbol = ref<string>('')
  const orderBook = ref<StockOrderBook>({ stockSymbol: '', bids: [], asks: [] })
  const tradeHistory = ref<StockTradeExecution[]>([])
  const orderSide = ref<'BUY' | 'SELL'>('BUY')
  const orderPrice = ref<number>(0)
  const orderQuantity = ref<number>(100)
  const orderLoading = ref(false)
  const orderError = ref<string | null>(null)
  const orderSuccess = ref<string | null>(null)
  const quantityByCompany = ref<Record<string, number>>({})
  const errorByCompany = ref<Record<string, string | null>>({})
  const successByCompany = ref<Record<string, string | null>>({})
  const expandedCompany = ref<string | null>(null)
  const priceHistoryByCompany = ref<Record<string, StockExchangePriceHistoryPoint[]>>({})
  const priceHistoryLoadingByCompany = ref<Record<string, boolean>>({})
  const priceHistoryErrorByCompany = ref<Record<string, string | null>>({})
  const dividendProposalsByCompany = ref<Record<string, DividendProposal[]>>({})
  const dividendProposalsLoadingByCompany = ref<Record<string, boolean>>({})
  const dividendProposalsErrorByCompany = ref<Record<string, string | null>>({})
  const dividendPerShareDraftByCompany = ref<Record<string, number>>({})
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
  const takeoverDialogCompanyId = ref<string | null>(null)
  const takeoverDialogOpen = computed({
    get: () => takeoverDialogCompanyId.value !== null,
    set: (val: boolean) => { if (!val) closeTakeoverDialog() },
  })
  const takeoverLoading = ref(false)
  const takeoverError = ref<string | null>(null)
  const takeoverSuccess = ref<ReplaceCeoResult | null>(null)
  const filterText = ref('')
  const selectedCityFilter = ref('ALL')
  const selectedIndustryFilter = ref('ALL')
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

  const availableCityFilters = computed(() => ['ALL', ...new Set(listings.value.map((listing) => listing.primaryCityName).filter((city) => city)).values()])
  const availableIndustryFilters = computed(() => ['ALL', ...new Set(listings.value.map((listing) => listing.primaryIndustry).filter((industry) => industry)).values()])
  const takeoverTargetCompanyName = computed(() =>
    listings.value.find((listing) => listing.companyId === takeoverDialogCompanyId.value)?.companyName ?? '')
  const stockSymbolForListing = (listing: StockExchangeListing): string =>
    listing.stockSymbol?.trim().length
      ? listing.stockSymbol
      : `CMP-${listing.companyId.replace(/-/g, '').toUpperCase()}`
  const selectedOrderListing = computed(() =>
    listings.value.find((listing) => stockSymbolForListing(listing) === selectedOrderBookSymbol.value) ?? null)

  const filteredAndSortedListings = computed(() => {
    const text = filterText.value.trim().toLowerCase()
    const filtered = listings.value.filter((listing) => {
      const matchesText = text.length === 0
        || listing.companyName.toLowerCase().includes(text)
        || listing.primaryCityName.toLowerCase().includes(text)
        || listing.primaryIndustry.toLowerCase().includes(text)
      const matchesCity = selectedCityFilter.value === 'ALL' || listing.primaryCityName === selectedCityFilter.value
      const matchesIndustry = selectedIndustryFilter.value === 'ALL' || listing.primaryIndustry === selectedIndustryFilter.value
      return matchesText && matchesCity && matchesIndustry
    })
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

  watch([filterText, selectedCityFilter, selectedIndustryFilter, sortField, sortDir], () => { currentPage.value = 1 })
  watch(totalPages, (value) => { if (currentPage.value > value) currentPage.value = value })
  watch(activeSettlementAccounts, (accounts) => {
    if (!accounts.some((account) => account.id === selectedSettlementBankAccountId.value))
      selectedSettlementBankAccountId.value = accounts[0]?.id ?? ''
  })
  watch(selectedOrderBookSymbol, () => {
    void loadOrderBookAndTrades()
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
    for (const listing of listings.value) {
      quantityByCompany.value[listing.companyId] ??= 100
      dividendPerShareDraftByCompany.value[listing.companyId] ??= Number(listing.sharePrice > 0 ? listing.sharePrice * 0.01 : 0.01)
    }
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

  async function loadDividendProposals(companyId: string) {
    dividendProposalsLoadingByCompany.value[companyId] = true
    dividendProposalsErrorByCompany.value[companyId] = null
    try {
      const listing = listings.value.find((candidate) => candidate.companyId === companyId)
      if (!listing) {
        dividendProposalsByCompany.value[companyId] = []
        return
      }

      const data = await gqlRequest<{ dividendProposals: DividendProposal[] }>(DIVIDEND_PROPOSALS_QUERY, {
        stockSymbol: stockSymbolForListing(listing),
      })
      dividendProposalsByCompany.value[companyId] = data.dividendProposals
    } catch (reason: unknown) {
      dividendProposalsErrorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.dividendGovernanceLoadFailed')
    } finally {
      dividendProposalsLoadingByCompany.value[companyId] = false
    }
  }

  function updateDividendPerShareDraft(companyId: string, value: number) {
    const safe = Number.isFinite(value) ? value : 0
    dividendPerShareDraftByCompany.value[companyId] = Math.max(0, safe)
  }

  async function toggleTradePanel(companyId: string) {
    expandedCompany.value = expandedCompany.value === companyId ? null : companyId
    errorByCompany.value[companyId] = null
    successByCompany.value[companyId] = null
    if (expandedCompany.value === companyId) {
      const loadTasks: Promise<void>[] = []
      if (!priceHistoryByCompany.value[companyId]) loadTasks.push(loadPriceHistory(companyId))
      if (!shareholdersByCompany.value[companyId]) loadTasks.push(loadShareholders(companyId))
      if (!dividendProposalsByCompany.value[companyId]) loadTasks.push(loadDividendProposals(companyId))
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
      const hasSelectedSymbol = listingData.stockExchangeListings.some((listing) => stockSymbolForListing(listing) === selectedOrderBookSymbol.value)
      if (!hasSelectedSymbol) {
        selectedOrderBookSymbol.value = listingData.stockExchangeListings[0] ? stockSymbolForListing(listingData.stockExchangeListings[0]) : ''
      }
      const hasSelectedSettlement = activeSettlementAccounts.value.some((account) => account.id === selectedSettlementBankAccountId.value)
      if (!hasSelectedSettlement) selectedSettlementBankAccountId.value = activeSettlementAccounts.value[0]?.id ?? ''
      setDefaultQuantities()
      if (auth.isAuthenticated) {
        await Promise.all([loadOpenOrders(), loadOrderBookAndTrades()])
      }
    } catch (reason: unknown) {
      if (!isRefresh) error.value = reason instanceof Error ? reason.message : t('stockExchange.loadFailed')
    } finally { loading.value = false }
  }

  async function loadOpenOrders() {
    if (!auth.isAuthenticated) {
      openOrders.value = []
      return
    }
    const data = await gqlRequest<{ myOpenOrders: OpenLimitOrder[] }>(OPEN_ORDERS_QUERY)
    openOrders.value = data.myOpenOrders
  }

  async function loadOrderBookAndTrades() {
    if (!selectedOrderBookSymbol.value) {
      orderBook.value = { stockSymbol: '', bids: [], asks: [] }
      tradeHistory.value = []
      return
    }

    const [orderBookData, tradeData] = await Promise.all([
      gqlRequest<{ orderBook: StockOrderBook }>(ORDER_BOOK_QUERY, { stockSymbol: selectedOrderBookSymbol.value }),
      gqlRequest<{ stockTradeHistory: StockTradeExecution[] }>(STOCK_TRADE_HISTORY_QUERY, { stockSymbol: selectedOrderBookSymbol.value, limit: 20 }),
    ])
    orderBook.value = orderBookData.orderBook
    tradeHistory.value = tradeData.stockTradeHistory
  }

  async function placeLimitOrder() {
    if (!selectedOrderBookSymbol.value) return
    if (!Number.isFinite(orderPrice.value) || orderPrice.value <= 0) {
      orderError.value = t('stockExchange.orderInvalidPrice')
      orderSuccess.value = null
      return
    }
    if (!Number.isFinite(orderQuantity.value) || orderQuantity.value <= 0) {
      orderError.value = t('stockExchange.orderInvalidQuantity')
      orderSuccess.value = null
      return
    }

    orderLoading.value = true
    orderError.value = null
    orderSuccess.value = null
    try {
      await gqlRequest<{ placeLimitOrder: OpenLimitOrder }>(PLACE_LIMIT_ORDER_MUTATION, {
        input: {
          stockSymbol: selectedOrderBookSymbol.value,
          side: orderSide.value,
          limitPrice: Number(orderPrice.value),
          quantity: Math.floor(orderQuantity.value),
        },
      })
      orderSuccess.value = t('stockExchange.orderPlaced')
      await Promise.all([loadData(true), auth.fetchMe()])
    } catch (reason: unknown) {
      orderError.value = reason instanceof Error ? reason.message : t('stockExchange.orderActionFailed')
    } finally {
      orderLoading.value = false
    }
  }

  async function cancelLimitOrder(orderId: string) {
    orderLoading.value = true
    orderError.value = null
    orderSuccess.value = null
    try {
      await gqlRequest<{ cancelLimitOrder: { id: string } }>(CANCEL_LIMIT_ORDER_MUTATION, { orderId })
      orderSuccess.value = t('stockExchange.orderCancelled')
      await Promise.all([loadData(true), auth.fetchMe()])
    } catch (reason: unknown) {
      orderError.value = reason instanceof Error ? reason.message : t('stockExchange.orderActionFailed')
    } finally {
      orderLoading.value = false
    }
  }

  function openTakeoverDialog(companyId: string) {
    takeoverDialogCompanyId.value = companyId
    takeoverError.value = null
    takeoverSuccess.value = null
  }

  function closeTakeoverDialog() {
    takeoverDialogCompanyId.value = null
    takeoverError.value = null
    takeoverSuccess.value = null
  }

  async function executeTakeover() {
    const companyId = takeoverDialogCompanyId.value
    const newCeoPlayerId = auth.player?.id
    if (!companyId || !newCeoPlayerId) return

    takeoverLoading.value = true
    takeoverError.value = null
    try {
      const data = await gqlRequest<{ replaceCEO: ReplaceCeoResult }>(REPLACE_CEO_MUTATION, {
        input: { companyId, newCeoPlayerId },
      })
      takeoverSuccess.value = data.replaceCEO
      await Promise.all([loadData(true), auth.fetchMe()])
      expandedCompany.value = companyId
    } catch (reason: unknown) {
      takeoverError.value = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
    } finally {
      takeoverLoading.value = false
    }
  }

  async function executeTrade(kind: 'buy' | 'sell', companyId: string) {
    const shareCount = getQuantity(companyId)
    if (shareCount <= 0) { errorByCompany.value[companyId] = t('stockExchange.invalidQuantity'); successByCompany.value[companyId] = null; return }
    actionLoadingKey.value = `${kind}-${companyId}`
    errorByCompany.value[companyId] = null
    successByCompany.value[companyId] = null
    if (!selectedSettlementBankAccountId.value) { errorByCompany.value[companyId] = t('stockExchange.selectSettlementAccount'); successByCompany.value[companyId] = null; return }
    try {
      let result: ShareTradeResult
      if (kind === 'buy') {
        const data = await gqlRequest<{ buyShares: ShareTradeResult }>(BUY_MUTATION, { input: { companyId, shareCount, bankAccountId: selectedSettlementBankAccountId.value } })
        result = data.buyShares
        successByCompany.value[companyId] = t('stockExchange.buySuccess', { company: result.companyName, shares: formatShares(result.shareCount) })
      } else {
        const data = await gqlRequest<{ sellShares: ShareTradeResult }>(SELL_MUTATION, { input: { companyId, shareCount, bankAccountId: selectedSettlementBankAccountId.value } })
        result = data.sellShares
        if (result.taxReserved > 0) successByCompany.value[companyId] = t('stockExchange.sellSuccessWithTax', { company: result.companyName, shares: formatShares(result.shareCount), tax: formatCurrency(result.taxReserved) })
        else successByCompany.value[companyId] = t('stockExchange.sellSuccess', { company: result.companyName, shares: formatShares(result.shareCount) })
      }
      await Promise.all([loadData(true), auth.fetchMe()])
    } catch (reason: unknown) {
      if (reason instanceof GraphQLError && reason.code === 'INVALID_CLIENT_OVERRIDE') {
        errorByCompany.value[companyId] = t('stockExchange.actionFailed')
      } else {
        errorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
      }
    } finally { actionLoadingKey.value = null }
  }

  async function proposeDividend(companyId: string) {
    const listing = listings.value.find((candidate) => candidate.companyId === companyId)
    if (!listing) {
      return
    }

    const dividendPerShare = Number(dividendPerShareDraftByCompany.value[companyId] ?? 0)
    if (!Number.isFinite(dividendPerShare) || dividendPerShare <= 0) {
      errorByCompany.value[companyId] = t('stockExchange.dividendInvalidPerShare')
      successByCompany.value[companyId] = null
      return
    }

    actionLoadingKey.value = `propose-dividend-${companyId}`
    errorByCompany.value[companyId] = null
    successByCompany.value[companyId] = null
    try {
      await gqlRequest<{ proposeDividend: { id: string } }>(PROPOSE_DIVIDEND_MUTATION, {
        input: {
          stockSymbol: stockSymbolForListing(listing),
          dividendPerShare,
        },
      })
      successByCompany.value[companyId] = t('stockExchange.dividendProposalCreated')
      await Promise.all([loadDividendProposals(companyId), loadData(true)])
    } catch (reason: unknown) {
      errorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
    } finally {
      actionLoadingKey.value = null
    }
  }

  async function voteDividendProposal(companyId: string, proposalId: string, choice: 'FOR' | 'AGAINST') {
    actionLoadingKey.value = `vote-dividend-${proposalId}-${choice}`
    errorByCompany.value[companyId] = null
    successByCompany.value[companyId] = null
    try {
      const result = await gqlRequest<{ voteDividendProposal: DividendVoteResult }>(VOTE_DIVIDEND_PROPOSAL_MUTATION, {
        input: {
          proposalId,
          choice,
        },
      })
      successByCompany.value[companyId] = t('stockExchange.dividendVoteSuccess', {
        shares: formatShares(result.voteDividendProposal.sharesVoted),
        choice: choice === 'FOR' ? t('stockExchange.voteFor') : t('stockExchange.voteAgainst'),
      })
      await loadDividendProposals(companyId)
    } catch (reason: unknown) {
      errorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
    } finally {
      actionLoadingKey.value = null
    }
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

  return {
    currentTick,
    loading,
    error,
    actionLoadingKey,
    personAccount,
    listings,
    openOrders,
    selectedOrderBookSymbol,
    orderBook,
    tradeHistory,
    orderSide,
    orderPrice,
    orderQuantity,
    orderLoading,
    orderError,
    orderSuccess,
    selectedOrderListing,
    stockSymbolForListing,
    selectedSettlementBankAccountId,
    quantityByCompany,
    errorByCompany,
    successByCompany,
    expandedCompany,
    priceHistoryByCompany,
    priceHistoryLoadingByCompany,
    priceHistoryErrorByCompany,
    dividendProposalsByCompany,
    dividendProposalsLoadingByCompany,
    dividendProposalsErrorByCompany,
    dividendPerShareDraftByCompany,
    shareholdersByCompany,
    shareholdersLoadingByCompany,
    shareholdersErrorByCompany,
    mergeDialogOpen,
    mergeDestinationCompanyId,
    mergeLoading,
    mergeError,
    mergeSuccess,
    takeoverDialogOpen,
    takeoverLoading,
    takeoverError,
    takeoverSuccess,
    takeoverTargetCompanyName,
    filterText,
    selectedCityFilter,
    selectedIndustryFilter,
    availableCityFilters,
    availableIndustryFilters,
    sortField,
    sortDir,
    currentPage,
    controlledCompanies,
    portfolioValue,
    recentDividendTotal,
    activeTradeAccountName,
    activeTradeAccountType,
    activeTradeAccountCash,
    activeSettlementAccounts,
    filteredAndSortedListings,
    totalPages,
    paginatedListings,
    toggleSort,
    sortIcon,
    isControlledCompany,
    updateQuantity,
    estimatedBuyCost,
    estimatedSellProceeds,
    toggleTradePanel,
    loadData,
    loadOrderBookAndTrades,
    placeLimitOrder,
    cancelLimitOrder,
    openTakeoverDialog,
    closeTakeoverDialog,
    executeTakeover,
    executeTrade,
    proposeDividend,
    voteDividendProposal,
    updateDividendPerShareDraft,
    openMergeDialog,
    closeMergeDialog,
    executeMerge,
    locale,
  }
}
