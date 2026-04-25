<script setup lang="ts">
import { computed, provide } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useBuildingDetail, BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import BuildingDetailHeader from '@/components/buildings/BuildingDetailHeader.vue'
import PurchaseSelectorDialog from '@/components/buildings/PurchaseSelectorDialog.vue'
import BuildingPropertyPanel from '@/components/buildings/BuildingPropertyPanel.vue'
import BuildingMediaHousePanel from '@/components/buildings/BuildingMediaHousePanel.vue'
import BuildingPowerPlantPanel from '@/components/buildings/BuildingPowerPlantPanel.vue'
import BuildingResearchPanel from '@/components/buildings/BuildingResearchPanel.vue'
import BuildingUnitGrid from '@/components/buildings/BuildingUnitGrid.vue'
import BuildingEditingSidebar from '@/components/buildings/BuildingEditingSidebar.vue'
import BuildingReadonlySidebar from '@/components/buildings/BuildingReadonlySidebar.vue'
import BuildingOverviewSidebar from '@/components/buildings/BuildingOverviewSidebar.vue'

const { t, locale } = useI18n()
const router = useRouter()

const bd = useBuildingDetail()
provide(BUILDING_DETAIL_KEY, bd)

const {
  building,
  loading,
  error,
  configWarnings,
  showStarterSetupBanner,
  showSalesShopStarterBanner,
  isUpgradeInProgress,
  cancellingPlan,
  cancelPlanError,
  allUnitsUnderUpgrade,
  lockedConfiguredProducts,
  lockedConfiguredProductNames,
  pendingConfiguration,
  remainingUpgradeTicks,
  isEditing,
  formatTickDuration,
  formatGameTickTime,
  applyStarterLayout,
  applyShopStarterLayout,
  cancelPlan,
} = bd

const showEditingSidebar = computed(() => Boolean(bd.selectedCell.value && bd.isEditing.value))
const showReadonlySidebar = computed(() => {
  const selectedCell = bd.selectedCell.value
  if (!selectedCell || bd.isEditing.value) {
    return false
  }

  return Boolean(bd.getUnitAtFrom(bd.activeUnits.value, selectedCell.x, selectedCell.y))
})
const showOverviewSidebar = computed(() => !showEditingSidebar.value && !showReadonlySidebar.value)
</script>

<template>
  <div class="building-detail-view container">
    <div class="page-nav">
      <RouterLink to="/dashboard" class="back-link"> <span>←</span> {{ t('buildingDetail.backToDashboard') }} </RouterLink>
    </div>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="router.push('/dashboard')">{{ t('buildingDetail.backToDashboard') }}</button>
    </div>

    <template v-else-if="building">
      <BuildingDetailHeader />
      <PurchaseSelectorDialog />
      <BuildingPropertyPanel v-if="building.type === 'APARTMENT' || building.type === 'COMMERCIAL'" />
      <BuildingMediaHousePanel v-if="building.type === 'MEDIA_HOUSE'" />

      <div v-if="configWarnings.length > 0" class="config-warnings" role="alert">
        <strong>{{ t('buildingDetail.warnings.title') }}</strong>
        <ul>
          <li v-for="(warning, i) in configWarnings" :key="i">
            {{ t(warning.key, warning.params || {}) }}
          </li>
        </ul>
      </div>

      <BuildingPowerPlantPanel v-if="building.type === 'POWER_PLANT'" />
      <BuildingResearchPanel v-if="building.type === 'RESEARCH_DEVELOPMENT'" />

      <div v-if="showStarterSetupBanner" class="starter-setup-banner" role="region" aria-label="starter setup">
        <div class="starter-setup-content">
          <h2 class="starter-setup-title">🏭 {{ t('buildingDetail.starterSetup.title') }}</h2>
          <p class="starter-setup-body">{{ t('buildingDetail.starterSetup.body') }}</p>
          <p class="starter-setup-desc">{{ t('buildingDetail.starterSetup.starterLayoutDesc') }}</p>
          <p class="starter-setup-whatnext">{{ t('buildingDetail.starterSetup.whatNext') }}</p>
        </div>
        <div class="starter-setup-actions">
          <button class="btn btn-primary" @click="applyStarterLayout">
            {{ t('buildingDetail.starterSetup.applyStarter') }}
          </button>
        </div>
      </div>

      <div v-if="showSalesShopStarterBanner" class="starter-setup-banner starter-setup-banner--shop" role="region" aria-label="shop starter setup">
        <div class="starter-setup-content">
          <h2 class="starter-setup-title">🏪 {{ t('buildingDetail.shopStarterSetup.title') }}</h2>
          <p class="starter-setup-body">{{ t('buildingDetail.shopStarterSetup.body') }}</p>
          <p class="starter-setup-desc">{{ t('buildingDetail.shopStarterSetup.starterLayoutDesc') }}</p>
          <p class="starter-setup-whatnext">{{ t('buildingDetail.shopStarterSetup.whatNext') }}</p>
        </div>
        <div class="starter-setup-actions">
          <button class="btn btn-primary" @click="applyShopStarterLayout">
            {{ t('buildingDetail.shopStarterSetup.applyStarter') }}
          </button>
        </div>
      </div>

      <div v-if="isUpgradeInProgress" class="upgrade-banner" role="status">
        <div>
          <strong>{{ t('buildingDetail.upgradeQueuedTitle') }}</strong>
          <p>{{ t('buildingDetail.upgradeQueuedBody', { time: formatTickDuration(remainingUpgradeTicks, locale) }) }}</p>
        </div>
        <div class="upgrade-banner-actions">
          <div class="upgrade-pill" :title="t('buildingDetail.upgradeAppliesAt', { time: pendingConfiguration!.appliesAtTick })">
            {{ t('buildingDetail.upgradeAppliesAt', { time: formatGameTickTime(pendingConfiguration!.appliesAtTick, locale) }) }}
          </div>
          <button v-if="!isEditing" class="btn btn-danger btn-sm" :disabled="cancellingPlan" @click="cancelPlan">
            {{ cancellingPlan ? t('common.loading') : t('buildingDetail.cancelPlan') }}
          </button>
        </div>
      </div>
      <div v-if="cancelPlanError" class="error-banner" role="alert">{{ cancelPlanError }}</div>

      <div v-if="allUnitsUnderUpgrade.length > 0" class="concurrent-upgrades-panel" aria-label="Units under upgrade">
        <h4>⏳ {{ t('buildingDetail.unitUpgrade.concurrentTitle') }}</h4>
        <p class="concurrent-upgrades-help">{{ t('buildingDetail.unitUpgrade.concurrentHelp') }}</p>
        <ul class="concurrent-upgrades-list">
          <li
            v-for="u in allUnitsUnderUpgrade"
            :key="`${u.gridX}-${u.gridY}`"
            class="concurrent-upgrade-item"
            :aria-label="`${u.unitType} at (${u.gridX}, ${u.gridY}) upgrading to level ${u.toLevel}`"
          >
            <span class="concurrent-upgrade-type">{{ u.unitType }}</span>
            <span class="concurrent-upgrade-pos">({{ u.gridX }}, {{ u.gridY }})</span>
            <span class="concurrent-upgrade-arrow">→</span>
            <span class="concurrent-upgrade-level">{{ t('buildingDetail.unitUpgrade.nextLevel', { level: u.toLevel }) }}</span>
            <span class="concurrent-upgrade-ticks" :title="u.ticksRemaining + ' ticks'">{{
              t('buildingDetail.unitUpgrade.ticksRemaining', { time: formatTickDuration(u.ticksRemaining, locale) })
            }}</span>
          </li>
        </ul>
      </div>

      <div v-if="lockedConfiguredProducts.length > 0" class="pro-access-banner" role="status">
        <strong>{{ t('catalog.proLockedTitle') }}</strong>
        <p>
          {{
            t('buildingDetail.proAccessGrandfathered', {
              products: lockedConfiguredProductNames,
            })
          }}
        </p>
      </div>

      <div class="main-content">
        <BuildingUnitGrid />
        <BuildingEditingSidebar v-if="showEditingSidebar" />
        <BuildingReadonlySidebar v-else-if="showReadonlySidebar" />
        <BuildingOverviewSidebar v-if="showOverviewSidebar" />
      </div>
    </template>
  </div>
</template>

<style scoped>
.building-detail-view {
  padding: 2rem 1rem;
  max-width: 1400px;
}

.page-nav {
  margin-bottom: 1.5rem;
}

.main-content {
  display: grid;
  grid-template-columns: minmax(0, 1.05fr) minmax(360px, 0.95fr);
  gap: 2rem;
  align-items: start;
}

@media (min-width: 1320px) {
  .main-content {
    grid-template-columns: minmax(0, 1fr) minmax(420px, 1fr);
  }
}

.grid-container {
  min-width: 0; /* Allow shrinking */
}

.sidebar {
  position: sticky;
  top: 2rem;
  min-width: 0; /* Allow shrinking */
}

@media (max-width: 1024px) {
  .main-content {
    grid-template-columns: 1fr;
    gap: 1.5rem;
  }

  .sidebar {
    position: static;
    order: -1; /* Show sidebar above grid on mobile */
  }
}

@media (max-width: 768px) {
  .building-detail-view {
    padding: 1rem 0.5rem;
  }

  .main-content {
    gap: 1rem;
  }
}

.page-nav {
  margin-bottom: 1.5rem;
}

.back-link {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  text-decoration: none;
}

.back-link:hover {
  color: var(--color-primary);
  text-decoration: none;
}

