<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { formatMoney, formatCompactMoney } from '@/lib/currencyFormat'
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'

const { t, locale } = useI18n()
const auth = useAuthStore()
const { isAuthenticated, player } = storeToRefs(auth)

// ── Local types (enriched with nested city/company from GraphQL) ──────────────

interface MarketCity {
  id: string
  name: string
  currencyCode: string
  countryCode?: string
}

interface MarketCompany {
  id: string
  name: string
  player?: { displayName: string }
}

interface MarketBuilding {
  id: string
  name: string
  type: string
  level: number
  isForSale: boolean
  askingPrice: number | null
  city: MarketCity
  company: MarketCompany
}

interface MarketOffer {
  id: string
  offeredPrice: number
  status: 'PENDING' | 'ACCEPTED' | 'REJECTED'
  negotiationNote: string | null
  createdAtUtc: string
  resolvedAtUtc: string | null
  buyerPlayer: { displayName: string }
  buyerCompany: { id: string; name: string }
}

interface BuildingMarketListing {
  building: MarketBuilding
  pendingOfferCount: number
}

interface BuildingMarketMyListing {
  building: MarketBuilding
  offers: MarketOffer[]
}

interface FilterCity {
  id: string
  name: string
}

// ── State ────────────────────────────────────────────────────────────────────

const activeTab = ref<'market' | 'myListings'>('market')
const loading = ref(false)
const error = ref<string | null>(null)
const actionError = ref<string | null>(null)
const actionSuccess = ref<string | null>(null)

const marketListings = ref<BuildingMarketListing[]>([])
const myListings = ref<BuildingMarketMyListing[]>([])
const filterCities = ref<FilterCity[]>([])

const filterCityId = ref('')
const filterType = ref('')
const filterMaxPrice = ref<number | null>(null)

const offerTargetListing = ref<BuildingMarketListing | null>(null)
const offerAmount = ref<number | null>(null)
const offerNote = ref('')
const offerBuyerCompanyId = ref('')
const offerSubmitting = ref(false)

// ── GraphQL ───────────────────────────────────────────────────────────────────

const BUILDING_MARKET_QUERY = `
  query BuildingMarket($cityId: UUID, $buildingType: String, $maxPrice: Decimal) {
    buildingMarket(cityId: $cityId, buildingType: $buildingType, maxPrice: $maxPrice) {
      pendingOfferCount
      building {
        id name type isForSale askingPrice level
        city { id name currencyCode countryCode }
        company { id name player { displayName } }
      }
    }
  }
`

const MY_BUILDING_LISTINGS_QUERY = `
  query {
    myBuildingListings {
      building {
        id name type isForSale askingPrice level
        city { id name currencyCode }
        company { id name }
      }
      offers {
        id offeredPrice status negotiationNote createdAtUtc resolvedAtUtc
        buyerPlayer { displayName }
        buyerCompany { id name }
      }
    }
  }
`

const CITIES_QUERY = `query { cities { id name } }`

const MAKE_OFFER_MUTATION = `
  mutation MakeOffer($input: MakeOfferOnBuildingInput!) {
    makeOfferOnBuilding(input: $input) {
      id offeredPrice status
    }
  }
`

const ACCEPT_OFFER_MUTATION = `
  mutation AcceptOffer($input: AcceptBuildingOfferInput!) {
    acceptBuildingOffer(input: $input) {
      building { id name companyId isForSale }
      offer { id status }
    }
  }
`

const REJECT_OFFER_MUTATION = `
  mutation RejectOffer($input: RejectBuildingOfferInput!) {
    rejectBuildingOffer(input: $input) {
      id status
    }
  }
`

// ── Data loading ──────────────────────────────────────────────────────────────

const loadMarket = async () => {
  loading.value = true
  error.value = null
  try {
    const vars: Record<string, unknown> = {}
    if (filterCityId.value) vars.cityId = filterCityId.value
    if (filterType.value) vars.buildingType = filterType.value
    if (filterMaxPrice.value !== null) vars.maxPrice = filterMaxPrice.value
    const data = await gqlRequest<{ buildingMarket: BuildingMarketListing[] }>(
      BUILDING_MARKET_QUERY,
      vars,
    )
    marketListings.value = data.buildingMarket
  } catch {
    error.value = t('buildingMarket.loadFailed')
  } finally {
    loading.value = false
  }
}

const loadMyListings = async () => {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ myBuildingListings: BuildingMarketMyListing[] }>(
      MY_BUILDING_LISTINGS_QUERY,
    )
    myListings.value = data.myBuildingListings
  } catch {
    error.value = t('buildingMarket.loadFailed')
  } finally {
    loading.value = false
  }
}

const loadCities = async () => {
  try {
    const data = await gqlRequest<{ cities: FilterCity[] }>(CITIES_QUERY)
    filterCities.value = data.cities
  } catch {
    // non-critical
  }
}

onMounted(async () => {
  await Promise.all([loadMarket(), loadCities()])
})

