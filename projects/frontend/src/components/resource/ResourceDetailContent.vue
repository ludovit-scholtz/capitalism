<script setup lang="ts">
import { useI18n } from 'vue-i18n'
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
import { onCatalogImageError } from '@/lib/catalogImageFallback'
import { isProductLocked } from '@/lib/productAccess'
import ResourceProductGrid from '@/components/resource/ResourceProductGrid.vue'
import type { ProductType, ResourceType } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  selectedResource: ResourceType | null
  selectedProduct: ProductType | null
  relatedProducts: ProductType[]
  locale: string
  products: ProductType[]
}>()

const emit = defineEmits<{
  (e: 'navigate-to-entry', slug: string): void
}>()

function getSelectedImageUrl() {
  if (props.selectedResource) return getResourceImageUrl(props.selectedResource)
  if (props.selectedProduct) return getProductImageUrl(props.selectedProduct)
  return null
}

function getSelectedTitle() {
  if (props.selectedResource) return getLocalizedResourceName(props.selectedResource, props.locale)
  if (props.selectedProduct) return getLocalizedProductName(props.selectedProduct, props.locale)
  return ''
}

function getSelectedDescription() {
  if (props.selectedResource) return getLocalizedResourceDescription(props.selectedResource, props.locale)
  if (props.selectedProduct) return getLocalizedProductDescription(props.selectedProduct, props.locale)
  return ''
}

function getProductImage(product: ProductType) {
  return getProductImageUrl(product)
}

const DEFAULT_INDUSTRY = 'ELECTRONICS'

function getIngredientImage(recipe: ProductType['recipes'][number]) {
  if (recipe.resourceType) return getResourceImageUrl(recipe.resourceType)
  if (recipe.inputProductType) {
    const inputProductType = recipe.inputProductType
    const product = props.products.find((candidate) => candidate.id === inputProductType.id)
    return getProductImageUrl({ slug: inputProductType.slug, name: inputProductType.name, industry: product?.industry ?? DEFAULT_INDUSTRY })
  }
  return null
}

function getProductAccessText(product: ProductType) {
  if (!product.isProOnly) return t('catalog.free')
  return isProductLocked(product) ? t('catalog.proRequired') : t('catalog.proUnlocked')
}

function getProductAccessDetail(product: ProductType) {
  return isProductLocked(product) ? t('catalog.proDetail') : t('catalog.proUnlockedDetail')
}

function navigateToIngredient(recipe: ProductType['recipes'][number]) {
  const slug = recipe.resourceType?.slug ?? recipe.inputProductType?.slug ?? null
  if (slug) emit('navigate-to-entry', slug)
}
</script>

<template>
  <header class="resource-hero">
    <img v-if="getSelectedImageUrl()" :src="getSelectedImageUrl() ?? undefined" :alt="getSelectedTitle()" class="resource-hero-image" @error="onCatalogImageError" />
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
      <p v-if="selectedProduct?.isProOnly" class="resource-description">{{ getProductAccessDetail(selectedProduct) }}</p>
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
        <RouterLink to="/exchange" class="btn-exchange-link" :aria-label="t('resourceDetail.checkExchangePrices')"> {{ t('resourceDetail.checkExchangePrices') }} </RouterLink>
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
        <img v-if="getIngredientImage(recipe)" :src="getIngredientImage(recipe) ?? undefined" :alt="getLocalizedRecipeIngredientName(recipe, locale)" class="composition-image" @error="onCatalogImageError" />
        <strong>{{ recipe.quantity }} {{ recipe.resourceType?.unitSymbol ?? recipe.inputProductType?.unitSymbol }}</strong>
        <span>{{ getLocalizedRecipeIngredientName(recipe, locale) }}</span>
      </div>
      <span class="composition-arrow" aria-hidden="true">→</span>
      <div
        class="composition-node output clickable"
        role="link"
        tabindex="0"
        :aria-label="t('encyclopedia.viewDetail') + ': ' + getLocalizedProductName(selectedProduct, locale)"
        @click="emit('navigate-to-entry', selectedProduct.slug)"
        @keydown.enter="emit('navigate-to-entry', selectedProduct.slug)"
        @keydown.space.prevent="emit('navigate-to-entry', selectedProduct.slug)"
      >
        <img :src="getProductImage(selectedProduct)" :alt="getLocalizedProductName(selectedProduct, locale)" class="composition-image" @error="onCatalogImageError" />
        <strong>{{ selectedProduct.outputQuantity }} {{ selectedProduct.unitSymbol }}</strong>
        <span>{{ getLocalizedProductName(selectedProduct, locale) }}</span>
      </div>
    </div>

    <p class="recipe-summary">
      <span class="meta-label">{{ t('resourceDetail.recipeLabel') }}:</span>
      <span>{{ getLocalizedRecipeSummary(selectedProduct, locale) }}</span>
    </p>
  </section>

  <ResourceProductGrid
    :related-products="relatedProducts"
    :locale="locale"
    :selected-resource="selectedResource"
    :selected-product="selectedProduct"
    @navigate-to-entry="emit('navigate-to-entry', $event)"
  />
