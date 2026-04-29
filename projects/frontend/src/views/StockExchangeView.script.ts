/* oxlint-disable no-unused-vars */
 
 
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

type ControlledCompanyAccount = {
  id: string
  name: string
  cash: number | null
}

type SortField = 'name' | 'price' | 'marketValue' | 'ownership' | 'dividend'
type SortDir = 'asc' | 'desc'

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

// Shareholders state per company
const shareholdersByCompany = ref<Record<string, CompanyOwnership>>({})
const shareholdersLoadingByCompany = ref<Record<string, boolean>>({})
const shareholdersErrorByCompany = ref<Record<string, string | null>>({})

// Merge dialog state
const mergeDialogCompanyId = ref<string | null>(null)
const mergeDestinationCompanyId = ref<string>('')
const mergeLoading = ref(false)
const mergeError = ref<string | null>(null)
const mergeSuccess = ref<MergeCompanyResult | null>(null)

// Sort and filter state
const filterText = ref('')
const sortField = ref<SortField>('marketValue')
const sortDir = ref<SortDir>('desc')
const currentPage = ref(1)
const pageSize = 10

const PERSON_ACCOUNT_QUERY = `
  query PersonAccount {
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

const LISTINGS_QUERY = `
  query StockExchangeListings {
    stockExchangeListings {
      companyId
      companyName
      totalSharesIssued
      publicFloatShares
      sharePrice
      marketValue
      bidPrice
      askPrice
      dividendPayoutRatio
      playerOwnedShares
      controlledCompanyOwnedShares
      combinedControlledOwnershipRatio
      canClaimControl
      canMerge
    }
  }
`

const MY_BANK_ACCOUNTS_QUERY = `
  query MyBankAccounts {
    myBankAccounts {
      id
      accountNumber
      currencyCode
      balance
      companyId
      companyName
      ownerType
      ownerDisplayName
    }
  }
`

const BUY_MUTATION = `
  mutation BuyShares($input: BuySharesInput!) {
    buyShares(input: $input) {
      companyId
      companyName
      accountType
      accountCompanyId
      accountName
      shareCount
      pricePerShare
      totalValue
      ownedShareCount
      publicFloatShares
      personalCash
      personalTaxReserve
      companyCash
    }
  }
`

const SELL_MUTATION = `
  mutation SellShares($input: SellSharesInput!) {
    sellShares(input: $input) {
      companyId
      companyName
      accountType
      accountCompanyId
      accountName
      shareCount
      pricePerShare
      totalValue
      taxReserved
      ownedShareCount
      publicFloatShares
      personalCash
      personalTaxReserve
      companyCash
    }
  }
`

const PRICE_HISTORY_QUERY = `
  query StockExchangePriceHistory($companyId: UUID!) {
    stockExchangePriceHistory(companyId: $companyId) {
      companyId
      tick
      price
      recordedAtUtc
    }
  }
`

const MERGE_MUTATION = `
  mutation MergeCompany($input: MergeCompanyInput!) {
    mergeCompany(input: $input) {
      destinationCompanyId
      destinationCompanyName
      absorbedCompanyName
      cashTransferred
      buildingsTransferred
    }
  }
`

const COMPANY_SHAREHOLDERS_QUERY = `
  query CompanyShareholders($companyId: UUID!) {
    companyShareholders(companyId: $companyId) {
      companyId
      companyName
      totalSharesIssued
      publicFloatShares
      shareholderCount
      shareholders {
        holderName
        holderType
        holderPlayerId
        holderCompanyId
        shareCount
        ownershipRatio
      }
    }
  }