// ── Tab switching ─────────────────────────────────────────────────────────────

const switchTab = async (tab: 'market' | 'myListings') => {
  activeTab.value = tab
  actionError.value = null
  actionSuccess.value = null
  if (tab === 'myListings') {
    await loadMyListings()
  } else {
    await loadMarket()
  }
}

// ── Filters ────────────────────────────────────────────────────────────────────

const applyFilters = () => loadMarket()

const clearFilters = () => {
  filterCityId.value = ''
  filterType.value = ''
  filterMaxPrice.value = null
  loadMarket()
}

// ── Offer flow ─────────────────────────────────────────────────────────────────

const openOfferModal = (listing: BuildingMarketListing) => {
  offerTargetListing.value = listing
  offerAmount.value = listing.building.askingPrice
  offerNote.value = ''
  const firstCompany = player.value?.companies?.[0]
  offerBuyerCompanyId.value = firstCompany?.id ?? ''
  actionError.value = null
}

const closeOfferModal = () => {
  offerTargetListing.value = null
}

const submitOffer = async () => {
  if (!offerTargetListing.value || !offerAmount.value || !offerBuyerCompanyId.value) return
  offerSubmitting.value = true
  actionError.value = null
  try {
    await gqlRequest<{ makeOfferOnBuilding: { id: string; status: string } }>(MAKE_OFFER_MUTATION, {
      input: {
        buildingId: offerTargetListing.value.building.id,
        buyerCompanyId: offerBuyerCompanyId.value,
        offeredPrice: offerAmount.value,
        negotiationNote: offerNote.value || null,
      },
    })
    actionSuccess.value = t('buildingMarket.offerSubmitted')
    closeOfferModal()
    await loadMarket()
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : String(err)
    actionError.value = msg.includes('INSUFFICIENT_FUNDS')
      ? t('buildingMarket.insufficientFunds')
      : msg
  } finally {
    offerSubmitting.value = false
  }
}

const acceptOffer = async (offer: MarketOffer) => {
  actionError.value = null
  actionSuccess.value = null
  try {
    await gqlRequest<{ acceptBuildingOffer: { building: { id: string }; offer: { id: string } } }>(
      ACCEPT_OFFER_MUTATION,
      { input: { offerId: offer.id } },
    )
    actionSuccess.value = t('buildingMarket.offerAccepted')
    await loadMyListings()
  } catch (err: unknown) {
    actionError.value = err instanceof Error ? err.message : String(err)
  }
}

const rejectOffer = async (offer: MarketOffer) => {
  actionError.value = null
  actionSuccess.value = null
  try {
    await gqlRequest<{ rejectBuildingOffer: { id: string; status: string } }>(REJECT_OFFER_MUTATION, {
      input: { offerId: offer.id },
    })
    actionSuccess.value = t('buildingMarket.offerRejected')
    await loadMyListings()
  } catch (err: unknown) {
    actionError.value = err instanceof Error ? err.message : String(err)
  }
}

// ── Static helpers ────────────────────────────────────────────────────────────

const buildingTypes = [
  'MINE',
  'FACTORY',
  'SALES_SHOP',
  'RESEARCH_DEVELOPMENT',
  'APARTMENT',
  'COMMERCIAL',
  'MEDIA_HOUSE',
  'BANK',
  'EXCHANGE',
  'POWER_PLANT',
]

const offerBuyerCompanies = computed(() => player.value?.companies ?? [])

// Auto-select first company when auth loads after modal is opened
watch(offerBuyerCompanies, (companies) => {
  if (offerTargetListing.value && !offerBuyerCompanyId.value && companies.length > 0) {
    const first = companies[0]
    if (first) offerBuyerCompanyId.value = first.id
  }
})
</script>

