<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
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
import type { EncyclopediaCatalogEntry, EncyclopediaEntryDetail, EncyclopediaRecipeIngredient } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)
const detail = ref<EncyclopediaEntryDetail | null>(null)

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

const selectedSlug = computed(() => String(route.params.slug ?? ''))
const selectedEntry = computed(() => detail.value?.entry ?? null)
const hiddenProduct = computed(() => selectedEntry.value?.kind === 'PRODUCT' && selectedEntry.value.isProOnly && !showProProducts.value)

const producedByRecipes = computed(() =>
  (detail.value?.producedByRecipes ?? []).filter((recipe) => showProProducts.value || !recipe.output.isProOnly),
)
const usedInRecipes = computed(() =>
  (detail.value?.usedInRecipes ?? []).filter((recipe) => showProProducts.value || !recipe.output.isProOnly),
)

watch(
  selectedSlug,
  async (slug) => {
    if (!slug) {
      return
    }

    try {
      loading.value = true
      error.value = null
      detail.value = null

      const result = await gqlRequest<{ encyclopediaResourceDetail: EncyclopediaEntryDetail | null }>(
        `
          query EncyclopediaResourceDetail($slug: String!) {
            encyclopediaResourceDetail(slug: $slug) {
              entry {
                id
                kind
                name
                slug
                category
                industry
                description
                imageUrl
                isPerishable
                isProOnly
                isUnlockedForCurrentPlayer
                basePrice
                weightPerUnit
                baseCraftTicks
                outputQuantity
                energyConsumptionMwh
                basicLaborHours
                unitName
                unitSymbol
              }
              producedByRecipes {
                id
                recipeName
                buildingType
                outputQuantity
                output {
                  id
                  kind
                  name
                  slug
                  category
                  industry
                  description
                  imageUrl
                  isPerishable
                  isProOnly
                  isUnlockedForCurrentPlayer
                  basePrice
                  weightPerUnit
                  baseCraftTicks
                  outputQuantity
                  energyConsumptionMwh
                  basicLaborHours
                  unitName
                  unitSymbol
                }
                inputs {
                  kind
                  name
                  slug
                  category
                  industry
                  imageUrl
                  quantity
                  unitName
                  unitSymbol
                  isPerishable
                  isProOnly
                  isUnlockedForCurrentPlayer
                }
              }
              usedInRecipes {
                id
                recipeName
                buildingType
                outputQuantity
                output {
                  id
                  kind
                  name
                  slug
                  category
                  industry
                  description
                  imageUrl
                  isPerishable
                  isProOnly
                  isUnlockedForCurrentPlayer
                  basePrice
                  weightPerUnit
                  baseCraftTicks
                  outputQuantity
                  energyConsumptionMwh
                  basicLaborHours
                  unitName
                  unitSymbol
                }
                inputs {
                  kind
                  name
                  slug
                  category
                  industry
                  imageUrl
                  quantity
                  unitName
                  unitSymbol
                  isPerishable
                  isProOnly
                  isUnlockedForCurrentPlayer
                }
              }
            }
          }
        `,
        { slug },
      )

      detail.value = result.encyclopediaResourceDetail
    } catch (reason: unknown) {
      error.value = reason instanceof Error ? reason.message : t('resourceDetail.loadFailed')
    } finally {
      loading.value = false
    }
  },
  { immediate: true },
)

function getEntryTitle(entry: EncyclopediaCatalogEntry | EncyclopediaRecipeIngredient | null) {
  if (!entry) {
    return '—'
  }

  return entry.kind === 'RESOURCE'
    ? getLocalizedResourceName(entry, locale.value)
    : getLocalizedProductName(entry, locale.value)
}

function getEntryDescription(entry: EncyclopediaCatalogEntry | null) {
  if (!entry) {
    return ''
  }

  if (entry.kind === 'RESOURCE') {
    return getLocalizedResourceDescription(entry, locale.value)
  }

  return getLocalizedProductDescription({ ...entry, industry: entry.industry ?? entry.category, recipes: [] }, locale.value)
}