`

const controlledCompanies = computed<ControlledCompanyAccount[]>(() => {
  const directCompanies = (auth.player?.companies ?? []).map((company) => ({
    id: company.id,
    name: company.name,
    cash: company.cash,
  }))
  const directCompanyIds = new Set(directCompanies.map((company) => company.id))
  // Only include companies where the player has ALREADY switched to company account — not merely where
  // they COULD claim control. Including all `canClaimControl` companies causes isControlledCompany()
  // to return true, hiding the Claim Control button before the player has actually claimed anything.
  const activeCompanyId = personAccount.value?.activeCompanyId ?? null
  const derivedCompanies = listings.value
    .filter((listing) => listing.canClaimControl)
    .filter((listing) => !directCompanyIds.has(listing.companyId))
    .filter((listing) => activeCompanyId === listing.companyId)
    .map((listing) => ({
      id: listing.companyId,
      name: listing.companyName,
      cash: null,
    }))

  return [...directCompanies, ...derivedCompanies]
})

const portfolioValue = computed(() => personAccount.value?.shareholdings.reduce((total, holding) => total + holding.marketValue, 0) ?? 0)

const recentDividendTotal = computed(() => personAccount.value?.dividendPayments.slice(0, 5).reduce((total, payment) => total + payment.totalAmount, 0) ?? 0)

const activeTradeAccount = computed(() => getActiveAccountOption(auth.player, auth.player?.companies ?? []))

const activeTradeAccountName = computed(() => activeTradeAccount.value?.name ?? personAccount.value?.displayName ?? t('stockExchange.personAccount'))

const activeTradeAccountType = computed(() => activeTradeAccount.value?.accountType ?? 'PERSON')

const activeTradeAccountCash = computed(() => {
  if (activeTradeAccount.value?.accountType === 'COMPANY') {
    return activeTradeAccount.value.cash
  }

  // For personal account, always use availableCash (gross personalCash minus taxReserve).
  // If personAccount hasn't loaded yet, show null rather than the incorrect gross amount.
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
    if (sortField.value === 'name') {
      cmp = a.companyName.localeCompare(b.companyName)
    } else if (sortField.value === 'price') {
      cmp = a.sharePrice - b.sharePrice
    } else if (sortField.value === 'marketValue') {
      cmp = a.marketValue - b.marketValue
    } else if (sortField.value === 'ownership') {
      cmp = a.combinedControlledOwnershipRatio - b.combinedControlledOwnershipRatio
    } else if (sortField.value === 'dividend') {
      cmp = a.dividendPayoutRatio - b.dividendPayoutRatio
    }
    return sortDir.value === 'asc' ? cmp : -cmp
  })
})

const totalPages = computed(() => Math.max(1, Math.ceil(filteredAndSortedListings.value.length / pageSize)))

const paginatedListings = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  return filteredAndSortedListings.value.slice(start, start + pageSize)
})

watch([filterText, sortField, sortDir], () => {
  currentPage.value = 1
})

watch(totalPages, (value) => {
  if (currentPage.value > value) {
    currentPage.value = value
  }
})

watch(activeSettlementAccounts, (accounts) => {
  if (!accounts.some((account) => account.id === selectedSettlementBankAccountId.value)) {
    selectedSettlementBankAccountId.value = accounts[0]?.id ?? ''
  }
})

function toggleSort(field: SortField) {
  if (sortField.value === field) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortField.value = field
    sortDir.value = 'desc'
  }
}

function sortIcon(field: SortField): string {
  if (sortField.value !== field) return '\u2195'
  return sortDir.value === 'asc' ? '\u2191' : '\u2193'
}

function setDefaultQuantities() {
  for (const listing of listings.value) {
    quantityByCompany.value[listing.companyId] ??= 100
  }
}

function isControlledCompany(companyId: string): boolean {
  return controlledCompanies.value.some((company) => company.id === companyId)
}

function getQuantity(companyId: string): number {
  const value = Number(quantityByCompany.value[companyId] ?? 100)
  if (!Number.isFinite(value)) {
    return 0
  }
  return Math.max(Math.floor(value), 0)
}

function updateQuantity(companyId: string, value: number) {
  quantityByCompany.value[companyId] = Math.max(Math.floor(Number.isFinite(value) ? value : 0), 1)
}

function estimatedBuyCost(listing: StockExchangeListing): number {
  return getQuantity(listing.companyId) * listing.askPrice
}

function estimatedSellProceeds(listing: StockExchangeListing): number {
  return getQuantity(listing.companyId) * listing.bidPrice
}

function resolveTradeAccount(): { tradeAccountType: string; tradeAccountCompanyId: string | null } {
  if (activeTradeAccountType.value === 'COMPANY') {
    return {
      tradeAccountType: 'COMPANY',
      tradeAccountCompanyId: activeTradeAccount.value?.companyId ?? auth.player?.activeCompanyId ?? null,
    }
  }

  return { tradeAccountType: 'PERSON', tradeAccountCompanyId: null }
}

async function loadPriceHistory(companyId: string) {
  priceHistoryLoadingByCompany.value[companyId] = true
  priceHistoryErrorByCompany.value[companyId] = null

  try {
    const data = await gqlRequest<{ stockExchangePriceHistory: StockExchangePriceHistoryPoint[] }>(PRICE_HISTORY_QUERY, {
      companyId,
    })
    priceHistoryByCompany.value[companyId] = data.stockExchangePriceHistory
  } catch (reason: unknown) {
    priceHistoryErrorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.historyLoadFailed')
  } finally {
    priceHistoryLoadingByCompany.value[companyId] = false
  }
}

async function loadShareholders(companyId: string) {
  shareholdersLoadingByCompany.value[companyId] = true
  shareholdersErrorByCompany.value[companyId] = null

  try {
    const data = await gqlRequest<{ companyShareholders: CompanyOwnership | null }>(COMPANY_SHAREHOLDERS_QUERY, { companyId })
    if (data.companyShareholders) {
      shareholdersByCompany.value[companyId] = data.companyShareholders
    }
  } catch (reason: unknown) {
    shareholdersErrorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.shareholdersLoadFailed')
  } finally {
    shareholdersLoadingByCompany.value[companyId] = false
  }
}

async function toggleTradePanel(companyId: string) {
  expandedCompany.value = expandedCompany.value === companyId ? null : companyId
  errorByCompany.value[companyId] = null
  successByCompany.value[companyId] = null

  if (expandedCompany.value === companyId) {
    const loadTasks: Promise<void>[] = []
    if (!priceHistoryByCompany.value[companyId]) {
      loadTasks.push(loadPriceHistory(companyId))
    }
    if (!shareholdersByCompany.value[companyId]) {
      loadTasks.push(loadShareholders(companyId))
    }
    await Promise.all(loadTasks)
  }
}

async function loadData(isRefresh = false) {
  if (!isRefresh) {
    loading.value = true
  }
  error.value = null

  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    const listingDataPromise = gqlRequest<{ stockExchangeListings: StockExchangeListing[] }>(LISTINGS_QUERY)
    const accountDataPromise = auth.isAuthenticated
      ? gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY)
      : Promise.resolve({ myBankAccounts: [] as PlayerBankAccountSummary[] })
    let resolvedPersonAccount: PersonAccount | null = null

    try {
      const personData = await gqlRequest<{ personAccount: PersonAccount | null }>(PERSON_ACCOUNT_QUERY)
      resolvedPersonAccount = personData.personAccount
    } catch {
      resolvedPersonAccount = null
    }

    const listingData = await listingDataPromise
    const accountData = await accountDataPromise

    if (!deepEqual(personAccount.value, resolvedPersonAccount)) {
      personAccount.value = resolvedPersonAccount
    }
    if (!deepEqual(listings.value, listingData.stockExchangeListings)) {
      listings.value = listingData.stockExchangeListings
    }
    if (!deepEqual(myBankAccounts.value, accountData.myBankAccounts)) {
      myBankAccounts.value = accountData.myBankAccounts
    }

    const hasSelectedSettlement = activeSettlementAccounts.value.some((account) => account.id === selectedSettlementBankAccountId.value)
    if (!hasSelectedSettlement) {
      selectedSettlementBankAccountId.value = activeSettlementAccounts.value[0]?.id ?? ''
    }

    setDefaultQuantities()
  } catch (reason: unknown) {
    if (!isRefresh) {
      error.value = reason instanceof Error ? reason.message : t('stockExchange.loadFailed')
    }
  } finally {
    loading.value = false
  }
}

async function switchToCompanyAccount(companyId: string) {
  actionLoadingKey.value = `switch-${companyId}`
  errorByCompany.value[companyId] = null
  successByCompany.value[companyId] = null

  const companyName =
    auth.player?.companies.find((company) => company.id === companyId)?.name ?? listings.value.find((listing) => listing.companyId === companyId)?.companyName ?? t('stockExchange.companyAccount')

  try {
    await auth.switchAccountContext('COMPANY', companyId)
    await loadData(true)
    successByCompany.value[companyId] = t('stockExchange.switchSuccess', { account: companyName })
    // Auto-open the trade panel so the success message is visible to the player
    expandedCompany.value = companyId
  } catch (reason: unknown) {
    errorByCompany.value[companyId] = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
  } finally {
    actionLoadingKey.value = null
  }
}

async function executeTrade(kind: 'buy' | 'sell', companyId: string) {
  const shareCount = getQuantity(companyId)
  if (shareCount <= 0) {
    errorByCompany.value[companyId] = t('stockExchange.invalidQuantity')
    successByCompany.value[companyId] = null
    return
  }

  actionLoadingKey.value = `${kind}-${companyId}`
  errorByCompany.value[companyId] = null
  successByCompany.value[companyId] = null

  const { tradeAccountType, tradeAccountCompanyId } = resolveTradeAccount()
  if (!selectedSettlementBankAccountId.value) {
    errorByCompany.value[companyId] = t('stockExchange.selectSettlementAccount')
    successByCompany.value[companyId] = null
    return
  }

  try {
    let result: ShareTradeResult
    if (kind === 'buy') {
      const data = await gqlRequest<{ buyShares: ShareTradeResult }>(BUY_MUTATION, {
        input: {
          companyId,
          shareCount,
          tradeAccountType,
          tradeAccountCompanyId,
          bankAccountId: selectedSettlementBankAccountId.value,
        },
      })
      result = data.buyShares
      successByCompany.value[companyId] = t('stockExchange.buySuccess', {
        company: result.companyName,
        shares: formatShares(result.shareCount),
      })
    } else {
      const data = await gqlRequest<{ sellShares: ShareTradeResult }>(SELL_MUTATION, {
        input: {
          companyId,
          shareCount,
          tradeAccountType,
          tradeAccountCompanyId,
          bankAccountId: selectedSettlementBankAccountId.value,
        },
      })
      result = data.sellShares
      if (result.taxReserved > 0) {
        successByCompany.value[companyId] = t('stockExchange.sellSuccessWithTax', {
          company: result.companyName,
          shares: formatShares(result.shareCount),
          tax: formatCurrency(result.taxReserved),
        })
      } else {
        successByCompany.value[companyId] = t('stockExchange.sellSuccess', {
          company: result.companyName,
          shares: formatShares(result.shareCount),
        })
      }
    }

    await Promise.all([loadData(true), auth.fetchMe()])
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

function closeMergeDialog() {
  mergeDialogCompanyId.value = null
  mergeError.value = null
  mergeSuccess.value = null
}

async function executeMerge() {
  const targetCompanyId = mergeDialogCompanyId.value
  if (!targetCompanyId || !mergeDestinationCompanyId.value) return

  mergeLoading.value = true
  mergeError.value = null
  mergeSuccess.value = null

  try {
    const data = await gqlRequest<{ mergeCompany: MergeCompanyResult }>(MERGE_MUTATION, {
      input: {
        targetCompanyId,
        destinationCompanyId: mergeDestinationCompanyId.value,
      },
    })
    mergeSuccess.value = data.mergeCompany
    await Promise.all([loadData(true), auth.fetchMe()])
  } catch (reason: unknown) {
    mergeError.value = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
  } finally {
    mergeLoading.value = false
  }
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value)
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`
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

