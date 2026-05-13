<script setup lang="ts">
import { computed, inject, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  powerPlantAnalytics,
  powerPlantAnalyticsLoading,
  cityPowerBalance,
  cityPowerBalanceLoading,
  loadCityPowerBalance,
  setPlantDispatch,
  dispatchSaving,
  dispatchError,
  dispatchSuccess,
  listEnergyForSale,
  energyListingSaving,
  energyListingError,
  energyListingSuccess,
  cancelEnergyListing,
  energyCancelSaving,
  energyCancelError,
  formatCurrency,
} = bd

// Grid economics constants — must be kept manually synchronized with GameConstants.cs.
// Backend: GameConstants.GridSurplusIncomePerMwTick = 5, GameConstants.GridFinePerMwTick = 8.
// These are used only for frontend projection estimates; the ledger is the source of truth.
const SURPLUS_RATE_PER_MW_TICK = 5
const FINE_RATE_PER_MW_TICK = 8

// Per-tick projected economics based on current city balance.
// Returns null when projection is unavailable, 'no-supply' when city has no power supply yet.
type ProjectedResult =
  | { kind: 'surplus' | 'fine' | 'balanced'; amount: number; sharePercent: number }
  | { kind: 'no-supply' }
  | null

const projected = computed<ProjectedResult>(() => {
  const balance = cityPowerBalance.value
  const plantOutput = building.value?.powerOutput ?? 0
  if (!balance) return null
  if (plantOutput <= 0) return null
  if (balance.totalSupplyMw <= 0) return { kind: 'no-supply' }

  const capacityShare = plantOutput / balance.totalSupplyMw
  const sharePercent = Math.round(capacityShare * 100)

  if (balance.reserveMw > 0) {
    const surplusIncome = balance.reserveMw * SURPLUS_RATE_PER_MW_TICK * capacityShare
    return { kind: 'surplus', amount: surplusIncome, sharePercent }
  } else if (balance.reserveMw < 0) {
    const fine = Math.abs(balance.reserveMw) * FINE_RATE_PER_MW_TICK * capacityShare
    return { kind: 'fine', amount: fine, sharePercent }
  }
  return { kind: 'balanced', amount: 0, sharePercent }
})

// Whether this is a thermal (COAL/GAS) plant — fuel reserve logic only applies to these.
const isThermalPlant = computed(() => {
  const pt = building.value?.powerPlantType
  return pt === 'COAL' || pt === 'GAS'
})

// Dispatch control slider (local draft, synced from building on mount/change).
const draftDispatch = ref(100)
watch(
  () => building.value?.dispatchTargetPercent,
  (v) => {
    if (v != null) draftDispatch.value = v
  },
  { immediate: true },
)

async function applyDispatch() {
  if (!building.value) return
  await setPlantDispatch(building.value.id, draftDispatch.value)
}

function loadBalanceIfNeeded() {
  if (building.value?.type === 'POWER_PLANT' && building.value?.cityId) {
    loadCityPowerBalance(building.value.cityId)
  }
}

onMounted(loadBalanceIfNeeded)
watch(() => building.value?.cityId, loadBalanceIfNeeded)

// Pre-compute the chart scale for the P&L bar chart (avoids O(n²) re-computation in template).
const chartMaxValue = computed(() => {
  const tl = powerPlantAnalytics.value?.timeline
  if (!tl || tl.length === 0) return 1
  return Math.max(...tl.map((s) => Math.max(s.surplusIncome ?? 0, (s.gridFine ?? 0) + (s.operatingCosts ?? 0) + (s.fuelCosts ?? 0)))) || 1
})

// ── Energy spot-market listing ────────────────────────────────────────────
const showCreateListingForm = ref(false)
const draftListingPrice = ref<string>('')
const draftListingCapacity = ref<string>('')

