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
  marketValuation?: {
    landValue: number
    structureValue: number
    unitsValue: number
    totalValue: number
    minimumSalePrice: number
    currencyCode: string
  } | null
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
interface LoanSummary {
  id: string
  status: string
  missedPayments: number
  remainingPrincipal: number
  collateralBuildingId: string | null
}
interface DestroyBuildingResult {
  buildingId: string
  buildingName: string
  refundAmount: number
  currencyCode: string
}

const building = ref<CompanyBuilding | null>(null)
const cities = ref<City[]>([])
const loans = ref<LoanSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const salePrice = ref<number | null>(null)
const saving = ref(false)
const saveError = ref<string | null>(null)
const saveSuccess = ref(false)
const isListing = ref(true)
const showDestroyConfirm = ref(false)
const destroying = ref(false)
const destroyError = ref<string | null>(null)
const destroyedResult = ref<DestroyBuildingResult | null>(null)
const lastAction = ref<'list' | 'cancel' | 'destroy'>('list')

const DATA_QUERY = `
  {
    myCompanies { id name buildings { id name type level isForSale askingPrice listedAtUtc cityId populationIndex marketValuation { landValue structureValue unitsValue totalValue minimumSalePrice currencyCode } units { id } } }
    cities { id name currencyCode }
    myLoans { id status missedPayments remainingPrincipal collateralBuildingId }
  }
`

const SET_FOR_SALE_MUTATION = `
  mutation SetBuildingForSale($input: SetBuildingForSaleInput!) {
    setBuildingForSale(input: $input) {
      id isForSale askingPrice listedAtUtc
    }
  }
`
const DESTROY_BUILDING_MUTATION = `
  mutation DestroyBuilding($input: DestroyBuildingInput!) {
    destroyBuilding(input: $input) {
      buildingId
      buildingName
      refundAmount
      currencyCode
    }
  }
`

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ myCompanies: Company[]; cities: City[]; myLoans: LoanSummary[] }>(DATA_QUERY)
    cities.value = data.cities ?? []
    loans.value = data.myLoans ?? []
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
  if (salePriceBelowMinimum.value) return
  saving.value = true
  saveError.value = null
  try {
    lastAction.value = 'list'
    await gqlRequest(SET_FOR_SALE_MUTATION, {
      input: { buildingId: building.value.id, isForSale: true, askingPrice: salePrice.value },
    })
    saveSuccess.value = true
    await router.push(`/building/${building.value!.id}`)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : String(err)
    if (msg.includes('BUILDING_IS_COLLATERAL')) {
      saveError.value = t('buildingDetail.collateralBlockedByLoans', { count: 1 })
    } else if (msg.includes('ASKING_PRICE_BELOW_MINIMUM')) {
      saveError.value = t('buildingDetail.minimumSalePriceError', {
        minimum: formatCurrency(minimumSalePrice.value ?? 0, currencyCode.value),
        marketValue: formatCurrency(estimatedMarketValue.value ?? 0, currencyCode.value),
      })
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
  if (cancelSaleLockedByUnpaidLoan.value) return
  saving.value = true
  saveError.value = null
  try {
    lastAction.value = 'cancel'
    await gqlRequest(SET_FOR_SALE_MUTATION, {
      input: { buildingId: building.value.id, isForSale: false, askingPrice: null },
    })
    saveSuccess.value = true
    await router.push(`/building/${building.value!.id}`)
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err)
    if (msg.includes('BUILDING_SALE_LOCKED_BY_UNPAID_COLLATERAL')) {
      saveError.value = t('buildingDetail.cancelSaleLockedTooltip')
    } else {
      saveError.value = t('buildingDetail.saleFailed')
    }
    console.error('[SellBuildingView] Failed to cancel listing:', err)
  } finally {
    saving.value = false
  }
}

