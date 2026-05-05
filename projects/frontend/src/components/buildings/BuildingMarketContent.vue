<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { formatMoney, formatCompactMoney } from '@/lib/currencyFormat'
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import BuildingMarketOfferModal from '@/components/buildings/BuildingMarketOfferModal.vue'
import type {
  BuildingMarketListing,
  BuildingMarketMyListing,
  FilterCity,
  MarketOffer,
} from '@/types/buildingMarket'

const { t } = useI18n()

interface BuyerCompany {
  id: string
  name: string
}

const props = defineProps<{
  activeTab: 'market' | 'myListings'
  loading: boolean
  error: string | null
  actionError: string | null
  actionSuccess: string | null
  marketListings: BuildingMarketListing[]
  myListings: BuildingMarketMyListing[]
  filterCities: FilterCity[]
  filterCityId: string
  filterType: string
  filterMaxPrice: number | null
  offerTargetListing: BuildingMarketListing | null
  offerAmount: number | null
  offerNote: string
  offerBuyerCompanyId: string
  offerBuyerCompanies: BuyerCompany[]
  offerSubmitting: boolean
  buildingTypes: string[]
  locale: string
  isAuthenticated: boolean
}>()

const emit = defineEmits<{
  'update:filterCityId': [value: string]
  'update:filterType': [value: string]
  'update:filterMaxPrice': [value: number | null]
  'update:offerAmount': [value: number | null]
  'update:offerNote': [value: string]
  'update:offerBuyerCompanyId': [value: string]
  'apply-filters': []
  'clear-filters': []
  'open-offer-modal': [listing: BuildingMarketListing]
  'close-offer-modal': []
  'submit-offer': []
  'accept-offer': [offer: MarketOffer]
  'reject-offer': [offer: MarketOffer]
  'load-market': []
  'load-my-listings': []
}>()
</script>

