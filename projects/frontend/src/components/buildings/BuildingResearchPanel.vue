<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { building, researchBrands, researchBrandsLoading, hasConfiguredRdUnits, formatCurrency } = bd
</script>

<template>
<div v-if="building?.type === 'RESEARCH_DEVELOPMENT'" class="research-progress-panel" role="region" aria-label="research progress">
  <div class="research-progress-header">
    <h2 class="research-progress-title">🔬 {{ t('research.panelTitle') }}</h2>
  </div>
  <p class="research-progress-intro">{{ t('research.intro') }}</p>

  <div v-if="researchBrandsLoading" class="research-loading">{{ t('common.loading') }}</div>

  <div v-else-if="researchBrands.length === 0" class="research-empty-state">
    <span v-if="hasConfiguredRdUnits">⏳ {{ t('research.emptyStatePending') }}</span>
    <span v-else>{{ t('research.emptyState') }}</span>
  </div>

  <div v-else class="research-brand-list">
    <div v-for="brand in researchBrands" :key="brand.id" class="research-brand-card">
      <div class="research-brand-header">
        <span class="research-brand-name">{{ brand.productName || brand.name }}</span>
        <span class="research-brand-scope-badge">
          {{
            brand.scope === 'PRODUCT' ? t('buildingDetail.config.scopeProduct') : brand.scope === 'CATEGORY' ? t('buildingDetail.config.scopeCategory') : t('buildingDetail.config.scopeCompany')
          }}
        </span>
      </div>
      <div v-if="brand.industryCategory" class="research-brand-industry">
        {{ brand.industryCategory }}
      </div>
      <div class="research-brand-metrics">
        <!-- Product Quality metric (only shown when > 0 or scope is product-quality-relevant) -->
        <div v-if="brand.quality > 0" class="research-metric">
          <span class="research-metric-label">{{ t('research.qualityLabel') }}</span>
          <div class="research-progress-bar" :aria-label="`Product quality ${(brand.quality * 100).toFixed(1)}%`">
            <div class="research-progress-fill research-progress-quality" :style="{ width: `${(brand.quality * 100).toFixed(1)}%` }"></div>
          </div>
          <span class="research-metric-value">{{ (brand.quality * 100).toFixed(1) }}%</span>
        </div>
        <!-- Marketing Efficiency metric (BRAND_QUALITY R&D result) -->
        <div v-if="brand.marketingEfficiencyMultiplier > 1" class="research-metric">
          <span class="research-metric-label">{{ t('research.marketingEfficiencyLabel') }}</span>
          <div class="research-progress-bar" :aria-label="`Marketing efficiency ${brand.marketingEfficiencyMultiplier.toFixed(2)}x`">
            <div class="research-progress-fill research-progress-efficiency" :style="{ width: `${Math.min(100, (brand.marketingEfficiencyMultiplier - 1) * 100).toFixed(1)}%` }"></div>
          </div>
          <span class="research-metric-value">{{ brand.marketingEfficiencyMultiplier.toFixed(2) }}×</span>
        </div>
        <!-- Brand Awareness (from marketing spend, informational) -->
        <div v-if="brand.awareness > 0" class="research-metric">
          <span class="research-metric-label">{{ t('research.awarenessLabel') }}</span>
          <div class="research-progress-bar" :aria-label="`Brand awareness ${(brand.awareness * 100).toFixed(1)}%`">
            <div class="research-progress-fill research-progress-awareness" :style="{ width: `${(brand.awareness * 100).toFixed(1)}%` }"></div>
          </div>
          <span class="research-metric-value">{{ (brand.awareness * 100).toFixed(1) }}%</span>
        </div>
      </div>

      <!-- Cumulative research budget panel (PRODUCT_QUALITY R&D model) -->
      <div v-if="brand.scope === 'PRODUCT' && (brand.accumulatedResearchBudget != null || brand.baseResearchBudget != null)" class="research-budget-panel">
        <div class="research-budget-row">
          <span class="research-budget-label">{{ t('research.budget.accumulated') }}</span>
          <span class="research-budget-value">{{ brand.accumulatedResearchBudget != null ? formatCurrency(brand.accumulatedResearchBudget) : '—' }}</span>
        </div>
        <div class="research-budget-row">
          <span class="research-budget-label">{{ t('research.budget.target') }}</span>
          <span class="research-budget-value">{{ brand.baseResearchBudget != null ? formatCurrency(brand.baseResearchBudget) : '—' }}</span>
        </div>
        <div
          v-if="brand.maxCompetitorBudget != null && brand.accumulatedResearchBudget != null && brand.maxCompetitorBudget > (brand.accumulatedResearchBudget ?? 0)"
          class="research-budget-row research-budget-row--competitor"
        >
          <span class="research-budget-label">{{ t('research.budget.topCompetitor') }}</span>
          <span class="research-budget-value research-budget-value--warn">{{ formatCurrency(brand.maxCompetitorBudget) }}</span>
        </div>
        <p class="research-budget-hint">{{ t('research.budget.decayHint') }}</p>
      </div>

      <p class="research-brand-effect">
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
