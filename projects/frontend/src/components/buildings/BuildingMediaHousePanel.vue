<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, cityMediaHouses, cityMediaHousesLoading, contentBudgetInput, savingContentBudget, contentBudgetError, contentBudgetSuccess, initContentBudgetInput, saveContentBudget, formatCurrency } = bd
</script>

<template>
<div
  v-if="building?.type === 'MEDIA_HOUSE'"
  class="media-house-mgmt-panel mb-4 rounded-xl border border-divider bg-card p-4 sm:p-5"
  role="region"
  aria-label="media house management"
>
  <div class="media-house-mgmt-header mb-4 flex items-center justify-between">
    <h2 class="media-house-mgmt-title text-lg font-semibold text-foreground">📡 {{ t('mediaHouse.panelTitle') }}</h2>
  </div>

  <!-- Key metrics row -->
  <div class="media-house-metrics grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
    <div class="media-house-metric rounded-lg border border-divider bg-surface p-3">
      <span class="media-house-metric-label text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.channelType') }}</span>
      <span class="media-house-metric-value mt-1 block text-sm font-semibold text-foreground">{{ building?.mediaType ?? '—' }}</span>
    </div>
    <div class="media-house-metric rounded-lg border border-divider bg-surface p-3">
      <span class="media-house-metric-label text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.accumulatedContent') }}</span>
      <span class="media-house-metric-value mh-content-value mt-1 block text-sm font-semibold text-foreground">
        {{ (building?.contentValue ?? 0).toFixed(0) }}
      </span>
    </div>
    <div class="media-house-metric rounded-lg border border-divider bg-surface p-3">
      <span class="media-house-metric-label text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.activeContentBudget') }}</span>
      <span
        class="media-house-metric-value mt-1 block text-sm font-semibold"
        :class="{ 'mh-budget-active text-emerald-600 dark:text-emerald-300': building?.contentBudgetPerTick, 'mh-budget-none text-muted': !building?.contentBudgetPerTick }"
      >
        {{
          building?.contentBudgetPerTick
            ? formatCurrency(building?.contentBudgetPerTick) + ' / ' + t('mediaHouse.perTick')
            : t('mediaHouse.noBudget')
        }}
      </span>
    </div>
    <div v-if="building?.contentBudgetPerTick" class="media-house-metric rounded-lg border border-divider bg-surface p-3">
      <span class="media-house-metric-label text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.levelEfficiency') }}</span>
      <span class="media-house-metric-value mh-efficiency mt-1 block text-sm font-semibold text-foreground">
        {{ Math.round((1 - 1 / (building?.level + 1)) * 100) }}%
      </span>
    </div>
  </div>

  <!-- Content ranking among city competitors -->
  <div v-if="cityMediaHouses.length > 0" class="media-house-ranking-section mt-4 rounded-lg border border-divider bg-surface p-3.5">
    <h3 class="media-house-section-title text-sm font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.competitorsTitle') }}</h3>
    <div class="media-house-competitors mt-2 grid gap-2">
      <div
        v-for="mh in cityMediaHouses.filter((m) => m.mediaType === building?.mediaType)"
        :key="mh.id"
        class="mh-competitor-row grid grid-cols-[minmax(0,1fr)_minmax(110px,1.5fr)_auto_auto] items-center gap-2 rounded-md border border-divider bg-card px-2.5 py-2"
        :class="{ 'mh-competitor-own border-primary/60 bg-primary/10': mh.id === building?.id }"
      >
        <span class="mh-competitor-name truncate text-sm font-medium text-foreground">{{ mh.name }}</span>
        <div class="mh-competitor-bar-wrap h-2 overflow-hidden rounded-full bg-border">
          <div
            class="mh-competitor-bar h-full rounded-full bg-primary"
            :style="{ width: mh.contentRanking + '%' }"
            :class="{ 'mh-bar-own bg-primary': mh.id === building?.id, 'mh-bar-gov bg-amber-500': mh.isGovernmentOwned }"
          />
        </div>
        <span class="mh-competitor-pct text-xs font-semibold text-foreground">{{ mh.contentRanking.toFixed(0) }}%</span>
        <span v-if="mh.id === building?.id" class="mh-competitor-you rounded-full border border-primary/60 bg-primary/15 px-2 py-0.5 text-[0.7rem] font-semibold text-primary">{{ t('mediaHouse.youBadge') }}</span>
      </div>
    </div>
    <p class="media-house-ranking-hint mt-2 text-xs text-muted">{{ t('mediaHouse.rankingHint') }}</p>
  </div>
  <div v-else-if="cityMediaHousesLoading" class="media-house-loading mt-3 text-sm text-muted">{{ t('common.loading') }}</div>

  <!-- Content budget configuration (owner only, not government-owned) -->
  <div class="media-house-budget-section mt-4 rounded-lg border border-divider bg-surface p-3.5">
    <h3 class="media-house-section-title text-sm font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.budgetConfigTitle') }}</h3>
    <p class="media-house-budget-hint mt-1 text-sm text-muted">{{ t('mediaHouse.budgetHint', { efficiency: Math.round((1 - 1 / (building?.level + 1)) * 100) }) }}</p>
    <div class="media-house-budget-form mt-3 grid gap-2">
      <label class="form-label">{{ t('mediaHouse.budgetLabel') }}</label>
      <input
        type="number"
        class="form-input"
        :placeholder="t('mediaHouse.budgetPlaceholder')"
        :value="contentBudgetInput"
        @focus="initContentBudgetInput"
        @input="contentBudgetInput = isNaN(($event.target as HTMLInputElement).valueAsNumber) ? null : ($event.target as HTMLInputElement).valueAsNumber"
        min="0"
        step="100"
      />
      <p class="media-house-budget-preview rounded-md border border-emerald-300/50 bg-emerald-500/10 px-2.5 py-2 text-xs text-emerald-800 dark:text-emerald-300" v-if="contentBudgetInput && contentBudgetInput > 0">
        {{
          t('mediaHouse.budgetPreview', {
            gain: Math.round(contentBudgetInput * (1 - 1 / (building?.level + 1))),
            spend: formatCurrency(contentBudgetInput),
          })
        }}
      </p>
      <p v-if="contentBudgetError" class="media-house-budget-error rounded-md border border-red-300/50 bg-red-500/10 px-2.5 py-2 text-xs text-red-700 dark:text-red-300">{{ contentBudgetError }}</p>
      <p v-if="contentBudgetSuccess" class="media-house-budget-success rounded-md border border-emerald-300/50 bg-emerald-500/10 px-2.5 py-2 text-xs text-emerald-800 dark:text-emerald-300">{{ t('mediaHouse.budgetSaved') }}</p>
      <button
        class="btn btn-primary mt-1"
        :disabled="savingContentBudget"
        @click="saveContentBudget"
      >
        {{ savingContentBudget ? t('common.saving') : t('mediaHouse.saveBudgetBtn') }}
      </button>
      <button
        v-if="building?.contentBudgetPerTick"
        class="btn btn-secondary"
        :disabled="savingContentBudget"
        @click="contentBudgetInput = 0; saveContentBudget()"
      >
        {{ t('mediaHouse.stopInvestmentBtn') }}
      </button>
    </div>
  </div>

  <!-- Marketing effectiveness context -->
  <div class="media-house-effectiveness-section mt-4 rounded-lg border border-divider bg-surface p-3.5">
    <h3 class="media-house-section-title text-sm font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.effectivenessTitle') }}</h3>
    <div class="media-house-effectiveness-row mt-2 flex items-center justify-between rounded-md border border-divider bg-card px-2.5 py-2">
      <span class="mh-channel-mult-label text-sm text-muted">{{ t('mediaHouse.channelMultiplier') }}</span>
      <span class="mh-channel-mult-value text-sm font-semibold text-foreground">×{{ building?.mediaType === 'TV' ? '2.0' : building?.mediaType === 'RADIO' ? '1.5' : '1.0' }}</span>
    </div>
    <div class="media-house-effectiveness-row mt-2 flex items-center justify-between rounded-md border border-divider bg-card px-2.5 py-2">
      <span class="mh-channel-mult-label text-sm text-muted">{{ t('mediaHouse.contentBoostLabel') }}</span>
      <span class="mh-channel-mult-value text-sm font-semibold text-foreground">
        {{
          (() => {
            const ownMh = cityMediaHouses.find((m) => m.id === building?.id)
            if (!ownMh) return '—'
            const fraction = ownMh.contentRanking / 100
            const multiplier = 0.5 + fraction * 1.0
            return '×' + multiplier.toFixed(2)
          })()
        }}
      </span>
    </div>
    <p class="media-house-effectiveness-hint mt-2 text-xs text-muted">{{ t('mediaHouse.effectivenessHint') }}</p>
  </div>
</div>

</template>
