<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import type { ProductType, ResourceType } from '@/types'
import ResourceDetailContent from '@/components/resource/ResourceDetailContent.vue'

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

function goBack() {
  router.push({ name: 'encyclopedia', query: showProProducts.value ? { showPro: '1' } : {} })
}
</script>

<template>
  <div class="resource-detail-view container">
    <nav class="breadcrumb">
      <button type="button" class="back-link" @click="goBack">← {{ t('resourceDetail.backToEncyclopedia') }}</button>
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
      <button type="button" class="btn-primary" @click="goBack">{{ t('resourceDetail.backToEncyclopedia') }}</button>
    </div>

    <template v-else>
      <ResourceDetailContent
        :selected-resource="selectedResource"
        :selected-product="selectedProduct"
        :related-products="relatedProducts"
        :locale="locale"
        :products="products"
        @navigate-to-entry="navigateToEntry"
      />
    </template>
  </div>
</template>

<style scoped>
.resource-detail-view {
  padding: 2rem 1rem 3rem;
  display: grid;
  gap: 2rem;
}

.breadcrumb {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.back-link {
  background: none;
  border: none;
  color: var(--color-primary);
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  padding: 0.4rem 0;
  text-decoration: none;
}

.back-link:hover {
  text-decoration: underline;
}

.filter-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--color-text-secondary);
  font-weight: 600;
}

.filter-toggle input {
  accent-color: var(--color-primary);
}

.not-found {
  text-align: center;
  padding: 3rem 1rem;
  display: grid;
  gap: 1rem;
  justify-items: center;
}

.not-found h2 {
  margin: 0;
}

.btn-primary {
  padding: 0.75rem 1.5rem;
  background: var(--color-primary);
  color: #fff;
  border: none;
  border-radius: 10px;
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
}

.btn-primary:hover {
  opacity: 0.9;
}
</style>
