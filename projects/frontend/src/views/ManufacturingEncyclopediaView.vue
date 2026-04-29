<script setup lang="ts">
/* eslint-disable @typescript-eslint/no-unused-vars */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import { isProductLocked } from '@/lib/productAccess'
import { formatMoney } from '@/lib/currencyFormat'
import {
  getLocalizedCategory,
  getLocalizedIndustry,
  getLocalizedProductDescription,
  getLocalizedProductName,
  getLocalizedRecipeIngredientName,
  getLocalizedResourceDescription,
  getLocalizedResourceName,
  getProductImageUrl,
  getResourceImageUrl,
} from '@/lib/catalogPresentation'
import type { ProductType, ResourceType } from '@/types'

type CatalogEntry = {
  id: string
  slug: string
  kind: 'resource' | 'product'
  title: string
  description: string
  imageUrl: string | null
  pill: string
  badge: string
  meta: string[]
  industry: string | null
  accessText: string | null
  accessClass: 'locked' | 'unlocked' | null
  searchText: string
}

type EncyclopediaTopicSlug = 'onboarding-help' | 'factory-layout-help' | 'sales-shop-help' | 'forex-trading-help' | 'stock-exchange-help' | 'resources-definition'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)
const search = ref('')
const industry = ref('ALL')
const resources = ref<ResourceType[]>([])
const products = ref<ProductType[]>([])
const fullscreenImage = ref<{ src: string; alt: string } | null>(null)

const topicSlugs: EncyclopediaTopicSlug[] = ['onboarding-help', 'factory-layout-help', 'sales-shop-help', 'forex-trading-help', 'stock-exchange-help', 'resources-definition']

const showProProducts = computed({
  get: () => route.query.showPro === '1',
  set: (value: boolean) => {
    const nextQuery = { ...route.query }

    if (value) {
      nextQuery.showPro = '1'
    } else {
      delete nextQuery.showPro
    }

    router.replace({ path: route.path, query: nextQuery })
  },
})

const visibleProducts = computed(() => (showProProducts.value ? products.value : products.value.filter((product) => !product.isProOnly)))

const industries = computed(() => ['ALL', ...new Set(visibleProducts.value.map((product) => product.industry))])

const hiddenProProductCount = computed(() => (showProProducts.value ? 0 : products.value.filter((product) => product.isProOnly).length))

const selectedTopic = computed<EncyclopediaTopicSlug>(() => {
  const rawTopic = String(route.params.topicSlug ?? 'resources-definition')
  return topicSlugs.includes(rawTopic as EncyclopediaTopicSlug) ? (rawTopic as EncyclopediaTopicSlug) : 'resources-definition'
})

const topicMenu = computed(() => [
  { slug: 'onboarding-help' as const, label: t('encyclopedia.topicOnboardingHelp') },
  { slug: 'factory-layout-help' as const, label: t('encyclopedia.topicFactoryLayoutHelp') },
  { slug: 'sales-shop-help' as const, label: t('encyclopedia.topicSalesShopHelp') },
  { slug: 'forex-trading-help' as const, label: t('encyclopedia.topicForexTradingHelp') },
  { slug: 'stock-exchange-help' as const, label: t('encyclopedia.topicStockExchangeHelp') },
  { slug: 'resources-definition' as const, label: t('encyclopedia.topicResourcesDefinition') },
])

const resourcesBySlug = computed(() => new Map(resources.value.map((resource) => [resource.slug, resource])))
const productsBySlug = computed(() => new Map(products.value.map((product) => [product.slug, product])))