function getEntryImage(entry: EncyclopediaCatalogEntry | EncyclopediaRecipeIngredient | null) {
  if (!entry) {
    return null
  }

  return entry.kind === 'RESOURCE'
    ? getResourceImageUrl(entry)
    : getProductImageUrl({ name: entry.name, slug: entry.slug, industry: entry.industry ?? entry.category })
}

function getEntryBadge(entry: EncyclopediaCatalogEntry | null) {
  if (!entry) {
    return ''
  }

  return entry.kind === 'RESOURCE'
    ? getLocalizedCategory(entry.category, locale.value)
    : getLocalizedIndustry(entry.industry ?? entry.category, locale.value)
}

function getBuildingGuideRoute(buildingType: string) {
  if (buildingType === 'FACTORY') {
    return { name: 'encyclopedia-topic', params: { topicSlug: 'factory-layout-help' }, query: route.query }
  }

  return { name: 'encyclopedia-topic', params: { topicSlug: 'resources-definition' }, query: route.query }
}

function getBuildingTypeLabel(buildingType: string) {
  if (buildingType === 'FACTORY') {
    return t('encyclopedia.buildingTypeFactory')
  }

  if (buildingType === 'MINE') {
    return t('encyclopedia.buildingTypeMine')
  }

  return buildingType.replace(/_/g, ' ')
}

