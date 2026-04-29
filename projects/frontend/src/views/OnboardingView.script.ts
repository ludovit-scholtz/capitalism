/* oxlint-disable no-unused-vars */
 
 
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import { computeSimulatedProfit, trackOnboardingEvent } from '@/lib/onboardingAnalytics'
import { getLocalizedProductDescription, getLocalizedProductName, getLocalizedRecipeIngredientName, getLocalizedResourceName, getProductImageUrl } from '@/lib/catalogPresentation'
import { formatGameTickTime } from '@/lib/gameTime'
import { useTickRefresh } from '@/composables/useTickRefresh'
import {
  canProceedStep3 as checkCanProceedStep3,
  canProceedStep4 as checkCanProceedStep4,
  clampStep,
  getAvailableLots,
  getMaxReachableStep,
  getRecommendedFactoryLotIds,
  getRecommendedShopLotIds,
  keyToStep,
  stepToKey,
} from '@/lib/onboardingHelpers'
import OnboardingLotSelector from '@/components/onboarding/OnboardingLotSelector.vue'
import { useAuthStore } from '@/stores/auth'
import { useTickCountdown } from '@/composables/useTickCountdown'
import { formatMoney } from '@/lib/currencyFormat'
import { generateOnboardingCompanyName } from '@/lib/onboardingCompanyName'
import type { BuildingLot, City, EurFxRate, FirstSaleMission, GameState, OnboardingResult, OnboardingStartResult, ProductType } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
auth.initFromStorage()
const masterPortalUrl = import.meta.env.VITE_MASTER_WEB_URL || 'http://localhost:5174'

function hasStoredSessionToken() {
  if (typeof localStorage !== 'undefined') {
    const stored = localStorage.getItem('auth_token')
    const expires = localStorage.getItem('auth_expires')
    if (stored && expires && new Date(expires) > new Date()) {
      return true
    }
  }

  if (typeof document === 'undefined') {
    return false
  }

  const readCookie = (name: string) => {
    const prefix = `${name}=`
    const match = document.cookie.split('; ').find((entry) => entry.startsWith(prefix))
    return match ? decodeURIComponent(match.slice(prefix.length)) : null
  }

  const cookieToken = readCookie('auth_token')
  const cookieExpires = readCookie('auth_expires')
  return !!cookieToken && !!cookieExpires && new Date(cookieExpires) > new Date()
}

const hasAuthenticatedSession = computed(() => auth.isAuthenticated || !!auth.player || hasStoredSessionToken())

const PERSONAL_STARTING_CASH = 200_000
const FOUNDER_CONTRIBUTION = 200_000
const DEFAULT_IPO_RAISE_TARGET = 400_000