.unit-detail {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.unit-config {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.unit-config-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  padding: 1rem 1.5rem;
  background: var(--color-bg);
  border-bottom: 1px solid var(--color-border);
}

.unit-config-header h3 {
  font-size: 1.125rem;
  margin: 0;
}

/* ── Unit detail tabs ────────────────────────────────────────── */
.unit-detail-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0;
  background: var(--color-bg);
  border-bottom: 1px solid var(--color-border);
  padding: 0 1rem;
  overflow-x: auto;
  scrollbar-width: none;
}

.unit-detail-tabs::-webkit-scrollbar {
  display: none;
}

.unit-tab-btn {
  flex-shrink: 0;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  padding: 0.6rem 0.85rem;
  font: inherit;
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--color-text-secondary);
  cursor: pointer;
  white-space: nowrap;
  transition:
    color 0.15s,
    border-color 0.15s;
}

.unit-tab-btn:hover {
  color: var(--color-text);
}

.unit-tab-btn--active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
  font-weight: 600;
}

/* Quick action card inside the Quick Actions tab */
.quick-action-current-price {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.unit-detail {
  padding: 1.5rem;
}

.unit-detail h4 {
  font-size: 1rem;
  margin: 0 0 0.35rem;
}

@media (min-width: 1025px) {
  .unit-detail {
    border: none;
    border-radius: 0;
    padding: 0;
    margin-bottom: 0;
    background: transparent;
  }

  .unit-config {
    border: none;
    border-radius: 0;
    background: transparent;
  }

  .unit-config-header {
    background: transparent;
    border-bottom: none;
    padding: 0 0 1rem 0;
  }
}

.building-title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.building-title h1 {
  font-size: 1.5rem;
}

.building-type-badge {
  background: var(--color-primary);
  color: #fff;
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
}

.building-meta,
.upgrade-summary,
.grid-legend,
.unit-stats,
.unit-links,
.unit-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.upgrade-summary {
  margin-top: 1rem;
  margin-bottom: 0.5rem;
}

/* Link changes summary panel shown before submission */
.link-changes-summary {
  margin-top: 0.75rem;
  padding: 0.75rem 1rem;
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 8px);
}

.link-changes-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin: 0 0 0.5rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.link-changes-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.link-change-item {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.link-change-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  font-size: 0.75rem;
  font-weight: 700;
  flex-shrink: 0;
}

