<script setup lang="ts">
import StockMarketListingRow from '@/components/stock/StockMarketListingRow.vue'
import StockMergeDialog from '@/components/stock/StockMergeDialog.vue'
import StockPersonPortfolio from '@/components/stock/StockPersonPortfolio.vue'
import StockSummaryCards from '@/components/stock/StockSummaryCards.vue'
import StockTakeoverDialog from '@/components/stock/StockTakeoverDialog.vue'
import { useStockExchange } from '@/composables/useStockExchange'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const { currentTick, loading, error, personAccount, portfolioValue, recentDividendTotal, locale, filterText, selectedCityFilter, selectedIndustryFilter, availableCityFilters, availableIndustryFilters, filteredAndSortedListings, paginatedListings, totalPages, currentPage, toggleSort, sortIcon, isControlledCompany, actionLoadingKey, activeTradeAccountName, activeTradeAccountType, activeTradeAccountCash, activeSettlementAccounts, selectedSettlementBankAccountId, quantityByCompany, errorByCompany, successByCompany, expandedCompany, priceHistoryByCompany, priceHistoryLoadingByCompany, priceHistoryErrorByCompany, dividendProposalsByCompany, dividendProposalsLoadingByCompany, dividendProposalsErrorByCompany, dividendPerShareDraftByCompany, shareholdersByCompany, shareholdersLoadingByCompany, shareholdersErrorByCompany, mergeDialogOpen, mergeDestinationCompanyId, controlledCompanies, mergeLoading, mergeError, mergeSuccess, takeoverDialogOpen, takeoverLoading, takeoverError, takeoverSuccess, takeoverTargetCompanyName, updateQuantity, estimatedBuyCost, estimatedSellProceeds, toggleTradePanel, executeTrade, proposeDividend, voteDividendProposal, updateDividendPerShareDraft, openTakeoverDialog, closeTakeoverDialog, executeTakeover, openMergeDialog, closeMergeDialog, executeMerge, loadData, openOrders, selectedOrderBookSymbol, orderBook, tradeHistory, orderSide, orderPrice, orderQuantity, orderLoading, orderError, orderSuccess, selectedOrderListing, stockSymbolForListing, placeLimitOrder, cancelLimitOrder } = useStockExchange()

function formatCityFilterLabel(city: string): string {
  return city === 'UNKNOWN' ? t('stockExchange.unknownCity') : city
}