async function confirmDestroy() {
  if (!building.value) return
  destroying.value = true
  destroyError.value = null
  try {
    const data = await gqlRequest<{ destroyBuilding: DestroyBuildingResult }>(DESTROY_BUILDING_MUTATION, {
      input: { buildingId: building.value.id },
    })
    lastAction.value = 'destroy'
    destroyedResult.value = data.destroyBuilding
    saveSuccess.value = true
    showDestroyConfirm.value = false
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : String(err)
    if (msg.includes('BUILDING_HAS_UNPAID_COLLATERAL_LOAN')) {
      destroyError.value = t('buildingDetail.destroyBlockedByUnpaidLoan')
    } else {
      destroyError.value = t('buildingDetail.destroyFailed')
    }
  } finally {
    destroying.value = false
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
  if (b.marketValuation?.totalValue != null) return b.marketValuation.totalValue
  return computeEstimatedMarketValue({
    level: b.level,
    unitCount: b.units?.length ?? 0,
    populationIndex: b.populationIndex,
  })
})
const minimumSalePrice = computed(() => {
  if (!estimatedMarketValue.value) return null
  if (building.value?.marketValuation?.minimumSalePrice != null) return building.value.marketValuation.minimumSalePrice
  return Math.round(estimatedMarketValue.value * 0.7 * 100) / 100
})
const salePriceBelowMinimum = computed(() => {
  if (salePrice.value == null || minimumSalePrice.value == null) return false
  return salePrice.value < minimumSalePrice.value
})

const isPriceHigh = computed(() => {
  if (!salePrice.value || !estimatedMarketValue.value) return false
  return isAskingPriceTooHigh(salePrice.value, estimatedMarketValue.value)
})
const destroyRefundAmount = computed(() => {
  if (!estimatedMarketValue.value) return null
  return Math.round(estimatedMarketValue.value * 0.8 * 100) / 100
})
const cancelSaleLockedByUnpaidLoan = computed(() => {
  if (!building.value?.isForSale) return false
  return loans.value.some(
    (loan) =>
      loan.collateralBuildingId === building.value!.id &&
      (loan.status === 'OVERDUE' || loan.status === 'DEFAULTED') &&
      (loan.missedPayments ?? 0) > 0 &&
      (loan.remainingPrincipal ?? 0) > 0,
  )
})

const cityName = computed(() => {
  if (!building.value) return ''
  return cities.value.find((c) => c.id === building.value!.cityId)?.name ?? ''
})