const ipoOptions = [
  {
    raiseTarget: 400_000,
    founderOwnershipRatio: 0.5,
    titleKey: 'onboarding.ipoOptionStarterTitle',
    descriptionKey: 'onboarding.ipoOptionStarterDesc',
  },
  {
    raiseTarget: 600_000,
    founderOwnershipRatio: 0.3333,
    titleKey: 'onboarding.ipoOptionGrowthTitle',
    descriptionKey: 'onboarding.ipoOptionGrowthDesc',
  },
  {
    raiseTarget: 800_000,
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
}

const step = ref(1)
const loading = ref(false)
const error = ref<string | null>(null)
const onboardingCompanyCash = ref<number | null>(null)
const milestoneLoading = ref(false)
const milestoneError = ref<string | null>(null)
const milestoneCompleted = ref(false)

// Guest mode state
const isGuestMode = computed(() => !hasAuthenticatedSession.value)
const guestSaveError = ref<string | null>(null)
const guestSaveLoading = ref(false)
const guestAuthMode = ref<'register' | 'login'>('register')
const guestEmail = ref('')
const guestPassword = ref('')
const guestDisplayName = ref('')

const industries = ref<string[]>([])
const cities = ref<City[]>([])
const eurFxRates = ref<EurFxRate[]>([])
const products = ref<ProductType[]>([])
const cityLots = ref<BuildingLot[]>([])

/** FX rate for the currently selected city: units of city currency per 1 EUR. Defaults to 1 (EUR). */
const cityFxRate = computed<number>(() => {
  const code = selectedCity.value?.currencyCode ?? 'EUR'
  if (code === 'EUR') return 1
  const rate = eurFxRates.value.find((r) => r.currencyCode === code)
  return rate?.rate ?? 1
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
const companyName = computed(() => generateOnboardingCompanyName(selectedIndustry.value, selectedCity.value?.name))
const starterCompany = computed(() => {
  const companyId = auth.player?.onboardingCompanyId
  if (!companyId) {
    return auth.player?.companies.find((company) => company.name === companyName.value) ?? null
  }

  return auth.player?.companies.find((company) => company.id === companyId) ?? null
})
const selectedIpoOption = computed(() => ipoOptions.find((option) => option.raiseTarget === selectedIpoRaiseTarget.value) ?? ipoOptions[0])
/** Company starting cash in the selected city's local currency. */
const companyStartingCash = computed(() => Math.round((FOUNDER_CONTRIBUTION + selectedIpoOption.value.raiseTarget) * cityFxRate.value))
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
const recommendedShopLotIds = computed(() => getRecommendedShopLotIds(availableShopLots.value))

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
const completionCurrencyCode = computed<string>(
  () =>
    completionResult.value?.cityCurrencyCode ??
    selectedCity.value?.currencyCode ??
    'EUR',
)

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
  B2B_SALES: '🔗',
  PUBLIC_SALES: '🏷️',
  MINING: '⛏️',
  BRANDING: '🎨',
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
 * Matches the ConfigureStarterFactory backend output: PURCHASE → MANUFACTURING → STORAGE → B2B_SALES.
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
 * Matches the AddStarterShop backend output: PURCHASE → PUBLIC_SALES.
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
}

/** Maps each starter industry to its i18n description key. */
const industryDescKeys: Record<string, string> = {
  FURNITURE: 'onboarding.industryDescFurniture',
  FOOD_PROCESSING: 'onboarding.industryDescFoodProcessing',
  HEALTHCARE: 'onboarding.industryDescHealthcare',
}

/** Maps each starter industry to its i18n first-product hint key. */
const industryFirstProductKeys: Record<string, string> = {
  FURNITURE: 'onboarding.industryFirstProductFurniture',
  FOOD_PROCESSING: 'onboarding.industryFirstProductFoodProcessing',
  HEALTHCARE: 'onboarding.industryFirstProductHealthcare',
}

/** Maps each starter industry to its i18n "why choose" tag key. */
const industryWhyKeys: Record<string, string> = {
  FURNITURE: 'onboarding.industryWhyFurniture',
  FOOD_PROCESSING: 'onboarding.industryWhyFoodProcessing',
  HEALTHCARE: 'onboarding.industryWhyHealthcare',
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
  return [400000, 600000, 800000].includes(parsed) ? parsed : null
}

function applyRouteSelections() {
  selectedIndustry.value = typeof route.query.industry === 'string' ? route.query.industry : ''
  selectedProductId.value = typeof route.query.productId === 'string' ? route.query.productId : ''
  selectedCityId.value = typeof route.query.cityId === 'string' ? route.query.cityId : ''
  selectedFactoryLotId.value = typeof route.query.factoryLotId === 'string' ? route.query.factoryLotId : ''
  selectedShopLotId.value = typeof route.query.shopLotId === 'string' ? route.query.shopLotId : ''
  selectedIpoRaiseTarget.value = parseIpoRaiseTarget(route.query.ipoRaiseTarget)
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

  return query
}

function isRouteQuerySynced(nextQuery: Record<string, string>): boolean {
  return Object.entries(nextQuery).every(([key, value]) => route.query[key] === value) && Object.keys(route.query).every((key) => nextQuery[key] !== undefined)
}

watch([step, selectedIndustry, selectedProductId, selectedCityId, selectedIpoRaiseTarget, selectedFactoryLotId, selectedShopLotId], async () => {
  if (!isRouteStateReady.value) {
    return
  }

  const nextQuery = buildRouteQuery()
  if (!isRouteQuerySynced(nextQuery)) {
    await router.replace({ query: nextQuery })
  }
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

onMounted(async () => {
  trackOnboardingEvent('onboarding_start', { authenticated: hasAuthenticatedSession.value })

  if (hasAuthenticatedSession.value) {
    if (!auth.player) {
      await auth.fetchMe()
    }

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
      const [citiesData, fxRatesData] = await Promise.all([
        gqlRequest<{ cities: City[] }>(CITIES_QUERY),
        gqlRequest<{ eurFxRates: EurFxRate[] }>('{ eurFxRates { currencyCode rate } }'),
      ])
      await Promise.all([loadGameState(), loadFirstSaleMission()])

      cities.value = citiesData.cities
      eurFxRates.value = fxRatesData.eurFxRates

      // Derive the player's onboarding city from the shop building that was created during
      // onboarding (onboardingShopBuildingId → building.cityId → city for correct currency).
      const shopBuilding = auth.player?.companies
        .flatMap((company) => company.buildings)
        .find((building) => building.id === auth.player?.onboardingShopBuildingId)
      if (shopBuilding?.cityId) {
        selectedCityId.value = shopBuilding.cityId
      }

      step.value = 7
      isRouteStateReady.value = true
      return
    }
  }

  try {
    loading.value = true
    const [industriesData, citiesData, fxRatesData] = await Promise.all([
      gqlRequest<{ starterIndustries: { industries: string[] } }>('{ starterIndustries { industries } }'),
      gqlRequest<{ cities: City[] }>(CITIES_QUERY),
      gqlRequest<{ eurFxRates: EurFxRate[] }>('{ eurFxRates { currencyCode rate } }'),
    ])

    industries.value = industriesData.starterIndustries.industries
    cities.value = citiesData.cities
    eurFxRates.value = fxRatesData.eurFxRates

    applyRouteSelections()

    if (selectedIndustry.value) {
      await loadProducts()
      if (!products.value.some((product) => product.id === selectedProductId.value)) {
        selectedProductId.value = ''
      }
    }

    if (selectedCityId.value) {
      await loadLots()
      if (!cityLots.value.some((lot) => lot.id === selectedFactoryLotId.value)) {
        selectedFactoryLotId.value = ''
      }
      if (!cityLots.value.some((lot) => lot.id === selectedShopLotId.value)) {
        selectedShopLotId.value = ''
      }
    }

    if (hasAuthenticatedSession.value) {
      await syncOngoingOnboardingState()
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
  selectedIndustry.value = industry
  selectedProductId.value = ''
  selectedCityId.value = ''
  selectedIpoRaiseTarget.value = null
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null
  cityLots.value = []
  await loadProducts()
  step.value = 2
  trackOnboardingEvent('industry_selected', { industry })
}

function selectProduct(productId: string) {
  error.value = null
  selectedProductId.value = productId
  selectedCityId.value = ''
  selectedIpoRaiseTarget.value = null
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null
  cityLots.value = []
  step.value = 3
  trackOnboardingEvent('product_selected', { productId, industry: selectedIndustry.value })
}

async function selectCity(cityId: string) {
  error.value = null
  selectedCityId.value = cityId
  selectedIpoRaiseTarget.value = null
  selectedFactoryLotId.value = ''
  selectedShopLotId.value = ''
  onboardingCompanyCash.value = null
  await loadLots()
  step.value = 4
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

async function startOnboardingCompany() {
  if (!canProceedStep3.value || !selectedFactoryLot.value) return

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
    const result = await gqlRequest<{ startOnboardingCompany: OnboardingStartResult }>(
      `mutation StartOnboardingCompany($input: StartOnboardingCompanyInput!) {
        startOnboardingCompany(input: $input) {
          nextStep
          company { id name cash }
          factory { id name type }
          factoryLot {
            id cityId name description district latitude longitude price suitableTypes
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
    error.value = e instanceof Error ? e.message : t('onboarding.lotUnavailableBody')
    await loadLots()
    if (selectedFactoryLot.value?.ownerCompanyId) {
      selectedFactoryLotId.value = ''
    }
  } finally {
    loading.value = false
  }
}

async function completeOnboarding() {
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

  // Client-side validation: password must be at least 8 characters for registration
  if (guestAuthMode.value === 'register' && guestPassword.value.length < 8) {
    guestSaveError.value = t('auth.passwordTooShort')
    return
  }

  guestSaveLoading.value = true

  try {
    if (guestAuthMode.value === 'register') {
      await auth.register(guestEmail.value, guestDisplayName.value || companyName.value, guestPassword.value)
    } else {
      await auth.login(guestEmail.value, guestPassword.value)
    }

    // Persist the onboarding city so it survives login and is the active city after authentication.
    if (selectedCityId.value) {
      auth.switchCity(selectedCityId.value)
    }

    // Now authenticated — check if this player already completed onboarding
    if (!auth.player) {
      await auth.fetchMe()
    }

    if (auth.player?.onboardingFirstSaleCompletedAtUtc || auth.player?.onboardingCompletedAtUtc) {
      router.push('/dashboard')
      return
    }

    if (selectedIndustry.value && selectedCityId.value && selectedFactoryLotId.value && selectedProductId.value && selectedShopLotId.value) {
      try {
        loading.value = true
        error.value = null

        const startResult = await gqlRequest<{ startOnboardingCompany: OnboardingStartResult }>(
          `mutation StartOnboardingCompany($input: StartOnboardingCompanyInput!) {
            startOnboardingCompany(input: $input) {
              nextStep
              company { id name cash }
              factory { id name type }
              factoryLot {
                id cityId name description district latitude longitude price suitableTypes
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
          authMode: guestAuthMode.value,
        })
      } catch (migrationErr: unknown) {
        const code = migrationErr instanceof GraphQLError ? migrationErr.code : undefined
        if (code === 'LOT_ALREADY_OWNED') {
          // A lot was taken between the guest simulation and the real purchase — restart
          // wizard from step 1 so the player can pick fresh lots.
          onboardingCompanyCash.value = null
          completionResult.value = null
          selectedFactoryLotId.value = ''
          selectedShopLotId.value = ''
          await loadLots()
          step.value = 1
          error.value = t('onboarding.guestMigrationRetry')
        } else {
          // Any other backend failure (network outage, validation error, auth mismatch,
          // duplicate submit, etc.) must be shown explicitly — NOT masked as a lot-conflict.
          error.value = migrationErr instanceof Error ? migrationErr.message : t('onboarding.guestMigrationGenericError')
        }
      } finally {
        loading.value = false
      }
    } else {
      step.value = 1
    }
  } catch (e: unknown) {
    guestSaveError.value = e instanceof Error ? e.message : t('auth.loginFailed')
  } finally {
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
  if (selectedCity.value) {
    return formatCurrency(getProductLocalPrice(product, selectedCity.value.currencyCode), selectedCity.value.currencyCode)
  }

  return cities.value
    .slice(0, 3)
    .map((city) => `${city.name}: ${formatCurrency(getProductLocalPrice(product, city.currencyCode), city.currencyCode)}`)
    .join(' · ')
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
function getUnitTypeLabel(unitType: string): string {
  const key = `buildingDetail.unitTypes.${unitType}`
  const translated = t(key)
  return translated === key ? unitType : translated
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
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