<template>
  <div class="building-market-content">
    <div v-if="actionSuccess" class="alert alert-success" role="status">{{ actionSuccess }}</div>
    <div v-if="actionError && !offerTargetListing" class="alert alert-error" role="alert">{{ actionError }}</div>

    <!-- ── Market tab ── -->
    <template v-if="activeTab === 'market'">
      <div class="filters-bar">
        <select
          :value="filterCityId"
          class="filter-select"
          :aria-label="t('buildingMarket.filterCity')"
          @change="emit('update:filterCityId', ($event.target as HTMLSelectElement).value)"
        >
          <option value="">{{ t('buildingMarket.filterCity') }}</option>
          <option v-for="city in filterCities" :key="city.id" :value="city.id">{{ city.name }}</option>
        </select>
        <select
          :value="filterType"
          class="filter-select"
          :aria-label="t('buildingMarket.filterType')"
          @change="emit('update:filterType', ($event.target as HTMLSelectElement).value)"
        >
          <option value="">{{ t('buildingMarket.filterType') }}</option>
          <option v-for="bt in buildingTypes" :key="bt" :value="bt">{{ t(`buildings.types.${bt}`) }}</option>
        </select>
        <input
          :value="filterMaxPrice"
          type="number"
          class="filter-input"
          :placeholder="t('buildingMarket.filterMaxPrice')"
          min="0"
          @input="emit('update:filterMaxPrice', ($event.target as HTMLInputElement).valueAsNumber || null)"
        />
        <button class="btn btn-primary" @click="emit('apply-filters')">{{ t('common.all') }}</button>
        <button class="btn btn-secondary" @click="emit('clear-filters')">{{ t('common.clearFilter') }}</button>
      </div>

      <UiStateLoading v-if="loading" :label="t('common.loading')" />
      <UiStateError
        v-else-if="error"
        :message="error"
        :retry-label="t('common.retry')"
        @retry="emit('load-market')"
      />
      <div v-else-if="marketListings.length === 0" class="empty-state">
        <p>{{ t('buildingMarket.noListings') }}</p>
      </div>
      <div v-else class="market-listings-grid">
        <article
          v-for="item in marketListings"
          :key="item.building.id"
          class="market-listing-card"
        >
          <div class="listing-header">
            <span class="building-type-badge">{{ t(`buildings.types.${item.building.type}`) }}</span>
            <span class="for-sale-badge">{{ t('buildingMarket.forSaleBadge') }}</span>
          </div>
          <h3 class="building-name">{{ item.building.name }}</h3>
          <dl class="listing-details">
            <dt>{{ t('buildingMarket.city') }}</dt>
            <dd>{{ item.building.city.name }}</dd>
            <dt>{{ t('buildingMarket.seller') }}</dt>
            <dd>{{ item.building.company.name }}</dd>
            <dt>{{ t('buildingMarket.askingPrice') }}</dt>
            <dd class="asking-price">
              {{ formatMoney(item.building.askingPrice ?? 0, item.building.city.currencyCode, locale) }}
            </dd>
            <dt>{{ t('buildingMarket.pendingOffers') }}</dt>
            <dd>{{ item.pendingOfferCount }}</dd>
          </dl>
          <button
            v-if="isAuthenticated"
            class="btn btn-primary make-offer-btn"
            @click="emit('open-offer-modal', item)"
          >
            {{ t('buildingMarket.makeOffer') }}
          </button>
        </article>
      </div>
    </template>

    <!-- ── My Listings tab ── -->
    <template v-if="activeTab === 'myListings' && isAuthenticated">
      <UiStateLoading v-if="loading" :label="t('common.loading')" />
      <UiStateError
        v-else-if="error"
        :message="error"
        :retry-label="t('common.retry')"
        @retry="emit('load-my-listings')"
      />
      <div v-else-if="myListings.length === 0" class="empty-state">
        <p>{{ t('buildingMarket.noMyListings') }}</p>
      </div>
      <div v-else class="my-listings">
        <article
          v-for="item in myListings"
          :key="item.building.id"
          class="my-listing-card"
        >
          <div class="listing-header">
            <span class="building-type-badge">{{ t(`buildings.types.${item.building.type}`) }}</span>
          </div>
          <h3 class="building-name">{{ item.building.name }}</h3>
          <dl class="listing-details">
            <dt>{{ t('buildingMarket.city') }}</dt>
            <dd>{{ item.building.city.name }}</dd>
            <dt>{{ t('buildingMarket.askingPrice') }}</dt>
            <dd class="asking-price">
              {{ formatMoney(item.building.askingPrice ?? 0, item.building.city.currencyCode, locale) }}
            </dd>
          </dl>

          <div class="offers-section">
            <h4 class="offers-title">{{ t('buildingMarket.offers') }}</h4>
            <p v-if="item.offers.length === 0" class="no-offers-hint">{{ t('buildingMarket.noOffers') }}</p>
            <div
              v-for="offer in item.offers"
              :key="offer.id"
              class="offer-row"
              :class="`offer-${offer.status.toLowerCase()}`"
            >
              <div class="offer-buyer">{{ offer.buyerCompany.name }}</div>
              <div class="offer-price">
                {{ formatCompactMoney(offer.offeredPrice, item.building.city.currencyCode, locale) }}
              </div>
              <div class="offer-status">{{ t(`buildingMarket.offerStatus.${offer.status}`) }}</div>
              <div v-if="offer.negotiationNote" class="offer-note">{{ offer.negotiationNote }}</div>
              <div v-if="offer.status === 'PENDING'" class="offer-actions">
                <button class="btn btn-primary btn-sm" @click="emit('accept-offer', offer)">
                  {{ t('buildingMarket.acceptOffer') }}
                </button>
                <button class="btn btn-secondary btn-sm" @click="emit('reject-offer', offer)">
                  {{ t('buildingMarket.rejectOffer') }}
                </button>
              </div>
            </div>
          </div>
        </article>
      </div>
    </template>

    <!-- ── Offer modal ── -->
    <BuildingMarketOfferModal
      v-if="offerTargetListing"
      :building-name="offerTargetListing.building.name"
      :offer-amount="offerAmount"
      :offer-note="offerNote"
      :offer-buyer-company-id="offerBuyerCompanyId"
      :offer-buyer-companies="offerBuyerCompanies"
      :offer-submitting="offerSubmitting"
      :action-error="actionError"
      @update:offer-amount="emit('update:offerAmount', $event)"
      @update:offer-note="emit('update:offerNote', $event)"
      @update:offer-buyer-company-id="emit('update:offerBuyerCompanyId', $event)"
      @close="emit('close-offer-modal')"
      @submit="emit('submit-offer')"
    />
  </div>
