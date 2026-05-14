<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { getActiveAccountOption } from '@/lib/accountContext'
import { computeStockPositionSummary, findStockListingByCompanyId, stockSymbolForListing } from '@/lib/stockTrading'
import type { CompanyOwnership, OpenLimitOrder, PersonAccount, PlayerBankAccountSummary, ShareTradeResult, StockExchangeListing, StockExchangePriceHistoryPoint, StockOrderBook, StockTradeExecution } from '@/types'
import {
  BUY_MUTATION,
  CANCEL_LIMIT_ORDER_MUTATION,
  COMPANY_SHAREHOLDERS_QUERY,
  LISTINGS_QUERY,
  MY_BANK_ACCOUNTS_QUERY,
  OPEN_ORDERS_QUERY,
  ORDER_BOOK_QUERY,
  PERSON_ACCOUNT_QUERY,
  PLACE_LIMIT_ORDER_MUTATION,
  PRICE_HISTORY_QUERY,
  SELL_MUTATION,
  STOCK_TRADE_HISTORY_QUERY,
} from '@/components/stock/stockExchangeQueries'

const route = useRoute()
const { t, locale } = useI18n()
const auth = useAuthStore()

const loading = ref(true)
const error = ref<string | null>(null)
const listing = ref<StockExchangeListing | null>(null)
const personAccount = ref<PersonAccount | null>(null)
const myBankAccounts = ref<PlayerBankAccountSummary[]>([])
const shareholders = ref<CompanyOwnership | null>(null)
const priceHistory = ref<StockExchangePriceHistoryPoint[]>([])
const orderBook = ref<StockOrderBook>({ stockSymbol: '', bids: [], asks: [] })
const tradeHistory = ref<StockTradeExecution[]>([])
const openOrders = ref<OpenLimitOrder[]>([])
const selectedSettlementBankAccountId = ref('')
const tradeQuantity = ref(100)
const orderSide = ref<'BUY' | 'SELL'>('BUY')
const orderPrice = ref(0)
const orderQuantity = ref(100)
const actionLoading = ref(false)
const orderLoading = ref(false)
const tradeError = ref<string | null>(null)
const tradeSuccess = ref<string | null>(null)
const orderError = ref<string | null>(null)
const orderSuccess = ref<string | null>(null)

const companyId = computed(() => (typeof route.params.companyId === 'string' ? route.params.companyId : ''))
const stockSymbol = computed(() => (listing.value ? stockSymbolForListing(listing.value) : ''))
const hasListing = computed(() => listing.value !== null)
const activeTradeAccount = computed(() => getActiveAccountOption(auth.player, auth.player?.companies ?? []))
const activeTradeAccountType = computed(() => activeTradeAccount.value?.accountType ?? 'PERSON')
const activeTradeAccountName = computed(() => activeTradeAccount.value?.name ?? personAccount.value?.displayName ?? t('stockExchange.personAccount'))
const activeTradeAccountCash = computed(() =>
  activeTradeAccountType.value === 'COMPANY'
    ? activeTradeAccount.value?.cash ?? null
    : personAccount.value?.availableCash ?? null,
)
const activeSettlementAccounts = computed(() => {
  if (activeTradeAccountType.value === 'COMPANY') {
    const activeCompanyId = activeTradeAccount.value?.companyId ?? auth.player?.activeCompanyId ?? null
    return myBankAccounts.value.filter((account) => account.ownerType === 'COMPANY' && account.companyId === activeCompanyId && account.currencyCode === 'USD')
  }

  return myBankAccounts.value.filter((account) => account.ownerType === 'PERSON' && account.currencyCode === 'USD')
})

const stockPosition = computed(() =>
  computeStockPositionSummary(personAccount.value, companyId.value, listing.value?.sharePrice ?? 0),
)

const bidDepthTotal = computed(() => orderBook.value.bids.reduce((sum, level) => sum + level.totalQuantity, 0))
const askDepthTotal = computed(() => orderBook.value.asks.reduce((sum, level) => sum + level.totalQuantity, 0))
const estimatedTradeNotional = computed(() => Math.max(0, tradeQuantity.value) * (listing.value?.askPrice ?? 0))
const estimatedOrderNotional = computed(() => Math.max(0, orderQuantity.value) * Math.max(0, orderPrice.value))
const chartPoints = computed(() => priceHistory.value.slice(-24))

