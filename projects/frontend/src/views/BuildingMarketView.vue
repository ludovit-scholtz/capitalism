<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { GraphQLError } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import BuildingMarketContent from '@/components/buildings/BuildingMarketContent.vue'
import type {
  BuildingMarketListing,
  BuildingMarketMyListing,
  FilterCity,
  MarketOffer,
} from '@/types/buildingMarket'

const { t, locale } = useI18n()
const auth = useAuthStore()
const { isAuthenticated, player } = storeToRefs(auth)

// ── State ────────────────────────────────────────────────────────────────────

const activeTab = ref<'market' | 'myListings'>('market')
const loading = ref(false)
const error = ref<string | null>(null)
const actionError = ref<string | null>(null)
const actionSuccess = ref<string | null>(null)
const offerActionPendingId = ref<string | null>(null)

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
        id name type isForSale askingPrice level isCollateralized foreclosureTicksRemaining
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
        id name type isForSale askingPrice level isCollateralized foreclosureTicksRemaining
        city { id name currencyCode }
        company { id name }
      }
      offers {
        id offerVersion offeredPrice status negotiationNote createdAtUtc resolvedAtUtc
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

const CANCEL_OFFER_MUTATION = `
  mutation CancelOffer($input: CancelBuildingOfferInput!) {
    cancelBuildingOffer(input: $input) {
      id status
    }
  }
`

function isOfferVersionConflict(err: unknown): boolean {
  const msg = err instanceof Error ? err.message : String(err)
  return msg.includes('OFFER_VERSION_CONFLICT') || msg.toLowerCase().includes('offer version conflict')
}

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
  if (offerActionPendingId.value) return
  offerActionPendingId.value = offer.id
  actionError.value = null
  actionSuccess.value = null
  try {
    await gqlRequest<{ acceptBuildingOffer: { building: { id: string }; offer: { id: string } } }>(
      ACCEPT_OFFER_MUTATION,
      { input: { offerId: offer.id, offerVersion: offer.offerVersion } },
    )
    actionSuccess.value = t('buildingMarket.offerAccepted')
    await loadMyListings()
  } catch (err: unknown) {
    if (isOfferVersionConflict(err)) {
      actionError.value = t('buildingMarket.offerConflictRefreshing')
      await Promise.all([loadMyListings(), loadMarket()])
      setTimeout(() => {
        if (actionError.value === t('buildingMarket.offerConflictRefreshing')) {
          actionError.value = null
        }
      }, 4000)
    } else if (err instanceof GraphQLError && err.code === 'BUILDING_LOCKED_AS_COLLATERAL') {
      actionError.value = t('buildingDetail.collateralLockedToast')
      await Promise.all([loadMyListings(), loadMarket()])
    } else if (err instanceof GraphQLError && err.code === 'COLLATERAL_OWNERSHIP_CONFLICT') {
      actionError.value = t('buildingDetail.collateralOwnershipConflictToast')
      await Promise.all([loadMyListings(), loadMarket()])
    } else {
      actionError.value = err instanceof Error ? err.message : String(err)
    }
  } finally {
    offerActionPendingId.value = null
  }
}

const rejectOffer = async (offer: MarketOffer) => {
  if (offerActionPendingId.value) return
  offerActionPendingId.value = offer.id
  actionError.value = null
  actionSuccess.value = null
  try {
    await gqlRequest<{ cancelBuildingOffer: { id: string; status: string } }>(CANCEL_OFFER_MUTATION, {
      input: { offerId: offer.id, offerVersion: offer.offerVersion },
    })
    actionSuccess.value = t('buildingMarket.offerRejected')
    await loadMyListings()
  } catch (err: unknown) {
    if (isOfferVersionConflict(err)) {
      actionError.value = t('buildingMarket.offerConflictRefreshing')
      await Promise.all([loadMyListings(), loadMarket()])
      setTimeout(() => {
        if (actionError.value === t('buildingMarket.offerConflictRefreshing')) {
          actionError.value = null
        }
      }, 4000)
    } else {
      actionError.value = err instanceof Error ? err.message : String(err)
    }
  } finally {
    offerActionPendingId.value = null
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

    <BuildingMarketContent
      :active-tab="activeTab"
      :loading="loading"
      :error="error"
      :action-error="actionError"
      :action-success="actionSuccess"
      :market-listings="marketListings"
      :my-listings="myListings"
      :filter-cities="filterCities"
      :filter-city-id="filterCityId"
      :filter-type="filterType"
      :filter-max-price="filterMaxPrice"
      :offer-target-listing="offerTargetListing"
      :offer-amount="offerAmount"
      :offer-note="offerNote"
      :offer-buyer-company-id="offerBuyerCompanyId"
      :offer-buyer-companies="offerBuyerCompanies"
      :offer-submitting="offerSubmitting"
      :offer-action-pending-id="offerActionPendingId"
      :building-types="buildingTypes"
      :locale="locale"
      :is-authenticated="isAuthenticated"
      @update:filter-city-id="filterCityId = $event"
      @update:filter-type="filterType = $event"
      @update:filter-max-price="filterMaxPrice = $event"
      @update:offer-amount="offerAmount = $event"
      @update:offer-note="offerNote = $event"
      @update:offer-buyer-company-id="offerBuyerCompanyId = $event"
      @apply-filters="applyFilters"
      @clear-filters="clearFilters"
      @open-offer-modal="openOfferModal"
      @close-offer-modal="closeOfferModal"
      @submit-offer="submitOffer"
      @accept-offer="acceptOffer"
      @reject-offer="rejectOffer"
      @load-market="loadMarket"
      @load-my-listings="loadMyListings"
    />
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
</style>