const onboardingGuideCards = [
  {
    titleKey: 'encyclopedia.onboardingGuideStep1Title',
    bodyKey: 'encyclopedia.onboardingGuideStep1Body',
    imageUrl: '/onboarding-help/step-1-industry.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep2Title',
    bodyKey: 'encyclopedia.onboardingGuideStep2Body',
    imageUrl: '/onboarding-help/step-2-product.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep3Title',
    bodyKey: 'encyclopedia.onboardingGuideStep3Body',
    imageUrl: '/onboarding-help/step-3-city.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep4Title',
    bodyKey: 'encyclopedia.onboardingGuideStep4Body',
    imageUrl: '/onboarding-help/step-4-ipo.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep5Title',
    bodyKey: 'encyclopedia.onboardingGuideStep5Body',
    imageUrl: '/onboarding-help/step-5-factory-lot.png',
  },
  {
    titleKey: 'encyclopedia.onboardingGuideStep6Title',
    bodyKey: 'encyclopedia.onboardingGuideStep6Body',
    imageUrl: '/onboarding-help/step-6-shop-lot.png',
  },
]

const manufacturingGuideCards = [
  {
    titleKey: 'encyclopedia.manufacturingGuideStepPurchaseTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepPurchaseBody',
    resourceSlug: 'grain',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepManufactureTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepManufactureBody',
    productSlug: 'bread',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepStorageTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepStorageBody',
    productSlug: 'wooden-chair',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepPublicSalesTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepPublicSalesBody',
    productSlug: 'basic-medicine',
  },
  {
    titleKey: 'encyclopedia.manufacturingGuideStepUnitTypesTitle',
    bodyKey: 'encyclopedia.manufacturingGuideStepUnitTypesBody',
    imageUrl: '/onboarding-help/step-5-factory-lot.png',
  },
]

const manufacturingGuideTopics = [
  'encyclopedia.manufacturingGuideTopicPurchase',
  'encyclopedia.manufacturingGuideTopicManufacturing',
  'encyclopedia.manufacturingGuideTopicStorage',
  'encyclopedia.manufacturingGuideTopicPublicSales',
  'encyclopedia.manufacturingGuideTopicUnitTypes',
]

const salesShopGuideCards = [
  {
    titleKey: 'encyclopedia.salesShopGuideStepBuyBuildingTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepBuyBuildingBody',
    imageUrl: '/sales-shop-help/step-1-buy-sales-shop-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.salesShopGuideStepPurchaseUnitTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepPurchaseUnitBody',
    imageUrl: '/sales-shop-help/step-2-purchase-unit-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.salesShopGuideStepPublicSalesTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepPublicSalesBody',
    imageUrl: '/sales-shop-help/step-3-public-sales-unit-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.salesShopGuideStepMarketingTitle',
    bodyKey: 'encyclopedia.salesShopGuideStepMarketingBody',
    imageUrl: '/sales-shop-help/step-4-marketing-unit-1920x1080.png',
  },
]

const salesShopGuideTopics = [
  'encyclopedia.salesShopGuideTopicBuyBuilding',
  'encyclopedia.salesShopGuideTopicPurchaseUnit',
  'encyclopedia.salesShopGuideTopicPublicSalesUnit',
  'encyclopedia.salesShopGuideTopicMarketingUnit',
]

const forexGuideCards = [
  {
    titleKey: 'encyclopedia.forexGuideStepSwapOverviewTitle',
    bodyKey: 'encyclopedia.forexGuideStepSwapOverviewBody',
    imageUrl: '/forex-help/step-1-swap-overview-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepSwapExecutionTitle',
    bodyKey: 'encyclopedia.forexGuideStepSwapExecutionBody',
    imageUrl: '/forex-help/step-2-quote-and-confirm-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepTransferTitle',
    bodyKey: 'encyclopedia.forexGuideStepTransferBody',
    imageUrl: '/forex-help/step-3-account-transfer-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepRatesTitle',
    bodyKey: 'encyclopedia.forexGuideStepRatesBody',
    imageUrl: '/forex-help/step-4-fx-rates-board-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepHistoryTitle',
    bodyKey: 'encyclopedia.forexGuideStepHistoryBody',
    imageUrl: '/forex-help/step-5-swap-history-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepGoldSwapTitle',
    bodyKey: 'encyclopedia.forexGuideStepGoldSwapBody',
    imageUrl: '/forex-help/step-6-gold-amm-swap-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepGoldPositionsTitle',
    bodyKey: 'encyclopedia.forexGuideStepGoldPositionsBody',
    imageUrl: '/forex-help/step-7-gold-amm-positions-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.forexGuideStepGoldLiquidityTitle',
    bodyKey: 'encyclopedia.forexGuideStepGoldLiquidityBody',
    imageUrl: '/forex-help/step-8-gold-amm-liquidity-1920x1080.png',
  },
]

