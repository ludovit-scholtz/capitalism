<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatCurrency } from '@/lib/loanHelpers'
import { computeEstimatedMarketValue, isAskingPriceTooHigh } from '@/lib/sellBuilding'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const buildingId = computed(() => route.params.id as string)

interface CompanyBuilding {
  id: string
  name: string
  type: string
  level: number
  isForSale: boolean
  askingPrice: number | null
  listedAtUtc: string | null
  cityId: string
  populationIndex?: number | null
  units: Array<{ id: string }>
}
interface Company {
  id: string
  name: string
  buildings: CompanyBuilding[]
}
interface City {
  id: string
  name: string
  currencyCode: string
}

const building = ref<CompanyBuilding | null>(null)
const cities = ref<City[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const salePrice = ref<number | null>(null)
const saving = ref(false)
const saveError = ref<string | null>(null)
const saveSuccess = ref(false)
const isListing = ref(true)

const DATA_QUERY = `
  {
    myCompanies { id name buildings { id name type level isForSale askingPrice listedAtUtc cityId populationIndex units { id } } }
    cities { id name currencyCode }
  }
`

const SET_FOR_SALE_MUTATION = `
  mutation SetBuildingForSale($input: SetBuildingForSaleInput!) {
    setBuildingForSale(input: $input) {
      id isForSale askingPrice listedAtUtc
    }
  }
`

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ myCompanies: Company[]; cities: City[] }>(DATA_QUERY)
    cities.value = data.cities ?? []
    const allBuildings = data.myCompanies.flatMap((c) => c.buildings)
    const found = allBuildings.find((b) => b.id === buildingId.value)
    if (!found) {
      error.value = t('buildingDetail.notFound')
      return
    }
    building.value = found
    salePrice.value = found.askingPrice ?? null
    isListing.value = !found.isForSale
  } catch (err) {
    error.value = t('buildingDetail.loadFailed')
    console.error('[SellBuildingView] Failed to load data:', err)
  } finally {
    loading.value = false
  }
}

async function submitListing() {
  if (!building.value) return
  if (!salePrice.value || salePrice.value <= 0) return
  saving.value = true
  saveError.value = null
  try {
    await gqlRequest(SET_FOR_SALE_MUTATION, {
      input: { buildingId: building.value.id, isForSale: true, askingPrice: salePrice.value },
    })
    saveSuccess.value = true
    await router.push(`/building/${building.value!.id}`)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : String(err)
    if (msg.includes('BUILDING_IS_COLLATERAL')) {
      saveError.value = t('buildingDetail.collateralBlockedByLoans', { count: 1 })
    } else if (msg.includes('INVALID_ASKING_PRICE')) {
      saveError.value = t('buildingDetail.askingPriceMustBePositive')
    } else {
      saveError.value = t('buildingDetail.saleFailed')
    }
  } finally {
    saving.value = false
  }
}

async function cancelListing() {
  if (!building.value) return
  saving.value = true
  saveError.value = null
  try {
    await gqlRequest(SET_FOR_SALE_MUTATION, {
      input: { buildingId: building.value.id, isForSale: false, askingPrice: null },
    })
    saveSuccess.value = true
    await router.push(`/building/${building.value!.id}`)
  } catch (err) {
    saveError.value = t('buildingDetail.saleFailed')
    console.error('[SellBuildingView] Failed to cancel listing:', err)
  } finally {
    saving.value = false
  }
}

function formatBuildingType(type: string) {
  return type.replace(/_/g, ' ')
}

function onPriceInput(event: Event) {
  const inp = event.target as HTMLInputElement
  salePrice.value = isNaN(inp.valueAsNumber) ? null : inp.valueAsNumber
}

const estimatedMarketValue = computed(() => {
  const b = building.value
  if (!b) return null
  return computeEstimatedMarketValue({
    level: b.level,
    unitCount: b.units?.length ?? 0,
    populationIndex: b.populationIndex,
  })
})

const isPriceHigh = computed(() => {
  if (!salePrice.value || !estimatedMarketValue.value) return false
  return isAskingPriceTooHigh(salePrice.value, estimatedMarketValue.value)
})

const cityName = computed(() => {
  if (!building.value) return ''
  return cities.value.find((c) => c.id === building.value!.cityId)?.name ?? ''
})

const currencyCode = computed(() => {
  if (!building.value) return 'EUR'
  return cities.value.find((c) => c.id === building.value!.cityId)?.currencyCode ?? 'EUR'
})

onMounted(loadData)
</script>

