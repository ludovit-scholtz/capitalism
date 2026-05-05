<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import UnitConfigurationTabView from '@/components/buildings/UnitConfigurationTabView.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  isEditing,
  selectedCell,
  showUnitPicker,
  plannedUnits,
  allowedUnits,
  placeUnit,
  setReadOnlySelectedCell,
  getUnitAtFrom,
  getUnitColor,
  getUnitConstructionCost,
  formatCurrency,
} = bd
</script>

<template>
  <div class="sidebar" v-if="selectedCell && isEditing">
    <div v-if="showUnitPicker" class="unit-picker">
      <div class="picker-header">
        <h3>{{ t('buildingDetail.selectUnitType') }}</h3>
        <button class="btn btn-ghost" @click="showUnitPicker = false">{{ t('common.close') }}</button>
      </div>
      <p class="picker-subtitle">{{ t('buildingDetail.allowedUnits') }}</p>
      <div class="picker-grid">
        <button v-for="unitType in allowedUnits" :key="unitType" class="picker-option" @click="placeUnit(unitType)">
          <span class="picker-color" :style="{ background: getUnitColor(unitType) }"></span>
          <div class="picker-info">
            <span class="picker-name">{{ t(`buildingDetail.unitTypes.${unitType}`) }}</span>
            <span class="picker-desc">{{ t(`buildingDetail.unitDescriptions.${unitType}`) }}</span>
            <span class="picker-cost">{{ t('buildingDetail.unitCost', { cost: formatCurrency(getUnitConstructionCost(unitType)) }) }}</span>
          </div>
        </button>
      </div>
    </div>

    <div v-if="!showUnitPicker && getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)" class="unit-config">
      <div class="unit-config-header">
        <h3>{{ t('buildingDetail.unitConfiguration') }}</h3>
        <button class="btn btn-ghost" @click="setReadOnlySelectedCell(null)">{{ t('common.close') }}</button>
      </div>
      <UnitConfigurationTabView />
    </div>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>
<style scoped src="./BuildingSidebar.analytics.css"></style>
<style scoped src="./BuildingSidebar.exchange.css"></style>
