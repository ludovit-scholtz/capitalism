<script setup lang="ts">
import { inject, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  setPowerPriority,
  prioritySaving,
  priorityError,
  prioritySuccess,
  setMaxEnergyBidPrice,
  maxBidSaving,
  maxBidError,
  maxBidSuccess,
  formatCurrency,
} = bd

// ── Power priority ─────────────────────────────────────────────────────────
const draftPriority = ref(5)

watch(
  () => building.value?.powerPriority,
  (v) => {
    if (v != null) draftPriority.value = v
  },
  { immediate: true },
)

async function savePriority() {
  if (!building.value) return
  await setPowerPriority(building.value.id, draftPriority.value)
}

// ── Max bid price ──────────────────────────────────────────────────────────
const draftMaxBid = ref<string>('')

watch(
  () => building.value?.maxEnergyBidPrice,
  (v) => {
    draftMaxBid.value = v != null ? String(v) : ''
  },
  { immediate: true },
)

async function saveMaxBid() {
  if (!building.value) return
  const parsed = draftMaxBid.value.trim() === '' ? null : parseFloat(draftMaxBid.value)
  if (parsed !== null && isNaN(parsed)) return
  await setMaxEnergyBidPrice(building.value.id, parsed)
}

async function clearMaxBid() {
  if (!building.value) return
  draftMaxBid.value = ''
  await setMaxEnergyBidPrice(building.value.id, null)
}
</script>

<template>
  <div
    class="building-energy-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5"
    role="region"
    :aria-label="t('energyPanel.panelTitle')"
  >
    <h2 class="energy-panel-title mb-4 text-base font-semibold text-foreground">⚡ {{ t('energyPanel.panelTitle') }}</h2>

    <!-- Power priority -->
    <div class="energy-priority-section mb-5">
      <label class="mb-1 block text-sm font-medium text-foreground" for="power-priority-select">
        {{ t('energyPanel.priorityLabel') }}
      </label>
      <p class="priority-hint mb-2 text-xs text-muted">{{ t('energyPanel.priorityHint') }}</p>
      <div class="flex items-center gap-3">
        <select
          id="power-priority-select"
          v-model="draftPriority"
          class="power-priority-select rounded-md border border-divider bg-surface px-3 py-2 text-sm text-foreground"
        >
          <option v-for="n in 10" :key="n" :value="n">{{ n }}</option>
        </select>
        <button
          class="btn btn-secondary btn-sm priority-save-btn"
          :disabled="prioritySaving"
          @click="savePriority"
        >
          {{ prioritySaving ? '…' : t('energyPanel.prioritySave') }}
        </button>
      </div>
      <p v-if="prioritySuccess" class="priority-success mt-1 text-xs text-green-600 dark:text-green-400">
        {{ t('energyPanel.prioritySaved') }}
      </p>
      <p v-if="priorityError" class="priority-error mt-1 text-xs text-red-600 dark:text-red-400">
        {{ priorityError }}
      </p>
    </div>

    <!-- Max spot-market bid price (only for non-power-plant buildings) -->
    <div v-if="building?.type !== 'POWER_PLANT'" class="energy-bid-section">
      <label class="mb-1 block text-sm font-medium text-foreground" for="max-bid-input">
        {{ t('energyPanel.maxBidLabel') }}
        <span class="font-normal text-muted">{{ t('energyPanel.maxBidUnit') }}</span>
      </label>
      <p class="bid-hint mb-2 text-xs text-muted">{{ t('energyPanel.maxBidHint') }}</p>
      <div class="flex flex-wrap items-center gap-3">
        <input
          id="max-bid-input"
          v-model="draftMaxBid"
          type="number"
          min="0"
          step="0.001"
          class="max-bid-input w-36 rounded-md border border-divider bg-surface px-3 py-2 text-sm text-foreground"
          :placeholder="formatCurrency(0)"
        />
        <button
          class="btn btn-secondary btn-sm bid-save-btn"
          :disabled="maxBidSaving"
          @click="saveMaxBid"
        >
          {{ maxBidSaving ? '…' : t('energyPanel.maxBidSave') }}
        </button>
        <button
          v-if="building?.maxEnergyBidPrice != null"
          class="btn btn-ghost btn-sm bid-clear-btn text-muted"
          :disabled="maxBidSaving"
          @click="clearMaxBid"
        >
          {{ t('energyPanel.maxBidClear') }}
        </button>
      </div>
      <p v-if="maxBidSuccess" class="bid-success mt-1 text-xs text-green-600 dark:text-green-400">
        {{ t('energyPanel.maxBidSaved') }}
      </p>
      <p v-if="maxBidError" class="bid-error mt-1 text-xs text-red-600 dark:text-red-400">
        {{ maxBidError }}
      </p>
    </div>
  </div>
</template>

<style scoped>
.btn-sm {
  padding: 0.35rem 0.75rem;
  font-size: 0.8125rem;
}
</style>
