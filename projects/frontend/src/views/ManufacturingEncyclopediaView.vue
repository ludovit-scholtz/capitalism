<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { ProductType, ResourceType } from '@/types'

const { t } = useI18n()

const loading = ref(true)
const error = ref<string | null>(null)
const search = ref('')
const industry = ref('ALL')
const resources = ref<ResourceType[]>([])
const products = ref<ProductType[]>([])

const industries = computed(() => ['ALL', ...new Set(products.value.map((product) => product.industry))])

const filteredProducts = computed(() => {
  const query = search.value.trim().toLowerCase()
  return products.value.filter((product) => {
    const matchesIndustry = industry.value === 'ALL' || product.industry === industry.value
    const matchesSearch = query.length === 0
      || product.name.toLowerCase().includes(query)
      || (product.description ?? '').toLowerCase().includes(query)
      || product.recipes.some((recipe) =>
        (recipe.resourceType?.name ?? '').toLowerCase().includes(query)
        || (recipe.inputProductType?.name ?? '').toLowerCase().includes(query),
      )
    return matchesIndustry && matchesSearch
  })
})

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
          unitName
          unitSymbol
          description
          recipes {
            quantity
            resourceType { id name slug unitName unitSymbol }
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

function formatIndustry(value: string) {
  return value.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase())
}

function formatRecipe(product: ProductType) {
  return product.recipes.map((recipe) => {
    const ingredient = recipe.resourceType?.name ?? recipe.inputProductType?.name ?? ''
    const unitSymbol = recipe.resourceType?.unitSymbol ?? recipe.inputProductType?.unitSymbol ?? ''
    return `${recipe.quantity} ${unitSymbol}`.trim() + ` ${ingredient}`
  }).join(' + ')
}
</script>

<template>
  <div class="encyclopedia-view container">
    <header class="hero">
      <div>
        <p class="eyebrow">{{ t('encyclopedia.eyebrow') }}</p>
        <h1>{{ t('encyclopedia.title') }}</h1>
        <p class="subtitle">{{ t('encyclopedia.subtitle') }}</p>
      </div>
      <div class="hero-stats">
        <div class="stat-card">
          <strong>{{ resources.length }}</strong>
          <span>{{ t('encyclopedia.resourcesCount') }}</span>
        </div>
        <div class="stat-card">
          <strong>{{ products.length }}</strong>
          <span>{{ t('encyclopedia.productsCount') }}</span>
        </div>
      </div>
    </header>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="error-message" role="alert">{{ error }}</div>
    <template v-else>
      <section class="resources-section">
        <div class="section-header">
          <h2>{{ t('encyclopedia.rawMaterials') }}</h2>
          <p>{{ t('encyclopedia.rawMaterialsHelp') }}</p>
        </div>
        <div class="resource-grid">
          <article v-for="resource in resources" :key="resource.id" class="resource-card">
            <img v-if="resource.imageUrl" :src="resource.imageUrl" :alt="resource.name" class="resource-image" />
            <div class="resource-body">
              <div class="resource-heading">
                <h3>{{ resource.name }}</h3>
                <span class="resource-unit">{{ resource.unitSymbol }}</span>
              </div>
                <p class="resource-description">{{ resource.description }}</p>
              <div class="resource-meta">
                <span>{{ t('encyclopedia.basePrice') }}: ${{ resource.basePrice }}</span>
                <span>{{ t('encyclopedia.weight') }}: {{ resource.weightPerUnit }} kg/{{ resource.unitSymbol }}</span>
                <span>{{ resource.category }}</span>
              </div>
            </div>
          </article>
        </div>
      </section>

      <section class="products-section">
        <div class="section-header">
          <h2>{{ t('encyclopedia.manufacturedProducts') }}</h2>
          <p>{{ t('encyclopedia.manufacturedProductsHelp') }}</p>
        </div>

        <div class="filters">
          <input v-model="search" type="search" class="filter-input" :placeholder="t('encyclopedia.searchPlaceholder')" />
          <select v-model="industry" class="filter-select">
            <option v-for="option in industries" :key="option" :value="option">
              {{ option === 'ALL' ? t('encyclopedia.allIndustries') : formatIndustry(option) }}
            </option>
          </select>
        </div>

        <div class="product-grid">
          <article v-for="product in filteredProducts" :key="product.id" class="product-card">
            <div class="product-heading">
              <div>
                <h3>{{ product.name }}</h3>
                <p class="product-industry">{{ formatIndustry(product.industry) }}</p>
              </div>
              <span class="product-batch">{{ product.outputQuantity }} {{ product.unitSymbol }}</span>
            </div>
            <p class="product-description">{{ product.description }}</p>
            <div class="product-meta">
              <span>{{ t('encyclopedia.basePrice') }}: ${{ product.basePrice }}</span>
              <span>{{ t('encyclopedia.craftTime') }}: {{ product.baseCraftTicks }}</span>
              <span>{{ t('encyclopedia.energy') }}: {{ product.energyConsumptionMwh }} MW</span>
            </div>
            <p class="recipe-line">
              <strong>{{ t('encyclopedia.recipe') }}:</strong>
              {{ formatRecipe(product) }}
              <span class="recipe-output">→ {{ product.outputQuantity }} {{ product.unitSymbol }} {{ product.name }}</span>
            </p>
          </article>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.encyclopedia-view {
  padding: 2rem 1rem 3rem;
  display: grid;
  gap: 2rem;
}

.hero,
.section-header,
.filters,
.hero-stats,
.resource-meta,
.product-meta {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
}

.hero {
  justify-content: space-between;
  align-items: flex-end;
}

.eyebrow,
.subtitle,
.section-header p,
.resource-description,
.product-description,
.product-industry,
.recipe-line,
.resource-meta,
.product-meta {
  color: var(--color-text-secondary);
}

.hero h1,
.section-header h2,
.resource-heading h3,
.product-heading h3 {
  margin: 0;
}

.hero-stats {
  justify-content: flex-end;
}

.stat-card,
.resource-card,
.product-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
}

.stat-card {
  padding: 1rem 1.25rem;
  min-width: 120px;
  display: grid;
  gap: 0.25rem;
}

.resource-grid,
.product-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 1rem;
}

.resource-card {
  overflow: hidden;
}

.resource-image {
  width: 100%;
  aspect-ratio: 16 / 9;
  object-fit: cover;
  background: var(--color-bg);
}

.resource-body,
.product-card {
  padding: 1rem;
}

.resource-heading,
.product-heading {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
}

.resource-unit,
.product-batch {
  padding: 0.25rem 0.6rem;
  border-radius: 999px;
  background: rgba(0, 71, 255, 0.08);
  color: var(--color-primary);
  font-weight: 600;
  font-size: 0.75rem;
}

.filters {
  align-items: center;
}

.filter-input,
.filter-select {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-bg);
  color: var(--color-text);
  padding: 0.75rem 0.9rem;
}

.filter-input {
  flex: 1;
  min-width: 240px;
}

.recipe-output {
  display: block;
  margin-top: 0.35rem;
  color: var(--color-text);
}

@media (max-width: 720px) {
  .hero {
    align-items: stretch;
  }

  .hero-stats {
    justify-content: stretch;
  }

  .stat-card {
    flex: 1;
  }
}
</style>