// --- Pie chart helpers ---

const PIE_COLORS = ['#4e79a7', '#f28e2b', '#e15759', '#76b7b2', '#59a14f', '#edc948', '#b07aa1', '#ff9da7', '#9c755f', '#bab0ac']

const PUBLIC_FLOAT_COLOR = '#c8c8c8'
const PIE_OTHER_THRESHOLD = 0.02 // holders < 2% are grouped as "Other"
const PIE_MAX_NAMED_SLICES = 8 // at most 8 named slices before grouping

type PieSlice = {
  label: string
  ratio: number
  color: string
  isPublicFloat?: boolean
  isOther?: boolean
}

function buildPieSlices(ownership: CompanyOwnership): PieSlice[] {
  if (ownership.totalSharesIssued <= 0) return []

  const slices: PieSlice[] = []
  const holders = ownership.shareholders
  let namedSliceCount = 0

  // Separate prominent holders from minor ones
  const prominentHolders = holders.filter((h) => h.ownershipRatio >= PIE_OTHER_THRESHOLD)
  const minorHolders = holders.filter((h) => h.ownershipRatio < PIE_OTHER_THRESHOLD)

  // If there are too many, cap and push rest to "other"
  const displayHolders = prominentHolders.length > PIE_MAX_NAMED_SLICES ? prominentHolders.slice(0, PIE_MAX_NAMED_SLICES) : prominentHolders
  const overflowHolders = prominentHolders.length > PIE_MAX_NAMED_SLICES ? [...prominentHolders.slice(PIE_MAX_NAMED_SLICES), ...minorHolders] : minorHolders

  for (const holder of displayHolders) {
    slices.push({
      label: holder.holderName,
      ratio: holder.ownershipRatio,
      // ?? fallback needed for TypeScript strict noUncheckedIndexedAccess, never reached at runtime
      color: PIE_COLORS[namedSliceCount % PIE_COLORS.length] ?? '#808080',
    })
    namedSliceCount++
  }

  // Group "other" named shareholders
  const otherNamedRatio = overflowHolders.reduce((sum, h) => sum + h.ownershipRatio, 0)
  if (otherNamedRatio > 0.0001) {
    slices.push({
      label: t('stockExchange.shareholdersOther'),
      ratio: otherNamedRatio,
      // ?? fallback needed for TypeScript strict noUncheckedIndexedAccess, never reached at runtime
      color: PIE_COLORS[namedSliceCount % PIE_COLORS.length] ?? '#808080',
      isOther: true,
    })
  }

  // Public float slice
  const floatRatio = ownership.totalSharesIssued > 0 ? ownership.publicFloatShares / ownership.totalSharesIssued : 0
  if (floatRatio > 0.0001) {
    slices.push({
      label: t('stockExchange.shareholdersPublicFloatLabel'),
      ratio: floatRatio,
      color: PUBLIC_FLOAT_COLOR,
      isPublicFloat: true,
    })
  }

  return slices
}

