<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { draftConstructionCost, projectedCompanyCashAfterApply, formatCurrency } = bd
</script>

<template>
  <section class="building-edit-basic-data-tab" role="tabpanel" :aria-label="t('buildingDetail.editBuildingTabBasicData')">
    <h4>{{ t('buildingDetail.editBuildingTabBasicData') }}</h4>
    <p class="unit-desc">{{ t('buildingDetail.editBuildingBasicDataBody') }}</p>

    <div class="unit-insight-card">
      <h5>{{ t('buildingDetail.sidebarPlaceholderTitle') }}</h5>
      <p class="unit-desc">{{ t('buildingDetail.sidebarPlaceholderBodyEditing') }}</p>
    </div>

    <div class="unit-insight-card placeholder-summary-card">
      <h5>{{ t('buildingDetail.costSummaryTitle') }}</h5>
      <div class="unit-stats">
        <span class="stat">{{ t('buildingDetail.totalBuildCost', { cost: formatCurrency(draftConstructionCost) }) }}</span>
        <span v-if="projectedCompanyCashAfterApply != null" class="stat">
          {{ t('buildingDetail.cashAfterApply', { cash: formatCurrency(projectedCompanyCashAfterApply) }) }}
        </span>
      </div>
    </div>
  </section>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>