<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import { getProductImageUrl, getResourceImageUrl } from '@/lib/catalogPresentation'
import {
  onboardingGuideCards,
  manufacturingGuideCards,
  manufacturingGuideTopics,
  salesShopGuideCards,
  salesShopGuideTopics,
  forexGuideCards,
  forexGuideTopics,
  stockExchangeGuideCards,
  stockExchangeGuideTopics,
  localizeGuideImageUrl,
} from '@/lib/encyclopediaGuideData'
import EncyclopediaResourcesCatalog from '@/components/encyclopedia/EncyclopediaResourcesCatalog.vue'
import type { ProductType, ResourceType } from '@/types'

type EncyclopediaTopicSlug = 'onboarding-help' | 'factory-layout-help' | 'sales-shop-help' | 'forex-trading-help' | 'stock-exchange-help' | 'resources-definition'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)
const resources = ref<ResourceType[]>([])
const products = ref<ProductType[]>([])
const fullscreenImage = ref<{ src: string; alt: string } | null>(null)

const topicSlugs: EncyclopediaTopicSlug[] = ['onboarding-help', 'factory-layout-help', 'sales-shop-help', 'forex-trading-help', 'stock-exchange-help', 'resources-definition']

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

function getGuideCardImage(card: { resourceSlug?: string; productSlug?: string; imageUrl?: string }) {
  if (card.imageUrl) {
    return localizeGuideImageUrl(card.imageUrl, locale.value)
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
  const showPro = route.query.showPro === '1'
  router.push({
    name: 'encyclopedia-detail',
    params: { slug },
    query: showPro ? { showPro: '1' } : {},
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
          <strong>{{ resources.length + products.length }}</strong>
          <span class="text-muted text-sm">{{ t('encyclopedia.resourcesCount') }}</span>
        </div>
        <div class="stat-card bg-card border border-divider rounded-2xl px-5 py-4 min-w-[120px] grid gap-1 max-sm:flex-1">
          <strong>{{ products.length }}</strong>
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

      <div v-if="selectedTopic === 'forex-trading-help'" class="encyclopedia-help-section rounded-2xl border border-divider bg-card p-6 lg:p-8 flex flex-col gap-8">
        <section class="flex flex-col gap-3">
          <h2 class="m-0">{{ t('encyclopedia.forexGuideTitle') }}</h2>
          <p class="text-muted m-0">{{ t('encyclopedia.forexGuideSubtitle') }}</p>
          <h3 class="m-0 text-base mt-2">{{ t('encyclopedia.forexGuideTopicsTitle') }}</h3>
          <ul class="m-0 pl-5 text-sm text-muted flex flex-col gap-1">
            <li v-for="topic in forexGuideTopics" :key="topic">
              {{ t(topic) }}
            </li>
          </ul>
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 mt-2">
            <article v-for="card in forexGuideCards" :key="card.titleKey" class="forex-help-card rounded-xl border border-divider bg-page overflow-hidden">
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

      <div v-if="selectedTopic === 'stock-exchange-help'" class="encyclopedia-help-section rounded-2xl border border-divider bg-card p-6 lg:p-8 flex flex-col gap-8">
        <section class="flex flex-col gap-3">
          <h2 class="m-0">{{ t('encyclopedia.stockExchangeGuideTitle') }}</h2>
          <p class="text-muted m-0">{{ t('encyclopedia.stockExchangeGuideSubtitle') }}</p>
          <h3 class="m-0 text-base mt-2">{{ t('encyclopedia.stockExchangeGuideTopicsTitle') }}</h3>
          <ul class="m-0 pl-5 text-sm text-muted flex flex-col gap-1">
            <li v-for="topic in stockExchangeGuideTopics" :key="topic">
              {{ t(topic) }}
            </li>
          </ul>
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 mt-2">
            <article v-for="card in stockExchangeGuideCards" :key="card.titleKey" class="stock-exchange-help-card rounded-xl border border-divider bg-page overflow-hidden">
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

      <EncyclopediaResourcesCatalog v-if="selectedTopic === 'resources-definition'" :resources="resources" :products="products" @navigate="navigateToEntry" />
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
