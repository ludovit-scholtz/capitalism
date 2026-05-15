<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { formatMoney } from '@/lib/currencyFormat'
import {
  getLocalizedCategory,
  getLocalizedIndustry,
  getLocalizedProductDescription,
  getLocalizedProductName,
  getLocalizedResourceDescription,
  getLocalizedResourceName,
  getProductImageUrl,
  getResourceImageUrl,
} from '@/lib/catalogPresentation'
import { onCatalogImageError } from '@/lib/catalogImageFallback'
import { isProductLocked } from '@/lib/productAccess'
import { useDebouncedRef } from '@/composables/useDebounce'
import type { EncyclopediaCatalogEntry } from '@/types'

type FilterChip = {
  id: string
  label: string
}

const props = defineProps<{
  entries: EncyclopediaCatalogEntry[]
}>()

const emit = defineEmits<{
  navigate: [slug: string]
}>()

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const search = ref('')
const activeFilter = ref('ALL')
const debouncedSearch = useDebouncedRef(search, 300)

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

const visibleEntries = computed(() =>
  props.entries.filter((entry) => showProProducts.value || entry.kind === 'RESOURCE' || !entry.isProOnly),
)

const hiddenProProductCount = computed(
  () =>
    props.entries.filter((entry) => entry.kind === 'PRODUCT' && entry.isProOnly).length -
    visibleEntries.value.filter((entry) => entry.kind === 'PRODUCT' && entry.isProOnly).length,
)

const filterChips = computed<FilterChip[]>(() => [
  { id: 'ALL', label: t('encyclopedia.filterAll') },
  { id: 'RESOURCE', label: t('encyclopedia.filterRawMaterials') },
  ...new Set(
    visibleEntries.value
      .filter((entry) => entry.kind === 'PRODUCT')
      .map((entry) => entry.industry)
      .filter((industry): industry is string => Boolean(industry)),
  ),
].map((chip) => (typeof chip === 'string' ? { id: chip, label: getLocalizedIndustry(chip, locale.value) } : chip)))

const filteredEntries = computed(() => {
  const query = debouncedSearch.value.trim().toLowerCase()

  return visibleEntries.value.filter((entry) => {
    const matchesFilter =
      activeFilter.value === 'ALL'
      || (activeFilter.value === 'RESOURCE' && entry.kind === 'RESOURCE')
      || entry.industry === activeFilter.value

    if (!matchesFilter) {
      return false
    }

    if (!query) {
      return true
    }

    return [
      getEntryTitle(entry),
      getEntryDescription(entry),
      entry.slug,
      entry.category,
      entry.industry ?? '',
    ]
      .join(' ')
      .toLowerCase()
      .includes(query)
  })
})

function getEntryTitle(entry: EncyclopediaCatalogEntry) {
  return entry.kind === 'RESOURCE'
    ? getLocalizedResourceName(entry, locale.value)
    : getLocalizedProductName(entry, locale.value)
}

function getEntryDescription(entry: EncyclopediaCatalogEntry) {
  if (entry.kind === 'RESOURCE') {
    return getLocalizedResourceDescription(entry, locale.value)
  }

  return getLocalizedProductDescription({ ...entry, industry: entry.industry ?? entry.category, recipes: [] }, locale.value)
}

function getEntryImage(entry: EncyclopediaCatalogEntry) {
  return entry.kind === 'RESOURCE'
    ? getResourceImageUrl(entry)
    : getProductImageUrl({
        name: entry.name,
        slug: entry.slug,
        industry: entry.industry ?? entry.category,
      })
}

function getEntryBadge(entry: EncyclopediaCatalogEntry) {
  return entry.kind === 'RESOURCE'
    ? getLocalizedCategory(entry.category, locale.value)
    : getLocalizedIndustry(entry.industry ?? entry.category, locale.value)
}

function getEntryMeta(entry: EncyclopediaCatalogEntry) {
  if (entry.kind === 'RESOURCE') {
    return [
      `${t('encyclopedia.basePrice')}: ${formatMoney(entry.basePrice, 'EUR', locale.value)}`,
      `${t('encyclopedia.weight')}: ${entry.weightPerUnit ?? 0} kg/${entry.unitSymbol}`,
    ]
  }

  return [
    `${t('encyclopedia.basePrice')}: ${formatMoney(entry.basePrice, 'EUR', locale.value)}`,
    `${t('encyclopedia.energy')}: ${entry.energyConsumptionMwh ?? 0} MWh`,
    `${t('encyclopedia.basicLaborHours')}: ${entry.basicLaborHours ?? 0} h`,
    `${t('encyclopedia.output')}: ${entry.outputQuantity ?? 0} ${entry.unitSymbol}`,
  ]
}

function getProductAccessText(entry: EncyclopediaCatalogEntry) {
  if (!entry.isProOnly) {
    return t('catalog.free')
  }

  return isProductLocked(entry) ? t('catalog.proRequired') : t('catalog.proUnlocked')
}

