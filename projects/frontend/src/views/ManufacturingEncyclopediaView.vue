<script setup lang="ts">
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

type EncyclopediaTopicSlug = 'onboarding-help' | 'factory-layout-help' | 'sales-shop-help' | 'resources-definition'

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

const topicSlugs: EncyclopediaTopicSlug[] = ['onboarding-help', 'factory-layout-help', 'sales-shop-help', 'resources-definition']

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

<template>
  <div class="container py-8 pb-16 flex flex-col gap-8">
    <nav class="encyclopedia-topic-nav rounded-2xl border border-divider bg-card p-2 flex flex-wrap gap-2" :aria-label="t('encyclopedia.topicMenuLabel')">
      <button
        v-for="topic in topicMenu"
        :key="topic.slug"
        type="button"
        class="topic-tab px-4 py-2 rounded-xl font-semibold text-sm transition-colors"
        :class="topic.slug === selectedTopic ? 'bg-brand text-black' : 'bg-page text-muted hover:text-body'"
        :aria-pressed="topic.slug === selectedTopic"
        @click="selectTopic(topic.slug)"
      >
        {{ topic.label }}
      </button>
    </nav>

    <!-- Hero -->
    <header class="flex flex-wrap justify-between items-end gap-4 max-sm:items-stretch">
      <div>
        <p class="text-sm text-muted mb-1">{{ t('encyclopedia.eyebrow') }}</p>
        <h1 class="m-0">{{ t('encyclopedia.title') }}</h1>
        <p class="text-muted mt-1">{{ t('encyclopedia.subtitle') }}</p>
      </div>
      <div class="flex flex-wrap gap-4 justify-end max-sm:justify-stretch">
        <div class="stat-card bg-card border border-divider rounded-2xl px-5 py-4 min-w-[120px] grid gap-1 max-sm:flex-1">
          <strong>{{ catalogEntries.length }}</strong>
          <span class="text-muted text-sm">{{ t('encyclopedia.resourcesCount') }}</span>
        </div>
        <div class="stat-card bg-card border border-divider rounded-2xl px-5 py-4 min-w-[120px] grid gap-1 max-sm:flex-1">
          <strong>{{ visibleProducts.length }}</strong>
          <span class="text-muted text-sm">{{ t('encyclopedia.productsCount') }}</span>
        </div>
      </div>
    </header>

    <!-- Loading / error -->
    <div v-if="loading" class="text-muted py-12 text-center">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="bg-card border border-divider rounded-2xl p-6 text-center text-muted" role="alert">
      {{ error }}
    </div>

    <!-- Main section -->
    <section v-else class="flex flex-col gap-2">
      <div v-if="selectedTopic === 'onboarding-help'" class="encyclopedia-help-section rounded-2xl border border-divider bg-card p-6 lg:p-8 flex flex-col gap-8">
        <section class="flex flex-col gap-3">
          <h2 class="m-0">{{ t('encyclopedia.onboardingGuideTitle') }}</h2>
          <p class="text-muted m-0">{{ t('encyclopedia.onboardingGuideSubtitle') }}</p>
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 mt-2">
            <article v-for="card in onboardingGuideCards" :key="card.titleKey" class="onboarding-help-card rounded-xl border border-divider bg-page overflow-hidden">
              <button
                v-if="getGuideCardImage(card)"
                type="button"
                class="help-image-trigger block w-full"
                :aria-label="t('encyclopedia.openImageFullscreen', { title: t(card.titleKey) })"
                @click="openImageFullscreen(getGuideCardImage(card), t(card.titleKey))"
              >
                <img :src="getGuideCardImage(card) ?? undefined" :alt="t(card.titleKey)" class="help-card-image w-full h-36 object-cover" />
              </button>
              <div class="p-4 flex flex-col gap-2">
                <h3 class="m-0 text-base">{{ t(card.titleKey) }}</h3>
                <p class="m-0 text-sm text-muted">{{ t(card.bodyKey) }}</p>
              </div>
            </article>
          </div>
        </section>
      </div>

      <div v-if="selectedTopic === 'factory-layout-help'" class="encyclopedia-help-section rounded-2xl border border-divider bg-card p-6 lg:p-8 flex flex-col gap-8">
        <section class="flex flex-col gap-3">
          <h2 class="m-0">{{ t('encyclopedia.manufacturingGuideTitle') }}</h2>
          <p class="text-muted m-0">{{ t('encyclopedia.manufacturingGuideSubtitle') }}</p>
          <h3 class="m-0 text-base mt-2">{{ t('encyclopedia.manufacturingGuideTopicsTitle') }}</h3>
          <ul class="m-0 pl-5 text-sm text-muted flex flex-col gap-1">
            <li v-for="topic in manufacturingGuideTopics" :key="topic">
              {{ t(topic) }}
            </li>
          </ul>
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 mt-2">
            <article v-for="card in manufacturingGuideCards" :key="card.titleKey" class="manufacturing-help-card rounded-xl border border-divider bg-page overflow-hidden">
              <button
                v-if="getGuideCardImage(card)"
                type="button"
                class="help-image-trigger block w-full"
                :aria-label="t('encyclopedia.openImageFullscreen', { title: t(card.titleKey) })"
                @click="openImageFullscreen(getGuideCardImage(card), t(card.titleKey))"
              >
                <img :src="getGuideCardImage(card) ?? undefined" :alt="t(card.titleKey)" class="help-card-image w-full h-28 object-cover" />
              </button>
              <div class="p-4 flex flex-col gap-2">
                <h3 class="m-0 text-base">{{ t(card.titleKey) }}</h3>
                <p class="m-0 text-sm text-muted">{{ t(card.bodyKey) }}</p>
              </div>
            </article>
          </div>
        </section>
      </div>

      <div v-if="selectedTopic === 'sales-shop-help'" class="encyclopedia-help-section rounded-2xl border border-divider bg-card p-6 lg:p-8 flex flex-col gap-8">
        <section class="flex flex-col gap-3">
          <h2 class="m-0">{{ t('encyclopedia.salesShopGuideTitle') }}</h2>
          <p class="text-muted m-0">{{ t('encyclopedia.salesShopGuideSubtitle') }}</p>
          <h3 class="m-0 text-base mt-2">{{ t('encyclopedia.salesShopGuideTopicsTitle') }}</h3>
          <ul class="m-0 pl-5 text-sm text-muted flex flex-col gap-1">
            <li v-for="topic in salesShopGuideTopics" :key="topic">
              {{ t(topic) }}
            </li>
          </ul>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-2">
            <article v-for="card in salesShopGuideCards" :key="card.titleKey" class="sales-shop-help-card rounded-xl border border-divider bg-page overflow-hidden">
              <button
                v-if="getGuideCardImage(card)"
                type="button"
                class="help-image-trigger block w-full"
                :aria-label="t('encyclopedia.openImageFullscreen', { title: t(card.titleKey) })"
                @click="openImageFullscreen(getGuideCardImage(card), t(card.titleKey))"
              >
                <img :src="getGuideCardImage(card) ?? undefined" :alt="t(card.titleKey)" class="help-card-image w-full h-36 object-cover" />
              </button>
              <div class="p-4 flex flex-col gap-2">
                <h3 class="m-0 text-base">{{ t(card.titleKey) }}</h3>
                <p class="m-0 text-sm text-muted">{{ t(card.bodyKey) }}</p>
              </div>
            </article>
          </div>
        </section>
      </div>

      <div v-if="selectedTopic === 'resources-definition'" class="flex flex-col gap-2">
        <div class="flex flex-col gap-1">
          <h2 class="m-0">{{ t('encyclopedia.resourcesTitle') }}</h2>
          <p class="text-muted">{{ t('encyclopedia.resourcesHelp') }}</p>
          <p v-if="hiddenProProductCount > 0 && !showProProducts" class="text-muted text-sm">
            {{ t('encyclopedia.proHiddenNotice', { count: hiddenProProductCount }) }}
          </p>
        </div>

        <!-- Filters -->
        <div class="flex items-center gap-4 flex-wrap mt-6">
          <input v-model="search" type="search" class="flex-1 min-w-60 border border-divider rounded-xl bg-page text-body px-4 py-3" :placeholder="t('encyclopedia.searchPlaceholder')" />
          <select v-model="industry" class="border border-divider rounded-xl bg-page text-body px-4 py-3" :aria-label="t('encyclopedia.filterByIndustry')">
            <option v-for="option in industries" :key="option" :value="option">
              {{ option === 'ALL' ? t('encyclopedia.allIndustries') : getIndustryLabel(option) }}
            </option>
          </select>
          <label class="inline-flex items-center gap-2 text-muted font-semibold cursor-pointer">
            <input v-model="showProProducts" type="checkbox" class="accent-[var(--color-primary)]" />
            <span>{{ t('encyclopedia.showProProducts') }}</span>
          </label>
        </div>

        <!-- Resource grid -->
        <div class="encyclopedia-grid grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6 gap-4 mt-6">
          <p v-if="catalogEntries.length === 0" class="search-empty-state text-center col-span-full py-12 text-muted">
            {{ t('encyclopedia.searchNoResults') }}
          </p>
          <article
            v-for="entry in catalogEntries"
            :key="entry.id"
            class="resource-card--link bg-card border border-divider rounded-2xl overflow-hidden cursor-pointer hover:border-brand focus-visible:border-brand focus-visible:outline-none transition-colors"
            :class="`resource-card--${entry.kind}`"
            role="button"
            tabindex="0"
            :aria-label="t('encyclopedia.viewDetail') + ': ' + entry.title"
            @click="navigateToEntry(entry.slug)"
            @keydown.enter="navigateToEntry(entry.slug)"
            @keydown.space.prevent="navigateToEntry(entry.slug)"
          >
            <img v-if="entry.imageUrl" :src="entry.imageUrl ?? undefined" :alt="entry.title" class="w-full h-32 object-cover bg-page" />
            <div class="p-4 flex flex-col gap-3">
              <!-- Heading row -->
              <div class="flex justify-between items-start gap-4">
                <div>
                  <p class="text-xs font-bold uppercase tracking-[0.05em] text-muted mb-1">
                    {{ entry.badge }}
                  </p>
                  <h3 class="m-0 text-base font-semibold">{{ entry.title }}</h3>
                </div>
                <span class="px-2.5 py-1 rounded-full bg-brand/10 text-brand text-xs font-semibold shrink-0">
                  {{ entry.pill }}
                </span>
              </div>

              <!-- Pro access badge -->
              <span
                v-if="entry.accessText"
                class="inline-flex items-center justify-center w-fit px-2 py-0.5 rounded-full border text-[0.72rem] font-bold"
                :class="{
                  'text-orange-400 border-orange-500/50 bg-orange-500/10': entry.accessClass === 'locked',
                  'text-green-400 border-green-500/50 bg-green-500/10': entry.accessClass === 'unlocked',
                }"
              >
                {{ entry.accessText }}
              </span>

              <!-- Description (keep class for E2E) -->
              <p class="resource-description text-sm text-muted">{{ entry.description }}</p>

              <!-- Meta (keep class for E2E) -->
              <div class="resource-meta flex flex-wrap gap-2 text-xs text-muted">
                <span v-for="metaEntry in entry.meta" :key="metaEntry">{{ metaEntry }}</span>
              </div>

              <span class="text-xs font-semibold text-brand">{{ t('encyclopedia.viewDetail') }} →</span>
            </div>
          </article>
        </div>

        <div class="encyclopedia-help-section mt-10 rounded-2xl border border-divider bg-card p-6 lg:p-8 flex flex-col gap-8">
          <section class="flex flex-col gap-3">
            <h3 class="m-0">{{ t('encyclopedia.helpSectionTitle') }}</h3>
            <p class="text-muted m-0">{{ t('encyclopedia.helpSectionSubtitle') }}</p>
            <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mt-2">
              <article class="rounded-xl border border-divider bg-page p-4 flex flex-col gap-2">
                <h4 class="m-0 text-base">{{ t('encyclopedia.gameplayGuideCardMarketsTitle') }}</h4>
                <p class="m-0 text-sm text-muted">{{ t('encyclopedia.gameplayGuideCardMarketsBody') }}</p>
              </article>
              <article class="rounded-xl border border-divider bg-page p-4 flex flex-col gap-2">
                <h4 class="m-0 text-base">{{ t('encyclopedia.gameplayGuideCardFlowTitle') }}</h4>
                <p class="m-0 text-sm text-muted">{{ t('encyclopedia.gameplayGuideCardFlowBody') }}</p>
              </article>
              <article class="rounded-xl border border-divider bg-page p-4 flex flex-col gap-2">
                <h4 class="m-0 text-base">{{ t('encyclopedia.gameplayGuideCardIterationTitle') }}</h4>
                <p class="m-0 text-sm text-muted">{{ t('encyclopedia.gameplayGuideCardIterationBody') }}</p>
              </article>
            </div>
          </section>
        </div>
      </div>
    </section>

    <div
      v-if="fullscreenImage"
      class="fixed inset-0 z-[200] bg-black/90 p-4 lg:p-8 flex items-center justify-center"
      role="dialog"
      aria-modal="true"
      :aria-label="t('encyclopedia.fullscreenDialogLabel')"
      @click.self="closeImageFullscreen"
      @keydown.esc="closeImageFullscreen"
    >
      <button
        type="button"
        class="absolute top-4 right-4 rounded-lg bg-page/90 text-body px-3 py-2 text-sm font-semibold"
        :aria-label="t('encyclopedia.closeFullscreenImage')"
        @click="closeImageFullscreen"
      >
        {{ t('encyclopedia.closeFullscreenImage') }}
      </button>
      <img :src="fullscreenImage.src" :alt="fullscreenImage.alt" class="fullscreen-help-image max-w-full max-h-full object-contain rounded-xl border border-divider" />
    </div>
  </div>
</template>