<template>
  <div class="building-market-view">
    <div class="page-header">
      <h1 class="page-title">{{ t('buildingMarket.title') }}</h1>
      <p class="page-subtitle">{{ t('buildingMarket.subtitle') }}</p>
    </div>

    <!-- Tabs -->
    <div class="tabs" role="tablist">
      <button
        class="tab-btn"
        :class="{ active: activeTab === 'market' }"
        role="tab"
        :aria-selected="activeTab === 'market'"
        @click="switchTab('market')"
      >
        {{ t('buildingMarket.tabMarket') }}
      </button>
      <button
        v-if="isAuthenticated"
        class="tab-btn"
        :class="{ active: activeTab === 'myListings' }"
        role="tab"
        :aria-selected="activeTab === 'myListings'"
        @click="switchTab('myListings')"
      >
        {{ t('buildingMarket.tabMyListings') }}
      </button>
    </div>

    <div v-if="actionSuccess" class="alert alert-success" role="status">{{ actionSuccess }}</div>
    <div v-if="actionError && !offerTargetListing" class="alert alert-error" role="alert">{{ actionError }}</div>

    <!-- ── Market tab ── -->
    <template v-if="activeTab === 'market'">
      <div class="filters-bar">
        <select v-model="filterCityId" class="filter-select" :aria-label="t('buildingMarket.filterCity')">
          <option value="">{{ t('buildingMarket.filterCity') }}</option>
          <option v-for="city in filterCities" :key="city.id" :value="city.id">{{ city.name }}</option>
        </select>
        <select v-model="filterType" class="filter-select" :aria-label="t('buildingMarket.filterType')">
          <option value="">{{ t('buildingMarket.filterType') }}</option>
          <option v-for="bt in buildingTypes" :key="bt" :value="bt">{{ t(`buildings.types.${bt}`) }}</option>
        </select>
        <input
          v-model.number="filterMaxPrice"
          type="number"
          class="filter-input"
          :placeholder="t('buildingMarket.filterMaxPrice')"
          min="0"
        />
        <button class="btn btn-primary" @click="applyFilters">{{ t('common.all') }}</button>
        <button class="btn btn-secondary" @click="clearFilters">{{ t('common.clearFilter') }}</button>
      </div>

      <UiStateLoading v-if="loading" :label="t('common.loading')" />
      <UiStateError
        v-else-if="error"
        :message="error"
        :retry-label="t('common.retry')"
        @retry="loadMarket"
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
            @click="openOfferModal(item)"
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
        @retry="loadMyListings"
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
                <button class="btn btn-primary btn-sm" @click="acceptOffer(offer)">
                  {{ t('buildingMarket.acceptOffer') }}
                </button>
                <button class="btn btn-secondary btn-sm" @click="rejectOffer(offer)">
                  {{ t('buildingMarket.rejectOffer') }}
                </button>
              </div>
            </div>
          </div>
        </article>
      </div>
    </template>

    <!-- ── Offer modal ── -->
    <div v-if="offerTargetListing" class="modal-overlay" @click.self="closeOfferModal">
      <div class="modal-panel" role="dialog" :aria-label="t('buildingMarket.makeOffer')">
        <h2 class="modal-title">{{ t('buildingMarket.makeOffer') }}</h2>
        <p class="modal-building-name">{{ offerTargetListing.building.name }}</p>
        <p class="offer-tip">{{ t('buildingMarket.offerTip') }}</p>

        <label class="form-label" for="offerAmount">{{ t('buildingMarket.offerAmount') }}</label>
        <input
          id="offerAmount"
          v-model.number="offerAmount"
          type="number"
          class="form-input"
          :placeholder="t('buildingMarket.offerAmountPlaceholder')"
          min="1"
        />

        <label class="form-label" for="buyerCompany">{{ t('buildingMarket.buyerCompany') }}</label>
        <select id="buyerCompany" v-model="offerBuyerCompanyId" class="form-input">
          <option v-for="co in offerBuyerCompanies" :key="co.id" :value="co.id">{{ co.name }}</option>
        </select>

        <label class="form-label" for="offerNote">{{ t('buildingMarket.offerNote') }}</label>
        <textarea
          id="offerNote"
          v-model="offerNote"
          class="form-input"
          :placeholder="t('buildingMarket.offerNotePlaceholder')"
          rows="3"
        />

        <div v-if="actionError" class="alert alert-error">{{ actionError }}</div>

        <div class="modal-actions">
          <button
            class="btn btn-primary"
            :disabled="offerSubmitting || !offerAmount || !offerBuyerCompanyId"
            @click="submitOffer"
          >
            {{ offerSubmitting ? t('common.saving') : t('buildingMarket.submitOffer') }}
          </button>
          <button class="btn btn-secondary" @click="closeOfferModal">
            {{ t('buildingMarket.cancelOffer') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.building-market-view {
  padding: var(--spacing-6, 1.5rem) var(--spacing-4, 1rem);
  max-width: 1200px;
  margin: 0 auto;
}

.page-header {
  margin-bottom: var(--spacing-6, 1.5rem);
}

.page-title {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 0.25rem;
}

.page-subtitle {
  color: var(--color-text-secondary);
  font-size: 0.95rem;
}

.tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
  border-bottom: 1px solid var(--color-border);
}

.tab-btn {
  padding: 0.5rem 1.25rem;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  color: var(--color-text-secondary);
  transition: color 0.15s, border-color 0.15s;
}

.tab-btn.active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}

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

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-panel {
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 2rem;
  max-width: 480px;
  width: 90%;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.modal-title {
  font-size: 1.2rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.modal-building-name {
  color: var(--color-text-secondary);
  font-size: 0.95rem;
  margin: 0;
}

.offer-tip {
  background: var(--color-info-bg, #eff6ff);
  color: var(--color-info, #1d4ed8);
  border-radius: 6px;
  padding: 0.5rem 0.75rem;
  font-size: 0.85rem;
  margin: 0;
}

.form-label {
  font-weight: 500;
  font-size: 0.9rem;
  color: var(--color-text-secondary);
}

.form-input {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg);
  color: var(--color-text-primary);
  font-size: 0.95rem;
  box-sizing: border-box;
}

.modal-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.5rem;
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
