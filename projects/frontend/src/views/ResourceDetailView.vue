<script setup lang="ts">
import { computed, ref, watch } from 'vue'
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
  getLocalizedRecipeSummary,
  getLocalizedResourceDescription,
  getLocalizedResourceName,
  getLocalizedUnitName,
  getProductImageUrl,
  getResourceImageUrl,
} from '@/lib/catalogPresentation'
import type { ProductType, ResourceType } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)
const resources = ref<ResourceType[]>([])
const products = ref<ProductType[]>([])

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

const visibleProducts = computed(() => (showProProducts.value ? products.value : products.value.filter((product) => !product.isProOnly)))

const selectedResource = computed(() => resources.value.find((resource) => resource.slug === selectedSlug.value) ?? null)

const hiddenProduct = computed(() => {
  const product = products.value.find((candidate) => candidate.slug === selectedSlug.value) ?? null
  return product?.isProOnly && !showProProducts.value ? product : null
})

const selectedProduct = computed(() => visibleProducts.value.find((product) => product.slug === selectedSlug.value) ?? null)

const relatedProducts = computed(() => {
  if (selectedResource.value) {
    return visibleProducts.value.filter((product) => product.recipes.some((recipe) => recipe.resourceType?.slug === selectedResource.value?.slug))
  }

  if (selectedProduct.value) {
    const selectedProductValue = selectedProduct.value

    return visibleProducts.value.filter((product) => product.id !== selectedProductValue.id && product.recipes.some((recipe) => recipe.inputProductType?.slug === selectedProductValue.slug))
  }

  return []
})

watch(
  () => route.params.slug as string,
  async (slug) => {
    if (!slug) {
      return
    }

    try {
      loading.value = true
      error.value = null

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
      error.value = reason instanceof Error ? reason.message : t('resourceDetail.loadFailed')
    } finally {
      loading.value = false
    }
  },
  { immediate: true },
)

function getSelectedImageUrl() {
  if (selectedResource.value) {
    return getResourceImageUrl(selectedResource.value)
  }

  if (selectedProduct.value) {
    return getProductImageUrl(selectedProduct.value)
  }

  return null
}

function getSelectedTitle() {
  if (selectedResource.value) {
    return getLocalizedResourceName(selectedResource.value, locale.value)
  }

  if (selectedProduct.value) {
    return getLocalizedProductName(selectedProduct.value, locale.value)
  }

  return ''
}

function getSelectedDescription() {
  if (selectedResource.value) {
    return getLocalizedResourceDescription(selectedResource.value, locale.value)
  }

  if (selectedProduct.value) {
    return getLocalizedProductDescription(selectedProduct.value, locale.value)
  }

  return ''
}

function getProductImage(product: ProductType) {
  return getProductImageUrl(product)
}

function getIngredientImage(recipe: ProductType['recipes'][number]) {
  if (recipe.resourceType) {
    return getResourceImageUrl(recipe.resourceType)
  }

  if (recipe.inputProductType) {
    const inputProductType = recipe.inputProductType
    const product = products.value.find((candidate) => candidate.id === inputProductType.id)
    return getProductImageUrl({
      slug: inputProductType.slug,
      name: inputProductType.name,
      industry: product?.industry ?? 'ELECTRONICS',
    })
  }

  return null
}

function getIngredientTargetSlug(recipe: ProductType['recipes'][number]) {
  return recipe.resourceType?.slug ?? recipe.inputProductType?.slug ?? null
}

function getIngredientQuantityForSelectedEntry(product: ProductType): number {
  if (selectedResource.value) {
    const recipe = product.recipes.find((candidate) => candidate.resourceType?.id === selectedResource.value?.id)
    return recipe?.quantity ?? 0
  }

  if (selectedProduct.value) {
    const recipe = product.recipes.find((candidate) => candidate.inputProductType?.id === selectedProduct.value?.id)
    return recipe?.quantity ?? 0
  }

  return 0
}

function getIngredientUnitForSelectedEntry(product: ProductType): string {
  if (selectedResource.value) {
    return selectedResource.value.unitSymbol
  }

  if (selectedProduct.value) {
    const recipe = product.recipes.find((candidate) => candidate.inputProductType?.id === selectedProduct.value?.id)
    return recipe?.inputProductType?.unitSymbol ?? selectedProduct.value.unitSymbol
  }

  return ''
}

function getProductAccessText(product: ProductType) {
  if (!product.isProOnly) {
    return t('catalog.free')
  }

  return isProductLocked(product) ? t('catalog.proRequired') : t('catalog.proUnlocked')
}

function getProductAccessDetail(product: ProductType) {
  return isProductLocked(product) ? t('catalog.proDetail') : t('catalog.proUnlockedDetail')
}

