<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { gqlRequest as gqlMasterRequest } from '@/lib/graphqlMasterServer'
import { computeSimulatedProfit, trackOnboardingEvent } from '@/lib/onboardingAnalytics'
import { getLocalizedProductDescription, getLocalizedProductName, getLocalizedRecipeIngredientName, getLocalizedResourceName, getProductImageUrl } from '@/lib/catalogPresentation'
import { onCatalogImageError } from '@/lib/catalogImageFallback'
import { formatGameTickTime } from '@/lib/gameTime'
import { useTickRefresh } from '@/composables/useTickRefresh'
import {
  canProceedStep3 as checkCanProceedStep3,
  canProceedStep4 as checkCanProceedStep4,
  clampStep,
  getAvailableLots,
  getDefaultRecommendedLotId,
  getMaxReachableStep,
  getRecommendedFactoryLotIds,
  getRecommendedShopLotIds,
  keyToStep,
  stepToKey,
} from '@/lib/onboardingHelpers'
import OnboardingLotSelector from '@/components/onboarding/OnboardingLotSelector.vue'
import OnboardingProgressTracker from '@/components/onboarding/OnboardingProgressTracker.vue'
import OnboardingUnitChain from '@/components/onboarding/OnboardingUnitChain.vue'
import { useAuthStore } from '@/stores/auth'
import { useReferralStore } from '@/stores/referral'
import { useTickCountdown } from '@/composables/useTickCountdown'
import { formatMoney } from '@/lib/currencyFormat'
import { generateOnboardingCompanyName, resetNameSession } from '@/lib/onboardingCompanyName'
import { generatePersonalAccountName, type PlayerGender } from '@/lib/personalAccountName'
import { resolveMasterWebUrl } from '@/lib/runtimeGraphqlUrl'
import type { BuildingLot, City, EurFxRate, FirstSaleMission, GameState, OnboardingResult, OnboardingStartResult, ProductType } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const referralStore = useReferralStore()
referralStore.initFromStorage()
auth.initFromStorage()
const masterPortalUrl = resolveMasterWebUrl(import.meta.env.VITE_MASTER_WEB_URL)

const hasAuthenticatedSession = computed(() => auth.isAuthenticated || !!auth.player)
const activeReferralCode = computed(() => (hasAuthenticatedSession.value ? null : referralStore.pendingCode))

const PERSONAL_STARTING_CASH = 200_000
const FOUNDER_CONTRIBUTION = 200_000
const DEFAULT_IPO_RAISE_TARGET = 200_000

const ipoOptions = [
  {
    raiseTarget: 200_000,
    founderOwnershipRatio: 0.5,
    titleKey: 'onboarding.ipoOptionStarterTitle',
    descriptionKey: 'onboarding.ipoOptionStarterDesc',
  },
  {
    raiseTarget: 400_000,
    founderOwnershipRatio: 0.3333,
    titleKey: 'onboarding.ipoOptionGrowthTitle',
    descriptionKey: 'onboarding.ipoOptionGrowthDesc',
  },
  {
    raiseTarget: 600_000,
    founderOwnershipRatio: 0.25,
    titleKey: 'onboarding.ipoOptionExpansionTitle',
    descriptionKey: 'onboarding.ipoOptionExpansionDesc',
  },
] as const

const CITIES_QUERY = `
  {
    cities {
      id
      name
      countryCode
      currencyCode
      latitude
      longitude
      population
      resources {
        resourceType {
          id
          name
          slug
          category
          basePrice
          weightPerUnit
          unitName
          unitSymbol
          imageUrl
          description
        }
        abundance
      }
    }
  }
`

const LOTS_QUERY = `
  query CityLots($cityId: UUID!) {
    cityLots(cityId: $cityId) {
      id
      cityId
      name
      description
      district
      latitude
      longitude
      populationIndex
      basePrice
      price
      suitableTypes
      ownerCompanyId
      buildingId
      ownerCompany { id name }
      building { id name type }
    }
  }
`

const PRODUCTS_QUERY = `
  query Products($industry: String) {
    productTypes(industry: $industry) {
      id
      name
      slug
      industry
      basePrice
      baseCraftTicks
      outputQuantity
      energyConsumptionMwh
      unitName
      unitSymbol
      isProOnly
      isUnlockedForCurrentPlayer
      description
      recipes {
        quantity
        resourceType {
          id
          name
          slug
          category
          basePrice
          weightPerUnit
          unitName
          unitSymbol
          imageUrl
          description
        }
        inputProductType {
          id
          name
          slug
          unitName
          unitSymbol
        }
      }
    }
  }
`

const starterProductSlugByIndustry: Record<string, string[]> = {
  FURNITURE: ['wooden-chair', 'wooden-table', 'wooden-bed'],
  FOOD_PROCESSING: ['bread', 'pasta', 'crackers'],
  HEALTHCARE: ['basic-medicine', 'bandages', 'first-aid-kit'],
  ELECTRONICS: ['basic-electronics', 'led-screen', 'circuit-board'],
  CONSTRUCTION: ['residential-block', 'commercial-block', 'industrial-block'],
  PHARMACEUTICALS: ['aspirin', 'vitamin-capsule', 'antibiotic'],
  ENERGY: ['coal-briquette', 'heating-oil', 'industrial-fuel'],
  LOGISTICS: ['shipping-bag', 'storage-sack', 'cargo-pack'],
}

const step = ref(1)
const loading = ref(false)
const error = ref<string | null>(null)
const factoryActionError = ref<string | null>(null)
const onboardingCompanyCash = ref<number | null>(null)
const milestoneLoading = ref(false)
const milestoneError = ref<string | null>(null)
const milestoneCompleted = ref(false)

// Guest mode state
const isGuestMode = computed(() => !hasAuthenticatedSession.value)
const guestSaveError = ref<string | null>(null)
const guestSaveLoading = ref(false)
const resettingOnboarding = ref(false)
const resetStatusMessage = ref<string | null>(null)

const industries = ref<string[]>([])
const proOnlyIndustries = ref<string[]>([])
const cities = ref<City[]>([])
const eurFxRates = ref<EurFxRate[]>([])
const products = ref<ProductType[]>([])
const cityLots = ref<BuildingLot[]>([])

// Guests see only free industries; authenticated users see all (pro-only show a badge and are gated at click time)
const visibleIndustries = computed(() => industries.value.filter((industry) => !proOnlyIndustries.value.includes(industry) || hasAuthenticatedSession.value))

/** FX rate for the currently selected city: units of city currency per 1 EUR. Defaults to 1 (EUR). */
const cityFxRate = computed<number>(() => {
  const code = selectedCity.value?.currencyCode ?? 'EUR'
  if (code === 'EUR') return 1
  const rate = eurFxRates.value.find((r) => r.currencyCode === code)
  return rate?.rate ?? 1
})

/** FX rate for USD conversion: units of city currency per 1 USD. */
const cityUsdFxRate = computed<number>(() => {
  const targetCode = selectedCity.value?.currencyCode ?? 'USD'
  if (targetCode === 'USD') return 1

  const eurToTarget = getFxRateForCurrency(targetCode)
  const eurToUsd = getFxRateForCurrency('USD')
  if (!eurToUsd || eurToUsd <= 0) return 1

  return eurToTarget / eurToUsd
})

const selectedIndustry = ref('')
const selectedCityId = ref('')
const selectedProductId = ref('')
const selectedFactoryLotId = ref('')
const selectedShopLotId = ref('')
const selectedIpoRaiseTarget = ref<number | null>(null)
const isRouteStateReady = ref(false)

const completionResult = ref<OnboardingResult | null>(null)
const gameState = ref<GameState | null>(null)
const firstSaleMission = ref<FirstSaleMission | null>(null)
const firstSaleMissionLoading = ref(false)

const { tickCountdown, startTickCountdown, stopTickCountdown } = useTickCountdown(gameState)

const selectedCity = computed(() => cities.value.find((city) => city.id === selectedCityId.value) ?? null)
const selectedProduct = computed(() => products.value.find((product) => product.id === selectedProductId.value) ?? null)
const selectedFactoryLot = computed(() => cityLots.value.find((lot) => lot.id === selectedFactoryLotId.value) ?? null)
const selectedShopLot = computed(() => cityLots.value.find((lot) => lot.id === selectedShopLotId.value) ?? null)

/** The current company name is generated automatically and can be changed later in the game. */
const companyName = ref('')
const personalAccountName = ref('')
const selectedPersonalGender = ref<PlayerGender>('UNSPECIFIED')

/** Derives a fresh suggested name from the current industry. */
function updateGeneratedCompanyName() {
  companyName.value = generateOnboardingCompanyName(selectedIndustry.value)
}

function refreshSuggestedPersonalAccountName() {
  const currentAlias = personalAccountName.value.trim()
  if (currentAlias && !currentAlias.includes('@')) {
    personalAccountName.value = currentAlias
    return
  }

  const currentGender = auth.player?.gender as PlayerGender | undefined
  if (currentGender === 'FEMALE' || currentGender === 'MALE' || currentGender === 'UNSPECIFIED') {
    selectedPersonalGender.value = currentGender
  }

  const existingAlias = (auth.player?.personalAccountName ?? auth.player?.displayName ?? '').trim()
  if (existingAlias && !existingAlias.includes('@')) {
    personalAccountName.value = existingAlias
    return
  }

  personalAccountName.value = generatePersonalAccountName(selectedPersonalGender.value)
}

// Auto-refresh the suggested name whenever industry or city changes so the
// first suggestion always reflects the player's current choices.
watch([selectedIndustry, selectedCity], ([newIndustry, newCity]) => {
  resetNameSession(`${newIndustry}:${newCity?.name ?? ''}`)
  updateGeneratedCompanyName()
  refreshSuggestedPersonalAccountName()
})
const starterCompany = computed(() => {
  const companyId = auth.player?.onboardingCompanyId
  if (!companyId) {
    return auth.player?.companies.find((company) => company.name === companyName.value) ?? null
  }

  return auth.player?.companies.find((company) => company.id === companyId) ?? null
})
const hasResettableOnboardingState = computed(() => {
  const player = auth.player
  if (!hasAuthenticatedSession.value || !player || player.onboardingCompletedAtUtc) {
    return false
  }

  return Boolean(
    player.onboardingCurrentStep ||
    player.onboardingIndustry ||
    player.onboardingCityId ||
    player.onboardingCompanyId ||
    player.onboardingFactoryLotId ||
    player.onboardingShopBuildingId ||
    player.companies.length > 0,
  )
})
const selectedIpoOption = computed(() => ipoOptions.find((option) => option.raiseTarget === selectedIpoRaiseTarget.value) ?? ipoOptions[0])
/** Company starting cash in the selected city's local currency (USD founder + USD IPO raise). */
const companyStartingCash = computed(() => Math.round((FOUNDER_CONTRIBUTION + selectedIpoOption.value.raiseTarget) * cityUsdFxRate.value))
const remainingPersonalCash = computed(() => PERSONAL_STARTING_CASH - FOUNDER_CONTRIBUTION)
const hasGuestFactoryPurchaseInRoute = computed(() => isGuestMode.value && (route.query.step === 'shop' || route.query.step === 'complete'))
const effectiveOnboardingCompanyCash = computed(() => {
  if (onboardingCompanyCash.value !== null) return onboardingCompanyCash.value
  if (hasGuestFactoryPurchaseInRoute.value && selectedFactoryLot.value) {
    return Math.max(companyStartingCash.value - selectedFactoryLot.value.price, 0)
  }
  return null
})
const starterCash = computed(() => effectiveOnboardingCompanyCash.value ?? starterCompany.value?.cash ?? companyStartingCash.value)

const availableFactoryLots = computed(() => getAvailableLots(cityLots.value, 'FACTORY'))
const availableShopLots = computed(() => getAvailableLots(cityLots.value, 'SALES_SHOP'))
const recommendedFactoryLotIds = computed(() => getRecommendedFactoryLotIds(availableFactoryLots.value))
const recommendedShopLotIds = computed(() => getRecommendedShopLotIds(availableShopLots.value, 2, selectedFactoryLot.value))