const currencyCode = computed(() => {
  if (!building.value) return 'EUR'
  if (building.value.marketValuation?.currencyCode) return building.value.marketValuation.currencyCode
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
        {{
          lastAction === 'destroy'
            ? t('buildingDetail.destroySuccessWithAmount', {
                amount: formatCurrency(
                  destroyedResult?.refundAmount ?? destroyRefundAmount ?? 0,
                  destroyedResult?.currencyCode ?? currencyCode,
                ),
              })
            : isListing
              ? t('buildingDetail.listingSuccess')
              : t('buildingDetail.cancelListingSuccess')
        }}
      </p>
      <p v-if="lastAction !== 'destroy'" class="mt-1 text-sm text-muted">{{ t('buildingDetail.sellRedirecting') }}</p>
      <div v-else class="mt-4 flex flex-wrap justify-center gap-2">
        <button class="btn btn-secondary" @click="router.push('/dashboard')">
          {{ t('nav.dashboard') }}
        </button>
        <button class="btn btn-primary" @click="router.push('/bank-statement')">
          {{ t('bankStatement.title') }}
        </button>
      </div>
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
        <div v-if="building.marketValuation" class="mt-2 grid gap-1 text-xs text-muted">
          <p>{{ t('buildingDetail.marketValueLand', { amount: formatCurrency(building.marketValuation.landValue, currencyCode) }) }}</p>
          <p>{{ t('buildingDetail.marketValueStructure', { amount: formatCurrency(building.marketValuation.structureValue, currencyCode) }) }}</p>
          <p>{{ t('buildingDetail.marketValueUnits', { amount: formatCurrency(building.marketValuation.unitsValue, currencyCode) }) }}</p>
        </div>
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
          :disabled="saving || cancelSaleLockedByUnpaidLoan"
          :title="cancelSaleLockedByUnpaidLoan ? t('buildingDetail.cancelSaleLockedTooltip') : ''"
          @click="cancelListing"
        >
          <font-awesome-icon v-if="saving" icon="spinner" spin class="mr-2" />
          {{ t('buildingDetail.cancelSale') }}
        </button>
        <p v-if="cancelSaleLockedByUnpaidLoan" class="mt-2 text-xs text-amber-700 dark:text-amber-300">
          {{ t('buildingDetail.cancelSaleLockedTooltip') }}
        </p>
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
          <p v-if="minimumSalePrice !== null" class="mt-1 text-xs text-muted">
            {{ t('buildingDetail.minimumSalePriceHint', { minimum: formatCurrency(minimumSalePrice, currencyCode) }) }}
          </p>
          <!-- Validation messages -->
          <p v-if="salePrice !== null && salePrice <= 0" class="mt-1 text-xs text-red-500">
            {{ t('buildingDetail.askingPriceMustBePositive') }}
          </p>
          <p v-else-if="salePriceBelowMinimum" class="mt-1 text-xs text-red-500">
            {{
              t('buildingDetail.minimumSalePriceError', {
                minimum: formatCurrency(minimumSalePrice ?? 0, currencyCode),
                marketValue: formatCurrency(estimatedMarketValue ?? 0, currencyCode),
              })
            }}
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
            :disabled="saving || !salePrice || salePrice <= 0 || salePriceBelowMinimum"
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

      <div class="mt-5 rounded-xl border border-red-400/40 bg-red-500/5 p-4">
        <p class="text-sm font-semibold text-red-700 dark:text-red-300">
          🗑️ {{ t('buildingDetail.destroyBuilding') }}
        </p>
        <p class="mt-1 text-xs text-muted">{{ t('buildingDetail.destroyHint') }}</p>
        <div class="mt-3 rounded-lg border border-red-300/40 bg-red-500/10 p-3 text-sm">
          <p>{{ t('buildingDetail.destroyPropertyValue', { amount: formatCurrency(estimatedMarketValue ?? 0, currencyCode) }) }}</p>
          <p class="font-semibold text-red-700 dark:text-red-300">
            {{ t('buildingDetail.destroyRefundPreview', { amount: formatCurrency(destroyRefundAmount ?? 0, currencyCode) }) }}
          </p>
          <p class="text-xs text-muted">{{ t('buildingDetail.destroyCurrency', { currency: currencyCode }) }}</p>
        </div>
        <p v-if="destroyError" class="mt-2 text-sm text-red-700 dark:text-red-300">{{ destroyError }}</p>
        <button class="open-destroy-confirm-btn btn btn-danger mt-3 w-full" :disabled="destroying" @click="showDestroyConfirm = true">
          {{ t('buildingDetail.destroyBuilding') }}
        </button>
      </div>
    </template>

    <div v-if="showDestroyConfirm" class="destroy-confirm-dialog fixed inset-0 z-40 flex items-center justify-center bg-black/50 p-4">
      <div class="w-full max-w-md rounded-xl border border-divider bg-card p-5 shadow-xl">
        <h2 class="text-lg font-semibold text-foreground">
          {{ t('buildingDetail.destroyConfirmTitle', { name: building?.name ?? '' }) }}
        </h2>
        <p class="mt-2 text-sm text-muted">
          {{
            t('buildingDetail.destroyConfirmBody', {
              amount: formatCurrency(destroyRefundAmount ?? 0, currencyCode),
            })
          }}
        </p>
        <div class="mt-4 flex justify-end gap-3">
          <button class="btn btn-secondary" :disabled="destroying" @click="showDestroyConfirm = false">
            {{ t('common.cancel') }}
          </button>
          <button class="confirm-destroy-btn btn btn-danger" :disabled="destroying" @click="confirmDestroy">
            <font-awesome-icon v-if="destroying" icon="spinner" spin class="mr-2" />
            {{ t('buildingDetail.destroyConfirmAction') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.sell-building-view {
  padding-left: 1rem;
  padding-right: 1rem;
}
</style>
