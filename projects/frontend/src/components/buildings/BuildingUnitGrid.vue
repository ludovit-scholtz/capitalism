<script setup lang="ts">
import { inject } from 'vue'
import { useI18n } from 'vue-i18n'
import { BUILDING_DETAIL_KEY } from '@/composables/useBuildingDetail'
import DiagonalConnector from '@/components/buildings/DiagonalConnector.vue'

const { t } = useI18n()
const bd = inject(BUILDING_DETAIL_KEY)!
const { locale, currentTick, saving, saveError, isEditing, selectedCell, draftUpgradeUnitIds, activeUnits, plannedUnits, allowedUnits, isUpgradeInProgress, showPlanningSection, draftTotalTicks, hasDraftChanges, draftConstructionCost, projectedCompanyCashAfterApply, draftLinkChanges, draftUnitChanges, clickReadOnlyCell, clickDraftCell, toggleHorizontalLink, toggleVerticalLink, togglePrimaryDiagonalLink, toggleSecondaryDiagonalLink, isHorizontalLinkActiveFor, isVerticalLinkActiveFor, canToggleHorizontalLink, canToggleVerticalLink, canTogglePrimaryDiagonalLink, canToggleSecondaryDiagonalLink, getHorizontalLinkStateFor, getVerticalLinkStateFor, getPrimaryDiagonalLinkStateFor, getSecondaryDiagonalLinkStateFor, getHorizontalLinkArrow, getVerticalLinkArrow, getDisplayedTicks, startEditing, cancelEditing, storeConfiguration, isUnitReverting, getUnitPrimaryMetric, getUnitInventorySummary, isCellUnderUpgrade, formatCurrency, formatTickDuration, formatGameTickTime, formatUnitQuantity, getUnitColor, getUnitAtFrom, getUnitDisplayLabel, getUnitDisplayImageUrl, getUnitDisplayMonogram, getUnitInventoryCostLabel, getUnitNextTickOperatingCostLabel, getUnitFlowSegments, getCellOutflowClass, getCellCapacityTooltip, getGridCellAriaLabel, getFillBucket, gridIndexes } = bd
</script>

