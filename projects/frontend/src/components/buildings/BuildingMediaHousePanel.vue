<script setup lang="ts">
import { inject, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  building,
  cityMediaHouses,
  cityMediaHousesLoading,
  contentBudgetInput,
  savingContentBudget,
  contentBudgetError,
  contentBudgetSuccess,
  initContentBudgetInput,
  saveContentBudget,
  upgradingMediaHouse,
  mediaHouseUpgradeError,
  mediaHouseUpgradeSuccess,
  mediaHouseAnalytics,
  mediaHouseAnalyticsLoading,
  upgradeMediaHouse,
  formatCurrency,
} = bd

const stopInvestment = async () => {
  contentBudgetInput.value = 0
  await saveContentBudget()
}

const efficiencyPct = computed(() =>
  Math.round((1 - 1 / ((building.value?.level ?? 1) + 1)) * 100),
)

const ownMh = computed(() => cityMediaHouses.value.find((m) => m.id === building.value?.id))

const channelMultiplier = computed(() => {
  const mt = building.value?.mediaType
  return mt === 'TV' ? 2.0 : mt === 'RADIO' ? 1.5 : 1.0
})

const contentBoostMultiplier = computed(() => {
  if (!ownMh.value) return null
  const fraction = ownMh.value.contentRanking / 100
  return 0.5 + fraction * 1.0
})

const strategyRatingIcon = computed(() => {
  const r = mediaHouseAnalytics.value?.strategyRating
  if (r === 'DOMINANT') return '🏆'
  if (r === 'COMPETITIVE') return '📈'
  if (r === 'GROWING') return '🌱'
  return '🔧'
})

const strategyRatingColor = computed(() => {
  const r = mediaHouseAnalytics.value?.strategyRating
  if (r === 'DOMINANT') return 'text-amber-600 dark:text-amber-400'
  if (r === 'COMPETITIVE') return 'text-emerald-600 dark:text-emerald-400'
  if (r === 'GROWING') return 'text-sky-600 dark:text-sky-400'
  return 'text-muted'
})
</script>