const forexGuideTopics = [
  'encyclopedia.forexGuideTopicSwap',
  'encyclopedia.forexGuideTopicTransfer',
  'encyclopedia.forexGuideTopicRates',
  'encyclopedia.forexGuideTopicHistory',
  'encyclopedia.forexGuideTopicGoldSwap',
  'encyclopedia.forexGuideTopicGoldPositions',
  'encyclopedia.forexGuideTopicGoldLiquidity',
]

const stockExchangeGuideCards = [
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepIpoTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepIpoBody',
    imageUrl: '/stock-exchange-help/step-1-ipo-plan-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepCompanyBuyTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepCompanyBuyBody',
    imageUrl: '/stock-exchange-help/step-2-company-buy-shares-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepPersonalBuyTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepPersonalBuyBody',
    imageUrl: '/stock-exchange-help/step-3-personal-buy-shares-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepSellTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepSellBody',
    imageUrl: '/stock-exchange-help/step-4-sell-shares-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepUsdForexTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepUsdForexBody',
    imageUrl: '/stock-exchange-help/step-5-usd-forex-swap-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepTaxLedgerTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepTaxLedgerBody',
    imageUrl: '/stock-exchange-help/step-6-tax-reserve-ledger-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepDividendConfigTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepDividendConfigBody',
    imageUrl: '/stock-exchange-help/step-7-dividend-config-company-settings-1920x1080.png',
  },
  {
    titleKey: 'encyclopedia.stockExchangeGuideStepDividendPersonalTitle',
    bodyKey: 'encyclopedia.stockExchangeGuideStepDividendPersonalBody',
    imageUrl: '/stock-exchange-help/step-8-dividend-effects-personal-account-1920x1080.png',
  },
]

const stockExchangeGuideTopics = [
  'encyclopedia.stockExchangeGuideTopicIpo',
  'encyclopedia.stockExchangeGuideTopicCompanyBuy',
  'encyclopedia.stockExchangeGuideTopicPersonalBuy',
  'encyclopedia.stockExchangeGuideTopicSell',
  'encyclopedia.stockExchangeGuideTopicUsdForex',
  'encyclopedia.stockExchangeGuideTopicTax',
  'encyclopedia.stockExchangeGuideTopicDividendConfig',
  'encyclopedia.stockExchangeGuideTopicDividendPersonal',
]