<template>
<div class="grid-container">
  <div v-if="!isEditing" class="grid-section">
    <div class="grid-header">
      <div>
        <h2>{{ t('buildingDetail.activeConfiguration') }}</h2>
        <p class="section-subtitle">{{ t('buildingDetail.activeConfigurationHelp') }}</p>
      </div>
      <button v-if="!isEditing" class="btn btn-secondary" @click="startEditing">
        {{ t('buildingDetail.editConfiguration') }}
      </button>
    </div>

    <div class="unit-grid readonly-grid">
      <template v-for="y in gridIndexes" :key="`active-row-${y}`">
        <div class="grid-row unit-row">
          <template v-for="x in gridIndexes" :key="`active-unit-${x}-${y}`">
            <div
              class="grid-cell readonly clickable"
              :class="{
                occupied: !!getUnitAtFrom(activeUnits, x, y),
                selected: selectedCell?.x === x && selectedCell?.y === y,
                'under-upgrade': isCellUnderUpgrade(x, y),
              }"
              :style="
                getUnitAtFrom(activeUnits, x, y)
                  ? { borderColor: getUnitColor(getUnitAtFrom(activeUnits, x, y)!.unitType), background: getUnitColor(getUnitAtFrom(activeUnits, x, y)!.unitType) + '18' }
                  : {}
              "
              role="button"
              :tabindex="getUnitAtFrom(activeUnits, x, y) ? 0 : -1"
              :aria-label="getGridCellAriaLabel(getUnitAtFrom(activeUnits, x, y))"
              @click="clickReadOnlyCell(x, y)"
              @keydown.enter.space.prevent="clickReadOnlyCell(x, y)"
            >
              <template v-if="getUnitAtFrom(activeUnits, x, y)">
                <div class="cell-heading" aria-hidden="true">
                  <span class="cell-type">{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(activeUnits, x, y)!.unitType}`) }}</span>
                  <span class="cell-level">Lv.{{ getUnitAtFrom(activeUnits, x, y)!.level }}</span>
                </div>
                <!-- Under-upgrade badge -->
                <span v-if="isCellUnderUpgrade(x, y)" class="cell-upgrading-badge" aria-label="Unit is being upgraded">⏳</span>
                <div v-if="getUnitDisplayLabel(getUnitAtFrom(activeUnits, x, y))" class="cell-item-block" aria-hidden="true">
                  <img v-if="getUnitDisplayImageUrl(getUnitAtFrom(activeUnits, x, y))" class="cell-item-image" :src="getUnitDisplayImageUrl(getUnitAtFrom(activeUnits, x, y))!" alt="" />
                  <span v-else class="cell-item-avatar">{{ getUnitDisplayMonogram(getUnitAtFrom(activeUnits, x, y)) }}</span>
                  <div class="cell-item-copy">
                    <span class="cell-item">{{ getUnitDisplayLabel(getUnitAtFrom(activeUnits, x, y)) }}</span>
                    <span v-if="getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))" class="cell-stock">
                      {{ formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))!.quantity) }}/{{
                        formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))!.capacity)
                      }}
                    </span>
                  </div>
                </div>
                <span v-if="getUnitPrimaryMetric(getUnitAtFrom(activeUnits, x, y))" class="cell-metric" aria-hidden="true">
                  {{ getUnitPrimaryMetric(getUnitAtFrom(activeUnits, x, y)) }}
                </span>
                <span v-if="getUnitInventoryCostLabel(getUnitAtFrom(activeUnits, x, y))" class="cell-value" aria-hidden="true">
                  {{ t('buildingDetail.inventory.sourcingCostsShort', { value: getUnitInventoryCostLabel(getUnitAtFrom(activeUnits, x, y)) }) }}
                </span>
                <span v-if="getUnitNextTickOperatingCostLabel(getUnitAtFrom(activeUnits, x, y))" class="cell-operating-cost" aria-hidden="true">
                  {{ t('buildingDetail.operatingCost.tileLabel', { cost: getUnitNextTickOperatingCostLabel(getUnitAtFrom(activeUnits, x, y)) }) }}
                </span>
                <div
                  v-if="getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))?.capacity"
                  class="cell-capacity"
                  aria-hidden="true"
                  :title="getCellCapacityTooltip(getUnitAtFrom(activeUnits, x, y))"
                >
                  <span
                    class="cell-capacity-fill"
                    :data-fill="getFillBucket(getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))!.fillPercent)"
                    :style="{ width: `${getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).fillWidth}%` }"
                  ></span>
                  <span
                    v-if="getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).inflowWidth > 0"
                    class="cell-capacity-inflow"
                    :style="{ left: `${getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).inflowLeft}%`, width: `${getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).inflowWidth}%` }"
                  ></span>
                  <span
                    v-if="getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).outflowWidth > 0"
                    :class="getCellOutflowClass(getUnitAtFrom(activeUnits, x, y))"
                    :style="{ left: `${getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).outflowLeft}%`, width: `${getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).outflowWidth}%` }"
                  ></span>
                </div>
                <div v-if="getUnitFlowSegments(getUnitAtFrom(activeUnits, x, y)).hasMovement" class="cell-flow-labels" aria-hidden="true">
                  <span v-if="(getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))?.lastTickInflow ?? 0) > 0" class="cell-flow-in"
                    >↑{{ formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))!.lastTickInflow!) }}</span
                  >
                  <span
                    v-if="(getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))?.lastTickOutflow ?? 0) > 0"
                    :class="getUnitAtFrom(activeUnits, x, y)?.unitType === 'PUBLIC_SALES' ? 'cell-flow-sold' : 'cell-flow-out'"
                    >↓{{ formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(activeUnits, x, y))!.lastTickOutflow!) }}</span
                  >
                </div>
              </template>
              <template v-else>
                <span class="cell-empty" aria-hidden="true">+</span>
              </template>
            </div>

            <div
              v-if="x < 3"
              :key="`active-horizontal-${x}-${y}`"
              class="link-toggle horizontal readonly"
              :class="[
                `link-state-${getHorizontalLinkStateFor(activeUnits, x, y)}`,
                { active: isHorizontalLinkActiveFor(activeUnits, x, y), disabled: !canToggleHorizontalLink(activeUnits, x, y) },
              ]"
            >
              <span class="link-line"></span>
              <span v-if="getHorizontalLinkStateFor(activeUnits, x, y) !== 'none'" class="link-arrow" aria-hidden="true">{{
                getHorizontalLinkArrow(getHorizontalLinkStateFor(activeUnits, x, y))
              }}</span>
            </div>
          </template>
        </div>

        <div v-if="y < 3" class="grid-row connector-row">
          <template v-for="x in gridIndexes" :key="`active-connector-${x}-${y}`">
            <div
              class="link-toggle vertical readonly"
              :class="[`link-state-${getVerticalLinkStateFor(activeUnits, x, y)}`, { active: isVerticalLinkActiveFor(activeUnits, x, y), disabled: !canToggleVerticalLink(activeUnits, x, y) }]"
            >
              <span class="link-line"></span>
              <span v-if="getVerticalLinkStateFor(activeUnits, x, y) !== 'none'" class="link-arrow" aria-hidden="true">{{
                getVerticalLinkArrow(getVerticalLinkStateFor(activeUnits, x, y))
              }}</span>
            </div>

            <DiagonalConnector
              v-if="x < 3"
              :key="`active-diagonal-${x}-${y}`"
              :x="x"
              :y="y"
              :primary-state="getPrimaryDiagonalLinkStateFor(activeUnits, x, y)"
              :secondary-state="getSecondaryDiagonalLinkStateFor(activeUnits, x, y)"
              :can-toggle-primary="canTogglePrimaryDiagonalLink(activeUnits, x, y)"
              :can-toggle-secondary="canToggleSecondaryDiagonalLink(activeUnits, x, y)"
              :is-readonly="true"
            />
          </template>
        </div>
      </template>
    </div>
  </div>

  <div v-if="showPlanningSection" class="grid-section">
    <div class="grid-header plan-header">
      <div>
        <h2>{{ isUpgradeInProgress ? t('buildingDetail.queuedConfiguration') : t('buildingDetail.plannedConfiguration') }}</h2>
        <p class="section-subtitle">
          {{ isUpgradeInProgress ? t('buildingDetail.queuedConfigurationHelp') : t('buildingDetail.plannedConfigurationHelp') }}
        </p>
      </div>
      <div class="grid-actions">
        <button class="btn btn-secondary" @click="cancelEditing">
          {{ t('buildingDetail.cancelEditing') }}
        </button>
        <button class="btn btn-primary" :disabled="saving || !hasDraftChanges" @click="storeConfiguration">
          {{
            saving
              ? t('common.loading')
              : draftUpgradeUnitIds.size > 0
                ? t('buildingDetail.unitUpgrade.storeWithUpgradesButton', { count: draftUpgradeUnitIds.size })
                : t('buildingDetail.storeConfiguration')
          }}
        </button>
      </div>
    </div>

    <!-- Inline save error (e.g. RECIPE_INPUT_MISMATCH, PRO_SUBSCRIPTION_REQUIRED) -->
    <div v-if="saveError" class="save-error-banner" role="alert">
      ⚠️ {{ saveError }}
      <button class="btn btn-ghost btn-sm" @click="saveError = null">{{ t('common.close') }}</button>
    </div>

    <div class="upgrade-summary">
      <span class="upgrade-summary-pill" :title="'Tick #' + currentTick">{{
        t('buildingDetail.currentTickLabel', { time: formatGameTickTime(currentTick, locale) })
      }}</span>
      <span class="upgrade-summary-pill" :title="draftTotalTicks + ' ticks'">{{ t('buildingDetail.totalUpgradeTicks', { time: formatTickDuration(draftTotalTicks, locale) }) }}</span>
      <span class="upgrade-summary-pill">{{ t('buildingDetail.totalBuildCost', { cost: formatCurrency(draftConstructionCost) }) }}</span>
      <span v-if="projectedCompanyCashAfterApply != null" class="upgrade-summary-pill">
        {{ t('buildingDetail.cashAfterApply', { cash: formatCurrency(projectedCompanyCashAfterApply) }) }}
      </span>
    </div>

    <div v-if="draftLinkChanges.length > 0" class="link-changes-summary" role="region" :aria-label="t('buildingDetail.linkChangesSummaryTitle')">
      <h4 class="link-changes-title">{{ t('buildingDetail.linkChangesSummaryTitle') }}</h4>
      <ul class="link-changes-list">
        <li v-for="(change, i) in draftLinkChanges" :key="i" class="link-change-item" :class="change.changeType === 'added' ? 'link-change-added' : 'link-change-removed'">
          <span class="link-change-badge" aria-hidden="true">{{ change.changeType === 'added' ? '+' : '−' }}</span>
          <span>{{ change.description }}</span>
        </li>
      </ul>
    </div>

    <div v-if="draftUnitChanges.length > 0" class="unit-changes-summary" role="region" :aria-label="t('buildingDetail.unitChangesSummaryTitle')">
      <h4 class="unit-changes-title">{{ t('buildingDetail.unitChangesSummaryTitle') }}</h4>
      <ul class="unit-changes-list">
        <li
          v-for="(change, i) in draftUnitChanges"
          :key="i"
          class="unit-change-item"
          :class="{
            'unit-change-added': change.changeType === 'added',
            'unit-change-removed': change.changeType === 'removed',
            'unit-change-replaced': change.changeType === 'replaced',
          }"
        >
          <span class="unit-change-badge" aria-hidden="true">
            {{ change.changeType === 'added' ? '+' : change.changeType === 'removed' ? '−' : '↺' }}
          </span>
          <span class="unit-change-description">
            <template v-if="change.changeType === 'added'">
              {{ t('buildingDetail.unitChangeAdded', { type: t(`buildingDetail.unitTypes.${change.unitType}`), x: change.gridX, y: change.gridY }) }}
            </template>
            <template v-else-if="change.changeType === 'removed'">
              {{ t('buildingDetail.unitChangeRemoved', { type: t(`buildingDetail.unitTypes.${change.unitType}`), x: change.gridX, y: change.gridY }) }}
            </template>
            <template v-else>
              {{
                t('buildingDetail.unitChangeReplaced', {
                  from: t(`buildingDetail.unitTypes.${change.previousUnitType}`),
                  to: t(`buildingDetail.unitTypes.${change.unitType}`),
                  x: change.gridX,
                  y: change.gridY,
                })
              }}
            </template>
          </span>
          <span class="unit-change-meta">
            <span class="unit-change-ticks" :title="t('buildingDetail.unitChangeTicks', { ticks: change.ticks })">{{ formatTickDuration(change.ticks, locale) }}</span>
            <span v-if="change.cost > 0" class="unit-change-cost">{{ formatCurrency(change.cost) }}</span>
          </span>
        </li>
      </ul>
    </div>

    <div class="unit-grid">
      <template v-for="y in gridIndexes" :key="`planned-row-${y}`">
        <div class="grid-row unit-row">
          <template v-for="x in gridIndexes" :key="`planned-unit-${x}-${y}`">
            <button
              class="grid-cell"
              :class="{
                occupied: !!getUnitAtFrom(plannedUnits, x, y),
                selected: selectedCell?.x === x && selectedCell?.y === y,
                changed: !!getUnitAtFrom(plannedUnits, x, y) && getDisplayedTicks(getUnitAtFrom(plannedUnits, x, y)!) > 0,
                reverting: isUnitReverting(getUnitAtFrom(plannedUnits, x, y)),
              }"
              :style="
                getUnitAtFrom(plannedUnits, x, y)
                  ? { borderColor: getUnitColor(getUnitAtFrom(plannedUnits, x, y)!.unitType), background: getUnitColor(getUnitAtFrom(plannedUnits, x, y)!.unitType) + '18' }
                  : {}
              "
              :aria-label="getGridCellAriaLabel(getUnitAtFrom(plannedUnits, x, y))"
              @click="clickDraftCell(x, y)"
            >
              <template v-if="getUnitAtFrom(plannedUnits, x, y)">
                <div class="cell-heading" aria-hidden="true">
                  <span class="cell-type">{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(plannedUnits, x, y)!.unitType}`) }}</span>
                  <span class="cell-level">Lv.{{ getUnitAtFrom(plannedUnits, x, y)!.level }}</span>
                </div>
                <div v-if="getUnitDisplayLabel(getUnitAtFrom(plannedUnits, x, y))" class="cell-item-block" aria-hidden="true">
                  <img v-if="getUnitDisplayImageUrl(getUnitAtFrom(plannedUnits, x, y))" class="cell-item-image" :src="getUnitDisplayImageUrl(getUnitAtFrom(plannedUnits, x, y))!" alt="" />
                  <span v-else class="cell-item-avatar">{{ getUnitDisplayMonogram(getUnitAtFrom(plannedUnits, x, y)) }}</span>
                  <div class="cell-item-copy">
                    <span class="cell-item">{{ getUnitDisplayLabel(getUnitAtFrom(plannedUnits, x, y)) }}</span>
                    <span v-if="getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))" class="cell-stock">
                      {{ formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))!.quantity) }}/{{
                        formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))!.capacity)
                      }}
                    </span>
                  </div>
                </div>
                <span v-if="getUnitPrimaryMetric(getUnitAtFrom(plannedUnits, x, y))" class="cell-metric" aria-hidden="true">
                  {{ getUnitPrimaryMetric(getUnitAtFrom(plannedUnits, x, y)) }}
                </span>
                <span v-if="getUnitInventoryCostLabel(getUnitAtFrom(plannedUnits, x, y))" class="cell-value" aria-hidden="true">
                  {{ t('buildingDetail.inventory.sourcingCostsShort', { value: getUnitInventoryCostLabel(getUnitAtFrom(plannedUnits, x, y)) }) }}
                </span>
                <div
                  v-if="getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))?.capacity"
                  class="cell-capacity"
                  aria-hidden="true"
                  :title="getCellCapacityTooltip(getUnitAtFrom(plannedUnits, x, y))"
                >
                  <span
                    class="cell-capacity-fill"
                    :data-fill="getFillBucket(getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))!.fillPercent)"
                    :style="{ width: `${getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).fillWidth}%` }"
                  ></span>
                  <span
                    v-if="getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).inflowWidth > 0"
                    class="cell-capacity-inflow"
                    :style="{ left: `${getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).inflowLeft}%`, width: `${getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).inflowWidth}%` }"
                  ></span>
                  <span
                    v-if="getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).outflowWidth > 0"
                    :class="getCellOutflowClass(getUnitAtFrom(plannedUnits, x, y))"
                    :style="{
                      left: `${getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).outflowLeft}%`,
                      width: `${getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).outflowWidth}%`,
                    }"
                  ></span>
                </div>
                <div v-if="getUnitFlowSegments(getUnitAtFrom(plannedUnits, x, y)).hasMovement" class="cell-flow-labels" aria-hidden="true">
                  <span v-if="(getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))?.lastTickInflow ?? 0) > 0" class="cell-flow-in"
                    >↑{{ formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))!.lastTickInflow!) }}</span
                  >
                  <span
                    v-if="(getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))?.lastTickOutflow ?? 0) > 0"
                    :class="getUnitAtFrom(plannedUnits, x, y)?.unitType === 'PUBLIC_SALES' ? 'cell-flow-sold' : 'cell-flow-out'"
                    >↓{{ formatUnitQuantity(getUnitInventorySummary(getUnitAtFrom(plannedUnits, x, y))!.lastTickOutflow!) }}</span
                  >
                </div>
                <span
                  v-if="getDisplayedTicks(getUnitAtFrom(plannedUnits, x, y)!) > 0"
                  class="cell-pending"
                  aria-hidden="true"
                  :title="getDisplayedTicks(getUnitAtFrom(plannedUnits, x, y)!) + ' ticks'"
                >
                  {{ t('buildingDetail.unitUnavailableFor', { time: formatTickDuration(getDisplayedTicks(getUnitAtFrom(plannedUnits, x, y)!), locale) }) }}
                </span>
                <span v-if="isUnitReverting(getUnitAtFrom(plannedUnits, x, y))" class="cell-reverting" aria-hidden="true">{{ t('buildingDetail.reverting') }}</span>
              </template>
              <template v-else>
                <span class="cell-empty" aria-hidden="true">+</span>
              </template>
            </button>

            <button
              v-if="x < 3"
              :key="`planned-horizontal-${x}-${y}`"
              class="link-toggle horizontal"
              :class="[
                `link-state-${getHorizontalLinkStateFor(plannedUnits, x, y)}`,
                { active: isHorizontalLinkActiveFor(plannedUnits, x, y), disabled: !canToggleHorizontalLink(plannedUnits, x, y) },
              ]"
              :disabled="!canToggleHorizontalLink(plannedUnits, x, y)"
              :aria-label="t('buildingDetail.linkHorizontalAriaLabel', { state: getHorizontalLinkStateFor(plannedUnits, x, y) })"
              @click="toggleHorizontalLink(x, y)"
            >
              <span class="link-line"></span>
              <span v-if="getHorizontalLinkStateFor(plannedUnits, x, y) !== 'none'" class="link-arrow" aria-hidden="true">{{
                getHorizontalLinkArrow(getHorizontalLinkStateFor(plannedUnits, x, y))
              }}</span>
            </button>
          </template>
        </div>

        <div v-if="y < 3" class="grid-row connector-row">
          <template v-for="x in gridIndexes" :key="`planned-connector-${x}-${y}`">
            <button
              class="link-toggle vertical"
              :class="[
                `link-state-${getVerticalLinkStateFor(plannedUnits, x, y)}`,
                { active: isVerticalLinkActiveFor(plannedUnits, x, y), disabled: !canToggleVerticalLink(plannedUnits, x, y) },
              ]"
              :disabled="!canToggleVerticalLink(plannedUnits, x, y)"
              :aria-label="t('buildingDetail.linkVerticalAriaLabel', { state: getVerticalLinkStateFor(plannedUnits, x, y) })"
              @click="toggleVerticalLink(x, y)"
            >
              <span class="link-line"></span>
              <span v-if="getVerticalLinkStateFor(plannedUnits, x, y) !== 'none'" class="link-arrow" aria-hidden="true">{{
                getVerticalLinkArrow(getVerticalLinkStateFor(plannedUnits, x, y))
              }}</span>
            </button>

            <DiagonalConnector
              v-if="x < 3"
              :key="`planned-diagonal-${x}-${y}`"
              :x="x"
              :y="y"
              :primary-state="getPrimaryDiagonalLinkStateFor(plannedUnits, x, y)"
              :secondary-state="getSecondaryDiagonalLinkStateFor(plannedUnits, x, y)"
              :can-toggle-primary="canTogglePrimaryDiagonalLink(plannedUnits, x, y)"
              :can-toggle-secondary="canToggleSecondaryDiagonalLink(plannedUnits, x, y)"
              @toggle-primary="togglePrimaryDiagonalLink(x, y)"
              @toggle-secondary="toggleSecondaryDiagonalLink(x, y)"
            />
          </template>
        </div>
      </template>
    </div>

    <div class="grid-legend">
      <div v-for="unitType in allowedUnits" :key="unitType" class="legend-item">
        <span class="legend-color" :style="{ background: getUnitColor(unitType) }"></span>
        <span>{{ t(`buildingDetail.unitTypes.${unitType}`) }}</span>
      </div>
    </div>

  </div>
</div>
</template>
