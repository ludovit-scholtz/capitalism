<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import StockMarketListingRow from '@/components/stock/StockMarketListingRow.vue'
import StockMergeDialog from '@/components/stock/StockMergeDialog.vue'
import StockPersonPortfolio from '@/components/stock/StockPersonPortfolio.vue'
import StockSummaryCards from '@/components/stock/StockSummaryCards.vue'
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
const mergeDialogOpen = computed({
  get: () => mergeDialogCompanyId.value !== null,
  set: (val: boolean) => {
    if (!val) closeMergeDialog()
  },
})
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
  // Only include companies where the player has ALREADY switched to company account ÔÇö not merely where
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

function formatShares(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
    maximumFractionDigits: Number.isInteger(value) ? 0 : 4,
  }).format(value)
}

onMounted(() => {
  void loadData()
})

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await loadData(true)
  await restoreScrollPosition(scrollPos)
})
</script>

<template>
  <div class="stocks-view">
    <section class="stocks-hero pb-8 pt-14">
      <div class="container">
        <p class="stocks-eyebrow mb-2 text-[0.8rem] font-bold uppercase tracking-[0.14em] text-brand">{{ t('stockExchange.eyebrow') }}</p>
        <h1 class="stocks-title m-0">{{ t('stockExchange.title') }}</h1>
        <p class="stocks-subtitle mt-3 max-w-4xl text-muted">{{ t('stockExchange.subtitle') }}</p>
        <div class="stocks-hero-meta mt-4 flex flex-wrap gap-3">
          <span
            class="stocks-tick-chip inline-flex cursor-default select-none items-center gap-1.5 rounded-full border border-white/15 bg-white/5 px-3 py-1 text-[0.78rem] text-muted"
            :title="t('stockExchange.tickHint')"
            ><span class="stocks-tick-label text-[0.72rem] font-semibold uppercase tracking-[0.04em] text-brand">{{ t('stockExchange.tick') }}</span
            ><span class="stocks-tick-value font-bold text-body">{{ currentTick !== null ? currentTick : '\u2014' }}</span></span
          >
        </div>
      </div>
    </section>
    <div class="container stocks-body grid gap-6 pb-12">
      <div v-if="loading" class="state-box">
        <p>{{ t('common.loading') }}</p>
      </div>
      <div v-else-if="error" class="state-box state-error" role="alert">
        <p>{{ error }}</p>
        <button class="btn btn-secondary" @click="() => void loadData()">{{ t('common.tryAgain') }}</button>
      </div>
      <div v-else class="grid gap-6">
        <StockSummaryCards v-if="personAccount" :person-account="personAccount" :portfolio-value="portfolioValue" :recent-dividend-total="recentDividendTotal" :locale="locale" />
        <section class="panel">
          <div class="section-header">
            <div>
              <h2>{{ t('stockExchange.marketTitle') }}</h2>
              <p>{{ t('stockExchange.marketDesc') }}</p>
            </div>
          </div>
          <p v-if="!personAccount" class="market-note">
            {{ t('stockExchange.signInBody') }} <RouterLink to="/login">{{ t('common.login') }}</RouterLink>
          </p>
          <div class="market-controls">
            <input v-model="filterText" type="search" :placeholder="t('stockExchange.filterPlaceholder')" class="filter-input" :aria-label="t('stockExchange.filterLabel')" />
          </div>
          <div v-if="filteredAndSortedListings.length === 0" class="empty-state">{{ filterText ? t('stockExchange.noListingsFiltered') : t('stockExchange.noListings') }}</div>
          <div v-else class="table-wrapper">
            <table class="data-table market-table" :aria-label="t('stockExchange.marketTitle')">
              <thead>
                <tr>
                  <th>
                    <button class="sort-btn" @click="toggleSort('name')">
                      {{ t('stockExchange.company') }} <span aria-hidden="true">{{ sortIcon('name') }}</span>
                    </button>
                  </th>
                  <th>
                    <button class="sort-btn" @click="toggleSort('price')">
                      {{ t('stockExchange.sharePrice') }} <span aria-hidden="true">{{ sortIcon('price') }}</span>
                    </button>
                  </th>
                  <th>
                    <button class="sort-btn" @click="toggleSort('marketValue')">
                      {{ t('stockExchange.marketValue') }} <span aria-hidden="true">{{ sortIcon('marketValue') }}</span>
                    </button>
                  </th>
                  <th>{{ t('stockExchange.publicFloat') }}</th>
                  <th>
                    <button class="sort-btn" @click="toggleSort('ownership')">
                      {{ t('stockExchange.controlRatio') }} <span aria-hidden="true">{{ sortIcon('ownership') }}</span>
                    </button>
                  </th>
                  <th>
                    <button class="sort-btn" @click="toggleSort('dividend')">
                      {{ t('stockExchange.dividendPayout') }} <span aria-hidden="true">{{ sortIcon('dividend') }}</span>
                    </button>
                  </th>
                  <th v-if="personAccount">{{ t('stockExchange.actions') }}</th>
                </tr>
              </thead>
              <tbody>
                <StockMarketListingRow
                  v-for="listing in paginatedListings"
                  :key="listing.companyId"
                  :listing="listing"
                  :locale="locale"
                  :show-actions="!!personAccount"
                  :expanded="expandedCompany === listing.companyId"
                  :is-controlled-company="isControlledCompany(listing.companyId)"
                  :action-loading-key="actionLoadingKey"
                  :active-trade-account-name="activeTradeAccountName"
                  :active-trade-account-type="activeTradeAccountType"
                  :active-trade-account-cash="activeTradeAccountCash"
                  :active-settlement-accounts="activeSettlementAccounts"
                  :selected-settlement-bank-account-id="selectedSettlementBankAccountId"
                  :quantity="quantityByCompany[listing.companyId] ?? 100"
                  :estimated-buy-cost="estimatedBuyCost(listing)"
                  :estimated-sell-proceeds="estimatedSellProceeds(listing)"
                  :success-message="successByCompany[listing.companyId] ?? null"
                  :error-message="errorByCompany[listing.companyId] ?? null"
                  :price-history="priceHistoryByCompany[listing.companyId] ?? []"
                  :price-history-loading="priceHistoryLoadingByCompany[listing.companyId] ?? false"
                  :price-history-error="priceHistoryErrorByCompany[listing.companyId] ?? null"
                  :shareholders="shareholdersByCompany[listing.companyId] ?? null"
                  :shareholders-loading="shareholdersLoadingByCompany[listing.companyId] ?? false"
                  :shareholders-error="shareholdersErrorByCompany[listing.companyId] ?? null"
                  @toggle-trade-panel="toggleTradePanel(listing.companyId)"
                  @switch-to-company="switchToCompanyAccount(listing.companyId)"
                  @open-merge="openMergeDialog(listing.companyId)"
                  @update:settlement-bank-account-id="selectedSettlementBankAccountId = $event"
                  @update-quantity="updateQuantity(listing.companyId, $event)"
                  @buy="executeTrade('buy', listing.companyId)"
                  @sell="executeTrade('sell', listing.companyId)"
                />
              </tbody>
            </table>
            <div v-if="totalPages > 1" class="pagination-bar">
              <button class="btn btn-secondary btn-sm" :disabled="currentPage === 1" @click="currentPage -= 1">{{ t('stockExchange.prevPage') }}</button
              ><span class="pagination-bar__status"> {{ t('stockExchange.pageStatus', { page: currentPage, total: totalPages }) }} </span
              ><button class="btn btn-secondary btn-sm" :disabled="currentPage === totalPages" @click="currentPage += 1">{{ t('stockExchange.nextPage') }}</button>
            </div>
          </div>
        </section>
        <StockPersonPortfolio v-if="personAccount" :person-account="personAccount" :locale="locale" />
      </div>
    </div>
  </div>
  <StockMergeDialog
    v-model="mergeDialogOpen"
    v-model:destination-company-id="mergeDestinationCompanyId"
    :controlled-companies="controlledCompanies"
    :merge-loading="mergeLoading"
    :merge-error="mergeError"
    :merge-success="mergeSuccess"
    :locale="locale"
    @confirm="executeMerge"
    @close="closeMergeDialog"
  />
