<script setup lang="ts">
import { inject } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import BuildingFinancialTimelineChart from '@/components/buildings/BuildingFinancialTimelineChart.vue'
import BuildingBankAccountPanel from '@/components/buildings/BuildingBankAccountPanel.vue'

const { t } = useI18n()
const router = useRouter()
const bd = inject(BUILDING_DETAIL_KEY)!
const {
  locale,
  building,
  loading,
  isEditing,
  buildingFinancialTimeline,
  buildingFinancialTimelineLoading,
  layoutName,
  layoutDescription,
  masterLayouts,
  masterLayoutsLoading,
  masterLayoutsError,
  localLayouts,
  layoutSaving,
  layoutSaveError,
  layoutSaveSuccess,
  layoutDeleteError,
  overwriteConfirmPending,
  draftConstructionCost,
  projectedCompanyCashAfterApply,
  buildingOverviewCityName,
  cityCurrencyCode,
  buildingOverviewMapRoute,
  buildingFinancialSnapshots,
  buildingFinancialHasActivity,
  masterConnected,
  masterUserEmail,
  saveLayout,
  requestLoadLayout,
  confirmOverwrite,
  cancelOverwrite,
  deleteLayout,
  layoutStructureSummary,
  formatCurrency,
  formatBuildingType,
  formatGameTickTime,
  getUnitColor,
  getLayoutCellType,
  formatGpsLocation,
  loadBuilding,
} = bd
</script>

