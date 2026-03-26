<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import type { Building, BuildingUnit } from '@/types'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const gridIndexes = [0, 1, 2, 3] as const
const connectorIndexes = [0, 1, 2] as const

const buildingId = computed(() => route.params.id as string)
const building = ref<Building | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const selectedCell = ref<{ x: number; y: number } | null>(null)
const showUnitPicker = ref(false)
const editMode = ref(false)
const pendingTicks = ref<Record<string, number>>({})

const allowedUnitsMap: Record<string, string[]> = {
  MINE: ['MINING', 'STORAGE', 'B2B_SALES'],
  FACTORY: ['PURCHASE', 'MANUFACTURING', 'BRANDING', 'STORAGE', 'B2B_SALES'],
  SALES_SHOP: ['PURCHASE', 'MARKETING', 'PUBLIC_SALES'],
  RESEARCH_DEVELOPMENT: ['PRODUCT_QUALITY', 'BRAND_QUALITY'],
}

const unitColors: Record<string, string> = {
  MINING: '#FF6D00',
  STORAGE: '#8B949E',
  B2B_SALES: '#00C853',
  PURCHASE: '#0047FF',
  MANUFACTURING: '#FF6D00',
  BRANDING: '#9333EA',
  MARKETING: '#EC4899',
  PUBLIC_SALES: '#00C853',
  PRODUCT_QUALITY: '#0047FF',
  BRAND_QUALITY: '#9333EA',
}

const allowedUnits = computed(() => {
  if (!building.value) return []
  return allowedUnitsMap[building.value.type] || []
})

function getUnitAt(x: number, y: number): BuildingUnit | undefined {
  return building.value?.units.find((u) => u.gridX === x && u.gridY === y)
}

function getUnitColor(unitType: string): string {
  return unitColors[unitType] || '#8B949E'
}

