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
  class="media-house-mgmt-panel"
  role="region"
  aria-label="media house management"
>
  <div class="media-house-mgmt-header">
    <h2 class="media-house-mgmt-title">📡 {{ t('mediaHouse.panelTitle') }}</h2>
  </div>

  <!-- Key metrics row -->
  <div class="media-house-metrics">
    <div class="media-house-metric">
      <span class="media-house-metric-label">{{ t('mediaHouse.channelType') }}</span>
      <span class="media-house-metric-value">{{ building?.mediaType ?? '—' }}</span>
    </div>
    <div class="media-house-metric">
      <span class="media-house-metric-label">{{ t('mediaHouse.accumulatedContent') }}</span>
      <span class="media-house-metric-value mh-content-value">
        {{ (building?.contentValue ?? 0).toFixed(0) }}
      </span>
    </div>
    <div class="media-house-metric">
      <span class="media-house-metric-label">{{ t('mediaHouse.activeContentBudget') }}</span>
      <span
        class="media-house-metric-value"
        :class="{ 'mh-budget-active': building?.contentBudgetPerTick, 'mh-budget-none': !building?.contentBudgetPerTick }"
      >
        {{
          building?.contentBudgetPerTick
            ? formatCurrency(building?.contentBudgetPerTick) + ' / ' + t('mediaHouse.perTick')
            : t('mediaHouse.noBudget')
        }}
      </span>
    </div>
    <div v-if="building?.contentBudgetPerTick" class="media-house-metric">
      <span class="media-house-metric-label">{{ t('mediaHouse.levelEfficiency') }}</span>
      <span class="media-house-metric-value mh-efficiency">
        {{ Math.round((1 - 1 / (building?.level + 1)) * 100) }}%
      </span>
    </div>
  </div>

  <!-- Content ranking among city competitors -->
  <div v-if="cityMediaHouses.length > 0" class="media-house-ranking-section">
    <h3 class="media-house-section-title">{{ t('mediaHouse.competitorsTitle') }}</h3>
    <div class="media-house-competitors">
      <div
        v-for="mh in cityMediaHouses.filter((m) => m.mediaType === building?.mediaType)"
        :key="mh.id"
        class="mh-competitor-row"
        :class="{ 'mh-competitor-own': mh.id === building?.id }"
      >
        <span class="mh-competitor-name">{{ mh.name }}</span>
        <div class="mh-competitor-bar-wrap">
          <div
            class="mh-competitor-bar"
            :style="{ width: mh.contentRanking + '%' }"
            :class="{ 'mh-bar-own': mh.id === building?.id, 'mh-bar-gov': mh.isGovernmentOwned }"
          />
        </div>
        <span class="mh-competitor-pct">{{ mh.contentRanking.toFixed(0) }}%</span>
        <span v-if="mh.id === building?.id" class="mh-competitor-you">{{ t('mediaHouse.youBadge') }}</span>
      </div>
    </div>
    <p class="media-house-ranking-hint">{{ t('mediaHouse.rankingHint') }}</p>
  </div>
  <div v-else-if="cityMediaHousesLoading" class="media-house-loading">{{ t('common.loading') }}</div>

  <!-- Content budget configuration (owner only, not government-owned) -->
  <div class="media-house-budget-section">
    <h3 class="media-house-section-title">{{ t('mediaHouse.budgetConfigTitle') }}</h3>
    <p class="media-house-budget-hint">{{ t('mediaHouse.budgetHint', { efficiency: Math.round((1 - 1 / (building?.level + 1)) * 100) }) }}</p>
    <div class="media-house-budget-form">
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
      <p class="media-house-budget-preview" v-if="contentBudgetInput && contentBudgetInput > 0">
        {{
          t('mediaHouse.budgetPreview', {
            gain: Math.round(contentBudgetInput * (1 - 1 / (building?.level + 1))),
            spend: formatCurrency(contentBudgetInput),
          })
        }}
      </p>
      <p v-if="contentBudgetError" class="media-house-budget-error">{{ contentBudgetError }}</p>
      <p v-if="contentBudgetSuccess" class="media-house-budget-success">{{ t('mediaHouse.budgetSaved') }}</p>
      <button
        class="btn btn-primary"
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
  <div class="media-house-effectiveness-section">
    <h3 class="media-house-section-title">{{ t('mediaHouse.effectivenessTitle') }}</h3>
    <div class="media-house-effectiveness-row">
      <span class="mh-channel-mult-label">{{ t('mediaHouse.channelMultiplier') }}</span>
      <span class="mh-channel-mult-value">×{{ building?.mediaType === 'TV' ? '2.0' : building?.mediaType === 'RADIO' ? '1.5' : '1.0' }}</span>
    </div>
    <div class="media-house-effectiveness-row">
      <span class="mh-channel-mult-label">{{ t('mediaHouse.contentBoostLabel') }}</span>
      <span class="mh-channel-mult-value">
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
    <p class="media-house-effectiveness-hint">{{ t('mediaHouse.effectivenessHint') }}</p>
  </div>
</div>

</template>
