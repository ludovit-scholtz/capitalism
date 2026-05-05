<script setup lang="ts">
import StockMarketListingRow from '@/components/stock/StockMarketListingRow.vue'
import StockMergeDialog from '@/components/stock/StockMergeDialog.vue'
import StockPersonPortfolio from '@/components/stock/StockPersonPortfolio.vue'
import StockSummaryCards from '@/components/stock/StockSummaryCards.vue'
import { useStockExchange } from '@/composables/useStockExchange'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const { currentTick, loading, error, personAccount, portfolioValue, recentDividendTotal, locale, filterText, filteredAndSortedListings, paginatedListings, totalPages, currentPage, toggleSort, sortIcon, isControlledCompany, actionLoadingKey, activeTradeAccountName, activeTradeAccountType, activeTradeAccountCash, activeSettlementAccounts, selectedSettlementBankAccountId, quantityByCompany, errorByCompany, successByCompany, expandedCompany, priceHistoryByCompany, priceHistoryLoadingByCompany, priceHistoryErrorByCompany, shareholdersByCompany, shareholdersLoadingByCompany, shareholdersErrorByCompany, mergeDialogOpen, mergeDestinationCompanyId, controlledCompanies, mergeLoading, mergeError, mergeSuccess, updateQuantity, estimatedBuyCost, estimatedSellProceeds, toggleTradePanel, switchToCompanyAccount, executeTrade, openMergeDialog, closeMergeDialog, executeMerge, loadData } = useStockExchange()
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