<template>
  <div class="sell-building-view mx-auto max-w-lg pt-8 pb-16">
    <!-- Back link -->
    <button
      class="back-link mb-6 flex items-center gap-2 text-sm text-muted hover:text-foreground"
      @click="router.push(building ? `/building/${building.id}` : '/dashboard')"
    >
      <font-awesome-icon icon="arrow-left" />
      {{ t('buildingDetail.backToBuilding') }}
    </button>

    <!-- Loading state -->
    <div v-if="loading" class="flex items-center justify-center py-20">
      <font-awesome-icon icon="spinner" spin class="text-2xl text-primary" />
    </div>

    <!-- Error state -->
    <div
      v-else-if="error"
      class="rounded-xl border border-red-300/60 bg-red-500/10 p-6 text-red-700 dark:text-red-300"
    >
      <p>{{ error }}</p>
      <button class="btn btn-secondary mt-4" @click="loadData">{{ t('common.retry') }}</button>
    </div>

    <!-- Success state -->
    <div
      v-else-if="saveSuccess"
      class="sell-success rounded-xl border border-green-300/60 bg-green-500/10 p-6 text-center"
      role="status"
    >
      <font-awesome-icon icon="circle-check" class="mb-3 text-4xl text-green-600 dark:text-green-400" />
      <p class="font-semibold text-green-700 dark:text-green-300">
        {{ isListing ? t('buildingDetail.listingSuccess') : t('buildingDetail.cancelListingSuccess') }}
      </p>
      <p class="mt-1 text-sm text-muted">{{ t('buildingDetail.sellRedirecting') }}</p>
    </div>

    <!-- Sell form -->
    <template v-else-if="building">
      <h1 class="mb-6 text-2xl font-semibold text-foreground">
        {{ building.isForSale ? t('buildingDetail.editSale') : t('buildingDetail.sellBuilding') }}
      </h1>

      <!-- Building summary card -->
      <div class="building-summary mb-5 rounded-xl border border-divider bg-card p-4">
        <div class="flex items-start justify-between gap-3">
          <div>
            <p class="building-summary-name text-lg font-semibold text-foreground">{{ building.name }}</p>
            <p class="mt-0.5 text-sm text-muted">
              {{ formatBuildingType(building.type) }} &middot; {{ t('common.level') }} {{ building.level }}
            </p>
            <p v-if="cityName" class="mt-0.5 text-sm text-muted">{{ cityName }}</p>
          </div>
          <span
            class="inline-flex items-center rounded-full border px-3 py-1 text-xs font-semibold"
            :class="
              building.isForSale
                ? 'border-green-300/60 bg-green-500/10 text-green-700 dark:text-green-300'
                : 'border-divider bg-surface text-muted'
            "
          >
            {{ building.isForSale ? t('buildingDetail.forSale') : t('buildingDetail.notForSale') }}
          </span>
        </div>
      </div>

      <!-- Estimated market value -->
      <div
        v-if="estimatedMarketValue"
        class="estimated-value-card mb-5 rounded-xl border border-divider bg-surface p-4"
      >
        <p class="text-xs text-muted">{{ t('buildingDetail.estimatedMarketValue') }}</p>
        <p class="estimated-value mt-0.5 text-xl font-bold text-foreground">
          {{ formatCurrency(estimatedMarketValue, currencyCode) }}
        </p>
        <p class="mt-0.5 text-xs text-muted">{{ t('buildingDetail.estimatedValueHint') }}</p>
      </div>

      <!-- Cancel listing section (if already listed) -->
      <div
        v-if="building.isForSale"
        class="mb-5 rounded-xl border border-amber-300/60 bg-amber-500/5 p-4"
      >
        <p class="text-sm font-medium text-amber-700 dark:text-amber-300">
          <font-awesome-icon icon="tag" class="mr-1" />
          {{
            t('buildingDetail.currentlyListed', {
              price: formatCurrency(building.askingPrice ?? 0, currencyCode),
            })
          }}
        </p>
        <p class="mt-1 text-xs text-muted">{{ t('buildingDetail.cancelListingHint') }}</p>
        <button
          class="cancel-listing-btn btn btn-danger mt-3 w-full"
          :disabled="saving"
          @click="cancelListing"
        >
          <font-awesome-icon v-if="saving" icon="spinner" spin class="mr-2" />
          {{ t('buildingDetail.cancelSale') }}
        </button>
      </div>

      <!-- Price form -->
      <div class="sell-form-section rounded-xl border border-divider bg-card p-5">
        <h2 class="mb-4 text-base font-semibold text-foreground">
          {{ building.isForSale ? t('buildingDetail.updateAskingPrice') : t('buildingDetail.setAskingPrice') }}
        </h2>

        <div class="form-field mb-4">
          <label for="asking-price" class="form-label">{{ t('buildingDetail.askingPrice') }}</label>
          <input
            id="asking-price"
            type="number"
            class="form-input"
            :placeholder="t('buildingDetail.askingPricePlaceholder')"
            :value="salePrice"
            min="1"
            step="1000"
            :disabled="saving"
            @input="onPriceInput"
          />
          <!-- Validation messages -->
          <p v-if="salePrice !== null && salePrice <= 0" class="mt-1 text-xs text-red-500">
            {{ t('buildingDetail.askingPriceMustBePositive') }}
          </p>
          <p
            v-else-if="isPriceHigh"
            class="price-high-warning mt-1 rounded border border-amber-300/60 bg-amber-500/10 px-2 py-1.5 text-xs text-amber-700 dark:text-amber-300"
          >
            <font-awesome-icon icon="triangle-exclamation" class="mr-1" />
            {{ t('buildingDetail.askingPriceHighWarning') }}
          </p>
        </div>

        <!-- Save error -->
        <p
          v-if="saveError"
          class="mb-3 rounded border border-red-300/60 bg-red-500/10 px-3 py-2 text-sm text-red-700 dark:text-red-300"
          role="alert"
        >
          {{ saveError }}
        </p>

        <!-- Action buttons -->
        <div class="sell-actions flex gap-3">
          <button
            class="list-for-sale-btn btn btn-primary flex-1"
            :disabled="saving || !salePrice || salePrice <= 0"
            @click="submitListing"
          >
            <font-awesome-icon v-if="saving" icon="spinner" spin class="mr-2" />
            {{ building.isForSale ? t('buildingDetail.updateListing') : t('buildingDetail.listForSale') }}
          </button>
          <button
            class="btn btn-secondary"
            :disabled="saving"
            @click="router.push(`/building/${building.id}`)"
          >
            {{ t('common.cancel') }}
          </button>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.sell-building-view {
  padding-left: 1rem;
  padding-right: 1rem;
}
</style>