function formatIndustryFilterLabel(industry: string): string {
  if (industry === 'DIVERSIFIED') {
    return t('stockExchange.diversifiedIndustry')
  }

  return industry
    .split('_')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join(' ')
}
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
            <label class="filter-select-label">
              <span>{{ t('stockExchange.cityFilterLabel') }}</span>
              <select v-model="selectedCityFilter" class="filter-select" :aria-label="t('stockExchange.cityFilterLabel')">
                <option value="ALL">{{ t('stockExchange.allCities') }}</option>
                <option v-for="city in availableCityFilters.filter((value) => value !== 'ALL')" :key="city" :value="city">{{ formatCityFilterLabel(city) }}</option>
              </select>
            </label>
            <label class="filter-select-label">
              <span>{{ t('stockExchange.industryFilterLabel') }}</span>
              <select v-model="selectedIndustryFilter" class="filter-select" :aria-label="t('stockExchange.industryFilterLabel')">
                <option value="ALL">{{ t('stockExchange.allIndustries') }}</option>
                <option v-for="industry in availableIndustryFilters.filter((value) => value !== 'ALL')" :key="industry" :value="industry">{{ formatIndustryFilterLabel(industry) }}</option>
              </select>
            </label>
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
                  :dividend-proposals="dividendProposalsByCompany[listing.companyId] ?? []"
                  :dividend-proposals-loading="dividendProposalsLoadingByCompany[listing.companyId] ?? false"
                  :dividend-proposals-error="dividendProposalsErrorByCompany[listing.companyId] ?? null"
                  :dividend-per-share-draft="dividendPerShareDraftByCompany[listing.companyId] ?? 0"
                  :shareholders="shareholdersByCompany[listing.companyId] ?? null"
                  :shareholders-loading="shareholdersLoadingByCompany[listing.companyId] ?? false"
                  :shareholders-error="shareholdersErrorByCompany[listing.companyId] ?? null"
                  @toggle-trade-panel="toggleTradePanel(listing.companyId)"
                  @open-takeover="openTakeoverDialog(listing.companyId)"
                  @open-merge="openMergeDialog(listing.companyId)"
                  @update:settlement-bank-account-id="selectedSettlementBankAccountId = $event"
                  @update-quantity="updateQuantity(listing.companyId, $event)"
                  @buy="executeTrade('buy', listing.companyId)"
                  @sell="executeTrade('sell', listing.companyId)"
                  @update-dividend-per-share="updateDividendPerShareDraft(listing.companyId, $event)"
                  @propose-dividend="proposeDividend(listing.companyId)"
                  @vote-dividend="voteDividendProposal(listing.companyId, $event.proposalId, $event.choice)"
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
        <section v-if="personAccount" class="panel limit-order-panel">
          <div class="section-header">
            <div>
              <h2>{{ t('stockExchange.limitOrdersTitle') }}</h2>
              <p>{{ t('stockExchange.limitOrdersDesc') }}</p>
            </div>
          </div>
          <div class="order-grid">
            <div class="order-form">
              <label class="filter-select-label">
                <span>{{ t('stockExchange.orderSymbol') }}</span>
                <select v-model="selectedOrderBookSymbol" class="filter-select" :aria-label="t('stockExchange.orderSymbol')">
                  <option v-for="listing in filteredAndSortedListings" :key="listing.companyId" :value="stockSymbolForListing(listing)">{{ listing.companyName }} ({{ stockSymbolForListing(listing) }})</option>
                </select>
              </label>
              <label class="filter-select-label">
                <span>{{ t('stockExchange.orderSide') }}</span>
                <select v-model="orderSide" class="filter-select" :aria-label="t('stockExchange.orderSide')">
                  <option value="BUY">{{ t('stockExchange.buy') }}</option>
                  <option value="SELL">{{ t('stockExchange.sell') }}</option>
                </select>
              </label>
              <label class="filter-select-label">
                <span>{{ t('stockExchange.orderPrice') }}</span>
                <input v-model.number="orderPrice" type="number" min="0" step="0.0001" class="filter-input" :aria-label="t('stockExchange.orderPrice')" />
              </label>
              <label class="filter-select-label">
                <span>{{ t('stockExchange.orderQuantity') }}</span>
                <input v-model.number="orderQuantity" type="number" min="1" step="1" class="filter-input" :aria-label="t('stockExchange.orderQuantity')" />
              </label>
              <p class="market-note">
                {{ t('stockExchange.orderEstimate') }}
                <strong>{{ new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format((Number(orderPrice) || 0) * (Number(orderQuantity) || 0)) }}</strong>
              </p>
              <p v-if="selectedOrderListing" class="market-note">
                {{ t('stockExchange.orderAvailableBalance', { account: activeTradeAccountName, balance: new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(activeTradeAccountCash ?? 0) }) }}
              </p>
              <button class="btn btn-primary" :disabled="orderLoading" @click="placeLimitOrder">
                {{ t('stockExchange.placeLimitOrder') }}
              </button>
              <p v-if="orderSuccess" class="order-success">{{ orderSuccess }}</p>
              <p v-if="orderError" class="order-error">{{ orderError }}</p>
            </div>
            <div class="order-book">
              <h3>{{ t('stockExchange.orderBookTitle') }}</h3>
              <div class="order-book-tables">
                <div>
                  <h4 class="book-side-title book-side-buy">{{ t('stockExchange.bids') }}</h4>
                  <table class="data-table order-book-table">
                    <thead>
                      <tr>
                        <th>{{ t('stockExchange.price') }}</th>
                        <th>{{ t('stockExchange.quantity') }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="bid in orderBook.bids" :key="`bid-${bid.price}`" class="book-row-buy">
                        <td>{{ new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', maximumFractionDigits: 4 }).format(bid.price) }}</td>
                        <td>{{ bid.totalQuantity }}</td>
                      </tr>
                      <tr v-if="orderBook.bids.length === 0">
                        <td colspan="2">{{ t('stockExchange.noBids') }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <div>
                  <h4 class="book-side-title book-side-sell">{{ t('stockExchange.asks') }}</h4>
                  <table class="data-table order-book-table">
                    <thead>
                      <tr>
                        <th>{{ t('stockExchange.price') }}</th>
                        <th>{{ t('stockExchange.quantity') }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="ask in orderBook.asks" :key="`ask-${ask.price}`" class="book-row-sell">
                        <td>{{ new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', maximumFractionDigits: 4 }).format(ask.price) }}</td>
                        <td>{{ ask.totalQuantity }}</td>
                      </tr>
                      <tr v-if="orderBook.asks.length === 0">
                        <td colspan="2">{{ t('stockExchange.noAsks') }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </div>
          <div class="order-lists">
            <div>
              <h3>{{ t('stockExchange.openOrdersTitle') }}</h3>
              <ul v-if="openOrders.length > 0" class="open-orders-list">
                <li v-for="order in openOrders" :key="order.id" class="open-order-row">
                  <span>{{ order.companyName }} · {{ order.side }} · {{ order.filledQuantity }} / {{ order.quantity }} @ {{ new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', maximumFractionDigits: 4 }).format(order.limitPrice) }}</span>
                  <button class="btn btn-secondary btn-sm" :disabled="orderLoading" @click="cancelLimitOrder(order.id)">{{ t('stockExchange.cancelOrder') }}</button>
                </li>
              </ul>
              <p v-else class="market-note">{{ t('stockExchange.noOpenOrders') }}</p>
            </div>
            <div>
              <h3>{{ t('stockExchange.matchedTradesTitle') }}</h3>
              <ul v-if="tradeHistory.length > 0" class="open-orders-list">
                <li v-for="trade in tradeHistory" :key="trade.id" class="open-order-row">
                  <span>{{ trade.quantity }} @ {{ new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', maximumFractionDigits: 4 }).format(trade.price) }}</span>
                  <span>{{ t('stockExchange.tick') }} {{ trade.executedAtTick }}</span>
                </li>
              </ul>
              <p v-else class="market-note">{{ t('stockExchange.noTradeHistory') }}</p>
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
  <StockTakeoverDialog
    v-model="takeoverDialogOpen"
    :company-name="takeoverTargetCompanyName"
    :takeover-loading="takeoverLoading"
    :takeover-error="takeoverError"
    :takeover-success="takeoverSuccess"
    @confirm="executeTakeover"
    @close="closeTakeoverDialog"
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

.filter-select-label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  color: var(--color-text-secondary);
  font-size: 0.78rem;
  font-weight: 600;
}

.filter-select {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-background);
  color: var(--color-text);
  padding: 0.55rem 0.85rem;
  font-size: 0.9rem;
  min-width: 180px;
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

.order-grid {
  display: grid;
  grid-template-columns: minmax(280px, 360px) 1fr;
  gap: 1rem;
}

.order-form {
  display: grid;
  gap: 0.75rem;
  align-content: start;
}

.order-book-tables {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.book-side-title {
  margin: 0 0 0.35rem;
  font-size: 0.85rem;
}

.book-side-buy {
  color: var(--color-success);
}

.book-side-sell {
  color: var(--color-danger);
}

.order-book-table td,
.order-book-table th {
  white-space: normal;
}

.book-row-buy {
  background: color-mix(in srgb, var(--color-success) 10%, transparent);
}

.book-row-sell {
  background: color-mix(in srgb, var(--color-danger) 10%, transparent);
}

.order-lists {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.open-orders-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  gap: 0.5rem;
}

.open-order-row {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 0.6rem 0.75rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.order-success {
  color: var(--color-success);
  margin: 0;
}

.order-error {
  color: var(--color-danger);
  margin: 0;
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
  .order-grid {
    grid-template-columns: 1fr;
  }
  .order-book-tables,
  .order-lists {
    grid-template-columns: 1fr;
  }
}
</style>
