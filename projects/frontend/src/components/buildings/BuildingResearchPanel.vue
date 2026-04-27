<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, researchBrands, researchBrandsLoading, hasConfiguredRdUnits, formatCurrency } = bd
</script>

<template>
  <div
    v-if="building?.type === 'RESEARCH_DEVELOPMENT'"
    class="research-progress-panel mb-4 rounded-lg border border-divider bg-card p-5"
    role="region"
    :aria-label="t('buildingDetail.accessibility.researchProgress')"
  >
    <div class="research-progress-header mb-2 flex items-center justify-between">
      <h2 class="research-progress-title m-0 text-lg font-semibold text-foreground">🔬 {{ t('research.panelTitle') }}</h2>
    </div>
    <p class="research-progress-intro mb-4 text-sm text-muted">{{ t('research.intro') }}</p>

    <div v-if="researchBrandsLoading" class="research-loading py-2 text-sm text-muted">{{ t('common.loading') }}</div>

    <div v-else-if="researchBrands.length === 0" class="research-empty-state py-2 text-sm italic text-muted">
      <span v-if="hasConfiguredRdUnits">⏳ {{ t('research.emptyStatePending') }}</span>
      <span v-else>{{ t('research.emptyState') }}</span>
    </div>

    <div v-else class="research-brand-list flex flex-col gap-3">
      <div v-for="brand in researchBrands" :key="brand.id" class="research-brand-card rounded-md border border-divider bg-surface p-3.5">
        <div class="research-brand-header mb-1 flex flex-wrap items-center gap-2">
          <span class="research-brand-name text-[0.9375rem] font-semibold text-foreground">{{ brand.productName || brand.name }}</span>
          <span class="research-brand-scope-badge inline-flex rounded-full bg-primary/15 px-1.5 py-0.5 text-[0.6875rem] font-semibold uppercase tracking-wide text-primary">
            {{ brand.scope === 'PRODUCT' ? t('buildingDetail.config.scopeProduct') : brand.scope === 'CATEGORY' ? t('buildingDetail.config.scopeCategory') : t('buildingDetail.config.scopeCompany') }}
          </span>
        </div>
        <div v-if="brand.industryCategory" class="research-brand-industry mb-2 text-[0.8125rem] text-muted">
          {{ brand.industryCategory }}
        </div>
        <div class="research-brand-metrics my-2 flex flex-col gap-2">
          <div v-if="brand.quality > 0" class="research-metric grid grid-cols-[7rem_minmax(0,1fr)_3.5rem] items-center gap-2 sm:grid-cols-[8rem_minmax(0,1fr)_3.5rem]">
            <span class="research-metric-label whitespace-nowrap text-[0.8125rem] text-muted">{{ t('research.qualityLabel') }}</span>
            <div
              class="research-progress-bar h-2 overflow-hidden rounded-full bg-border"
              :aria-label="t('buildingDetail.accessibility.researchProductQuality', { value: (brand.quality * 100).toFixed(1) })"
            >
              <div
                class="research-progress-fill research-progress-quality h-full rounded-full bg-primary transition-[width] duration-300"
                :style="{ width: `${(brand.quality * 100).toFixed(1)}%` }"
              ></div>
            </div>
            <span class="research-metric-value text-right text-[0.8125rem] font-semibold text-foreground">{{ (brand.quality * 100).toFixed(1) }}%</span>
          </div>

          <div v-if="brand.marketingEfficiencyMultiplier > 1" class="research-metric grid grid-cols-[7rem_minmax(0,1fr)_3.5rem] items-center gap-2 sm:grid-cols-[8rem_minmax(0,1fr)_3.5rem]">
            <span class="research-metric-label whitespace-nowrap text-[0.8125rem] text-muted">{{ t('research.marketingEfficiencyLabel') }}</span>
            <div
              class="research-progress-bar h-2 overflow-hidden rounded-full bg-border"
              :aria-label="t('buildingDetail.accessibility.researchMarketingEfficiency', { value: brand.marketingEfficiencyMultiplier.toFixed(2) })"
            >
              <div
                class="research-progress-fill research-progress-efficiency h-full rounded-full bg-emerald-500 transition-[width] duration-300"
                :style="{ width: `${Math.min(100, (brand.marketingEfficiencyMultiplier - 1) * 100).toFixed(1)}%` }"
              ></div>
            </div>
            <span class="research-metric-value text-right text-[0.8125rem] font-semibold text-foreground">{{ brand.marketingEfficiencyMultiplier.toFixed(2) }}×</span>
          </div>

          <div v-if="brand.awareness > 0" class="research-metric grid grid-cols-[7rem_minmax(0,1fr)_3.5rem] items-center gap-2 sm:grid-cols-[8rem_minmax(0,1fr)_3.5rem]">
            <span class="research-metric-label whitespace-nowrap text-[0.8125rem] text-muted">{{ t('research.awarenessLabel') }}</span>
            <div
              class="research-progress-bar h-2 overflow-hidden rounded-full bg-border"
              :aria-label="t('buildingDetail.accessibility.researchBrandAwareness', { value: (brand.awareness * 100).toFixed(1) })"
            >
              <div
                class="research-progress-fill research-progress-awareness h-full rounded-full bg-violet-500 transition-[width] duration-300"
                :style="{ width: `${(brand.awareness * 100).toFixed(1)}%` }"
              ></div>
            </div>
            <span class="research-metric-value text-right text-[0.8125rem] font-semibold text-foreground">{{ (brand.awareness * 100).toFixed(1) }}%</span>
          </div>
        </div>

        <div
          v-if="brand.scope === 'PRODUCT' && (brand.accumulatedResearchBudget != null || brand.baseResearchBudget != null)"
          class="research-budget-panel mt-2 rounded-md bg-surface-muted px-3 py-2.5"
        >
          <div class="research-budget-row flex items-center justify-between gap-2 py-0.5">
            <span class="research-budget-label text-[0.8125rem] text-muted">{{ t('research.budget.accumulated') }}</span>
            <span class="research-budget-value text-[0.8125rem] font-semibold text-foreground">{{
              brand.accumulatedResearchBudget != null ? formatCurrency(brand.accumulatedResearchBudget) : '—'
            }}</span>
          </div>
          <div class="research-budget-row flex items-center justify-between gap-2 py-0.5">
            <span class="research-budget-label text-[0.8125rem] text-muted">{{ t('research.budget.target') }}</span>
            <span class="research-budget-value text-[0.8125rem] font-semibold text-foreground">{{ brand.baseResearchBudget != null ? formatCurrency(brand.baseResearchBudget) : '—' }}</span>
          </div>
          <div
            v-if="brand.maxCompetitorBudget != null && brand.accumulatedResearchBudget != null && brand.maxCompetitorBudget > (brand.accumulatedResearchBudget ?? 0)"
            class="research-budget-row research-budget-row--competitor mt-1 flex items-center justify-between gap-2 border-t border-divider pt-1.5"
          >
            <span class="research-budget-label text-[0.8125rem] text-muted">{{ t('research.budget.topCompetitor') }}</span>
            <span class="research-budget-value research-budget-value--warn text-[0.8125rem] font-semibold text-warning">{{ formatCurrency(brand.maxCompetitorBudget) }}</span>
          </div>
          <p class="research-budget-hint mt-1.5 text-xs italic text-muted">{{ t('research.budget.decayHint') }}</p>
        </div>

        <p class="research-brand-effect mt-2 flex flex-col gap-0.5 text-[0.8125rem] text-muted">
          <span v-if="brand.quality > 0">
            {{
              /* Brand quality contributes up to 30% quality bonus to manufactured output (game formula: quality * 30). */
              t('research.qualityEffect', { pct: (brand.quality * 30).toFixed(1) })
            }}
          </span>
          <span v-if="brand.marketingEfficiencyMultiplier > 1">
            {{ t('research.marketingEfficiencyEffect', { multiplier: brand.marketingEfficiencyMultiplier.toFixed(2) }) }}
          </span>
        </p>
      </div>
    </div>
  </div>
</template>