.link-change-added .link-change-badge {
  background: rgba(0, 200, 83, 0.12);
  color: var(--color-secondary, #00c853);
}

.link-change-removed .link-change-badge {
  background: rgba(220, 38, 38, 0.12);
  color: var(--color-danger, #dc2626);
}

/* Unit-level planned changes summary panel */
.unit-changes-summary {
  margin-top: 0.75rem;
  padding: 0.75rem 1rem;
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 8px);
}

.unit-changes-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin: 0 0 0.5rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.unit-changes-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.unit-change-item {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.unit-change-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  font-size: 0.75rem;
  font-weight: 700;
  flex-shrink: 0;
}

.unit-change-added .unit-change-badge {
  background: rgba(0, 200, 83, 0.12);
  color: var(--color-secondary, #00c853);
}

.unit-change-removed .unit-change-badge {
  background: rgba(220, 38, 38, 0.12);
  color: var(--color-danger, #dc2626);
}

.unit-change-replaced .unit-change-badge {
  background: rgba(234, 179, 8, 0.12);
  color: var(--color-warning, #ca8a04);
}

.unit-change-description {
  flex: 1;
}

.unit-change-meta {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.unit-change-ticks {
  white-space: nowrap;
}

.unit-change-cost {
  white-space: nowrap;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.meta-pill,
.upgrade-summary-pill,
.upgrade-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.35rem 0.85rem;
  background: var(--color-bg);
  border-radius: 9999px;
  font-size: 0.8125rem;
}

.meta-pill.for-sale {
  background: rgba(0, 200, 83, 0.1);
  color: var(--color-secondary);
}

.power-status-pill.power-status-powered {
  background: rgba(34, 197, 94, 0.1);
  color: var(--color-secondary);
}

.power-status-pill.power-status-constrained {
  background: rgba(251, 191, 36, 0.15);
  color: #f59e0b;
}

.power-status-pill.power-status-offline {
  background: rgba(248, 113, 113, 0.1);
  color: var(--color-danger);
}

.meta-label,
.section-subtitle,
.stat,
.link-label,
.picker-subtitle,
.legend-item,
.unit-desc,
.loading {
  color: var(--color-text-secondary);
}

.meta-label {
  font-size: 0.75rem;
}

.meta-value {
  font-weight: 600;
}

.upgrade-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem 1.25rem;
  margin-top: 1rem;
  margin-bottom: 1.5rem;
  background: linear-gradient(135deg, rgba(19, 127, 236, 0.09), rgba(0, 200, 83, 0.08));
  border: 1px solid rgba(19, 127, 236, 0.18);
  border-radius: var(--radius-lg, 12px);
}

.upgrade-banner-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-shrink: 0;
}

.error-banner {
  padding: 0.75rem 1.25rem;
  margin-bottom: 1rem;
  border: 1px solid rgba(220, 38, 38, 0.3);
  border-radius: var(--radius-lg);
  background: rgba(220, 38, 38, 0.08);
  color: #dc2626;
  font-size: 0.875rem;
}

.save-error-banner {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
  border: 1px solid rgba(220, 38, 38, 0.35);
  border-radius: var(--radius-lg);
  background: rgba(220, 38, 38, 0.08);
  color: #dc2626;
  font-size: 0.875rem;
  line-height: 1.4;
}

.pro-access-banner {
  margin-bottom: 1.5rem;
  padding: 1rem 1.25rem;
  border: 1px solid rgba(255, 109, 0, 0.3);
  border-radius: var(--radius-lg);
  background: rgba(255, 109, 0, 0.08);
}

.starter-setup-banner {
  margin-bottom: 1.5rem;
  padding: 1.25rem;
  border: 1px solid rgba(0, 200, 83, 0.3);
  border-radius: var(--radius-lg);
  background: linear-gradient(135deg, rgba(0, 200, 83, 0.07), rgba(19, 127, 236, 0.05));
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.starter-setup-content {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.starter-setup-title {
  font-size: 1.0625rem;
  font-weight: 700;
  color: var(--color-secondary);
  margin: 0;
}

.starter-setup-body {
  font-size: 0.9375rem;
  margin: 0;
}

.starter-setup-desc {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  padding: 0.5rem 0.75rem;
  border-left: 3px solid rgba(0, 200, 83, 0.4);
  margin: 0;
  background: rgba(0, 200, 83, 0.04);
  border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
}

.starter-setup-whatnext {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
  margin: 0;
}

.starter-setup-actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.upgrade-banner p {
  margin: 0.35rem 0 0;
  font-size: 0.875rem;
}

.pro-access-banner p {
  margin: 0.35rem 0 0;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

/* ── Production chain status panel ─────────────────────────────────────── */

.production-chain-panel {
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 1.25rem 1.5rem;
  margin-top: 1.25rem;
  margin-bottom: 1.5rem;
}

.chain-panel-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
  flex-wrap: wrap;
}

.chain-panel-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.chain-status-badge {
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
}

.chain-status-badge--complete {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}

.chain-status-badge--incomplete {
  background: rgba(251, 191, 36, 0.15);
  color: #fbbf24;
}

.chain-panel-dismiss {
  margin-left: auto;
  background: none;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm, 4px);
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 0.75rem;
  padding: 0.2rem 0.6rem;
  line-height: 1.4;
  transition:
    background 0.15s,
    color 0.15s;
}

.chain-panel-dismiss:hover {
  background: var(--color-surface-muted);
  color: var(--color-text-primary);
}

.chain-flow {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 1rem;
}

.chain-step {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 110px;
  padding: 0.75rem;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  text-align: center;
  gap: 0.25rem;
}

.chain-step--configured {
  border-color: #34d399;
  background: rgba(52, 211, 153, 0.1);
}

.chain-step--missing {
  border-color: #fbbf24;
  background: rgba(251, 191, 36, 0.1);
}

.chain-step-icon {
  font-size: 1.5rem;
  line-height: 1;
}

.chain-step-type {
  font-size: 0.7rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.chain-step-value {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text);
  word-break: break-word;
}

.chain-step-missing-label {
  font-size: 0.8rem;
  color: var(--color-text-tertiary, var(--color-text-secondary));
  font-style: italic;
}

.chain-arrow {
  font-size: 1.5rem;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.chain-guidance {
  border-top: 1px solid var(--color-border);
  padding-top: 1rem;
  margin-top: 0.5rem;
}

.chain-guidance-title {
  margin: 0 0 0.5rem;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.chain-todo {
  margin: 0 0 0.75rem;
  padding-left: 1.25rem;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

.chain-todo li {
  margin-bottom: 0.3rem;
}

.chain-action-hint {
  margin: 0;
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  font-style: italic;
}

.chain-complete-message {
  border-top: 1px solid var(--color-border);
  padding-top: 1rem;
  margin-top: 0.5rem;
}

.chain-complete-message p {
  margin: 0 0 0.4rem;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

.chain-next-step {
  font-weight: 600;
  color: var(--color-text-primary) !important;
}

.grid-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.grid-actions {
  display: flex;
  gap: 0.75rem;
}

.grid-header h2 {
  font-size: 1.125rem;
  margin: 0;
}

.section-subtitle {
  margin: 0.35rem 0 0;
  font-size: 0.875rem;
}

.unit-grid {
  display: grid;
  gap: 0.6rem;
  margin-bottom: 1rem;
}

.grid-row {
  display: grid;
  grid-template-columns: minmax(80px, 1fr) 40px minmax(80px, 1fr) 40px minmax(80px, 1fr) 40px minmax(80px, 1fr);
  align-items: center;
  justify-items: center;
}

.connector-row {
  min-height: 32px;
}

.grid-cell {
  aspect-ratio: 1;
  width: 100%;
  min-height: 96px;
  display: flex;
  flex-direction: column;
  align-items: stretch;
  justify-content: flex-start;
  gap: 0.35rem;
  padding: 0.5rem;
  border: 2px solid var(--color-border);
  border-radius: 12px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.76), rgba(244, 247, 251, 0.92));
  color: var(--color-text);
  transition:
    border-color 0.15s ease,
    box-shadow 0.15s ease,
    transform 0.15s ease;
}

.cell-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.5rem;
}

.grid-cell:not(:disabled):hover {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 4px rgba(19, 127, 236, 0.1);
}

.grid-cell.selected {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 4px rgba(19, 127, 236, 0.12);
}

.grid-cell.readonly,
.readonly-grid .grid-cell {
  cursor: default;
}

.grid-cell.changed {
  box-shadow: inset 0 0 0 2px rgba(19, 127, 236, 0.14);
}

.cell-type {
  font-size: 0.6875rem;
  font-weight: 700;
  text-align: left;
  line-height: 1.2;
}

.cell-item-block {
  display: grid;
  grid-template-columns: 28px minmax(0, 1fr);
  gap: 0.45rem;
  align-items: center;
}

.cell-item-image,
.cell-item-avatar {
  width: 28px;
  height: 28px;
  border-radius: 8px;
}

.cell-item-image {
  object-fit: cover;
  border: 1px solid color-mix(in srgb, var(--color-border) 80%, transparent);
}

.cell-item-avatar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 0.6875rem;
  font-weight: 800;
  background: color-mix(in srgb, var(--color-primary) 10%, white 90%);
  color: var(--color-primary);
}

.cell-item-copy {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.cell-item,
.cell-metric,
.cell-value,
.cell-operating-cost,
.cell-stock {
  font-size: 0.5625rem;
  text-align: left;
  line-height: 1.2;
  color: var(--color-text-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

.cell-item {
  font-weight: 600;
  color: var(--color-text);
}

.cell-stock,
.cell-value {
  font-weight: 600;
}

.cell-operating-cost {
  opacity: 0.75;
}

.cell-level,
.cell-pending,
.cell-reverting {
  font-size: 0.625rem;
  flex-shrink: 0;
}

.cell-pending {
  color: #f59e0b;
  text-align: center;
}

.cell-reverting {
  color: #c084fc;
  text-align: center;
  font-style: italic;
}

.grid-cell.reverting {
  border-style: dashed !important;
  opacity: 0.85;
}

.cell-inventory-indicator {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  margin-top: 0.25rem;
}

.inventory-icon {
  font-size: 1rem;
}

.cell-empty {
  font-size: 1.35rem;
  opacity: 0.45;
  margin: auto;
}

.cell-capacity {
  position: relative;
  width: 100%;
  height: 0.5rem;
  border-radius: 999px;
  overflow: hidden;
  background: color-mix(in srgb, var(--color-border) 75%, transparent);
}

.cell-capacity-fill {
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--color-primary), #38bdf8);
  transition: background 0.2s ease;
}

.cell-capacity-fill[data-fill='medium'] {
  background: linear-gradient(90deg, #3b82f6, #38bdf8);
}

.cell-capacity-fill[data-fill='high'] {
  background: linear-gradient(90deg, #f59e0b, #ef4444);
}

.cell-capacity-inflow {
  position: absolute;
  top: 0;
  height: 100%;
  background: #22c55e;
  border-radius: inherit;
  animation: flow-in-pulse 2s ease-in-out infinite;
}

.cell-capacity-outflow {
  position: absolute;
  top: 0;
  height: 100%;
  background: rgba(245, 158, 11, 0.75);
  border-radius: inherit;
}

.cell-capacity-sold {
  position: absolute;
  top: 0;
  height: 100%;
  background: linear-gradient(90deg, #10b981, #34d399);
  border-radius: inherit;
  animation: sold-sweep 1.8s ease-in-out infinite;
}

@keyframes sold-sweep {
  0%,
  100% {
    opacity: 0.9;
  }
  50% {
    opacity: 0.45;
  }
}

.cell-flow-labels {
  display: flex;
  gap: 0.3rem;
  flex-wrap: nowrap;
  overflow: hidden;
  margin-top: auto;
}

.cell-flow-in,
.cell-flow-out,
.cell-flow-sold {
  font-size: 0.5rem;
  font-weight: 700;
  line-height: 1;
  padding: 0.1rem 0.25rem;
  border-radius: 999px;
  white-space: nowrap;
}

.cell-flow-in {
  background: color-mix(in srgb, #22c55e 15%, transparent);
  color: #16a34a;
}

.cell-flow-out {
  background: color-mix(in srgb, #f59e0b 15%, transparent);
  color: #d97706;
}

.cell-flow-sold {
  background: color-mix(in srgb, #10b981 15%, transparent);
  color: #059669;
}

.link-toggle {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 94%, white 6%);
  transition:
    border-color 0.15s ease,
    background 0.15s ease,
    box-shadow 0.15s ease;
}

/* Exclude DiagonalConnector hit-area buttons from cursor and hover rules —
   the connector's scoped CSS handles its own interaction states. */
.link-toggle:not(.diag-hit-area):disabled,
.link-toggle:not(.diag-hit-area).readonly {
  cursor: default;
}

.link-toggle:not(.diag-hit-area):not(:disabled):not(.readonly):hover {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(19, 127, 236, 0.12);
}

.link-toggle.disabled {
  opacity: 0.28;
}

.link-toggle.horizontal {
  width: 32px;
  height: 14px;
  border-radius: 999px;
}

.link-toggle.vertical {
  width: 14px;
  height: 32px;
  border-radius: 999px;
}

.link-line {
  display: block;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-border) 82%, transparent);
}

.horizontal .link-line {
  width: 20px;
  height: 4px;
}

.vertical .link-line {
  width: 4px;
  height: 20px;
}

.link-toggle.active .link-line {
  background: var(--color-primary);
}

/* Directional arrow indicator inside link toggle buttons */
.link-arrow {
  position: absolute;
  font-size: 12px;
  line-height: 1;
  color: var(--color-primary);
  pointer-events: none;
  font-weight: 700;
  text-shadow: 0 1px 2px rgba(8, 15, 28, 0.65);
}

.link-toggle.horizontal .link-arrow {
  right: 1px;
  top: 50%;
  transform: translateY(-50%);
}

.link-toggle.horizontal.link-state-backward .link-arrow {
  right: auto;
  left: 1px;
}

.link-toggle.horizontal.link-state-both .link-arrow {
  right: auto;
  left: 50%;
  transform: translate(-50%, -50%);
}

.link-toggle.vertical .link-arrow {
  bottom: 1px;
  left: 50%;
  transform: translateX(-50%);
}

.link-toggle.vertical.link-state-backward .link-arrow {
  bottom: auto;
  top: 1px;
}

.link-toggle.vertical.link-state-both .link-arrow {
  top: 50%;
  bottom: auto;
  transform: translate(-50%, -50%);
}

/* config-help-notice is a softer variant of config-help for guidance messages */
.config-help-notice {
  color: var(--color-text-muted, #6b7280);
  font-style: italic;
}

.readonly-grid .link-toggle.active,
.link-toggle.readonly.active {
  background: rgba(19, 127, 236, 0.08);
}

.unit-picker-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.58);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 200;
}

.unit-picker {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  width: 100%;
  max-width: 320px;
}

@media (min-width: 1025px) {
  .unit-picker-overlay {
    display: none;
  }

  .unit-picker {
    border: none;
    border-radius: 0;
    padding: 0;
    width: 100%;
    max-width: none;
    background: transparent;
  }
}

.picker-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.picker-header h3,
.unit-detail h3 {
  font-size: 1.125rem;
  margin: 0;
}

.picker-grid {
  display: grid;
  gap: 0.5rem;
}

.picker-option {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.8rem;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  color: var(--color-text);
  text-align: left;
  transition:
    border-color 0.15s ease,
    background 0.15s ease;
}

.picker-option:hover {
  border-color: var(--color-primary);
  background: rgba(0, 71, 255, 0.04);
}

.picker-color,
.legend-color {
  flex-shrink: 0;
  border-radius: 4px;
}

.picker-color {
  width: 16px;
  height: 16px;
}

.legend-color {
  width: 12px;
  height: 12px;
}

.picker-info {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.picker-name {
  font-weight: 600;
  font-size: 0.875rem;
}

.picker-desc {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.picker-cost {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--color-text);
}

.unit-desc {
  margin: 0.35rem 0 0.85rem;
  font-size: 0.8125rem;
}

.link-badge {
  background: rgba(0, 71, 255, 0.08);
  color: var(--color-primary);
  padding: 0.15rem 0.55rem;
  border-radius: 9999px;
  font-size: 0.6875rem;
  font-weight: 600;
}

.btn-sm {
  padding: 0.375rem 0.75rem;
  font-size: 0.8125rem;
}

.error-message {
  background: rgba(248, 113, 113, 0.1);
  color: var(--color-danger);
  padding: 1rem;
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  gap: 1rem;
}

.loading {
  text-align: center;
  padding: 3rem;
}

@media (max-width: 920px) {
  .building-detail-view {
    max-width: 100%;
  }

  .grid-header {
    flex-direction: column;
    align-items: stretch;
  }

  .grid-actions {
    width: 100%;
  }
}

@media (max-width: 720px) {
  .grid-row {
    grid-template-columns: minmax(62px, 1fr) 30px minmax(62px, 1fr) 30px minmax(62px, 1fr) 30px minmax(62px, 1fr);
  }

  .grid-cell {
    min-height: 68px;
    padding: 0.35rem;
  }

  .link-toggle.horizontal {
    width: 26px;
  }

  .link-toggle.vertical {
    height: 26px;
  }

  .upgrade-banner,
  .grid-header {
    flex-direction: column;
    align-items: flex-start;
  }
}

/* Sale dialog */
.sale-dialog {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.sale-dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.sale-dialog-header h3 {
  font-size: 1.125rem;
  margin: 0;
}

.sale-dialog-body {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.sale-dialog-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.5rem;
}

.form-label {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.form-input {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 8px);
  background: var(--color-bg);
  color: var(--color-text);
  font-size: 0.875rem;
}

.form-input:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(0, 71, 255, 0.1);
}

/* Config warnings */
.property-panel {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.property-panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.25rem;
}

.property-panel-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0;
}

.property-metrics {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
}

.property-metric {
  background: var(--color-bg);
  border: 1px solid color-mix(in srgb, var(--color-border) 80%, transparent);
  border-radius: var(--radius-md, 8px);
  padding: 0.75rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.property-metric-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  font-weight: 500;
}

.property-metric-value {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text);
}

.property-metric-zero {
  color: var(--color-text-secondary);
}

.pending-rent-notice {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: rgba(59, 130, 246, 0.08);
  border: 1px solid rgba(59, 130, 246, 0.3);
  border-radius: var(--radius-md, 8px);
  padding: 0.75rem 1rem;
  font-size: 0.875rem;
  color: #60a5fa;
  margin-bottom: 0.75rem;
}

.pending-rent-icon {
  font-size: 1rem;
  flex-shrink: 0;
}

.property-empty-state {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  padding: 0.5rem 0;
}

.rent-dialog {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.25rem;
  margin-top: 1rem;
}

.rent-dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.rent-dialog-header h3 {
  font-size: 1rem;
  margin: 0;
}

.rent-dialog-body {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.rent-dialog-hint {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.rent-dialog-error {
  font-size: 0.8125rem;
  color: #dc2626;
  margin: 0;
}

.rent-dialog-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.25rem;
}

.config-warnings {
  background: rgba(255, 109, 0, 0.08);
  border: 1px solid rgba(255, 109, 0, 0.3);
  border-radius: var(--radius-lg);
  padding: 1rem 1.25rem;
  margin-bottom: 1.5rem;
  color: #f59e0b;
}

.config-warnings strong {
  display: block;
  margin-bottom: 0.5rem;
}

.config-warnings ul {
  margin: 0;
  padding-left: 1.25rem;
}

.config-warnings li {
  font-size: 0.875rem;
  margin-bottom: 0.25rem;
}

/* ── Media house management panel ──────────────────── */
.media-house-mgmt-panel {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.media-house-mgmt-header {
  margin-bottom: 1.25rem;
}

.media-house-mgmt-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0;
}

.media-house-metrics {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.media-house-metric {
  background: var(--color-bg);
  border: 1px solid color-mix(in srgb, var(--color-border) 80%, transparent);
  border-radius: var(--radius-md, 8px);
  padding: 0.75rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.media-house-metric-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  font-weight: 500;
}

.media-house-metric-value {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text);
}

.mh-content-value {
  color: #60a5fa;
}
.mh-budget-active {
  color: #34d399;
}
.mh-budget-none {
  color: var(--color-text-secondary);
}
.mh-efficiency {
  color: #a78bfa;
}

.media-house-section-title {
  font-size: 0.9rem;
  font-weight: 600;
  margin: 0 0 0.75rem;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.media-house-ranking-section,
.media-house-budget-section,
.media-house-effectiveness-section {
  margin-bottom: 1.5rem;
  padding-top: 1.25rem;
  border-top: 1px solid var(--color-border);
}

.media-house-competitors {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.mh-competitor-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 0.75rem;
  border-radius: var(--radius-md, 8px);
  background: var(--color-bg);
}

.mh-competitor-own {
  border: 1px solid rgba(99, 102, 241, 0.4);
  background: rgba(99, 102, 241, 0.06);
}

.mh-competitor-name {
  font-size: 0.875rem;
  font-weight: 500;
  min-width: 140px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mh-competitor-bar-wrap {
  flex: 1;
  height: 8px;
  background: var(--color-border);
  border-radius: 4px;
  overflow: hidden;
}

.mh-competitor-bar {
  height: 100%;
  border-radius: 4px;
  background: #60a5fa;
  transition: width 0.3s;
}

.mh-bar-own {
  background: #818cf8;
}
.mh-bar-gov {
  background: #4b5563;
}

.mh-competitor-pct {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  min-width: 38px;
  text-align: right;
}

.mh-competitor-you {
  font-size: 0.7rem;
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
  border-radius: 4px;
  padding: 0.1rem 0.4rem;
  font-weight: 600;
}

.media-house-ranking-hint {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.media-house-budget-hint,
.media-house-effectiveness-hint {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.75rem;
}

.media-house-budget-form {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-width: 400px;
}

.media-house-budget-preview {
  font-size: 0.8rem;
  color: #34d399;
  margin: 0;
}

.media-house-budget-error {
  font-size: 0.8rem;
  color: var(--color-error, #f87171);
  margin: 0;
}

.media-house-budget-success {
  font-size: 0.8rem;
  color: #34d399;
  margin: 0;
}

.media-house-loading {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  padding: 0.5rem 0;
}

.media-house-effectiveness-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.mh-channel-mult-label {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  min-width: 160px;
}

.mh-channel-mult-value {
  font-size: 1rem;
  font-weight: 700;
  color: var(--color-text);
}

/* Unit config fields */
.unit-config-fields {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.unit-config-fields h5 {
  font-size: 0.875rem;
  font-weight: 600;
  margin: 0 0 0.75rem;
}

.purchase-selector-trigger {
  width: 100%;
  justify-content: center;
}

.purchase-selection-summary {
  margin-top: 0.5rem;
  padding: 0.75rem;
  border-top: 1px solid var(--color-border);
  display: grid;
  gap: 0.2rem;
}

.purchase-selection-meta {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
}

.purchase-selector-page {
  position: fixed;
  inset: 0;
  z-index: 110;
  background: rgba(15, 23, 42, 0.82);
  padding: 2rem;
  overflow: auto;
}

.purchase-selector-shell {
  margin: 0 auto;
  background: var(--color-bg);
  border-radius: 18px;
  border: 1px solid var(--color-border);
  padding: 1.5rem;
  display: grid;
  gap: 1rem;
}

.purchase-selector-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
}

.purchase-selector-header h2 {
  margin: 0.2rem 0 0;
}

.purchase-selector-eyebrow {
  margin: 0;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--color-primary);
  font-weight: 700;
}

.purchase-selector-grid {
  display: grid;
  gap: 1rem;
  grid-template-columns: 1.2fr 0.8fr;
}

.purchase-selector-card {
  border: 1px solid var(--color-border);
  border-radius: 14px;
  padding: 1rem;
  background: var(--color-surface-raised);
}

.purchase-vendor-list {
  display: grid;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.purchase-vendor-card {
  width: 100%;
  text-align: left;
  display: grid;
  gap: 0.2rem;
  border-radius: 12px;
  border: 1px solid var(--color-border);
  background: var(--color-bg);
  color: var(--color-text);
  padding: 0.85rem 0.95rem;
  cursor: pointer;
  margin-top: 0.75rem;
}

.purchase-vendor-card.selected {
  border-color: var(--color-primary);
  background: rgba(37, 99, 235, 0.08);
}

.purchase-vendor-card span {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
}

.purchase-vendor-pricing {
  font-variant-numeric: tabular-nums;
}

.purchase-selector-actions {
  display: flex;
  justify-content: flex-end;
}

.config-field {
  margin-bottom: 0.75rem;
}

.config-label {
  display: block;
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin-bottom: 0.25rem;
}

.config-help {
  margin: -0.25rem 0 0;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.config-onboarding-hint {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  background: var(--color-surface);
  border-left: 3px solid #60a5fa;
  padding: 0.5rem 0.75rem;
  border-radius: 0 6px 6px 0;
  margin-bottom: 0.75rem;
  line-height: 1.4;
}

.unit-config-readonly-details {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  margin-top: 0.5rem;
}

.unit-insight-card {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.unit-insight-card h5 {
  margin: 0 0 0.75rem;
  font-size: 0.875rem;
}

.inventory-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 0.75rem;
  margin-bottom: 0.9rem;
}

.inventory-summary-stat {
  padding: 0.8rem 0.9rem;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  border-radius: var(--radius-md, 8px);
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 94%, white 6%);
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.inventory-summary-label {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.inventory-summary-stat strong {
  font-size: 0.95rem;
  color: var(--color-text);
}

.detail-capacity {
  position: relative;
  width: 100%;
  height: 0.5rem;
  border-radius: 999px;
  overflow: hidden;
  background: color-mix(in srgb, var(--color-border) 75%, transparent);
}

.detail-capacity-fill {
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--color-primary), #38bdf8);
}

.detail-capacity-inflow {
  position: absolute;
  top: 0;
  height: 100%;
  background: #22c55e;
  border-radius: inherit;
  animation: flow-in-pulse 2s ease-in-out infinite;
}

.detail-capacity-outflow {
  position: absolute;
  top: 0;
  height: 100%;
  background: rgba(245, 158, 11, 0.75);
  border-radius: inherit;
}

.exchange-offers-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  list-style: none;
  padding: 0;
  margin: 0;
}

.exchange-offer-item {
  padding: 0.75rem;
  border-radius: var(--radius-md, 8px);
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 92%, white 8%);
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
}

.exchange-offer-header {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
  font-size: 0.8125rem;
}

.exchange-offer-metrics {
  display: grid;
  gap: 0.25rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.offer-blocked {
  opacity: 0.5;
}

.offer-blocked-reason {
  margin-top: 0.35rem;
  font-size: 0.7rem;
  color: var(--color-error, #ef4444);
}

.exchange-no-valid-offers {
  color: var(--color-error, #ef4444);
  font-size: 0.8rem;
}

.exchange-selection-hint {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  font-style: italic;
  margin-bottom: 0.5rem;
}

.offer-best {
  border-color: var(--color-primary, #3b82f6);
  background: color-mix(in srgb, var(--color-primary, #3b82f6) 8%, var(--color-surface-raised, var(--color-surface)));
}

.offer-best-badge {
  font-size: 0.65rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-primary, #3b82f6);
  background: color-mix(in srgb, var(--color-primary, #3b82f6) 15%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-primary, #3b82f6) 40%, transparent);
  border-radius: var(--radius-sm, 4px);
  padding: 0.1rem 0.4rem;
  white-space: nowrap;
}

.logistics-trap-warning {
  font-size: 0.75rem;
  color: var(--color-warning-text, #92400e);
  background: var(--color-warning-bg, #fef3c7);
  border: 1px solid var(--color-warning-border, #f59e0b);
  border-radius: var(--radius-md, 8px);
  padding: 0.5rem 0.75rem;
  margin-bottom: 0.5rem;
}

.exchange-sort-controls {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
}

.exchange-sort-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.exchange-sort-btn {
  font-size: 0.7rem;
  padding: 0.1rem 0.5rem;
  border-radius: var(--radius-sm, 4px);
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition:
    background 0.15s,
    color 0.15s,
    border-color 0.15s;
}

.exchange-sort-btn.active {
  background: color-mix(in srgb, var(--color-primary, #3b82f6) 15%, transparent);
  border-color: var(--color-primary, #3b82f6);
  color: var(--color-primary, #3b82f6);
  font-weight: 600;
}

.exchange-sort-btn:hover:not(.active) {
  background: color-mix(in srgb, var(--color-border) 30%, transparent);
  color: var(--color-text);
}

.exchange-view-link {
  display: inline-block;
  margin-top: 0.5rem;
  font-size: 0.75rem;
  color: var(--color-primary, #3b82f6);
  text-decoration: none;
  font-weight: 500;
}

.exchange-view-link:hover {
  text-decoration: underline;
}

/* Inventory table */
.inventory-table {
  margin-top: 0.75rem;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  border-radius: var(--radius-md, 8px);
  overflow: hidden;
}

.inventory-table-header,
.inventory-table-row {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) 90px 90px minmax(110px, 0.9fr);
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  align-items: center;
}

.inventory-table-header {
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 95%, white 5%);
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.inventory-table-row {
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 95%, transparent);
}

.inventory-table-row:last-child {
  border-bottom: none;
}

.inventory-col-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 0;
}

.inventory-col-quantity,
.inventory-col-quality,
.inventory-col-cost {
  font-size: 0.8125rem;
}

.inventory-col-cost {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.125rem;
}

.inventory-item-stack {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.inventory-item-image {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  object-fit: cover;
  flex-shrink: 0;
}

.inventory-item-avatar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 6px;
  background: var(--color-primary);
  color: white;
  font-size: 0.875rem;
  font-weight: 700;
  flex-shrink: 0;
}

.inventory-item-name {
  font-weight: 600;
  color: var(--color-text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.inventory-item-cost {
  font-weight: 700;
  color: var(--color-text);
}

.inventory-item-secondary {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.inventory-empty {
  margin: 0;
  padding: 0.85rem 0.95rem;
  border-radius: var(--radius-md, 8px);
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 94%, white 6%);
  color: var(--color-text-secondary);
  font-size: 0.8125rem;
}

.inventory-item-quantity,
.inventory-item-quality {
  color: var(--color-text-secondary);
}

/* Layout section */
.layout-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1rem;
  margin-top: 1rem;
}

.layout-header {
  margin-bottom: 0.75rem;
}

.layout-header h4 {
  font-size: 1rem;
  margin: 0;
}

.layout-save {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.layout-desc-input {
  font-size: 0.8125rem;
}

.layout-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.layout-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 0.5rem;
  background: var(--color-bg);
  border-radius: var(--radius-md, 8px);
  gap: 0.5rem;
}

/* Mini 4×4 layout preview grid */
.layout-mini-grid {
  display: grid;
  grid-template-columns: repeat(4, 10px);
  grid-template-rows: repeat(4, 10px);
  gap: 2px;
  flex-shrink: 0;
  align-self: center;
}

.layout-mini-cell {
  width: 10px;
  height: 10px;
  border-radius: 2px;
  background: color-mix(in srgb, var(--color-border) 30%, transparent);
}

.layout-mini-cell-occupied {
  opacity: 0.85;
}

.layout-item-info {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  min-width: 0;
}

.layout-name {
  font-size: 0.8125rem;
  font-weight: 600;
}

.layout-desc {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.layout-meta {
  font-size: 0.75rem;
  color: var(--color-text-tertiary, var(--color-text-secondary));
}

.layout-item-actions {
  display: flex;
  gap: 0.25rem;
  flex-shrink: 0;
}

.layout-empty {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin: 0.5rem 0;
}

.layout-save-success {
  font-size: 0.8125rem;
  color: var(--color-success, #22c55e);
  margin: 0.25rem 0 0;
}

.layout-save-error {
  font-size: 0.8125rem;
  color: var(--color-danger, #ef4444);
  margin: 0.25rem 0 0;
}

.layout-overwrite-confirm {
  background: color-mix(in srgb, var(--color-warning, #f59e0b) 12%, transparent);
  border: 1px solid var(--color-warning, #f59e0b);
  border-radius: var(--radius-md, 8px);
  padding: 0.75rem;
  margin-bottom: 0.75rem;
}

.layout-confirm-title {
  font-size: 0.875rem;
  font-weight: 600;
  margin: 0 0 0.25rem;
}

.layout-confirm-summary {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  margin: 0 0 0.5rem;
}

.layout-confirm-text {
  font-size: 0.875rem;
  margin: 0 0 0.5rem;
}

.layout-confirm-actions {
  display: flex;
  gap: 0.5rem;
}

.layout-cloud-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}

.layout-cloud-badge {
  font-size: 0.75rem;
  font-weight: 600;
  background: color-mix(in srgb, var(--color-primary, #3b82f6) 15%, transparent);
  color: var(--color-primary, #3b82f6);
  border-radius: var(--radius-sm, 4px);
  padding: 0.125rem 0.375rem;
}

.layout-local-badge {
  font-size: 0.75rem;
  font-weight: 600;
  background: color-mix(in srgb, var(--color-border) 40%, transparent);
  color: var(--color-text-secondary);
  border-radius: var(--radius-sm, 4px);
  padding: 0.125rem 0.375rem;
}

.layout-connected-email {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
}

.layout-master-connect {
  margin-bottom: 0.75rem;
}

.layout-connect-body {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin: 0 0 0.75rem;
}

.layout-login-form {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.layout-form-toggle {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin: 0.25rem 0 0;
}

.layout-local-section {
  margin-top: 1rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--color-border);
}

.layout-local-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.layout-sync-hint {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  margin: 0.5rem 0 0;
  font-style: italic;
}

.btn-link {
  background: none;
  border: none;
  padding: 0;
  color: var(--color-primary, #3b82f6);
  font-size: inherit;
  cursor: pointer;
  text-decoration: underline;
}

.btn-xs {
  padding: 0.125rem 0.375rem;
  font-size: 0.75rem;
}

.placeholder-detail {
  min-height: 240px;
}

.building-overview-detail {
  min-height: 240px;
}

.building-overview-name {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
  color: var(--color-text);
}

.placeholder-summary-card {
  border-top-style: dashed;
}

.building-overview-location-grid {
  display: grid;
  gap: 0.75rem;
  margin-bottom: 0.9rem;
}

.building-overview-location-row {
  padding: 0.8rem 0.9rem;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  border-radius: var(--radius-md, 8px);
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 94%, white 6%);
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.building-overview-label {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.building-overview-map-link {
  width: fit-content;
}

.grid-cell.clickable {
  cursor: pointer;
}

/* R&D Research Progress Panel */
.research-progress-panel {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 8px);
  padding: 1.25rem;
  margin-bottom: 1rem;
}

/* Power plant analytics panel */
.power-plant-analytics-panel {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 8px);
  padding: 1.25rem;
  margin-bottom: 1rem;
}

.power-plant-analytics-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.75rem;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.power-plant-analytics-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0;
  color: var(--color-text);
}

.ppa-tick-window {
  margin-bottom: 0.75rem;
}

.ppa-loading {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  padding: 0.5rem 0;
}

.ppa-summary-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem;
  margin-bottom: 1rem;
}

@media (min-width: 640px) {
  .ppa-summary-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.ppa-metric {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.ppa-metric-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  cursor: default;
}

.ppa-metric-value {
  font-size: 1rem;
  font-weight: 600;
}

.ppa-income {
  color: var(--color-success, #22c55e);
}
.ppa-fine {
  color: var(--color-error, #ef4444);
}
.ppa-cost {
  color: var(--color-text-secondary);
}

.ppa-chart {
  display: flex;
  align-items: flex-end;
  gap: 1px;
  height: 56px;
  margin-bottom: 0.75rem;
  overflow: hidden;
  border-radius: 4px;
}

.ppa-bar-group {
  display: flex;
  align-items: flex-end;
  gap: 1px;
  flex: 1;
  min-width: 2px;
}

.ppa-bar {
  flex: 1;
  min-width: 1px;
  border-radius: 2px 2px 0 0;
}

.ppa-bar-income {
  background: var(--color-success, #22c55e);
  opacity: 0.8;
}

.ppa-bar-cost {
  background: var(--color-error, #ef4444);
  opacity: 0.7;
}

.ppa-empty-state {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  padding: 0.5rem 0;
}

.ppa-unit-guide {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.ppa-unit-card {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
}

.ppa-unit-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.ppa-unit-desc {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin: 0.25rem 0 0;
}

.research-progress-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.research-progress-title {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0;
  color: var(--color-text);
}

.research-progress-intro {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  margin: 0 0 1rem;
}

.research-loading {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

.research-empty-state {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  font-style: italic;
}

.research-brand-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.research-brand-card {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm, 6px);
  padding: 0.875rem;
}

.research-brand-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.25rem;
}

.research-brand-name {
  font-weight: 600;
  font-size: 0.9375rem;
  color: var(--color-text);
}

.research-brand-scope-badge {
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 0.125rem 0.375rem;
  border-radius: 9999px;
  background: var(--color-primary-subtle, rgba(0, 71, 255, 0.1));
  color: var(--color-primary);
}

.research-brand-industry {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.5rem;
}

.research-brand-metrics {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin: 0.5rem 0;
}

.research-metric {
  display: grid;
  grid-template-columns: 8rem 1fr 3.5rem;
  align-items: center;
  gap: 0.5rem;
}

.research-metric-label {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.research-metric-value {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--color-text);
  text-align: right;
}

.research-progress-bar {
  height: 0.5rem;
  background: var(--color-border);
  border-radius: 9999px;
  overflow: hidden;
}

.research-progress-fill {
  height: 100%;
  border-radius: 9999px;
  transition: width 0.3s ease;
}

.research-progress-quality {
  background: #0047ff;
}

.research-progress-awareness {
  background: #9333ea;
}

.research-progress-efficiency {
  background: #16a34a;
}

.research-brand-effect {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin: 0.5rem 0 0;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

/* Research budget panel (cumulative budget model) */
.research-budget-panel {
  background: var(--color-bg-subtle, rgba(0, 0, 0, 0.04));
  border-radius: var(--radius-sm, 6px);
  padding: 0.625rem 0.75rem;
  margin-top: 0.5rem;
}

.research-budget-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 0.125rem 0;
}

.research-budget-row--competitor {
  border-top: 1px solid var(--color-border);
  margin-top: 0.25rem;
  padding-top: 0.375rem;
}

.research-budget-label {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.research-budget-value {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.research-budget-value--warn {
  color: var(--color-warning, #d97706);
}

.research-budget-hint {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  font-style: italic;
  margin: 0.375rem 0 0;
}

/* ── Market Intelligence panel ── */
.market-intelligence-panel {
  margin-top: 1.25rem;
}

.unit-product-analytics-panel {
  margin-top: 1.25rem;
}

/* Product context row: chip + tick window label */
.mi-context-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
  flex-wrap: wrap;
}

.mi-product-chip {
  display: inline-block;
  background: var(--color-primary-light, rgba(59, 130, 246, 0.12));
  color: var(--color-primary, #3b82f6);
  border: 1px solid var(--color-primary-light, rgba(59, 130, 246, 0.3));
  border-radius: 999px;
  padding: 0.15rem 0.65rem;
  font-size: 0.78rem;
  font-weight: 700;
  letter-spacing: 0.02em;
}

.mi-tick-window {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

/* Trend direction colours */
.mi-trend-up {
  color: #4ade80;
}

.mi-trend-down {
  color: #f87171;
}

.mi-trend-flat {
  color: var(--color-text-secondary);
}

.mi-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(110px, 1fr));
  gap: 0.6rem;
  margin: 0.75rem 0 1rem;
}

.mi-metric {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.55rem 0.7rem;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.mi-metric-label {
  font-size: 0.7rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.mi-metric-value {
  font-size: 0.9375rem;
  color: var(--color-text);
}

.mi-empty-state {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  background: var(--color-surface);
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-md);
  padding: 0.75rem;
  margin: 0.5rem 0 0.75rem;
}

.building-profit-positive-text {
  color: #4ade80;
}

.building-profit-negative-text {
  color: #f87171;
}

@media (max-width: 640px) {
  .building-overview-map-link {
    width: 100%;
    justify-content: center;
  }
}

.mi-section {
  margin-bottom: 1rem;
}

.mi-market-share {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  margin-top: 0.4rem;
}

.mi-share-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.8125rem;
}

.mi-share-row-you .mi-share-label {
  font-weight: 600;
  color: var(--color-primary, #0047ff);
}

.mi-share-label {
  width: 6.5rem;
  flex-shrink: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-text);
}

.mi-share-bar-wrap {
  flex: 1;
  height: 10px;
  background: var(--color-border);
  border-radius: 9999px;
  overflow: hidden;
}

.mi-share-bar {
  height: 100%;
  background: var(--color-primary, #0047ff);
  border-radius: 9999px;
  transition: width 0.3s ease;
}

.mi-share-row-you .mi-share-bar {
  background: #16a34a;
}

.mi-share-pct {
  width: 3rem;
  text-align: right;
  font-variant-numeric: tabular-nums;
  flex-shrink: 0;
  color: var(--color-text-secondary);
}

.mi-demand-card {
  margin-top: 0.75rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  padding: 0.75rem;
}

.mi-demand-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.35rem;
}

.mi-demand-title {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.mi-demand-badge {
  font-size: 0.75rem;
  font-weight: 700;
  padding: 0.2rem 0.55rem;
  border-radius: 9999px;
}

/* Demand signal color themes */
.mi-demand-no-data .mi-demand-badge {
  background: var(--color-border);
  color: var(--color-text-secondary);
}

.mi-demand-strong .mi-demand-badge {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}

.mi-demand-moderate .mi-demand-badge {
  background: rgba(96, 165, 250, 0.15);
  color: #60a5fa;
}

.mi-demand-weak .mi-demand-badge {
  background: rgba(251, 191, 36, 0.15);
  color: #fbbf24;
}

.mi-demand-supply-constrained .mi-demand-badge {
  background: rgba(248, 113, 113, 0.15);
  color: #f87171;
}

.mi-share-row-unmet .mi-share-label {
  color: var(--color-text-secondary);
  font-style: italic;
}

.mi-share-bar-unmet {
  background: #94a3b8 !important;
  opacity: 0.7;
}

/* Context card for elasticity, quality, brand */
.mi-context-card {
  margin-bottom: 0.75rem;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
  padding: 0.6rem;
  background: var(--color-surface);
}

.mi-context-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.5rem;
}

.mi-context-item {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.mi-context-label {
  font-size: 0.6875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.mi-context-value {
  font-size: 0.9375rem;
  font-variant-numeric: tabular-nums;
}

.mi-context-hint {
  font-size: 0.6875rem;
  color: var(--color-text-secondary);
  line-height: 1.3;
}

.mi-elastic-high {
  color: #f87171;
}

.mi-elastic-low {
  color: #4ade80;
}

.mi-quality-high {
  color: #4ade80;
}

.mi-quality-low {
  color: #f59e0b;
}

/* ─── Bar chart layout ─── */
.mi-chart-section {
  margin-bottom: 0.9rem;
}

.mi-chart-label {
  display: block;
  font-size: 0.7rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  margin-bottom: 0.3rem;
}

.mi-bar-chart {
  display: flex;
  align-items: flex-end;
  gap: 1px;
  height: 60px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: 4px;
  overflow: hidden;
}

.mi-bar {
  flex: 1;
  min-width: 1px;
  border-radius: 2px 2px 0 0;
  background: var(--color-primary, #3b82f6);
  transition: height 0.2s ease;
}

.mi-bar-revenue {
  background: #3b82f6;
}

.mi-bar-quantity {
  background: #8b5cf6;
}

.mi-bar-price {
  background: #f59e0b;
}

.mi-bar-cost {
  background: #ef4444;
}

.mi-hint {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
  margin-top: 0.4rem;
}

/* ─── Profit chart bars ─── */
.mi-bar-profit-positive {
  background: #16a34a;
}

.mi-bar-profit-negative {
  background: #dc2626;
  align-self: flex-end;
}

/* ─── Demand Drivers ─── */
.mi-demand-drivers {
  margin-top: 0.75rem;
  margin-bottom: 0.5rem;
}

.mi-driver-list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  margin-top: 0.4rem;
}

.mi-driver-entry {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.4rem 0.6rem;
  border-radius: var(--radius-sm, 6px);
  border: 1px solid var(--color-border);
  font-size: 0.8rem;
}

.mi-driver-positive {
  border-left: 3px solid #16a34a;
  background: rgba(22, 163, 74, 0.12);
}

.mi-driver-neutral {
  border-left: 3px solid #94a3b8;
  background: rgba(148, 163, 184, 0.1);
}

.mi-driver-negative {
  border-left: 3px solid #dc2626;
  background: rgba(220, 38, 38, 0.12);
}

.mi-driver-icon {
  font-weight: 700;
  font-size: 0.9rem;
  flex-shrink: 0;
  margin-top: 0.05rem;
}

.mi-driver-positive .mi-driver-icon {
  color: #4ade80;
}

.mi-driver-neutral .mi-driver-icon {
  color: #94a3b8;
}

.mi-driver-negative .mi-driver-icon {
  color: #f87171;
}

.mi-driver-content {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.mi-driver-factor {
  font-size: 0.78rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text);
}

.mi-driver-desc {
  color: var(--color-text-secondary);
  font-size: 0.78rem;
  line-height: 1.3;
}

@media (max-width: 640px) {
  .mi-summary-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .mi-share-label {
    width: 5rem;
  }

  .mi-context-grid {
    grid-template-columns: 1fr;
  }
}

/* ─── Operational Status Card ─── */
.operational-status-card {
  margin-top: 0.75rem;
}

.operational-status-row {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-bottom: 0.35rem;
}

.status-badge {
  display: inline-block;
  padding: 0.15rem 0.55rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.status-active {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}

.status-idle {
  background: rgba(148, 163, 184, 0.12);
  color: #94a3b8;
}

.status-blocked {
  background: rgba(248, 113, 113, 0.15);
  color: #f87171;
}

.status-full {
  background: rgba(251, 191, 36, 0.15);
  color: #fbbf24;
}

.status-unconfigured {
  background: rgba(148, 163, 184, 0.12);
  color: #94a3b8;
  font-style: italic;
}

.idle-ticks-label {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
}

.blocked-reason-text {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  line-height: 1.45;
  margin: 0;
}

.operating-costs-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem;
  margin-top: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--color-border);
}

.operating-cost-label {
  font-size: 0.72rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.operating-cost-item {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
  background: color-mix(in srgb, var(--color-border) 40%, transparent);
  border-radius: 0.25rem;
  padding: 0.1rem 0.35rem;
}

/* ─── Recent Activity Panel ─── */
.recent-activity-panel {
  margin-top: 0.75rem;
}

.activity-list {
  list-style: none;
  padding: 0;
  margin: 0.5rem 0 0;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.activity-item {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  font-size: 0.78rem;
  line-height: 1.4;
}

.activity-tick {
  flex-shrink: 0;
  font-size: 0.68rem;
  font-weight: 700;
  color: var(--color-text-secondary);
  min-width: 3.5rem;
}

.activity-desc {
  color: var(--color-text);
}

.activity-purchased .activity-tick {
  color: #60a5fa;
}
.activity-manufactured .activity-tick {
  color: #4ade80;
}
.activity-sold .activity-tick {
  color: #c084fc;
}
.activity-moved .activity-tick {
  color: #fbbf24;
}

/* ── Procurement Mode Selector ── */
.procurement-mode-options {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.25rem;
}

.procurement-mode-option {
  display: flex;
  flex-direction: column;
  padding: 0.6rem 0.75rem;
  border: 1px solid var(--color-border, #e2e8f0);
  border-radius: 6px;
  cursor: pointer;
  transition:
    border-color 0.15s,
    background 0.15s;
  gap: 0.15rem;
}

.procurement-mode-option:hover {
  border-color: var(--color-primary, #2563eb);
  background: var(--color-bg-hover, rgba(59, 130, 246, 0.1));
}

.procurement-mode-option.selected {
  border-color: var(--color-primary, #2563eb);
  background: var(--color-bg-selected, rgba(59, 130, 246, 0.15));
}

.procurement-mode-radio {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
  pointer-events: none;
}

.procurement-mode-label {
  font-weight: 600;
  font-size: 0.88rem;
  color: var(--color-text);
}

.procurement-mode-desc {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  line-height: 1.35;
}

/* ── Procurement Preview Card ── */
.procurement-preview {
  margin-top: 1rem;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  border: 1px solid var(--color-border, #e2e8f0);
  background: var(--color-surface, #f8fafc);
}

.procurement-preview-title {
  font-size: 0.82rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  margin: 0 0 0.6rem;
}

.procurement-preview-loading {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.procurement-preview-empty {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.preview-status {
  display: inline-block;
  font-size: 0.82rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
}

.preview-status.ok {
  color: #4ade80;
}

.preview-status.blocked {
  color: #dc2626;
}

.preview-details {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.preview-row {
  display: flex;
  gap: 0.5rem;
  font-size: 0.82rem;
}

.preview-label {
  color: var(--color-text-secondary);
  min-width: 7rem;
  flex-shrink: 0;
}

.preview-value {
  color: var(--color-text);
  font-weight: 500;
}

.preview-delivered {
  color: #4ade80;
  font-weight: 700;
}

.preview-blocked-price {
  color: #dc2626;
}

.preview-block-details {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-bottom: 0.5rem;
}

.preview-block-reason {
  font-size: 0.82rem;
  font-weight: 600;
  color: #dc2626;
}

.preview-block-message {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin: 0.15rem 0 0.35rem;
  line-height: 1.45;
}

/* ── Sourcing Comparison Panel ── */
.sourcing-comparison {
  margin-top: 1rem;
  padding: 0.85rem 1rem;
  border-radius: 8px;
  border: 1px solid var(--color-border, #e2e8f0);
  background: var(--color-surface, #f8fafc);
}

.sourcing-comparison-title {
  font-size: 0.82rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  margin: 0 0 0.25rem;
}

.sourcing-comparison-subtitle {
  margin: 0 0 0.75rem;
  font-size: 0.78rem;
}

.sourcing-comparison-loading,
.sourcing-comparison-empty {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.sourcing-trap-note {
  font-size: 0.78rem;
  color: #fbbf24;
  background: rgba(251, 191, 36, 0.12);
  border: 1px solid rgba(251, 191, 36, 0.3);
  border-radius: 4px;
  padding: 0.35rem 0.6rem;
  margin-bottom: 0.6rem;
  line-height: 1.4;
}

.sourcing-table-wrapper {
  overflow-x: auto;
  margin: 0 -0.5rem;
}

.sourcing-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.78rem;
}

.sourcing-table th {
  text-align: left;
  font-weight: 600;
  color: var(--color-text-secondary);
  padding: 0.3rem 0.5rem;
  border-bottom: 1px solid var(--color-border, #e2e8f0);
  white-space: nowrap;
}

.sourcing-table td {
  padding: 0.35rem 0.5rem;
  border-bottom: 1px solid var(--color-border-muted, #f0f4f8);
  vertical-align: top;
}

.sourcing-row.recommended {
  background: rgba(52, 211, 153, 0.08);
}

.sourcing-row.ineligible {
  opacity: 0.65;
}

.sourcing-row.recommended td {
  border-bottom-color: rgba(52, 211, 153, 0.2);
}

.sourcing-col-source {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 7rem;
}

.source-type-badge {
  font-size: 0.68rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.source-name {
  font-weight: 500;
  color: var(--color-text);
}

.source-distance {
  font-size: 0.68rem;
  color: var(--color-text-secondary);
}

.sourcing-col-transit .transit-cost {
  color: #f59e0b;
  font-weight: 500;
}

.col-landed {
  font-weight: 700;
  color: var(--color-text);
}

.sourcing-row.recommended .col-landed strong {
  color: #34d399;
}

.sourcing-row.ineligible .col-landed strong {
  color: #dc2626;
}

.sc-badge {
  display: inline-block;
  padding: 0.15rem 0.45rem;
  border-radius: 3px;
  font-size: 0.68rem;
  font-weight: 600;
  white-space: nowrap;
}

.sc-badge--recommended {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}

.sc-badge--eligible {
  background: rgba(96, 165, 250, 0.12);
  color: #60a5fa;
}

.sc-badge--blocked {
  background: rgba(248, 113, 113, 0.15);
  color: #f87171;
  cursor: help;
}

.sourcing-filter-hint {
  margin-top: 0.6rem;
  font-size: 0.75rem;
}

/* ── Quick Price Update Panel ── */
.mi-price-update-panel {
  margin-top: 1rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  padding: 0.75rem;
  background: var(--color-surface-raised, #f9fafb);
}

.mi-price-update-title {
  font-size: 0.8rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
  margin: 0 0 0.2rem;
}

.mi-price-update-desc {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  margin: 0 0 0.6rem;
  line-height: 1.4;
}

.mi-price-update-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.mi-price-update-label {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.mi-price-input {
  width: 6rem;
  padding: 0.3rem 0.5rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-size: 0.85rem;
  background: var(--color-background);
  color: var(--color-text);
}

.mi-price-update-btn {
  font-size: 0.8rem;
  padding: 0.3rem 0.75rem;
  white-space: nowrap;
}

.mi-price-impact-hint {
  font-size: 0.78rem;
  margin: 0 0 0.5rem;
  padding: 0.35rem 0.5rem;
  border-radius: var(--radius-sm);
  line-height: 1.4;
}

.mi-price-impact-raise {
  background: rgba(251, 191, 36, 0.12);
  color: #fbbf24;
  border: 1px solid rgba(251, 191, 36, 0.3);
}

.mi-price-impact-lower {
  background: rgba(52, 211, 153, 0.12);
  color: #4ade80;
  border: 1px solid rgba(52, 211, 153, 0.3);
}

.mi-price-success {
  font-size: 0.8rem;
  color: #4ade80;
  margin: 0.4rem 0 0;
}

.mi-price-error {
  font-size: 0.8rem;
  color: var(--color-danger, #dc2626);
  margin: 0.4rem 0 0;
}

/* ── B2B Competitive Price Hint ── */
.config-price-hint {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  margin: 0.25rem 0 0;
}

/* ── B2B No-source warning ── */
.b2b-no-source-warning {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  background: rgba(234, 179, 8, 0.1);
  border: 1px solid rgba(234, 179, 8, 0.4);
  border-radius: var(--radius-md, 6px);
  padding: 0.85rem 1rem;
  margin-bottom: 1rem;
}

.b2b-no-source-icon {
  font-size: 1.1rem;
  color: #eab308;
  flex-shrink: 0;
  line-height: 1.4;
}

.b2b-no-source-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.b2b-no-source-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: #d97706;
  margin: 0;
}

.b2b-no-source-body {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
  margin: 0;
  line-height: 1.45;
}

.btn-link {
  background: none;
  border: none;
  padding: 0;
  font-size: inherit;
  color: var(--color-primary, #3b82f6);
  cursor: pointer;
  text-decoration: underline;
  font-weight: 500;
}

.btn-link:hover {
  opacity: 0.8;
}

/* ── Flush Storage Section ── */
.flush-storage-section {
  margin-top: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--color-border);
}

/* ── Unit upgrade panel ─────────────────────────────────────────────── */
.unit-upgrade-panel h5 {
  margin-bottom: 0.5rem;
}

.unit-upgrade-in-progress {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
}

.unit-upgrade-progress-badge {
  font-size: 1.25rem;
  line-height: 1;
}

.unit-upgrade-progress-body strong {
  font-size: 0.85rem;
  color: var(--color-text-primary);
}

.unit-upgrade-progress-desc {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  margin: 0.2rem 0 0;
}

.unit-upgrade-max-level {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
}

.unit-upgrade-max-badge {
  color: #f59e0b;
  font-size: 1.1rem;
}

.unit-upgrade-max-note {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  margin: 0.3rem 0 0;
}

.unit-upgrade-not-available p {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.unit-upgrade-levels {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-bottom: 0.5rem;
}

.unit-upgrade-level {
  font-size: 0.85rem;
  font-weight: 600;
  padding: 0.2rem 0.55rem;
  border-radius: var(--radius-sm);
}

.current-level {
  background: var(--color-surface-secondary, #f3f4f6);
  color: var(--color-text-primary);
}

.next-level {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}

.unit-upgrade-arrow {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.unit-upgrade-stats {
  margin-bottom: 0.5rem;
}

.unit-upgrade-stat-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.8rem;
  padding: 0.2rem 0;
}

.unit-upgrade-stat-label {
  color: var(--color-text-secondary);
}

.stat-current {
  color: var(--color-text-secondary);
}

.stat-next {
  color: #4ade80;
  font-weight: 600;
}

.unit-upgrade-meta {
  display: flex;
  gap: 1rem;
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.6rem;
}

.unit-upgrade-cost {
  font-weight: 500;
}

.unit-upgrade-confirm-btn {
  width: 100%;
}

/* Staged upgrade state */
.unit-upgrade-staged {
  margin-top: 0.6rem;
  padding: 0.5rem 0.75rem;
  background: rgba(74, 222, 128, 0.08);
  border: 1px solid rgba(74, 222, 128, 0.3);
  border-radius: var(--radius-sm);
}

.unit-upgrade-staged-badge {
  font-size: 0.82rem;
  font-weight: 700;
  color: #4ade80;
  display: block;
  margin-bottom: 0.25rem;
}

.unit-upgrade-stage-info {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  margin: 0 0 0.4rem;
  line-height: 1.4;
}

.unit-upgrade-actions {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  margin-top: 0.6rem;
}

.unit-upgrade-stage-btn {
  width: 100%;
}

/* Downtime notice shown both when upgrade is pending and when it's about to be scheduled */
.unit-upgrade-downtime-notice {
  font-size: 0.78rem;
  color: #f59e0b;
  margin: 0.4rem 0 0.5rem;
  padding: 0.4rem 0.5rem;
  background: rgba(245, 158, 11, 0.08);
  border-left: 3px solid #f59e0b;
  border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
  line-height: 1.45;
}

.unit-upgrade-downtime-notice.available {
  margin-bottom: 0.6rem;
}

/* Under-upgrade indicator on grid cells */
.grid-cell.under-upgrade {
  position: relative;
  opacity: 0.75;
}

.cell-upgrading-badge {
  position: absolute;
  top: 2px;
  right: 2px;
  font-size: 0.7rem;
  line-height: 1;
  background: rgba(245, 158, 11, 0.15);
  border-radius: 4px;
  padding: 1px 3px;
  z-index: 2;
}

/* Stat delta badge shown in the before/after stat table */
.stat-delta {
  font-size: 0.72rem;
  font-weight: 600;
  margin-left: 0.35rem;
  padding: 0.1rem 0.3rem;
  border-radius: var(--radius-sm);
}

.stat-delta-negative {
  color: #f87171;
  background: rgba(248, 113, 113, 0.12);
}

.stat-delta-positive {
  color: #4ade80;
  background: rgba(74, 222, 128, 0.12);
}

/* Concurrent upgrades summary panel */
.concurrent-upgrades-panel {
  margin: 0.75rem 0;
  padding: 0.75rem 1rem;
  background: rgba(245, 158, 11, 0.06);
  border: 1px solid rgba(245, 158, 11, 0.25);
  border-radius: var(--radius-sm);
}

.concurrent-upgrades-panel h4 {
  margin: 0 0 0.3rem;
  font-size: 0.9rem;
  color: #f59e0b;
}

.concurrent-upgrades-help {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  margin: 0 0 0.5rem;
}

.concurrent-upgrades-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.concurrent-upgrade-item {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.8rem;
  padding: 0.25rem 0.5rem;
  background: rgba(245, 158, 11, 0.04);
  border-radius: var(--radius-sm);
}

.concurrent-upgrade-type {
  font-weight: 600;
  color: var(--color-text-primary);
  min-width: 8rem;
}

.concurrent-upgrade-pos {
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.concurrent-upgrade-arrow {
  color: var(--color-text-secondary);
}

.concurrent-upgrade-level {
  color: #4ade80;
  font-weight: 600;
}

.concurrent-upgrade-ticks {
  margin-left: auto;
  color: #f59e0b;
  font-size: 0.75rem;
}

.flush-confirm-dialog {
  margin-top: 0.5rem;
  padding: 0.75rem;
  background: rgba(251, 191, 36, 0.08);
  border: 1px solid rgba(251, 191, 36, 0.3);
  border-radius: var(--radius-sm);
}

.flush-confirm-msg {
  font-size: 0.82rem;
  color: #fbbf24;
  margin: 0 0 0.6rem;
  line-height: 1.4;
}

.flush-confirm-actions {
  display: flex;
  gap: 0.5rem;
}

.form-success {
  font-size: 0.8rem;
  color: #4ade80;
  margin: 0.35rem 0 0;
}

@media (max-width: 900px) {
  .purchase-selector-page {
    padding: 1rem;
  }

  .purchase-selector-grid {
    grid-template-columns: 1fr;
  }
}

.currency-badge {
  display: inline-block;
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  padding: 0.1rem 0.35rem;
  border-radius: 0.25rem;
  background: var(--color-surface-hover, rgba(0, 0, 0, 0.06));
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  vertical-align: middle;
  margin-left: 0.25rem;
}

/* ── Media house strategic picker ───────────────────────────── */
.media-house-picker {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  max-height: 320px;
  overflow-y: auto;
}

.media-house-option {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  padding: 0.5rem 0.6rem;
  border-radius: 0.4rem;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  cursor: pointer;
  transition:
    border-color 0.15s,
    background 0.15s;
}

.media-house-option:hover:not(.mh-disabled) {
  border-color: var(--color-primary, #4caf50);
  background: var(--color-surface-hover, rgba(0, 0, 0, 0.04));
}

.media-house-option.selected {
  border-color: var(--color-primary, #4caf50);
  background: color-mix(in srgb, var(--color-primary, #4caf50) 10%, transparent);
}

.media-house-option.mh-disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.media-house-option.mh-own {
  border-color: color-mix(in srgb, var(--color-primary, #4caf50) 40%, transparent);
}

.mh-option-row {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.mh-option-name {
  font-weight: 600;
  font-size: 0.88rem;
  flex: 1;
}

.mh-badge {
  display: inline-block;
  font-size: 0.65rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  padding: 0.1rem 0.35rem;
  border-radius: 0.25rem;
  white-space: nowrap;
}

.mh-type-badge {
  background: var(--color-surface-hover, rgba(0, 0, 0, 0.06));
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
}

.mh-gov-badge {
  background: #e8f4fd;
  border: 1px solid #90caf9;
  color: #1565c0;
}

.mh-own-badge {
  background: #e8f5e9;
  border: 1px solid #81c784;
  color: #2e7d32;
}

.mh-option-meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  flex-wrap: wrap;
}

.mh-meta-city {
  font-style: italic;
}

.mh-meta-reach {
  font-weight: 600;
}

.mh-meta-ranking {
  color: var(--color-text-secondary);
}

.mh-meta-status {
  font-weight: 600;
}

.mh-status-offline {
  color: #c62828;
}

.mh-status-construction {
  color: #f57f17;
}
</style>
