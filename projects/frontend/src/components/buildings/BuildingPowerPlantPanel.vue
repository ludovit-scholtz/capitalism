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

      <!-- Fuel reserve (thermal plants only) -->
      <div
        v-if="isThermalPlant"
        class="fuel-reserve mb-4 rounded-lg border border-divider bg-surface p-3"
      >
        <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
          <span class="text-xs font-semibold uppercase tracking-wide text-muted">⛽ {{ t('powerPlant.fuelReserve.title') }}</span>
          <span class="text-sm font-bold text-foreground">{{ (powerPlantAnalytics.fuelReserveMwh ?? 0).toFixed(1) }} MWh</span>
        </div>
        <p class="text-xs text-muted">{{ t('powerPlant.fuelReserve.hint') }}</p>
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
