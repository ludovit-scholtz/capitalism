<script setup lang="ts">
import { computed, provide, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useBuildingDetail, BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import { useFirstTimeUserGates } from '@/composables/useFirstTimeUserGates'
import { gqlRequest } from '@/lib/graphql'
import BuildingDetailHeader from '@/components/buildings/BuildingDetailHeader.vue'
import PurchaseSelectorDialog from '@/components/buildings/PurchaseSelectorDialog.vue'
import BuildingPropertyPanel from '@/components/buildings/BuildingPropertyPanel.vue'
import BuildingMediaHousePanel from '@/components/buildings/BuildingMediaHousePanel.vue'
import BuildingPowerPlantPanel from '@/components/buildings/BuildingPowerPlantPanel.vue'
import BuildingResearchPanel from '@/components/buildings/BuildingResearchPanel.vue'
import BuildingChainStatusPanel from '@/components/buildings/BuildingChainStatusPanel.vue'
import BuildingUnitGrid from '@/components/buildings/BuildingUnitGrid.vue'
import BuildingEditingSidebar from '@/components/buildings/BuildingEditingSidebar.vue'
import BuildingReadonlySidebar from '@/components/buildings/BuildingReadonlySidebar.vue'
import BuildingOverviewSidebar from '@/components/buildings/BuildingOverviewSidebar.vue'
import TutorialTooltip from '@/components/ui/TutorialTooltip.vue'

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
  upgradeBlockMessage,
  isEditing,
  formatTickDuration,
  formatGameTickTime,
  applyStarterLayout,
  applyShopStarterLayout,
  cancelPlan,
} = bd

const showEditingSidebar = computed(() => Boolean(bd.selectedCell.value && bd.isEditing.value))
const showReadonlySidebar = computed(() => {
  if (bd.isEditing.value) return false

  const selectedCell = bd.selectedCell.value

  // Building-level tabs (e.g. supply chain for FACTORY with no cell selected)
  if (!selectedCell) {
    return bd.unitDetailTabs.value.length > 0
  }

  return Boolean(bd.getUnitAtFrom(bd.activeUnits.value, selectedCell.x, selectedCell.y))
})
const showOverviewSidebar = computed(() => !showEditingSidebar.value && !showReadonlySidebar.value)

/** True for multi-unit building types that should show the factory-style grid editor. */
const isMultiUnitBuilding = computed(() => building.value?.type !== 'APARTMENT' && building.value?.type !== 'COMMERCIAL' && building.value?.type !== 'MEDIA_HOUSE')

// ── First-time user tutorial tooltips ────────────────────────────────────────
const {
  showBuildingDetailTooltip,
  showGridEditorTooltip,
  hydrateFromBackend,
  dismissBuildingDetailTooltip,
  dismissGridEditorTooltip,
} = useFirstTimeUserGates()

const buildingDetailTooltipReady = ref(false)

onMounted(async () => {
  // Hydrate tooltip state from backend (best-effort)
  try {
    const data = await gqlRequest<{
      tutorialProgress: Array<{ milestone: string; isCompleted: boolean; completedAtUtc: string | null }>
    }>('{ tutorialProgress { milestone isCompleted completedAtUtc } }')
    await hydrateFromBackend(data.tutorialProgress ?? [])
  } catch {
    // Non-critical: fall through to sessionStorage state
  }
  // Small delay so building loads before tooltip
  setTimeout(() => {
    buildingDetailTooltipReady.value = true
  }, 800)
})
</script>

