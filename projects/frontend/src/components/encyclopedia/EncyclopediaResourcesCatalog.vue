<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
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

const props = defineProps<{
  resources: ResourceType[]
  products: ProductType[]
}>()

const emit = defineEmits<{
  navigate: [slug: string]
}>()

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const search = ref('')
const industry = ref('ALL')

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

const visibleProducts = computed(() => (showProProducts.value ? props.products : props.products.filter((product) => !product.isProOnly)))

const industries = computed(() => ['ALL', ...new Set(visibleProducts.value.map((product) => product.industry))])

const hiddenProProductCount = computed(() => (showProProducts.value ? 0 : props.products.filter((product) => product.isProOnly).length))

const catalogEntries = computed<CatalogEntry[]>(() => {
  const query = search.value.trim().toLowerCase()
  const entries: CatalogEntry[] = [
    ...props.resources.map((resource) => {
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

function getIndustryLabel(value: string) {
  return getLocalizedIndustry(value, locale.value)
}

function getProductAccessText(product: ProductType) {
  if (!product.isProOnly) {
    return t('catalog.free')
  }
  return isProductLocked(product) ? t('catalog.proRequired') : t('catalog.proUnlocked')
}

function navigateToEntry(slug: string) {
  emit('navigate', slug)
}
</script>

<template>
  <div class="flex flex-col gap-2">
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
</template>