const sortedProducts = computed(() => {
  const prods = [...products.value]
  if (selectedProductId.value) {
    const selected = prods.find((p) => p.id === selectedProductId.value)
    if (selected) {
      prods.splice(prods.indexOf(selected), 1)
      prods.unshift(selected)
    }
  }
  return prods
})

const canProceedStep3 = computed(() => checkCanProceedStep3(selectedFactoryLot.value, companyStartingCash.value))
const canProceedStep4 = computed(() => checkCanProceedStep4(selectedProductId.value, selectedShopLot.value, starterCash.value))
const canShowStep4Summary = computed(() => !!selectedProduct.value && !!selectedFactoryLot.value && !!selectedShopLot.value)

/**
 * True when the player has completed the lot flow but has not yet completed
 * the first-sale milestone. In this state the configure-guide step (step 5)
 * should be shown even after a page refresh.
 */
const isResumingConfigureStep = computed(() => !!auth.player?.onboardingCompletedAtUtc && !auth.player.onboardingFirstSaleCompletedAtUtc && !!auth.player.onboardingShopBuildingId)

/** Building ID for the "Configure My Sales Shop" CTA. Works both in-session and after resume. */
const shopBuildingId = computed(() => completionResult.value?.salesShop.id ?? auth.player?.onboardingShopBuildingId ?? null)

/** Cash balance to show in the configure-guide panel. Works in-session and after resume. */
const configureGuideCash = computed(() => {
  if (completionResult.value) return completionResult.value.company.cash
  return auth.player?.companies[0]?.cash ?? 0
})

/**
 * Currency code to use for all monetary display on the completion step (step 7).
 * Prefers the explicit `cityCurrencyCode` from the backend's `OnboardingResult`, which is
 * populated when `finishOnboarding` or `completeOnboarding` is called in this session.
 * Falls back to `selectedCity.value?.currencyCode` (set during the wizard flow) and finally
 * to 'EUR' so the display always has a valid currency even in the resume-after-reload case.
 */
const completionCurrencyCode = computed<string>(() => completionResult.value?.cityCurrencyCode ?? selectedCity.value?.currencyCode ?? 'EUR')

/**
 * The auto-configured public sale price for the guest's shop.
 * The backend sets MinPrice = local basePrice × 1.5 for the PUBLIC_SALES unit during FinishOnboarding.
 */
const guestConfiguredShopPrice = computed(() => {
  const base = selectedProduct.value ? getProductLocalPrice(selectedProduct.value) : null
  if (!base) return null
  return Math.round(base * 1.5 * 100) / 100
})

/** Simulated first-tick profit for the guest completion preview. */
const simulatedProfit = computed(() => {
  if (!selectedProduct.value) return null
  const recipeCost = selectedProduct.value.recipes.reduce((sum, r) => {
    const unitCost = (r.resourceType?.basePrice ?? 0) * cityFxRate.value
    return sum + unitCost * r.quantity
  }, 0)
  return computeSimulatedProfit(getProductLocalPrice(selectedProduct.value), selectedProduct.value.outputQuantity, recipeCost > 0 ? recipeCost : undefined)
})

function findResumedShopBasePrice(): number | null {
  const resumedShop = auth.player?.companies.flatMap((company) => company.buildings).find((building) => building.id === shopBuildingId.value)
  const publicSalesUnit = resumedShop?.units.find((unit) => unit.unitType === 'PUBLIC_SALES')
  return publicSalesUnit?.minPrice ?? null
}

const configureGuideBasePrice = computed(() => {
  if (completionResult.value?.selectedProduct.basePrice) {
    return getProductLocalPrice(completionResult.value.selectedProduct)
  }

  if (selectedProduct.value?.basePrice) {
    return getProductLocalPrice(selectedProduct.value)
  }

  return findResumedShopBasePrice()
})

/** Unit type icon mapping for the factory layout display. */
const unitTypeIcons: Record<string, string> = {
  PURCHASE: '🛒',
  MANUFACTURING: '⚙️',
  STORAGE: '📦',
  B2B_SALES: '🤝',
  PUBLIC_SALES: '🛍️',
  MINING: '⛏️',
  BRANDING: '🏷️',
  MARKETING: '📣',
}

/**
 * Returns the factory units from completionResult sorted by gridX position,
 * ready for display as a production chain.
 */
const completionFactoryUnits = computed(() => {
  const units = completionResult.value?.factory?.units
  if (!units || units.length === 0) return null
  return [...units].sort((a, b) => a.gridX - b.gridX || a.gridY - b.gridY)
})

/**
 * Returns the sales shop units from completionResult sorted by gridX position.
 */
const completionShopUnits = computed(() => {
  const units = completionResult.value?.salesShop?.units
  if (!units || units.length === 0) return null
  return [...units].sort((a, b) => a.gridX - b.gridX || a.gridY - b.gridY)
})

/**
 * Returns a static guest factory layout showing what will be configured on save.
 * Matches the ConfigureStarterFactory backend output: PURCHASE -> MANUFACTURING -> STORAGE -> B2B_SALES.
 */
const guestFactoryLayout = computed(() => {
  if (!isGuestMode.value || step.value !== 7) return null
  return [
    { unitType: 'PURCHASE', gridX: 0 },
    { unitType: 'MANUFACTURING', gridX: 1 },
    { unitType: 'STORAGE', gridX: 2 },
    { unitType: 'B2B_SALES', gridX: 3 },
  ]
})

/**
 * Returns a static guest shop layout showing what will be configured on save.
 * Matches the AddStarterShop backend output: PURCHASE -> PUBLIC_SALES.
 */
const guestShopLayout = computed(() => {
  if (!isGuestMode.value || step.value !== 7) return null
  return [
    { unitType: 'PURCHASE', gridX: 0 },
    { unitType: 'PUBLIC_SALES', gridX: 1 },
  ]
})

const industryIcons: Record<string, string> = {
  FURNITURE: '🪑',
  FOOD_PROCESSING: '🍞',
  HEALTHCARE: '💊',
  ELECTRONICS: '💻',
  CONSTRUCTION: '🏗️',
}

/** Maps each starter industry to its i18n description key. */
const industryDescKeys: Record<string, string> = {
  FURNITURE: 'onboarding.industryDescFurniture',
  FOOD_PROCESSING: 'onboarding.industryDescFoodProcessing',
  HEALTHCARE: 'onboarding.industryDescHealthcare',
  ELECTRONICS: 'onboarding.industryDescElectronics',
  CONSTRUCTION: 'onboarding.industryDescConstruction',
  PHARMACEUTICALS: 'onboarding.industryDescPharmaceuticals',
  ENERGY: 'onboarding.industryDescEnergy',
  LOGISTICS: 'onboarding.industryDescLogistics',
}

/** Maps each starter industry to its i18n first-product hint key. */
const industryFirstProductKeys: Record<string, string> = {
  FURNITURE: 'onboarding.industryFirstProductFurniture',
  FOOD_PROCESSING: 'onboarding.industryFirstProductFoodProcessing',
  HEALTHCARE: 'onboarding.industryFirstProductHealthcare',
  ELECTRONICS: 'onboarding.industryFirstProductElectronics',
  CONSTRUCTION: 'onboarding.industryFirstProductConstruction',
  PHARMACEUTICALS: 'onboarding.industryFirstProductPharmaceuticals',
  ENERGY: 'onboarding.industryFirstProductEnergy',
  LOGISTICS: 'onboarding.industryFirstProductLogistics',
}

/** Maps each starter industry to its i18n "why choose" tag key. */
const industryWhyKeys: Record<string, string> = {
  FURNITURE: 'onboarding.industryWhyFurniture',
  FOOD_PROCESSING: 'onboarding.industryWhyFoodProcessing',
  HEALTHCARE: 'onboarding.industryWhyHealthcare',
  ELECTRONICS: 'onboarding.industryWhyElectronics',
  CONSTRUCTION: 'onboarding.industryWhyConstruction',
  PHARMACEUTICALS: 'onboarding.industryWhyPharmaceuticals',
  ENERGY: 'onboarding.industryWhyEnergy',
  LOGISTICS: 'onboarding.industryWhyLogistics',
}

function resolveMaxReachableStep(): number {
  if (isGuestMode.value) {
    const guestCompletedAllSteps = !!selectedFactoryLotId.value && effectiveOnboardingCompanyCash.value !== null && !!selectedShopLotId.value && !!selectedProductId.value
    if (guestCompletedAllSteps) return 7
    if (selectedFactoryLotId.value && effectiveOnboardingCompanyCash.value !== null) return 6
    return getMaxReachableStep({
      hasCompletionResult: false,
      isResumingConfigureStep: false,
      onboardingCurrentStep: null,
      hasLocalFactoryProgress: false,
      selectedIndustry: selectedIndustry.value,
      selectedProductId: selectedProductId.value,
      selectedCityId: selectedCityId.value,
      hasSelectedIpoPlan: selectedIpoRaiseTarget.value !== null,
    })
  }

  return getMaxReachableStep({
    hasCompletionResult: !!completionResult.value,
    isResumingConfigureStep: isResumingConfigureStep.value,
    onboardingCurrentStep: auth.player?.onboardingCurrentStep,
    hasLocalFactoryProgress: !!selectedFactoryLotId.value && onboardingCompanyCash.value !== null,
    selectedIndustry: selectedIndustry.value,
    selectedProductId: selectedProductId.value,
    selectedCityId: selectedCityId.value,
    hasSelectedIpoPlan: selectedIpoRaiseTarget.value !== null,
  })
}

function resolveClampStep(requestedStep: number): number {
  return clampStep(requestedStep, resolveMaxReachableStep())
}

function parseIpoRaiseTarget(value: unknown): number | null {
  const parsed = Number(value)
  return [200000, 400000, 600000].includes(parsed) ? parsed : null
}

function applyRouteSelections() {
  selectedIndustry.value = typeof route.query.industry === 'string' ? route.query.industry : ''
  selectedProductId.value = typeof route.query.productId === 'string' ? route.query.productId : ''
  selectedCityId.value = typeof route.query.cityId === 'string' ? route.query.cityId : ''
  selectedFactoryLotId.value = typeof route.query.factoryLotId === 'string' ? route.query.factoryLotId : ''
  selectedShopLotId.value = typeof route.query.shopLotId === 'string' ? route.query.shopLotId : ''
  personalAccountName.value = typeof route.query.personalAccountName === 'string' ? route.query.personalAccountName : ''
  selectedIpoRaiseTarget.value = parseIpoRaiseTarget(route.query.ipoRaiseTarget)
}

function resetOnboardingSelections() {
  selectedIndustry.value = ''
  selectedProductId.value = ''
  selectedCityId.value = ''
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  selectedIpoRaiseTarget.value = null
  onboardingCompanyCash.value = null
  completionResult.value = null
  cityLots.value = []
  products.value = []
  step.value = 1
}

function buildRouteQuery() {
  const query: Record<string, string> = {
    step: stepToKey(step.value),
  }

  if (selectedIndustry.value) query.industry = selectedIndustry.value
  if (selectedProductId.value) query.productId = selectedProductId.value
  if (selectedCityId.value) query.cityId = selectedCityId.value
  if (selectedIpoRaiseTarget.value !== null) query.ipoRaiseTarget = String(selectedIpoRaiseTarget.value)
  if (selectedFactoryLotId.value) query.factoryLotId = selectedFactoryLotId.value
  if (selectedShopLotId.value) query.shopLotId = selectedShopLotId.value
  if (personalAccountName.value.trim()) query.personalAccountName = personalAccountName.value.trim()

  return query
}

function isRouteQuerySynced(nextQuery: Record<string, string>): boolean {
  return Object.entries(nextQuery).every(([key, value]) => route.query[key] === value) && Object.keys(route.query).every((key) => nextQuery[key] !== undefined)
}

watch([step, selectedIndustry, selectedProductId, selectedCityId, selectedIpoRaiseTarget, selectedFactoryLotId, selectedShopLotId, personalAccountName], async () => {
  if (factoryActionError.value) {
    factoryActionError.value = null
  }

  if (!isRouteStateReady.value) {
    return
  }

  const nextQuery = buildRouteQuery()
  if (!isRouteQuerySynced(nextQuery)) {
    await router.replace({ query: nextQuery })
  }
})