<template>
  <div class="building-detail-view container px-4 py-8 max-[768px]:px-2 max-[768px]:py-4 relative">
    <!-- Building detail first-visit contextual tooltip overlay -->
    <TutorialTooltip
      v-if="buildingDetailTooltipReady && showBuildingDetailTooltip"
      milestone="TOOLTIP_BUILDING_DETAIL_SHOWN"
      :title="t('tutorial.tooltips.buildingDetailOverlay.title')"
      :description="t('tutorial.tooltips.buildingDetailOverlay.body')"
      position="bottom"
      @dismiss="dismissBuildingDetailTooltip"
    />
    <div class="page-nav mb-6">
      <RouterLink to="/dashboard" class="back-link inline-flex items-center gap-1.5 text-sm text-muted no-underline hover:text-brand">
        <span>←</span> {{ t('buildingDetail.backToDashboard') }}
      </RouterLink>
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
          <li v-for="(warning, i) in configWarnings" :key="i">{{ t(warning.key, warning.params || {}) }}</li>
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
          <button class="btn btn-primary" @click="applyStarterLayout">{{ t('buildingDetail.starterSetup.applyStarter') }}</button>
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
          <button class="btn btn-primary" @click="applyShopStarterLayout">{{ t('buildingDetail.shopStarterSetup.applyStarter') }}</button>
        </div>
      </div>

      <div v-if="isUpgradeInProgress" class="upgrade-banner" role="status">
        <div>
          <strong>{{ t('buildingDetail.upgradeQueuedTitle') }}</strong>
          <p>{{ t('buildingDetail.upgradeQueuedBody', { time: formatTickDuration(remainingUpgradeTicks, locale) }) }}</p>
          <p v-if="upgradeBlockMessage" class="upgrade-block-message">{{ upgradeBlockMessage }}</p>
        </div>
        <div class="upgrade-banner-actions">
          <div class="upgrade-pill" :title="t('buildingDetail.upgradeAppliesAt', { time: pendingConfiguration?.appliesAtTick })">
            {{ t('buildingDetail.upgradeAppliesAt', { time: formatGameTickTime(pendingConfiguration?.appliesAtTick ?? 0, locale) }) }}
          </div>
          <button v-if="!isEditing" class="btn btn-danger btn-sm" :disabled="cancellingPlan" @click="cancelPlan">{{ cancellingPlan ? t('common.loading') : t('buildingDetail.cancelPlan') }}</button>
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
        <p>{{ t('buildingDetail.proAccessGrandfathered', { products: lockedConfiguredProductNames }) }}</p>
      </div>

      <BuildingChainStatusPanel v-if="isMultiUnitBuilding" />

      <div
        class="main-content grid items-start gap-8 [grid-template-columns:minmax(0,1.05fr)_minmax(360px,0.95fr)] min-[1320px]:[grid-template-columns:minmax(0,1fr)_minmax(420px,1fr)] max-[1024px]:grid-cols-1 max-[1024px]:gap-6 max-[768px]:gap-4"
      >
        <template v-if="isMultiUnitBuilding">
          <!-- Grid editor section with contextual tooltip for first-time visitors -->
          <div class="relative">
            <TutorialTooltip
              v-if="buildingDetailTooltipReady && showGridEditorTooltip && !showBuildingDetailTooltip"
              milestone="TOOLTIP_GRID_EDITOR_SHOWN"
              :title="t('tutorial.tooltips.gridEditorOverlay.title')"
              :description="t('tutorial.tooltips.gridEditorOverlay.body')"
              position="bottom"
              @dismiss="dismissGridEditorTooltip"
            />
            <BuildingUnitGrid />
          </div>
          <BuildingEditingSidebar v-if="showEditingSidebar" />
          <BuildingReadonlySidebar v-else-if="showReadonlySidebar" />
          <BuildingOverviewSidebar v-if="showOverviewSidebar" />
        </template>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* ── Pill badges (used in BuildingDetailHeader rendered by parent) ── */
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

.meta-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.meta-value {
  font-weight: 600;
}

/* ── Loading / Error states ── */
.loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
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

/* ── Upgrade in-progress banner ── */
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

.upgrade-banner p {
  margin: 0.35rem 0 0;
  font-size: 0.875rem;
}

.upgrade-block-message {
  color: #b45309;
  font-weight: 600;
}

/* ── Error / warning banners ── */
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

/* ── Pro access banner ── */
.pro-access-banner {
  margin-bottom: 1.5rem;
  padding: 1rem 1.25rem;
  border: 1px solid rgba(255, 109, 0, 0.3);
  border-radius: var(--radius-lg);
  background: rgba(255, 109, 0, 0.08);
}

.pro-access-banner p {
  margin: 0.35rem 0 0;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

/* ── Starter setup guided banner ── */
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

/* ── Config warnings ── */
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

/* ── Concurrent unit upgrades summary panel ── */
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
</style>