function formatBuildingType(type: string): string {
  return type.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

function clickCell(x: number, y: number) {
  if (editMode.value) {
    // In edit mode, clicking cells does nothing, links are toggled separately
    return
  }
  const existing = getUnitAt(x, y)
  if (existing) {
    selectedCell.value = { x, y }
    showUnitPicker.value = false
  } else {
    selectedCell.value = { x, y }
    showUnitPicker.value = true
  }
}

// Place a unit on the grid (client-side preview; persisted when backend mutation is implemented)
async function placeUnit(unitType: string) {
  if (!selectedCell.value || !building.value) return
  showUnitPicker.value = false

  const newUnit: BuildingUnit = {
    id: `unit-${Date.now()}`,
    buildingId: building.value.id,
    unitType,
    gridX: selectedCell.value.x,
    gridY: selectedCell.value.y,
    level: 1,
    linkUp: false,
    linkDown: false,
    linkLeft: false,
    linkRight: false,
    linkUpLeft: false,
    linkUpRight: false,
    linkDownLeft: false,
    linkDownRight: false,
  }

  building.value.units = [...building.value.units, newUnit]
  selectedCell.value = null
}

// Remove a unit from the grid (client-side preview; persisted when backend mutation is implemented)
function removeUnit(x: number, y: number) {
  if (!building.value) return
  building.value.units = building.value.units.filter((u) => !(u.gridX === x && u.gridY === y))
  selectedCell.value = null
}

function toggleHorizontalLink(x: number, y: number) {
  const unit1 = getUnitAt(x, y)
  const unit2 = getUnitAt(x + 1, y)
  if (unit1 && unit2) {
    unit1.linkRight = !unit1.linkRight
    unit2.linkLeft = !unit2.linkLeft
  }
}

function toggleVerticalLink(x: number, y: number) {
  const unit1 = getUnitAt(x, y)
  const unit2 = getUnitAt(x, y + 1)
  if (unit1 && unit2) {
    unit1.linkDown = !unit1.linkDown
    unit2.linkUp = !unit2.linkUp
  }
}

function clearDiagonalState(x: number, y: number) {
  const topLeft = getUnitAt(x, y)
  const topRight = getUnitAt(x + 1, y)
  const bottomLeft = getUnitAt(x, y + 1)
  const bottomRight = getUnitAt(x + 1, y + 1)

  if (topLeft) topLeft.linkDownRight = false
  if (bottomRight) bottomRight.linkUpLeft = false
  if (topRight) topRight.linkDownLeft = false
  if (bottomLeft) bottomLeft.linkUpRight = false
}

function getDiagonalState(x: number, y: number): 'none' | 'tl-br' | 'tr-bl' {
  const topLeft = getUnitAt(x, y)
  const topRight = getUnitAt(x + 1, y)

  if (topLeft?.linkDownRight) return 'tl-br'
  if (topRight?.linkDownLeft) return 'tr-bl'
  return 'none'
}

function toggleDiagonalLink(x: number, y: number) {
  const topLeft = getUnitAt(x, y)
  const topRight = getUnitAt(x + 1, y)
  const bottomLeft = getUnitAt(x, y + 1)
  const bottomRight = getUnitAt(x + 1, y + 1)
  if (!topLeft || !topRight || !bottomLeft || !bottomRight) return

  const currentState = getDiagonalState(x, y)
  clearDiagonalState(x, y)

  if (currentState === 'none') {
    topLeft.linkDownRight = true
    bottomRight.linkUpLeft = true
    return
  }

  if (currentState === 'tl-br') {
    topRight.linkDownLeft = true
    bottomLeft.linkUpRight = true
  }
}

function isHorizontalLinkActive(x: number, y: number): boolean {
  return getUnitAt(x, y)?.linkRight || false
}

function isVerticalLinkActive(x: number, y: number): boolean {
  return getUnitAt(x, y)?.linkDown || false
}

function canToggleHorizontalLink(x: number, y: number): boolean {
  return !!getUnitAt(x, y) && !!getUnitAt(x + 1, y)
}

function canToggleVerticalLink(x: number, y: number): boolean {
  return !!getUnitAt(x, y) && !!getUnitAt(x, y + 1)
}

function canToggleDiagonalLink(x: number, y: number): boolean {
  return !!getUnitAt(x, y) && !!getUnitAt(x + 1, y) && !!getUnitAt(x, y + 1) && !!getUnitAt(x + 1, y + 1)
}

function saveChanges() {
  // TODO: send to backend
  // For now, simulate pending ticks
  building.value?.units.forEach(unit => {
    pendingTicks.value[unit.id] = 1 // Assume 1 tick for link changes
  })
  editMode.value = false
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  try {
    const data = await gqlRequest<{ myCompanies: { buildings: Building[] }[] }>(
      `{ myCompanies {
        buildings {
          id name type level powerConsumption isForSale cityId
          units { id unitType gridX gridY level linkUp linkDown linkLeft linkRight linkUpLeft linkUpRight linkDownLeft linkDownRight }
        }
      } }`,
    )
    const allBuildings = data.myCompanies.flatMap((c) => c.buildings)
    building.value = allBuildings.find((b) => b.id === buildingId.value) || null
    if (!building.value) {
      error.value = 'Building not found'
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load building'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="building-detail-view container">
    <div class="page-nav">
      <RouterLink to="/dashboard" class="back-link">
        <span>←</span> {{ t('buildingDetail.backToDashboard') }}
      </RouterLink>
    </div>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="router.push('/dashboard')">{{ t('buildingDetail.backToDashboard') }}</button>
    </div>

    <template v-else-if="building">
      <div class="building-header">
        <div class="building-title">
          <h1>{{ building.name }}</h1>
          <span class="building-type-badge">{{ formatBuildingType(building.type) }}</span>
        </div>
        <div class="building-meta">
          <span class="meta-pill">
            <span class="meta-label">{{ t('common.level') }}</span>
            <span class="meta-value">{{ building.level }}</span>
          </span>
          <span class="meta-pill">
            <span class="meta-label">{{ t('buildings.power') }}</span>
            <span class="meta-value">{{ building.powerConsumption }} MW</span>
          </span>
          <span class="meta-pill" :class="building.isForSale ? 'for-sale' : ''">
            {{ building.isForSale ? t('buildingDetail.forSale') : t('buildingDetail.notForSale') }}
          </span>
        </div>
      </div>

      <!-- Unit Grid -->
      <div class="grid-section">
        <div class="grid-header">
          <h2>{{ t('buildingDetail.unitGrid') }}</h2>
          <div class="grid-actions">
            <button v-if="!editMode" class="btn btn-primary" @click="editMode = true">{{ t('buildingDetail.editLinks') }}</button>
            <button v-else class="btn btn-success" @click="saveChanges">{{ t('buildingDetail.saveChanges') }}</button>
            <button v-if="editMode" class="btn btn-secondary" @click="editMode = false">{{ t('common.cancel') }}</button>
          </div>
        </div>

        <div class="unit-grid" :class="{ 'edit-mode': editMode }">
          <template v-for="y in gridIndexes" :key="`row-${y}`">
            <div class="grid-row unit-row">
              <template v-for="x in gridIndexes" :key="`unit-${x}-${y}`">
                <button
              class="grid-cell"
              :class="{
                occupied: !!getUnitAt(x, y),
                selected: selectedCell?.x === x && selectedCell?.y === y,
              }"
              :style="getUnitAt(x, y)
                ? { borderColor: getUnitColor(getUnitAt(x, y)!.unitType), background: getUnitColor(getUnitAt(x, y)!.unitType) + '18' }
                : {}"
              @click="clickCell(x, y)"
            >
              <template v-if="getUnitAt(x, y)">
                <span class="cell-type">{{ t(`buildingDetail.unitTypes.${getUnitAt(x, y)!.unitType}`) }}</span>
                <span class="cell-level">Lv.{{ getUnitAt(x, y)!.level }}</span>
                <span v-if="pendingTicks[getUnitAt(x, y)!.id]" class="cell-pending">{{ pendingTicks[getUnitAt(x, y)!.id] }} ticks</span>
              </template>
              <template v-else>
                <span class="cell-empty">+</span>
              </template>
            </button>

                <button
                  v-if="x < 3"
                  :key="`horizontal-${x}-${y}`"
                  class="link-toggle horizontal"
                  :class="{
                    active: isHorizontalLinkActive(x, y),
                    disabled: !canToggleHorizontalLink(x, y),
                  }"
                  :disabled="!editMode || !canToggleHorizontalLink(x, y)"
                  :aria-label="t('buildingDetail.linkRight')"
                  @click="toggleHorizontalLink(x, y)"
                >
                  <span class="link-line"></span>
                </button>
              </template>
            </div>

            <div v-if="y < 3" class="grid-row connector-row">
              <template v-for="x in gridIndexes" :key="`connector-${x}-${y}`">
                <button
                  class="link-toggle vertical"
                  :class="{
                    active: isVerticalLinkActive(x, y),
                    disabled: !canToggleVerticalLink(x, y),
                  }"
                  :disabled="!editMode || !canToggleVerticalLink(x, y)"
                  :aria-label="t('buildingDetail.linkDown')"
                  @click="toggleVerticalLink(x, y)"
                >
                  <span class="link-line"></span>
                </button>

                <button
                  v-if="x < 3"
                  :key="`diagonal-${x}-${y}`"
                  class="link-toggle diagonal"
                  :class="[
                    `state-${getDiagonalState(x, y)}`,
                    { disabled: !canToggleDiagonalLink(x, y) },
                  ]"
                  :disabled="!editMode || !canToggleDiagonalLink(x, y)"
                  :aria-label="t('buildingDetail.links')"
                  @click="toggleDiagonalLink(x, y)"
                >
                  <span class="diag-line diag-line-primary"></span>
                  <span class="diag-line diag-line-secondary"></span>
                </button>
              </template>
            </div>
          </template>
        </div>

        <!-- Link indicators between cells -->
        <div class="grid-legend">
          <div v-for="unitType in allowedUnits" :key="unitType" class="legend-item">
            <span class="legend-color" :style="{ background: getUnitColor(unitType) }"></span>
            <span>{{ t(`buildingDetail.unitTypes.${unitType}`) }}</span>
          </div>
        </div>
      </div>

      <!-- Unit Picker Modal -->
      <div v-if="showUnitPicker && selectedCell" class="unit-picker-overlay" @click.self="showUnitPicker = false">
        <div class="unit-picker">
          <div class="picker-header">
            <h3>{{ t('buildingDetail.selectUnitType') }}</h3>
            <button class="btn btn-ghost" @click="showUnitPicker = false">{{ t('common.close') }}</button>
          </div>
          <p class="picker-subtitle">{{ t('buildingDetail.allowedUnits') }}</p>
          <div class="picker-grid">
            <button
              v-for="unitType in allowedUnits"
              :key="unitType"
              class="picker-option"
              @click="placeUnit(unitType)"
            >
              <span class="picker-color" :style="{ background: getUnitColor(unitType) }"></span>
              <div class="picker-info">
                <span class="picker-name">{{ t(`buildingDetail.unitTypes.${unitType}`) }}</span>
                <span class="picker-desc">{{ t(`buildingDetail.unitDescriptions.${unitType}`) }}</span>
              </div>
            </button>
          </div>
        </div>
      </div>

      <!-- Selected unit detail -->
      <div v-if="selectedCell && getUnitAt(selectedCell.x, selectedCell.y) && !showUnitPicker" class="unit-detail">
        <h3>{{ t(`buildingDetail.unitTypes.${getUnitAt(selectedCell.x, selectedCell.y)!.unitType}`) }}</h3>
        <p class="unit-desc">{{ t(`buildingDetail.unitDescriptions.${getUnitAt(selectedCell.x, selectedCell.y)!.unitType}`) }}</p>
        <div class="unit-stats">
          <span class="stat">{{ t('common.level') }}: {{ getUnitAt(selectedCell.x, selectedCell.y)!.level }}</span>
          <span class="stat">Grid: ({{ selectedCell.x }}, {{ selectedCell.y }})</span>
        </div>
        <div class="unit-links">
          <span class="link-label">{{ t('buildingDetail.links') }}:</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkUp" class="link-badge">{{ t('buildingDetail.linkUp') }}</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkDown" class="link-badge">{{ t('buildingDetail.linkDown') }}</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkLeft" class="link-badge">{{ t('buildingDetail.linkLeft') }}</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkRight" class="link-badge">{{ t('buildingDetail.linkRight') }}</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkUpLeft" class="link-badge">{{ t('buildingDetail.linkUpLeft') }}</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkUpRight" class="link-badge">{{ t('buildingDetail.linkUpRight') }}</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkDownLeft" class="link-badge">{{ t('buildingDetail.linkDownLeft') }}</span>
          <span v-if="getUnitAt(selectedCell.x, selectedCell.y)!.linkDownRight" class="link-badge">{{ t('buildingDetail.linkDownRight') }}</span>
        </div>
        <div class="unit-actions">
          <button class="btn btn-danger btn-sm" @click="removeUnit(selectedCell.x, selectedCell.y)">
            {{ t('buildingDetail.removeUnit') }}
          </button>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.building-detail-view {
  padding: 2rem 1rem;
  max-width: 800px;
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

.building-header {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
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

.building-meta {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.meta-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.25rem 0.75rem;
  background: var(--color-bg);
  border-radius: 9999px;
  font-size: 0.8125rem;
}

.meta-pill.for-sale {
  background: rgba(0, 200, 83, 0.1);
  color: var(--color-secondary);
}

.meta-label {
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.meta-value {
  font-weight: 600;
}

.grid-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.grid-section h2 {
  font-size: 1.125rem;
  margin-bottom: 1rem;
}

.unit-grid {
  display: grid;
  gap: 1rem;
  margin-bottom: 1rem;
}

.grid-row {
  display: grid;
  grid-template-columns: minmax(92px, 1fr) 44px minmax(92px, 1fr) 44px minmax(92px, 1fr) 44px minmax(92px, 1fr);
  align-items: center;
  justify-items: center;
  column-gap: 0;
}

.connector-row {
  min-height: 44px;
}

.grid-cell {
  aspect-ratio: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  cursor: pointer;
  transition: all 0.15s ease;
  padding: 0.5rem;
  color: var(--color-text);
  min-height: 80px;
}

.link-toggle {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 92%, white 8%);
  cursor: pointer;
  transition: transform 0.15s ease, border-color 0.15s ease, background 0.15s ease, box-shadow 0.15s ease;
}

.link-toggle:disabled {
  cursor: default;
  opacity: 0.45;
}

.link-toggle:not(:disabled):hover {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(19, 127, 236, 0.12);
}

.link-toggle.horizontal {
  width: 36px;
  height: 18px;
  border-radius: 999px;
}

.link-toggle.vertical {
  width: 18px;
  height: 36px;
  border-radius: 999px;
}

.link-toggle.diagonal {
  width: 36px;
  height: 36px;
  border-radius: 14px;
}

.link-line {
  display: block;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-border) 82%, transparent);
}