watch([step, availableFactoryLots, recommendedFactoryLotIds, companyStartingCash], () => {
  if (step.value !== 5 || selectedFactoryLotId.value) return
  selectedFactoryLotId.value = getDefaultRecommendedLotId(availableFactoryLots.value, recommendedFactoryLotIds.value, companyStartingCash.value)
})

watch([step, availableShopLots, recommendedShopLotIds, starterCash], () => {
  if (step.value !== 6 || selectedShopLotId.value) return
  selectedShopLotId.value = getDefaultRecommendedLotId(availableShopLots.value, recommendedShopLotIds.value, starterCash.value)
})

async function loadProducts() {
  if (!selectedIndustry.value) return

  loading.value = true
  try {
    const data = await gqlRequest<{ productTypes: ProductType[] }>(PRODUCTS_QUERY, {
      industry: selectedIndustry.value,
    })
    const allowedSlugs = starterProductSlugByIndustry[selectedIndustry.value] ?? []
    products.value = data.productTypes.filter((product) => allowedSlugs.includes(product.slug))
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load products'
  } finally {
    loading.value = false
  }
}

async function loadLots() {
  if (!selectedCityId.value) {
    cityLots.value = []
    return
  }

  loading.value = true
  try {
    const data = await gqlRequest<{ cityLots: BuildingLot[] }>(LOTS_QUERY, { cityId: selectedCityId.value })
    cityLots.value = data.cityLots
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load city lots'
  } finally {
    loading.value = false
  }
}

async function syncOngoingOnboardingState() {
  if (!auth.player) {
    return
  }

  if (auth.player.onboardingCurrentStep !== 'SHOP_SELECTION') {
    onboardingCompanyCash.value = null
    return
  }

  selectedIndustry.value = auth.player.onboardingIndustry ?? selectedIndustry.value
  selectedCityId.value = auth.player.onboardingCityId ?? selectedCityId.value
  selectedFactoryLotId.value = auth.player.onboardingFactoryLotId ?? selectedFactoryLotId.value
  selectedIpoRaiseTarget.value ??= DEFAULT_IPO_RAISE_TARGET

  const ongoingCompany = auth.player.companies.find((company) => company.id === auth.player?.onboardingCompanyId)
  if (ongoingCompany) {
    onboardingCompanyCash.value = ongoingCompany.cash
  }

  step.value = 6
}

function hasPendingGuestProgress() {
  return !!(selectedIndustry.value && selectedCityId.value && selectedFactoryLotId.value && selectedProductId.value && selectedShopLotId.value)
}

async function migrateGuestProgressToAuthenticated() {
  if (!hasPendingGuestProgress()) {
    step.value = 1
    return
  }

  try {
    loading.value = true
    error.value = null
    await persistPersonalAccountNameIfNeeded()

    const startResult = await gqlRequest<{ startOnboardingCompany: OnboardingStartResult }>(
      `mutation StartOnboardingCompany($input: StartOnboardingCompanyInput!) {
        startOnboardingCompany(input: $input) {
          nextStep
          company { id name cash }
          factory { id name type }
          factoryLot {
            id cityId name description district latitude longitude populationIndex basePrice price suitableTypes
            ownerCompanyId buildingId
            ownerCompany { id name }
            building { id name type }
          }
        }
      }`,
      {
        input: {
          industry: selectedIndustry.value,
          cityId: selectedCityId.value,
          ipoRaiseTarget: selectedIpoRaiseTarget.value ?? DEFAULT_IPO_RAISE_TARGET,
          companyName: companyName.value,
          factoryLotId: selectedFactoryLotId.value,
        },
      },
    )

    onboardingCompanyCash.value = startResult.startOnboardingCompany.company.cash
    selectedFactoryLotId.value = startResult.startOnboardingCompany.factoryLot.id
    await auth.fetchMe()

    const finishResult = await gqlRequest<{ finishOnboarding: OnboardingResult }>(
      `mutation FinishOnboarding($input: FinishOnboardingInput!) {
        finishOnboarding(input: $input) {
          company { id name cash }
          factory { id name type units { id unitType gridX gridY level linkRight } }
          salesShop { id name type units { id unitType gridX gridY level linkRight } }
          selectedProduct { name industry basePrice }
          cityCurrencyCode
        }
      }`,
      {
        input: {
          productTypeId: selectedProductId.value,
          shopLotId: selectedShopLotId.value,
        },
      },
    )

    completionResult.value = finishResult.finishOnboarding
    onboardingCompanyCash.value = finishResult.finishOnboarding.company.cash
    await auth.fetchMe()
    step.value = 7
    trackOnboardingEvent('onboarding_converted', {
      industry: selectedIndustry.value,
      cityId: selectedCityId.value,
      authMode: 'biatec_oidc',
    })
  } catch (migrationErr: unknown) {
    const code = migrationErr instanceof GraphQLError ? migrationErr.code : undefined
    if (code === 'LOT_ALREADY_OWNED') {
      onboardingCompanyCash.value = null
      completionResult.value = null
      selectedFactoryLotId.value = ''
      selectedShopLotId.value = ''
      await loadLots()
      step.value = 1
      error.value = t('onboarding.guestMigrationRetry')
    } else {
      error.value = migrationErr instanceof Error ? migrationErr.message : t('onboarding.guestMigrationGenericError')
    }
  } finally {
    loading.value = false
    guestSaveLoading.value = false
  }
}

onMounted(async () => {
  if (!auth.player) {
    await auth.fetchMe()
  }

  trackOnboardingEvent('onboarding_start', { authenticated: hasAuthenticatedSession.value })

  if (hasAuthenticatedSession.value) {
    if (auth.player?.onboardingFirstSaleCompletedAtUtc) {
      router.push('/dashboard')
      return
    }

    if (auth.player?.onboardingCompletedAtUtc && !auth.player.onboardingShopBuildingId) {
      router.push('/dashboard')
      return
    }

    if (isResumingConfigureStep.value) {
      // Load cities and FX rates in the resume path so that formatCurrency() can use the
      // player's onboarding city currency instead of defaulting to EUR.  The city is derived
      // from the player's shop building, which is recorded as onboardingShopBuildingId.
      const [citiesData, fxRatesData] = await Promise.all([gqlRequest<{ cities: City[] }>(CITIES_QUERY), gqlRequest<{ eurFxRates: EurFxRate[] }>('{ eurFxRates { currencyCode rate } }')])
      await Promise.all([loadGameState(), loadFirstSaleMission()])

      cities.value = citiesData.cities
      eurFxRates.value = fxRatesData.eurFxRates

      // Derive the player's onboarding city from the shop building that was created during
      // onboarding (onboardingShopBuildingId -> building.cityId -> city for correct currency).
      const shopBuilding = auth.player?.companies.flatMap((company) => company.buildings).find((building) => building.id === auth.player?.onboardingShopBuildingId)
      if (shopBuilding?.cityId) {
        selectedCityId.value = shopBuilding.cityId
        auth.switchCity(shopBuilding.cityId)
      }

      step.value = 7
      isRouteStateReady.value = true
      return
    }
  }

  try {
    loading.value = true
    const [industriesData, citiesData, fxRatesData] = await Promise.all([
      gqlRequest<{ starterIndustries: { industries: string[]; proOnlyIndustries: string[] } }>('{ starterIndustries { industries proOnlyIndustries } }'),
      gqlRequest<{ cities: City[] }>(CITIES_QUERY),
      gqlRequest<{ eurFxRates: EurFxRate[] }>('{ eurFxRates { currencyCode rate } }'),
    ])

    industries.value = industriesData.starterIndustries.industries
    proOnlyIndustries.value = industriesData.starterIndustries.proOnlyIndustries
    cities.value = citiesData.cities
    eurFxRates.value = fxRatesData.eurFxRates

    applyRouteSelections()
    ensureSelectedIndustryIsVisible()

    if (selectedIndustry.value) {
      await loadProducts()
      if (!products.value.some((product) => product.id === selectedProductId.value)) {
        selectedProductId.value = ''
      }
    }

    if (selectedCityId.value) {
      auth.switchCity(selectedCityId.value)
      await loadLots()
      if (!cityLots.value.some((lot) => lot.id === selectedFactoryLotId.value)) {
        selectedFactoryLotId.value = ''
      }
      if (!cityLots.value.some((lot) => lot.id === selectedShopLotId.value)) {
        selectedShopLotId.value = ''
      }
    }

    refreshSuggestedPersonalAccountName()

    if (hasAuthenticatedSession.value) {
      await syncOngoingOnboardingState()

      if (!auth.player?.onboardingCurrentStep && !auth.player?.onboardingCompletedAtUtc && !auth.player?.onboardingFirstSaleCompletedAtUtc && hasPendingGuestProgress()) {
        guestSaveLoading.value = true
        await migrateGuestProgressToAuthenticated()
      }
    }

    step.value = resolveClampStep(keyToStep(route.query.step))
    isRouteStateReady.value = true
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load data'
  } finally {
    loading.value = false
  }
})

async function selectIndustry(industry: string) {
  error.value = null

  // Block authenticated non-Pro users from selecting Pro-only industries.
  if (proOnlyIndustries.value.includes(industry) && hasAuthenticatedSession.value && !auth.isProSubscriber) {
    error.value = t('onboarding.proRequiredError')
    return
  }

  selectedIndustry.value = industry
  selectedProductId.value = ''
  selectedIpoRaiseTarget.value = null
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null
  await loadProducts()
  step.value = 3
  trackOnboardingEvent('industry_selected', { industry })
}

function selectProduct(productId: string) {
  error.value = null
  selectedProductId.value = productId
  selectedIpoRaiseTarget.value = null
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null
  step.value = 4
  trackOnboardingEvent('product_selected', { productId, industry: selectedIndustry.value })
}

async function selectCity(cityId: string) {
  error.value = null
  resetStatusMessage.value = null
  selectedCityId.value = cityId
  auth.switchCity(cityId)
  selectedIpoRaiseTarget.value = null
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null
  await loadLots()
  step.value = 2
  trackOnboardingEvent('city_selected', { cityId })
}

function selectIpoPlan(raiseTarget: number) {
  error.value = null
  selectedIpoRaiseTarget.value = raiseTarget
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null
  step.value = 5
  trackOnboardingEvent('ipo_selected', { raiseTarget })
}

function prevStep() {
  if (step.value > 1 && auth.player?.onboardingCurrentStep !== 'SHOP_SELECTION') {
    step.value--
  }
}

function resolveFactoryStartValidationError(): string | null {
  if (!personalAccountName.value.trim()) {
    return t('onboarding.personalAccountNameRequired')
  }

  if (personalAccountName.value.trim().length > 40) {
    return t('onboarding.personalAccountNameTooLong')
  }

  if (!selectedFactoryLotId.value) {
    return t('onboarding.selectFactoryLotToContinue')
  }

  if (!selectedFactoryLot.value) {
    return t('onboarding.lotUnavailableBody')
  }

  if (selectedFactoryLot.value.ownerCompanyId) {
    return t('onboarding.lotUnavailableBody')
  }

  if (companyStartingCash.value < selectedFactoryLot.value.price) {
    return t('onboarding.needsMoreCash')
  }

  return null
}

function reportFactoryStartError(message: string, details?: unknown) {
  factoryActionError.value = message
  console.error('[Onboarding] Failed to buy first factory lot.', {
    message,
    selectedFactoryLotId: selectedFactoryLotId.value,
    selectedCityId: selectedCityId.value,
    selectedIndustry: selectedIndustry.value,
    companyStartingCash: companyStartingCash.value,
    selectedLotPrice: selectedFactoryLot.value?.price ?? null,
    details,
  })
}