function goBack() {
  router.push({ name: 'encyclopedia', query: showProProducts.value ? { showPro: '1' } : {} })
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
  <div class="container flex flex-col gap-6 py-8 pb-16">
    <nav class="flex flex-wrap items-center justify-between gap-4">
      <button type="button" class="text-sm font-semibold text-brand" @click="goBack">
        ← {{ t('resourceDetail.backToEncyclopedia') }}
      </button>

      <label class="inline-flex items-center gap-2 text-sm font-semibold text-muted">
        <input v-model="showProProducts" type="checkbox" class="accent-[var(--color-primary)]" />
        <span>{{ t('encyclopedia.showProProducts') }}</span>
      </label>
    </nav>

    <div
      v-if="loading"
      class="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3"
    >
      <div
        v-for="index in 6"
        :key="index"
        class="h-48 animate-pulse rounded-2xl border border-divider bg-card"
      />
    </div>
    <div
      v-else-if="error"
      class="rounded-2xl border border-divider bg-card p-6 text-center text-muted"
      role="alert"
    >
      {{ error }}
    </div>
    <div
      v-else-if="!selectedEntry || hiddenProduct"
      class="grid justify-items-center gap-4 rounded-2xl border border-divider bg-card p-8 text-center"
    >
      <h2 class="m-0">{{ t('resourceDetail.notFound') }}</h2>
      <p class="m-0 text-muted">
        {{ hiddenProduct ? t('resourceDetail.hiddenByFilterHint') : t('resourceDetail.notFoundHint') }}
      </p>
      <button type="button" class="rounded-xl bg-brand px-4 py-2 font-semibold text-black" @click="goBack">
        {{ t('resourceDetail.backToEncyclopedia') }}
      </button>
    </div>

    <template v-else>
      <section class="grid gap-6 rounded-3xl border border-divider bg-card p-6 lg:grid-cols-[320px_minmax(0,1fr)] lg:p-8">
        <img
          :src="getEntryImage(selectedEntry) ?? undefined"
          :alt="getEntryTitle(selectedEntry)"
          class="h-[200px] w-full rounded-2xl bg-page object-cover lg:h-full"
          @error="onCatalogImageError"
        />

        <div class="flex flex-col gap-4">
          <div class="flex flex-wrap gap-2">
            <span class="rounded-full border border-brand/30 bg-brand/10 px-3 py-1 text-xs font-semibold text-brand">
              {{ getEntryBadge(selectedEntry) }}
            </span>
            <span class="rounded-full border border-divider bg-page px-3 py-1 text-xs font-semibold text-muted">
              {{ selectedEntry.unitName }} ({{ selectedEntry.unitSymbol }})
            </span>
            <span
              v-if="selectedEntry.kind === 'PRODUCT' && selectedEntry.isPerishable"
              class="rounded-full border border-amber-500/40 bg-amber-500/10 px-3 py-1 text-xs font-semibold text-amber-300"
            >
              {{ t('encyclopedia.perishable') }}
            </span>
          </div>

          <div class="flex flex-col gap-2">
            <h1 class="m-0">{{ getEntryTitle(selectedEntry) }}</h1>
            <p class="m-0 text-muted">{{ getEntryDescription(selectedEntry) }}</p>
          </div>

          <div class="resource-meta flex flex-wrap gap-3 text-sm">
            <div class="rounded-xl border border-divider bg-page px-4 py-3">
              <div class="text-xs uppercase tracking-[0.05em] text-muted">{{ t('resourceDetail.basePrice') }}</div>
              <div class="font-semibold">{{ formatMoney(selectedEntry.basePrice, 'EUR', locale) }}</div>
            </div>
            <div
              v-if="selectedEntry.kind === 'RESOURCE'"
              class="rounded-xl border border-divider bg-page px-4 py-3"
            >
              <div class="text-xs uppercase tracking-[0.05em] text-muted">{{ t('resourceDetail.weight') }}</div>
              <div class="font-semibold">{{ selectedEntry.weightPerUnit ?? 0 }} kg/{{ selectedEntry.unitSymbol }}</div>
            </div>
            <div
              v-if="selectedEntry.kind === 'PRODUCT'"
              class="rounded-xl border border-divider bg-page px-4 py-3"
            >
              <div class="text-xs uppercase tracking-[0.05em] text-muted">{{ t('resourceDetail.craftTicks') }}</div>
              <div class="font-semibold">{{ selectedEntry.baseCraftTicks ?? 0 }}</div>
            </div>
            <div
              v-if="selectedEntry.kind === 'PRODUCT'"
              class="rounded-xl border border-divider bg-page px-4 py-3"
            >
              <div class="text-xs uppercase tracking-[0.05em] text-muted">{{ t('encyclopedia.output') }}</div>
              <div class="font-semibold">{{ selectedEntry.outputQuantity ?? 0 }} {{ selectedEntry.unitSymbol }}</div>
            </div>
          </div>
        </div>
      </section>

      <section class="grid gap-4">
        <div class="flex flex-col gap-1">
          <h2 class="m-0">{{ t('encyclopedia.detailProducedByTitle') }}</h2>
          <p class="m-0 text-muted">{{ t('encyclopedia.detailProducedByHelp') }}</p>
        </div>

        <div v-if="producedByRecipes.length === 0" class="rounded-2xl border border-divider bg-card p-6 text-muted">
          {{ t('encyclopedia.detailProducedByEmpty') }}
        </div>

        <div v-else class="grid gap-4 lg:grid-cols-2">
          <article
            v-for="recipe in producedByRecipes"
            :key="recipe.id"
            class="rounded-2xl border border-divider bg-card p-5"
          >
            <div class="mb-4 flex items-start justify-between gap-4">
              <div>
                <p class="mb-1 text-xs font-semibold uppercase tracking-[0.05em] text-muted">
                  {{ t('encyclopedia.recipe') }}
                </p>
                <h3 class="m-0 text-base">{{ recipe.recipeName }}</h3>
              </div>
              <RouterLink
                :to="getBuildingGuideRoute(recipe.buildingType)"
                class="rounded-full border border-divider bg-page px-3 py-1 text-xs font-semibold text-muted"
              >
                {{ getBuildingTypeLabel(recipe.buildingType) }}
              </RouterLink>
            </div>

            <div class="flex flex-wrap items-center gap-3">
              <template v-if="recipe.inputs.length > 0">
                <button
                  v-for="input in recipe.inputs"
                  :key="`${recipe.id}-${input.slug}`"
                  type="button"
                  class="flex items-center gap-3 rounded-2xl border border-divider bg-page px-3 py-2 text-left"
                  @click="navigateToEntry(input.slug)"
                >
                  <img
                    :src="getEntryImage(input) ?? undefined"
                    :alt="getEntryTitle(input)"
                    class="h-12 w-12 rounded-xl bg-card object-cover"
                    @error="onCatalogImageError"
                  />
                  <span>
                    <strong class="block">{{ input.quantity }} {{ input.unitSymbol }}</strong>
                    <span class="text-sm text-muted">{{ getEntryTitle(input) }}</span>
                  </span>
                </button>
                <span class="text-2xl text-muted">→</span>
              </template>

              <button
                type="button"
                class="flex items-center gap-3 rounded-2xl border border-brand/20 bg-brand/5 px-3 py-2 text-left"
                @click="navigateToEntry(recipe.output.slug)"
              >
                <img
                  :src="getEntryImage(recipe.output) ?? undefined"
                  :alt="getEntryTitle(recipe.output)"
                  class="h-12 w-12 rounded-xl bg-card object-cover"
                  @error="onCatalogImageError"
                />
                <span>
                  <strong class="block">{{ recipe.outputQuantity }} {{ recipe.output.unitSymbol }}</strong>
                  <span class="text-sm text-muted">{{ getEntryTitle(recipe.output) }}</span>
                </span>
              </button>
            </div>
          </article>
        </div>
      </section>

      <section class="grid gap-4">
        <div class="flex flex-col gap-1">
          <h2 class="m-0">{{ t('encyclopedia.detailUsedInTitle') }}</h2>
          <p class="m-0 text-muted">{{ t('encyclopedia.detailUsedInHelp') }}</p>
        </div>

        <div v-if="usedInRecipes.length === 0" class="rounded-2xl border border-divider bg-card p-6 text-muted">
          {{ t('encyclopedia.detailUsedInEmpty') }}
        </div>

        <div v-else class="grid gap-4 lg:grid-cols-2">
          <article
            v-for="recipe in usedInRecipes"
            :key="recipe.id"
            class="rounded-2xl border border-divider bg-card p-5"
          >
            <div class="mb-4 flex items-start justify-between gap-4">
              <div>
                <p class="mb-1 text-xs font-semibold uppercase tracking-[0.05em] text-muted">
                  {{ t('encyclopedia.recipe') }}
                </p>
                <h3 class="m-0 text-base">{{ recipe.recipeName }}</h3>
              </div>
              <RouterLink
                :to="getBuildingGuideRoute(recipe.buildingType)"
                class="rounded-full border border-divider bg-page px-3 py-1 text-xs font-semibold text-muted"
              >
                {{ getBuildingTypeLabel(recipe.buildingType) }}
              </RouterLink>
            </div>

            <div class="flex flex-wrap items-center gap-3">
              <button
                v-for="input in recipe.inputs"
                :key="`${recipe.id}-${input.slug}`"
                type="button"
                class="flex items-center gap-3 rounded-2xl border border-divider bg-page px-3 py-2 text-left"
                @click="navigateToEntry(input.slug)"
              >
                <img
                  :src="getEntryImage(input) ?? undefined"
                  :alt="getEntryTitle(input)"
                  class="h-12 w-12 rounded-xl bg-card object-cover"
                  @error="onCatalogImageError"
                />
                <span>
                  <strong class="block">{{ input.quantity }} {{ input.unitSymbol }}</strong>
                  <span class="text-sm text-muted">{{ getEntryTitle(input) }}</span>
                </span>
              </button>
              <span class="text-2xl text-muted">→</span>
              <button
                type="button"
                class="flex items-center gap-3 rounded-2xl border border-brand/20 bg-brand/5 px-3 py-2 text-left"
                @click="navigateToEntry(recipe.output.slug)"
              >
                <img
                  :src="getEntryImage(recipe.output) ?? undefined"
                  :alt="getEntryTitle(recipe.output)"
                  class="h-12 w-12 rounded-xl bg-card object-cover"
                  @error="onCatalogImageError"
                />
                <span>
                  <strong class="block">{{ recipe.outputQuantity }} {{ recipe.output.unitSymbol }}</strong>
                  <span class="text-sm text-muted">{{ getEntryTitle(recipe.output) }}</span>
                </span>
              </button>
            </div>
          </article>
        </div>
      </section>
    </template>
  </div>
</template>