.horizontal .link-line {
  width: 24px;
  height: 4px;
}

.vertical .link-line {
  width: 4px;
  height: 24px;
}

.link-toggle.active .link-line {
  background: var(--color-primary);
}

.diag-line {
  position: absolute;
  width: 26px;
  height: 3px;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-border) 82%, transparent);
  opacity: 0;
}

.diag-line-primary {
  transform: rotate(45deg);
}

.diag-line-secondary {
  transform: rotate(-45deg);
}

.diagonal.state-tl-br .diag-line-primary,
.diagonal.state-tr-bl .diag-line-secondary {
  opacity: 1;
  background: var(--color-primary);
}

.diagonal.state-none .diag-line-primary,
.diagonal.state-none .diag-line-secondary {
  opacity: 0.35;
}

.grid-cell:hover {
  border-color: var(--color-primary);
}

.grid-cell.selected {
  box-shadow: 0 0 0 2px var(--color-primary);
}

.grid-cell.occupied {
  background: var(--color-surface-raised);
}

.cell-type {
  font-size: 0.6875rem;
  font-weight: 700;
  text-align: center;
  line-height: 1.2;
}

.cell-level {
  font-size: 0.625rem;
  color: var(--color-text-secondary);
}

.cell-pending {
  color: orange;
  font-size: 0.625rem;
  margin-top: 0.125rem;
}

