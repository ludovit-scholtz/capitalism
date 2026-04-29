import { computed, type InjectionKey, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { getInventorySourcingCostPerUnit, getPlannedUnitConstructionCost, getTotalInventorySourcingCost, getUnitConstructionCost, sumPlannedConfigurationCost } from '@/lib/buildingUnitEconomics'
import { isProductLocked } from '@/lib/productAccess'
import { formatPercent, formatUnitQuantity, getFillBucket, getFlowSegments, getUnitConfiguredItemId, getUnitPriceMetric } from '@/lib/gridTileHelpers'
import {
  applyHorizontalLinkCycle,
  applyPrimaryDiagonalLinkCycle,
  applySecondaryDiagonalLinkCycle,
  applyVerticalLinkCycle,
  getHorizontalLinkArrow,
  getHorizontalLinkState,
  getPrimaryDiagonalLinkState,
  getSecondaryDiagonalLinkState,
  getVerticalLinkArrow,
  getVerticalLinkState,
} from '@/lib/linkHelpers'
import { annotateExchangeOffers, selectOptimalOffer, sortExchangeOffers, detectLogisticsTrap, type AnnotatedExchangeOffer, type ExchangeSortBy } from '@/lib/globalExchange'
import {
  getLocalizedProductDescription,
  getLocalizedProductName,
  getLocalizedResourceDescription,
  getLocalizedResourceName,
  getProductImageUrl,
  getResourceImageUrl,
  getLocalizedIndustry,
} from '@/lib/catalogPresentation'
import { PRODUCTION_PANEL_DISMISSED_KEY, SALES_PANEL_DISMISSED_KEY, isBuildingPanelDismissed, dismissBuildingPanel, shouldShowPanel } from '@/lib/panelDismissal'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { gqlRequest, GraphQLError } from '@/lib/graphql'
import {
  isMasterConnected,
  fetchMasterLayouts,
  saveMasterLayout,
  deleteMasterLayout,
  saveLocalLayout,
  deleteLocalLayout,
  getLocalLayoutsForType,
  type BuildingLayoutTemplate,
  type LayoutUnit,
} from '@/lib/masterLayoutApi'
import { deepEqual } from '@/lib/utils'
import { useAuthStore } from '@/stores/auth'
import { useGameStateStore } from '@/stores/gameState'
import { formatTickDuration, formatGameTickTime } from '@/lib/gameTime'
import { getUnitResourceHistoryItemKey, type UnitResourceHistoryItemOption } from '@/lib/unitResourceHistory'
import { buildPurchaseVendorOptions, collectSameCityVendorItemKeys, getPurchaseSelectorItemKey, sortPurchaseSelectorItems } from '@/lib/purchaseSelector'
import { getSalesUnitProductOptions } from '@/lib/salesUnitProductPicker'
import type {
  Building,
  BuildingConfigurationPlanRemoval,
  BuildingConfigurationPlanUnit,
  BuildingFinancialTimeline,
  BuildingUnit,
  BuildingUnitInventory,
  BuildingUnitInventorySummary,
  BuildingUnitResourceHistoryPoint,
  BuildingUnitOperationalStatus,
  BuildingRecentActivityEvent,
  City,
  CityPowerBalance,
  Company,
  EurFxRate,
  GlobalExchangeOffer,
  PowerPlantAnalytics,
  ProcurementPreview,
  ProductType,
  PublicSalesAnalytics,
  RankedProductResult,
  ResearchBrandState,
  ResourceType,
  CityMediaHouseInfo,
  MediaHouseAnalyticsResult,
  SourcingCandidate,
  UnitProductAnalytics,
} from '@/types'
import type { HorizontalLinkState, VerticalLinkState } from '@/lib/linkHelpers'

export type GridUnit = BuildingUnit | BuildingConfigurationPlanUnit | EditableGridUnit
export type ItemSelection = { kind: 'resource' | 'product'; id: string } | null
export type SelectorItem = {
  kind: 'resource' | 'product'
  id: string
  name: string
  imageUrl?: string | null
  description?: string | null
  helperText?: string | null
  groupLabel: string
  unitSymbol?: string | null
  badge?: string | null
  disabled?: boolean
}

export type PurchaseVendorOption = {
  companyId: string
  companyName: string
  buildingId: string
  buildingName: string
  cityId: string
  distanceKm: number
  pricePerUnit: number | null
  transitCostPerUnit: number
}

export type PurchaseVendorCompanyData = {
  id: string
  name: string
  buildings: Array<{
    id: string
    name: string
    cityId: string
    latitude: number
    longitude: number
    units: Array<{
      id: string
      unitType: string
      resourceTypeId: string | null
      productTypeId: string | null
      minPrice: number | null
    }>
  }>
}

export type EditableGridUnit = {
  id: string
  unitType: string
  gridX: number
  gridY: number
  level: number
  linkUp: boolean
  linkDown: boolean
  linkLeft: boolean
  linkRight: boolean
  linkUpLeft: boolean
  linkUpRight: boolean
  linkDownLeft: boolean
  linkDownRight: boolean
  resourceTypeId: string | null
  productTypeId: string | null
  minPrice: number | null
  maxPrice: number | null
  purchaseSource: string | null
  saleVisibility: string | null
  budget: number | null
  mediaHouseBuildingId: string | null
  minQuality: number | null
  brandScope: string | null
  vendorLockCompanyId: string | null
  lockedCityId: string | null
  industryCategory: string | null
  isReverting?: boolean
}

// ──────────────────────────────────────────────────────────────────────────────
// Injection key for the building detail composable (provide/inject pattern)
// ──────────────────────────────────────────────────────────────────────────────

export const BUILDING_DETAIL_KEY = Symbol('buildingDetail') as InjectionKey<ReturnType<typeof useBuildingDetail>>

export function useBuildingDetail() {
  const LINK_CHANGE_TICKS = 1
  const UNIT_PLAN_CHANGE_TICKS = 3
  const gridIndexes = [0, 1, 2, 3] as const
  const SUPPORTED_INDUSTRIES = ['FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE', 'ELECTRONICS', 'CONSTRUCTION'] as const

  const { t, locale } = useI18n()
  const router = useRouter()
  const route = useRoute()
  const auth = useAuthStore()
  const gameStateStore = useGameStateStore()

  const buildingId = computed(() => route.params.id as string)
  const building = ref<Building | null>(null)
  const currentTick = ref(0)
  const loading = ref(true)
  const saving = ref(false)
  /** Page-level error (building not found, load failed). Shown as a full-page error state. */
  const error = ref<string | null>(null)
  /** Inline save error (e.g. RECIPE_INPUT_MISMATCH). Shown within the planning section. */
  const saveError = ref<string | null>(null)
  const companyCash = ref<number | null>(null)
  const isEditing = ref(false)
  type GridCellSelection = { x: number; y: number }

  const selectedCell = ref<GridCellSelection | null>(null)
  const showUnitPicker = ref(false)
  const draftUnits = ref<EditableGridUnit[]>([])
  const editBaselineUnits = ref<EditableGridUnit[]>([])
  const resourceTypes = ref<ResourceType[]>([])
  const productTypes = ref<ProductType[]>([])
  const rankedProducts = ref<RankedProductResult[]>([])
  const rankedProductsLoading = ref(false)
  const cities = ref<City[]>([])
  const eurFxRates = ref<EurFxRate[]>([])
  const unitInventorySummaries = ref<BuildingUnitInventorySummary[]>([])
  const unitInventories = ref<BuildingUnitInventory[]>([])
  const unitResourceHistories = ref<BuildingUnitResourceHistoryPoint[]>([])
  const exchangeOffers = ref<GlobalExchangeOffer[]>([])
  const exchangeOffersLoading = ref(false)
  const exchangeSortBy = ref<ExchangeSortBy>('deliveredPrice')

  function parseUnitQuery(value: unknown): GridCellSelection | null {
    if (typeof value !== 'string') return null
    const match = value.match(/^([0-3]),([0-3])$/)
    if (!match) return null

    const x = Number(match[1])
    const y = Number(match[2])
    if (!Number.isInteger(x) || !Number.isInteger(y)) return null

    return { x, y }
  }

  function parseUnitTabQuery(value: unknown): string | null {
    if (typeof value !== 'string' || value.length === 0) return null
    return value
  }

  function syncSelectedCellQuery(cell: GridCellSelection | null) {
    const nextUnit = cell ? `${cell.x},${cell.y}` : undefined
    const currentUnit = typeof route.query.unit === 'string' ? route.query.unit : undefined
    if (currentUnit === nextUnit) return

    void router.replace({
      query: {
        ...route.query,
        unit: nextUnit,
      },
    })
  }

  function syncSelectedUnitTabQuery(tab: string | null) {
    const nextTab = tab ?? undefined
    const currentTab = typeof route.query.unitTab === 'string' ? route.query.unitTab : undefined
    if (currentTab === nextTab) return

    void router.replace({
      query: {
        ...route.query,
        unitTab: nextTab,
      },
    })
  }

  function setReadOnlySelectedCell(cell: GridCellSelection | null) {
    selectedCell.value = cell
    syncSelectedCellQuery(cell)
    if (!cell) {
      syncSelectedUnitTabQuery(null)
    }
  }

  function restoreReadOnlySelectedCell(units: GridUnit[]) {
    const requestedCell = parseUnitQuery(route.query.unit)
    if (!requestedCell) {
      selectedCell.value = null
      return
    }

    // During live refreshes there can be a brief empty-state before units repopulate.
    // Keep the requested selection in that window so route-driven state does not get lost.
    if (units.length === 0) {
      selectedCell.value = requestedCell
      return
    }

    const hasUnit = !!getUnitAtFrom(units, requestedCell.x, requestedCell.y)
    if (!hasUnit) {
      selectedCell.value = null
      syncSelectedCellQuery(null)
      return
    }

    selectedCell.value = requestedCell
  }

  function clickReadOnlyCell(x: number, y: number) {
    setReadOnlySelectedCell(getUnitAtFrom(activeUnits.value, x, y) ? { x, y } : null)
  }

  // Procurement preview (next-tick execution preview for PURCHASE units)
  const procurementPreview = ref<ProcurementPreview | null>(null)
  const procurementPreviewLoading = ref(false)
  let activeProcurementPreviewRequest = 0

  // Sourcing comparison (all candidates ranked by landed cost for PURCHASE units)
  const sourcingCandidates = ref<SourcingCandidate[]>([])
  const sourcingCandidatesLoading = ref(false)
  let activeSourcingCandidatesRequest = 0
  const purchaseVendorCompanies = ref<PurchaseVendorCompanyData[]>([])
  const showPurchaseSelector = ref(false)

  // Operational status per unit (ACTIVE/IDLE/BLOCKED/FULL/UNCONFIGURED)
  const unitOperationalStatuses = ref<BuildingUnitOperationalStatus[]>([])
  const unitOperationalStatusesLoading = ref(false)

  // Recent tick-by-tick activity feed for the building
  const recentActivity = ref<BuildingRecentActivityEvent[]>([])
  const recentActivityLoading = ref(false)
  const buildingFinancialTimeline = ref<BuildingFinancialTimeline | null>(null)
  const buildingFinancialTimelineLoading = ref(false)

  // Power plant analytics state
  const powerPlantAnalytics = ref<PowerPlantAnalytics | null>(null)
  const powerPlantAnalyticsLoading = ref(false)
  const cityPowerBalance = ref<CityPowerBalance | null>(null)
  const cityPowerBalanceLoading = ref(false)

  // R&D research progress state
  const researchBrands = ref<ResearchBrandState[]>([])
  const researchBrandsLoading = ref(false)

  // City media houses — loaded lazily when a MARKETING unit is selected
  const cityMediaHouses = ref<CityMediaHouseInfo[]>([])
  const cityMediaHousesLoading = ref(false)

  /** The media house selected in the current draft marketing unit (if any). */
  const selectedDraftMediaHouse = computed(() => {
    if (!selectedCell.value) return null
    const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
    if (!unit?.mediaHouseBuildingId) return null
    return cityMediaHouses.value.find((mh) => mh.id === unit.mediaHouseBuildingId) ?? null
  })

  // Public Sales market intelligence analytics
  const publicSalesAnalytics = ref<PublicSalesAnalytics | null>(null)
  const publicSalesAnalyticsLoading = ref(false)
  // Manufacturing unit product analytics
  const unitProductAnalytics = ref<UnitProductAnalytics | null>(null)
  const unitProductAnalyticsLoading = ref(false)
  // Quick price update (instant, no tick delay)
  const quickPriceInput = ref<number | null>(null)
  const quickPriceSaving = ref(false)
  const quickPriceSuccess = ref(false)
  const quickPriceError = ref<string | null>(null)
  const showSaleDialog = ref(false)
  const salePrice = ref<number | null>(null)
  const savingSale = ref(false)
  const cancellingPlan = ref(false)
  const cancelPlanError = ref<string | null>(null)
  const layoutName = ref('')
  const layoutDescription = ref('')
  // Master-API layout state
  const masterLayouts = ref<BuildingLayoutTemplate[]>([])
  const masterLayoutsLoading = ref(false)
  const masterLayoutsError = ref<string | null>(null)
  const localLayouts = ref<BuildingLayoutTemplate[]>([])
  const layoutSaving = ref(false)
  const layoutSaveError = ref<string | null>(null)
  const layoutSaveSuccess = ref(false)
  const layoutDeleteError = ref<string | null>(null)
  const overwriteConfirmPending = ref<BuildingLayoutTemplate | null>(null)
  const masterConnected = computed(() => isMasterConnected())
  const masterUserEmail = computed(() => auth.player?.email ?? '')
  const selectedHistoryItemKey = ref<string | null>(null)

  const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

  // Property management (APARTMENT / COMMERCIAL)
  const showRentDialog = ref(false)
  const newRentPerSqm = ref<number | null>(null)
  const savingRent = ref(false)
  const rentSaveError = ref<string | null>(null)

  // Media house content management (MEDIA_HOUSE)
  const contentBudgetInput = ref<number | null>(null)
  const savingContentBudget = ref(false)
  const contentBudgetError = ref<string | null>(null)
  const contentBudgetSuccess = ref(false)

  // Media house upgrade (MEDIA_HOUSE)
  const upgradingMediaHouse = ref(false)
  const mediaHouseUpgradeError = ref<string | null>(null)
  const mediaHouseUpgradeSuccess = ref(false)

  // Media house analytics (MEDIA_HOUSE)
  const mediaHouseAnalytics = ref<MediaHouseAnalyticsResult | null>(null)
  const mediaHouseAnalyticsLoading = ref(false)

  // Flush storage
  const showFlushConfirmDialog = ref(false)
  const flushingStorage = ref(false)
  const flushStorageError = ref<string | null>(null)
  const flushStorageSuccess = ref(false)

  // Unit upgrade
  const schedulingUpgrade = ref(false)
  const unitUpgradeError = ref<string | null>(null)
  const unitUpgradeInfoCache = ref<import('@/types').UnitUpgradeInfo | null>(null)
  /** Unit IDs staged for upgrade via the "Stage Upgrade" button; applied when "Store Upgrade" is clicked. */
  const draftUpgradeUnitIds = ref<Set<string>>(new Set())

  let activeBuildingLoadRequest = 0
  let activeExchangeOffersRequest = 0

  const allowedUnitsMap: Record<string, string[]> = {
    MINE: ['MINING', 'STORAGE', 'B2B_SALES'],
    FACTORY: ['PURCHASE', 'MANUFACTURING', 'BRANDING', 'STORAGE', 'B2B_SALES'],
    SALES_SHOP: ['PURCHASE', 'MARKETING', 'STORAGE', 'PUBLIC_SALES'],
    RESEARCH_DEVELOPMENT: ['PRODUCT_QUALITY', 'BRAND_QUALITY'],
  }

  const unitColors: Record<string, string> = {
    MINING: '#ff6d00',
    STORAGE: '#8b949e',
    B2B_SALES: '#00c853',
    PURCHASE: '#0047ff',
    MANUFACTURING: '#ff6d00',
    BRANDING: '#9333ea',
    MARKETING: '#ec4899',
    PUBLIC_SALES: '#00c853',
    PRODUCT_QUALITY: '#0047ff',
    BRAND_QUALITY: '#9333ea',
  }

  const activeUnits = computed(() => building.value?.units ?? [])
  const pendingConfiguration = computed(() => building.value?.pendingConfiguration ?? null)
  const pendingUnits = computed(() => pendingConfiguration.value?.units ?? [])
  const pendingRemovals = computed(() => pendingConfiguration.value?.removals ?? [])
  const plannedUnits = computed<GridUnit[]>(() => (isEditing.value ? draftUnits.value : pendingUnits.value))
  const allowedUnits = computed(() => {
    if (!building.value) return []
    return allowedUnitsMap[building.value.type] || []
  })
  const showStarterSetupBanner = computed(() => building.value?.type === 'FACTORY' && activeUnits.value.length === 0 && pendingConfiguration.value === null && !isEditing.value)
  const intermediateProductIds = computed(() => {
    const ids = new Set<string>()
    for (const product of productTypes.value) {
      for (const recipe of product.recipes) {
        if (recipe.inputProductType?.id) {
          ids.add(recipe.inputProductType.id)
        }
      }
    }
    return ids
  })
  const allSelectableItems = computed<SelectorItem[]>(() => [
    ...resourceTypes.value.map((resource) => ({
      kind: 'resource' as const,
      id: resource.id,
      name: getLocalizedResourceName(resource, locale.value),
      imageUrl: getResourceImageUrl(resource),
      description: getLocalizedResourceDescription(resource, locale.value),
      groupLabel: t('buildingDetail.selector.rawMaterials'),
      unitSymbol: resource.unitSymbol,
    })),
    ...productTypes.value.map((product) => ({
      kind: 'product' as const,
      id: product.id,
      name: getLocalizedProductName(product, locale.value),
      imageUrl: getProductImageUrl(product),
      description: getLocalizedProductDescription(product, locale.value),
      helperText: isProductLocked(product) ? t('catalog.proDetail') : null,
      groupLabel: t('buildingDetail.selector.products'),
      unitSymbol: product.unitSymbol,
      badge: product.isProOnly ? t('catalog.proBadge') : null,
      disabled: isProductLocked(product),
    })),
  ])
  const lockedConfiguredProducts = computed(() => {
    const configuredProductIds = new Set([...activeUnits.value, ...pendingUnits.value].map((unit) => unit.productTypeId).filter((value): value is string => !!value))

    return productTypes.value.filter((product) => configuredProductIds.has(product.id) && isProductLocked(product))
  })
  const lockedConfiguredProductNames = computed(() => lockedConfiguredProducts.value.map((product) => getLocalizedProductName(product, locale.value)).join(', '))
  const isUpgradeInProgress = computed(() => pendingConfiguration.value !== null)
  const showPlanningSection = computed(() => isEditing.value)

  // True when at least one R&D unit (active or pending) is configured but no research brands exist yet
  const hasConfiguredRdUnits = computed(() => {
    if (building.value?.type !== 'RESEARCH_DEVELOPMENT') return false
    const rdUnitTypes = ['PRODUCT_QUALITY', 'BRAND_QUALITY']
    const activeLive = activeUnits.value.some((u) => rdUnitTypes.includes(u.unitType))
    const pendingLive = pendingUnits.value.some((u) => rdUnitTypes.includes(u.unitType))
    return activeLive || pendingLive
  })
  const remainingUpgradeTicks = computed(() => {
    if (!pendingConfiguration.value) return 0
    return Math.max(pendingConfiguration.value.appliesAtTick - currentTick.value, 0)
  })
  const draftTotalTicks = computed(() => {
    const positions = new Set<string>()

    for (const unit of activeUnits.value) positions.add(`${unit.gridX},${unit.gridY}`)
    for (const unit of draftUnits.value) positions.add(`${unit.gridX},${unit.gridY}`)
    for (const unit of pendingUnits.value) positions.add(`${unit.gridX},${unit.gridY}`)
    for (const removal of pendingRemovals.value) positions.add(`${removal.gridX},${removal.gridY}`)

    return Array.from(positions).reduce((maxTicks, position) => {
      const [gridX = 0, gridY = 0] = position.split(',').map(Number)
      return Math.max(maxTicks, getDraftTicksAt(gridX, gridY))
    }, 0)
  })
  const hasDraftChanges = computed(() => !areUnitCollectionsEqual(draftUnits.value, editBaselineUnits.value) || draftUpgradeUnitIds.value.size > 0)
  const draftConstructionCost = computed(() => sumPlannedConfigurationCost(activeUnits.value, draftUnits.value))
  const projectedCompanyCashAfterApply = computed(() => {
    if (companyCash.value == null) return null
    return companyCash.value - draftConstructionCost.value
  })

  // ── Production chain status ──

  /**
   * Returns the PURCHASE, MANUFACTURING and STORAGE units from the best available
   * source: active live units first, pending-configuration units second.
   * Shown in the production-chain status panel.
   */
  const chainDisplayUnits = computed(() => {
    const units = activeUnits.value.length > 0 ? activeUnits.value : pendingUnits.value
    return {
      purchase: units.find((u) => u.unitType === 'PURCHASE') ?? null,
      manufacturing: units.find((u) => u.unitType === 'MANUFACTURING') ?? null,
      storage: units.find((u) => u.unitType === 'STORAGE') ?? null,
    }
  })

  const chainStatus = computed(() => {
    const { purchase, manufacturing, storage } = chainDisplayUnits.value
    const isPurchaseConfigured = !!(purchase && (purchase.resourceTypeId || purchase.productTypeId))
    const isManufacturingConfigured = !!(manufacturing && manufacturing.productTypeId)
    const isStoragePresent = !!storage
    return {
      isPurchaseConfigured,
      isManufacturingConfigured,
      isStoragePresent,
      isChainComplete: isPurchaseConfigured && isManufacturingConfigured && isStoragePresent,
    }
  })

  // ── Panel dismissal state ──

  /** Whether the production-chain panel has been dismissed for the current building. */
  const productionChainPanelDismissed = ref(false)
  /** Whether the sales-chain panel has been dismissed for the current building. */
  const salesChainPanelDismissed = ref(false)

  function loadPanelDismissalState(bid: string): void {
    productionChainPanelDismissed.value = isBuildingPanelDismissed(PRODUCTION_PANEL_DISMISSED_KEY, bid)
    salesChainPanelDismissed.value = isBuildingPanelDismissed(SALES_PANEL_DISMISSED_KEY, bid)
  }

  function dismissProductionChainPanel(): void {
    const bid = buildingId.value
    if (!bid) return
    dismissBuildingPanel(PRODUCTION_PANEL_DISMISSED_KEY, bid)
    productionChainPanelDismissed.value = true
  }

  function dismissSalesChainPanel(): void {
    const bid = buildingId.value
    if (!bid) return
    dismissBuildingPanel(SALES_PANEL_DISMISSED_KEY, bid)
    salesChainPanelDismissed.value = true
  }

  /**
   * Shows the production-chain status panel for a factory that already has units
   * saved (active or pending) but is not currently in edit mode.
   * Stays hidden after the player dismisses it unless the chain becomes incomplete
   * (an error condition that requires the player's attention).
   */
  const showProductionChainPanel = computed(() => {
    if (isEditing.value) return false
    if (building.value?.type !== 'FACTORY') return false
    if (activeUnits.value.length === 0 && pendingConfiguration.value === null) return false
    if (showStarterSetupBanner.value) return false
    return shouldShowPanel(productionChainPanelDismissed.value, chainStatus.value.isChainComplete)
  })

  /**
   * Mirrors showStarterSetupBanner but for SALES_SHOP buildings.
   */
  const showSalesShopStarterBanner = computed(() => building.value?.type === 'SALES_SHOP' && activeUnits.value.length === 0 && pendingConfiguration.value === null && !isEditing.value)

  const shopChainDisplayUnits = computed(() => {
    const units = activeUnits.value.length > 0 ? activeUnits.value : pendingUnits.value
    return {
      purchase: units.find((u) => u.unitType === 'PURCHASE') ?? null,
      publicSales: units.find((u) => u.unitType === 'PUBLIC_SALES') ?? null,
    }
  })

  const shopChainStatus = computed(() => {
    const { purchase, publicSales } = shopChainDisplayUnits.value
    const isPurchaseConfigured = !!(purchase && (purchase.resourceTypeId || purchase.productTypeId))
    const isPublicSalesConfigured = !!(publicSales && publicSales.productTypeId && publicSales.minPrice !== null && publicSales.minPrice !== undefined)
    return {
      isPurchaseConfigured,
      isPublicSalesConfigured,
      isChainComplete: isPurchaseConfigured && isPublicSalesConfigured,
    }
  })

  /**
   * Shows the sales-chain status panel for a sales shop that already has units
   * saved (active or pending) but is not currently in edit mode.
   * Stays hidden after the player dismisses it unless the chain becomes incomplete
   * (an error condition that requires the player's attention).
   */
  const showSalesChainPanel = computed(() => {
    if (isEditing.value) return false
    if (building.value?.type !== 'SALES_SHOP') return false
    if (activeUnits.value.length === 0 && pendingConfiguration.value === null) return false
    if (showSalesShopStarterBanner.value) return false
    return shouldShowPanel(salesChainPanelDismissed.value, shopChainStatus.value.isChainComplete)
  })

  type LinkChangeSummaryEntry = {
    description: string
    changeType: 'added' | 'removed'
  }

  /**
   * Computes a list of individual directional link changes between the edit baseline
   * and the current draft layout, for display in the submission summary panel.
   */
  const draftLinkChanges = computed<LinkChangeSummaryEntry[]>(() => {
    if (!isEditing.value) return []
    const changes: LinkChangeSummaryEntry[] = []

    const baselineByPos = new Map(editBaselineUnits.value.map((u) => [`${u.gridX},${u.gridY}`, u]))
    const draftByPos = new Map(draftUnits.value.map((u) => [`${u.gridX},${u.gridY}`, u]))

    // Check each grid position that appears in baseline or draft
    const allPositions = new Set([...Array.from(baselineByPos.keys()), ...Array.from(draftByPos.keys())])
    for (const pos of Array.from(allPositions)) {
      const baseline = baselineByPos.get(pos)
      const draft = draftByPos.get(pos)
      const linkDirs = [
        { flag: 'linkRight', dx: 1, dy: 0, labelKey: 'buildingDetail.linkRight' },
        { flag: 'linkLeft', dx: -1, dy: 0, labelKey: 'buildingDetail.linkLeft' },
        { flag: 'linkDown', dx: 0, dy: 1, labelKey: 'buildingDetail.linkDown' },
        { flag: 'linkUp', dx: 0, dy: -1, labelKey: 'buildingDetail.linkUp' },
        { flag: 'linkDownRight', dx: 1, dy: 1, labelKey: 'buildingDetail.linkDownRight' },
        { flag: 'linkDownLeft', dx: -1, dy: 1, labelKey: 'buildingDetail.linkDownLeft' },
        { flag: 'linkUpRight', dx: 1, dy: -1, labelKey: 'buildingDetail.linkUpRight' },
        { flag: 'linkUpLeft', dx: -1, dy: -1, labelKey: 'buildingDetail.linkUpLeft' },
      ] as const

      const [bx = 0, by = 0] = pos.split(',').map(Number)
      for (const { flag, dx, dy, labelKey } of linkDirs) {
        const wasActive = !!baseline?.[flag as keyof typeof baseline]
        const isActive = !!draft?.[flag as keyof typeof draft]
        if (wasActive === isActive) continue

        const srcType = draft?.unitType ?? baseline?.unitType ?? '?'
        const targetPos = `${bx + dx},${by + dy}`
        const targetUnit = draftByPos.get(targetPos) ?? baselineByPos.get(targetPos)
        const tgtType = targetUnit?.unitType ?? '?'
        const dirLabel = t(labelKey)
        const src = `${t(`buildingDetail.unitTypes.${srcType}`)} (${bx},${by})`
        const tgt = `${t(`buildingDetail.unitTypes.${tgtType}`)} (${bx + dx},${by + dy})`
        changes.push({
          description: `${src} ${dirLabel.toLowerCase()} → ${tgt}`,
          changeType: isActive ? 'added' : 'removed',
        })
      }
    }

    return changes
  })

  type UnitChangeSummaryEntry = {
    changeType: 'added' | 'removed' | 'replaced'
    gridX: number
    gridY: number
    unitType: string
    previousUnitType?: string
    ticks: number
    cost: number
  }

  /**
   * Computes a list of individual unit structural changes (additions, removals, type
   * changes) between the edit baseline and the current draft layout.  Link-only
   * adjustments on unchanged units are excluded here — they appear in draftLinkChanges.
   */
  const draftUnitChanges = computed<UnitChangeSummaryEntry[]>(() => {
    if (!isEditing.value) return []
    const entries: UnitChangeSummaryEntry[] = []

    const baselineByPos = new Map(editBaselineUnits.value.map((u) => [`${u.gridX},${u.gridY}`, u]))
    const draftByPos = new Map(draftUnits.value.map((u) => [`${u.gridX},${u.gridY}`, u]))
    const allPositions = new Set([...Array.from(baselineByPos.keys()), ...Array.from(draftByPos.keys())])

    for (const pos of Array.from(allPositions)) {
      const baseline = baselineByPos.get(pos)
      const draft = draftByPos.get(pos)
      const [gx = 0, gy = 0] = pos.split(',').map(Number)

      if (!baseline && draft) {
        // New unit added
        entries.push({
          changeType: 'added',
          gridX: gx,
          gridY: gy,
          unitType: draft.unitType,
          ticks: UNIT_PLAN_CHANGE_TICKS,
          cost: Math.round(getUnitConstructionCost(draft.unitType) * cityFxRate.value * 100) / 100,
        })
      } else if (baseline && !draft) {
        // Existing unit removed
        entries.push({
          changeType: 'removed',
          gridX: gx,
          gridY: gy,
          unitType: baseline.unitType,
          ticks: UNIT_PLAN_CHANGE_TICKS,
          cost: 0,
        })
      } else if (baseline && draft && baseline.unitType !== draft.unitType) {
        // Unit type replaced
        entries.push({
          changeType: 'replaced',
          gridX: gx,
          gridY: gy,
          unitType: draft.unitType,
          previousUnitType: baseline.unitType,
          ticks: UNIT_PLAN_CHANGE_TICKS,
          cost: Math.round(getUnitConstructionCost(draft.unitType) * cityFxRate.value * 100) / 100,
        })
      }
    }

    entries.sort((a, b) => a.gridY - b.gridY || a.gridX - b.gridX)
    return entries
  })

  const selectedDisplayUnit = computed<GridUnit | undefined>(() => {
    if (!selectedCell.value) return undefined

    if (isEditing.value) {
      return getUnitAtFrom(plannedUnits.value, selectedCell.value.x, selectedCell.value.y)
    }

    return getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y)
  })
  const selectedPurchaseUnit = computed(() => (selectedDisplayUnit.value?.unitType === 'PURCHASE' ? selectedDisplayUnit.value : undefined))
  const selectedPublicSalesUnit = computed(() => (!isEditing.value && selectedDisplayUnit.value?.unitType === 'PUBLIC_SALES' ? selectedDisplayUnit.value : undefined))
  const selectedManufacturingUnit = computed(() => (!isEditing.value && selectedDisplayUnit.value?.unitType === 'MANUFACTURING' ? selectedDisplayUnit.value : undefined))
  const selectedDraftPurchaseUnit = computed(() => (isEditing.value && selectedDisplayUnit.value?.unitType === 'PURCHASE' ? (selectedDisplayUnit.value as EditableGridUnit) : undefined))
  const selectedDraftPublicSalesUnit = computed(() => {
    if (!selectedCell.value || !isEditing.value) return undefined
    const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
    return unit?.unitType === 'PUBLIC_SALES' ? unit : undefined
  })

  const selectedDraftB2bSalesUnit = computed(() => {
    if (!selectedCell.value || !isEditing.value) return undefined
    const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
    return unit?.unitType === 'B2B_SALES' ? unit : undefined
  })

  const publicSalesFilteredRankedProducts = computed<RankedProductResult[]>(() =>
    getSalesUnitProductOptions({
      unit: selectedDraftPublicSalesUnit.value,
      draftUnits: draftUnits.value,
      rankedProducts: rankedProducts.value,
      unitInventories: unitInventories.value,
    }),
  )

  const b2bSalesFilteredRankedProducts = computed<RankedProductResult[]>(() =>
    getSalesUnitProductOptions({
      unit: selectedDraftB2bSalesUnit.value,
      draftUnits: draftUnits.value,
      rankedProducts: rankedProducts.value,
      unitInventories: unitInventories.value,
    }),
  )

  const selectedHistoryItemOptions = computed<UnitResourceHistoryItemOption[]>(() => getUnitResourceHistoryItemOptions(selectedDisplayUnit.value))
  const selectedUnitResourceHistory = computed(() => getSelectedUnitResourceHistory(selectedDisplayUnit.value))
  const buildingOverviewCityName = computed(() => getCityName(building.value?.cityId))
  const cityCurrencyCode = computed(() => {
    if (!building.value?.cityId) return 'EUR'
    return cities.value.find((c) => c.id === building.value!.cityId)?.currencyCode ?? 'EUR'
  })
  /** EUR → local currency multiplier for the building's city (e.g. ~25.2 for CZK). */
  const cityFxRate = computed<number>(() => {
    const code = cityCurrencyCode.value
    if (code === 'EUR') return 1
    const entry = eurFxRates.value.find((r) => r.currencyCode === code)
    return entry?.rate ?? 1
  })
  const buildingOverviewMapRoute = computed(() => {
    if (!building.value) return null

    return {
      name: 'city-map',
      params: { id: building.value.cityId },
      query: { building: building.value.id },
    }
  })
  const buildingFinancialSnapshots = computed(() => buildingFinancialTimeline.value?.timeline ?? [])
  const buildingFinancialHasActivity = computed(() => buildingFinancialSnapshots.value.some((snapshot) => snapshot.sales > 0 || snapshot.costs > 0 || snapshot.profit !== 0))

  /** Source metadata returned by getB2BPriceSource. */
  interface B2BPriceSourceInfo {
    price: number
    sourceType: 'manufacturing' | 'mining'
    itemName: string | null
  }

  /** Full price source info for the currently selected B2B_SALES draft unit. */
  const b2bPriceSource = computed<B2BPriceSourceInfo | null>(() => {
    if (!selectedCell.value || !isEditing.value) return null
    const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
    if (!unit || unit.unitType !== 'B2B_SALES') return null
    return getB2BPriceSource(unit)
  })

  /** Convenience accessor – price only (used for auto-fill on placement). */
  const b2bSuggestedPrice = computed<number | null>(() => b2bPriceSource.value?.price ?? null)

  /**
   * True when the current draft contains at least one MANUFACTURING unit with a product
   * or MINING unit with a resource — i.e., there is a configured upstream source from
   * which a B2B price can be derived. False means the no-source guidance should be shown.
   */
  const b2bHasUpstreamSource = computed<boolean>(() => {
    if (!selectedCell.value || !isEditing.value) return true
    const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
    if (!unit || unit.unitType !== 'B2B_SALES') return true
    return draftUnits.value.some((u) => (u.unitType === 'MANUFACTURING' && !!u.productTypeId) || (u.unitType === 'MINING' && !!u.resourceTypeId))
  })

  /** Max revenue across all history ticks – used to normalise the revenue bar chart heights. */
  const miMaxRevenue = computed(() => publicSalesAnalytics.value?.revenueHistory.reduce((m, s) => Math.max(m, s.revenue), 0) ?? 0)
  /** Max quantity across all history ticks – used to normalise the quantity bar chart heights. */
  const miMaxQuantitySold = computed(() => publicSalesAnalytics.value?.revenueHistory.reduce((m, s) => Math.max(m, s.quantitySold), 0) ?? 0)
  /** Max price per unit across all price history ticks – used to normalise the price bar chart heights. */
  const miMaxPricePerUnit = computed(() => publicSalesAnalytics.value?.priceHistory.reduce((m, s) => Math.max(m, s.pricePerUnit), 0) ?? 0)
  /** Max absolute profit value across profit history – used to normalise the profit bar chart heights. */
  const miMaxAbsProfit = computed(() => publicSalesAnalytics.value?.profitHistory?.reduce((m, p) => Math.max(m, Math.abs(p.profit)), 0) ?? 0)
  /** Max absolute estimated profit for manufacturing unit analytics chart normalisation. */
  const upaMaxAbsProfit = computed(() => unitProductAnalytics.value?.snapshots.reduce((m, s) => Math.max(m, Math.abs(s.estimatedProfit ?? 0)), 0) ?? 0)
  /** Max total cost for manufacturing unit analytics chart normalisation. */
  const upaMaxCost = computed(() => unitProductAnalytics.value?.snapshots.reduce((m, s) => Math.max(m, s.totalCost), 0) ?? 0)
  /** Max estimated revenue for manufacturing unit analytics chart normalisation. */
  const upaMaxEstRevenue = computed(() => unitProductAnalytics.value?.snapshots.reduce((m, s) => Math.max(m, s.estimatedRevenue ?? 0), 0) ?? 0)
  // Current configured min price for the selected PUBLIC_SALES unit (0 if not set)
  const currentPublicSalesMinPrice = computed(() => (typeof selectedPublicSalesUnit.value?.minPrice === 'number' ? selectedPublicSalesUnit.value.minPrice : 0))

  // ── Unit detail tab state ───────────────────────────────────────────────────
  /** Currently active tab key in the read-only unit detail sidebar. */
  const selectedUnitTab = ref<string>('basicInfo')

  /** Ordered list of tabs available for the currently selected unit type. */
  const unitDetailTabs = computed<Array<{ key: string }>>(() => {
    const unitType = selectedDisplayUnit.value?.unitType
    if (!unitType || isEditing.value) return []
    const tabs: Array<{ key: string }> = [{ key: 'basicInfo' }]
    if (unitType === 'PUBLIC_SALES') tabs.push({ key: 'quickActions' })
    tabs.push({ key: 'inventory' })
    tabs.push({ key: 'history' })
    if (unitType === 'PUBLIC_SALES' || unitType === 'PURCHASE' || unitType === 'MANUFACTURING') {
      tabs.push({ key: 'marketIntelligence' })
    }
    tabs.push({ key: 'recentActivity' })
    return tabs
  })

  function restoreSelectedUnitTabFromRoute() {
    if (isEditing.value || unitDetailTabs.value.length === 0) {
      selectedUnitTab.value = 'basicInfo'
      syncSelectedUnitTabQuery(null)
      return
    }

    const availableKeys = new Set(unitDetailTabs.value.map((tab) => tab.key))
    const requestedTab = parseUnitTabQuery(route.query.unitTab)

    if (requestedTab && availableKeys.has(requestedTab)) {
      if (selectedUnitTab.value !== requestedTab) {
        selectedUnitTab.value = requestedTab
      }
      return
    }

    if (!availableKeys.has(selectedUnitTab.value)) {
      selectedUnitTab.value = 'basicInfo'
    }

    syncSelectedUnitTabQuery(selectedUnitTab.value)
  }

  watch(
    () => selectedUnitTab.value,
    (tab) => {
      if (isEditing.value || unitDetailTabs.value.length === 0) {
        syncSelectedUnitTabQuery(null)
        return
      }
      const isValid = unitDetailTabs.value.some((t) => t.key === tab)
      syncSelectedUnitTabQuery(isValid ? tab : 'basicInfo')
    },
  )

  watch(
    () => route.query.unitTab,
    () => {
      restoreSelectedUnitTabFromRoute()
    },
  )

  watch(
    () => unitDetailTabs.value.map((tab) => tab.key).join(','),
    () => {
      restoreSelectedUnitTabFromRoute()
    },
  )

  let activeBuildingFinancialTimelineRequest = 0
  let activePowerPlantAnalyticsRequest = 0

  type ExchangeOfferItem = AnnotatedExchangeOffer

  const annotatedExchangeOffers = computed<ExchangeOfferItem[]>(() => {
    const maxPrice = selectedPurchaseUnit.value?.maxPrice ?? null
    const minQuality = selectedPurchaseUnit.value?.minQuality ?? null
    return annotateExchangeOffers(exchangeOffers.value, maxPrice, minQuality)
  })

  const exchangeOfferItems = computed<ExchangeOfferItem[]>(() => sortExchangeOffers(annotatedExchangeOffers.value, exchangeSortBy.value))

  const allExchangeOffersBlocked = computed(() => exchangeOfferItems.value.length > 0 && exchangeOfferItems.value.every((o) => o.blocked))

  const bestExchangeOfferCityId = computed<string | null>(() => {
    return selectOptimalOffer(annotatedExchangeOffers.value)?.cityId ?? null
  })

  const logisticsTrapWarning = computed(() => detectLogisticsTrap(annotatedExchangeOffers.value))

  // Whether sticker price (before transit) differs from landed-cost ranking in the comparison.
  const sourcingCheapestStickerDiffersFromBestLanded = computed(() => {
    const candidates = sourcingCandidates.value.filter((c) => c.isEligible)
    if (candidates.length < 2) return false
    const byLanded = [...candidates].sort((a, b) => (a.deliveredPricePerUnit ?? 0) - (b.deliveredPricePerUnit ?? 0))
    const bySticker = [...candidates].sort((a, b) => (a.exchangePricePerUnit ?? a.deliveredPricePerUnit ?? 0) - (b.exchangePricePerUnit ?? b.deliveredPricePerUnit ?? 0))
    return byLanded[0]?.sourceCityId !== bySticker[0]?.sourceCityId
  })

  const selectedPurchaseResourceSlug = computed<string | null>(() => {
    const resourceId = selectedPurchaseUnit.value?.resourceTypeId ?? null
    if (!resourceId) return null
    return resourceTypes.value.find((r) => r.id === resourceId)?.slug ?? null
  })

  const purchaseSelectorItems = computed<SelectorItem[]>(() => {
    const preferredHint = t('buildingDetail.purchaseSelector.ownSupplyHint')
    const annotatePreferred = (items: SelectorItem[]) =>
      items.map((item) =>
        sameCityVendorItemKeys.value.has(getPurchaseSelectorItemKey(item.kind, item.id))
          ? {
              ...item,
              helperText: item.helperText ? `${preferredHint} ${item.helperText}` : preferredHint,
            }
          : item,
      )

    if (building.value?.type === 'FACTORY') {
      return sortPurchaseSelectorItems(annotatePreferred(getFactoryPurchaseSelectableItems()), building.value?.type ?? null, sameCityVendorItemKeys.value)
    }

    if (building.value?.type === 'SALES_SHOP') {
      return sortPurchaseSelectorItems(
        annotatePreferred([...allSelectableItems.value.filter((item) => item.kind === 'product'), ...allSelectableItems.value.filter((item) => item.kind === 'resource')]),
        building.value?.type ?? null,
        sameCityVendorItemKeys.value,
      )
    }

    return sortPurchaseSelectorItems(annotatePreferred(allSelectableItems.value), building.value?.type ?? null, sameCityVendorItemKeys.value)
  })

  const selectedPurchaseSelection = computed<ItemSelection>(() => getItemSelection(selectedDraftPurchaseUnit.value))

  const sameCityVendorItemKeys = computed(() => collectSameCityVendorItemKeys(purchaseVendorCompanies.value, building.value?.cityId ?? null, building.value?.id ?? null))

  const resourceTypesById = computed(() => new Map(resourceTypes.value.map((resource) => [resource.id, resource])))
  const productTypesById = computed(() => new Map(productTypes.value.map((product) => [product.id, product])))

  const purchaseVendorOptions = computed<PurchaseVendorOption[]>(() => {
    return buildPurchaseVendorOptions(
      purchaseVendorCompanies.value,
      selectedPurchaseSelection.value,
      building.value?.cityId ?? null,
      building.value?.id ?? null,
      building.value ? { latitude: building.value.latitude, longitude: building.value.longitude } : null,
      resourceTypesById.value,
      productTypesById.value,
    )
  })

  const selectedPurchaseVendorSummary = computed<string | null>(() => {
    const companyId = selectedDraftPurchaseUnit.value?.vendorLockCompanyId
    if (!companyId) return null
    const match = purchaseVendorOptions.value.find((option) => option.companyId === companyId)
    if (match) {
      return match.pricePerUnit != null ? `${match.companyName} · ${match.buildingName} · ${formatCurrency(match.pricePerUnit)}` : `${match.companyName} · ${match.buildingName}`
    }
    if (companyId === building.value?.companyId) return t('buildingDetail.purchaseSelector.vendorOwnCompany')
    return purchaseVendorCompanies.value.find((company) => company.id === companyId)?.name ?? null
  })

  function formatBuildingType(type: string): string {
    return type.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase())
  }

  function getUnitColor(unitType: string): string {
    return unitColors[unitType] || '#8b949e'
  }

  function getUnitAtFrom(units: GridUnit[], x: number, y: number): GridUnit | undefined {
    return units.find((unit) => unit.gridX === x && unit.gridY === y)
  }

  /** Returns the unitType at (x, y) in a BuildingLayoutTemplate, or null if empty. */
  function getLayoutCellType(layout: BuildingLayoutTemplate, x: number, y: number): string | null {
    return layout.units.find((u) => u.gridX === x && u.gridY === y)?.unitType ?? null
  }

  /**
   * Returns a compact human-readable structural summary for a BuildingLayoutTemplate.
   * e.g. "1× Purchase · 2× Manufacturing · 1× Storage · with links"
   */
  function layoutStructureSummary(layout: BuildingLayoutTemplate): string {
    if (!layout.units.length) return `0 ${t('buildingDetail.layouts.units')}`
    const counts: Record<string, number> = {}
    let hasLinks = false
    for (const u of layout.units) {
      counts[u.unitType] = (counts[u.unitType] ?? 0) + 1
      if (u.linkUp || u.linkDown || u.linkLeft || u.linkRight || u.linkUpLeft || u.linkUpRight || u.linkDownLeft || u.linkDownRight) {
        hasLinks = true
      }
    }
    const parts = Object.entries(counts).map(([type, count]) => `${count}× ${t(`buildingDetail.unitTypes.${type}`)}`)
    if (hasLinks) parts.push(t('buildingDetail.layouts.hasLinks'))
    return parts.join(' · ')
  }

  function getDraftUnitAt(x: number, y: number): EditableGridUnit | undefined {
    return draftUnits.value.find((unit) => unit.gridX === x && unit.gridY === y)
  }

  function getPendingUnitAt(x: number, y: number): BuildingConfigurationPlanUnit | undefined {
    return pendingUnits.value.find((unit) => unit.gridX === x && unit.gridY === y)
  }

  function getPendingRemovalAt(x: number, y: number): BuildingConfigurationPlanRemoval | undefined {
    return pendingRemovals.value.find((removal) => removal.gridX === x && removal.gridY === y)
  }

  function cloneUnit(unit: GridUnit): EditableGridUnit {
    return {
      id: unit.id,
      unitType: unit.unitType,
      gridX: unit.gridX,
      gridY: unit.gridY,
      level: unit.level,
      linkUp: unit.linkUp,
      linkDown: unit.linkDown,
      linkLeft: unit.linkLeft,
      linkRight: unit.linkRight,
      linkUpLeft: unit.linkUpLeft,
      linkUpRight: unit.linkUpRight,
      linkDownLeft: unit.linkDownLeft,
      linkDownRight: unit.linkDownRight,
      resourceTypeId: ('resourceTypeId' in unit ? unit.resourceTypeId : null) ?? null,
      productTypeId: ('productTypeId' in unit ? unit.productTypeId : null) ?? null,
      minPrice: ('minPrice' in unit ? unit.minPrice : null) ?? null,
      maxPrice: ('maxPrice' in unit ? unit.maxPrice : null) ?? null,
      purchaseSource: ('purchaseSource' in unit ? unit.purchaseSource : null) ?? null,
      saleVisibility: ('saleVisibility' in unit ? unit.saleVisibility : null) ?? null,
      budget: ('budget' in unit ? unit.budget : null) ?? null,
      mediaHouseBuildingId: ('mediaHouseBuildingId' in unit ? unit.mediaHouseBuildingId : null) ?? null,
      minQuality: ('minQuality' in unit ? unit.minQuality : null) ?? null,
      brandScope: ('brandScope' in unit ? unit.brandScope : null) ?? null,
      vendorLockCompanyId: ('vendorLockCompanyId' in unit ? unit.vendorLockCompanyId : null) ?? null,
      lockedCityId: ('lockedCityId' in unit ? unit.lockedCityId : null) ?? null,
      industryCategory: ('industryCategory' in unit ? unit.industryCategory : null) ?? null,
      isReverting: ('isReverting' in unit ? unit.isReverting : undefined) ?? undefined,
    }
  }

  function setDraftUnitsFrom(sourceUnits: GridUnit[]) {
    draftUnits.value = sourceUnits.map((unit) => cloneUnit(unit))
  }

  function setEditBaselineFrom(sourceUnits: GridUnit[]) {
    editBaselineUnits.value = sourceUnits.map((unit) => cloneUnit(unit))
  }

  function getEditingSourceUnits(): GridUnit[] {
    return pendingConfiguration.value?.units ?? activeUnits.value
  }

  function startEditing() {
    const sourceUnits = getEditingSourceUnits()
    setDraftUnitsFrom(sourceUnits)
    setEditBaselineFrom(sourceUnits)
    isEditing.value = true
    setReadOnlySelectedCell(null)
    showUnitPicker.value = false
    refreshLocalLayouts()
    refreshMasterLayouts()
  }

  function cancelEditing() {
    const sourceUnits = getEditingSourceUnits()
    setDraftUnitsFrom(sourceUnits)
    setEditBaselineFrom(sourceUnits)
    isEditing.value = false
    setReadOnlySelectedCell(null)
    showUnitPicker.value = false
    saveError.value = null
    draftUpgradeUnitIds.value = new Set()
  }

  function applyStarterLayout() {
    // Pre-populate the draft with a PURCHASE → MANUFACTURING → STORAGE → B2B_SALES chain at y=0
    const starterUnits: EditableGridUnit[] = [
      {
        id: 'draft-starter-0-0',
        unitType: 'PURCHASE',
        gridX: 0,
        gridY: 0,
        level: 1,
        linkUp: false,
        linkDown: false,
        linkLeft: false,
        linkRight: true,
        linkUpLeft: false,
        linkUpRight: false,
        linkDownLeft: false,
        linkDownRight: false,
        resourceTypeId: null,
        productTypeId: null,
        minPrice: null,
        maxPrice: null,
        purchaseSource: null,
        saleVisibility: null,
        budget: null,
        mediaHouseBuildingId: null,
        minQuality: null,
        brandScope: null,
        vendorLockCompanyId: null,
        lockedCityId: null,
        industryCategory: null,
      },
      {
        id: 'draft-starter-1-0',
        unitType: 'MANUFACTURING',
        gridX: 1,
        gridY: 0,
        level: 1,
        linkUp: false,
        linkDown: false,
        linkLeft: false,
        linkRight: true,
        linkUpLeft: false,
        linkUpRight: false,
        linkDownLeft: false,
        linkDownRight: false,
        resourceTypeId: null,
        productTypeId: null,
        minPrice: null,
        maxPrice: null,
        purchaseSource: null,
        saleVisibility: null,
        budget: null,
        mediaHouseBuildingId: null,
        minQuality: null,
        brandScope: null,
        vendorLockCompanyId: null,
        lockedCityId: null,
        industryCategory: null,
      },
      {
        id: 'draft-starter-2-0',
        unitType: 'STORAGE',
        gridX: 2,
        gridY: 0,
        level: 1,
        linkUp: false,
        linkDown: false,
        linkLeft: false,
        linkRight: true,
        linkUpLeft: false,
        linkUpRight: false,
        linkDownLeft: false,
        linkDownRight: false,
        resourceTypeId: null,
        productTypeId: null,
        minPrice: null,
        maxPrice: null,
        purchaseSource: null,
        saleVisibility: null,
        budget: null,
        mediaHouseBuildingId: null,
        minQuality: null,
        brandScope: null,
        vendorLockCompanyId: null,
        lockedCityId: null,
        industryCategory: null,
      },
      {
        id: 'draft-starter-3-0',
        unitType: 'B2B_SALES',
        gridX: 3,
        gridY: 0,
        level: 1,
        linkUp: false,
        linkDown: false,
        linkLeft: false,
        linkRight: false,
        linkUpLeft: false,
        linkUpRight: false,
        linkDownLeft: false,
        linkDownRight: false,
        resourceTypeId: null,
        productTypeId: null,
        minPrice: null,
        maxPrice: null,
        purchaseSource: null,
        saleVisibility: 'GROUP',
        budget: null,
        mediaHouseBuildingId: null,
        minQuality: null,
        brandScope: null,
        vendorLockCompanyId: null,
        lockedCityId: null,
        industryCategory: null,
      },
    ]
    setDraftUnitsFrom(starterUnits)
    setEditBaselineFrom([])
    isEditing.value = true
    setReadOnlySelectedCell(null)
    showUnitPicker.value = false
    refreshLocalLayouts()
    refreshMasterLayouts()
  }

  function applyShopStarterLayout() {
    const shopStarterUnits: EditableGridUnit[] = [
      {
        id: 'draft-shop-starter-0-0',
        unitType: 'PURCHASE',
        gridX: 0,
        gridY: 0,
        level: 1,
        linkUp: false,
        linkDown: false,
        linkLeft: false,
        linkRight: true,
        linkUpLeft: false,
        linkUpRight: false,
        linkDownLeft: false,
        linkDownRight: false,
        resourceTypeId: null,
        productTypeId: null,
        minPrice: null,
        maxPrice: null,
        purchaseSource: null,
        saleVisibility: null,
        budget: null,
        mediaHouseBuildingId: null,
        minQuality: null,
        brandScope: null,
        vendorLockCompanyId: null,
        lockedCityId: null,
        industryCategory: null,
      },
      {
        id: 'draft-shop-starter-1-0',
        unitType: 'PUBLIC_SALES',
        gridX: 1,
        gridY: 0,
        level: 1,
        linkUp: false,
        linkDown: false,
        linkLeft: false,
        linkRight: false,
        linkUpLeft: false,
        linkUpRight: false,
        linkDownLeft: false,
        linkDownRight: false,
        resourceTypeId: null,
        productTypeId: null,
        minPrice: null,
        maxPrice: null,
        purchaseSource: null,
        saleVisibility: null,
        budget: null,
        mediaHouseBuildingId: null,
        minQuality: null,
        brandScope: null,
        vendorLockCompanyId: null,
        lockedCityId: null,
        industryCategory: null,
      },
    ]
    setDraftUnitsFrom(shopStarterUnits)
    setEditBaselineFrom([])
    isEditing.value = true
    setReadOnlySelectedCell(null)
    showUnitPicker.value = false
    refreshLocalLayouts()
    refreshMasterLayouts()
  }

  function clickDraftCell(x: number, y: number) {
    if (!isEditing.value) {
      return
    }

    const existing = getDraftUnitAt(x, y)
    selectedCell.value = { x, y }
    showUnitPicker.value = !existing
  }

  function placeUnit(unitType: string) {
    if (!selectedCell.value || !isEditing.value) return

    const newUnit: EditableGridUnit = {
      id: `draft-${selectedCell.value.x}-${selectedCell.value.y}-${Date.now()}`,
      unitType,
      gridX: selectedCell.value.x,
      gridY: selectedCell.value.y,
      level: getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y)?.level ?? 1,
      linkUp: false,
      linkDown: false,
      linkLeft: false,
      linkRight: false,
      linkUpLeft: false,
      linkUpRight: false,
      linkDownLeft: false,
      linkDownRight: false,
      resourceTypeId: null,
      productTypeId: null,
      minPrice: null,
      maxPrice: null,
      purchaseSource: null,
      saleVisibility: null,
      budget: null,
      mediaHouseBuildingId: null,
      minQuality: null,
      brandScope: null,
      vendorLockCompanyId: null,
      lockedCityId: null,
      industryCategory: null,
    }

    // Auto-fill competitive default price for B2B_SALES based on adjacent/building units
    if (unitType === 'B2B_SALES') {
      newUnit.saleVisibility = 'GROUP'
      const suggestedPrice = getB2BSuggestedPrice(newUnit)
      if (suggestedPrice !== null) {
        newUnit.minPrice = suggestedPrice
      }
    }

    draftUnits.value = [...draftUnits.value.filter((unit) => !(unit.gridX === newUnit.gridX && unit.gridY === newUnit.gridY)), newUnit]
    selectedCell.value = null
    showUnitPicker.value = false
  }

  function clearConnectionsAround(x: number, y: number) {
    const left = getDraftUnitAt(x - 1, y)
    const right = getDraftUnitAt(x + 1, y)
    const up = getDraftUnitAt(x, y - 1)
    const down = getDraftUnitAt(x, y + 1)
    const upLeft = getDraftUnitAt(x - 1, y - 1)
    const upRight = getDraftUnitAt(x + 1, y - 1)
    const downLeft = getDraftUnitAt(x - 1, y + 1)
    const downRight = getDraftUnitAt(x + 1, y + 1)

    if (left) left.linkRight = false
    if (right) right.linkLeft = false
    if (up) up.linkDown = false
    if (down) down.linkUp = false
    if (upLeft) upLeft.linkDownRight = false
    if (upRight) upRight.linkDownLeft = false
    if (downLeft) downLeft.linkUpRight = false
    if (downRight) downRight.linkUpLeft = false
  }

  function removeDraftUnit(x: number, y: number) {
    if (!isEditing.value) return

    clearConnectionsAround(x, y)
    draftUnits.value = draftUnits.value.filter((unit) => !(unit.gridX === x && unit.gridY === y))
    selectedCell.value = null
    showUnitPicker.value = false
  }

  /** Wraps the imported getHorizontalLinkState to use the local GridUnit array type. */
  function getHorizontalLinkStateFor(units: GridUnit[], x: number, y: number): HorizontalLinkState {
    return getHorizontalLinkState(units, x, y)
  }

  /** Wraps the imported getVerticalLinkState to use the local GridUnit array type. */
  function getVerticalLinkStateFor(units: GridUnit[], x: number, y: number): VerticalLinkState {
    return getVerticalLinkState(units, x, y)
  }

  /**
   * Cycles horizontal link through: none → forward (A→B) → backward (B→A) → both → none.
   * Links are directional: each unit's flag is set independently.
   */
  function toggleHorizontalLink(x: number, y: number) {
    if (!isEditing.value) return

    const left = getDraftUnitAt(x, y)
    const right = getDraftUnitAt(x + 1, y)
    if (!left || !right) return

    applyHorizontalLinkCycle(left, right, getHorizontalLinkStateFor(draftUnits.value, x, y))
  }

  /**
   * Cycles vertical link through: none → forward (A→B) → backward (B→A) → both → none.
   */
  function toggleVerticalLink(x: number, y: number) {
    if (!isEditing.value) return

    const top = getDraftUnitAt(x, y)
    const bottom = getDraftUnitAt(x, y + 1)
    if (!top || !bottom) return

    applyVerticalLinkCycle(top, bottom, getVerticalLinkStateFor(draftUnits.value, x, y))
  }

  function getPrimaryDiagonalLinkStateFor(units: GridUnit[], x: number, y: number) {
    return getPrimaryDiagonalLinkState(units, x, y)
  }

  function getSecondaryDiagonalLinkStateFor(units: GridUnit[], x: number, y: number) {
    return getSecondaryDiagonalLinkState(units, x, y)
  }

  function togglePrimaryDiagonalLink(x: number, y: number) {
    if (!isEditing.value) return

    const topLeft = getDraftUnitAt(x, y)
    const bottomRight = getDraftUnitAt(x + 1, y + 1)
    if (!topLeft || !bottomRight) return

    applyPrimaryDiagonalLinkCycle(topLeft, bottomRight, getPrimaryDiagonalLinkStateFor(draftUnits.value, x, y))
  }

  function toggleSecondaryDiagonalLink(x: number, y: number) {
    if (!isEditing.value) return

    const topRight = getDraftUnitAt(x + 1, y)
    const bottomLeft = getDraftUnitAt(x, y + 1)
    if (!topRight || !bottomLeft) return

    applySecondaryDiagonalLinkCycle(topRight, bottomLeft, getSecondaryDiagonalLinkStateFor(draftUnits.value, x, y))
  }

  function isHorizontalLinkActiveFor(units: GridUnit[], x: number, y: number): boolean {
    return getHorizontalLinkStateFor(units, x, y) !== 'none'
  }

  function isVerticalLinkActiveFor(units: GridUnit[], x: number, y: number): boolean {
    return getVerticalLinkStateFor(units, x, y) !== 'none'
  }

  function canToggleHorizontalLink(units: GridUnit[], x: number, y: number): boolean {
    return !!getUnitAtFrom(units, x, y) && !!getUnitAtFrom(units, x + 1, y)
  }

  function canToggleVerticalLink(units: GridUnit[], x: number, y: number): boolean {
    return !!getUnitAtFrom(units, x, y) && !!getUnitAtFrom(units, x, y + 1)
  }

  function canTogglePrimaryDiagonalLink(units: GridUnit[], x: number, y: number): boolean {
    return !!getUnitAtFrom(units, x, y) && !!getUnitAtFrom(units, x + 1, y + 1)
  }

  function canToggleSecondaryDiagonalLink(units: GridUnit[], x: number, y: number): boolean {
    return !!getUnitAtFrom(units, x + 1, y) && !!getUnitAtFrom(units, x, y + 1)
  }

  // ---------------------------------------------------------------------------
  // Advanced flow visualization helpers
  // ---------------------------------------------------------------------------

  /**
   * Returns true when the horizontal link at (x,y)↔(x+1,y) shows real inventory
   * movement (either adjacent unit had inflow or outflow last tick).
   * Used to apply the "live" pulse class to the link connector.
   */
  function isHorizontalLinkLive(units: GridUnit[], x: number, y: number): boolean {
    if (getHorizontalLinkStateFor(units, x, y) === 'none') return false
    const leftInv = getUnitInventorySummary(getUnitAtFrom(units, x, y))
    const rightInv = getUnitInventorySummary(getUnitAtFrom(units, x + 1, y))
    return (
      (leftInv?.lastTickOutflow ?? 0) > 0 ||
      (leftInv?.lastTickInflow ?? 0) > 0 ||
      (rightInv?.lastTickOutflow ?? 0) > 0 ||
      (rightInv?.lastTickInflow ?? 0) > 0
    )
  }

  /**
   * Returns true when the vertical link at (x,y)↔(x,y+1) shows real inventory
   * movement (either adjacent unit had inflow or outflow last tick).
   */
  function isVerticalLinkLive(units: GridUnit[], x: number, y: number): boolean {
    if (getVerticalLinkStateFor(units, x, y) === 'none') return false
    const topInv = getUnitInventorySummary(getUnitAtFrom(units, x, y))
    const bottomInv = getUnitInventorySummary(getUnitAtFrom(units, x, y + 1))
    return (
      (topInv?.lastTickOutflow ?? 0) > 0 ||
      (topInv?.lastTickInflow ?? 0) > 0 ||
      (bottomInv?.lastTickOutflow ?? 0) > 0 ||
      (bottomInv?.lastTickInflow ?? 0) > 0
    )
  }

  /**
   * Returns true when a link connector (horizontal or vertical) is adjacent to the
   * currently selected cell.  Used to apply the "selected-path" highlight class so
   * players can trace the full connection chain out of the unit they clicked.
   *
   * @param direction 'h' for the horizontal link at (lx, ly)↔(lx+1, ly)
   *                  'v' for the vertical link at (lx, ly)↔(lx, ly+1)
   */
  function isLinkConnectedToSelectedCell(direction: 'h' | 'v', lx: number, ly: number): boolean {
    if (!selectedCell.value) return false
    const { x, y } = selectedCell.value
    if (direction === 'h') return (lx === x && ly === y) || (lx + 1 === x && ly === y)
    return (lx === x && ly === y) || (lx === x && ly + 1 === y)
  }

  /**
   * Builds a plain-language flow hint for a horizontal link connector.
   * Returned as the native `title` attribute of the connector element so players
   * see context on hover without any custom tooltip component.
   *
   * Example: "Iron Ore: Mining → Storage (active last tick)"
   */
  function getHorizontalLinkFlowHint(units: GridUnit[], x: number, y: number): string {
    const state = getHorizontalLinkStateFor(units, x, y)
    if (state === 'none') return ''
    const left = getUnitAtFrom(units, x, y)
    const right = getUnitAtFrom(units, x + 1, y)
    if (!left || !right) return ''

    const [fromUnit, toUnit] = state === 'backward' ? [right, left] : [left, right]
    const fromLabel = t(`buildingDetail.unitTypes.${fromUnit.unitType}`)
    const toLabel = t(`buildingDetail.unitTypes.${toUnit.unitType}`)
    const itemLabel = getUnitDisplayLabel(fromUnit) ?? getUnitDisplayLabel(toUnit)
    const status = isHorizontalLinkLive(units, x, y)
      ? t('buildingDetail.linkFlowLive')
      : t('buildingDetail.linkFlowConfigured')

    if (state === 'both') {
      return t('buildingDetail.linkFlowHintBidirectional', {
        a: fromLabel,
        b: toLabel,
        item: itemLabel ?? t('buildingDetail.linkFlowItemGeneric'),
        status,
      })
    }
    return t('buildingDetail.linkFlowHint', {
      from: fromLabel,
      to: toLabel,
      item: itemLabel ?? t('buildingDetail.linkFlowItemGeneric'),
      status,
    })
  }

  /**
   * Builds a plain-language flow hint for a vertical link connector.
   *
   * Example: "Coal: Fuel Purchase → Energy Producing (active last tick)"
   */
  function getVerticalLinkFlowHint(units: GridUnit[], x: number, y: number): string {
    const state = getVerticalLinkStateFor(units, x, y)
    if (state === 'none') return ''
    const top = getUnitAtFrom(units, x, y)
    const bottom = getUnitAtFrom(units, x, y + 1)
    if (!top || !bottom) return ''

    const [fromUnit, toUnit] = state === 'backward' ? [bottom, top] : [top, bottom]
    const fromLabel = t(`buildingDetail.unitTypes.${fromUnit.unitType}`)
    const toLabel = t(`buildingDetail.unitTypes.${toUnit.unitType}`)
    const itemLabel = getUnitDisplayLabel(fromUnit) ?? getUnitDisplayLabel(toUnit)
    const status = isVerticalLinkLive(units, x, y)
      ? t('buildingDetail.linkFlowLive')
      : t('buildingDetail.linkFlowConfigured')

    if (state === 'both') {
      return t('buildingDetail.linkFlowHintBidirectional', {
        a: fromLabel,
        b: toLabel,
        item: itemLabel ?? t('buildingDetail.linkFlowItemGeneric'),
        status,
      })
    }
    return t('buildingDetail.linkFlowHint', {
      from: fromLabel,
      to: toLabel,
      item: itemLabel ?? t('buildingDetail.linkFlowItemGeneric'),
      status,
    })
  }

  /**
   * Returns true when the cell at (cx, cy) is directly linked to the currently
   * selected cell.  Used to apply the "connected" highlight class on grid cells
   * so players can trace the full chain out of a selected unit at a glance.
   */
  function isCellConnectedToSelected(units: GridUnit[], cx: number, cy: number): boolean {
    if (!selectedCell.value) return false
    const { x, y } = selectedCell.value
    if (cx === x && cy === y) return false // the selected cell itself

    // Check if either cell links to the other
    const selected = getUnitAtFrom(units, x, y)
    const candidate = getUnitAtFrom(units, cx, cy)
    if (!selected || !candidate) return false

    const dx = cx - x
    const dy = cy - y

    if (dx === 1 && dy === 0) return getHorizontalLinkStateFor(units, x, y) !== 'none'
    if (dx === -1 && dy === 0) return getHorizontalLinkStateFor(units, cx, cy) !== 'none'
    if (dx === 0 && dy === 1) return getVerticalLinkStateFor(units, x, y) !== 'none'
    if (dx === 0 && dy === -1) return getVerticalLinkStateFor(units, cx, cy) !== 'none'
    if (dx === 1 && dy === 1) return getPrimaryDiagonalLinkStateFor(units, x, y) !== 'none'
    if (dx === -1 && dy === -1) return getPrimaryDiagonalLinkStateFor(units, cx, cy) !== 'none'
    if (dx === 1 && dy === -1) return getSecondaryDiagonalLinkStateFor(units, x, cy) !== 'none'
    if (dx === -1 && dy === 1) return getSecondaryDiagonalLinkStateFor(units, cx, y) !== 'none'

    return false
  }

  function getDraftTicksForUnit(unit: EditableGridUnit): number {
    const baselinePendingUnit = getPendingUnitAt(unit.gridX, unit.gridY)
    const baselinePendingRemoval = getPendingRemovalAt(unit.gridX, unit.gridY)
    const activeUnit = getUnitAtFrom(activeUnits.value, unit.gridX, unit.gridY) as BuildingUnit | undefined

    if (baselinePendingUnit && areUnitsEquivalent(baselinePendingUnit, unit)) {
      return getRemainingTicksFromApplyTick(baselinePendingUnit.appliesAtTick)
    }

    if (baselinePendingRemoval && activeUnit && areUnitsEquivalent(activeUnit, unit)) {
      return getCancelTicks(baselinePendingRemoval.ticksRequired)
    }

    if (baselinePendingUnit && activeUnit && areUnitsEquivalent(activeUnit, unit)) {
      return getCancelTicks(baselinePendingUnit.ticksRequired)
    }

    if (!activeUnit) return UNIT_PLAN_CHANGE_TICKS

    if (activeUnit.unitType !== unit.unitType) {
      return UNIT_PLAN_CHANGE_TICKS
    }

    if (
      activeUnit.linkUp !== unit.linkUp ||
      activeUnit.linkDown !== unit.linkDown ||
      activeUnit.linkLeft !== unit.linkLeft ||
      activeUnit.linkRight !== unit.linkRight ||
      activeUnit.linkUpLeft !== unit.linkUpLeft ||
      activeUnit.linkUpRight !== unit.linkUpRight ||
      activeUnit.linkDownLeft !== unit.linkDownLeft ||
      activeUnit.linkDownRight !== unit.linkDownRight
    ) {
      return LINK_CHANGE_TICKS
    }

    if (
      (activeUnit.resourceTypeId ?? null) !== (unit.resourceTypeId ?? null) ||
      (activeUnit.productTypeId ?? null) !== (unit.productTypeId ?? null) ||
      (activeUnit.minPrice ?? null) !== (unit.minPrice ?? null) ||
      (activeUnit.maxPrice ?? null) !== (unit.maxPrice ?? null) ||
      (activeUnit.purchaseSource ?? null) !== (unit.purchaseSource ?? null) ||
      (activeUnit.saleVisibility ?? null) !== (unit.saleVisibility ?? null) ||
      (activeUnit.budget ?? null) !== (unit.budget ?? null) ||
      (activeUnit.mediaHouseBuildingId ?? null) !== (unit.mediaHouseBuildingId ?? null) ||
      (activeUnit.minQuality ?? null) !== (unit.minQuality ?? null) ||
      (activeUnit.brandScope ?? null) !== (unit.brandScope ?? null) ||
      (activeUnit.vendorLockCompanyId ?? null) !== (unit.vendorLockCompanyId ?? null) ||
      (activeUnit.lockedCityId ?? null) !== (unit.lockedCityId ?? null) ||
      (activeUnit.industryCategory ?? null) !== (unit.industryCategory ?? null)
    ) {
      return LINK_CHANGE_TICKS
    }

    return 0
  }

  function getDraftTicksAt(x: number, y: number): number {
    const draftUnit = getDraftUnitAt(x, y)
    if (draftUnit) {
      return getDraftTicksForUnit(draftUnit)
    }

    const pendingRemoval = getPendingRemovalAt(x, y)
    if (pendingRemoval) {
      return getRemainingTicksFromApplyTick(pendingRemoval.appliesAtTick)
    }

    const pendingUnit = getPendingUnitAt(x, y)
    if (pendingUnit) {
      return getCancelTicks(pendingUnit.ticksRequired)
    }

    return getUnitAtFrom(activeUnits.value, x, y) ? UNIT_PLAN_CHANGE_TICKS : 0
  }

  function getDisplayedTicks(unit: GridUnit): number {
    if ('appliesAtTick' in unit && 'isChanged' in unit && unit.isChanged) {
      return getRemainingTicksFromApplyTick(unit.appliesAtTick)
    }

    return getDraftTicksForUnit(unit as EditableGridUnit)
  }

  function getRemainingTicksFromApplyTick(appliesAtTick: number): number {
    return Math.max(appliesAtTick - currentTick.value, 0)
  }

  function getCancelTicks(baseTicks: number): number {
    return Math.max(Math.ceil(baseTicks * 0.1), 1)
  }

  function isUnitReverting(unit: GridUnit | undefined): boolean {
    if (!unit) return false
    return 'isReverting' in unit ? !!(unit as BuildingConfigurationPlanUnit | EditableGridUnit).isReverting : false
  }

  type UnitComparisonKeys =
    | 'unitType'
    | 'gridX'
    | 'gridY'
    | 'linkUp'
    | 'linkDown'
    | 'linkLeft'
    | 'linkRight'
    | 'linkUpLeft'
    | 'linkUpRight'
    | 'linkDownLeft'
    | 'linkDownRight'
    | 'resourceTypeId'
    | 'productTypeId'
    | 'minPrice'
    | 'maxPrice'
    | 'purchaseSource'
    | 'saleVisibility'
    | 'budget'
    | 'mediaHouseBuildingId'
    | 'minQuality'
    | 'brandScope'
    | 'vendorLockCompanyId'
    | 'lockedCityId'

  function areUnitsEquivalent(left: Pick<EditableGridUnit, UnitComparisonKeys>, right: Pick<EditableGridUnit, UnitComparisonKeys>): boolean {
    return (
      left.unitType === right.unitType &&
      left.gridX === right.gridX &&
      left.gridY === right.gridY &&
      left.linkUp === right.linkUp &&
      left.linkDown === right.linkDown &&
      left.linkLeft === right.linkLeft &&
      left.linkRight === right.linkRight &&
      left.linkUpLeft === right.linkUpLeft &&
      left.linkUpRight === right.linkUpRight &&
      left.linkDownLeft === right.linkDownLeft &&
      left.linkDownRight === right.linkDownRight &&
      (left.resourceTypeId ?? null) === (right.resourceTypeId ?? null) &&
      (left.productTypeId ?? null) === (right.productTypeId ?? null) &&
      (left.minPrice ?? null) === (right.minPrice ?? null) &&
      (left.maxPrice ?? null) === (right.maxPrice ?? null) &&
      (left.purchaseSource ?? null) === (right.purchaseSource ?? null) &&
      (left.saleVisibility ?? null) === (right.saleVisibility ?? null) &&
      (left.budget ?? null) === (right.budget ?? null) &&
      (left.mediaHouseBuildingId ?? null) === (right.mediaHouseBuildingId ?? null) &&
      (left.minQuality ?? null) === (right.minQuality ?? null) &&
      (left.brandScope ?? null) === (right.brandScope ?? null) &&
      (left.vendorLockCompanyId ?? null) === (right.vendorLockCompanyId ?? null) &&
      (left.lockedCityId ?? null) === (right.lockedCityId ?? null)
    )
  }

  function areUnitCollectionsEqual(left: EditableGridUnit[], right: EditableGridUnit[]): boolean {
    if (left.length !== right.length) {
      return false
    }

    const sortedLeft = [...left].sort(compareUnits)
    const sortedRight = [...right].sort(compareUnits)

    return sortedLeft.every((unit, index) => areUnitsEquivalent(unit, sortedRight[index]!))
  }

  function compareUnits(left: EditableGridUnit, right: EditableGridUnit): number {
    if (left.gridY !== right.gridY) return left.gridY - right.gridY
    if (left.gridX !== right.gridX) return left.gridX - right.gridX
    return left.unitType.localeCompare(right.unitType)
  }

  async function storeConfiguration() {
    if (!building.value || saving.value || !hasDraftChanges.value) return

    saving.value = true
    saveError.value = null

    try {
      // 1. Apply any staged unit upgrades first
      const stagedIds = Array.from(draftUpgradeUnitIds.value)
      for (const unitId of stagedIds) {
        await gqlRequest<{ scheduleUnitUpgrade: { id: string; appliesAtTick: number; totalTicksRequired: number } }>(
          `mutation SUU($input: ScheduleUnitUpgradeInput!) {
            scheduleUnitUpgrade(input: $input) { id appliesAtTick totalTicksRequired }
          }`,
          { input: { unitId } },
        )
      }
      draftUpgradeUnitIds.value = new Set()

      // 2. Store structural configuration changes (if any)
      const hasStructuralChanges = !areUnitCollectionsEqual(draftUnits.value, editBaselineUnits.value)
      if (hasStructuralChanges) {
        await gqlRequest<{
          storeBuildingConfiguration: {
            id: string
            appliesAtTick: number
            totalTicksRequired: number
          }
        }>(
          `mutation StoreBuildingConfiguration($input: StoreBuildingConfigurationInput!) {
            storeBuildingConfiguration(input: $input) {
              id
              appliesAtTick
              totalTicksRequired
            }
          }`,
          {
            input: {
              buildingId: building.value.id,
              units: draftUnits.value.map((unit) => ({
                unitType: unit.unitType,
                gridX: unit.gridX,
                gridY: unit.gridY,
                linkUp: unit.linkUp,
                linkDown: unit.linkDown,
                linkLeft: unit.linkLeft,
                linkRight: unit.linkRight,
                linkUpLeft: unit.linkUpLeft,
                linkUpRight: unit.linkUpRight,
                linkDownLeft: unit.linkDownLeft,
                linkDownRight: unit.linkDownRight,
                resourceTypeId: unit.resourceTypeId,
                productTypeId: unit.productTypeId,
                minPrice: unit.minPrice,
                maxPrice: unit.maxPrice,
                purchaseSource: unit.purchaseSource,
                saleVisibility: unit.saleVisibility,
                budget: unit.budget,
                mediaHouseBuildingId: unit.mediaHouseBuildingId,
                minQuality: unit.minQuality,
                brandScope: unit.brandScope,
                vendorLockCompanyId: unit.vendorLockCompanyId,
                lockedCityId: unit.lockedCityId,
                industryCategory: unit.industryCategory,
              })),
            },
          },
        )
      }

      isEditing.value = false
      await loadBuilding()
    } catch (reason: unknown) {
      const code = reason instanceof GraphQLError ? reason.code : undefined
      const raw = reason instanceof Error ? reason.message : String(reason)
      if (code === 'INSUFFICIENT_FUNDS' || raw.includes('INSUFFICIENT_FUNDS')) {
        saveError.value = t('buildingDetail.unitUpgrade.errorInsufficientFunds')
      } else if (code === 'MAX_CONCURRENT_UPGRADES' || raw.includes('MAX_CONCURRENT_UPGRADES')) {
        saveError.value = t('buildingDetail.unitUpgrade.errorMaxConcurrentUpgrades')
      } else if (code === 'UNIT_ALREADY_UPGRADING' || raw.includes('UNIT_ALREADY_UPGRADING')) {
        saveError.value = t('buildingDetail.unitUpgrade.errorUnitAlreadyUpgrading')
      } else {
        saveError.value = raw || t('buildingDetail.storeUpgradeFailed')
      }
    } finally {
      saving.value = false
    }
  }

  function cancelPlan() {
    if (!building.value || cancellingPlan.value || !pendingConfiguration.value) return

    cancellingPlan.value = true
    cancelPlanError.value = null

    gqlRequest<{
      cancelBuildingConfiguration: {
        id: string
        totalTicksRequired: number
      }
    }>(
      `mutation CancelBuildingConfiguration($input: CancelBuildingConfigurationInput!) {
        cancelBuildingConfiguration(input: $input) {
          id
          totalTicksRequired
        }
      }`,
      { input: { buildingId: building.value.id } },
    )
      .then(() => {
        return loadBuilding()
      })
      .catch((reason: unknown) => {
        cancelPlanError.value = reason instanceof Error ? reason.message : t('buildingDetail.cancelPlanFailed')
      })
      .finally(() => {
        cancellingPlan.value = false
      })
  }

  // ── Resource path validation ──

  type ValidationWarning = { key: string; params?: Record<string, unknown> }

  function getLinkedUnits(unit: EditableGridUnit, units: EditableGridUnit[]): EditableGridUnit[] {
    const linked: EditableGridUnit[] = []
    const byPos = new Map(units.map((u) => [`${u.gridX},${u.gridY}`, u]))

    if (unit.linkUp) {
      const u = byPos.get(`${unit.gridX},${unit.gridY - 1}`)
      if (u) linked.push(u)
    }
    if (unit.linkDown) {
      const u = byPos.get(`${unit.gridX},${unit.gridY + 1}`)
      if (u) linked.push(u)
    }
    if (unit.linkLeft) {
      const u = byPos.get(`${unit.gridX - 1},${unit.gridY}`)
      if (u) linked.push(u)
    }
    if (unit.linkRight) {
      const u = byPos.get(`${unit.gridX + 1},${unit.gridY}`)
      if (u) linked.push(u)
    }
    if (unit.linkUpLeft) {
      const u = byPos.get(`${unit.gridX - 1},${unit.gridY - 1}`)
      if (u) linked.push(u)
    }
    if (unit.linkUpRight) {
      const u = byPos.get(`${unit.gridX + 1},${unit.gridY - 1}`)
      if (u) linked.push(u)
    }
    if (unit.linkDownLeft) {
      const u = byPos.get(`${unit.gridX - 1},${unit.gridY + 1}`)
      if (u) linked.push(u)
    }
    if (unit.linkDownRight) {
      const u = byPos.get(`${unit.gridX + 1},${unit.gridY + 1}`)
      if (u) linked.push(u)
    }

    return linked
  }

  const configWarnings = computed<ValidationWarning[]>(() => {
    const units = isEditing.value ? draftUnits.value : (pendingConfiguration.value?.units ?? activeUnits.value).map(cloneUnit)
    const warnings: ValidationWarning[] = []
    if (!building.value || units.length === 0) return warnings

    const purchaseUnits = units.filter((u) => u.unitType === 'PURCHASE')
    const manufacturingUnits = units.filter((u) => u.unitType === 'MANUFACTURING')
    const publicSalesUnits = units.filter((u) => u.unitType === 'PUBLIC_SALES')
    const marketingUnits = units.filter((u) => u.unitType === 'MARKETING')
    const brandingUnits = units.filter((u) => u.unitType === 'BRANDING')
    const miningUnits = units.filter((u) => u.unitType === 'MINING')
    const storageUnits = units.filter((u) => u.unitType === 'STORAGE')
    const productQualityUnits = units.filter((u) => u.unitType === 'PRODUCT_QUALITY')
    const brandQualityUnits = units.filter((u) => u.unitType === 'BRAND_QUALITY')

    // Check unit-specific configuration
    for (const unit of purchaseUnits) {
      if (!unit.resourceTypeId && !unit.productTypeId) {
        warnings.push({ key: 'buildingDetail.warnings.purchaseNoItem', params: { x: unit.gridX, y: unit.gridY } })
      }
    }
    for (const unit of manufacturingUnits) {
      if (!unit.productTypeId) {
        warnings.push({ key: 'buildingDetail.warnings.manufacturingNoProduct', params: { x: unit.gridX, y: unit.gridY } })
      }
    }
    for (const unit of publicSalesUnits) {
      if (!unit.productTypeId && !unit.resourceTypeId) {
        warnings.push({ key: 'buildingDetail.warnings.salesNoItem', params: { x: unit.gridX, y: unit.gridY } })
      }
    }
    for (const unit of marketingUnits) {
      if (!unit.budget || unit.budget <= 0) {
        warnings.push({ key: 'buildingDetail.warnings.marketingNoBudget', params: { x: unit.gridX, y: unit.gridY } })
      }
      if (!unit.mediaHouseBuildingId) {
        warnings.push({ key: 'buildingDetail.warnings.marketingNoMediaHouse', params: { x: unit.gridX, y: unit.gridY } })
      }
    }
    for (const unit of brandingUnits) {
      if (!unit.brandScope) {
        warnings.push({ key: 'buildingDetail.warnings.brandingNoScope', params: { x: unit.gridX, y: unit.gridY } })
      }
    }
    for (const unit of productQualityUnits) {
      if (!unit.productTypeId) {
        warnings.push({ key: 'buildingDetail.warnings.productQualityNoProduct', params: { x: unit.gridX, y: unit.gridY } })
      }
    }
    for (const unit of brandQualityUnits) {
      if (!unit.brandScope) {
        warnings.push({ key: 'buildingDetail.warnings.brandQualityNoScope', params: { x: unit.gridX, y: unit.gridY } })
        continue
      }

      if (['PRODUCT', 'CATEGORY'].includes(unit.brandScope) && !unit.productTypeId && !unit.industryCategory) {
        warnings.push({ key: 'buildingDetail.warnings.brandQualityNoProduct', params: { x: unit.gridX, y: unit.gridY } })
      }
    }

    // Check resource flow connectivity
    if (building.value.type === 'FACTORY') {
      for (const pu of purchaseUnits) {
        const linked = getLinkedUnits(pu, units)
        const hasConsumer = linked.some((u) => ['MANUFACTURING', 'STORAGE'].includes(u.unitType))
        if (!hasConsumer) {
          warnings.push({ key: 'buildingDetail.warnings.purchaseNotLinked', params: { x: pu.gridX, y: pu.gridY } })
        }
      }
      for (const mu of manufacturingUnits) {
        const linked = getLinkedUnits(mu, units)
        const hasOutput = linked.some((u) => ['STORAGE', 'B2B_SALES'].includes(u.unitType))
        if (!hasOutput) {
          warnings.push({ key: 'buildingDetail.warnings.manufacturingNotLinked', params: { x: mu.gridX, y: mu.gridY } })
        }
      }
    }

    if (building.value.type === 'MINE') {
      for (const mu of miningUnits) {
        const linked = getLinkedUnits(mu, units)
        const hasOutput = linked.some((u) => ['STORAGE', 'B2B_SALES'].includes(u.unitType))
        if (!hasOutput) {
          warnings.push({ key: 'buildingDetail.warnings.miningNotLinked', params: { x: mu.gridX, y: mu.gridY } })
        }
      }
    }

    if (building.value.type === 'SALES_SHOP') {
      for (const pu of purchaseUnits) {
        const linked = getLinkedUnits(pu, units)
        const hasConsumer = linked.some((u) => ['PUBLIC_SALES', 'MARKETING', 'STORAGE'].includes(u.unitType))
        if (!hasConsumer) {
          warnings.push({ key: 'buildingDetail.warnings.purchaseNotLinked', params: { x: pu.gridX, y: pu.gridY } })
        }
      }
    }

    // Check unlinked storage units
    for (const su of storageUnits) {
      const linked = getLinkedUnits(su, units)
      if (linked.length === 0) {
        warnings.push({ key: 'buildingDetail.warnings.storageNotLinked', params: { x: su.gridX, y: su.gridY } })
      }
    }

    // Check recipe compatibility: if a Manufacturing unit has a product configured,
    // ensure at least one Purchase unit in the plan supplies a matching resource/product.
    if (building.value.type === 'FACTORY' && isEditing.value) {
      for (const mu of manufacturingUnits) {
        if (!mu.productTypeId) continue
        const product = productTypes.value.find((p) => p.id === mu.productTypeId)
        if (!product || product.recipes.length === 0) continue

        const configuredPurchaseResourceIds = purchaseUnits.filter((pu) => pu.resourceTypeId).map((pu) => pu.resourceTypeId!)

        const configuredPurchaseProductIds = purchaseUnits.filter((pu) => pu.productTypeId).map((pu) => pu.productTypeId!)

        if (configuredPurchaseResourceIds.length === 0 && configuredPurchaseProductIds.length === 0) {
          continue // Incomplete (not incompatible) — missing resource is surfaced by the purchaseNoItem warning above
        }

        const anyRecipeSupplied = product.recipes.some(
          (recipe) =>
            (recipe.resourceType?.id && configuredPurchaseResourceIds.includes(recipe.resourceType.id)) ||
            (recipe.inputProductType?.id && configuredPurchaseProductIds.includes(recipe.inputProductType.id)),
        )

        if (!anyRecipeSupplied) {
          warnings.push({
            key: 'buildingDetail.warnings.recipeMismatch',
            params: { x: mu.gridX, y: mu.gridY, product: product.name },
          })
        }
      }
    }

    return warnings
  })

  // ── Building sale ──

  function openSaleDialog() {
    salePrice.value = building.value?.askingPrice ?? null
    showSaleDialog.value = true
  }

  function closeSaleDialog() {
    showSaleDialog.value = false
  }

  async function setBuildingForSale(forSale: boolean) {
    if (!building.value || savingSale.value) return
    savingSale.value = true
    try {
      await gqlRequest<{ setBuildingForSale: { id: string } }>(
        `mutation SetBuildingForSale($input: SetBuildingForSaleInput!) {
          setBuildingForSale(input: $input) { id isForSale askingPrice }
        }`,
        {
          input: {
            buildingId: building.value.id,
            isForSale: forSale,
            askingPrice: forSale ? salePrice.value : null,
          },
        },
      )
      showSaleDialog.value = false
      await loadBuilding()
    } catch (reason: unknown) {
      error.value = reason instanceof Error ? reason.message : t('buildingDetail.saleFailed')
    } finally {
      savingSale.value = false
    }
  }

  // ── Rent management (APARTMENT / COMMERCIAL) ──

  function openRentDialog() {
    newRentPerSqm.value = building.value?.pendingPricePerSqm ?? building.value?.pricePerSqm ?? null
    rentSaveError.value = null
    showRentDialog.value = true
  }

  function closeRentDialog() {
    showRentDialog.value = false
    rentSaveError.value = null
  }

  async function saveRentPerSqm() {
    if (!building.value || savingRent.value || newRentPerSqm.value === null) return
    savingRent.value = true
    rentSaveError.value = null
    try {
      const result = await gqlRequest<{
        setRentPerSqm: { id: string; pricePerSqm: number | null; pendingPricePerSqm: number | null; pendingPriceActivationTick: number | null }
      }>(
        `mutation SetRentPerSqm($input: SetRentPerSqmInput!) {
          setRentPerSqm(input: $input) {
            id pricePerSqm pendingPricePerSqm pendingPriceActivationTick
          }
        }`,
        {
          input: {
            buildingId: building.value.id,
            rentPerSqm: newRentPerSqm.value,
          },
        },
      )
      // Update local building state with returned values.
      if (building.value) {
        building.value.pendingPricePerSqm = result.setRentPerSqm.pendingPricePerSqm
        building.value.pendingPriceActivationTick = result.setRentPerSqm.pendingPriceActivationTick
      }
      showRentDialog.value = false
    } catch (reason: unknown) {
      rentSaveError.value = reason instanceof Error ? reason.message : t('property.saveFailed')
    } finally {
      savingRent.value = false
    }
  }

  // ── Media house content management ──

  function initContentBudgetInput() {
    contentBudgetInput.value = building.value?.contentBudgetPerTick ?? null
    contentBudgetError.value = null
    contentBudgetSuccess.value = false
  }

  async function saveContentBudget() {
    if (!building.value || savingContentBudget.value) return
    savingContentBudget.value = true
    contentBudgetError.value = null
    contentBudgetSuccess.value = false
    try {
      const result = await gqlRequest<{
        setMediaHouseContentBudget: { id: string; contentBudgetPerTick: number | null; contentValue: number }
      }>(
        `mutation SetMediaHouseContentBudget($input: SetMediaHouseContentBudgetInput!) {
          setMediaHouseContentBudget(input: $input) {
            id contentBudgetPerTick contentValue
          }
        }`,
        {
          input: {
            buildingId: building.value.id,
            contentBudgetPerTick: contentBudgetInput.value ?? 0,
          },
        },
      )
      if (building.value) {
        building.value.contentBudgetPerTick = result.setMediaHouseContentBudget.contentBudgetPerTick
        building.value.contentValue = result.setMediaHouseContentBudget.contentValue
      }
      contentBudgetSuccess.value = true
      // Also refresh the city media houses list to reflect updated content budget.
      await loadCityMediaHouses()
      setTimeout(() => {
        contentBudgetSuccess.value = false
      }, 3000)
    } catch (reason: unknown) {
      contentBudgetError.value = reason instanceof Error ? reason.message : t('mediaHouse.saveFailed')
    } finally {
      savingContentBudget.value = false
    }
  }

  /** Upgrades the current MEDIA_HOUSE building to the next level. */
  async function upgradeMediaHouse() {
    if (!building.value) return
    upgradingMediaHouse.value = true
    mediaHouseUpgradeError.value = null
    mediaHouseUpgradeSuccess.value = false
    try {
      const result = await gqlRequest<{
        upgradeMediaHouse: { id: string; level: number }
      }>(
        `mutation UpgradeMediaHouse($input: UpgradeMediaHouseInput!) {
          upgradeMediaHouse(input: $input) {
            id level
          }
        }`,
        { input: { buildingId: building.value.id } },
      )
      if (building.value) {
        building.value.level = result.upgradeMediaHouse.level
      }
      mediaHouseUpgradeSuccess.value = true
      // Refresh analytics after upgrade.
      await loadMediaHouseAnalytics()
      setTimeout(() => {
        mediaHouseUpgradeSuccess.value = false
      }, 4000)
    } catch (reason: unknown) {
      mediaHouseUpgradeError.value =
        reason instanceof Error ? reason.message : t('mediaHouse.upgradeFailed')
    } finally {
      upgradingMediaHouse.value = false
    }
  }

  /** Loads brand-impact analytics for the current MEDIA_HOUSE building. */
  async function loadMediaHouseAnalytics() {
    if (!building.value || building.value.type !== 'MEDIA_HOUSE') return
    mediaHouseAnalyticsLoading.value = true
    try {
      const data = await gqlRequest<{ mediaHouseAnalytics: MediaHouseAnalyticsResult | null }>(
        `query MediaHouseAnalytics($buildingId: UUID!) {
          mediaHouseAnalytics(buildingId: $buildingId) {
            buildingId buildingName mediaType level contentValue contentRankingPct
            channelMultiplier effectiveMultiplier currentEfficiencyPct nextLevelEfficiencyPct
            isMaxLevel upgradeCostEur upgradeTimeTicks maxLevel
            totalIncomeLast100Ticks avgIncomePerTick
            advertiserCount strategyRating strategyTip
            incomeHistory { tick amount description }
            brandEffects {
              companyId companyName brandScope productName
              brandAwareness marketingQuality effectivenessMultiplierApplied
            }
          }
        }`,
        { buildingId: building.value.id },
      )
      mediaHouseAnalytics.value = data.mediaHouseAnalytics ?? null
    } catch {
      mediaHouseAnalytics.value = null
    } finally {
      mediaHouseAnalyticsLoading.value = false
    }
  }

  /** Extract serialisable unit data from the draft list. */
  function getDraftLayoutUnits(): LayoutUnit[] {
    return draftUnits.value.map((u) => ({
      unitType: u.unitType,
      gridX: u.gridX,
      gridY: u.gridY,
      linkUp: u.linkUp,
      linkDown: u.linkDown,
      linkLeft: u.linkLeft,
      linkRight: u.linkRight,
      linkUpLeft: u.linkUpLeft,
      linkUpRight: u.linkUpRight,
      linkDownLeft: u.linkDownLeft,
      linkDownRight: u.linkDownRight,
      resourceTypeId: u.resourceTypeId,
      productTypeId: u.productTypeId,
      minPrice: u.minPrice,
      maxPrice: u.maxPrice,
      purchaseSource: u.purchaseSource,
      saleVisibility: u.saleVisibility,
      budget: u.budget,
      mediaHouseBuildingId: u.mediaHouseBuildingId,
      minQuality: u.minQuality,
      brandScope: u.brandScope,
      vendorLockCompanyId: u.vendorLockCompanyId,
      lockedCityId: u.lockedCityId,
    }))
  }

  /** Refresh local layouts from localStorage into the reactive ref. */
  function refreshLocalLayouts(): void {
    if (!building.value) return
    localLayouts.value = getLocalLayoutsForType(building.value.type)
  }

  /** Fetch cloud layouts from master API (no-op when not connected). */
  async function refreshMasterLayouts(): Promise<void> {
    if (!masterConnected.value) {
      masterLayouts.value = []
      return
    }
    masterLayoutsLoading.value = true
    masterLayoutsError.value = null
    try {
      const all = await fetchMasterLayouts()
      masterLayouts.value = building.value ? all.filter((l) => l.buildingType === building.value!.type) : all
    } catch (err: unknown) {
      masterLayoutsError.value = err instanceof Error ? err.message : String(err)
    } finally {
      masterLayoutsLoading.value = false
    }
  }

  /** Save current draft as a named layout template. */
  async function saveLayout(): Promise<void> {
    if (!building.value || !layoutName.value.trim()) return
    if (draftUnits.value.length === 0) {
      layoutSaveError.value = t('buildingDetail.layouts.noUnits')
      return
    }

    layoutSaving.value = true
    layoutSaveError.value = null
    layoutSaveSuccess.value = false

    const name = layoutName.value.trim()
    const description = layoutDescription.value.trim() || null
    const buildingType = building.value.type
    const units = getDraftLayoutUnits()

    try {
      if (masterConnected.value) {
        // Find if an existing cloud layout with the same name exists so we can update it
        const existing = masterLayouts.value.find((l) => l.name === name)
        const saved = await saveMasterLayout(name, description, buildingType, units, existing?.id)
        // Update local cache
        const idx = masterLayouts.value.findIndex((l) => l.id === saved.id)
        if (idx >= 0) {
          masterLayouts.value.splice(idx, 1, saved)
        } else {
          masterLayouts.value = [saved, ...masterLayouts.value]
        }
      } else {
        // Fallback: localStorage
        saveLocalLayout(name, description, buildingType, units)
        refreshLocalLayouts()
      }
      layoutName.value = ''
      layoutDescription.value = ''
      layoutSaveSuccess.value = true
      setTimeout(() => {
        layoutSaveSuccess.value = false
      }, 3000)
    } catch (err: unknown) {
      // If cloud failed, persist locally and surface a fallback warning
      if (masterConnected.value) {
        try {
          saveLocalLayout(name, description, buildingType, units)
          refreshLocalLayouts()
          layoutSaveError.value = t('buildingDetail.layouts.masterError')
          layoutSaveSuccess.value = true
          setTimeout(() => {
            layoutSaveSuccess.value = false
            layoutSaveError.value = null
          }, 4000)
        } catch {
          layoutSaveError.value = err instanceof Error ? err.message : String(err)
        }
      } else {
        layoutSaveError.value = err instanceof Error ? err.message : String(err)
      }
    } finally {
      layoutSaving.value = false
    }
  }

  /** Confirm and apply a layout template to the current draft. */
  function requestLoadLayout(layout: BuildingLayoutTemplate): void {
    // Compatibility check: building types must match
    if (layout.buildingType !== building.value?.type) {
      layoutSaveError.value = t('buildingDetail.layouts.incompatible', {
        type: layout.buildingType,
        buildingType: building.value?.type ?? '?',
      })
      return
    }
    // If the draft is non-empty, ask for overwrite confirmation
    if (draftUnits.value.length > 0) {
      overwriteConfirmPending.value = layout
    } else {
      applyLayout(layout)
    }
  }

  function confirmOverwrite(): void {
    if (overwriteConfirmPending.value) {
      applyLayout(overwriteConfirmPending.value)
      overwriteConfirmPending.value = null
    }
  }

  function cancelOverwrite(): void {
    overwriteConfirmPending.value = null
  }

  function applyLayout(layout: BuildingLayoutTemplate): void {
    draftUnits.value = layout.units.map((u, i) => ({
      id: `layout-${i}-${Date.now()}`,
      ...u,
      lockedCityId: u.lockedCityId ?? null,
      industryCategory: u.industryCategory ?? null,
      level: 1,
    }))
  }

  /** Delete a layout template. */
  async function deleteLayout(layout: BuildingLayoutTemplate): Promise<void> {
    layoutDeleteError.value = null
    try {
      if (!layout.isLocal && layout.id) {
        await deleteMasterLayout(layout.id)
        masterLayouts.value = masterLayouts.value.filter((l) => l.id !== layout.id)
      } else {
        deleteLocalLayout(layout.name, layout.buildingType)
        refreshLocalLayouts()
      }
    } catch (err: unknown) {
      layoutDeleteError.value = err instanceof Error ? err.message : String(err)
    }
  }

  // ── Unit config helpers ──

  function getResourceName(id: string | null): string {
    if (!id) return '—'
    const resource = resourceTypes.value.find((candidate) => candidate.id === id)
    return resource ? getLocalizedResourceName(resource, locale.value) : id
  }

  function getProductName(id: string | null): string {
    if (!id) return '—'
    const product = productTypes.value.find((candidate) => candidate.id === id)
    return product ? getLocalizedProductName(product, locale.value) : id
  }

  function getItemSelection(unit: EditableGridUnit | undefined): ItemSelection {
    if (!unit) return null
    if (unit.productTypeId) {
      return { kind: 'product', id: unit.productTypeId }
    }
    if (unit.resourceTypeId) {
      return { kind: 'resource', id: unit.resourceTypeId }
    }
    return null
  }

  function setItemSelection(unit: EditableGridUnit | undefined, selection: ItemSelection) {
    if (!unit) return
    unit.resourceTypeId = selection?.kind === 'resource' ? selection.id : null
    unit.productTypeId = selection?.kind === 'product' ? selection.id : null
    if (!selection) {
      unit.vendorLockCompanyId = null
    }
  }

  function openPurchaseSelector() {
    showPurchaseSelector.value = true
  }

  function closePurchaseSelector() {
    showPurchaseSelector.value = false
  }

  function applyPurchaseSelection(selection: ItemSelection) {
    const unit = selectedDraftPurchaseUnit.value
    if (!unit) return
    setItemSelection(unit, selection)
  }

  function selectPurchaseVendor(companyId: string | null) {
    const unit = selectedDraftPurchaseUnit.value
    if (!unit) return
    unit.vendorLockCompanyId = companyId
    if (building.value?.type === 'SALES_SHOP' && companyId) {
      unit.purchaseSource = 'LOCAL'
    }
  }

  function getFactoryPurchaseSelectableItems(): SelectorItem[] {
    if (building.value?.type !== 'FACTORY') {
      return allSelectableItems.value
    }

    return [...allSelectableItems.value.filter((item) => item.kind === 'resource'), ...allSelectableItems.value.filter((item) => item.kind === 'product' && intermediateProductIds.value.has(item.id))]
  }

  function getDirectlyConnectedUnits(unit: EditableGridUnit, units: EditableGridUnit[]): EditableGridUnit[] {
    return units.filter((candidate) => {
      if (candidate.id === unit.id) return false

      if (candidate.gridX === unit.gridX && candidate.gridY === unit.gridY - 1) {
        return unit.linkUp || candidate.linkDown
      }
      if (candidate.gridX === unit.gridX && candidate.gridY === unit.gridY + 1) {
        return unit.linkDown || candidate.linkUp
      }
      if (candidate.gridX === unit.gridX - 1 && candidate.gridY === unit.gridY) {
        return unit.linkLeft || candidate.linkRight
      }
      if (candidate.gridX === unit.gridX + 1 && candidate.gridY === unit.gridY) {
        return unit.linkRight || candidate.linkLeft
      }
      if (candidate.gridX === unit.gridX - 1 && candidate.gridY === unit.gridY - 1) {
        return unit.linkUpLeft || candidate.linkDownRight
      }
      if (candidate.gridX === unit.gridX + 1 && candidate.gridY === unit.gridY - 1) {
        return unit.linkUpRight || candidate.linkDownLeft
      }
      if (candidate.gridX === unit.gridX - 1 && candidate.gridY === unit.gridY + 1) {
        return unit.linkDownLeft || candidate.linkUpRight
      }
      if (candidate.gridX === unit.gridX + 1 && candidate.gridY === unit.gridY + 1) {
        return unit.linkDownRight || candidate.linkUpLeft
      }
      return false
    })
  }

  function getReachableInputSelections(unit: EditableGridUnit | undefined): Set<string> {
    const selected = new Set<string>()
    if (!unit) return selected

    const queue = [unit]
    const visited = new Set<string>()

    while (queue.length > 0) {
      const current = queue.shift()!
      const key = `${current.gridX},${current.gridY}`
      if (visited.has(key)) continue
      visited.add(key)

      if (current.id !== unit.id && ['PURCHASE', 'MINING', 'STORAGE'].includes(current.unitType)) {
        if (current.resourceTypeId) selected.add(`resource:${current.resourceTypeId}`)
        if (current.productTypeId) selected.add(`product:${current.productTypeId}`)
      }

      for (const next of getDirectlyConnectedUnits(current, draftUnits.value)) {
        queue.push(next)
      }
    }

    return selected
  }

  function getManufacturingSelectableItems(unit: EditableGridUnit | undefined): SelectorItem[] {
    if (!unit) return []
    const reachableInputs = getReachableInputSelections(unit)
    return productTypes.value
      .filter((product) => product.recipes.length > 0)
      .filter((product) =>
        product.recipes.every((recipe) => {
          if (recipe.resourceType?.id) {
            return reachableInputs.has(`resource:${recipe.resourceType.id}`)
          }
          if (recipe.inputProductType?.id) {
            return reachableInputs.has(`product:${recipe.inputProductType.id}`)
          }
          return false
        }),
      )
      .map((product) => ({
        kind: 'product' as const,
        id: product.id,
        name: getLocalizedProductName(product, locale.value),
        imageUrl: getProductImageUrl(product),
        description: getLocalizedProductDescription(product, locale.value),
        helperText: isProductLocked(product) ? t('catalog.proDetail') : null,
        groupLabel: t('buildingDetail.selector.availableOutputs'),
        unitSymbol: product.unitSymbol,
        badge: product.isProOnly ? t('catalog.proBadge') : null,
        disabled: isProductLocked(product),
      }))
  }

  function getBrandScopeLabel(scope: string | null): string {
    if (!scope) return t('buildingDetail.config.none')

    switch (scope) {
      case 'PRODUCT':
        return t('buildingDetail.config.scopeProduct')
      case 'CATEGORY':
        return t('buildingDetail.config.scopeCategory')
      case 'COMPANY':
        return t('buildingDetail.config.scopeCompany')
      default:
        return scope
    }
  }

  function formatUnitMetric(label: string, value: string): string {
    return `${label}: ${value}`
  }

  function getUnitConfiguredItemLabel(unit: GridUnit | undefined): string | null {
    if (!unit) return null
    const item = getUnitConfiguredItemId(unit)
    if (!item) return null
    return item.kind === 'product' ? getProductName(item.id) : getResourceName(item.id)
  }

  function getUnitPrimaryMetric(unit: GridUnit | undefined): string | null {
    if (!unit) return null
    const metric = getUnitPriceMetric(unit)
    if (!metric) return null
    if (metric.kind === 'scope') {
      return formatUnitMetric(t('buildingDetail.gridMetrics.scope'), getBrandScopeLabel(metric.value as string))
    }
    return formatUnitMetric(t(`buildingDetail.gridMetrics.${metric.kind}`), formatCurrency(metric.value as number))
  }

  function getUnitInventorySummary(unit: GridUnit | undefined): BuildingUnitInventorySummary | undefined {
    if (!unit) return undefined
    const directSummary = unitInventorySummaries.value.find((summary) => summary.buildingUnitId === unit.id)
    if (directSummary) return directSummary

    const activeUnit = activeUnits.value.find((candidate) => candidate.gridX === unit.gridX && candidate.gridY === unit.gridY)

    if (!activeUnit) return undefined
    return unitInventorySummaries.value.find((summary) => summary.buildingUnitId === activeUnit.id)
  }

  function getUnitInventories(unit: GridUnit | undefined): BuildingUnitInventory[] {
    if (!unit) return []
    const directInventories = unitInventories.value.filter((inventory) => inventory.buildingUnitId === unit.id)
    if (directInventories.length > 0) {
      return [...directInventories].sort((left, right) => right.quantity - left.quantity)
    }

    const activeUnit = activeUnits.value.find((candidate) => candidate.gridX === unit.gridX && candidate.gridY === unit.gridY)

    if (!activeUnit) return []
    return unitInventories.value.filter((inventory) => inventory.buildingUnitId === activeUnit.id).sort((left, right) => right.quantity - left.quantity)
  }

  function getResolvedLiveUnitId(unit: GridUnit | undefined): string | null {
    if (!unit) return null

    const hasDirectLiveData = unitInventories.value.some((inventory) => inventory.buildingUnitId === unit.id) || unitResourceHistories.value.some((entry) => entry.buildingUnitId === unit.id)
    if (hasDirectLiveData) {
      return unit.id
    }

    const activeUnit = activeUnits.value.find((candidate) => candidate.gridX === unit.gridX && candidate.gridY === unit.gridY)

    return activeUnit?.id ?? unit.id
  }

  function getUnitResourceHistory(unit: GridUnit | undefined): BuildingUnitResourceHistoryPoint[] {
    const resolvedUnitId = getResolvedLiveUnitId(unit)
    if (!resolvedUnitId) return []

    return unitResourceHistories.value.filter((entry) => entry.buildingUnitId === resolvedUnitId).sort((left, right) => left.tick - right.tick)
  }

  function getHistoryItemLabel(resourceTypeId: string | null | undefined, productTypeId: string | null | undefined): string {
    if (resourceTypeId) {
      return getResourceName(resourceTypeId)
    }

    if (productTypeId) {
      return getProductName(productTypeId)
    }

    return t('buildingDetail.inventory.item')
  }

  function getUnitResourceHistoryItemOptions(unit: GridUnit | undefined): UnitResourceHistoryItemOption[] {
    if (!unit) return []

    const options = new Map<string, UnitResourceHistoryItemOption>()
    const order = new Map<string, number>()
    let nextOrder = 0

    for (const inventory of getUnitInventories(unit)) {
      const key = getUnitResourceHistoryItemKey(inventory.resourceTypeId, inventory.productTypeId)
      if (!key || options.has(key)) {
        continue
      }

      options.set(key, {
        key,
        label: getHistoryItemLabel(inventory.resourceTypeId, inventory.productTypeId),
      })
      order.set(key, nextOrder++)
    }

    for (const entry of getUnitResourceHistory(unit)) {
      const key = getUnitResourceHistoryItemKey(entry.resourceTypeId, entry.productTypeId)
      if (!key || options.has(key)) {
        continue
      }

      options.set(key, {
        key,
        label: getHistoryItemLabel(entry.resourceTypeId, entry.productTypeId),
      })
      order.set(key, nextOrder++)
    }

    return Array.from(options.values()).sort((left, right) => (order.get(left.key) ?? Number.MAX_SAFE_INTEGER) - (order.get(right.key) ?? Number.MAX_SAFE_INTEGER))
  }

  function getSelectedUnitResourceHistory(unit: GridUnit | undefined): BuildingUnitResourceHistoryPoint[] {
    const selectedKey = selectedHistoryItemKey.value
    if (!selectedKey) return []

    return getUnitResourceHistory(unit).filter((entry) => getUnitResourceHistoryItemKey(entry.resourceTypeId, entry.productTypeId) === selectedKey)
  }

  function getUnitInventoryItemCount(unit: GridUnit | undefined): number {
    return getUnitInventories(unit).length
  }

  function getUnitOperationalStatus(unit: GridUnit | undefined): BuildingUnitOperationalStatus | null {
    if (!unit) return null
    return unitOperationalStatuses.value.find((s) => s.buildingUnitId === unit.id) ?? null
  }

  const selectedActiveUnitOperationalStatus = computed<BuildingUnitOperationalStatus | null>(() => {
    if (!selectedCell.value) return null
    const unit = getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y)
    return getUnitOperationalStatus(unit)
  })

  /** The pending upgrade plan unit for the currently selected cell, if any. */
  const selectedCellPendingUpgrade = computed<{
    level: number
    ticksRemaining: number
  } | null>(() => {
    if (!selectedCell.value || !building.value?.pendingConfiguration) return null
    const plan = building.value.pendingConfiguration
    // Prefer the authoritative tick from the game-state store; fall back to the
    // local `currentTick` ref which is set from the same source during loadBuilding().
    const tick = gameStateStore.gameState?.currentTick ?? currentTick.value
    const planUnit = plan.units.find((u) => u.gridX === selectedCell.value!.x && u.gridY === selectedCell.value!.y && u.isChanged)
    if (!planUnit) return null
    const activeUnit = getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y)
    if (!activeUnit || planUnit.level <= activeUnit.level) return null
    return {
      level: planUnit.level,
      ticksRemaining: Math.max(0, planUnit.appliesAtTick - tick),
    }
  })

  /**
   * Returns true if the cell at (x, y) has an active level upgrade in progress
   * (a pending plan unit with isChanged=true, ticksRequired>0, and applies in the future).
   */
  function isCellUnderUpgrade(x: number, y: number): boolean {
    if (!building.value?.pendingConfiguration) return false
    const plan = building.value.pendingConfiguration
    const tick = gameStateStore.gameState?.currentTick ?? currentTick.value
    const planUnit = plan.units.find((u) => u.gridX === x && u.gridY === y && u.isChanged && u.ticksRequired > 0)
    if (!planUnit) return false
    const activeUnit = getUnitAtFrom(activeUnits.value, x, y)
    if (!activeUnit) return false
    // Upgrade is in progress when the plan applies this tick or later and the level is increasing.
    // Using >= matches the backend rule: BuildingUpgradePhase runs at order 100 (after operational
    // phases), so a unit due on the current tick is still offline during purchasing/sales/etc.
    return planUnit.appliesAtTick >= tick && planUnit.level > activeUnit.level
  }

  /** Upgrade info for the currently selected cell unit (cached from last fetch). */
  const selectedCellUpgradeInfo = computed<import('@/types').UnitUpgradeInfo | null>(() => {
    if (!selectedCell.value) return null
    const unit = getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y)
    if (!unit || !unit.id) return null
    if (unitUpgradeInfoCache.value?.unitId === unit.id) return unitUpgradeInfoCache.value
    return null
  })

  /** Whether the currently selected cell's unit has been staged for upgrade via "Stage Upgrade". */
  const isSelectedCellStaged = computed(() => (selectedCellUpgradeInfo.value ? draftUpgradeUnitIds.value.has(selectedCellUpgradeInfo.value.unitId) : false))

  function toggleStagedUpgrade(unitId: string) {
    const next = new Set(draftUpgradeUnitIds.value)
    if (next.has(unitId)) {
      next.delete(unitId)
    } else {
      next.add(unitId)
    }
    draftUpgradeUnitIds.value = next
    unitUpgradeError.value = null
  }

  /**
   * List of all active units in this building that are currently under a level upgrade,
   * used to show the concurrent-upgrades summary panel.
   */
  const allUnitsUnderUpgrade = computed<Array<{ unitType: string; gridX: number; gridY: number; toLevel: number; ticksRemaining: number }>>(() => {
    if (!building.value?.pendingConfiguration) return []
    const plan = building.value.pendingConfiguration
    const tick = gameStateStore.gameState?.currentTick ?? currentTick.value
    return plan.units
      .filter((pu) => pu.isChanged && pu.ticksRequired > 0 && pu.appliesAtTick >= tick)
      .flatMap((pu) => {
        const activeUnit = getUnitAtFrom(activeUnits.value, pu.gridX, pu.gridY)
        if (!activeUnit || pu.level <= activeUnit.level) return []
        return [
          {
            unitType: pu.unitType,
            gridX: pu.gridX,
            gridY: pu.gridY,
            toLevel: pu.level,
            ticksRemaining: Math.max(0, pu.appliesAtTick - tick),
          },
        ]
      })
  })

  function formatCurrency(value: number | null | undefined): string {
    const amount = value ?? 0
    return new Intl.NumberFormat(locale.value, {
      style: 'currency',
      currency: cityCurrencyCode.value,
      minimumFractionDigits: Number.isInteger(amount) ? 0 : 2,
      maximumFractionDigits: 2,
    }).format(amount)
  }

  function getPurchaseVendorTransitLabel(transitCostPerUnit: number): string {
    return t('buildingDetail.purchaseSelector.vendorTransit', { price: formatCurrency(transitCostPerUnit) })
  }

  function getCityName(cityId: string | null | undefined): string {
    if (!cityId) return t('common.notAvailable')
    return cities.value.find((city) => city.id === cityId)?.name ?? t('common.notAvailable')
  }

  function formatGpsLocation(latitude: number | null | undefined, longitude: number | null | undefined): string {
    if (latitude == null || longitude == null) return t('common.notAvailable')

    const latitudeDirection = latitude >= 0 ? 'N' : 'S'
    const longitudeDirection = longitude >= 0 ? 'E' : 'W'
    return `${Math.abs(latitude).toFixed(5)}°${latitudeDirection}, ${Math.abs(longitude).toFixed(5)}°${longitudeDirection}`
  }

  function getConfiguredItemImageUrl(unit: GridUnit | undefined): string | null {
    const resourceTypeId = unit && 'resourceTypeId' in unit ? unit.resourceTypeId : null
    if (resourceTypeId) {
      const resource = resourceTypes.value.find((r) => r.id === resourceTypeId)
      return resource ? getResourceImageUrl(resource) : null
    }
    const productTypeId = unit && 'productTypeId' in unit ? unit.productTypeId : null
    if (!productTypeId) return null
    const product = productTypes.value.find((p) => p.id === productTypeId)
    return product ? getProductImageUrl(product) : null
  }

  function getConfiguredItemMonogram(unit: GridUnit | undefined): string {
    const label = getUnitConfiguredItemLabel(unit)
    if (!label) return '?'

    return label
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('')
  }

  function getInventoryItemImageUrl(inventory: BuildingUnitInventory): string | null {
    if (inventory.resourceTypeId) {
      const resource = resourceTypes.value.find((r) => r.id === inventory.resourceTypeId)
      return resource ? getResourceImageUrl(resource) : null
    }
    if (inventory.productTypeId) {
      const product = productTypes.value.find((p) => p.id === inventory.productTypeId)
      return product ? getProductImageUrl(product) : null
    }
    return null
  }

  function getPrimaryInventoryItem(unit: GridUnit | undefined): BuildingUnitInventory | undefined {
    return getUnitInventories(unit)[0]
  }

  function getUnitDisplayLabel(unit: GridUnit | undefined): string | null {
    const primaryInventory = getPrimaryInventoryItem(unit)
    if (primaryInventory) {
      const extraItems = getUnitInventoryItemCount(unit) - 1
      const primaryName = getInventoryItemName(primaryInventory)
      return extraItems > 0 ? `${primaryName} ${t('buildingDetail.inventory.moreItems', { count: extraItems })}` : primaryName
    }

    return getUnitConfiguredItemLabel(unit)
  }

  function getUnitDisplayImageUrl(unit: GridUnit | undefined): string | null {
    const primaryInventory = getPrimaryInventoryItem(unit)
    if (primaryInventory) {
      return getInventoryItemImageUrl(primaryInventory)
    }

    return getConfiguredItemImageUrl(unit)
  }

  function getUnitDisplayMonogram(unit: GridUnit | undefined): string {
    const primaryInventory = getPrimaryInventoryItem(unit)
    if (primaryInventory) {
      return getInventoryItemMonogram(primaryInventory)
    }

    return getConfiguredItemMonogram(unit)
  }

  function getInventoryItemName(inventory: BuildingUnitInventory): string {
    if (inventory.resourceTypeId) {
      return getResourceName(inventory.resourceTypeId)
    }
    if (inventory.productTypeId) {
      return getProductName(inventory.productTypeId)
    }
    return 'Unknown'
  }

  function getInventoryItemMonogram(inventory: BuildingUnitInventory): string {
    const name = getInventoryItemName(inventory)
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('')
  }

  function getInventoryItemSourcingCostTotal(inventory: BuildingUnitInventory): number {
    return inventory.sourcingCostTotal ?? 0
  }

  function getInventoryItemSourcingCostLabel(inventory: BuildingUnitInventory): string {
    return formatCurrency(getInventoryItemSourcingCostTotal(inventory))
  }

  function getInventoryItemSourcingCostPerUnitLabel(inventory: BuildingUnitInventory): string | null {
    const sourcingCostPerUnit = inventory.sourcingCostPerUnit ?? getInventorySourcingCostPerUnit(inventory.quantity, inventory.sourcingCostTotal)
    return sourcingCostPerUnit == null ? null : t('buildingDetail.inventory.perUnit', { value: formatCurrency(sourcingCostPerUnit) })
  }

  function getUnitInventoryCost(unit: GridUnit | undefined): number | null {
    if (!unit) return null
    const summary = getUnitInventorySummary(unit)
    if (summary) return summary.totalSourcingCost

    const inventories = getUnitInventories(unit)
    if (inventories.length === 0) return null

    return getTotalInventorySourcingCost(
      inventories.map((inventory) => ({
        quantity: inventory.quantity,
        sourcingCostTotal: getInventoryItemSourcingCostTotal(inventory),
      })),
    )
  }

  function getUnitInventoryCostLabel(unit: GridUnit | undefined): string | null {
    const value = getUnitInventoryCost(unit)
    return value == null ? null : formatCurrency(value)
  }

  function getUnitNextTickOperatingCost(unit: GridUnit | undefined): number | null {
    const status = getUnitOperationalStatus(unit)
    if (!status) return null
    const labor = status.nextTickLaborCost ?? 0
    const energy = status.nextTickEnergyCost ?? 0
    const total = labor + energy
    return total > 0 ? total : null
  }

  function getUnitNextTickOperatingCostLabel(unit: GridUnit | undefined): string | null {
    const cost = getUnitNextTickOperatingCost(unit)
    return cost == null ? null : formatCurrency(cost)
  }

  function getDraftUnitConstructionCost(unit: GridUnit | undefined): number {
    if (!unit) return 0
    const activeUnit = getUnitAtFrom(activeUnits.value, unit.gridX, unit.gridY)
    // Apply the building city's FX rate so costs are shown in local currency (e.g. CZK, INR).
    return Math.round(getPlannedUnitConstructionCost(activeUnit, unit) * cityFxRate.value * 100) / 100
  }

  function getDraftUnitConstructionCostLabel(unit: GridUnit | undefined): string | null {
    const cost = getDraftUnitConstructionCost(unit)
    return cost > 0 ? formatCurrency(cost) : null
  }

  function getPurchaseUnitResourceTypeId(unit: GridUnit | undefined): string | null {
    return unit && 'resourceTypeId' in unit ? unit.resourceTypeId : null
  }

  function getPurchaseUnitSource(unit: GridUnit | undefined): string | null {
    return unit && 'purchaseSource' in unit ? unit.purchaseSource : null
  }

  /** Returns pre-computed flow segments for the given grid unit's inventory summary. */
  function getUnitFlowSegments(unit: GridUnit | undefined) {
    const inv = getUnitInventorySummary(unit)
    return getFlowSegments(inv?.fillPercent, inv?.capacity, inv?.lastTickInflow, inv?.lastTickOutflow)
  }

  /**
   * Returns the CSS class for the outflow segment of a capacity bar.
   * PUBLIC_SALES units use a 'sold' animated class to distinguish retail sales from generic outflow.
   */
  function getCellOutflowClass(unit: GridUnit | undefined): string {
    return unit?.unitType === 'PUBLIC_SALES' ? 'cell-capacity-sold' : 'cell-capacity-outflow'
  }

  /**
   * Returns a tooltip string for the capacity bar showing exact quantity, capacity,
   * and last-tick inflow/outflow values.
   */
  function getCellCapacityTooltip(unit: GridUnit | undefined): string {
    const inv = getUnitInventorySummary(unit)
    if (!inv?.capacity) return ''
    const fill = `${formatUnitQuantity(inv.quantity)}/${formatUnitQuantity(inv.capacity)}`
    const pct = formatPercent(inv.fillPercent)
    let tooltip = `${fill} (${pct})`
    if (inv.lastTickInflow != null && inv.lastTickInflow > 0) {
      tooltip += ` ↑${formatUnitQuantity(inv.lastTickInflow)}`
    }
    if (inv.lastTickOutflow != null && inv.lastTickOutflow > 0) {
      tooltip += ` ↓${formatUnitQuantity(inv.lastTickOutflow)}`
    }
    return tooltip
  }

  /** Flow segments for the currently selected active-view unit (avoids repeated calls in the detail panel). */
  const selectedActiveUnitFlowSegments = computed(() => {
    if (!selectedCell.value) return getFlowSegments(null, null, null, null)
    return getUnitFlowSegments(getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y))
  })

  /** Flow segments for the currently selected planned-view unit (avoids repeated calls in the detail panel). */
  const selectedPlannedUnitFlowSegments = computed(() => {
    if (!selectedCell.value) return getFlowSegments(null, null, null, null)
    return getUnitFlowSegments(getUnitAtFrom(plannedUnits.value, selectedCell.value.x, selectedCell.value.y))
  })

  function getGridCellAriaLabel(unit: GridUnit | undefined): string {
    if (!unit) return t('buildingDetail.cellAriaLabelEmpty')

    const typePart = t(`buildingDetail.unitTypes.${unit.unitType}`)
    const itemLabel = getUnitConfiguredItemLabel(unit)
    const metric = getUnitPrimaryMetric(unit)
    const inventory = getUnitInventorySummary(unit)

    const itemPart = itemLabel ? t('buildingDetail.cellAriaLabelItem', { item: itemLabel }) : ''
    const metricPart = metric ? t('buildingDetail.cellAriaLabelMetric', { metric }) : ''
    let fillPart = ''
    let inflowPart = ''
    let outflowPart = ''
    if (inventory?.capacity) {
      fillPart = t('buildingDetail.cellAriaLabelFill', {
        fill: formatPercent(inventory.fillPercent),
      })
      if (inventory.lastTickInflow != null && inventory.lastTickInflow > 0) {
        inflowPart = t('buildingDetail.cellAriaLabelInflow', {
          value: formatUnitQuantity(inventory.lastTickInflow),
        })
      }
      if (inventory.lastTickOutflow != null && inventory.lastTickOutflow > 0) {
        outflowPart = t('buildingDetail.cellAriaLabelOutflow', {
          value: formatUnitQuantity(inventory.lastTickOutflow),
        })
      }
    }
    return `${typePart}${itemPart}${metricPart}${fillPart}${inflowPart}${outflowPart}`
  }

  function updateSelectedUnitConfig(field: string, value: unknown) {
    if (!selectedCell.value || !isEditing.value) return
    const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
    if (!unit) return
    const sanitized = typeof value === 'number' && isNaN(value) ? null : value
    ;(unit as Record<string, unknown>)[field] = sanitized

    // When procurement mode changes away from EXCHANGE, clear the city lock.
    // LockedCityId only applies to EXCHANGE mode – keeping it silently restricts OPTIMAL sourcing.
    if (field === 'purchaseSource' && value !== 'EXCHANGE') {
      ;(unit as Record<string, unknown>)['lockedCityId'] = null
    }

    // When brand scope changes on a BRAND_QUALITY unit, clear the no-longer-relevant selection field.
    if (field === 'brandScope') {
      if (value !== 'PRODUCT') {
        ;(unit as Record<string, unknown>)['productTypeId'] = null
      }
      if (value !== 'CATEGORY') {
        ;(unit as Record<string, unknown>)['industryCategory'] = null
      }
    }
  }

  async function loadUnitInventorySummaries(requestId?: number) {
    if (!auth.token) {
      if (requestId == null || requestId === activeBuildingLoadRequest) {
        unitInventorySummaries.value = []
      }
      return
    }

    try {
      const data = await gqlRequest<{ buildingUnitInventorySummaries: BuildingUnitInventorySummary[] }>(
        `query BuildingUnitInventorySummaries($buildingId: UUID!) {
          buildingUnitInventorySummaries(buildingId: $buildingId) {
            buildingUnitId
            quantity
            capacity
            fillPercent
            averageQuality
            totalSourcingCost
            sourcingCostPerUnit
            lastTickInflow
            lastTickOutflow
          }
        }`,
        { buildingId: buildingId.value },
      )
      if (requestId != null && requestId !== activeBuildingLoadRequest) {
        return
      }

      if (!deepEqual(unitInventorySummaries.value, data.buildingUnitInventorySummaries)) {
        unitInventorySummaries.value = data.buildingUnitInventorySummaries
      }
    } catch {
      if (requestId != null && requestId !== activeBuildingLoadRequest) {
        return
      }

      if (unitInventorySummaries.value.length > 0) {
        unitInventorySummaries.value = []
      }
    }
  }

  async function loadUnitInventories(requestId?: number) {
    if (!auth.token) {
      if (requestId == null || requestId === activeBuildingLoadRequest) {
        unitInventories.value = []
      }
      return
    }

    try {
      const data = await gqlRequest<{ buildingUnitInventories: BuildingUnitInventory[] }>(
        `query BuildingUnitInventories($buildingId: UUID!) {
          buildingUnitInventories(buildingId: $buildingId) {
            id
            buildingUnitId
            resourceTypeId
            productTypeId
            quantity
            sourcingCostTotal
            sourcingCostPerUnit
            quality
          }
        }`,
        { buildingId: buildingId.value },
      )
      if (requestId != null && requestId !== activeBuildingLoadRequest) {
        return
      }

      if (!deepEqual(unitInventories.value, data.buildingUnitInventories)) {
        unitInventories.value = data.buildingUnitInventories
      }
    } catch {
      if (requestId != null && requestId !== activeBuildingLoadRequest) {
        return
      }

      if (unitInventories.value.length > 0) {
        unitInventories.value = []
      }
    }
  }

  async function loadUnitResourceHistories(requestId?: number) {
    if (!auth.token) {
      if (requestId == null || requestId === activeBuildingLoadRequest) {
        unitResourceHistories.value = []
      }
      return
    }

    try {
      const data = await gqlRequest<{ buildingUnitResourceHistories: BuildingUnitResourceHistoryPoint[] }>(
        `query BuildingUnitResourceHistories($buildingId: UUID!, $limit: Int) {
          buildingUnitResourceHistories(buildingId: $buildingId, limit: $limit) {
            buildingUnitId
            resourceTypeId
            productTypeId
            tick
            inflowQuantity
            outflowQuantity
            consumedQuantity
            producedQuantity
          }
        }`,
        { buildingId: buildingId.value, limit: 60 },
      )
      if (requestId != null && requestId !== activeBuildingLoadRequest) {
        return
      }

      if (!deepEqual(unitResourceHistories.value, data.buildingUnitResourceHistories)) {
        unitResourceHistories.value = data.buildingUnitResourceHistories
      }
    } catch {
      if (requestId != null && requestId !== activeBuildingLoadRequest) {
        return
      }

      if (unitResourceHistories.value.length > 0) {
        unitResourceHistories.value = []
      }
    }
  }

  async function loadGlobalExchangeOffers() {
    const requestId = ++activeExchangeOffersRequest
    const unit = selectedPurchaseUnit.value
    const resourceTypeId = getPurchaseUnitResourceTypeId(unit)
    const purchaseSource = getPurchaseUnitSource(unit)

    if (!building.value?.cityId || !resourceTypeId || !['EXCHANGE', 'OPTIMAL'].includes(purchaseSource ?? '')) {
      if (requestId === activeExchangeOffersRequest) {
        if (exchangeOffers.value.length > 0) {
          exchangeOffers.value = []
        }
        exchangeOffersLoading.value = false
      }
      return
    }

    exchangeOffersLoading.value = true
    try {
      const data = await gqlRequest<{ globalExchangeOffers: GlobalExchangeOffer[] }>(
        `query GlobalExchangeOffers($destinationCityId: UUID!, $resourceTypeId: UUID) {
          globalExchangeOffers(destinationCityId: $destinationCityId, resourceTypeId: $resourceTypeId) {
            cityId
            cityName
            resourceTypeId
            resourceName
            resourceSlug
            unitSymbol
            localAbundance
            exchangePricePerUnit
            estimatedQuality
            transitCostPerUnit
            deliveredPricePerUnit
            distanceKm
          }
        }`,
        {
          destinationCityId: building.value.cityId,
          resourceTypeId,
        },
      )
      if (requestId !== activeExchangeOffersRequest) {
        return
      }

      if (!deepEqual(exchangeOffers.value, data.globalExchangeOffers)) {
        exchangeOffers.value = data.globalExchangeOffers
      }
    } catch {
      if (requestId !== activeExchangeOffersRequest) {
        return
      }

      if (exchangeOffers.value.length > 0) {
        exchangeOffers.value = []
      }
    } finally {
      if (requestId === activeExchangeOffersRequest) {
        exchangeOffersLoading.value = false
      }
    }
  }

  async function loadProcurementPreview(isRefresh = false) {
    const unit = selectedPurchaseUnit.value
    if (!unit || !('id' in unit)) {
      procurementPreview.value = null
      procurementPreviewLoading.value = false
      return
    }
    const unitId = unit.id
    const requestId = ++activeProcurementPreviewRequest

    if (!isRefresh || procurementPreview.value == null) {
      procurementPreviewLoading.value = true
    }
    try {
      const data = await gqlRequest<{ procurementPreview: ProcurementPreview | null }>(
        `query ProcurementPreview($unitId: UUID!) {
          procurementPreview(buildingUnitId: $unitId) {
            sourceType
            sourceCityId
            sourceCityName
            sourceVendorCompanyId
            sourceVendorName
            exchangePricePerUnit
            transitCostPerUnit
            deliveredPricePerUnit
            estimatedQuality
            canExecute
            blockReason
            blockMessage
          }
        }`,
        { unitId },
      )
      if (requestId !== activeProcurementPreviewRequest) return
      procurementPreview.value = data.procurementPreview
    } catch {
      if (requestId !== activeProcurementPreviewRequest) return
      procurementPreview.value = null
    } finally {
      if (requestId === activeProcurementPreviewRequest) {
        procurementPreviewLoading.value = false
      }
    }
  }

  async function loadSourcingCandidates(isRefresh = false) {
    const unit = selectedPurchaseUnit.value
    if (!unit || !('id' in unit)) {
      sourcingCandidates.value = []
      sourcingCandidatesLoading.value = false
      return
    }
    const unitId = unit.id
    const requestId = ++activeSourcingCandidatesRequest

    if (!isRefresh || sourcingCandidates.value.length === 0) {
      sourcingCandidatesLoading.value = true
    }
    try {
      const data = await gqlRequest<{ sourcingCandidates: SourcingCandidate[] }>(
        `query SourcingCandidates($unitId: UUID!) {
          sourcingCandidates(buildingUnitId: $unitId) {
            sourceType
            sourceCityId
            sourceCityName
            sourceVendorCompanyId
            sourceVendorName
            exchangePricePerUnit
            transitCostPerUnit
            deliveredPricePerUnit
            estimatedQuality
            distanceKm
            isEligible
            blockReason
            blockMessage
            isRecommended
            rank
          }
        }`,
        { unitId },
      )
      if (requestId !== activeSourcingCandidatesRequest) return
      sourcingCandidates.value = data.sourcingCandidates ?? []
    } catch {
      if (requestId !== activeSourcingCandidatesRequest) return
      sourcingCandidates.value = []
    } finally {
      if (requestId === activeSourcingCandidatesRequest) {
        sourcingCandidatesLoading.value = false
      }
    }
  }

  async function loadResearchBrands() {
    const companyId = building.value?.companyId
    if (!companyId || building.value?.type !== 'RESEARCH_DEVELOPMENT') return

    researchBrandsLoading.value = true
    try {
      const data = await gqlRequest<{ companyBrands: ResearchBrandState[] }>(
        `query CompanyBrands($companyId: UUID!) {
          companyBrands(companyId: $companyId) {
            id
            companyId
            name
            scope
            productTypeId
            productName
            industryCategory
            awareness
            quality
            marketingEfficiencyMultiplier
            accumulatedResearchBudget
            baseResearchBudget
            maxCompetitorBudget
          }
        }`,
        { companyId },
      )
      researchBrands.value = data.companyBrands ?? []
    } catch {
      researchBrands.value = []
    } finally {
      researchBrandsLoading.value = false
    }
  }

  async function loadCityMediaHouses() {
    const cityId = building.value?.cityId
    if (!cityId) return
    const ownerCompanyId = building.value?.companyId ?? null
    cityMediaHousesLoading.value = true
    try {
      const data = await gqlRequest<{ cityMediaHouses: CityMediaHouseInfo[] }>(
        `query CityMediaHouses($cityId: UUID!, $ownerCompanyId: UUID) {
          cityMediaHouses(cityId: $cityId, ownerCompanyId: $ownerCompanyId) {
            id
            name
            cityId
            cityName
            mediaType
            ownerCompanyId
            ownerCompanyName
            effectivenessMultiplier
            powerStatus
            isUnderConstruction
            contentRanking
            contentValue
            contentBudgetPerTick
            isGovernmentOwned
          }
        }`,
        { cityId, ownerCompanyId },
      )
      cityMediaHouses.value = data.cityMediaHouses ?? []
    } catch {
      cityMediaHouses.value = []
    } finally {
      cityMediaHousesLoading.value = false
    }
  }

  async function loadPublicSalesAnalytics(unitId: string | null, isRefresh = false) {
    if (!unitId || !auth.token) {
      publicSalesAnalytics.value = null
      publicSalesAnalyticsLoading.value = false
      return
    }

    if (!isRefresh || publicSalesAnalytics.value == null) {
      publicSalesAnalyticsLoading.value = true
    }
    try {
      const data = await gqlRequest<{ publicSalesAnalytics: PublicSalesAnalytics | null }>(
        `query PublicSalesAnalytics($unitId: UUID!) {
          publicSalesAnalytics(unitId: $unitId) {
            buildingUnitId
            buildingId
            buildingName
            cityName
            productTypeId
            productName
            totalRevenue
            totalQuantitySold
            averagePricePerUnit
            currentSalesCapacity
            dataFromTick
            dataToTick
            demandSignal
            actionHint
            recentUtilization
            elasticityIndex
            unmetDemandShare
            populationIndex
            inventoryQuality
            brandAwareness
            brandQuality
            totalProfit
            trendDirection
            trendFactor
            cityCurrencyCode
            cityAveragePrice
            revenueHistory { tick revenue quantitySold }
            priceHistory { tick pricePerUnit }
            profitHistory { tick profit grossMarginPct }
            marketShare { label companyId share isUnmet }
            demandDrivers { factor impact score description }
          }
        }`,
        { unitId },
      )
      publicSalesAnalytics.value = data.publicSalesAnalytics ?? null
    } catch {
      publicSalesAnalytics.value = null
    } finally {
      publicSalesAnalyticsLoading.value = false
    }
  }

  async function loadUnitProductAnalytics(unitId: string | null, isRefresh = false) {
    if (!unitId || !auth.token) {
      unitProductAnalytics.value = null
      unitProductAnalyticsLoading.value = false
      return
    }

    if (!isRefresh || unitProductAnalytics.value == null) {
      unitProductAnalyticsLoading.value = true
    }
    try {
      const data = await gqlRequest<{ unitProductAnalytics: UnitProductAnalytics | null }>(
        `query UnitProductAnalytics($unitId: UUID!) {
          unitProductAnalytics(unitId: $unitId) {
            buildingUnitId
            unitType
            productTypeId
            productName
            dataFromTick
            dataToTick
            totalCost
            totalQuantityProduced
            estimatedRevenue
            estimatedProfit
            cityCurrencyCode
            snapshots { tick laborCost energyCost totalCost quantityProduced estimatedRevenue estimatedProfit }
          }
        }`,
        { unitId },
      )
      unitProductAnalytics.value = data.unitProductAnalytics ?? null
    } catch {
      unitProductAnalytics.value = null
    } finally {
      unitProductAnalyticsLoading.value = false
    }
  }

  async function submitQuickPriceUpdate() {
    const unit = selectedPublicSalesUnit.value
    const price = quickPriceInput.value
    if (!unit || !auth.token || price == null) return
    const unitId = getResolvedLiveUnitId(unit)
    if (!unitId) return
    quickPriceSaving.value = true
    quickPriceSuccess.value = false
    quickPriceError.value = null
    try {
      const data = await gqlRequest<{ updatePublicSalesPrice: { id: string; minPrice: number } }>(
        `mutation UpdatePublicSalesPrice($input: UpdatePublicSalesPriceInput!) {
          updatePublicSalesPrice(input: $input) { id minPrice }
        }`,
        { input: { unitId, newMinPrice: price } },
      )
      // Update local unit state immediately so UI reflects new price
      if (building.value) {
        const liveUnit = building.value.units?.find((u) => u.id === data.updatePublicSalesPrice.id)
        if (liveUnit) liveUnit.minPrice = data.updatePublicSalesPrice.minPrice
      }
      quickPriceInput.value = null
      quickPriceSuccess.value = true
      // Refresh analytics to reflect the new price
      await loadPublicSalesAnalytics(unitId)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err)
      quickPriceError.value = msg || t('buildingDetail.marketIntelligence.priceUpdateFailed')
    } finally {
      quickPriceSaving.value = false
    }
  }

  async function submitFlushStorage(unitId: string) {
    if (!auth.token) return
    flushingStorage.value = true
    flushStorageError.value = null
    flushStorageSuccess.value = false
    showFlushConfirmDialog.value = false
    try {
      await gqlRequest<{ flushStorage: { discardedItemCount: number; totalDiscardedValue: number } }>(
        `mutation FlushStorage($input: FlushStorageInput!) {
          flushStorage(input: $input) {
            discardedItemCount
            totalDiscardedValue
          }
        }`,
        { input: { buildingUnitId: unitId } },
      )
      flushStorageSuccess.value = true
      // Reload building data to reflect cleared inventory
      await loadBuilding({ preserveDraft: isEditing.value })
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err)
      flushStorageError.value = msg || t('buildingDetail.flushStorage.error')
    } finally {
      flushingStorage.value = false
    }
  }

  /** Fetches and caches upgrade info for the given unit ID. */
  async function fetchUpgradeInfo(unitId: string) {
    if (!auth.token) return
    try {
      const result = await gqlRequest<{ unitUpgradeInfo: import('@/types').UnitUpgradeInfo | null }>(
        `query UUI($unitId: UUID!) {
          unitUpgradeInfo(unitId: $unitId) {
            unitId unitType currentLevel nextLevel isMaxLevel isUpgradable
            upgradeCost upgradeTicks currentStat nextStat statLabel
            currentLaborHoursPerTick nextLaborHoursPerTick
            currentEnergyMwhPerTick nextEnergyMwhPerTick
            currentLaborCostPerTick nextLaborCostPerTick
            currentEnergyCostPerTick nextEnergyCostPerTick
            currentStorageCapacity nextStorageCapacity
          }
        }`,
        { unitId },
      )
      unitUpgradeInfoCache.value = result.unitUpgradeInfo
    } catch {
      // silently ignore — upgrade panel will remain hidden
    }
  }

  /** Schedules a unit level upgrade via the backend mutation. */
  async function submitUnitUpgrade(unitId: string) {
    if (!auth.token || schedulingUpgrade.value) return
    schedulingUpgrade.value = true
    unitUpgradeError.value = null
    try {
      await gqlRequest<{ scheduleUnitUpgrade: { id: string } }>(
        `mutation SUU($input: ScheduleUnitUpgradeInput!) {
          scheduleUnitUpgrade(input: $input) { id appliesAtTick totalTicksRequired }
        }`,
        { input: { unitId } },
      )
      // Reload building to show the pending upgrade progress
      await loadBuilding({ preserveDraft: isEditing.value })
      // Refresh upgrade info cache so the panel transitions to pending state
      await fetchUpgradeInfo(unitId)
    } catch (err: unknown) {
      const code = err instanceof GraphQLError ? err.code : undefined
      const raw = err instanceof Error ? err.message : String(err)
      if (code === 'INSUFFICIENT_FUNDS' || raw.includes('INSUFFICIENT_FUNDS')) {
        unitUpgradeError.value = t('buildingDetail.unitUpgrade.errorInsufficientFunds')
      } else if (code === 'MAX_LEVEL_REACHED' || raw.includes('MAX_LEVEL_REACHED')) {
        unitUpgradeError.value = t('buildingDetail.unitUpgrade.errorMaxLevel')
      } else if (code === 'MAX_CONCURRENT_UPGRADES' || raw.includes('MAX_CONCURRENT_UPGRADES')) {
        unitUpgradeError.value = t('buildingDetail.unitUpgrade.errorMaxConcurrentUpgrades')
      } else if (code === 'UNIT_ALREADY_UPGRADING' || raw.includes('UNIT_ALREADY_UPGRADING')) {
        unitUpgradeError.value = t('buildingDetail.unitUpgrade.errorUnitAlreadyUpgrading')
      } else if (code === 'PENDING_CONFIGURATION_EXISTS' || raw.includes('PENDING_CONFIGURATION_EXISTS')) {
        unitUpgradeError.value = t('buildingDetail.unitUpgrade.errorPendingPlan')
      } else {
        unitUpgradeError.value = t('buildingDetail.unitUpgrade.errorGeneric')
      }
    } finally {
      schedulingUpgrade.value = false
    }
  }

  /**
   * Returns price source info for a B2B_SALES unit including the price, source type, and item name.
   * For factory buildings: uses the basePrice of the product from a linked (or any) MANUFACTURING unit.
   * For mine buildings: uses the basePrice of the resource from a linked (or any) MINING unit.
   * Returns null when no relevant configured unit is found in the current draft.
   */
  function getB2BPriceSource(unit: EditableGridUnit): B2BPriceSourceInfo | null {
    // Find adjacent units from the existing draft state (before the new unit is added at this position)
    const byPos = new Map(draftUnits.value.map((u) => [`${u.gridX},${u.gridY}`, u]))
    const neighbors: EditableGridUnit[] = []
    const directions = [
      { dx: -1, dy: 0 },
      { dx: 1, dy: 0 },
      { dx: 0, dy: -1 },
      { dx: 0, dy: 1 },
    ]
    for (const { dx, dy } of directions) {
      const neighbor = byPos.get(`${unit.gridX + dx},${unit.gridY + dy}`)
      if (neighbor) neighbors.push(neighbor)
    }

    // Factory path: look for a connected MANUFACTURING unit with a product set
    const mfgUnit = neighbors.find((n) => n.unitType === 'MANUFACTURING' && n.productTypeId)
    if (mfgUnit?.productTypeId) {
      const product = productTypes.value.find((p) => p.id === mfgUnit.productTypeId)
      if (product?.basePrice != null) {
        return { price: Math.round(product.basePrice * cityFxRate.value * 100) / 100, sourceType: 'manufacturing', itemName: product.name ?? null }
      }
    }
    // Fall back to any MANUFACTURING unit in the building with a product
    const anyMfg = draftUnits.value.find((u) => u.unitType === 'MANUFACTURING' && u.productTypeId)
    if (anyMfg?.productTypeId) {
      const product = productTypes.value.find((p) => p.id === anyMfg.productTypeId)
      if (product?.basePrice != null) {
        return { price: Math.round(product.basePrice * cityFxRate.value * 100) / 100, sourceType: 'manufacturing', itemName: product.name ?? null }
      }
    }

    // Mine path: look for a connected MINING unit with a resource type set
    const miningUnit = neighbors.find((n) => n.unitType === 'MINING' && n.resourceTypeId)
    if (miningUnit?.resourceTypeId) {
      const resource = resourceTypes.value.find((r) => r.id === miningUnit.resourceTypeId)
      if (resource?.basePrice != null) {
        return { price: Math.round(resource.basePrice * cityFxRate.value * 100) / 100, sourceType: 'mining', itemName: resource.name ?? null }
      }
    }
    // Fall back to any MINING unit in the building with a resource type
    const anyMining = draftUnits.value.find((u) => u.unitType === 'MINING' && u.resourceTypeId)
    if (anyMining?.resourceTypeId) {
      const resource = resourceTypes.value.find((r) => r.id === anyMining.resourceTypeId)
      if (resource?.basePrice != null) {
        return { price: Math.round(resource.basePrice * cityFxRate.value * 100) / 100, sourceType: 'mining', itemName: resource.name ?? null }
      }
    }

    return null
  }

  /** Backward-compatible helper used only for auto-fill on placement. */
  function getB2BSuggestedPrice(unit: EditableGridUnit): number | null {
    return getB2BPriceSource(unit)?.price ?? null
  }

  async function loadUnitOperationalStatuses(buildingId: string) {
    if (!auth.token) return
    unitOperationalStatusesLoading.value = true
    try {
      const data = await gqlRequest<{ buildingUnitOperationalStatuses: BuildingUnitOperationalStatus[] }>(
        `query BuildingUnitOperationalStatuses($buildingId: UUID!) {
          buildingUnitOperationalStatuses(buildingId: $buildingId) {
            buildingUnitId
            status
            blockedCode
            blockedReason
            idleTicks
            nextTickLaborCost
            nextTickEnergyCost
          }
        }`,
        { buildingId },
      )
      unitOperationalStatuses.value = data.buildingUnitOperationalStatuses ?? []
    } catch {
      unitOperationalStatuses.value = []
    } finally {
      unitOperationalStatusesLoading.value = false
    }
  }

  async function loadRecentActivity(buildingId: string) {
    if (!auth.token) return
    recentActivityLoading.value = true
    try {
      const data = await gqlRequest<{ buildingRecentActivity: BuildingRecentActivityEvent[] }>(
        `query BuildingRecentActivity($buildingId: UUID!, $limit: Int) {
          buildingRecentActivity(buildingId: $buildingId, limit: $limit) {
            tick
            buildingUnitId
            eventType
            description
            quantity
            amount
            resourceTypeId
            productTypeId
          }
        }`,
        { buildingId, limit: 30 },
      )
      recentActivity.value = data.buildingRecentActivity ?? []
    } catch {
      recentActivity.value = []
    } finally {
      recentActivityLoading.value = false
    }
  }

  async function loadBuildingFinancialTimeline(buildingId: string, isRefresh = false) {
    if (!auth.token) {
      buildingFinancialTimeline.value = null
      buildingFinancialTimelineLoading.value = false
      return
    }

    const requestId = ++activeBuildingFinancialTimelineRequest
    if (!isRefresh || buildingFinancialTimeline.value == null) {
      buildingFinancialTimelineLoading.value = true
    }

    try {
      const data = await gqlRequest<{ buildingFinancialTimeline: BuildingFinancialTimeline }>(
        `query BuildingFinancialTimeline($buildingId: UUID!, $limit: Int) {
          buildingFinancialTimeline(buildingId: $buildingId, limit: $limit) {
            buildingId
            buildingName
            dataFromTick
            dataToTick
            totalSales
            totalCosts
            totalProfit
            timeline {
              tick
              sales
              costs
              profit
            }
          }
        }`,
        { buildingId, limit: 100 },
      )
      if (requestId !== activeBuildingFinancialTimelineRequest) {
        return
      }

      buildingFinancialTimeline.value = data.buildingFinancialTimeline
    } catch {
      if (requestId !== activeBuildingFinancialTimelineRequest) {
        return
      }

      if (!isRefresh) {
        buildingFinancialTimeline.value = null
      }
    } finally {
      if (requestId === activeBuildingFinancialTimelineRequest) {
        buildingFinancialTimelineLoading.value = false
      }
    }
  }

  async function loadPowerPlantAnalytics(buildingId: string, isRefresh = false) {
    if (!auth.token) {
      powerPlantAnalytics.value = null
      powerPlantAnalyticsLoading.value = false
      return
    }

    const requestId = ++activePowerPlantAnalyticsRequest
    if (!isRefresh || powerPlantAnalytics.value == null) {
      powerPlantAnalyticsLoading.value = true
    }

    try {
      const data = await gqlRequest<{ powerPlantAnalytics: PowerPlantAnalytics }>(
        `query PowerPlantAnalytics($buildingId: UUID!, $limit: Int) {
          powerPlantAnalytics(buildingId: $buildingId, limit: $limit) {
            buildingId
            buildingName
            plantType
            currentOutputMw
            dispatchTargetPercent
            fuelReserveMwh
            maxFuelReserveMwh
            fuelReservePercent
            fuelPurchaseCapacityMwhPerTick
            energyProducingCapacityMw
            fuelConstrainedOutputMw
            fuelTypeLabel
            fuelCostPerMwhEur
            dataFromTick
            dataToTick
            totalSurplusIncome
            totalGridFines
            totalOperatingCosts
            totalFuelCosts
            totalNetProfit
            timeline {
              tick
              surplusIncome
              gridFine
              operatingCosts
              fuelCosts
              netProfit
            }
          }
        }`,
        { buildingId, limit: 100 },
      )
      if (requestId !== activePowerPlantAnalyticsRequest) return
      powerPlantAnalytics.value = data.powerPlantAnalytics
    } catch {
      if (requestId !== activePowerPlantAnalyticsRequest) return
      if (!isRefresh) powerPlantAnalytics.value = null
    } finally {
      if (requestId === activePowerPlantAnalyticsRequest) {
        powerPlantAnalyticsLoading.value = false
      }
    }
  }

  const dispatchSaving = ref(false)
  const dispatchError = ref<string | null>(null)
  const dispatchSuccess = ref(false)

  async function setPlantDispatch(buildingId: string, dispatchTargetPercent: number) {
    dispatchSaving.value = true
    dispatchError.value = null
    dispatchSuccess.value = false
    try {
      const data = await gqlRequest<{ setPlantDispatch: { id: string; dispatchTargetPercent: number } }>(
        `mutation SetPlantDispatch($input: SetPlantDispatchInput!) {
          setPlantDispatch(input: $input) {
            id
            dispatchTargetPercent
          }
        }`,
        { input: { buildingId, dispatchTargetPercent } },
      )
      // Update the local building state immediately.
      if (building.value && data.setPlantDispatch) {
        building.value = { ...building.value, dispatchTargetPercent: data.setPlantDispatch.dispatchTargetPercent }
      }
      dispatchSuccess.value = true
      // Reload analytics to reflect the new dispatch target.
      void loadPowerPlantAnalytics(buildingId, true)
    } catch (e) {
      dispatchError.value = e instanceof Error ? e.message : 'Failed to update dispatch target.'
    } finally {
      dispatchSaving.value = false
    }
  }

  async function loadCityPowerBalance(cityId: string, isRefresh = false) {
    if (!cityId) return
    if (!isRefresh || cityPowerBalance.value == null) {
      cityPowerBalanceLoading.value = true
    }
    try {
      const data = await gqlRequest<{ cityPowerBalance: CityPowerBalance }>(
        `query CityPowerBalance($cityId: UUID!) {
          cityPowerBalance(cityId: $cityId) {
            cityId
            totalSupplyMw
            totalDemandMw
            reserveMw
            reservePercent
            status
            powerPlantCount
            consumerBuildingCount
          }
        }`,
        { cityId },
      )
      cityPowerBalance.value = data.cityPowerBalance
    } catch {
      if (!isRefresh) cityPowerBalance.value = null
    } finally {
      cityPowerBalanceLoading.value = false
    }
  }

  async function loadBuilding(options: { preserveDraft?: boolean } = {}) {
    const requestId = ++activeBuildingLoadRequest
    const shouldShowLoading = !building.value

    try {
      if (shouldShowLoading) {
        loading.value = true
      }
      error.value = null
      const preserveDraft = options.preserveDraft === true

      const [companiesData, gameStateData, resourceData, productData, citiesData, fxRatesData] = await Promise.all([
        gqlRequest<{ myCompanies: Company[] }>(
          `{ myCompanies {
            id
            name
            cash
            buildings {
              id
              companyId
              cityId
              type
              name
              latitude
              longitude
              level
              powerConsumption
              powerStatus
              isForSale
              askingPrice
              pricePerSqm
              pendingPricePerSqm
              pendingPriceActivationTick
              occupancyPercent
              totalAreaSqm
              powerPlantType
              powerOutput
              mediaType
              interestRate
              builtAtUtc
              contentValue
              contentBudgetPerTick
              isGovernmentOwned
              isSuspendedForFunds
              suspendedReason
              dispatchTargetPercent
              fuelReserveMwh
              cityReferenceRentPerSqm
              adjustedMarketRentPerSqm
              populationIndex
              units {
                id
                buildingId
                unitType
                gridX
                gridY
                level
                linkUp
                linkDown
                linkLeft
                linkRight
                linkUpLeft
                linkUpRight
                linkDownLeft
                linkDownRight
                resourceTypeId
                productTypeId
                minPrice
                maxPrice
                purchaseSource
                saleVisibility
                budget
                mediaHouseBuildingId
                minQuality
                brandScope
                vendorLockCompanyId
                lockedCityId
                industryCategory
              }
              pendingConfiguration {
                id
                buildingId
                submittedAtUtc
                submittedAtTick
                appliesAtTick
                totalTicksRequired
                removals {
                  id
                  gridX
                  gridY
                  startedAtTick
                  appliesAtTick
                  ticksRequired
                  isReverting
                }
                units {
                  id
                  unitType
                  gridX
                  gridY
                  level
                  linkUp
                  linkDown
                  linkLeft
                  linkRight
                  linkUpLeft
                  linkUpRight
                  linkDownLeft
                  linkDownRight
                  startedAtTick
                  appliesAtTick
                  ticksRequired
                  isChanged
                  isReverting
                  resourceTypeId
                  productTypeId
                  minPrice
                  maxPrice
                  purchaseSource
                  saleVisibility
                  budget
                  mediaHouseBuildingId
                  minQuality
                  brandScope
                  vendorLockCompanyId
                  lockedCityId
                  industryCategory
                }
              }
            }
          } }`,
        ),
        gqlRequest<{ gameState: { currentTick: number } | null }>(`{ gameState { currentTick } }`),
        gqlRequest<{ resourceTypes: ResourceType[] }>(`{
          resourceTypes {
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
        }`),
        gqlRequest<{ productTypes: ProductType[] }>(`{
          productTypes {
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
              resourceType { id name slug unitName unitSymbol weightPerUnit }
              inputProductType { id name slug unitName unitSymbol }
            }
          }
        }`),
        gqlRequest<{ cities: City[] }>(`{ cities { id name currencyCode } }`),
        gqlRequest<{ eurFxRates: EurFxRate[] }>(`{ eurFxRates { currencyCode rate } }`),
      ])

      if (requestId !== activeBuildingLoadRequest) {
        return
      }

      currentTick.value = gameStateData.gameState?.currentTick ?? 0
      if (!deepEqual(resourceTypes.value, resourceData.resourceTypes ?? [])) {
        resourceTypes.value = resourceData.resourceTypes ?? []
      }
      if (!deepEqual(productTypes.value, productData.productTypes ?? [])) {
        productTypes.value = productData.productTypes ?? []
      }
      if (!deepEqual(cities.value, citiesData.cities ?? [])) {
        cities.value = citiesData.cities ?? []
      }
      if (!deepEqual(eurFxRates.value, fxRatesData.eurFxRates ?? [])) {
        eurFxRates.value = fxRatesData.eurFxRates ?? []
      }
      const nextPurchaseVendorCompanies: PurchaseVendorCompanyData[] = companiesData.myCompanies.map((company) => ({
        id: company.id,
        name: company.name,
        buildings: company.buildings.map((candidate) => ({
          id: candidate.id,
          name: candidate.name,
          cityId: candidate.cityId,
          latitude: candidate.latitude,
          longitude: candidate.longitude,
          units: candidate.units.map((unit) => ({
            id: unit.id,
            unitType: unit.unitType,
            resourceTypeId: unit.resourceTypeId,
            productTypeId: unit.productTypeId,
            minPrice: unit.minPrice,
          })),
        })),
      }))
      if (!deepEqual(purchaseVendorCompanies.value, nextPurchaseVendorCompanies)) {
        purchaseVendorCompanies.value = nextPurchaseVendorCompanies
      }

      const allBuildings = companiesData.myCompanies.flatMap((company) => company.buildings)
      const newBuilding = allBuildings.find((candidate) => candidate.id === buildingId.value) || null
      if (!deepEqual(building.value, newBuilding)) {
        building.value = newBuilding
      }

      if (!building.value) {
        companyCash.value = null
        error.value = t('buildingDetail.notFound')
        return
      }

      const newCompanyCash = companiesData.myCompanies.find((company) => company.id === building.value?.companyId)?.cash ?? null
      if (companyCash.value !== newCompanyCash) {
        companyCash.value = newCompanyCash
      }

      if (!preserveDraft) {
        // Normal (non-edit) load: sync the draft editor with the latest server state so
        // the layout editor always shows up-to-date data when the player opens it.
        // When preserveDraft is true (called from useTickRefresh while isEditing is true),
        // these lines are skipped so the player's in-progress draft is never silently
        // discarded by a background tick refresh.  See also: UX convention in copilot-instructions.
        const sourceUnits = pendingConfiguration.value?.units ?? building.value.units
        setDraftUnitsFrom(sourceUnits)
        setEditBaselineFrom(sourceUnits)
        isEditing.value = false
        restoreReadOnlySelectedCell(building.value.units)
        showUnitPicker.value = false
      }

      await Promise.all([loadUnitInventorySummaries(requestId), loadUnitInventories(requestId), loadUnitResourceHistories(requestId)])

      if (requestId !== activeBuildingLoadRequest) {
        return
      }

      await Promise.all([loadGlobalExchangeOffers(), loadResearchBrands(), loadCityMediaHouses()])
      void loadUnitOperationalStatuses(buildingId.value)
      void loadRecentActivity(buildingId.value)
      void loadBuildingFinancialTimeline(buildingId.value)
      if (building.value?.type === 'POWER_PLANT') {
        void loadPowerPlantAnalytics(buildingId.value)
      }
      if (building.value?.type === 'MEDIA_HOUSE') {
        void loadMediaHouseAnalytics()
      }
    } catch (reason: unknown) {
      if (requestId !== activeBuildingLoadRequest) {
        return
      }

      error.value = reason instanceof Error ? reason.message : t('buildingDetail.loadFailed')
    } finally {
      if (requestId === activeBuildingLoadRequest) {
        loading.value = false
      }
    }
  }

  onMounted(async () => {
    if (!auth.isAuthenticated) {
      router.push('/login')
      return
    }

    // Load panel dismissal state for the initial building ID
    if (buildingId.value) {
      loadPanelDismissalState(buildingId.value)
    }

    await loadBuilding()
  })

  async function fetchRankedProducts(unitType: string) {
    if (!buildingId.value) return
    rankedProductsLoading.value = true
    try {
      const data = await gqlRequest<{ rankedProductTypes: RankedProductResult[] }>(
        `query RankedProducts($buildingId: UUID!, $unitType: String!) {
          rankedProductTypes(buildingId: $buildingId, unitType: $unitType) {
            rankingReason
            rankingScore
            productType {
              id name slug industry imageUrl basePrice baseCraftTicks outputQuantity
              energyConsumptionMwh basicLaborHours unitName unitSymbol isProOnly
              isUnlockedForCurrentPlayer description
              recipes { quantity resourceType { id name slug unitName unitSymbol weightPerUnit } inputProductType { id name slug unitName unitSymbol } }
            }
          }
        }`,
        { buildingId: buildingId.value, unitType },
      )
      rankedProducts.value = data.rankedProductTypes ?? []
    } catch {
      // Fall back to flat productTypes list if the ranked query fails
      rankedProducts.value = productTypes.value.map((pt) => ({
        productType: pt,
        rankingReason: 'catalog' as const,
        rankingScore: 10,
      }))
    } finally {
      rankedProductsLoading.value = false
    }
  }

  useTickRefresh(async () => {
    if (!auth.isAuthenticated || !building.value) {
      return
    }

    // Save scroll position before data update so the player's reading context is preserved
    const scrollPos = saveScrollPosition()
    try {
      await loadBuilding({ preserveDraft: isEditing.value })
    } finally {
      // Always restore scroll so the player's position is preserved regardless of errors
      await restoreScrollPosition(scrollPos)
    }

    // Refresh analytics for the selected PUBLIC_SALES unit on tick change
    const unitId = getResolvedLiveUnitId(selectedPublicSalesUnit.value)
    if (unitId) {
      void loadPublicSalesAnalytics(unitId, true)
    }
    // Refresh unit product analytics for the selected MANUFACTURING unit on tick change
    const mfgUnitId = getResolvedLiveUnitId(selectedManufacturingUnit.value)
    if (mfgUnitId) {
      void loadUnitProductAnalytics(mfgUnitId, true)
    }
    if (getResolvedLiveUnitId(selectedPurchaseUnit.value)) {
      void loadProcurementPreview(true)
      void loadSourcingCandidates(true)
    }
    void loadUnitOperationalStatuses(buildingId.value)
    void loadRecentActivity(buildingId.value)
    void loadBuildingFinancialTimeline(buildingId.value, true)
    if (building.value?.type === 'POWER_PLANT') {
      void loadPowerPlantAnalytics(buildingId.value, true)
    }
  })

  watch(
    () => route.query.unit,
    () => {
      if (!isEditing.value) {
        restoreReadOnlySelectedCell(activeUnits.value)
        restoreSelectedUnitTabFromRoute()
      }
    },
  )

  // Reload panel dismissal state when navigating to a different building
  watch(
    () => buildingId.value,
    (bid) => {
      if (bid) {
        loadPanelDismissalState(bid)
      }
    },
  )

  // Reset flush storage state when user navigates to a different unit
  watch(
    () => selectedCell.value,
    () => {
      if (isEditing.value) return
      syncSelectedCellQuery(selectedCell.value)
      flushStorageError.value = null
      flushStorageSuccess.value = false
      showFlushConfirmDialog.value = false
      if (!selectedDraftPurchaseUnit.value) {
        showPurchaseSelector.value = false
      }
    },
  )

  // Fetch ranked products when entering a product-selection unit in edit mode
  watch(
    () => {
      if (!isEditing.value || !selectedCell.value) return null
      const unit = getDraftUnitAt(selectedCell.value.x, selectedCell.value.y)
      if (!unit) return null
      const type = unit.unitType
      if (type === 'PUBLIC_SALES' || type === 'PRODUCT_QUALITY' || type === 'BRAND_QUALITY' || type === 'STORAGE' || type === 'B2B_SALES') return type
      return null
    },
    (unitType) => {
      if (unitType) void fetchRankedProducts(unitType)
      else rankedProducts.value = []
    },
    { immediate: false },
  )

  watch(
    () => [
      building.value?.cityId ?? null,
      selectedCell.value?.x ?? null,
      selectedCell.value?.y ?? null,
      getPurchaseUnitResourceTypeId(selectedPurchaseUnit.value),
      getPurchaseUnitSource(selectedPurchaseUnit.value),
      isEditing.value,
    ],
    () => {
      void loadGlobalExchangeOffers()
    },
  )

  // Load procurement preview when a purchase unit from the active (non-draft) layout is selected.
  watch(
    () => {
      const unit = selectedPurchaseUnit.value
      if (!unit || !('id' in unit)) return null
      // Only load preview for live units (not draft-only units that aren't saved yet).
      if (isEditing.value) return null
      return unit.id
    },
    (unitId) => {
      if (unitId) {
        void loadProcurementPreview()
        void loadSourcingCandidates()
      } else {
        procurementPreview.value = null
        sourcingCandidates.value = []
      }
    },
  )

  watch(
    () => ({
      unitId: getResolvedLiveUnitId(selectedDisplayUnit.value),
      itemKeys: selectedHistoryItemOptions.value.map((item) => item.key).join('|'),
    }),
    () => {
      if (selectedHistoryItemOptions.value.length === 0) {
        selectedHistoryItemKey.value = null
        return
      }

      const hasSelectedItem = selectedHistoryItemOptions.value.some((item) => item.key === selectedHistoryItemKey.value)
      if (!hasSelectedItem) {
        selectedHistoryItemKey.value = selectedHistoryItemOptions.value[0]?.key ?? null
      }
    },
    { immediate: true },
  )

  watch(
    () => getResolvedLiveUnitId(selectedPublicSalesUnit.value),
    (unitId) => {
      void loadPublicSalesAnalytics(unitId)
    },
    { immediate: true },
  )

  watch(
    () => getResolvedLiveUnitId(selectedManufacturingUnit.value),
    (unitId) => {
      void loadUnitProductAnalytics(unitId ?? null)
    },
    { immediate: true },
  )

  watch(
    () => selectedPublicSalesUnit.value?.id,
    () => {
      quickPriceInput.value = selectedPublicSalesUnit.value?.minPrice ?? null
    },
    { immediate: true },
  )

  // Fetch upgrade info when an active unit is selected while in edit mode.
  // Upgrade actions are only available in edit mode (per product requirements).
  watch(
    () => {
      if (!isEditing.value || !selectedCell.value) return null
      const unit = getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y)
      return unit?.id ?? null
    },
    (unitId) => {
      unitUpgradeInfoCache.value = null
      unitUpgradeError.value = null
      if (unitId) {
        void fetchUpgradeInfo(unitId)
      }
    },
  )

  return {
    locale,
    building,
    currentTick,
    loading,
    saving,
    error,
    saveError,
    companyCash,
    isEditing,
    selectedCell,
    showUnitPicker,
    draftUnits,
    editBaselineUnits,
    resourceTypes,
    productTypes,
    rankedProducts,
    rankedProductsLoading,
    cities,
    unitInventorySummaries,
    unitInventories,
    unitResourceHistories,
    exchangeOffers,
    exchangeOffersLoading,
    exchangeSortBy,
    procurementPreview,
    procurementPreviewLoading,
    sourcingCandidates,
    sourcingCandidatesLoading,
    purchaseVendorCompanies,
    showPurchaseSelector,
    unitOperationalStatuses,
    unitOperationalStatusesLoading,
    recentActivity,
    recentActivityLoading,
    buildingFinancialTimeline,
    buildingFinancialTimelineLoading,
    powerPlantAnalytics,
    powerPlantAnalyticsLoading,
    cityPowerBalance,
    cityPowerBalanceLoading,
    researchBrands,
    researchBrandsLoading,
    cityMediaHouses,
    cityMediaHousesLoading,
    publicSalesAnalytics,
    publicSalesAnalyticsLoading,
    unitProductAnalytics,
    unitProductAnalyticsLoading,
    quickPriceInput,
    quickPriceSaving,
    quickPriceSuccess,
    quickPriceError,
    showSaleDialog,
    salePrice,
    savingSale,
    cancellingPlan,
    cancelPlanError,
    layoutName,
    layoutDescription,
    masterLayouts,
    masterLayoutsLoading,
    masterLayoutsError,
    localLayouts,
    layoutSaving,
    layoutSaveError,
    layoutSaveSuccess,
    layoutDeleteError,
    overwriteConfirmPending,
    selectedHistoryItemKey,
    showRentDialog,
    newRentPerSqm,
    savingRent,
    rentSaveError,
    contentBudgetInput,
    savingContentBudget,
    contentBudgetError,
    contentBudgetSuccess,
    upgradingMediaHouse,
    mediaHouseUpgradeError,
    mediaHouseUpgradeSuccess,
    mediaHouseAnalytics,
    mediaHouseAnalyticsLoading,
    showFlushConfirmDialog,
    flushingStorage,
    flushStorageError,
    flushStorageSuccess,
    schedulingUpgrade,
    unitUpgradeError,
    unitUpgradeInfoCache,
    draftUpgradeUnitIds,
    productionChainPanelDismissed,
    salesChainPanelDismissed,
    selectedUnitTab,
    buildingId,
    activeUnits,
    pendingConfiguration,
    pendingUnits,
    pendingRemovals,
    plannedUnits,
    allowedUnits,
    showStarterSetupBanner,
    intermediateProductIds,
    allSelectableItems,
    lockedConfiguredProducts,
    lockedConfiguredProductNames,
    isUpgradeInProgress,
    showPlanningSection,
    hasConfiguredRdUnits,
    remainingUpgradeTicks,
    draftTotalTicks,
    hasDraftChanges,
    draftConstructionCost,
    projectedCompanyCashAfterApply,
    chainDisplayUnits,
    chainStatus,
    showProductionChainPanel,
    showSalesShopStarterBanner,
    shopChainDisplayUnits,
    shopChainStatus,
    showSalesChainPanel,
    draftLinkChanges,
    draftUnitChanges,
    selectedDisplayUnit,
    selectedPurchaseUnit,
    selectedPublicSalesUnit,
    selectedManufacturingUnit,
    selectedDraftPurchaseUnit,
    selectedDraftPublicSalesUnit,
    selectedDraftB2bSalesUnit,
    publicSalesFilteredRankedProducts,
    b2bSalesFilteredRankedProducts,
    selectedHistoryItemOptions,
    selectedUnitResourceHistory,
    buildingOverviewCityName,
    cityCurrencyCode,
    cityFxRate,
    buildingOverviewMapRoute,
    buildingFinancialSnapshots,
    buildingFinancialHasActivity,
    b2bPriceSource,
    b2bSuggestedPrice,
    b2bHasUpstreamSource,
    miMaxRevenue,
    miMaxQuantitySold,
    miMaxPricePerUnit,
    miMaxAbsProfit,
    upaMaxAbsProfit,
    upaMaxCost,
    upaMaxEstRevenue,
    currentPublicSalesMinPrice,
    unitDetailTabs,
    annotatedExchangeOffers,
    exchangeOfferItems,
    allExchangeOffersBlocked,
    bestExchangeOfferCityId,
    logisticsTrapWarning,
    sourcingCheapestStickerDiffersFromBestLanded,
    selectedPurchaseResourceSlug,
    purchaseSelectorItems,
    selectedPurchaseSelection,
    sameCityVendorItemKeys,
    resourceTypesById,
    productTypesById,
    purchaseVendorOptions,
    selectedPurchaseVendorSummary,
    selectedDraftMediaHouse,
    configWarnings,
    selectedActiveUnitOperationalStatus,
    selectedCellPendingUpgrade,
    selectedCellUpgradeInfo,
    isSelectedCellStaged,
    allUnitsUnderUpgrade,
    selectedActiveUnitFlowSegments,
    selectedPlannedUnitFlowSegments,
    masterConnected,
    masterUserEmail,
    parseUnitQuery,
    syncSelectedCellQuery,
    setReadOnlySelectedCell,
    restoreReadOnlySelectedCell,
    clickReadOnlyCell,
    clickDraftCell,
    placeUnit,
    removeDraftUnit,
    clearConnectionsAround,
    toggleHorizontalLink,
    toggleVerticalLink,
    togglePrimaryDiagonalLink,
    toggleSecondaryDiagonalLink,
    isHorizontalLinkActiveFor,
    isVerticalLinkActiveFor,
    canToggleHorizontalLink,
    canToggleVerticalLink,
    canTogglePrimaryDiagonalLink,
    canToggleSecondaryDiagonalLink,
    getHorizontalLinkStateFor,
    getVerticalLinkStateFor,
    getPrimaryDiagonalLinkStateFor,
    getSecondaryDiagonalLinkStateFor,
    getHorizontalLinkArrow,
    getVerticalLinkArrow,
    isHorizontalLinkLive,
    isVerticalLinkLive,
    isLinkConnectedToSelectedCell,
    isCellConnectedToSelected,
    getHorizontalLinkFlowHint,
    getVerticalLinkFlowHint,
    getDraftUnitAt,
    getDraftTicksForUnit,
    getDraftTicksAt,
    getDisplayedTicks,
    areUnitsEquivalent,
    areUnitCollectionsEqual,
    startEditing,
    cancelEditing,
    applyStarterLayout,
    applyShopStarterLayout,
    storeConfiguration,
    cancelPlan,
    getLinkedUnits,
    isUnitReverting,
    openSaleDialog,
    closeSaleDialog,
    setBuildingForSale,
    openRentDialog,
    closeRentDialog,
    saveRentPerSqm,
    initContentBudgetInput,
    saveContentBudget,
    getDraftLayoutUnits,
    refreshLocalLayouts,
    refreshMasterLayouts,
    saveLayout,
    requestLoadLayout,
    confirmOverwrite,
    cancelOverwrite,
    applyLayout,
    deleteLayout,
    layoutStructureSummary,
    getItemSelection,
    setItemSelection,
    openPurchaseSelector,
    closePurchaseSelector,
    applyPurchaseSelection,
    selectPurchaseVendor,
    getFactoryPurchaseSelectableItems,
    getManufacturingSelectableItems,
    getPurchaseVendorTransitLabel,
    getResourceName,
    getProductName,
    getBrandScopeLabel,
    formatUnitMetric,
    getUnitConfiguredItemLabel,
    getUnitPrimaryMetric,
    getUnitInventorySummary,
    getUnitInventories,
    getResolvedLiveUnitId,
    getUnitResourceHistory,
    getUnitResourceHistoryItemOptions,
    getSelectedUnitResourceHistory,
    getUnitInventoryItemCount,
    getUnitOperationalStatus,
    isCellUnderUpgrade,
    toggleStagedUpgrade,
    updateSelectedUnitConfig,
    formatCurrency,
    formatBuildingType,
    formatTickDuration,
    formatGameTickTime,
    formatPercent,
    formatUnitQuantity,
    getUnitColor,
    getUnitAtFrom,
    getLayoutCellType,
    getCityName,
    formatGpsLocation,
    getConfiguredItemImageUrl,
    getInventoryItemImageUrl,
    getInventoryItemMonogram,
    getInventoryItemSourcingCostPerUnitLabel,
    getPrimaryInventoryItem,
    getUnitDisplayLabel,
    getInventoryItemName,
    getInventoryItemSourcingCostLabel,
    getUnitDisplayImageUrl,
    getUnitDisplayMonogram,
    getUnitInventoryCost,
    getUnitNextTickOperatingCost,
    getDraftUnitConstructionCost,
    getDraftUnitConstructionCostLabel,
    getUnitInventoryCostLabel,
    getUnitNextTickOperatingCostLabel,
    getUnitConstructionCost,
    getPurchaseUnitResourceTypeId,
    getPurchaseUnitSource,
    getUnitFlowSegments,
    getCellOutflowClass,
    getCellCapacityTooltip,
    getGridCellAriaLabel,
    getFillBucket,
    getLocalizedIndustry,
    loadBuilding,
    loadUnitInventorySummaries,
    loadUnitInventories,
    loadUnitResourceHistories,
    loadGlobalExchangeOffers,
    loadProcurementPreview,
    loadSourcingCandidates,
    loadResearchBrands,
    loadCityMediaHouses,
    loadPublicSalesAnalytics,
    loadMediaHouseAnalytics,
    upgradeMediaHouse,
    loadUnitProductAnalytics,
    submitQuickPriceUpdate,
    submitFlushStorage,
    fetchUpgradeInfo,
    submitUnitUpgrade,
    loadUnitOperationalStatuses,
    loadRecentActivity,
    loadBuildingFinancialTimeline,
    loadPowerPlantAnalytics,
    loadCityPowerBalance,
    setPlantDispatch,
    dispatchSaving,
    dispatchError,
    dispatchSuccess,
    fetchRankedProducts,
    getB2BPriceSource,
    getB2BSuggestedPrice,
    dismissProductionChainPanel,
    dismissSalesChainPanel,
    gridIndexes,
    SUPPORTED_INDUSTRIES,
  }
}