watch(activeSettlementAccounts, (accounts) => {
  if (!accounts.some((account) => account.id === selectedSettlementBankAccountId.value)) {
    selectedSettlementBankAccountId.value = accounts[0]?.id ?? ''
  }
})

watch(listing, (value) => {
  if (!value) {
    return
  }

  if (!orderPrice.value || orderPrice.value <= 0) {
    orderPrice.value = value.askPrice
  }
})

watch(companyId, () => {
  void loadData()
})

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(locale.value, { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(value)
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(2)}%`
}

function formatShares(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
    maximumFractionDigits: Number.isInteger(value) ? 0 : 4,
  }).format(value)
}

function positionBarWidth(ownershipRatio: number): string {
  return `${Math.min(100, Math.max(0, ownershipRatio * 100))}%`
}

async function loadData() {
  loading.value = true
  error.value = null

  try {
    auth.initFromStorage()
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    const [listingData, personData, accountData] = await Promise.all([
      gqlRequest<{ stockExchangeListings: StockExchangeListing[] }>(LISTINGS_QUERY),
      auth.isAuthenticated
        ? gqlRequest<{ personAccount: PersonAccount | null }>(PERSON_ACCOUNT_QUERY).catch(() => ({ personAccount: null }))
        : Promise.resolve({ personAccount: null }),
      auth.isAuthenticated
        ? gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY)
        : Promise.resolve({ myBankAccounts: [] as PlayerBankAccountSummary[] }),
    ])

    personAccount.value = personData.personAccount
    myBankAccounts.value = accountData.myBankAccounts
    listing.value = findStockListingByCompanyId(listingData.stockExchangeListings, companyId.value)

    if (!listing.value) {
      shareholders.value = null
      priceHistory.value = []
      orderBook.value = { stockSymbol: '', bids: [], asks: [] }
      tradeHistory.value = []
      openOrders.value = []
      return
    }

    const symbol = stockSymbolForListing(listing.value)
    const [shareholderData, historyData, bookData, tradeData, openOrderData] = await Promise.all([
      gqlRequest<{ companyShareholders: CompanyOwnership | null }>(COMPANY_SHAREHOLDERS_QUERY, { companyId: listing.value.companyId }),
      gqlRequest<{ stockExchangePriceHistory: StockExchangePriceHistoryPoint[] }>(PRICE_HISTORY_QUERY, { companyId: listing.value.companyId }),
      gqlRequest<{ orderBook: StockOrderBook }>(ORDER_BOOK_QUERY, { stockSymbol: symbol }),
      gqlRequest<{ stockTradeHistory: StockTradeExecution[] }>(STOCK_TRADE_HISTORY_QUERY, { stockSymbol: symbol, limit: 30 }),
      auth.isAuthenticated
        ? gqlRequest<{ myOpenOrders: OpenLimitOrder[] }>(OPEN_ORDERS_QUERY)
        : Promise.resolve({ myOpenOrders: [] as OpenLimitOrder[] }),
    ])

    shareholders.value = shareholderData.companyShareholders
    priceHistory.value = historyData.stockExchangePriceHistory
    orderBook.value = bookData.orderBook
    tradeHistory.value = tradeData.stockTradeHistory
    openOrders.value = openOrderData.myOpenOrders.filter((order) => order.companyId === listing.value?.companyId)
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('stockExchange.loadFailed')
  } finally {
    loading.value = false
  }
}

async function executeTrade(kind: 'buy' | 'sell') {
  if (!listing.value) {
    return
  }
  if (!selectedSettlementBankAccountId.value) {
    tradeError.value = t('stockExchange.selectSettlementAccount')
    tradeSuccess.value = null
    return
  }
  if (!Number.isFinite(tradeQuantity.value) || tradeQuantity.value <= 0) {
    tradeError.value = t('stockExchange.invalidQuantity')
    tradeSuccess.value = null
    return
  }

  actionLoading.value = true
  tradeError.value = null
  tradeSuccess.value = null
  try {
    let result: ShareTradeResult

    if (kind === 'buy') {
      const response = await gqlRequest<{ buyShares: ShareTradeResult }>(BUY_MUTATION, {
        input: {
          companyId: listing.value.companyId,
          shareCount: Math.floor(tradeQuantity.value),
          bankAccountId: selectedSettlementBankAccountId.value,
        },
      })
      result = response.buyShares
      tradeSuccess.value = t('stockExchange.buySuccess', {
        company: result.companyName,
        shares: formatShares(result.shareCount),
      })
    } else {
      const response = await gqlRequest<{ sellShares: ShareTradeResult }>(SELL_MUTATION, {
        input: {
          companyId: listing.value.companyId,
          shareCount: Math.floor(tradeQuantity.value),
          bankAccountId: selectedSettlementBankAccountId.value,
        },
      })
      result = response.sellShares
      tradeSuccess.value = result.taxReserved > 0
        ? t('stockExchange.sellSuccessWithTax', {
            company: result.companyName,
            shares: formatShares(result.shareCount),
            tax: formatCurrency(result.taxReserved),
          })
        : t('stockExchange.sellSuccess', {
            company: result.companyName,
            shares: formatShares(result.shareCount),
          })
    }

    await Promise.all([loadData(), auth.fetchMe()])
  } catch (reason: unknown) {
    tradeError.value = reason instanceof Error ? reason.message : t('stockExchange.actionFailed')
  } finally {
    actionLoading.value = false
  }
}

async function placeLimitOrder() {
  if (!listing.value || !stockSymbol.value) {
    return
  }
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
        stockSymbol: stockSymbol.value,
        side: orderSide.value,
        limitPrice: Number(orderPrice.value),
        quantity: Math.floor(orderQuantity.value),
      },
    })
    orderSuccess.value = t('stockExchange.orderPlaced')
    await Promise.all([loadData(), auth.fetchMe()])
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
    await Promise.all([loadData(), auth.fetchMe()])
  } catch (reason: unknown) {
    orderError.value = reason instanceof Error ? reason.message : t('stockExchange.orderActionFailed')
  } finally {
    orderLoading.value = false
  }
}

onMounted(() => {
  void loadData()
})
</script>

<template>
  <div class="mx-auto flex w-full max-w-[1440px] flex-col gap-6 px-4 pb-16 pt-6 lg:px-8 lg:pt-8">
    <nav class="flex items-center gap-2 text-sm text-muted" aria-label="Breadcrumb">
      <RouterLink to="/stocks" class="font-medium text-primary hover:underline">{{ t('stockExchange.title') }}</RouterLink>
      <span aria-hidden="true">/</span>
      <span>{{ t('stockExchange.tradeDeskTitle') }}</span>
    </nav>

    <section v-if="loading" class="rounded-3xl border border-divider bg-card p-6 text-muted">{{ t('common.loading') }}</section>
    <section v-else-if="error" class="rounded-3xl border border-danger/30 bg-card p-6 text-danger" role="alert">
      <p>{{ error }}</p>
      <button class="btn btn-secondary mt-4" @click="() => void loadData()">{{ t('common.tryAgain') }}</button>
    </section>
    <section v-else-if="!hasListing" class="rounded-3xl border border-divider bg-card p-6">
      <h1 class="text-2xl font-bold text-body">{{ t('stockExchange.tradeDeskNotFoundTitle') }}</h1>
      <p class="mt-2 text-muted">{{ t('stockExchange.tradeDeskNotFoundBody') }}</p>
      <RouterLink to="/stocks" class="btn btn-primary mt-4 inline-flex">{{ t('stockExchange.backToMarket') }}</RouterLink>
    </section>
    <template v-else>
      <section class="rounded-3xl border border-divider bg-card p-6">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 class="text-3xl font-bold text-body">{{ listing?.companyName }}</h1>
            <p class="mt-1 text-sm text-muted">{{ t('stockExchange.tradeDeskSubtitle', { symbol: stockSymbol }) }}</p>
          </div>
          <div class="grid grid-cols-2 gap-3 text-sm md:grid-cols-4">
            <div class="rounded-2xl border border-divider px-3 py-2">
              <p class="text-xs uppercase tracking-[0.12em] text-muted">{{ t('stockExchange.sharePrice') }}</p>
              <p class="font-semibold text-body">{{ formatCurrency(listing?.sharePrice ?? 0) }}</p>
            </div>
            <div class="rounded-2xl border border-divider px-3 py-2">
              <p class="text-xs uppercase tracking-[0.12em] text-muted">{{ t('stockExchange.marketValue') }}</p>
              <p class="font-semibold text-body">{{ formatCurrency(listing?.marketValue ?? 0) }}</p>
            </div>
            <div class="rounded-2xl border border-divider px-3 py-2">
              <p class="text-xs uppercase tracking-[0.12em] text-muted">{{ t('stockExchange.totalSharesLabel') }}</p>
              <p class="font-semibold text-body">{{ formatShares(listing?.totalSharesIssued ?? 0) }}</p>
            </div>
            <div class="rounded-2xl border border-divider px-3 py-2">
              <p class="text-xs uppercase tracking-[0.12em] text-muted">{{ t('stockExchange.publicFloatLabel') }}</p>
              <p class="font-semibold text-body">{{ formatShares(listing?.publicFloatShares ?? 0) }}</p>
            </div>
          </div>
        </div>
      </section>

      <section class="grid gap-6 xl:grid-cols-3">
        <article class="rounded-3xl border border-divider bg-card p-6">
          <h2 class="text-xl font-semibold text-body">{{ t('stockExchange.orderBookTitle') }}</h2>
          <p class="mt-1 text-sm text-muted">{{ t('stockExchange.orderBookDesc', { symbol: stockSymbol }) }}</p>
          <div class="mt-4 grid gap-4 md:grid-cols-2">
            <div>
              <h3 class="text-sm font-semibold text-success">{{ t('stockExchange.bids') }}</h3>
              <table class="mt-2 w-full text-left text-sm">
                <thead class="text-xs uppercase tracking-[0.12em] text-muted"><tr><th>{{ t('stockExchange.price') }}</th><th>{{ t('stockExchange.quantity') }}</th></tr></thead>
                <tbody>
                  <tr v-for="bid in orderBook.bids" :key="`bid-${bid.price}`" class="border-b border-divider/50"><td>{{ formatCurrency(bid.price) }}</td><td>{{ bid.totalQuantity }}</td></tr>
                  <tr v-if="orderBook.bids.length === 0"><td colspan="2" class="py-2 text-muted">{{ t('stockExchange.noBids') }}</td></tr>
                </tbody>
              </table>
            </div>
            <div>
              <h3 class="text-sm font-semibold text-danger">{{ t('stockExchange.asks') }}</h3>
              <table class="mt-2 w-full text-left text-sm">
                <thead class="text-xs uppercase tracking-[0.12em] text-muted"><tr><th>{{ t('stockExchange.price') }}</th><th>{{ t('stockExchange.quantity') }}</th></tr></thead>
                <tbody>
                  <tr v-for="ask in orderBook.asks" :key="`ask-${ask.price}`" class="border-b border-divider/50"><td>{{ formatCurrency(ask.price) }}</td><td>{{ ask.totalQuantity }}</td></tr>
                  <tr v-if="orderBook.asks.length === 0"><td colspan="2" class="py-2 text-muted">{{ t('stockExchange.noAsks') }}</td></tr>
                </tbody>
              </table>
            </div>
          </div>
          <p class="mt-3 text-xs text-muted">{{ t('stockExchange.orderBookDepthSummary', { bids: bidDepthTotal, asks: askDepthTotal }) }}</p>
        </article>

        <article class="rounded-3xl border border-divider bg-card p-6 xl:col-span-2">
          <h2 class="text-xl font-semibold text-body">{{ t('stockExchange.priceHistoryTitle') }}</h2>
          <p class="mt-1 text-sm text-muted">{{ t('stockExchange.priceHistoryHint') }}</p>
          <div v-if="chartPoints.length > 0" class="mt-4 flex h-32 items-end gap-1 rounded-xl border border-divider p-3" :aria-label="t('stockExchange.priceHistoryChartLabel')" role="img">
            <div
              v-for="point in chartPoints"
              :key="`point-${point.tick}`"
              class="min-w-0 flex-1 rounded-sm bg-primary/60"
              :title="`${t('stockExchange.tick')} ${point.tick}: ${formatCurrency(point.price)}`"
              :style="{ height: `${Math.max(6, ((point.price / Math.max(...chartPoints.map((item) => item.price), 1)) * 100))}%` }"
            />
          </div>
          <p v-else class="mt-4 text-sm text-muted">{{ t('stockExchange.priceHistoryEmpty') }}</p>
          <h3 class="mt-5 text-sm font-semibold text-body">{{ t('stockExchange.matchedTradesTitle') }}</h3>
          <ul v-if="tradeHistory.length > 0" class="mt-2 grid gap-2">
            <li v-for="trade in tradeHistory" :key="trade.id" class="flex items-center justify-between rounded-xl border border-divider px-3 py-2 text-sm">
              <span>{{ formatShares(trade.quantity) }} @ {{ formatCurrency(trade.price) }}</span>
              <span class="text-muted">{{ t('stockExchange.tick') }} {{ trade.executedAtTick }}</span>
            </li>
          </ul>
          <p v-else class="mt-2 text-sm text-muted">{{ t('stockExchange.noTradeHistory') }}</p>
        </article>
      </section>

      <section class="grid gap-6 lg:grid-cols-2">
        <article class="rounded-3xl border border-divider bg-card p-6">
          <h2 class="text-xl font-semibold text-body">{{ t('stockExchange.tradeDeskActionTitle') }}</h2>
          <p class="mt-1 text-sm text-muted">{{ t('stockExchange.tradeDeskActionDesc') }}</p>
          <div class="mt-4 grid gap-3">
            <label class="text-sm font-medium text-body">{{ t('stockExchange.tradeAccountLabel') }}<span class="ml-2 text-muted">{{ activeTradeAccountName }}</span></label>
            <label class="grid gap-1 text-sm font-medium text-body">
              <span>{{ t('stockExchange.settlementAccountLabel') }}</span>
              <select v-model="selectedSettlementBankAccountId" class="rounded-xl border border-divider bg-background px-3 py-2">
                <option value="">{{ t('stockExchange.selectSettlementAccount') }}</option>
                <option v-for="account in activeSettlementAccounts" :key="account.id" :value="account.id">{{ account.accountNumber }} · {{ formatCurrency(account.balance) }}</option>
              </select>
            </label>
            <p v-if="activeSettlementAccounts.length === 0" class="text-sm text-warning">{{ t('stockExchange.noUsdSettlementAccount') }}</p>
            <label class="grid gap-1 text-sm font-medium text-body">
              <span>{{ t('stockExchange.quantity') }}</span>
              <input v-model.number="tradeQuantity" type="number" min="1" step="1" class="rounded-xl border border-divider bg-background px-3 py-2" />
            </label>
            <p class="text-sm text-muted">{{ t('stockExchange.estimatedCost', { total: formatCurrency(estimatedTradeNotional) }) }}</p>
            <div class="flex flex-wrap gap-2">
              <button class="btn btn-primary" :disabled="actionLoading" @click="executeTrade('buy')">{{ t('stockExchange.buyAt', { price: formatCurrency(listing?.askPrice ?? 0) }) }}</button>
              <button class="btn btn-secondary" :disabled="actionLoading" @click="executeTrade('sell')">{{ t('stockExchange.sellAt', { price: formatCurrency(listing?.bidPrice ?? 0) }) }}</button>
            </div>
            <p v-if="tradeSuccess" class="text-sm text-success">{{ tradeSuccess }}</p>
            <p v-if="tradeError" class="text-sm text-danger">{{ tradeError }}</p>
          </div>
        </article>

        <article class="rounded-3xl border border-divider bg-card p-6">
          <h2 class="text-xl font-semibold text-body">{{ t('stockExchange.limitOrdersTitle') }}</h2>
          <p class="mt-1 text-sm text-muted">{{ t('stockExchange.limitOrdersTradeDeskDesc', { symbol: stockSymbol }) }}</p>
          <div class="mt-4 grid gap-3">
            <label class="grid gap-1 text-sm font-medium text-body"><span>{{ t('stockExchange.orderSide') }}</span><select v-model="orderSide" class="rounded-xl border border-divider bg-background px-3 py-2"><option value="BUY">{{ t('stockExchange.buy') }}</option><option value="SELL">{{ t('stockExchange.sell') }}</option></select></label>
            <label class="grid gap-1 text-sm font-medium text-body"><span>{{ t('stockExchange.orderPrice') }}</span><input v-model.number="orderPrice" type="number" min="0" step="0.0001" class="rounded-xl border border-divider bg-background px-3 py-2" /></label>
            <label class="grid gap-1 text-sm font-medium text-body"><span>{{ t('stockExchange.orderQuantity') }}</span><input v-model.number="orderQuantity" type="number" min="1" step="1" class="rounded-xl border border-divider bg-background px-3 py-2" /></label>
            <p class="text-sm text-muted">{{ t('stockExchange.orderEstimate') }} <strong>{{ formatCurrency(estimatedOrderNotional) }}</strong></p>
            <button class="btn btn-primary" :disabled="orderLoading" @click="placeLimitOrder">{{ t('stockExchange.placeLimitOrder') }}</button>
            <p v-if="orderSuccess" class="text-sm text-success">{{ orderSuccess }}</p>
            <p v-if="orderError" class="text-sm text-danger">{{ orderError }}</p>
            <h3 class="mt-4 text-sm font-semibold text-body">{{ t('stockExchange.openOrdersTitle') }}</h3>
            <ul v-if="openOrders.length > 0" class="open-orders-list grid gap-2">
              <li v-for="order in openOrders" :key="order.id" class="flex items-center justify-between rounded-xl border border-divider px-3 py-2 text-sm">
                <span>{{ listing?.companyName }} · {{ order.side }} · {{ formatShares(order.remainingQuantity) }} @ {{ formatCurrency(order.limitPrice) }}</span>
                <button class="btn btn-secondary btn-sm" :disabled="orderLoading" @click="cancelLimitOrder(order.id)">{{ t('stockExchange.cancelOrder') }}</button>
              </li>
            </ul>
            <p v-else class="text-sm text-muted">{{ t('stockExchange.noOpenOrders') }}</p>
          </div>
        </article>
      </section>

      <section class="grid gap-6 lg:grid-cols-2">
        <article class="rounded-3xl border border-divider bg-card p-6">
          <h2 class="text-xl font-semibold text-body">{{ t('stockExchange.yourPositionTitle') }}</h2>
          <p class="mt-1 text-sm text-muted">{{ t('stockExchange.yourPositionDesc') }}</p>
          <dl class="mt-4 grid gap-3 text-sm">
            <div class="flex items-center justify-between"><dt class="text-muted">{{ t('stockExchange.ownedShares') }}</dt><dd class="font-semibold text-body">{{ formatShares(stockPosition.sharesOwned) }}</dd></div>
            <div>
              <div class="flex items-center justify-between"><dt class="text-muted">{{ t('stockExchange.holdingOwnership') }}</dt><dd class="font-semibold text-body">{{ formatPercent(stockPosition.ownershipRatio) }}</dd></div>
              <div class="mt-2 h-2 overflow-hidden rounded-full bg-background"><div class="h-full rounded-full bg-primary" :style="{ width: positionBarWidth(stockPosition.ownershipRatio) }" /></div>
            </div>
            <div class="flex items-center justify-between"><dt class="text-muted">{{ t('stockExchange.averageBuyPrice') }}</dt><dd class="font-semibold text-body">{{ stockPosition.averageBuyPrice === null ? '—' : formatCurrency(stockPosition.averageBuyPrice) }}</dd></div>
            <div class="flex items-center justify-between"><dt class="text-muted">{{ t('stockExchange.holdingMarketValue') }}</dt><dd class="font-semibold text-body">{{ formatCurrency(stockPosition.marketValue) }}</dd></div>
            <div class="flex items-center justify-between"><dt class="text-muted">{{ t('stockExchange.unrealizedPnl') }}</dt><dd class="font-semibold" :class="(stockPosition.unrealizedPnl ?? 0) >= 0 ? 'text-success' : 'text-danger'">{{ stockPosition.unrealizedPnl === null ? '—' : formatCurrency(stockPosition.unrealizedPnl) }}</dd></div>
            <div class="flex items-center justify-between"><dt class="text-muted">{{ t('stockExchange.availableCash') }}</dt><dd class="font-semibold text-body">{{ activeTradeAccountCash === null ? '—' : formatCurrency(activeTradeAccountCash) }}</dd></div>
          </dl>
        </article>

        <article class="rounded-3xl border border-divider bg-card p-6">
          <h2 class="text-xl font-semibold text-body">{{ t('stockExchange.shareholdersTitle') }}</h2>
          <p class="mt-1 text-sm text-muted">{{ t('stockExchange.shareholdersDesc') }}</p>
          <table v-if="shareholders && shareholders.shareholders.length > 0" class="mt-4 w-full text-left text-sm">
            <thead class="text-xs uppercase tracking-[0.12em] text-muted"><tr><th>{{ t('stockExchange.shareholdersHolder') }}</th><th>{{ t('stockExchange.shareholdersShares') }}</th><th>{{ t('stockExchange.shareholdersOwnership') }}</th></tr></thead>
            <tbody>
              <tr v-for="holder in shareholders.shareholders" :key="`${holder.holderPlayerId ?? holder.holderCompanyId ?? holder.holderName}`" class="border-b border-divider/50 last:border-b-0">
                <td class="py-2">{{ holder.holderName }}</td>
                <td class="py-2">{{ formatShares(holder.shareCount) }}</td>
                <td class="py-2">{{ formatPercent(holder.ownershipRatio) }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="mt-4 text-sm text-muted">{{ t('stockExchange.shareholdersEmpty') }}</p>
        </article>
      </section>
    </template>
  </div>
</template>