async function submitListing() {
  if (!building.value) return
  const price = parseFloat(draftListingPrice.value)
  const capacity = parseFloat(draftListingCapacity.value)
  if (isNaN(price) || isNaN(capacity) || price <= 0 || capacity <= 0) return
  await listEnergyForSale(building.value.id, price, capacity)
  if (energyListingSuccess.value) {
    draftListingPrice.value = ''
    draftListingCapacity.value = ''
    // Keep form open to show success message; will collapse when analytics reload returns activeListing
  }
}

async function doCancel(listingId: string) {
  await cancelEnergyListing(listingId)
}
</script>

<template>
  <div
    v-if="building?.type === 'POWER_PLANT'"
    class="power-plant-analytics-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5"
    role="region"
    :aria-label="t('buildingDetail.accessibility.powerPlantAnalytics')"
  >
    <div class="power-plant-analytics-header mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 class="power-plant-analytics-title text-lg font-semibold text-foreground">{{ t('powerPlant.analytics.panelTitle') }}</h2>
      <span class="meta-pill inline-flex items-center gap-1.5 rounded-full border border-divider bg-surface px-3 py-1.5 text-xs font-medium text-muted">
        {{ building?.powerOutput != null ? building?.powerOutput : '' }} MW · {{ building?.powerPlantType ?? '—' }}
      </span>
    </div>

    <!-- City power status block -->
    <div
      v-if="cityPowerBalanceLoading"
      class="ppa-city-status mb-3 rounded-lg border border-divider bg-surface px-3 py-2 text-sm text-muted"
    >{{ t('common.loading') }}</div>
    <div
      v-else-if="cityPowerBalance"
      class="ppa-city-status mb-3 rounded-lg border border-divider bg-surface px-3 py-2"
      :class="{
        'border-green-600/40': cityPowerBalance.status === 'BALANCED',
        'border-yellow-500/40': cityPowerBalance.status === 'CONSTRAINED',
        'border-red-600/40': cityPowerBalance.status === 'CRITICAL',
      }"
    >
      <div class="flex flex-wrap items-center justify-between gap-2">
        <span class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('powerPlant.cityPowerStatus.title') }}</span>
        <span
          class="rounded-full px-2 py-0.5 text-xs font-bold"
          :class="{
            'bg-green-600/20 text-green-400': cityPowerBalance.status === 'BALANCED',
            'bg-yellow-500/20 text-yellow-400': cityPowerBalance.status === 'CONSTRAINED',
            'bg-red-600/20 text-red-400': cityPowerBalance.status === 'CRITICAL',
          }"
        >
          {{
            cityPowerBalance.status === 'BALANCED'
              ? t('powerPlant.cityPowerStatus.statusPowered')
              : cityPowerBalance.status === 'CONSTRAINED'
                ? t('powerPlant.cityPowerStatus.statusConstrained')
                : t('powerPlant.cityPowerStatus.statusOffline')
          }}
        </span>
      </div>
      <div class="mt-2 flex flex-wrap gap-4 text-sm">
        <span>⚡ {{ t('powerPlant.cityPowerStatus.totalSupply') }}: <strong>{{ cityPowerBalance.totalSupplyMw.toFixed(1) }} MW</strong></span>
        <span>🏭 {{ t('powerPlant.cityPowerStatus.totalDemand') }}: <strong>{{ cityPowerBalance.totalDemandMw.toFixed(1) }} MW</strong></span>
        <span>
          {{ t('powerPlant.cityPowerStatus.balance') }}:
          <strong
            :class="{
              'text-green-400': cityPowerBalance.reserveMw >= 0,
              'text-red-400': cityPowerBalance.reserveMw < 0,
            }"
          >{{ cityPowerBalance.reserveMw >= 0 ? '+' : '' }}{{ cityPowerBalance.reserveMw.toFixed(1) }} MW</strong>
        </span>
      </div>
      <p class="ppa-city-hint mt-1.5 text-xs text-muted">
        {{
          cityPowerBalance.reserveMw > 0
            ? t('powerPlant.cityPowerStatus.surplusHint')
            : cityPowerBalance.reserveMw < 0
              ? t('powerPlant.cityPowerStatus.shortageHint')
              : t('powerPlant.cityPowerStatus.balancedHint')
        }}
      </p>

      <!-- Projected per-tick economics based on current balance -->
      <div
        v-if="projected && projected.kind !== 'no-supply'"
        class="ppa-projected mt-3 border-t border-divider pt-3"
      >
        <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">{{ t('powerPlant.cityPowerStatus.projectedTitle') }}</p>
        <div v-if="projected.kind === 'surplus'" class="flex flex-wrap items-center gap-3">
          <div class="rounded-lg bg-green-600/10 px-3 py-1.5 text-sm">
            <span class="text-muted">{{ t('powerPlant.cityPowerStatus.projectedSurplus') }}: </span>
            <strong class="text-green-400">+{{ formatCurrency(projected.amount) }}/tick</strong>
          </div>
          <p class="text-xs text-muted">
            {{ t('powerPlant.cityPowerStatus.projectedNote', { share: projected.sharePercent }) }}
          </p>
        </div>
        <div v-else-if="projected.kind === 'fine'" class="flex flex-wrap items-center gap-3">
          <div class="rounded-lg bg-red-600/10 px-3 py-1.5 text-sm">
            <span class="text-muted">{{ t('powerPlant.cityPowerStatus.projectedFine') }}: </span>
            <strong class="text-red-400">−{{ formatCurrency(projected.amount) }}/tick</strong>
          </div>
          <p class="text-xs text-muted">
            {{ t('powerPlant.cityPowerStatus.projectedNote', { share: projected.sharePercent }) }}
          </p>
        </div>
        <p v-else class="text-xs text-muted">{{ t('powerPlant.cityPowerStatus.balancedHint') }}</p>
      </div>
      <!-- City has supply data but no plants yet — different message from zero-output plant -->
      <p v-else-if="projected?.kind === 'no-supply'" class="mt-2 text-xs text-muted">
        {{ t('powerPlant.cityPowerStatus.projectedNoSupply') }}
      </p>
      <p v-else-if="cityPowerBalance && (building?.powerOutput ?? 0) <= 0" class="mt-2 text-xs text-muted">
        {{ t('powerPlant.cityPowerStatus.projectedNoData') }}
      </p>
    </div>

    <div v-if="powerPlantAnalyticsLoading" class="ppa-loading py-2 text-sm text-muted">{{ t('common.loading') }}</div>

    <template v-else-if="powerPlantAnalytics">
      <p class="ppa-tick-window config-help mb-3 text-sm text-muted">
        {{ t('powerPlant.analytics.tickWindow', { start: powerPlantAnalytics.dataFromTick, end: powerPlantAnalytics.dataToTick }) }}
      </p>

      <!-- Dispatch control -->
      <div class="dispatch-control mb-4 rounded-lg border border-divider bg-surface p-3">
        <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
          <span class="text-xs font-semibold uppercase tracking-wide text-muted">{{ t('powerPlant.dispatch.title') }}</span>
          <span
            class="rounded-full px-2 py-0.5 text-xs font-bold"
            :class="{
              'bg-green-600/20 text-green-400': draftDispatch >= 80,
              'bg-yellow-500/20 text-yellow-400': draftDispatch >= 40 && draftDispatch < 80,
              'bg-red-600/20 text-red-400': draftDispatch < 40,
            }"
          >{{ draftDispatch }}%</span>
        </div>
        <input
          v-model.number="draftDispatch"
          type="range"
          min="0"
          max="100"
          step="5"
          class="dispatch-slider w-full accent-[var(--color-accent)]"
          :aria-label="t('powerPlant.dispatch.sliderLabel')"
        />
        <div class="mt-2 flex flex-wrap items-center gap-3">
          <p class="flex-1 text-xs text-muted">{{ t('powerPlant.dispatch.hint') }}</p>
          <button
            class="rounded-lg bg-accent px-3 py-1.5 text-xs font-semibold text-white hover:opacity-90 disabled:opacity-50"
            :disabled="dispatchSaving || draftDispatch === (building?.dispatchTargetPercent ?? 100)"
            @click="applyDispatch"
          >
            {{ dispatchSaving ? t('common.saving') : t('powerPlant.dispatch.applyBtn') }}
          </button>
        </div>
        <p v-if="dispatchError" class="mt-1 text-xs text-red-400">{{ dispatchError }}</p>
        <p v-if="dispatchSuccess" class="dispatch-success mt-1 text-xs text-green-400">{{ t('powerPlant.dispatch.success') }}</p>
      </div>

      <!-- Energy spot-market listing ──────────────────────────────────────── -->
      <div class="energy-listing-section mb-4 rounded-lg border border-divider bg-surface p-3">
        <span class="mb-2 block text-xs font-semibold uppercase tracking-wide text-muted">💱 {{ t('energyPanel.spotMarketSection') }}</span>

        <!-- Active listing -->
        <template v-if="powerPlantAnalytics.activeListing">
          <div class="active-listing flex flex-wrap items-center justify-between gap-2 rounded-md border border-green-300/40 bg-green-500/10 px-3 py-2">
            <div class="text-sm">
              <span class="font-semibold text-green-700 dark:text-green-300">{{ t('energyPanel.activeListing') }}</span>
              <span class="ml-2 text-foreground">
                {{ t('energyPanel.listingPrice', { price: formatCurrency(powerPlantAnalytics.activeListing.pricePerKwhLocal) }) }}
              </span>
              <span class="ml-2 text-muted">
                {{ t('energyPanel.listingCapacity', { capacity: powerPlantAnalytics.activeListing.capacityKw }) }}
              </span>
              <span class="ml-2 text-muted">
                {{ t('energyPanel.listingAvailable', { available: powerPlantAnalytics.activeListing.availableKw }) }}
              </span>
            </div>
            <button
              class="btn btn-ghost btn-sm cancel-listing-btn text-red-500"
              :disabled="energyCancelSaving"
              @click="doCancel(powerPlantAnalytics.activeListing.listingId)"
            >
              {{ energyCancelSaving ? t('energyPanel.cancellingListing') : t('energyPanel.cancelListing') }}
            </button>
          </div>
          <p v-if="energyCancelError" class="cancel-error mt-1 text-xs text-red-400">{{ energyCancelError }}</p>
        </template>

        <!-- No active listing — show create form toggle or form -->
        <template v-else>
          <button
            v-if="!showCreateListingForm"
            class="create-listing-btn btn btn-secondary btn-sm"
            @click="showCreateListingForm = true"
          >
            {{ t('energyPanel.createListing') }}
          </button>
          <div v-else class="create-listing-form mt-2 space-y-3">
            <h4 class="text-xs font-semibold text-foreground">{{ t('energyPanel.createListingTitle') }}</h4>
            <div>
              <label class="mb-1 block text-xs text-muted" for="listing-price-input">{{ t('energyPanel.priceLabel') }}</label>
              <input
                id="listing-price-input"
                v-model="draftListingPrice"
                type="number"
                min="0.001"
                step="0.001"
                class="listing-price-input w-36 rounded-md border border-divider bg-card px-2 py-1.5 text-sm text-foreground"
              />
            </div>
            <div>
              <label class="mb-1 block text-xs text-muted" for="listing-capacity-input">{{ t('energyPanel.capacityLabel') }}</label>
              <input
                id="listing-capacity-input"
                v-model="draftListingCapacity"
                type="number"
                min="1"
                step="1"
                class="listing-capacity-input w-36 rounded-md border border-divider bg-card px-2 py-1.5 text-sm text-foreground"
              />
            </div>
            <div class="flex gap-2">
              <button
                class="submit-listing-btn btn btn-primary btn-sm"
                :disabled="energyListingSaving"
                @click="submitListing"
              >
                {{ energyListingSaving ? t('energyPanel.creatingListing') : t('energyPanel.submitListing') }}
              </button>
              <button class="btn btn-ghost btn-sm text-muted" @click="showCreateListingForm = false">{{ t('common.cancel') }}</button>
            </div>
            <p v-if="energyListingError" class="listing-error text-xs text-red-400">{{ energyListingError }}</p>
            <p v-if="energyListingSuccess" class="listing-success text-xs text-green-400">{{ t('energyPanel.listingCreated') }}</p>
          </div>
        </template>
      </div>

      <!-- Fuel reserve (thermal plants only) — richer capacity bar, chain visualization, multi-fuel badge -->
      <div
        v-if="isThermalPlant"
        class="fuel-reserve mb-4 rounded-lg border border-divider bg-surface p-3"
      >
        <!-- Header row: title + fuel type badge -->
        <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
          <span class="text-xs font-semibold uppercase tracking-wide text-muted">⛽ {{ t('powerPlant.fuelReserve.title') }}</span>
          <div class="flex items-center gap-2">
            <!-- Multi-fuel economics badge -->
            <span
              class="fuel-type-badge inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold"
              :class="powerPlantAnalytics.plantType === 'GAS' ? 'bg-blue-600/20 text-blue-300' : 'bg-orange-600/20 text-orange-300'"
              :title="powerPlantAnalytics.plantType === 'GAS'
                ? t('powerPlant.fuelReserve.gasPremiumNote')
                : t('powerPlant.fuelReserve.fuelCostHint', { cost: (powerPlantAnalytics.fuelCostPerMwhEur ?? 0).toFixed(2) })"
            >
              {{ powerPlantAnalytics.plantType === 'GAS' ? '🔵' : '🟠' }}
              {{ t('powerPlant.fuelReserve.fuelTypeBadge', { type: powerPlantAnalytics.fuelTypeLabel || powerPlantAnalytics.plantType }) }}
            </span>
            <span class="fuel-reserve-mwh text-sm font-bold text-foreground">
              {{ (powerPlantAnalytics.fuelReserveMwh ?? 0).toFixed(1) }}
              <span class="text-xs font-normal text-muted">/ {{ (powerPlantAnalytics.maxFuelReserveMwh ?? 0).toFixed(0) }} MWh</span>
            </span>
          </div>
        </div>

        <!-- Reserve capacity bar -->
        <div v-if="(powerPlantAnalytics.maxFuelReserveMwh ?? 0) > 0" class="fuel-reserve-bar-wrapper mb-2">
          <div class="mb-1 flex items-center justify-between text-xs text-muted">
            <span>{{ t('powerPlant.fuelReserve.capacityLabel') }}</span>
            <span class="font-semibold" :class="(powerPlantAnalytics.fuelReservePercent ?? 0) < 20 ? 'text-red-400' : (powerPlantAnalytics.fuelReservePercent ?? 0) < 50 ? 'text-yellow-400' : 'text-green-400'">
              {{ t('powerPlant.fuelReserve.fillPercent', { percent: powerPlantAnalytics.fuelReservePercent ?? 0 }) }}
            </span>
          </div>
          <div class="fuel-reserve-bar h-3 w-full overflow-hidden rounded-full bg-black/20">
            <div
              class="fuel-reserve-bar-fill h-full rounded-full transition-all duration-300"
              :class="{
                'bg-red-500': (powerPlantAnalytics.fuelReservePercent ?? 0) < 20,
                'bg-yellow-500': (powerPlantAnalytics.fuelReservePercent ?? 0) >= 20 && (powerPlantAnalytics.fuelReservePercent ?? 0) < 50,
                'bg-green-500': (powerPlantAnalytics.fuelReservePercent ?? 0) >= 50,
              }"
              :style="{ width: `${Math.max(powerPlantAnalytics.fuelReservePercent ?? 0, 1)}%` }"
            />
          </div>
          <div v-if="(powerPlantAnalytics.fuelPurchaseCapacityMwhPerTick ?? 0) > 0" class="mt-1 text-xs text-muted">
            {{ t('powerPlant.fuelReserve.procurementRate', { mwh: (powerPlantAnalytics.fuelPurchaseCapacityMwhPerTick ?? 0).toFixed(1) }) }}
          </div>
        </div>
        <!-- No FUEL_PURCHASE units installed -->
        <p v-else class="no-fp-units mb-2 text-xs text-yellow-400">{{ t('powerPlant.fuelReserve.noFuelPurchaseUnits') }}</p>

        <!-- Constrained output warning -->
        <div
          v-if="(powerPlantAnalytics.fuelConstrainedOutputMw ?? 0) > 0"
          class="fuel-constrained-warning mt-2 flex items-start gap-2 rounded-lg border border-red-500/30 bg-red-600/10 px-3 py-2 text-xs text-red-300"
        >
          <span class="mt-0.5 text-base">⚠️</span>
          <span>{{ t('powerPlant.fuelReserve.constrainedWarning', { mw: (powerPlantAnalytics.fuelConstrainedOutputMw ?? 0).toFixed(1) }) }}</span>
        </div>

        <!-- No ENERGY_PRODUCING units installed (guidance) -->
        <p v-else-if="(powerPlantAnalytics.energyProducingCapacityMw ?? 0) === 0" class="no-ep-units mt-2 text-xs text-muted">
          {{ t('powerPlant.fuelReserve.noEnergyProducingUnits') }}
        </p>

        <!-- Grid link chain: FUEL_PURCHASE → ENERGY_PRODUCING → GRID -->
        <div
          v-if="(powerPlantAnalytics.maxFuelReserveMwh ?? 0) > 0 || (powerPlantAnalytics.energyProducingCapacityMw ?? 0) > 0"
          class="grid-link-chain mt-3 border-t border-divider pt-3"
        >
          <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">{{ t('powerPlant.fuelReserve.gridLinkTitle') }}</p>
          <div class="flex flex-wrap items-center gap-1 text-xs">
            <!-- FP node -->
            <div
              class="grid-link-node fuel-purchase-node rounded-lg border border-divider bg-card px-2.5 py-1.5"
              :class="(powerPlantAnalytics.maxFuelReserveMwh ?? 0) > 0 ? 'border-orange-500/40' : 'opacity-40'"
            >
              <span class="font-semibold">⛽ {{ t('powerPlant.fuelReserve.gridLinkFuelPurchase') }}</span>
              <br />
              <span class="text-muted">{{ t('powerPlant.fuelReserve.gridLinkFuelPurchaseHint', { mwh: (powerPlantAnalytics.fuelPurchaseCapacityMwhPerTick ?? 0).toFixed(1), max: (powerPlantAnalytics.maxFuelReserveMwh ?? 0).toFixed(0) }) }}</span>
            </div>
            <!-- Arrow -->
            <span class="chain-arrow text-muted">→</span>
            <!-- EP node -->
            <div
              class="grid-link-node energy-producing-node rounded-lg border border-divider bg-card px-2.5 py-1.5"
              :class="(powerPlantAnalytics.energyProducingCapacityMw ?? 0) > 0 ? 'border-yellow-500/40' : 'opacity-40'"
            >
              <span class="font-semibold">🔥 {{ t('powerPlant.fuelReserve.gridLinkEnergyProducing') }}</span>
              <br />
              <span class="text-muted">{{ t('powerPlant.fuelReserve.gridLinkEnergyProducingHint', { mw: (powerPlantAnalytics.energyProducingCapacityMw ?? 0).toFixed(1) }) }}</span>
            </div>
            <!-- Arrow -->
            <span class="chain-arrow text-muted">→</span>
            <!-- Grid node -->
            <div class="grid-link-node city-grid-node rounded-lg border border-green-500/40 bg-card px-2.5 py-1.5">
              <span class="font-semibold text-green-400">⚡ {{ t('powerPlant.fuelReserve.gridLinkGrid') }}</span>
            </div>
          </div>
          <!-- GAS premium notice -->
          <p v-if="powerPlantAnalytics.plantType === 'GAS'" class="gas-premium-note mt-2 text-xs text-blue-300">
            {{ t('powerPlant.fuelReserve.gasPremiumNote') }}
          </p>
        </div>

        <!-- Legacy hint text (when no units yet) -->
        <p v-else class="mt-2 text-xs text-muted">{{ t('powerPlant.fuelReserve.hint') }}</p>
      </div>

      <!-- P&L summary grid (5 metrics incl. fuel costs for thermal) -->
      <div
        class="ppa-summary-grid grid gap-3"
        :class="isThermalPlant ? 'grid-cols-2 sm:grid-cols-3 lg:grid-cols-5' : 'grid-cols-2 sm:grid-cols-4'"
      >
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label" :title="t('powerPlant.analytics.surplusHint', { rate: SURPLUS_RATE_PER_MW_TICK })">
            {{ t('powerPlant.analytics.surplusIncome') }}
          </span>
          <strong class="ppa-metric-value ppa-income mt-1 block text-base">{{ formatCurrency(powerPlantAnalytics.totalSurplusIncome) }}</strong>
        </div>
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label" :title="t('powerPlant.analytics.fineHint', { rate: FINE_RATE_PER_MW_TICK })">
            {{ t('powerPlant.analytics.gridFine') }}
          </span>
          <strong class="ppa-metric-value ppa-fine mt-1 block text-base">{{ formatCurrency(powerPlantAnalytics.totalGridFines) }}</strong>
        </div>
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label">{{ t('powerPlant.analytics.operatingCosts') }}</span>
          <strong class="ppa-metric-value ppa-cost mt-1 block text-base">{{ formatCurrency(powerPlantAnalytics.totalOperatingCosts) }}</strong>
        </div>
        <div v-if="isThermalPlant" class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label">{{ t('powerPlant.analytics.fuelCosts') }}</span>
          <strong class="ppa-metric-value ppa-fuel-cost mt-1 block text-base">{{ formatCurrency(powerPlantAnalytics.totalFuelCosts) }}</strong>
        </div>
        <div v-if="(powerPlantAnalytics.totalSpotMarketRevenue ?? 0) > 0" class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label">{{ t('energyPanel.spotRevenue') }}</span>
          <strong class="ppa-metric-value ppa-spot-revenue mt-1 block text-base text-green-600 dark:text-green-400">{{ formatCurrency(powerPlantAnalytics.totalSpotMarketRevenue ?? 0) }}</strong>
        </div>
        <div class="ppa-metric rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-metric-label">{{ t('powerPlant.analytics.netProfit') }}</span>
          <strong
            class="ppa-metric-value mt-1 block text-base"
            :class="{
              'building-profit-positive-text': powerPlantAnalytics.totalNetProfit >= 0,
              'building-profit-negative-text': powerPlantAnalytics.totalNetProfit < 0,
            }"
          >
            {{ formatCurrency(powerPlantAnalytics.totalNetProfit) }}
          </strong>
        </div>
      </div>

      <!-- Per-tick P&L bar chart -->
      <div
        v-if="powerPlantAnalytics.timeline.some((s) => (s.surplusIncome ?? 0) > 0 || (s.gridFine ?? 0) > 0 || (s.operatingCosts ?? 0) > 0 || (s.fuelCosts ?? 0) > 0)"
        class="ppa-chart mt-4 flex h-14 items-end gap-px overflow-hidden rounded-md border border-divider bg-surface px-1 py-1"
        role="img"
        :aria-label="t('powerPlant.analytics.panelTitle')"
      >
        <div
          v-for="snap in powerPlantAnalytics.timeline"
          :key="snap.tick"
          class="ppa-bar-group flex min-w-[2px] flex-1 items-end gap-px"
          :title="
            t('powerPlant.analytics.tickTooltip', {
              tick: snap.tick,
              income: formatCurrency(snap.surplusIncome ?? 0),
              costs: formatCurrency((snap.gridFine ?? 0) + (snap.operatingCosts ?? 0) + (snap.fuelCosts ?? 0)),
            })
          "
        >
          <div
            v-if="(snap.surplusIncome ?? 0) > 0"
            class="ppa-bar ppa-bar-income min-w-[1px] flex-1 rounded-t-sm"
            :style="{
              height: `${Math.min(Math.round(((snap.surplusIncome ?? 0) / chartMaxValue) * 50), 50)}px`,
            }"
          />
          <div
            v-if="(snap.gridFine ?? 0) + (snap.operatingCosts ?? 0) + (snap.fuelCosts ?? 0) > 0"
            class="ppa-bar ppa-bar-cost min-w-[1px] flex-1 rounded-t-sm"
            :style="{
              height: `${Math.min(Math.round((((snap.gridFine ?? 0) + (snap.operatingCosts ?? 0) + (snap.fuelCosts ?? 0)) / chartMaxValue) * 50), 50)}px`,
            }"
          />
        </div>
      </div>
      <p v-else class="ppa-empty-state mt-3 text-sm text-muted">{{ t('powerPlant.analytics.noData') }}</p>
    </template>

    <p v-else class="ppa-empty-state mt-3 text-sm text-muted">{{ t('powerPlant.analytics.noData') }}</p>

    <!-- Unit description cards for all power plant unit types -->
    <div class="ppa-unit-guide mt-4 grid gap-3 border-t border-divider pt-4">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted">⚡ Generation Units</h3>
      <div class="grid gap-2 sm:grid-cols-2">
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">⚡</span>
          <div>
            <strong>{{ t('powerPlant.units.POWER_GENERATION.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.POWER_GENERATION.description', { boost: 10 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">🔥</span>
          <div>
            <strong>{{ t('powerPlant.units.ENERGY_PRODUCING.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.ENERGY_PRODUCING.description', { boost: 20 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">💨</span>
          <div>
            <strong>{{ t('powerPlant.units.WIND_TURBINE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.WIND_TURBINE.description', { boost: 8 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">💧</span>
          <div>
            <strong>{{ t('powerPlant.units.WATER_TURBINE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.WATER_TURBINE.description', { boost: 12 }) }}</p>
          </div>
        </div>
      </div>
      <h3 class="mt-1 text-xs font-semibold uppercase tracking-wide text-muted">🔋 Support Units</h3>
      <div class="grid gap-2 sm:grid-cols-3">
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">🔋</span>
          <div>
            <strong>{{ t('powerPlant.units.BATTERY_STORAGE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.BATTERY_STORAGE.description', { buffer: 5 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">🪨</span>
          <div>
            <strong>{{ t('powerPlant.units.ENERGY_STORAGE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.ENERGY_STORAGE.description', { buffer: 8 }) }}</p>
          </div>
        </div>
        <div class="ppa-unit-card flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <span class="ppa-unit-icon text-2xl">⛽</span>
          <div>
            <strong>{{ t('powerPlant.units.FUEL_PURCHASE.label') }}</strong>
            <p class="ppa-unit-desc mt-1 text-sm text-muted">{{ t('powerPlant.units.FUEL_PURCHASE.description', { boost: 10 }) }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