function navigateToEntry(slug: string) {
  emit('navigate', slug)
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <div class="flex flex-col gap-1">
      <h2 class="m-0">{{ t('encyclopedia.resourcesTitle') }}</h2>
      <p class="text-muted">{{ t('encyclopedia.resourcesHelp') }}</p>
      <p v-if="hiddenProProductCount > 0 && !showProProducts" class="text-muted text-sm">
        {{ t('encyclopedia.proHiddenNotice', { count: hiddenProProductCount }) }}
      </p>
    </div>

    <div class="sticky top-20 z-10 flex flex-col gap-4 rounded-2xl border border-divider bg-card/95 p-4 backdrop-blur-sm">
      <div class="flex flex-col gap-3 md:flex-row md:items-center">
        <label class="sr-only" for="encyclopedia-search">{{ t('encyclopedia.searchLabel') }}</label>
        <input
          id="encyclopedia-search"
          v-model="search"
          type="search"
          class="flex-1 rounded-xl border border-divider bg-page px-4 py-3 text-body"
          :placeholder="t('encyclopedia.searchPlaceholder')"
        />
        <label class="inline-flex items-center gap-2 text-sm font-semibold text-muted">
          <input v-model="showProProducts" type="checkbox" class="accent-[var(--color-primary)]" />
          <span>{{ t('encyclopedia.showProProducts') }}</span>
        </label>
      </div>

      <div class="flex gap-2 overflow-x-auto pb-1">
        <button
          v-for="chip in filterChips"
          :key="chip.id"
          type="button"
          class="shrink-0 rounded-full border px-4 py-2 text-sm font-semibold transition-colors"
          :class="
            activeFilter === chip.id
              ? 'border-brand bg-brand text-black'
              : 'border-divider bg-page text-muted hover:text-body'
          "
          :aria-pressed="activeFilter === chip.id"
          @click="activeFilter = chip.id"
        >
          {{ chip.label }}
        </button>
      </div>
    </div>

    <div class="encyclopedia-grid grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
      <p v-if="filteredEntries.length === 0" class="search-empty-state col-span-full py-12 text-center text-muted">
        {{ t('encyclopedia.searchNoResults') }}
      </p>

      <article
        v-for="entry in filteredEntries"
        :key="entry.id"
        class="resource-card--link overflow-hidden rounded-2xl border border-divider bg-card transition-colors hover:border-brand focus-visible:border-brand focus-visible:outline-none"
        :class="`resource-card--${entry.kind.toLowerCase()}`"
        role="button"
        tabindex="0"
        :aria-label="`${t('encyclopedia.viewDetail')}: ${getEntryTitle(entry)}`"
        @click="navigateToEntry(entry.slug)"
        @keydown.enter="navigateToEntry(entry.slug)"
        @keydown.space.prevent="navigateToEntry(entry.slug)"
      >
        <img
          :src="getEntryImage(entry)"
          :alt="getEntryTitle(entry)"
          class="h-32 w-full bg-page object-cover"
          @error="onCatalogImageError"
        />

        <div class="flex flex-col gap-3 p-4">
          <div class="flex items-start justify-between gap-4">
            <div class="min-w-0">
              <p class="mb-1 text-xs font-bold uppercase tracking-[0.05em] text-muted">
                {{ getEntryBadge(entry) }}
              </p>
              <h3 class="m-0 text-base font-semibold">{{ getEntryTitle(entry) }}</h3>
            </div>
            <span class="shrink-0 rounded-full bg-brand/10 px-2.5 py-1 text-xs font-semibold text-brand">
              {{ entry.unitSymbol }}
            </span>
          </div>

          <div class="flex flex-wrap gap-2">
            <span
              v-if="entry.kind === 'PRODUCT' && entry.isProOnly"
              class="inline-flex w-fit items-center justify-center rounded-full border px-2 py-0.5 text-[0.72rem] font-bold"
              :class="
                isProductLocked(entry)
                  ? 'border-orange-500/50 bg-orange-500/10 text-orange-400'
                  : 'border-green-500/50 bg-green-500/10 text-green-400'
              "
            >
              {{ getProductAccessText(entry) }}
            </span>
            <span
              v-if="entry.kind === 'PRODUCT' && entry.isPerishable"
              class="rounded-full border border-amber-500/40 bg-amber-500/10 px-2 py-0.5 text-[0.72rem] font-semibold text-amber-300"
            >
              {{ t('encyclopedia.perishable') }}
            </span>
          </div>

          <p class="resource-description text-sm text-muted">{{ getEntryDescription(entry) }}</p>

          <div class="resource-meta flex flex-wrap gap-2 text-xs text-muted">
            <span v-for="metaEntry in getEntryMeta(entry)" :key="metaEntry">{{ metaEntry }}</span>
          </div>

          <span class="text-xs font-semibold text-brand">{{ t('encyclopedia.viewDetail') }} →</span>
        </div>
      </article>
    </div>

    <div class="encyclopedia-help-section rounded-2xl border border-divider bg-card p-6 lg:p-8">
      <div class="flex flex-col gap-3">
        <h3 class="m-0">{{ t('encyclopedia.helpSectionTitle') }}</h3>
        <p class="m-0 text-muted">{{ t('encyclopedia.helpSectionSubtitle') }}</p>
        <div class="grid grid-cols-1 gap-4 md:grid-cols-3">
          <article class="rounded-xl border border-divider bg-page p-4">
            <h4 class="m-0 text-base">{{ t('encyclopedia.gameplayGuideCardMarketsTitle') }}</h4>
            <p class="mt-2 text-sm text-muted">{{ t('encyclopedia.gameplayGuideCardMarketsBody') }}</p>
          </article>
          <article class="rounded-xl border border-divider bg-page p-4">
            <h4 class="m-0 text-base">{{ t('encyclopedia.gameplayGuideCardFlowTitle') }}</h4>
            <p class="mt-2 text-sm text-muted">{{ t('encyclopedia.gameplayGuideCardFlowBody') }}</p>
          </article>
          <article class="rounded-xl border border-divider bg-page p-4">
            <h4 class="m-0 text-base">{{ t('encyclopedia.gameplayGuideCardIterationTitle') }}</h4>
            <p class="mt-2 text-sm text-muted">{{ t('encyclopedia.gameplayGuideCardIterationBody') }}</p>
          </article>
        </div>
      </div>
    </div>
  </div>
</template>