<template>
  <div class="sidebar sidebar-placeholder">
    <div class="unit-config">
      <div class="unit-config-header">
        <h3>{{ isEditing ? t('buildingDetail.unitDetails') : t('buildingDetail.overview.title') }}</h3>
      </div>
      <div v-if="isEditing" class="unit-detail placeholder-detail">
        <h4>{{ t('buildingDetail.sidebarPlaceholderTitle') }}</h4>
        <p class="unit-desc">
          {{ t('buildingDetail.sidebarPlaceholderBodyEditing') }}
        </p>
        <div class="unit-insight-card placeholder-summary-card">
          <h5>{{ t('buildingDetail.costSummaryTitle') }}</h5>
          <div class="unit-stats">
            <span class="stat">{{ t('buildingDetail.totalBuildCost', { cost: formatCurrency(draftConstructionCost) }) }}</span>
            <span v-if="projectedCompanyCashAfterApply != null" class="stat">
              {{ t('buildingDetail.cashAfterApply', { cash: formatCurrency(projectedCompanyCashAfterApply) }) }}
            </span>
          </div>
        </div>

        <div class="unit-insight-card building-bank-account-card">
          <h5>{{ t('buildingBankAccount.assignmentTitle') }}</h5>
          <BuildingBankAccountPanel :building-id="building?.id ?? ''" :company-id="building?.companyId ?? ''" :currency-code="cityCurrencyCode" :loading="loading" @updated="loadBuilding" />
        </div>

        <!-- ── Building Layouts panel ── -->
        <div class="layout-section" :aria-label="t('buildingDetail.accessibility.buildingLayouts')">
          <div class="layout-header">
            <h4>{{ t('buildingDetail.layouts.title') }}</h4>
          </div>

          <!-- Overwrite confirmation dialog -->
          <div v-if="overwriteConfirmPending" class="layout-overwrite-confirm" role="alertdialog" aria-modal="true">
            <p class="layout-confirm-title">{{ overwriteConfirmPending.name }}</p>
            <p class="layout-confirm-summary">{{ layoutStructureSummary(overwriteConfirmPending) }}</p>
            <p class="layout-confirm-text">{{ t('buildingDetail.layouts.overwriteConfirm') }}</p>
            <div class="layout-confirm-actions">
              <button class="btn btn-danger btn-sm" @click="confirmOverwrite">{{ t('common.confirm') }}</button>
              <button class="btn btn-ghost btn-sm" @click="cancelOverwrite">{{ t('common.cancel') }}</button>
            </div>
          </div>

          <!-- Save form -->
          <div class="layout-save" v-if="!overwriteConfirmPending">
            <input type="text" class="form-input" v-model="layoutName" :placeholder="t('buildingDetail.layouts.namePlaceholder')" :aria-label="t('buildingDetail.layouts.namePlaceholder')" />
            <input
              type="text"
              class="form-input layout-desc-input"
              v-model="layoutDescription"
              :placeholder="t('buildingDetail.layouts.descriptionPlaceholder')"
              :aria-label="t('buildingDetail.layouts.descriptionPlaceholder')"
            />
            <button class="btn btn-secondary btn-sm" :disabled="!layoutName.trim() || layoutSaving" @click="saveLayout">
              {{ layoutSaving ? t('buildingDetail.layouts.masterSaving') : t('buildingDetail.layouts.save') }}
            </button>
            <p v-if="layoutSaveSuccess" class="layout-save-success">✓ {{ t('buildingDetail.layouts.saveSuccess') }}</p>
            <p v-if="layoutSaveError" class="layout-save-error">{{ layoutSaveError }}</p>
            <p v-if="layoutDeleteError" class="layout-save-error">{{ layoutDeleteError }}</p>
          </div>

          <!-- Cloud layouts section -->
          <template v-if="masterConnected">
            <div class="layout-cloud-header">
              <span class="layout-cloud-badge">☁ {{ t('buildingDetail.layouts.cloudBadge') }}</span>
              <span class="layout-connected-email">{{ t('buildingDetail.layouts.masterConnected', { email: masterUserEmail }) }}</span>
            </div>
            <p v-if="masterLayoutsLoading" class="layout-empty">{{ t('common.loading') }}</p>
            <p v-else-if="masterLayoutsError" class="layout-save-error">{{ masterLayoutsError }}</p>
            <div v-else-if="masterLayouts.length > 0" class="layout-list">
              <div v-for="layout in masterLayouts" :key="layout.id ?? layout.name" class="layout-item">
                <!-- Mini 4×4 grid preview -->
                <div class="layout-mini-grid" aria-hidden="true">
                  <template v-for="row in 4" :key="row">
                    <template v-for="col in 4" :key="`${row}-${col}`">
                      <div
                        class="layout-mini-cell"
                        :class="{ 'layout-mini-cell-occupied': getLayoutCellType(layout, col - 1, row - 1) !== null }"
                        :style="getLayoutCellType(layout, col - 1, row - 1) ? { background: getUnitColor(getLayoutCellType(layout, col - 1, row - 1)!) } : {}"
                        :title="getLayoutCellType(layout, col - 1, row - 1) ? t(`buildingDetail.unitTypes.${getLayoutCellType(layout, col - 1, row - 1)}`) : ''"
                      />
                    </template>
                  </template>
                </div>
                <div class="layout-item-info">
                  <span class="layout-name">{{ layout.name }}</span>
                  <span v-if="layout.description" class="layout-desc">{{ layout.description }}</span>
                  <span class="layout-meta">{{ layoutStructureSummary(layout) }}</span>
                </div>
                <div class="layout-item-actions">
                  <button class="btn btn-ghost btn-sm" @click="requestLoadLayout(layout)">{{ t('buildingDetail.layouts.load') }}</button>
                  <button class="btn btn-ghost btn-sm" @click="deleteLayout(layout)">{{ t('buildingDetail.layouts.delete') }}</button>
                </div>
              </div>
            </div>
            <p v-else class="layout-empty">{{ t('buildingDetail.layouts.empty') }}</p>
            <p class="layout-sync-hint">{{ t('buildingDetail.layouts.cloudSyncHint') }}</p>
          </template>

          <!-- Shared-session sign-in prompt -->
          <template v-else>
            <div class="layout-master-connect">
              <p class="layout-connect-body">{{ t('buildingDetail.layouts.masterConnectBody') }}</p>
              <button class="btn btn-secondary btn-sm" @click="router.push('/login')">
                {{ t('buildingDetail.layouts.masterConnect') }}
              </button>
            </div>

            <!-- Local-only fallback -->
            <div class="layout-local-section">
              <div class="layout-local-header">
                <span class="layout-local-badge">{{ t('buildingDetail.layouts.localBadge') }}</span>
              </div>
              <div v-if="localLayouts.length > 0" class="layout-list">
                <div v-for="layout in localLayouts" :key="layout.name" class="layout-item">
                  <!-- Mini 4×4 grid preview -->
                  <div class="layout-mini-grid" aria-hidden="true">
                    <template v-for="row in 4" :key="row">
                      <template v-for="col in 4" :key="`${row}-${col}`">
                        <div
                          class="layout-mini-cell"
                          :class="{ 'layout-mini-cell-occupied': getLayoutCellType(layout, col - 1, row - 1) !== null }"
                          :style="getLayoutCellType(layout, col - 1, row - 1) ? { background: getUnitColor(getLayoutCellType(layout, col - 1, row - 1)!) } : {}"
                          :title="getLayoutCellType(layout, col - 1, row - 1) ? t(`buildingDetail.unitTypes.${getLayoutCellType(layout, col - 1, row - 1)}`) : ''"
                        />
                      </template>
                    </template>
                  </div>
                  <div class="layout-item-info">
                    <span class="layout-name">{{ layout.name }}</span>
                    <span v-if="layout.description" class="layout-desc">{{ layout.description }}</span>
                    <span class="layout-meta">{{ layoutStructureSummary(layout) }}</span>
                  </div>
                  <div class="layout-item-actions">
                    <button class="btn btn-ghost btn-sm" @click="requestLoadLayout(layout)">{{ t('buildingDetail.layouts.load') }}</button>
                    <button class="btn btn-ghost btn-sm" @click="deleteLayout(layout)">{{ t('buildingDetail.layouts.delete') }}</button>
                  </div>
                </div>
              </div>
              <p v-else class="layout-empty">{{ t('buildingDetail.layouts.empty') }}</p>
            </div>
          </template>
        </div>
        <!-- ── End Building Layouts panel ── -->
      </div>
      <div v-else class="unit-detail building-overview-detail">
        <p class="building-overview-name">{{ building?.name }}</p>
        <p class="unit-desc">{{ t('buildingDetail.overview.subtitle', { type: formatBuildingType(building?.type ?? '') }) }}</p>

        <div class="unit-insight-card building-financial-card">
          <h5>{{ t('buildingDetail.overview.statsTitle') }}</h5>
          <p
            v-if="buildingFinancialTimeline"
            class="config-help"
            :title="t('buildingDetail.overview.tickWindow', { start: buildingFinancialTimeline.dataFromTick, end: buildingFinancialTimeline.dataToTick })"
          >
            {{ formatGameTickTime(buildingFinancialTimeline.dataFromTick, locale) }} – {{ formatGameTickTime(buildingFinancialTimeline.dataToTick, locale) }}
          </p>
          <p v-else-if="buildingFinancialTimelineLoading" class="config-help">{{ t('common.loading') }}</p>

          <div class="mi-summary-grid">
            <div class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.overview.sales') }}</span>
              <strong class="mi-metric-value">{{ formatCurrency(buildingFinancialTimeline?.totalSales ?? 0) }}</strong>
            </div>
            <div class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.overview.costs') }}</span>
              <strong class="mi-metric-value">{{ formatCurrency(buildingFinancialTimeline?.totalCosts ?? 0) }}</strong>
            </div>
            <div class="mi-metric">
              <span class="mi-metric-label">{{ t('buildingDetail.overview.profit') }}</span>
              <strong
                class="mi-metric-value"
                :class="{
                  'building-profit-positive-text': (buildingFinancialTimeline?.totalProfit ?? 0) >= 0,
                  'building-profit-negative-text': (buildingFinancialTimeline?.totalProfit ?? 0) < 0,
                }"
              >
                {{ formatCurrency(buildingFinancialTimeline?.totalProfit ?? 0) }}
              </strong>
            </div>
          </div>

          <template v-if="buildingFinancialTimeline">
            <BuildingFinancialTimelineChart v-if="buildingFinancialHasActivity" :timeline="buildingFinancialSnapshots" :currency-code="cityCurrencyCode" />

            <p v-else class="mi-empty-state">
              {{ t('buildingDetail.overview.noFinancialData') }}
            </p>
          </template>

          <p v-else-if="!buildingFinancialTimelineLoading" class="mi-empty-state">
            {{ t('buildingDetail.overview.loadFailed') }}
          </p>
        </div>

        <div class="unit-insight-card building-location-card">
          <h5>{{ t('buildingDetail.overview.locationTitle') }}</h5>
          <div class="building-overview-location-grid">
            <div class="building-overview-location-row">
              <span class="building-overview-label">{{ t('buildingDetail.overview.city') }}</span>
              <strong>{{ buildingOverviewCityName }}</strong>
            </div>
            <div class="building-overview-location-row">
              <span class="building-overview-label">{{ t('buildingDetail.overview.gps') }}</span>
              <strong>{{ formatGpsLocation(building?.latitude, building?.longitude) }}</strong>
            </div>
          </div>
          <RouterLink v-if="buildingOverviewMapRoute" :to="buildingOverviewMapRoute" class="btn btn-secondary btn-sm building-overview-map-link">
            {{ t('buildingDetail.overview.showOnMap') }}
          </RouterLink>
        </div>

        <!-- ── Bank account panel ── -->
        <div class="unit-insight-card building-bank-account-card">
          <h5>{{ t('buildingBankAccount.panelTitle') }}</h5>
          <BuildingBankAccountPanel :building-id="building?.id ?? ''" :company-id="building?.companyId ?? ''" :currency-code="cityCurrencyCode" :loading="loading" @updated="loadBuilding" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped src="./BuildingSidebar.shared.css"></style>
<style scoped src="./BuildingSidebar.analytics.css"></style>
<style scoped src="./BuildingSidebar.exchange.css"></style>