async function persistPersonalAccountNameIfNeeded() {
  if (isGuestMode.value) {
    return
  }

  const trimmedAlias = personalAccountName.value.trim()
  const currentAlias = (auth.player?.personalAccountName ?? auth.player?.displayName ?? '').trim()
  if (!trimmedAlias || trimmedAlias === currentAlias) {
    return
  }

  await gqlMasterRequest<{ updatePersonalAccountName: { personalAccountName: string } }>(
    `mutation UpdatePersonalAccountName($input: UpdatePersonalAccountNameInput!) {
      updatePersonalAccountName(input: $input) {
        personalAccountName
        gender
      }
    }`,
    { input: { personalAccountName: trimmedAlias, gender: selectedPersonalGender.value } },
  )

  await gqlRequest<{ updateDisplayName: { displayName: string; gender: string } }>(
    `mutation UpdateDisplayName($displayName: String!, $gender: String) {
      updateDisplayName(input: { displayName: $displayName, gender: $gender }) {
        displayName
        gender
      }
    }`,
    { displayName: trimmedAlias, gender: selectedPersonalGender.value },
  )

  if (auth.player) {
    auth.player.displayName = trimmedAlias
    auth.player.personalAccountName = trimmedAlias
    auth.player.gender = selectedPersonalGender.value
  }
}

async function resetOnboardingProgress() {
  if (!hasResettableOnboardingState.value || resettingOnboarding.value) {
    return
  }

  const confirmed = window.confirm(t('onboarding.resetConfirm'))
  if (!confirmed) {
    return
  }

  resettingOnboarding.value = true
  resetStatusMessage.value = null
  error.value = null
  factoryActionError.value = null
  guestSaveError.value = null

  try {
    const result = await gqlRequest<{ resetOnboardingProgress: boolean }>(
      `mutation ResetOnboardingProgress {
        resetOnboardingProgress
      }`,
    )

    if (!result.resetOnboardingProgress) {
      throw new Error(t('common.unknownError'))
    }

    resetOnboardingSelections()
    await auth.fetchMe()
    refreshSuggestedPersonalAccountName()
    resetStatusMessage.value = t('onboarding.resetSuccess')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('common.unknownError')
  } finally {
    resettingOnboarding.value = false
  }
}

async function startOnboardingCompany() {
  factoryActionError.value = null
  resetStatusMessage.value = null

  const validationError = resolveFactoryStartValidationError()
  if (validationError) {
    reportFactoryStartError(validationError)
    return
  }

  if (!canProceedStep3.value || !selectedFactoryLot.value) {
    reportFactoryStartError(t('onboarding.lotUnavailableBody'), {
      canProceedStep3: canProceedStep3.value,
    })
    return
  }

  if (isGuestMode.value) {
    onboardingCompanyCash.value = companyStartingCash.value - selectedFactoryLot.value.price
    trackOnboardingEvent('factory_configured', {
      guest: true,
      lotId: selectedFactoryLotId.value,
      industry: selectedIndustry.value,
      cityId: selectedCityId.value,
      ipoRaiseTarget: selectedIpoRaiseTarget.value,
    })
    await loadLots()
    step.value = 6
    return
  }

  loading.value = true
  error.value = null

  try {
    await persistPersonalAccountNameIfNeeded()

    const result = await gqlRequest<{ startOnboardingCompany: OnboardingStartResult }>(
      `mutation StartOnboardingCompany($input: StartOnboardingCompanyInput!) {
        startOnboardingCompany(input: $input) {
          nextStep
          company { id name cash }
          factory { id name type }
          factoryLot {
            id cityId name description district latitude longitude populationIndex basePrice price suitableTypes
            ownerCompanyId buildingId
            ownerCompany { id name }
            building { id name type }
          }
        }
      }`,
      {
        input: {
          industry: selectedIndustry.value,
          cityId: selectedCityId.value,
          ipoRaiseTarget: selectedIpoRaiseTarget.value ?? DEFAULT_IPO_RAISE_TARGET,
          companyName: companyName.value,
          factoryLotId: selectedFactoryLotId.value,
        },
      },
    )

    onboardingCompanyCash.value = result.startOnboardingCompany.company.cash
    selectedFactoryLotId.value = result.startOnboardingCompany.factoryLot.id
    trackOnboardingEvent('factory_configured', {
      guest: false,
      lotId: selectedFactoryLotId.value,
      industry: selectedIndustry.value,
      cityId: selectedCityId.value,
      ipoRaiseTarget: selectedIpoRaiseTarget.value,
    })
    await auth.fetchMe()
    await loadLots()
    step.value = 6
  } catch (e: unknown) {
    const message = e instanceof Error ? e.message : t('onboarding.lotUnavailableBody')
    error.value = message
    reportFactoryStartError(message, e)
    await loadLots()
    if (selectedFactoryLot.value?.ownerCompanyId) {
      selectedFactoryLotId.value = ''
    }
  } finally {
    loading.value = false
  }
}

async function completeOnboarding() {
  factoryActionError.value = null

  if (isGuestMode.value) {
    onboardingCompanyCash.value = Math.max(starterCash.value - (selectedShopLot.value?.price ?? 0), 0)
    trackOnboardingEvent('shop_configured', {
      guest: true,
      lotId: selectedShopLotId.value,
      productId: selectedProductId.value,
      industry: selectedIndustry.value,
    })
    trackOnboardingEvent('save_prompt_shown', { guest: true })
    if (simulatedProfit.value) {
      trackOnboardingEvent('first_profit_shown', {
        guest: true,
        revenue: simulatedProfit.value.revenue,
        cost: simulatedProfit.value.cost,
        profit: simulatedProfit.value.profit,
        productId: selectedProductId.value,
      })
    }
    step.value = 7
    await loadGameState()
    return
  }

  loading.value = true
  error.value = null

  try {
    const result = await gqlRequest<{ finishOnboarding: OnboardingResult }>(
      `mutation FinishOnboarding($input: FinishOnboardingInput!) {
        finishOnboarding(input: $input) {
          company { id name cash }
          factory { id name type units { id unitType gridX gridY level linkRight } }
          salesShop { id name type units { id unitType gridX gridY level linkRight } }
          selectedProduct { name industry basePrice }
          cityCurrencyCode
        }
      }`,
      {
        input: {
          productTypeId: selectedProductId.value,
          shopLotId: selectedShopLotId.value,
        },
      },
    )

    completionResult.value = result.finishOnboarding
    onboardingCompanyCash.value = result.finishOnboarding.company.cash
    trackOnboardingEvent('shop_configured', {
      guest: false,
      productId: selectedProductId.value,
      industry: selectedIndustry.value,
    })
    trackOnboardingEvent('completed', { guest: false })
    await auth.fetchMe()
    step.value = 7
    await Promise.all([loadGameState(), loadFirstSaleMission()])
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('onboarding.lotUnavailableBody')
    await auth.fetchMe()
    await loadLots()
    onboardingCompanyCash.value = auth.player?.companies.find((company) => company.id === auth.player?.onboardingCompanyId)?.cash ?? onboardingCompanyCash.value
    if (selectedShopLot.value?.ownerCompanyId) {
      selectedShopLotId.value = ''
    }
  } finally {
    loading.value = false
  }
}

/**
 * Guest mode: register or login, then migrate saved choices to the real backend.
 * If the lot was taken in the meantime, restart wizard from step 1 with auth.
 */
async function saveGuestProgress() {
  guestSaveError.value = null
  guestSaveLoading.value = true

  try {
    const redirectPath = route.fullPath || '/onboarding'
    await auth.startBiatecOidcSignIn(redirectPath, { prompt: 'consent' })
  } catch (e: unknown) {
    guestSaveError.value = e instanceof Error ? e.message : t('auth.oidcCallbackFailed')
    guestSaveLoading.value = false
  }
}

function formatIndustry(industry: string): string {
  return industry.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase())
}

function getFxRateForCurrency(currencyCode = 'EUR'): number {
  if (currencyCode === 'EUR') return 1
  const rate = eurFxRates.value.find((entry) => entry.currencyCode === currencyCode)
  return rate?.rate ?? 1
}

function getProductLocalPrice(product: Pick<ProductType, 'basePrice'>, currencyCode = selectedCity.value?.currencyCode ?? 'EUR'): number {
  return Math.round(product.basePrice * getFxRateForCurrency(currencyCode) * 100) / 100
}

function getProductPriceSummary(product: ProductType): string {
  if (!selectedCity.value) return ''
  return formatCurrency(getProductLocalPrice(product, selectedCity.value.currencyCode), selectedCity.value.currencyCode)
}

function getProductName(product: ProductType): string {
  return getLocalizedProductName(product, locale.value)
}

function getProductDescription(product: ProductType): string {
  return getLocalizedProductDescription(product, locale.value)
}

function getProductImage(product: ProductType): string {
  return getProductImageUrl(product)
}

function getRecipeIngredientLabel(product: ProductType, index: number): string {
  const recipe = product.recipes[index]
  if (!recipe) return ''
  return `${recipe.quantity}× ${getLocalizedRecipeIngredientName(recipe, locale.value)}`
}

function getCityResourceName(city: City, index: number): string {
  const resource = city.resources[index]?.resourceType
  return resource ? getLocalizedResourceName(resource, locale.value) : ''
}

function formatCurrency(value: number, currencyCode?: string): string {
  const code = currencyCode ?? selectedCity.value?.currencyCode ?? 'EUR'
  return formatMoney(value, code, locale.value)
}

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`
}

/** Returns the translated label for a building unit type, falling back to the raw string. */
function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function ensureSelectedIndustryIsVisible() {
  if (!selectedIndustry.value || visibleIndustries.value.includes(selectedIndustry.value)) {
    return
  }

  selectedIndustry.value = ''
  selectedProductId.value = ''
  selectedIpoRaiseTarget.value = null
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null

  if (step.value > 2) {
    step.value = 2
  }
}

async function markMilestoneComplete() {
  milestoneError.value = null
  milestoneLoading.value = true
  try {
    await gqlRequest<{ completeFirstSaleMilestone: { onboardingFirstSaleCompletedAtUtc: string } }>(
      `mutation {
        completeFirstSaleMilestone {
          onboardingFirstSaleCompletedAtUtc
        }
      }`,
    )
    await auth.fetchMe()
    milestoneCompleted.value = true
  } catch (e: unknown) {
    if (e instanceof GraphQLError && e.code === 'FIRST_SALE_NOT_RECORDED') {
      milestoneError.value = t('onboarding.milestoneErrorFirstSaleNotRecorded')
    } else {
      milestoneError.value = (e instanceof Error ? e.message : '') || t('onboarding.milestoneError')
    }
  } finally {
    milestoneLoading.value = false
  }
}

const FIRST_SALE_MISSION_QUERY = `
  {
    firstSaleMission {
      phase
      shopBuildingId
      shopName
      blockers
      firstSaleRevenue
      firstSaleProductName
      firstSaleTick
      firstSaleQuantity
      firstSalePricePerUnit
    }
  }
