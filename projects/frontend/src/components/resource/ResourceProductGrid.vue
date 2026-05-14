<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import {
  getLocalizedIndustry,
  getLocalizedProductDescription,
  getLocalizedProductName,
  getLocalizedUnitName,
  getProductImageUrl,
} from '@/lib/catalogPresentation'
import { onCatalogImageError } from '@/lib/catalogImageFallback'
import type { ProductType, ResourceType } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  relatedProducts: ProductType[]
  locale: string
  selectedResource: ResourceType | null
  selectedProduct: ProductType | null
}>()

const emit = defineEmits<{
  (e: 'navigate-to-entry', slug: string): void
}>()

function getProductImage(product: ProductType) {
  return getProductImageUrl(product)
}

function getIngredientQuantityForSelectedEntry(product: ProductType): number {
  if (props.selectedResource) {
    const recipe = product.recipes.find((r) => r.resourceType?.id === props.selectedResource?.id)
    return recipe?.quantity ?? 0
  }
  if (props.selectedProduct) {
    const recipe = product.recipes.find((r) => r.inputProductType?.id === props.selectedProduct?.id)
    return recipe?.quantity ?? 0
  }
  return 0
}

function getIngredientUnitForSelectedEntry(): string {
  if (props.selectedResource) return props.selectedResource.unitSymbol
  if (props.selectedProduct) return props.selectedProduct.unitSymbol
  return ''
}
</script>

<template>
  <section class="products-section">
    <div class="section-header">
      <h2>{{ t('resourceDetail.usedInProducts') }}</h2>
      <p>{{ t('resourceDetail.usedInProductsHelp') }}</p>
    </div>

    <p v-if="relatedProducts.length === 0" class="empty-state">{{ t('resourceDetail.noProductsUsingResource') }}</p>

    <div v-else class="product-grid">
      <article
        v-for="product in relatedProducts"
        :key="product.id"
        class="product-card clickable"
        role="link"
        tabindex="0"
        :aria-label="t('encyclopedia.viewDetail') + ': ' + getLocalizedProductName(product, locale)"
        @click="emit('navigate-to-entry', product.slug)"
        @keydown.enter="emit('navigate-to-entry', product.slug)"
        @keydown.space.prevent="emit('navigate-to-entry', product.slug)"
      >
        <img :src="getProductImage(product)" :alt="getLocalizedProductName(product, locale)" class="product-image" @error="onCatalogImageError" />
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
            <strong>{{ getIngredientQuantityForSelectedEntry(product) }} {{ getIngredientUnitForSelectedEntry() }}</strong>
          </div>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
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

.product-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 1rem;
}

.product-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  overflow: hidden;
  display: grid;
}

.product-card.clickable {
  cursor: pointer;
  transition: all 0.2s ease;
}

.product-card.clickable:hover {
  border-color: var(--color-primary);
  box-shadow: 0 4px 12px rgba(0, 71, 255, 0.15);
  transform: translateY(-2px);
}

.product-card.clickable:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: 2px;
}

.product-image {
  width: 100%;
  aspect-ratio: 16 / 9;
  object-fit: cover;
  background: var(--color-bg);
}

.product-body {
  padding: 1rem;
  display: grid;
  gap: 0.75rem;
}

.product-heading {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
}

.product-heading h3 {
  margin: 0;
}

.product-industry {
  color: var(--color-text-secondary);
  font-size: 0.85rem;
  margin: 0;
}

.product-description {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
  margin: 0;
}

.product-batch {
  padding: 0.25rem 0.6rem;
  border-radius: 999px;
  background: rgba(0, 71, 255, 0.08);
  color: var(--color-primary);
  font-weight: 600;
  font-size: 0.75rem;
  white-space: nowrap;
}

.product-meta {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.ingredient-highlight {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  padding: 0.5rem 0.75rem;
  background: rgba(0, 71, 255, 0.06);
  border: 1px solid rgba(0, 71, 255, 0.2);
  border-radius: 8px;
  font-size: 0.875rem;
}

.ingredient-label {
  color: var(--color-text-secondary);
}

.products-section {
  display: grid;
  gap: 1rem;
}

@media (max-width: 720px) {
  .product-grid {
    grid-template-columns: 1fr;
  }
}
</style>