/** Converts a list of pie slices into SVG path data for a donut chart. */
function buildDonutPaths(slices: PieSlice[], cx: number, cy: number, r: number, innerR: number) {
  const paths: { d: string; color: string; label: string; ratio: number; isPublicFloat?: boolean; isOther?: boolean }[] = []
  if (slices.length === 0) return paths

  let startAngle = -Math.PI / 2 // Start at 12 o'clock

  for (const slice of slices) {
    const sweep = slice.ratio * 2 * Math.PI
    const endAngle = startAngle + sweep

    // Outer arc
    const x1 = cx + r * Math.cos(startAngle)
    const y1 = cy + r * Math.sin(startAngle)
    const x2 = cx + r * Math.cos(endAngle)
    const y2 = cy + r * Math.sin(endAngle)
    // Inner arc (for donut)
    const ix1 = cx + innerR * Math.cos(endAngle)
    const iy1 = cy + innerR * Math.sin(endAngle)
    const ix2 = cx + innerR * Math.cos(startAngle)
    const iy2 = cy + innerR * Math.sin(startAngle)

    const largeArc = sweep > Math.PI ? 1 : 0

    const d = `M ${x1} ${y1}` + ` A ${r} ${r} 0 ${largeArc} 1 ${x2} ${y2}` + ` L ${ix1} ${iy1}` + ` A ${innerR} ${innerR} 0 ${largeArc} 0 ${ix2} ${iy2}` + ` Z`

    paths.push({ d, color: slice.color, label: slice.label, ratio: slice.ratio, isPublicFloat: slice.isPublicFloat, isOther: slice.isOther })
    startAngle = endAngle
  }

  return paths
}

onMounted(() => {
  void loadData()
})

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await loadData(true)
  await restoreScrollPosition(scrollPos)
})