.cell-empty {
  font-size: 1.25rem;
  color: var(--color-text-secondary);
  opacity: 0.5;
}

.grid-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.legend-color {
  width: 12px;
  height: 12px;
  border-radius: 3px;
}

.unit-picker-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
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
  width: 90%;
  max-width: 480px;
}

.picker-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.picker-header h3 {
  font-size: 1.125rem;
}

.picker-subtitle {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin-bottom: 1rem;
}

.picker-grid {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.picker-option {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  cursor: pointer;
  transition: all 0.15s ease;
  color: var(--color-text);
  text-align: left;
}

.picker-option:hover {
  border-color: var(--color-primary);
  background: rgba(0, 71, 255, 0.04);
}

.picker-color {
  width: 16px;
  height: 16px;
  border-radius: 4px;
  flex-shrink: 0;
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

.unit-detail {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
}

.unit-detail h3 {
  font-size: 1.125rem;
  margin-bottom: 0.25rem;
}

.unit-desc {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.75rem;
}

.unit-stats {
  display: flex;
  gap: 1rem;
  margin-bottom: 0.75rem;
}

.stat {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.unit-links {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.link-label {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.link-badge {
  background: rgba(0, 71, 255, 0.1);
  color: var(--color-primary);
  padding: 0.125rem 0.5rem;
  border-radius: 9999px;
  font-size: 0.6875rem;
  font-weight: 500;
}

.unit-actions {
  display: flex;
  gap: 0.5rem;
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
  color: var(--color-text-secondary);
}

@media (max-width: 720px) {
  .grid-row {
    grid-template-columns: minmax(68px, 1fr) 34px minmax(68px, 1fr) 34px minmax(68px, 1fr) 34px minmax(68px, 1fr);
  }

  .grid-cell {
    min-height: 68px;
    padding: 0.375rem;
  }

  .link-toggle.horizontal {
    width: 28px;
  }

  .link-toggle.vertical {
    height: 28px;
  }

  .link-toggle.diagonal {
    width: 30px;
    height: 30px;
  }
}
</style>