</template>

<style scoped>
.filters-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1.5rem;
  align-items: center;
}

.filter-select,
.filter-input {
  padding: 0.4rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-card);
  color: var(--color-text-primary);
  font-size: 0.9rem;
  min-width: 150px;
}

.market-listings-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.25rem;
}

.market-listing-card,
.my-listing-card {
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.listing-header {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex-wrap: wrap;
}

.building-type-badge {
  background: var(--color-bg-subtle);
  color: var(--color-text-secondary);
  border-radius: 4px;
  padding: 0.15rem 0.5rem;
  font-size: 0.8rem;
  font-weight: 600;
}

.for-sale-badge {
  background: var(--color-success, #16a34a);
  color: #fff;
  border-radius: 4px;
  padding: 0.15rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
}

.building-name {
  font-size: 1.05rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.listing-details {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 0.25rem 0.75rem;
  font-size: 0.9rem;
  margin: 0;
}

.listing-details dt {
  color: var(--color-text-secondary);
  font-weight: 500;
}

.asking-price {
  font-weight: 700;
  color: var(--color-primary);
}

.make-offer-btn {
  margin-top: auto;
  align-self: flex-start;
}

.my-listings {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.offers-section {
  border-top: 1px solid var(--color-border);
  padding-top: 0.75rem;
}

.offers-title {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin-bottom: 0.5rem;
}

.no-offers-hint {
  color: var(--color-text-muted);
  font-size: 0.85rem;
}

.offer-row {
  display: grid;
  grid-template-columns: 1fr auto auto auto;
  gap: 0.5rem;
  align-items: center;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--color-border-subtle);
  font-size: 0.9rem;
}

.offer-buyer {
  font-weight: 500;
}

.offer-price {
  font-weight: 700;
  color: var(--color-primary);
}

.offer-status {
  padding: 0.1rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 600;
}

.offer-pending .offer-status {
  background: var(--color-warning-bg, #fef9c3);
  color: var(--color-warning, #854d0e);
}

.offer-accepted .offer-status {
  background: var(--color-success-bg, #dcfce7);
  color: var(--color-success, #16a34a);
}

.offer-rejected .offer-status {
  background: var(--color-error-bg, #fee2e2);
  color: var(--color-error, #dc2626);
}

.offer-note {
  grid-column: 1 / -1;
  color: var(--color-text-muted);
  font-size: 0.85rem;
  font-style: italic;
}

.offer-actions {
  display: flex;
  gap: 0.5rem;
  grid-column: 1 / -1;
}

.empty-state {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-muted);
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-size: 0.9rem;
  margin-bottom: 0.5rem;
}

.alert-success {
  background: var(--color-success-bg, #dcfce7);
  color: var(--color-success, #16a34a);
}

.alert-error {
  background: var(--color-error-bg, #fee2e2);
  color: var(--color-error, #dc2626);
}

.btn {
  padding: 0.5rem 1.25rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 600;
  transition: opacity 0.15s;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-primary {
  background: var(--color-primary);
  color: #fff;
}

.btn-secondary {
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border);
}

.btn-sm {
  padding: 0.25rem 0.75rem;
  font-size: 0.8rem;
}
</style>