const catalogEntries = computed<CatalogEntry[]>(() => {
  const query = search.value.trim().toLowerCase()
  const entries: CatalogEntry[] = [
    ...resources.value.map((resource) => {
      const title = getLocalizedResourceName(resource, locale.value)
      const description = getLocalizedResourceDescription(resource, locale.value)

      return {
        id: resource.id,
        slug: resource.slug,
        kind: 'resource' as const,
        title,
        description,
        imageUrl: getResourceImageUrl(resource),
        pill: resource.unitSymbol,
        badge: t('encyclopedia.resourceTypeRaw'),
        meta: [
          `${t('encyclopedia.basePrice')}: ${formatMoney(resource.basePrice, 'EUR', locale.value)}`,
          `${t('encyclopedia.weight')}: ${resource.weightPerUnit} kg/${resource.unitSymbol}`,
          getLocalizedCategory(resource.category, locale.value),
        ],
        industry: null,
        accessText: null,
        accessClass: null,
        searchText: [title, description, resource.category].join(' ').toLowerCase(),
      }
    }),
    ...visibleProducts.value.map((product) => {
      const title = getLocalizedProductName(product, locale.value)
      const description = getLocalizedProductDescription(product, locale.value)

      return {
        id: product.id,
        slug: product.slug,
        kind: 'product' as const,
        title,
        description,
        imageUrl: getProductImageUrl(product),
        pill: product.unitSymbol,
        badge: getLocalizedIndustry(product.industry, locale.value),
        meta: [
          `${t('encyclopedia.basePrice')}: ${formatMoney(product.basePrice, 'EUR', locale.value)}`,
          `${t('encyclopedia.energy')}: ${product.energyConsumptionMwh} MWh`,
          `${t('encyclopedia.basicLaborHours')}: ${product.basicLaborHours} h`,
          `${t('encyclopedia.output')}: ${product.outputQuantity} ${product.unitSymbol}`,
        ],
        industry: product.industry,
        accessText: product.isProOnly ? getProductAccessText(product) : null,
        accessClass: product.isProOnly ? (isProductLocked(product) ? ('locked' as const) : ('unlocked' as const)) : null,
        searchText: [title, description, product.industry, ...product.recipes.map((recipe) => getLocalizedRecipeIngredientName(recipe, locale.value))].join(' ').toLowerCase(),
      }
    }),
  ]

  return entries.filter((entry) => {
    const matchesIndustry = industry.value === 'ALL' || entry.industry === industry.value
    const matchesSearch = query.length === 0 || entry.searchText.includes(query)

    return matchesIndustry && matchesSearch
  })
})

watch(
  industries,
  (nextIndustries) => {
    if (!nextIndustries.includes(industry.value)) {
      industry.value = 'ALL'
    }
  },
  { immediate: true },
)

onMounted(async () => {
  try {
    loading.value = true
    const [resourceData, productData] = await Promise.all([
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
          basicLaborHours
          unitName
          unitSymbol
          isProOnly
          isUnlockedForCurrentPlayer
          description
          recipes {
            quantity
            resourceType { id name slug category basePrice weightPerUnit unitName unitSymbol imageUrl description }
            inputProductType { id name slug unitName unitSymbol }
          }
        }
      }`),
    ])

    resources.value = resourceData.resourceTypes
    products.value = productData.productTypes
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('encyclopedia.loadFailed')
  } finally {
    loading.value = false
  }
})

function getIndustryLabel(value: string) {
  return getLocalizedIndustry(value, locale.value)
}

function getProductAccessText(product: ProductType) {
  if (!product.isProOnly) {
    return t('catalog.free')
  }

  return isProductLocked(product) ? t('catalog.proRequired') : t('catalog.proUnlocked')
}

function getGuideCardImage(card: { resourceSlug?: string; productSlug?: string; imageUrl?: string }) {
  if (card.imageUrl) {
    return card.imageUrl
  }

  if (card.resourceSlug) {
    const resource = resourcesBySlug.value.get(card.resourceSlug)
    if (resource) {
      return getResourceImageUrl(resource)
    }
  }

  if (card.productSlug) {
    const product = productsBySlug.value.get(card.productSlug)
    if (product) {
      return getProductImageUrl(product)
    }
  }

  return null
}

function selectTopic(topicSlug: EncyclopediaTopicSlug) {
  if (topicSlug === selectedTopic.value) {
    return
  }

  router.push({ name: 'encyclopedia-topic', params: { topicSlug }, query: route.query })
}

function openImageFullscreen(src: string | null, alt: string) {
  if (!src) {
    return
  }

  fullscreenImage.value = { src, alt }
}

function closeImageFullscreen() {
  fullscreenImage.value = null
}

function navigateToEntry(slug: string) {
  router.push({
    name: 'encyclopedia-detail',
    params: { slug },
    query: showProProducts.value ? { showPro: '1' } : {},
  })
}
</script>

<template src="./ManufacturingEncyclopediaView.template.html"></template>