`

async function loadFirstSaleMission() {
  if (!hasAuthenticatedSession.value || milestoneCompleted.value) return
  firstSaleMissionLoading.value = true
  try {
    const data = await gqlRequest<{ firstSaleMission: FirstSaleMission }>(FIRST_SALE_MISSION_QUERY)
    firstSaleMission.value = data.firstSaleMission

    // Auto-complete the milestone when the simulation has recorded a real first sale
    if (data.firstSaleMission.phase === 'FIRST_SALE_RECORDED' && !milestoneCompleted.value && !milestoneLoading.value) {
      await markMilestoneComplete()
    }
  } catch (err) {
    // Best-effort polling — log for debugging but don't surface to user
    console.error('[firstSaleMission] Failed to load mission status:', err)
  } finally {
    firstSaleMissionLoading.value = false
  }
}

/** Translates a blocker code into a human-readable explanation. */
function blockerMessage(code: string): string {
  const map: Record<string, string> = {
    BUILDING_UNDER_CONSTRUCTION: t('onboarding.missionBlockerUnderConstruction'),
    PUBLIC_SALES_UNIT_MISSING: t('onboarding.missionBlockerNoPublicSalesUnit'),
    PRICE_NOT_SET: t('onboarding.missionBlockerPriceNotSet'),
    NO_INVENTORY: t('onboarding.missionBlockerNoInventory'),
  }
  return map[code] ?? t('onboarding.missionBlockerUnknown', { code })
}

function navigateToDashboard() {
  stopTickCountdown()
  router.push('/dashboard')
}

async function loadGameState() {
  try {
    const data = await gqlRequest<{ gameState: GameState }>('{ gameState { currentTick lastTickAtUtc tickIntervalSeconds taxRate } }')
    gameState.value = data.gameState
    startTickCountdown()
  } catch {
    // ignore — tick countdown is best-effort
  }
}

onUnmounted(() => {
  stopTickCountdown()
})

useTickRefresh(async () => {
  if (step.value !== 7) {
    return
  }

  await loadGameState()

  // Poll first-sale mission for authenticated players who are still in the configure step
  if (hasAuthenticatedSession.value && !milestoneCompleted.value) {
    await loadFirstSaleMission()
  }
})

watch(visibleIndustries, () => {
  ensureSelectedIndustryIsVisible()
})
</script>

<template>
  <div class="min-h-[calc(100vh-112px)] bg-gradient-to-b from-page to-[rgba(0,71,255,0.04)] py-8 px-4 flex items-center">
    <div class="container w-full max-w-[1280px] mx-auto">
      <div v-if="step < 7" class="text-center mb-8">
        <h1 class="text-3xl font-bold mb-2 bg-gradient-to-br from-brand to-[var(--color-secondary)] bg-clip-text text-transparent">{{ t('onboarding.title') }}</h1>
        <p class="text-muted text-sm">{{ t('onboarding.subtitle') }}</p>
      </div>
      <OnboardingProgressTracker v-if="step < 7" :step="step" />
      <div v-if="error" class="bg-[rgba(248,113,113,0.1)] border border-[rgba(248,113,113,0.3)] text-bad px-4 py-3 rounded-lg text-sm mb-4" role="alert">{{ error }}</div>
      <div v-if="activeReferralCode" class="referral-onboarding-banner mb-4 rounded-lg border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-200" role="status">
        <div class="flex items-start gap-3">
          <span aria-hidden="true" class="text-base">🎁</span>
          <div>
            <p class="font-semibold text-amber-300">
              {{ t('onboarding.referralBannerTitle', { code: activeReferralCode }) }}
            </p>
            <p class="text-amber-200/80">
              {{ t('onboarding.referralBannerBody') }}
              <span class="ml-1 inline-flex h-4 w-4 items-center justify-center rounded-full border border-amber-300/60 text-[0.65rem] font-bold" :title="t('onboarding.referralBannerTooltip')"
                >i</span
              >
            </p>
          </div>
        </div>
      </div>
      <div v-if="step === 1" class="step-content bg-card border border-divider rounded-xl p-8">
        <div class="mb-4">
          <h2 class="text-xl font-semibold mb-1">{{ t('onboarding.step1Title') }}</h2>
          <p class="text-muted text-sm">{{ t('onboarding.step1Desc') }}</p>
        </div>
        <div v-if="hasResettableOnboardingState || resetStatusMessage" class="mb-6 rounded-lg border border-amber-500/30 bg-amber-500/10 p-4 text-amber-100">
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <p class="m-0 text-sm font-semibold text-amber-200">{{ t('onboarding.resetTitle') }}</p>
              <p class="m-0 mt-1 text-sm text-amber-100/80">{{ t('onboarding.resetBody') }}</p>
            </div>
            <button v-if="hasResettableOnboardingState" class="onboarding-reset-btn btn btn-secondary whitespace-nowrap" :disabled="resettingOnboarding" @click="resetOnboardingProgress">
              {{ resettingOnboarding ? t('common.loading') : t('onboarding.resetButton') }}
            </button>
          </div>
          <p v-if="resetStatusMessage" class="m-0 mt-3 text-sm text-[var(--color-secondary)]" role="status">
            {{ resetStatusMessage }}
          </p>
        </div>
        <div class="grid grid-cols-1 gap-4 mb-6 md:grid-cols-2 xl:grid-cols-3">
          <button
            v-for="city in cities"
            :key="city.id"
            class="city-card flex flex-col gap-2 rounded-md border-2 border-divider bg-page p-6 text-left text-body transition-all duration-200 hover:-translate-y-0.5 hover:border-brand hover:shadow-[0_4px_16px_rgba(0,71,255,0.1)]"
            :class="{ 'border-brand bg-brand/10 shadow-[0_0_0_1px_var(--color-primary),0_4px_16px_rgba(0,71,255,0.15)]': selectedCityId === city.id, 'pick-hint': !selectedCityId }"
            @click="selectCity(city.id)"
          >
            <div class="flex items-center gap-2">
              <span class="text-2xl">🏙️</span><span class="font-bold text-base">{{ city.name }}</span>
            </div>
            <div class="flex gap-3 text-[0.8125rem] text-muted">
              <span class="bg-card-raised px-2 py-0.5 rounded font-semibold text-xs">{{ city.countryCode }}</span
              ><span v-if="city.currencyCode" class="city-currency bg-brand px-2 py-0.5 rounded font-semibold text-xs text-white">{{ city.currencyCode }}</span
              ><span>{{ t('onboarding.population') }} {{ city.population.toLocaleString() }}</span>
            </div>
            <div class="flex flex-wrap gap-1.5 items-center">
              <span class="text-xs text-muted">{{ t('onboarding.resources') }}:</span
              ><span v-for="(resource, index) in city.resources" :key="index" class="bg-[rgba(0,200,83,0.1)] text-[var(--color-secondary)] px-2 py-0.5 rounded-full text-[0.6875rem] font-medium">
                {{ getCityResourceName(city, index) }}
              </span>
            </div>
          </button>
        </div>
      </div>
      <div v-if="step === 2" class="step-content step-content-wide bg-card border border-divider rounded-xl p-8 flex flex-col gap-6">
        <div>
          <h2 class="text-xl font-semibold mb-1">{{ t('onboarding.step2Title') }}</h2>
          <p class="text-muted text-sm">{{ t('onboarding.step2Desc') }}</p>
        </div>
        <div class="grid grid-cols-1 gap-4 mb-6 md:grid-cols-2 xl:grid-cols-3">
          <button
            v-for="ind in visibleIndustries"
            :key="ind"
            class="industry-card relative flex flex-col gap-2 rounded-md border-2 border-divider bg-page p-6 text-center text-body transition-all duration-200 hover:-translate-y-0.5 hover:border-brand hover:shadow-[0_4px_16px_rgba(0,71,255,0.1)]"
            :class="{
              'border-brand bg-brand/10 shadow-[0_0_0_1px_var(--color-primary),0_4px_16px_rgba(0,71,255,0.15)]': selectedIndustry === ind,
              'pick-hint': !selectedIndustry,
            }"
            @click="selectIndustry(ind)"
          >
            <span
              v-if="proOnlyIndustries.includes(ind)"
              class="industry-pro-badge absolute top-2 right-2 rounded bg-amber-400/20 px-1.5 py-0.5 text-[0.625rem] font-bold text-amber-400 uppercase tracking-wide leading-none"
              >{{ t('onboarding.proBadge') }}</span
            >
            <span class="text-[2.5rem] leading-none">{{ industryIcons[ind] || '🏭' }}</span
            ><span class="font-bold text-base">{{ formatIndustry(ind) }}</span
            ><span class="card-first-product inline-block rounded bg-brand/10 px-2 py-0.5 text-xs font-semibold text-brand">{{ t(industryFirstProductKeys[ind] || '') }}</span
            ><span class="card-desc text-[0.8125rem] text-muted leading-snug">{{ t(industryDescKeys[ind] || '') }}</span
            ><span class="card-why text-[0.6875rem] text-subtle italic leading-snug">{{ t(industryWhyKeys[ind] || '') }}</span>
          </button>
        </div>
        <div class="step-actions flex gap-3 justify-end mt-2">
          <button class="btn btn-secondary" @click="prevStep">← {{ t('common.back') }}</button>
        </div>
      </div>
      <div v-if="step === 3" class="step-content bg-card border border-divider rounded-xl p-8">
        <div class="mb-4">
          <h2 class="text-xl font-semibold mb-1">{{ t('onboarding.step3Title') }}</h2>
          <p class="text-muted text-sm">{{ t('onboarding.step3Desc') }}</p>
        </div>
        <div class="grid grid-cols-1 gap-4 mb-6 md:grid-cols-2 xl:grid-cols-3">
          <button
            v-for="prod in sortedProducts"
            :key="prod.id"
            class="product-card flex flex-col gap-2 rounded-md border-2 border-divider bg-page p-6 text-center text-body transition-all duration-200 hover:-translate-y-0.5 hover:border-brand hover:shadow-[0_4px_16px_rgba(0,71,255,0.1)]"
            :class="{ 'border-brand bg-brand/10 shadow-[0_0_0_1px_var(--color-primary),0_4px_16px_rgba(0,71,255,0.15)]': selectedProductId === prod.id, 'pick-hint': !selectedProductId }"
            @click="selectProduct(prod.id)"
          >
            <img :src="getProductImage(prod)" :alt="getProductName(prod)" class="w-full aspect-video object-cover rounded bg-card-raised" @error="onCatalogImageError" /><span
              class="font-bold text-base"
              >{{ getProductName(prod) }}</span
            ><span class="text-[1rem] font-bold text-[var(--color-secondary)]">{{ getProductPriceSummary(prod) }}</span
            ><span class="text-xs text-muted">{{ t('onboarding.craftTime', { ticks: prod.baseCraftTicks }) }}</span
            ><span class="text-[0.8125rem] text-muted leading-snug">{{ getProductDescription(prod) }}</span>
            <div class="flex flex-col gap-1 text-xs text-muted">
              <span class="font-medium">{{ t('onboarding.requires') }}:</span
              ><span v-for="(recipe, index) in prod.recipes" :key="index" class="text-[var(--color-tertiary)] font-medium"> {{ getRecipeIngredientLabel(prod, index) }} </span>
            </div>
          </button>
        </div>
        <div class="step-actions flex gap-3 justify-end mt-2">
          <button class="btn btn-secondary" @click="prevStep">← {{ t('common.back') }}</button>
        </div>
      </div>
      <div v-if="step === 4" class="step-content step-content-wide bg-card border border-divider rounded-xl p-8 flex flex-col gap-6">
        <div>
          <h2 class="text-xl font-semibold mb-1">{{ t('onboarding.step4Title') }}</h2>
          <p class="text-muted text-sm">{{ t('onboarding.step4Desc') }}</p>
        </div>
        <div class="budget-grid grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-4">
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider">
            <span class="text-muted text-xs">{{ t('onboarding.founderContribution') }}</span
            ><strong>{{ formatCurrency(FOUNDER_CONTRIBUTION * cityUsdFxRate) }}</strong>
          </article>
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider">
            <span class="text-muted text-xs">{{ t('onboarding.personalCash') }}</span
            ><strong>{{ formatCurrency(remainingPersonalCash * cityUsdFxRate, selectedCity?.currencyCode) }}</strong>
          </article>
        </div>
        <div class="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-4">
          <button
            v-for="option in ipoOptions"
            :key="option.raiseTarget"
            class="ipo-card flex flex-col gap-2 rounded-md border-2 border-divider bg-page px-4 py-4 text-left text-body transition-all duration-200 hover:-translate-y-0.5 hover:border-brand"
            :class="{
              'border-brand bg-brand/10 shadow-[0_0_0_1px_var(--color-primary),0_4px_16px_rgba(0,71,255,0.15)]': selectedIpoRaiseTarget === option.raiseTarget,
              'pick-hint': selectedIpoRaiseTarget === null,
            }"
            @click="selectIpoPlan(option.raiseTarget)"
          >
            <span class="font-bold text-sm">{{ t(option.titleKey) }}</span
            ><span class="text-muted text-xs">{{ t('onboarding.ipoRaise') }}: {{ formatCurrency(option.raiseTarget * cityUsdFxRate) }}</span
            ><span class="text-muted text-xs">{{ t('onboarding.ipoFounderOwnership') }}: {{ formatPercent(option.founderOwnershipRatio) }}</span
            ><span class="text-muted text-xs">{{ t('onboarding.ipoPublicFloat') }}: {{ formatPercent(1 - option.founderOwnershipRatio) }}</span
            ><span class="text-muted text-[0.8125rem] leading-snug">{{ t(option.descriptionKey) }}</span>
          </button>
        </div>
        <div class="step-actions flex gap-3 justify-end mt-2">
          <button class="btn btn-secondary" @click="prevStep">← {{ t('common.back') }}</button>
        </div>
      </div>
      <div v-if="step === 5" class="step-content step-content-wide bg-card border border-divider rounded-xl p-8 flex flex-col gap-6">
        <div>
          <h2 class="text-xl font-semibold mb-1">{{ t('onboarding.step5Title') }}</h2>
          <p class="text-muted text-sm">{{ t('onboarding.step5Desc') }}</p>
        </div>
        <div class="budget-grid grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-4">
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider">
            <span class="text-muted text-xs">{{ t('onboarding.generatedCompanyName') }}</span
            ><strong>{{ companyName }}</strong>
          </article>
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider">
            <span class="text-muted text-xs">{{ t('onboarding.personalAccountNameLabel') }}</span
            ><strong>{{ personalAccountName }}</strong>
          </article>
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider">
            <span class="text-muted text-xs">{{ t('onboarding.startingCash') }}</span
            ><strong>{{ formatCurrency(companyStartingCash) }}</strong>
          </article>
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider" :class="{ warning: !!selectedFactoryLot && companyStartingCash < selectedFactoryLot.price }">
            <span class="text-muted text-xs">{{ t('onboarding.cashAfterPurchase') }}</span
            ><strong :class="!!selectedFactoryLot && companyStartingCash < selectedFactoryLot.price ? 'text-[var(--color-tertiary)]' : ''">{{
              formatCurrency(Math.max(companyStartingCash - (selectedFactoryLot?.price ?? 0), 0))
            }}</strong>
          </article>
        </div>
        <div class="bg-card-raised border border-divider rounded-lg p-4">
          <h3 class="font-semibold text-sm mb-2">{{ t('onboarding.factoryGuideTitle') }}</h3>
          <p class="text-muted text-sm m-0">{{ t('onboarding.factoryGuideBody') }}</p>
          <ul class="mt-3 pl-4 flex flex-col gap-1.5 text-sm text-muted">
            <li>{{ t('onboarding.factoryGuideHint1') }}</li>
            <li>{{ t('onboarding.factoryGuideHint2') }}</li>
            <li>{{ t('onboarding.factoryGuideHint3') }}</li>
          </ul>
        </div>
        <p v-if="availableFactoryLots.length === 0" class="empty-state-message text-muted text-sm m-0">{{ t('onboarding.noFactoryLots') }}</p>
        <OnboardingLotSelector
          v-else
          key="factory-lot-selector"
          v-model:selected-lot-id="selectedFactoryLotId"
          :lots="availableFactoryLots"
          required-building-type="FACTORY"
          :money-available="companyStartingCash"
          :recommended-lot-ids="recommendedFactoryLotIds"
          :city="selectedCity"
        />
        <p v-if="!selectedFactoryLotId" class="text-sm text-muted m-0" role="status">
          {{ t('onboarding.selectFactoryLotToContinue') }}
        </p>
        <div class="step-actions flex gap-3 justify-end mt-2">
          <button class="btn btn-secondary" :disabled="auth.player?.onboardingCurrentStep === 'SHOP_SELECTION'" @click="prevStep">← {{ t('common.back') }}</button
          ><button
            :class="selectedFactoryLotId ? 'btn btn-primary btn-lg' : 'btn btn-secondary btn-lg opacity-75 cursor-not-allowed'"
            :disabled="!selectedFactoryLotId || !canProceedStep3 || loading"
            @click="startOnboardingCompany"
          >
            {{ loading ? t('common.loading') : t('onboarding.purchaseFactory') }} <span v-if="!loading" class="ml-1">🏭</span>
          </button>
        </div>
        <p v-if="factoryActionError" class="mt-2 text-sm text-bad text-right" role="alert">
          {{ factoryActionError }}
        </p>
      </div>
      <div v-if="step === 6" class="step-content step-content-wide bg-card border border-divider rounded-xl p-8 flex flex-col gap-6">
        <div>
          <h2 class="text-xl font-semibold mb-1">{{ t('onboarding.step6Title') }}</h2>
          <p class="text-muted text-sm">{{ t('onboarding.step6Desc') }}</p>
        </div>
        <div class="bg-card-raised border border-divider rounded-lg p-4" role="status">
          <strong>{{ t('onboarding.factoryPurchasedTitle') }}</strong
          ><span class="text-muted ml-1"> {{ t('onboarding.factoryPurchasedBody', { lot: selectedFactoryLot?.name ?? '', cash: formatCurrency(starterCash) }) }} </span>
        </div>
        <div class="budget-grid grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-4">
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider">
            <span class="text-muted text-xs">{{ t('onboarding.availableCash') }}</span
            ><strong>{{ formatCurrency(starterCash) }}</strong>
          </article>
          <article v-if="selectedProduct" class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider">
            <span class="text-muted text-xs">{{ t('onboarding.selectProduct') }}</span
            ><strong>{{ getProductName(selectedProduct) }}</strong
            ><span class="text-xs text-muted">{{ getProductPriceSummary(selectedProduct) }}</span>
          </article>
          <article class="budget-card flex flex-col gap-1.5 p-4 rounded-lg bg-page border border-divider" :class="{ warning: !!selectedShopLot && starterCash < selectedShopLot.price }">
            <span class="text-muted text-xs">{{ t('onboarding.cashAfterPurchase') }}</span
            ><strong :class="!!selectedShopLot && starterCash < selectedShopLot.price ? 'text-[var(--color-tertiary)]' : ''">{{
              formatCurrency(Math.max(starterCash - (selectedShopLot?.price ?? 0), 0))
            }}</strong>
          </article>
        </div>
        <div class="bg-card-raised border border-divider rounded-lg p-4">
          <h3 class="font-semibold text-sm mb-2">{{ t('onboarding.shopGuideTitle') }}</h3>
          <p class="text-muted text-sm m-0">{{ t('onboarding.shopGuideBody') }}</p>
        </div>
        <p v-if="availableShopLots.length === 0" class="empty-state-message text-muted text-sm m-0">{{ t('onboarding.noShopLots') }}</p>
        <OnboardingLotSelector
          v-else
          key="shop-lot-selector"
          v-model:selected-lot-id="selectedShopLotId"
          :lots="availableShopLots"
          required-building-type="SALES_SHOP"
          :money-available="starterCash"
          :recommended-lot-ids="recommendedShopLotIds"
          :city="selectedCity"
          :reference-lot="selectedFactoryLot"
          :reference-distance-label="t('onboarding.distanceFromFactory')"
        />
        <p v-if="!selectedShopLotId" class="text-sm text-muted m-0" role="status">
          {{ t('onboarding.selectShopLotToContinue') }}
        </p>
        <div v-if="canShowStep4Summary" class="summary bg-card-raised border border-divider rounded-lg p-4">
          <div class="flex items-center gap-2 mb-2">
            <span class="text-xl">📋</span>
            <h3 class="font-semibold text-base m-0">{{ t('onboarding.summary') }}</h3>
          </div>
          <p class="text-muted text-sm m-0">
            {{ t('onboarding.shopSummaryText', { company: companyName, city: selectedCity?.name ?? '', product: selectedProduct ? getProductName(selectedProduct) : '' }) }}
          </p>
          <div class="flex flex-col gap-1.5 mt-3">
            <div class="flex items-center gap-2 text-[0.8125rem] px-2 py-1.5 bg-card rounded">
              <span>🏭</span><span>{{ selectedFactoryLot?.name }} — {{ t(`cityMap.districts.${selectedFactoryLot?.district}`) }}</span>
            </div>
            <div class="flex items-center gap-2 text-[0.8125rem] px-2 py-1.5 bg-card rounded">
              <span>🏪</span><span>{{ selectedShopLot?.name }} — {{ t(`cityMap.districts.${selectedShopLot?.district}`) }}</span>
            </div>
            <div class="flex items-center gap-2 text-[0.8125rem] px-2 py-1.5 bg-card rounded">
              <span>📦</span><span>{{ selectedProduct ? getProductName(selectedProduct) : '' }}</span>
            </div>
          </div>
        </div>
        <div class="step-actions flex gap-3 justify-end mt-2">
          <button class="btn btn-secondary" :disabled="auth.player?.onboardingCurrentStep === 'SHOP_SELECTION'" @click="prevStep">← {{ t('common.back') }}</button
          ><button
            :class="selectedShopLotId ? 'btn btn-primary btn-lg' : 'btn btn-secondary btn-lg opacity-75 cursor-not-allowed'"
            :disabled="!selectedShopLotId || !canProceedStep4 || loading"
            @click="completeOnboarding"
          >
            {{ loading ? t('common.loading') : t('onboarding.purchaseShop') }} <span v-if="!loading" class="ml-1">🏪</span>
          </button>
        </div>
      </div>
      <div
        v-if="step === 7 && (completionResult || isResumingConfigureStep || isGuestMode || milestoneCompleted)"
        class="step-content completion-step bg-card border border-divider rounded-xl p-8 flex flex-col gap-6 text-center"
      >
        <div>
          <h2 class="completion-title text-[1.75rem] font-bold mb-3 bg-gradient-to-br from-[var(--color-secondary)] to-brand bg-clip-text text-transparent">
            {{ t(isGuestMode ? 'onboarding.guestCompletionTitle' : 'onboarding.completionTitle') }}
          </h2>
          <p class="text-muted mx-auto max-w-[480px] leading-relaxed">{{ t(isGuestMode ? 'onboarding.guestCompletionDesc' : 'onboarding.completionDesc') }}</p>
        </div>
        <!-- Guest mode: show simulated achievements and save-progress form -->
        <div v-if="isGuestMode" class="completion-achievements grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-4 text-left">
          <div v-if="selectedCity" class="flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">🌍</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ selectedCity.name }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionCity') }}</span>
            </div>
          </div>
          <div v-if="selectedIndustry" class="flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">🏭</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ formatIndustry(selectedIndustry) }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionIndustry') }}</span>
            </div>
          </div>
          <div class="flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">🏢</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ companyName || t('onboarding.guestCompanyPlaceholder') }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionFactory') }}</span>
            </div>
          </div>
          <div class="flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">🏪</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ selectedShopLot?.name || t('onboarding.guestShopPlaceholder') }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionShop') }}</span>
            </div>
          </div>
          <div v-if="selectedProduct" class="flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">📦</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ getProductName(selectedProduct) }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionProduct', { product: getProductName(selectedProduct) }) }}</span>
            </div>
          </div>
          <div class="flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">💰</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ formatCurrency(effectiveOnboardingCompanyCash ?? companyStartingCash) }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionCapital', { amount: formatCurrency(effectiveOnboardingCompanyCash ?? companyStartingCash) }) }}</span>
            </div>
          </div>
        </div>
        <!-- Guest factory layout preview -->
        <div v-if="isGuestMode && guestFactoryLayout" class="factory-layout-panel bg-card border border-divider rounded-xl p-5 text-left" aria-label="Factory layout">
          <h3 class="text-base font-semibold mb-1">{{ t('onboarding.factoryLayoutTitle') }}</h3>
          <p class="text-sm text-muted mb-3 m-0">{{ t('onboarding.factoryLayoutGuestDesc') }}</p>
          <OnboardingUnitChain :units="guestFactoryLayout" :icons="unitTypeIcons" />
        </div>
        <!-- Guest shop layout preview -->
        <div v-if="isGuestMode && guestShopLayout" class="factory-layout-panel factory-layout-shop bg-card border border-[rgba(34,197,94,0.4)] rounded-xl p-5 text-left" aria-label="Sales shop layout">
          <h3 class="text-base font-semibold mb-1">{{ t('onboarding.shopLayoutTitle') }}</h3>
          <p class="text-sm text-muted mb-3 m-0">{{ t('onboarding.shopLayoutGuestDesc') }}</p>
          <OnboardingUnitChain :units="guestShopLayout" :icons="unitTypeIcons" />
        </div>
        <!-- Guest mode tick countdown -->
        <div v-if="isGuestMode && gameState" class="guest-tick-panel flex gap-3 items-start bg-page border border-divider rounded-lg p-4 text-sm text-muted text-left">
          <span class="text-2xl shrink-0">⏱</span>
          <div>
            <strong class="block mb-1 text-body">{{ t('onboarding.configureStepTick') }}</strong>
            <p class="m-0">{{ t('onboarding.configureStepTickDesc') }}</p>
            <p class="tick-status m-0">{{ t('onboarding.configureStepTickStatus', { time: formatGameTickTime(gameState.currentTick, locale) }) }}</p>
            <p v-if="tickCountdown" class="tick-countdown m-0 font-semibold text-good" role="timer">{{ tickCountdown }}</p>
          </div>
        </div>
        <!-- Guest simulated profit preview -->
        <div
          v-if="isGuestMode && simulatedProfit"
          class="guest-profit-preview bg-gradient-to-br from-[rgba(0,200,83,0.08)] to-[rgba(0,71,255,0.04)] border border-[rgba(0,200,83,0.4)] rounded-xl p-5 flex flex-col gap-4 text-left"
          role="region"
          aria-label="Simulated first profit"
        >
          <div class="flex gap-3 items-start">
            <span class="text-2xl shrink-0">📈</span>
            <div>
              <strong class="block mb-1 text-body">{{ t('onboarding.guestProfitPreviewTitle') }}</strong>
              <p class="text-sm text-muted m-0">{{ t('onboarding.guestProfitPreviewDesc') }}</p>
            </div>
          </div>
          <div class="flex flex-col gap-2 border-t border-[rgba(0,200,83,0.25)] pt-3">
            <div class="flex justify-between items-center text-sm">
              <span class="text-muted">{{ t('onboarding.guestProfitRevenue') }}</span
              ><span class="profit-stat-revenue">{{ formatCurrency(simulatedProfit.revenue) }}</span>
            </div>
            <div class="flex justify-between items-center text-sm">
              <span class="text-muted">{{ t('onboarding.guestProfitCost') }}</span
              ><span>-{{ formatCurrency(simulatedProfit.cost) }}</span>
            </div>
            <div class="flex justify-between items-center text-sm font-semibold border-t border-[rgba(0,200,83,0.25)] pt-2">
              <span class="text-muted">{{ t('onboarding.guestProfitNet') }}</span
              ><span :class="simulatedProfit.profit >= 0 ? 'text-good' : 'text-bad'"> {{ simulatedProfit.profit >= 0 ? '+' : '' }}{{ formatCurrency(simulatedProfit.profit) }} </span>
            </div>
          </div>
        </div>
        <!-- Guest pricing explanation -->
        <div
          v-if="isGuestMode && selectedProduct && guestConfiguredShopPrice"
          class="guest-price-panel bg-gradient-to-br from-[rgba(0,71,255,0.06)] to-[rgba(0,200,83,0.04)] border border-[rgba(0,71,255,0.3)] rounded-xl p-5 flex flex-col gap-3 text-left"
          role="region"
          aria-label="Configured sale price"
        >
          <div class="flex gap-3 items-start">
            <span class="text-2xl shrink-0">💲</span>
            <div>
              <strong class="block mb-1 text-body">{{ t('onboarding.guestPriceTitle') }}</strong>
              <p class="text-sm text-muted m-0">
                {{
                  t('onboarding.guestPriceDesc', {
                    product: getProductName(selectedProduct),
                    price: formatCurrency(guestConfiguredShopPrice),
                    basePrice: formatCurrency(getProductLocalPrice(selectedProduct)),
                  })
                }}
              </p>
            </div>
          </div>
          <p class="price-panel-tip text-xs text-muted border-t border-[rgba(0,71,255,0.15)] pt-2 m-0 italic">{{ t('onboarding.guestPriceTip') }}</p>
        </div>
        <!-- Guest save-progress section -->
        <section
          v-if="isGuestMode"
          class="guest-save-progress bg-gradient-to-br from-[rgba(0,71,255,0.06)] to-[rgba(0,200,83,0.04)] border-2 border-brand rounded-xl p-7 flex flex-col gap-5 text-left"
          aria-labelledby="guest-save-title"
        >
          <div class="flex gap-4 items-start">
            <span class="text-3xl shrink-0">🔐</span>
            <div>
              <h3 id="guest-save-title" class="text-xl font-semibold text-brand mb-1 m-0">{{ t('onboarding.guestSaveTitle') }}</h3>
              <p class="text-sm text-muted m-0">{{ t('onboarding.guestSaveSubtitle') }}</p>
            </div>
          </div>
          <ul class="guest-keeps-list list-none p-0 m-0 flex flex-col gap-1.5 text-sm text-muted" aria-label="What you keep when you save">
            <li>
              ✅ <strong class="text-body">{{ companyName || t('onboarding.guestCompanyPlaceholder') }}</strong> — {{ t('onboarding.guestSaveKeepsCompany') }}
            </li>
            <li v-if="selectedCity">
              ✅ <strong class="text-body">{{ selectedCity.name }}</strong> — {{ t('onboarding.guestSaveKeepsCity') }}
            </li>
            <li v-if="selectedProduct">
              ✅ <strong class="text-body">{{ getProductName(selectedProduct) }}</strong> — {{ t('onboarding.guestSaveKeepsProduct') }}
            </li>
            <li>✅ {{ t('onboarding.guestSaveKeepsSetup') }}</li>
          </ul>
          <div class="flex flex-col gap-3 max-w-[420px]">
            <p class="m-0 rounded-md border border-brand/25 bg-brand/10 px-3 py-3 text-sm text-body">
              {{ t('onboarding.guestSaveDriveAccessNotice') }}
            </p>
            <p v-if="guestSaveError" class="text-bad text-sm m-0" role="alert">{{ guestSaveError }}</p>
            <button class="btn btn-primary btn-lg w-full justify-center" :disabled="guestSaveLoading" @click="saveGuestProgress">
              <template v-if="guestSaveLoading">{{ t('common.loading') }}</template>
              <template v-else>
                <svg class="mr-2 h-4 w-4" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                  <path
                    fill="#EA4335"
                    d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.7-6-6s2.7-6 6-6c1.9 0 3.2.8 3.9 1.5l2.7-2.6C16.9 3.3 14.7 2.4 12 2.4 6.7 2.4 2.4 6.7 2.4 12S6.7 21.6 12 21.6c6.9 0 9.6-4.8 9.6-7.2 0-.5-.1-.9-.1-1.2H12z"
                  />
                  <path fill="#34A853" d="M2.4 7.7l3.2 2.3C6.5 8 9 6 12 6c1.9 0 3.2.8 3.9 1.5l2.7-2.6C16.9 3.3 14.7 2.4 12 2.4 8.1 2.4 4.7 4.6 2.4 7.7z" />
                  <path fill="#FBBC05" d="M12 21.6c2.6 0 4.8-.9 6.5-2.5l-3-2.5c-.8.6-1.9 1.1-3.5 1.1-3.9 0-5.3-2.6-5.5-3.9l-3.2 2.5c2.2 3.2 5.7 5.3 8.7 5.3z" />
                  <path fill="#4285F4" d="M21.6 12.4c0-.8-.1-1.3-.2-1.9H12v3.9h5.5c-.3 1.5-1.6 2.8-2.8 3.6l3 2.5c1.8-1.6 2.9-4 2.9-8.1z" />
                </svg>
                {{ t('auth.loginWithBiatec') }}
              </template>
            </button>
          </div>
        </section>
        <div v-if="completionResult" class="completion-achievements grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-4 text-left">
          <div class="achievement-item flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">🏭</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ completionResult.factory.name }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionFactory') }}</span>
            </div>
          </div>
          <div class="achievement-item flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">🏪</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ completionResult.salesShop.name }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionShop') }}</span>
            </div>
          </div>
          <div class="achievement-item flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">📦</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ completionResult.selectedProduct.name }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionProduct', { product: completionResult.selectedProduct.name }) }}</span>
            </div>
          </div>
          <div class="achievement-item flex items-start gap-3 bg-card border border-divider rounded-lg p-4">
            <span class="text-2xl shrink-0">💰</span>
            <div class="flex flex-col gap-1">
              <strong class="text-sm">{{ formatCurrency(completionResult.company.cash, completionCurrencyCode) }}</strong
              ><span class="text-muted text-xs">{{ t('onboarding.completionCapital', { amount: formatCurrency(completionResult.company.cash, completionCurrencyCode) }) }}</span>
            </div>
          </div>
        </div>
        <!-- Authenticated factory layout display -->
        <div v-if="completionFactoryUnits" class="factory-layout-panel bg-card border border-divider rounded-xl p-5 text-left" aria-label="Factory layout">
          <h3 class="text-base font-semibold mb-1">{{ t('onboarding.factoryLayoutTitle') }}</h3>
          <p class="text-sm text-muted mb-3 m-0">{{ t('onboarding.factoryLayoutDesc') }}</p>
          <OnboardingUnitChain :units="completionFactoryUnits" :icons="unitTypeIcons" />
        </div>
        <!-- Authenticated shop layout display -->
        <div v-if="completionShopUnits" class="factory-layout-panel factory-layout-shop bg-card border border-[rgba(34,197,94,0.4)] rounded-xl p-5 text-left" aria-label="Sales shop layout">
          <h3 class="text-base font-semibold mb-1">{{ t('onboarding.shopLayoutTitle') }}</h3>
          <p class="text-sm text-muted mb-3 m-0">{{ t('onboarding.shopLayoutDesc') }}</p>
          <OnboardingUnitChain :units="completionShopUnits" :icons="unitTypeIcons" />
        </div>
        <section
          v-if="!isGuestMode"
          class="startup-pack-panel text-left border border-[rgba(255,109,0,0.35)] rounded-xl p-6 bg-gradient-to-br from-[rgba(255,109,0,0.12)] to-[rgba(0,71,255,0.1)]"
          aria-labelledby="startup-pack-title"
        >
          <div class="flex justify-between items-start gap-4">
            <div>
              <span class="startup-pack-eyebrow inline-block mb-1.5 text-xs font-bold tracking-widest uppercase text-[var(--color-tertiary)]">{{ t('proAccess.eyebrow') }}</span>
              <h3 id="startup-pack-title" class="text-xl font-semibold m-0">{{ t('proAccess.title') }}</h3>
            </div>
            <span
              class="startup-pack-status px-3 py-1.5 rounded-full text-xs font-bold uppercase tracking-wide"
              :class="auth.isProSubscriber ? 'bg-[rgba(0,200,83,0.18)] text-[var(--color-secondary)]' : 'bg-[rgba(248,113,113,0.12)] text-[var(--color-danger)]'"
            >
              {{ auth.isProSubscriber ? t('proAccess.activeBadge') : t('proAccess.inactiveBadge') }}
            </span>
          </div>
          <p class="text-muted text-sm mt-3 mb-5">
            <template v-if="auth.isProSubscriber && auth.player?.proSubscriptionEndsAtUtc">
              {{ t('proAccess.activeBody', { date: formatDateTime(auth.player.proSubscriptionEndsAtUtc) }) }}
            </template>
            <template v-else>
              {{ t('proAccess.inactiveBody') }}
            </template>
          </p>
          <p class="text-muted text-sm mb-4">{{ t('proAccess.manageBody') }}</p>
          <div class="flex flex-wrap gap-3">
            <a class="btn btn-primary btn-lg" :href="masterPortalUrl" target="_blank" rel="noreferrer"> {{ t('proAccess.openPortal') }} </a>
          </div>
        </section>
        <section v-if="!isGuestMode && !milestoneCompleted" class="configure-guide bg-card border border-divider rounded-xl p-6 text-left" aria-labelledby="configure-guide-title">
          <h3 id="configure-guide-title" class="text-xl font-semibold text-brand mb-2">{{ t('onboarding.configureShopTitle') }}</h3>
          <p class="text-muted text-sm mb-5">{{ t('onboarding.configureShopDesc') }}</p>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-6">
            <article class="configure-step flex gap-3 items-start bg-page border border-divider rounded-lg p-4">
              <span class="text-2xl shrink-0">💰</span>
              <div>
                <strong class="block mb-1 text-sm">{{ t('onboarding.configureStepCash') }}</strong>
                <p class="text-muted text-xs m-0">{{ t('onboarding.configureStepCashDesc', { amount: formatCurrency(configureGuideCash, completionCurrencyCode) }) }}</p>
              </div>
            </article>
            <article class="configure-step flex gap-3 items-start bg-page border border-divider rounded-lg p-4">
              <span class="text-2xl shrink-0">💲</span>
              <div>
                <strong class="block mb-1 text-sm">{{ t('onboarding.configureStepPrice') }}</strong>
                <p class="text-muted text-xs m-0">
                  {{
                    configureGuideBasePrice === null
                      ? t('onboarding.configureStepPriceDesc')
                      : t('onboarding.configureStepPriceDescWithPrice', { price: formatCurrency(configureGuideBasePrice, completionCurrencyCode) })
                  }}
                </p>
              </div>
            </article>
            <article class="configure-step flex gap-3 items-start bg-page border border-divider rounded-lg p-4">
              <span class="text-2xl shrink-0">🌐</span>
              <div>
                <strong class="block mb-1 text-sm">{{ t('onboarding.configureStepPublicSales') }}</strong>
                <p class="text-muted text-xs m-0">{{ t('onboarding.configureStepPublicSalesDesc') }}</p>
              </div>
            </article>
            <article class="configure-step flex gap-3 items-start bg-page border border-divider rounded-lg p-4">
              <span class="text-2xl shrink-0">⏱</span>
              <div>
                <strong class="block mb-1 text-sm">{{ t('onboarding.configureStepTick') }}</strong>
                <p class="text-muted text-xs m-0">{{ t('onboarding.configureStepTickDesc') }}</p>
                <ol class="next-tick-process-list mt-2 ml-5 p-0 text-[0.85rem] text-muted flex flex-col gap-1">
                  <li>{{ t('onboarding.nextTickProcessStep1') }}</li>
                  <li>{{ t('onboarding.nextTickProcessStep2') }}</li>
                  <li>{{ t('onboarding.nextTickProcessStep3') }}</li>
                </ol>
                <p v-if="gameState" class="tick-status text-muted text-xs m-0 mt-1">{{ t('onboarding.configureStepTickStatus', { time: formatGameTickTime(gameState.currentTick, locale) }) }}</p>
                <p v-if="tickCountdown" class="tick-countdown m-0 font-semibold text-good text-sm" role="timer">{{ tickCountdown }}</p>
              </div>
            </article>
          </div>
          <div class="flex justify-center mb-6">
            <RouterLink v-if="shopBuildingId" :to="'/building/' + shopBuildingId" class="btn btn-primary btn-lg"> {{ t('onboarding.configureShopCta') }} <span class="ml-1">🏪</span></RouterLink>
          </div>
          <!-- Mission readiness status -->
          <div
            v-if="firstSaleMission"
            class="mission-status border rounded-lg p-4 flex flex-col gap-3 mb-2"
            :class="{
              'border-[rgba(255,149,0,0.5)] bg-[rgba(255,149,0,0.05)]': firstSaleMission.phase === 'CONFIGURE_SHOP',
              'border-[rgba(0,122,255,0.4)] bg-[rgba(0,122,255,0.04)]': firstSaleMission.phase === 'AWAITING_FIRST_SALE',
            }"
            role="status"
            aria-label="Business readiness"
          >
            <div class="flex items-center gap-2 flex-wrap">
              <span class="text-lg">{{ firstSaleMission.phase === 'AWAITING_FIRST_SALE' ? '⏳' : '⚠️' }}</span
              ><strong class="text-sm">{{ t('onboarding.missionStatusTitle') }}</strong
              ><span
                class="mission-phase-badge text-xs font-semibold px-2 py-0.5 rounded-full ml-auto"
                :class="{
                  'bg-[rgba(255,149,0,0.15)] text-[var(--color-warning,#ff9500)]': firstSaleMission.phase === 'CONFIGURE_SHOP',
                  'bg-[rgba(0,122,255,0.12)] text-[var(--color-primary,#007aff)]': firstSaleMission.phase === 'AWAITING_FIRST_SALE',
                }"
              >
                {{ firstSaleMission.phase === 'CONFIGURE_SHOP' ? t('onboarding.missionPhaseConfigureShop') : t('onboarding.missionPhaseAwaiting') }}
              </span>
            </div>
            <ul v-if="firstSaleMission.blockers.length > 0" class="mission-blockers m-0 pl-5 text-sm text-muted flex flex-col gap-1">
              <li v-for="blocker in firstSaleMission.blockers" :key="blocker" class="text-caution">{{ blockerMessage(blocker) }}</li>
            </ul>
            <p v-else class="m-0 text-sm text-muted">{{ t('onboarding.configureStepTickDesc') }}</p>
          </div>
          <div class="border-t border-divider pt-5 flex flex-col items-center gap-3 text-center">
            <p class="text-sm text-muted m-0">{{ t('onboarding.milestoneCompleteHint') }}</p>
            <button class="btn btn-secondary" :disabled="milestoneLoading" @click="markMilestoneComplete">
              {{ milestoneLoading ? t('common.loading') : t('onboarding.milestoneCompleteCta') }} <span v-if="!milestoneLoading" class="ml-1">✓</span>
            </button>
            <p v-if="milestoneError" class="milestone-error text-bad text-sm m-0" role="alert">{{ milestoneError }}</p>
          </div>
        </section>
        <!-- Business live panel: shown after marking milestone complete -->
        <section
          v-if="!isGuestMode && milestoneCompleted"
          class="business-live-panel bg-gradient-to-br from-[rgba(0,200,83,0.08)] to-[rgba(0,71,255,0.04)] border border-[rgba(0,200,83,0.4)] rounded-xl p-6 flex flex-col gap-5 text-left"
          aria-labelledby="business-live-title"
        >
          <div class="flex gap-4 items-start">
            <span class="text-3xl shrink-0">🎉</span>
            <div>
              <h3 id="business-live-title" class="text-lg font-semibold m-0 mb-1">{{ t('onboarding.businessLiveTitle') }}</h3>
              <p class="text-sm text-muted m-0">{{ t('onboarding.businessLiveDesc') }}</p>
            </div>
          </div>
          <!-- First-sale celebration -->
          <div
            v-if="firstSaleMission && firstSaleMission.firstSaleRevenue !== null"
            class="first-sale-celebration bg-[rgba(0,200,83,0.08)] border border-[rgba(0,200,83,0.3)] rounded-xl p-4 flex flex-col gap-3"
            role="region"
            aria-label="First sale details"
          >
            <h4 class="font-bold text-base m-0">{{ t('onboarding.firstSaleCelebrationTitle') }}</h4>
            <p class="text-sm text-muted m-0">{{ t('onboarding.firstSaleCelebrationDesc') }}</p>
            <dl class="grid grid-cols-2 gap-2 m-0 max-sm:grid-cols-1">
              <div v-if="firstSaleMission.firstSaleProductName" class="flex flex-col gap-0.5">
                <dt class="text-xs text-muted font-medium">{{ t('onboarding.firstSaleCelebrationProduct') }}</dt>
                <dd class="m-0 text-[0.95rem] font-semibold">{{ firstSaleMission.firstSaleProductName }}</dd>
              </div>
              <div v-if="firstSaleMission.firstSaleRevenue !== null" class="flex flex-col gap-0.5">
                <dt class="text-xs text-muted font-medium">{{ t('onboarding.firstSaleCelebrationRevenue') }}</dt>
                <dd class="m-0 text-lg font-semibold text-good">{{ formatCurrency(firstSaleMission.firstSaleRevenue) }}</dd>
              </div>
              <div v-if="firstSaleMission.firstSaleQuantity !== null" class="flex flex-col gap-0.5">
                <dt class="text-xs text-muted font-medium">{{ t('onboarding.firstSaleCelebrationQuantity') }}</dt>
                <dd class="m-0 text-[0.95rem] font-semibold">{{ firstSaleMission.firstSaleQuantity }}</dd>
              </div>
              <div v-if="firstSaleMission.firstSalePricePerUnit !== null" class="flex flex-col gap-0.5">
                <dt class="text-xs text-muted font-medium">{{ t('onboarding.firstSaleCelebrationPrice') }}</dt>
                <dd class="m-0 text-[0.95rem] font-semibold">{{ formatCurrency(firstSaleMission.firstSalePricePerUnit) }}</dd>
              </div>
              <div v-if="firstSaleMission.firstSaleTick !== null" class="flex flex-col gap-0.5">
                <dt class="text-xs text-muted font-medium">{{ t('onboarding.firstSaleCelebrationTick') }}</dt>
                <dd class="m-0 text-[0.95rem] font-semibold" :title="'tick #' + firstSaleMission.firstSaleTick">{{ formatGameTickTime(firstSaleMission.firstSaleTick, locale) }}</dd>
              </div>
            </dl>
          </div>
          <div v-if="gameState" class="flex flex-col gap-1">
            <p class="tick-status text-sm text-muted m-0" :title="'tick #' + gameState.currentTick">
              {{ t('onboarding.businessLiveTickInfo', { time: formatGameTickTime(gameState.currentTick, locale) }) }}
            </p>
            <p v-if="tickCountdown" class="tick-countdown m-0 font-semibold text-good" role="timer">{{ tickCountdown }}</p>
          </div>
          <div>
            <h4 class="text-sm font-semibold mb-2 m-0">{{ t('onboarding.nextTickProcessTitle') }}</h4>
            <ol class="next-tick-process-list ml-5 p-0 text-sm text-muted flex flex-col gap-1">
              <li>{{ t('onboarding.nextTickProcessStep1') }}</li>
              <li>{{ t('onboarding.nextTickProcessStep2') }}</li>
              <li>{{ t('onboarding.nextTickProcessStep3') }}</li>
            </ol>
          </div>
          <div class="flex gap-3 items-center flex-wrap">
            <button class="btn btn-primary btn-lg" @click="navigateToDashboard">
              {{ firstSaleMission?.firstSaleRevenue !== null ? t('onboarding.firstSaleCelebrationCta') : t('onboarding.businessLiveDashboardCta') }} <span class="ml-1">→</span></button
            ><RouterLink to="/leaderboard" class="btn btn-secondary">{{ t('onboarding.completionViewLeaderboard') }}</RouterLink
            ><RouterLink to="/encyclopedia" class="btn btn-secondary">{{ t('onboarding.completionBrowseEncyclopedia') }}</RouterLink>
          </div>
        </section>
        <div v-if="!isGuestMode && !milestoneCompleted" class="mt-4">
          <h3 class="text-lg font-semibold mb-4 text-muted">{{ t('onboarding.completionNextSteps') }}</h3>
          <div class="flex gap-4 justify-center flex-wrap">
            <RouterLink to="/dashboard" class="btn btn-secondary">{{ t('onboarding.completionGoDashboard') }} →</RouterLink
            ><RouterLink to="/leaderboard" class="btn btn-secondary">{{ t('onboarding.completionViewLeaderboard') }}</RouterLink
            ><RouterLink to="/encyclopedia" class="btn btn-secondary">{{ t('onboarding.completionBrowseEncyclopedia') }}</RouterLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
@keyframes pick-glow {
  0%,
  100% {
    border-color: var(--color-divider);
    box-shadow: none;
  }
  50% {
    border-color: var(--color-primary);
    box-shadow:
      0 0 0 1px var(--color-primary),
      0 0 12px 2px rgba(0, 71, 255, 0.25);
  }
}

.pick-hint {
  animation: pick-glow 1.8s ease-in-out infinite;
}

.pick-hint:hover {
  animation: none;
}

.step-content {
  width: 100%;
  max-width: 1160px;
  margin-inline: auto;
}

.step-content.step-content-wide {
  max-width: 1160px;
}

.step-content.completion-step {
  max-width: 1160px;
}
</style>