function getNotFoundHint() {
  return hiddenProduct.value ? t('resourceDetail.hiddenByFilterHint') : t('resourceDetail.notFoundHint')
}

function navigateToEntry(slug: string) {
  router.push({
    name: 'encyclopedia-detail',
    params: { slug },
    query: showProProducts.value ? { showPro: '1' } : {},
  })
}

function navigateToIngredient(recipe: ProductType['recipes'][number]) {
  const slug = getIngredientTargetSlug(recipe)
  if (!slug) {
    return
  }

  navigateToEntry(slug)
}

function goBack() {
  router.push({ name: 'encyclopedia', query: showProProducts.value ? { showPro: '1' } : {} })
}
</script>

<template>
  <div class="resource-detail-view container">
    <nav class="breadcrumb">
      <button type="button" class="back-link" @click="goBack">ÔćÉ {{ t('resourceDetail.backToEncyclopedia') }}</button>
      <label class="filter-toggle">
        <input v-model="showProProducts" type="checkbox" />
        <span>{{ t('encyclopedia.showProProducts') }}</span>
      </label>
    </nav>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="error-message" role="alert">{{ error }}</div>
    <div v-else-if="!selectedResource && !selectedProduct" class="not-found">
      <h2>{{ t('resourceDetail.notFound') }}</h2>
      <p>{{ getNotFoundHint() }}</p>
      <button type="button" class="btn-primary" @click="goBack">
        {{ t('resourceDetail.backToEncyclopedia') }}
      </button>
    </div>

    <template v-else>
      <header class="resource-hero">
        <img v-if="getSelectedImageUrl()" :src="getSelectedImageUrl() ?? undefined" :alt="getSelectedTitle()" class="resource-hero-image" />
        <div class="resource-hero-body">
          <div class="resource-badges">
            <span v-if="selectedResource" class="badge badge--category">{{ getLocalizedCategory(selectedResource.category, locale) }}</span>
            <span v-if="selectedResource" class="badge badge--unit">{{ getLocalizedUnitName(selectedResource.unitName, locale) }} ({{ selectedResource.unitSymbol }})</span>
            <span v-if="selectedProduct" class="badge badge--category">{{ getLocalizedIndustry(selectedProduct.industry, locale) }}</span>
            <span v-if="selectedProduct" class="badge badge--unit">{{ getLocalizedUnitName(selectedProduct.unitName, locale) }} ({{ selectedProduct.unitSymbol }})</span>
            <span v-if="selectedProduct?.isProOnly" class="product-access-badge" :class="{ locked: isProductLocked(selectedProduct), unlocked: !isProductLocked(selectedProduct) }">
              {{ getProductAccessText(selectedProduct) }}
            </span>
          </div>
          <h1>{{ getSelectedTitle() }}</h1>
          <p class="resource-description">{{ getSelectedDescription() }}</p>
          <p v-if="selectedProduct?.isProOnly" class="resource-description">
            {{ getProductAccessDetail(selectedProduct) }}
          </p>
          <div class="resource-meta">
            <div class="meta-item">
              <span class="meta-label">{{ t('resourceDetail.basePrice') }}</span>
              <strong class="meta-value">{{ formatMoney(selectedResource?.basePrice ?? selectedProduct?.basePrice ?? 0, 'EUR', locale) }}</strong>
            </div>
            <div v-if="selectedResource" class="meta-item">
              <span class="meta-label">{{ t('resourceDetail.weight') }}</span>
              <strong class="meta-value">{{ selectedResource.weightPerUnit }} kg/{{ selectedResource.unitSymbol }}</strong>
            </div>
            <div v-if="selectedProduct" class="meta-item">
              <span class="meta-label">{{ t('resourceDetail.craftTicks') }}</span>
              <strong class="meta-value">{{ selectedProduct.baseCraftTicks }}</strong>
            </div>
            <div v-if="selectedProduct" class="meta-item">
              <span class="meta-label">{{ t('encyclopedia.energy') }}</span>
              <strong class="meta-value">{{ selectedProduct.energyConsumptionMwh }} MWh</strong>
            </div>
            <div v-if="selectedProduct" class="meta-item">
              <span class="meta-label">{{ t('encyclopedia.basicLaborHours') }}</span>
              <strong class="meta-value">{{ selectedProduct.basicLaborHours }} h</strong>
            </div>
            <div v-if="selectedProduct" class="meta-item">
              <span class="meta-label">{{ t('resourceDetail.batchOutput') }}</span>
              <strong class="meta-value">{{ selectedProduct.outputQuantity }} {{ selectedProduct.unitSymbol }}</strong>
            </div>
          </div>
          <div v-if="selectedResource" class="hero-cta">
            <RouterLink to="/exchange" class="btn-exchange-link" :aria-label="t('resourceDetail.checkExchangePrices')">
              {{ t('resourceDetail.checkExchangePrices') }}
            </RouterLink>
          </div>
        </div>
      </header>

      <section v-if="selectedProduct" class="composition-panel">
        <div class="section-header">
          <h2>{{ t('encyclopedia.compositionTitle') }}</h2>
          <p>{{ t('encyclopedia.compositionHelp') }}</p>
        </div>

        <div class="composition-flow">
          <div
            v-for="(recipe, index) in selectedProduct.recipes"
            :key="`${selectedProduct.id}-${index}`"
            class="composition-node ingredient clickable"
            role="link"
            tabindex="0"
            :aria-label="t('encyclopedia.viewDetail') + ': ' + getLocalizedRecipeIngredientName(recipe, locale)"
            @click="navigateToIngredient(recipe)"
            @keydown.enter="navigateToIngredient(recipe)"
            @keydown.space.prevent="navigateToIngredient(recipe)"
          >
            <img v-if="getIngredientImage(recipe)" :src="getIngredientImage(recipe) ?? undefined" :alt="getLocalizedRecipeIngredientName(recipe, locale)" class="composition-image" />
            <strong>{{ recipe.quantity }} {{ recipe.resourceType?.unitSymbol ?? recipe.inputProductType?.unitSymbol }}</strong>
            <span>{{ getLocalizedRecipeIngredientName(recipe, locale) }}</span>
          </div>
          <span class="composition-arrow" aria-hidden="true">Ôćĺ</span>
          <div
            class="composition-node output clickable"
            role="link"
            tabindex="0"
            :aria-label="t('encyclopedia.viewDetail') + ': ' + getLocalizedProductName(selectedProduct, locale)"
            @click="navigateToEntry(selectedProduct.slug)"
            @keydown.enter="navigateToEntry(selectedProduct.slug)"
            @keydown.space.prevent="navigateToEntry(selectedProduct.slug)"
          >
            <img :src="getProductImage(selectedProduct)" :alt="getLocalizedProductName(selectedProduct, locale)" class="composition-image" />
            <strong>{{ selectedProduct.outputQuantity }} {{ selectedProduct.unitSymbol }}</strong>
            <span>{{ getLocalizedProductName(selectedProduct, locale) }}</span>
          </div>
        </div>

        <p class="recipe-summary">
          <span class="meta-label">{{ t('resourceDetail.recipeLabel') }}:</span>
          <span>{{ getLocalizedRecipeSummary(selectedProduct, locale) }}</span>
        </p>
      </section>

      <section class="products-section">
        <div class="section-header">
          <h2>{{ t('resourceDetail.usedInProducts') }}</h2>
          <p>{{ t('resourceDetail.usedInProductsHelp') }}</p>
        </div>

        <p v-if="relatedProducts.length === 0" class="empty-state">
          {{ t('resourceDetail.noProductsUsingResource') }}
        </p>

        <div v-else class="product-grid">
          <article
            v-for="product in relatedProducts"
            :key="product.id"
            class="product-card clickable"
            role="link"
            tabindex="0"
            :aria-label="t('encyclopedia.viewDetail') + ': ' + getLocalizedProductName(product, locale)"
            @click="navigateToEntry(product.slug)"
            @keydown.enter="navigateToEntry(product.slug)"
            @keydown.space.prevent="navigateToEntry(product.slug)"
          >
            <img :src="getProductImage(product)" :alt="getLocalizedProductName(product, locale)" class="product-image" />
            <div class="product-body">
              <div class="product-heading">
                <div>
                  <h3>{{ getLocalizedProductName(product, locale) }}</h3>
                  <p class="product-industry">{{ getLocalizedIndustry(product.industry, locale) }}</p>
                </div>
                <span class="product-batch">{{ product.outputQuantity }} {{ product.unitSymbol }}</span>
              </div>

              <p class="product-description">{{ getLocalizedProductDescription(product, locale) }}</p>

              <div class="product-meta">
                <span>{{ t('resourceDetail.craftTicks') }}: {{ product.baseCraftTicks }}</span>
                <span>{{ t('resourceDetail.batchOutput') }}: {{ product.outputQuantity }} {{ getLocalizedUnitName(product.unitName, locale) }}</span>
              </div>

              <div class="ingredient-highlight">
                <span class="ingredient-label">{{ t('resourceDetail.ingredientQuantity') }}:</span>
                <strong>{{ getIngredientQuantityForSelectedEntry(product) }} {{ getIngredientUnitForSelectedEntry(product) }}</strong>
              </div>
            </div>
          </article>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped src="./ResourceDetailView.styles.css"></style>