</template>

<style scoped>
.resource-hero {
  display: flex;
  gap: 2rem;
  align-items: flex-start;
  flex-wrap: wrap;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  overflow: hidden;
  padding: 1.5rem;
}

.resource-hero-image {
  width: min(280px, 100%);
  border-radius: 12px;
  aspect-ratio: 1;
  object-fit: cover;
  background: var(--color-bg);
  flex-shrink: 0;
}

.resource-hero-body {
  flex: 1;
  min-width: 240px;
  display: grid;
  gap: 0.75rem;
}

.resource-badges {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.badge,
.product-access-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.25rem 0.6rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 600;
  border: 1px solid var(--color-border);
}

.badge--category {
  background: rgba(0, 71, 255, 0.08);
  color: var(--color-primary);
  border-color: rgba(0, 71, 255, 0.25);
}

.badge--unit {
  background: var(--color-bg);
  color: var(--color-text-secondary);
}

.product-access-badge.locked {
  color: var(--color-tertiary);
  border-color: rgba(255, 109, 0, 0.45);
  background: rgba(255, 109, 0, 0.12);
}

.product-access-badge.unlocked {
  color: var(--color-secondary);
  border-color: rgba(0, 200, 83, 0.45);
  background: rgba(0, 200, 83, 0.12);
}

.resource-hero-body h1 {
  margin: 0;
  font-size: 1.75rem;
}

.resource-description {
  color: var(--color-text-secondary);
  margin: 0;
}

.resource-meta {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
}

.hero-cta {
  margin-top: 1rem;
}

.btn-exchange-link {
  display: inline-block;
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--color-primary);
  text-decoration: none;
  padding: 0.35rem 0.75rem;
  border: 1px solid color-mix(in srgb, var(--color-primary) 40%, transparent);
  border-radius: var(--radius-sm, 4px);
  transition: background 0.15s;
}

.btn-exchange-link:hover {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  text-decoration: none;
}

.meta-item {
  display: grid;
  gap: 0.15rem;
}

.meta-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.meta-value {
  font-size: 1rem;
}

.section-header {
  display: grid;
  gap: 0.25rem;
}

.section-header h2 {
  margin: 0;
}

.section-header p {
  color: var(--color-text-secondary);
  margin: 0;
}

.empty-state {
  color: var(--color-text-secondary);
  padding: 2rem;
  text-align: center;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
}

.composition-panel {
  display: grid;
  gap: 1rem;
}

.composition-flow {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.composition-flow--card {
  align-items: stretch;
}

.composition-node {
  width: 150px;
  padding: 0.75rem;
  display: grid;
  gap: 0.5rem;
  justify-items: center;
  text-align: center;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
}

.composition-node.clickable {
  cursor: pointer;
  transition:
    transform 0.2s ease,
    box-shadow 0.2s ease;
}

.composition-node.clickable:hover,
.composition-node.clickable:focus-visible {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  outline: none;
}

.composition-node.output {
  border-color: var(--color-primary);
}

.composition-image {
  width: 100%;
  border-radius: 12px;
  aspect-ratio: 1;
  object-fit: cover;
  background: var(--color-bg);
}

.composition-arrow {
  font-size: 1.5rem;
  color: var(--color-primary);
  font-weight: 700;
}

.recipe-summary {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
  margin: 0;
}

@media (max-width: 720px) {
  .resource-hero {
    flex-direction: column;
  }

  .resource-hero-image {
    width: 100%;
  }

  .composition-flow {
    flex-direction: column;
    align-items: stretch;
  }

  .composition-node {
    width: 100%;
  }

  .composition-arrow {
    transform: rotate(90deg);
    align-self: center;
  }
}
</style>