<template>
  <div
    v-if="building?.type === 'MEDIA_HOUSE'"
    class="media-house-mgmt-panel mb-4 flex flex-col gap-4"
    role="region"
    :aria-label="t('buildingDetail.accessibility.mediaHouseManagement')"
  >
    <!-- Panel header & key metrics -->
    <div class="rounded-xl border border-divider bg-card p-4 sm:p-5">
      <div class="mb-3 flex items-center justify-between">
        <h2 class="media-house-mgmt-title text-lg font-semibold text-foreground">
          📡 {{ t('mediaHouse.panelTitle') }}
        </h2>
        <span
          v-if="mediaHouseAnalytics"
          class="media-house-strategy-badge rounded-full border border-divider px-2.5 py-1 text-xs font-semibold"
          :class="strategyRatingColor"
        >
          {{ strategyRatingIcon }} {{ t(`mediaHouse.strategyRating.${mediaHouseAnalytics.strategyRating}`) }}
        </span>
      </div>

      <div class="media-house-metrics grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div class="media-house-metric rounded-lg border border-divider bg-surface p-3">
          <span class="media-house-metric-label text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.channelType') }}</span>
          <span class="media-house-metric-value mt-1 block text-sm font-semibold text-foreground">{{ building?.mediaType ?? 'NEWSPAPER' }}</span>
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
            :class="{
              'mh-budget-active text-emerald-600 dark:text-emerald-300': building?.contentBudgetPerTick,
              'mh-budget-none text-muted': !building?.contentBudgetPerTick,
            }"
          >
            {{
              building?.contentBudgetPerTick
                ? formatCurrency(building.contentBudgetPerTick) + ' / ' + t('mediaHouse.perTick')
                : t('mediaHouse.noBudget')
            }}
          </span>
        </div>
        <div class="media-house-metric rounded-lg border border-divider bg-surface p-3">
          <span class="media-house-metric-label text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.levelEfficiency') }}</span>
          <span class="media-house-metric-value mh-efficiency mt-1 block text-sm font-semibold text-foreground">{{ efficiencyPct }}%</span>
        </div>
      </div>

      <div
        v-if="mediaHouseAnalytics?.strategyTip"
        class="media-house-strategy-tip mt-3 rounded-lg border border-divider bg-surface px-3 py-2.5 text-sm text-muted"
      >
        {{ mediaHouseAnalytics.strategyTip }}
      </div>
    </div>

    <!-- Upgrade path -->
    <div class="media-house-upgrade-section rounded-xl border border-divider bg-card p-4 sm:p-5">
      <h3 class="media-house-section-title mb-3 text-sm font-semibold uppercase tracking-wide text-muted">
        {{ t('mediaHouse.upgradeTitle') }}
      </h3>

      <div class="mh-efficiency-ladder mb-4 grid grid-cols-5 gap-1.5">
        <div
          v-for="lvl in 5"
          :key="lvl"
          class="mh-efficiency-step rounded-md border px-1.5 py-2 text-center"
          :class="{
            'border-primary/60 bg-primary/10 text-primary': lvl === (building?.level ?? 1),
            'border-emerald-400/60 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400': lvl < (building?.level ?? 1),
            'border-divider bg-surface text-muted': lvl > (building?.level ?? 1),
          }"
        >
          <div class="text-xs font-semibold">Lv{{ lvl }}</div>
          <div class="mt-0.5 text-[0.7rem]">{{ Math.round((1 - 1 / (lvl + 1)) * 100) }}%</div>
        </div>
      </div>

      <div v-if="building && building.level < 5">
        <div class="mh-upgrade-cost-row mb-3 flex items-start gap-3 rounded-lg border border-divider bg-surface p-3">
          <div class="flex-1">
            <p class="text-sm font-semibold text-foreground">
              {{ t('mediaHouse.upgradeToLevel', { level: building.level + 1 }) }}
            </p>
            <p class="mt-0.5 text-xs text-muted">
              {{
                t('mediaHouse.upgradeEfficiencyGain', {
                  from: efficiencyPct,
                  to: mediaHouseAnalytics?.nextLevelEfficiencyPct ?? Math.round((1 - 1 / (building.level + 2)) * 100),
                })
              }}
            </p>
          </div>
          <div class="text-right" v-if="mediaHouseAnalytics">
            <p class="text-sm font-semibold text-foreground">~{{ formatCurrency(mediaHouseAnalytics.upgradeCostEur ?? 0) }}</p>
            <p class="mt-0.5 text-xs text-muted">{{ mediaHouseAnalytics.upgradeTimeTicks }} {{ t('common.ticks') }}</p>
          </div>
        </div>

        <p v-if="mediaHouseUpgradeError" class="mh-upgrade-error mb-2 rounded-md border border-red-300/50 bg-red-500/10 px-2.5 py-2 text-xs text-red-700 dark:text-red-300">
          {{ mediaHouseUpgradeError }}
        </p>
        <p v-if="mediaHouseUpgradeSuccess" class="mh-upgrade-success mb-2 rounded-md border border-emerald-300/50 bg-emerald-500/10 px-2.5 py-2 text-xs text-emerald-800 dark:text-emerald-300">
          {{ t('mediaHouse.upgradeSuccess', { level: building.level }) }}
        </p>

        <button
          class="btn btn-primary w-full"
          :disabled="upgradingMediaHouse || building.isGovernmentOwned"
          @click="upgradeMediaHouse"
        >
          {{ upgradingMediaHouse ? t('common.saving') : t('mediaHouse.upgradeBtn') }}
        </button>
        <p class="mt-1.5 text-xs text-muted">{{ t('mediaHouse.upgradeHint') }}</p>
      </div>

      <div v-else class="mh-max-level-badge rounded-lg border border-amber-400/60 bg-amber-500/10 px-3 py-2.5 text-sm font-semibold text-amber-700 dark:text-amber-400">
        🏆 {{ t('mediaHouse.maxLevelReached') }}
      </div>
    </div>

    <!-- Content ranking -->
    <div class="rounded-xl border border-divider bg-card p-4 sm:p-5">
      <h3 class="media-house-section-title mb-3 text-sm font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.competitorsTitle') }}</h3>
      <div v-if="cityMediaHouses.filter((m) => m.mediaType === building?.mediaType).length > 0" class="media-house-competitors grid gap-2">
        <div
          v-for="mh in cityMediaHouses.filter((m) => m.mediaType === building?.mediaType)"
          :key="mh.id"
          class="mh-competitor-row grid grid-cols-[minmax(0,1fr)_minmax(110px,1.5fr)_auto_auto] items-center gap-2 rounded-md border border-divider bg-surface px-2.5 py-2"
          :class="{ 'mh-competitor-own border-primary/60 bg-primary/10': mh.id === building?.id }"
        >
          <span class="mh-competitor-name truncate text-sm font-medium text-foreground">{{ mh.name }}</span>
          <div class="mh-competitor-bar-wrap h-2 overflow-hidden rounded-full bg-border">
            <div
              class="mh-competitor-bar h-full rounded-full"
              :style="{ width: mh.contentRanking + '%' }"
              :class="{
                'mh-bar-own bg-primary': mh.id === building?.id,
                'mh-bar-gov bg-amber-500': mh.isGovernmentOwned,
                'bg-muted': !mh.isGovernmentOwned && mh.id !== building?.id,
              }"
            />
          </div>
          <span class="mh-competitor-pct text-xs font-semibold text-foreground">{{ mh.contentRanking.toFixed(0) }}%</span>
          <span v-if="mh.id === building?.id" class="mh-competitor-you rounded-full border border-primary/60 bg-primary/15 px-2 py-0.5 text-[0.7rem] font-semibold text-primary">
            {{ t('mediaHouse.youBadge') }}
          </span>
        </div>
      </div>
      <div v-else-if="cityMediaHousesLoading" class="media-house-loading text-sm text-muted">{{ t('common.loading') }}</div>
      <p class="media-house-ranking-hint mt-2 text-xs text-muted">{{ t('mediaHouse.rankingHint') }}</p>
    </div>

    <!-- Effectiveness summary -->
    <div class="media-house-effectiveness-section rounded-xl border border-divider bg-card p-4 sm:p-5">
      <h3 class="media-house-section-title mb-3 text-sm font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.effectivenessTitle') }}</h3>
      <div class="grid gap-2">
        <div class="media-house-effectiveness-row flex items-center justify-between rounded-md border border-divider bg-surface px-2.5 py-2">
          <span class="mh-channel-mult-label text-sm text-muted">{{ t('mediaHouse.channelMultiplier') }}</span>
          <span class="mh-channel-mult-value text-sm font-semibold text-foreground">×{{ channelMultiplier.toFixed(1) }}</span>
        </div>
        <div class="media-house-effectiveness-row flex items-center justify-between rounded-md border border-divider bg-surface px-2.5 py-2">
          <span class="mh-channel-mult-label text-sm text-muted">{{ t('mediaHouse.contentBoostLabel') }}</span>
          <span class="mh-channel-mult-value text-sm font-semibold text-foreground">
            {{ contentBoostMultiplier !== null ? '×' + contentBoostMultiplier.toFixed(2) : '—' }}
          </span>
        </div>
        <div v-if="mediaHouseAnalytics" class="media-house-effectiveness-row flex items-center justify-between rounded-md border border-primary/30 bg-primary/5 px-2.5 py-2">
          <span class="mh-combined-mult-label text-sm font-semibold text-foreground">{{ t('mediaHouse.combinedEffectiveMultiplier') }}</span>
          <span class="mh-combined-mult-value text-sm font-bold text-primary">×{{ mediaHouseAnalytics.effectiveMultiplier.toFixed(2) }}</span>
        </div>
      </div>
      <p class="media-house-effectiveness-hint mt-2 text-xs text-muted">{{ t('mediaHouse.effectivenessHint') }}</p>
    </div>

    <!-- Brand-impact analytics -->
    <div
      v-if="mediaHouseAnalytics && !mediaHouseAnalyticsLoading"
      class="media-house-brand-analytics-section rounded-xl border border-divider bg-card p-4 sm:p-5"
    >
      <h3 class="media-house-section-title mb-1 text-sm font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.brandAnalyticsTitle') }}</h3>
      <p class="mb-3 text-xs text-muted">{{ t('mediaHouse.brandAnalyticsSubtitle') }}</p>

      <div class="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
        <div class="rounded-lg border border-divider bg-surface p-3">
          <span class="text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.advertiserCount') }}</span>
          <span class="mt-1 block text-lg font-bold text-foreground">{{ mediaHouseAnalytics.advertiserCount }}</span>
        </div>
        <div class="rounded-lg border border-divider bg-surface p-3">
          <span class="text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.avgIncomePerTick') }}</span>
          <span class="mt-1 block text-lg font-bold text-foreground">{{ formatCurrency(mediaHouseAnalytics.avgIncomePerTick) }}</span>
        </div>
        <div class="rounded-lg border border-divider bg-surface p-3">
          <span class="text-xs uppercase tracking-wide text-muted">{{ t('mediaHouse.totalIncomeLast100') }}</span>
          <span class="mt-1 block text-lg font-bold text-foreground">{{ formatCurrency(mediaHouseAnalytics.totalIncomeLast100Ticks) }}</span>
        </div>
      </div>

      <div v-if="mediaHouseAnalytics.brandEffects.length > 0">
        <h4 class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.brandEffectsTitle') }}</h4>
        <div class="media-house-brand-effects-list grid gap-2">
          <div
            v-for="(row, i) in mediaHouseAnalytics.brandEffects"
            :key="i"
            class="mh-brand-effect-row rounded-md border border-divider bg-surface px-3 py-2.5"
          >
            <div class="flex items-start justify-between gap-2">
              <div>
                <span class="text-xs font-semibold text-foreground">{{ row.productName }}</span>
                <span class="ml-1.5 rounded-full border border-divider bg-card px-1.5 py-0.5 text-[0.65rem] text-muted">{{ row.brandScope }}</span>
                <p class="mt-0.5 text-[0.7rem] text-muted">{{ row.companyName }}</p>
              </div>
              <span class="shrink-0 rounded-full border border-primary/40 bg-primary/10 px-2 py-0.5 text-xs font-bold text-primary">
                ×{{ row.effectivenessMultiplierApplied.toFixed(2) }}
              </span>
            </div>
            <div class="mt-2 flex gap-3">
              <div class="flex-1">
                <div class="mb-0.5 flex items-center justify-between">
                  <span class="text-[0.65rem] text-muted">{{ t('mediaHouse.brandAwareness') }}</span>
                  <span class="text-[0.65rem] font-semibold text-foreground">{{ row.brandAwareness.toFixed(0) }}%</span>
                </div>
                <div class="h-1.5 overflow-hidden rounded-full bg-border">
                  <div class="h-full rounded-full bg-primary" :style="{ width: row.brandAwareness + '%' }" />
                </div>
              </div>
              <div class="flex-1">
                <div class="mb-0.5 flex items-center justify-between">
                  <span class="text-[0.65rem] text-muted">{{ t('mediaHouse.marketingQuality') }}</span>
                  <span class="text-[0.65rem] font-semibold text-foreground">{{ row.marketingQuality.toFixed(0) }}%</span>
                </div>
                <div class="h-1.5 overflow-hidden rounded-full bg-border">
                  <div class="h-full rounded-full bg-emerald-500" :style="{ width: row.marketingQuality + '%' }" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div v-else class="rounded-lg border border-divider bg-surface px-3 py-3.5 text-sm text-muted">
        {{ t('mediaHouse.noAdvertisers') }}
      </div>

      <div v-if="mediaHouseAnalytics.incomeHistory.length > 0" class="mt-4">
        <h4 class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.incomeHistoryTitle') }}</h4>
        <div class="mh-income-chart flex h-12 items-end gap-0.5 overflow-hidden rounded-md border border-divider bg-surface px-2 py-1.5">
          <div
            v-for="(entry, i) in mediaHouseAnalytics.incomeHistory.slice(-30)"
            :key="i"
            class="mh-income-bar flex-1 rounded-sm bg-primary/60 transition-all"
            :style="{ height: Math.max(4, (entry.amount / Math.max(...mediaHouseAnalytics.incomeHistory.map((e) => e.amount), 1)) * 100) + '%' }"
            :title="`Tick ${entry.tick}: ${formatCurrency(entry.amount)}`"
          />
        </div>
        <p class="mt-1 text-[0.65rem] text-muted">{{ t('mediaHouse.incomeHistoryCaption') }}</p>
      </div>
    </div>
    <div v-else-if="mediaHouseAnalyticsLoading" class="media-house-analytics-loading rounded-xl border border-divider bg-card p-4 text-sm text-muted">
      {{ t('common.loading') }}
    </div>

    <!-- Content budget configuration -->
    <div class="media-house-budget-section rounded-xl border border-divider bg-card p-4 sm:p-5">
      <h3 class="media-house-section-title mb-1 text-sm font-semibold uppercase tracking-wide text-muted">{{ t('mediaHouse.budgetConfigTitle') }}</h3>
      <p class="media-house-budget-hint mb-3 text-sm text-muted">{{ t('mediaHouse.budgetHint', { efficiency: efficiencyPct }) }}</p>
      <div class="media-house-budget-form grid gap-2">
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
        <p
          v-if="contentBudgetInput && contentBudgetInput > 0"
          class="media-house-budget-preview rounded-md border border-emerald-300/50 bg-emerald-500/10 px-2.5 py-2 text-xs text-emerald-800 dark:text-emerald-300"
        >
          {{
            t('mediaHouse.budgetPreview', {
              gain: Math.round(contentBudgetInput * (1 - 1 / ((building?.level ?? 1) + 1))),
              spend: formatCurrency(contentBudgetInput),
            })
          }}
        </p>
        <p v-if="contentBudgetError" class="media-house-budget-error rounded-md border border-red-300/50 bg-red-500/10 px-2.5 py-2 text-xs text-red-700 dark:text-red-300">{{ contentBudgetError }}</p>
        <p v-if="contentBudgetSuccess" class="media-house-budget-success rounded-md border border-emerald-300/50 bg-emerald-500/10 px-2.5 py-2 text-xs text-emerald-800 dark:text-emerald-300">
          {{ t('mediaHouse.budgetSaved') }}
        </p>
        <button class="btn btn-primary mt-1" :disabled="savingContentBudget" @click="saveContentBudget">
          {{ savingContentBudget ? t('common.saving') : t('mediaHouse.saveBudgetBtn') }}
        </button>
        <button v-if="building?.contentBudgetPerTick" class="btn btn-secondary" :disabled="savingContentBudget" @click="stopInvestment">
          {{ t('mediaHouse.stopInvestmentBtn') }}
        </button>
      </div>
    </div>
  </div>
</template>