</template>

<style scoped>
.stocks-view {
  min-height: 100vh;
  background: radial-gradient(circle at top, color-mix(in srgb, var(--color-primary) 10%, transparent), transparent 35%), var(--color-background);
}

.panel,
.state-box {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 18px;
  box-shadow: var(--shadow-sm);
}

.panel {
  padding: 1.4rem;
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
}

.section-header p,
.market-note,
.empty-state {
  color: var(--color-text-secondary);
}

.section-header p {
  margin: 0.35rem 0 0;
}

.market-controls {
  display: flex;
  gap: 0.75rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.filter-input {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-background);
  color: var(--color-text);
  padding: 0.55rem 0.85rem;
  font-size: 0.9rem;
  min-width: 200px;
  max-width: 360px;
  width: 100%;
}

.filter-input:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 1px;
}

.table-wrapper {
  overflow-x: auto;
}

.pagination-bar {
  margin-top: 1rem;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.pagination-bar__status {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.data-table {
  width: 100%;
  border-collapse: collapse;
}

.data-table th,
.data-table td {
  padding: 0.85rem 0.7rem;
  border-bottom: 1px solid var(--color-border);
  text-align: left;
  white-space: nowrap;
}

.data-table tbody tr:last-child td {
  border-bottom: none;
}

.market-table th {
  font-size: 0.78rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-text-secondary);
}

.sort-btn {
  background: none;
  border: none;
  color: inherit;
  font: inherit;
  font-weight: 700;
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  cursor: pointer;
  padding: 0;
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  white-space: nowrap;
}

.sort-btn:hover {
  color: var(--color-primary);
}

.state-error {
  background: color-mix(in srgb, var(--color-danger, #ef4444) 12%, var(--color-surface));
}

@media (max-width: 720px) {
  .stocks-hero {
    padding-top: 2.5rem;
  }
  .panel {
    padding: 1rem;
  }
  .pagination-bar {
    justify-content: flex-start;
  }
}
</style>
