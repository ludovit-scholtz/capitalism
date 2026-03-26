<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import type { Building, BuildingConfigurationPlanRemoval, BuildingConfigurationPlanUnit, BuildingUnit } from '@/types'

type GridUnit = BuildingUnit | BuildingConfigurationPlanUnit | EditableGridUnit

type EditableGridUnit = {
  id: string
  unitType: string
  gridX: number
  gridY: number
  level: number
  linkUp: boolean
  linkDown: boolean
  linkLeft: boolean
  linkRight: boolean
  linkUpLeft: boolean
  linkUpRight: boolean
  linkDownLeft: boolean
  linkDownRight: boolean
}

const LINK_CHANGE_TICKS = 1
const UNIT_PLAN_CHANGE_TICKS = 3
const gridIndexes = [0, 1, 2, 3] as const

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const buildingId = computed(() => route.params.id as string)
const building = ref<Building | null>(null)
const currentTick = ref(0)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const isEditing = ref(false)
const selectedCell = ref<{ x: number; y: number } | null>(null)
const showUnitPicker = ref(false)
const draftUnits = ref<EditableGridUnit[]>([])
const editBaselineUnits = ref<EditableGridUnit[]>([])

const allowedUnitsMap: Record<string, string[]> = {
  MINE: ['MINING', 'STORAGE', 'B2B_SALES'],
  FACTORY: ['PURCHASE', 'MANUFACTURING', 'BRANDING', 'STORAGE', 'B2B_SALES'],
  SALES_SHOP: ['PURCHASE', 'MARKETING', 'PUBLIC_SALES'],
  RESEARCH_DEVELOPMENT: ['PRODUCT_QUALITY', 'BRAND_QUALITY'],
}

const unitColors: Record<string, string> = {
  MINING: '#ff6d00',
  STORAGE: '#8b949e',
  B2B_SALES: '#00c853',
  PURCHASE: '#0047ff',
  MANUFACTURING: '#ff6d00',
  BRANDING: '#9333ea',
  MARKETING: '#ec4899',
  PUBLIC_SALES: '#00c853',
  PRODUCT_QUALITY: '#0047ff',
  BRAND_QUALITY: '#9333ea',
}

const activeUnits = computed(() => building.value?.units ?? [])
const pendingConfiguration = computed(() => building.value?.pendingConfiguration ?? null)
const pendingUnits = computed(() => pendingConfiguration.value?.units ?? [])
const pendingRemovals = computed(() => pendingConfiguration.value?.removals ?? [])
const plannedUnits = computed<GridUnit[]>(() => (isEditing.value ? draftUnits.value : pendingUnits.value))
const allowedUnits = computed(() => {
  if (!building.value) return []
  return allowedUnitsMap[building.value.type] || []
})
const isUpgradeInProgress = computed(() => pendingConfiguration.value !== null)
const showPlanningSection = computed(() => isEditing.value)
const remainingUpgradeTicks = computed(() => {
  if (!pendingConfiguration.value) return 0
  return Math.max(pendingConfiguration.value.appliesAtTick - currentTick.value, 0)
})
const draftTotalTicks = computed(() => {
  const positions = new Set<string>()

  for (const unit of activeUnits.value) positions.add(`${unit.gridX},${unit.gridY}`)
  for (const unit of draftUnits.value) positions.add(`${unit.gridX},${unit.gridY}`)
  for (const unit of pendingUnits.value) positions.add(`${unit.gridX},${unit.gridY}`)
  for (const removal of pendingRemovals.value) positions.add(`${removal.gridX},${removal.gridY}`)

  return Array.from(positions).reduce((maxTicks, position) => {
    const [gridX = 0, gridY = 0] = position.split(',').map(Number)
    return Math.max(maxTicks, getDraftTicksAt(gridX, gridY))
  }, 0)
})
const hasDraftChanges = computed(() => !areUnitCollectionsEqual(draftUnits.value, editBaselineUnits.value))

function formatBuildingType(type: string): string {
  return type.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase())
}

function getUnitColor(unitType: string): string {
  return unitColors[unitType] || '#8b949e'
}

function getUnitAtFrom(units: GridUnit[], x: number, y: number): GridUnit | undefined {
  return units.find((unit) => unit.gridX === x && unit.gridY === y)
}

function getDraftUnitAt(x: number, y: number): EditableGridUnit | undefined {
  return draftUnits.value.find((unit) => unit.gridX === x && unit.gridY === y)
}

function getEditBaselineUnitAt(x: number, y: number): EditableGridUnit | undefined {
  return editBaselineUnits.value.find((unit) => unit.gridX === x && unit.gridY === y)
}

function getPendingUnitAt(x: number, y: number): BuildingConfigurationPlanUnit | undefined {
  return pendingUnits.value.find((unit) => unit.gridX === x && unit.gridY === y)
}

function getPendingRemovalAt(x: number, y: number): BuildingConfigurationPlanRemoval | undefined {
  return pendingRemovals.value.find((removal) => removal.gridX === x && removal.gridY === y)
}

function cloneUnit(unit: GridUnit): EditableGridUnit {
  return {
    id: unit.id,
    unitType: unit.unitType,
    gridX: unit.gridX,
    gridY: unit.gridY,
    level: unit.level,
    linkUp: unit.linkUp,
    linkDown: unit.linkDown,
    linkLeft: unit.linkLeft,
    linkRight: unit.linkRight,
    linkUpLeft: unit.linkUpLeft,
    linkUpRight: unit.linkUpRight,
    linkDownLeft: unit.linkDownLeft,
    linkDownRight: unit.linkDownRight,
  }
}

function setDraftUnitsFrom(sourceUnits: GridUnit[]) {
  draftUnits.value = sourceUnits.map((unit) => cloneUnit(unit))
}

function setEditBaselineFrom(sourceUnits: GridUnit[]) {
  editBaselineUnits.value = sourceUnits.map((unit) => cloneUnit(unit))
}

function getEditingSourceUnits(): GridUnit[] {
  return pendingConfiguration.value?.units ?? activeUnits.value
}

function startEditing() {
  const sourceUnits = getEditingSourceUnits()
  setDraftUnitsFrom(sourceUnits)
  setEditBaselineFrom(sourceUnits)
  isEditing.value = true
  selectedCell.value = null
  showUnitPicker.value = false
}

function cancelEditing() {
  const sourceUnits = getEditingSourceUnits()
  setDraftUnitsFrom(sourceUnits)
  setEditBaselineFrom(sourceUnits)
  isEditing.value = false
  selectedCell.value = null
  showUnitPicker.value = false
}

function clickDraftCell(x: number, y: number) {
  if (!isEditing.value) {
    return
  }

  const existing = getDraftUnitAt(x, y)
  selectedCell.value = { x, y }
  showUnitPicker.value = !existing
}

function placeUnit(unitType: string) {
  if (!selectedCell.value || !isEditing.value) return

  const newUnit: EditableGridUnit = {
    id: `draft-${selectedCell.value.x}-${selectedCell.value.y}-${Date.now()}`,
    unitType,
    gridX: selectedCell.value.x,
    gridY: selectedCell.value.y,
    level: getUnitAtFrom(activeUnits.value, selectedCell.value.x, selectedCell.value.y)?.level ?? 1,
    linkUp: false,
    linkDown: false,
    linkLeft: false,
    linkRight: false,
    linkUpLeft: false,
    linkUpRight: false,
    linkDownLeft: false,
    linkDownRight: false,
  }

  draftUnits.value = [...draftUnits.value.filter((unit) => !(unit.gridX === newUnit.gridX && unit.gridY === newUnit.gridY)), newUnit]
  selectedCell.value = null
  showUnitPicker.value = false
}

function clearConnectionsAround(x: number, y: number) {
  const left = getDraftUnitAt(x - 1, y)
  const right = getDraftUnitAt(x + 1, y)
  const up = getDraftUnitAt(x, y - 1)
  const down = getDraftUnitAt(x, y + 1)
  const upLeft = getDraftUnitAt(x - 1, y - 1)
  const upRight = getDraftUnitAt(x + 1, y - 1)
  const downLeft = getDraftUnitAt(x - 1, y + 1)
  const downRight = getDraftUnitAt(x + 1, y + 1)

  if (left) left.linkRight = false
  if (right) right.linkLeft = false
  if (up) up.linkDown = false
  if (down) down.linkUp = false
  if (upLeft) upLeft.linkDownRight = false
  if (upRight) upRight.linkDownLeft = false
  if (downLeft) downLeft.linkUpRight = false
  if (downRight) downRight.linkUpLeft = false
}

function removeDraftUnit(x: number, y: number) {
  if (!isEditing.value) return

  clearConnectionsAround(x, y)
  draftUnits.value = draftUnits.value.filter((unit) => !(unit.gridX === x && unit.gridY === y))
  selectedCell.value = null
  showUnitPicker.value = false
}

function toggleHorizontalLink(x: number, y: number) {
  if (!isEditing.value) return

  const left = getDraftUnitAt(x, y)
  const right = getDraftUnitAt(x + 1, y)
  if (!left || !right) return

  const next = !left.linkRight
  left.linkRight = next
  right.linkLeft = next
}

function toggleVerticalLink(x: number, y: number) {
  if (!isEditing.value) return

  const top = getDraftUnitAt(x, y)
  const bottom = getDraftUnitAt(x, y + 1)
  if (!top || !bottom) return

  const next = !top.linkDown
  top.linkDown = next
  bottom.linkUp = next
}

function clearDiagonalState(units: EditableGridUnit[], x: number, y: number) {
  const topLeft = getUnitAtFrom(units, x, y) as EditableGridUnit | undefined
  const topRight = getUnitAtFrom(units, x + 1, y) as EditableGridUnit | undefined
  const bottomLeft = getUnitAtFrom(units, x, y + 1) as EditableGridUnit | undefined
  const bottomRight = getUnitAtFrom(units, x + 1, y + 1) as EditableGridUnit | undefined

  if (topLeft) topLeft.linkDownRight = false
  if (bottomRight) bottomRight.linkUpLeft = false
  if (topRight) topRight.linkDownLeft = false
  if (bottomLeft) bottomLeft.linkUpRight = false
}

function getDiagonalStateFor(units: GridUnit[], x: number, y: number): 'none' | 'tl-br' | 'tr-bl' | 'cross' {
  const topLeft = getUnitAtFrom(units, x, y)
  const topRight = getUnitAtFrom(units, x + 1, y)
  const hasTopLeftToBottomRight = !!topLeft?.linkDownRight
  const hasTopRightToBottomLeft = !!topRight?.linkDownLeft

  if (hasTopLeftToBottomRight && hasTopRightToBottomLeft) return 'cross'
  if (hasTopLeftToBottomRight) return 'tl-br'
  if (hasTopRightToBottomLeft) return 'tr-bl'
  return 'none'
}

function toggleDiagonalLink(x: number, y: number) {
  if (!isEditing.value) return

  const topLeft = getDraftUnitAt(x, y)
  const topRight = getDraftUnitAt(x + 1, y)
  const bottomLeft = getDraftUnitAt(x, y + 1)
  const bottomRight = getDraftUnitAt(x + 1, y + 1)
  if (!topLeft || !topRight || !bottomLeft || !bottomRight) return

  const currentState = getDiagonalStateFor(draftUnits.value, x, y)
  clearDiagonalState(draftUnits.value, x, y)

  if (currentState === 'none') {
    topLeft.linkDownRight = true
    bottomRight.linkUpLeft = true
    return
  }

  if (currentState === 'tl-br') {
    topRight.linkDownLeft = true
    bottomLeft.linkUpRight = true
    return
  }

  if (currentState === 'tr-bl') {
    topLeft.linkDownRight = true
    bottomRight.linkUpLeft = true
    topRight.linkDownLeft = true
    bottomLeft.linkUpRight = true
  }
}

function isHorizontalLinkActiveFor(units: GridUnit[], x: number, y: number): boolean {
  return getUnitAtFrom(units, x, y)?.linkRight || false
}

function isVerticalLinkActiveFor(units: GridUnit[], x: number, y: number): boolean {
  return getUnitAtFrom(units, x, y)?.linkDown || false
}

function canToggleHorizontalLink(units: GridUnit[], x: number, y: number): boolean {
  return !!getUnitAtFrom(units, x, y) && !!getUnitAtFrom(units, x + 1, y)
}

function canToggleVerticalLink(units: GridUnit[], x: number, y: number): boolean {
  return !!getUnitAtFrom(units, x, y) && !!getUnitAtFrom(units, x, y + 1)
}

function canToggleDiagonalLink(units: GridUnit[], x: number, y: number): boolean {
  return !!getUnitAtFrom(units, x, y)
    && !!getUnitAtFrom(units, x + 1, y)
    && !!getUnitAtFrom(units, x, y + 1)
    && !!getUnitAtFrom(units, x + 1, y + 1)
}

function getDraftTicksForUnit(unit: EditableGridUnit): number {
  const baselinePendingUnit = getPendingUnitAt(unit.gridX, unit.gridY)
  const baselinePendingRemoval = getPendingRemovalAt(unit.gridX, unit.gridY)
  const activeUnit = getUnitAtFrom(activeUnits.value, unit.gridX, unit.gridY) as BuildingUnit | undefined

  if (baselinePendingUnit && areUnitsEquivalent(baselinePendingUnit, unit)) {
    return getRemainingTicksFromApplyTick(baselinePendingUnit.appliesAtTick)
  }

  if (baselinePendingRemoval && activeUnit && areUnitsEquivalent(activeUnit, unit)) {
    return getCancelTicks(baselinePendingRemoval.ticksRequired)
  }

  if (baselinePendingUnit && activeUnit && areUnitsEquivalent(activeUnit, unit)) {
    return getCancelTicks(baselinePendingUnit.ticksRequired)
  }

  if (!activeUnit) return UNIT_PLAN_CHANGE_TICKS

  if (activeUnit.unitType !== unit.unitType) {
    return UNIT_PLAN_CHANGE_TICKS
  }

  if (
    activeUnit.linkUp !== unit.linkUp
    || activeUnit.linkDown !== unit.linkDown
    || activeUnit.linkLeft !== unit.linkLeft
    || activeUnit.linkRight !== unit.linkRight
    || activeUnit.linkUpLeft !== unit.linkUpLeft
    || activeUnit.linkUpRight !== unit.linkUpRight
    || activeUnit.linkDownLeft !== unit.linkDownLeft
    || activeUnit.linkDownRight !== unit.linkDownRight
  ) {
    return LINK_CHANGE_TICKS
  }

  return 0
}

function getDraftTicksAt(x: number, y: number): number {
  const draftUnit = getDraftUnitAt(x, y)
  if (draftUnit) {
    return getDraftTicksForUnit(draftUnit)
  }

  const pendingRemoval = getPendingRemovalAt(x, y)
  if (pendingRemoval) {
    return getRemainingTicksFromApplyTick(pendingRemoval.appliesAtTick)
  }

  const pendingUnit = getPendingUnitAt(x, y)
  if (pendingUnit) {
    return getCancelTicks(pendingUnit.ticksRequired)
  }

  return getUnitAtFrom(activeUnits.value, x, y) ? UNIT_PLAN_CHANGE_TICKS : 0
}

function getDisplayedTicks(unit: GridUnit): number {
  if ('appliesAtTick' in unit && 'isChanged' in unit && unit.isChanged) {
    return getRemainingTicksFromApplyTick(unit.appliesAtTick)
  }

  return getDraftTicksForUnit(unit as EditableGridUnit)
}

function getRemainingTicksFromApplyTick(appliesAtTick: number): number {
  return Math.max(appliesAtTick - currentTick.value, 0)
}

function getCancelTicks(baseTicks: number): number {
  return Math.max(Math.ceil(baseTicks * 0.1), 1)
}

function areUnitsEquivalent(
  left: Pick<EditableGridUnit, 'unitType' | 'gridX' | 'gridY' | 'linkUp' | 'linkDown' | 'linkLeft' | 'linkRight' | 'linkUpLeft' | 'linkUpRight' | 'linkDownLeft' | 'linkDownRight'>,
  right: Pick<EditableGridUnit, 'unitType' | 'gridX' | 'gridY' | 'linkUp' | 'linkDown' | 'linkLeft' | 'linkRight' | 'linkUpLeft' | 'linkUpRight' | 'linkDownLeft' | 'linkDownRight'>,
): boolean {
  return left.unitType === right.unitType
    && left.gridX === right.gridX
    && left.gridY === right.gridY
    && left.linkUp === right.linkUp
    && left.linkDown === right.linkDown
    && left.linkLeft === right.linkLeft
    && left.linkRight === right.linkRight
    && left.linkUpLeft === right.linkUpLeft
    && left.linkUpRight === right.linkUpRight
    && left.linkDownLeft === right.linkDownLeft
    && left.linkDownRight === right.linkDownRight
}

function areUnitCollectionsEqual(left: EditableGridUnit[], right: EditableGridUnit[]): boolean {
  if (left.length !== right.length) {
    return false
  }

  const sortedLeft = [...left].sort(compareUnits)
  const sortedRight = [...right].sort(compareUnits)

  return sortedLeft.every((unit, index) => areUnitsEquivalent(unit, sortedRight[index]!))
}

function compareUnits(left: EditableGridUnit, right: EditableGridUnit): number {
  if (left.gridY !== right.gridY) return left.gridY - right.gridY
  if (left.gridX !== right.gridX) return left.gridX - right.gridX
  return left.unitType.localeCompare(right.unitType)
}

function storeConfiguration() {
  if (!building.value || saving.value || !hasDraftChanges.value) return

  saving.value = true
  error.value = null

  gqlRequest<{
    storeBuildingConfiguration: {
      id: string
      appliesAtTick: number
      totalTicksRequired: number
    }
  }>(
    `mutation StoreBuildingConfiguration($input: StoreBuildingConfigurationInput!) {
      storeBuildingConfiguration(input: $input) {
        id
        appliesAtTick
        totalTicksRequired
      }
    }`,
    {
      input: {
        buildingId: building.value.id,
        units: draftUnits.value.map((unit) => ({
          unitType: unit.unitType,
          gridX: unit.gridX,
          gridY: unit.gridY,
          linkUp: unit.linkUp,
          linkDown: unit.linkDown,
          linkLeft: unit.linkLeft,
          linkRight: unit.linkRight,
          linkUpLeft: unit.linkUpLeft,
          linkUpRight: unit.linkUpRight,
          linkDownLeft: unit.linkDownLeft,
          linkDownRight: unit.linkDownRight,
        })),
      },
    },
  )
    .then(() => {
      isEditing.value = false
      return loadBuilding()
    })
    .catch((reason: unknown) => {
      error.value = reason instanceof Error ? reason.message : t('buildingDetail.storeUpgradeFailed')
    })
    .finally(() => {
      saving.value = false
    })
}

async function loadBuilding() {
  try {
    loading.value = true
    error.value = null

    const [companiesData, gameStateData] = await Promise.all([
      gqlRequest<{ myCompanies: { buildings: Building[] }[] }>(
        `{ myCompanies {
          buildings {
            id
            companyId
            cityId
            type
            name
            latitude
            longitude
            level
            powerConsumption
            isForSale
            askingPrice
            pricePerSqm
            occupancyPercent
            totalAreaSqm
            powerPlantType
            powerOutput
            mediaType
            interestRate
            builtAtUtc
            units {
              id
              buildingId
              unitType
              gridX
              gridY
              level
              linkUp
              linkDown
              linkLeft
              linkRight
              linkUpLeft
              linkUpRight
              linkDownLeft
              linkDownRight
            }
            pendingConfiguration {
              id
              buildingId
              submittedAtUtc
              submittedAtTick
              appliesAtTick
              totalTicksRequired
              removals {
                id
                gridX
                gridY
                startedAtTick
                appliesAtTick
                ticksRequired
                isReverting
              }
              units {
                id
                unitType
                gridX
                gridY
                level
                linkUp
                linkDown
                linkLeft
                linkRight
                linkUpLeft
                linkUpRight
                linkDownLeft
                linkDownRight
                startedAtTick
                appliesAtTick
                ticksRequired
                isChanged
                isReverting
              }
            }
          }
        } }`,
      ),
      gqlRequest<{ gameState: { currentTick: number } | null }>(`{ gameState { currentTick } }`),
    ])

    currentTick.value = gameStateData.gameState?.currentTick ?? 0

    const allBuildings = companiesData.myCompanies.flatMap((company) => company.buildings)
    building.value = allBuildings.find((candidate) => candidate.id === buildingId.value) || null

    if (!building.value) {
      error.value = t('buildingDetail.notFound')
      return
    }

    const sourceUnits = pendingConfiguration.value?.units ?? building.value.units
    setDraftUnitsFrom(sourceUnits)
    setEditBaselineFrom(sourceUnits)
    isEditing.value = false
    selectedCell.value = null
    showUnitPicker.value = false
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : t('buildingDetail.loadFailed')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  await loadBuilding()
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

      <div v-if="isUpgradeInProgress" class="upgrade-banner" role="status">
        <div>
          <strong>{{ t('buildingDetail.upgradeQueuedTitle') }}</strong>
          <p>{{ t('buildingDetail.upgradeQueuedBody', { ticks: remainingUpgradeTicks }) }}</p>
        </div>
        <div class="upgrade-pill">
          {{ t('buildingDetail.upgradeAppliesAt', { tick: pendingConfiguration!.appliesAtTick }) }}
        </div>
      </div>

      <div class="grid-section">
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
                  class="grid-cell readonly"
                  :class="{ occupied: !!getUnitAtFrom(activeUnits, x, y) }"
                  :style="getUnitAtFrom(activeUnits, x, y)
                    ? { borderColor: getUnitColor(getUnitAtFrom(activeUnits, x, y)!.unitType), background: getUnitColor(getUnitAtFrom(activeUnits, x, y)!.unitType) + '18' }
                    : {}"
                >
                  <template v-if="getUnitAtFrom(activeUnits, x, y)">
                    <span class="cell-type">{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(activeUnits, x, y)!.unitType}`) }}</span>
                    <span class="cell-level">Lv.{{ getUnitAtFrom(activeUnits, x, y)!.level }}</span>
                  </template>
                  <template v-else>
                    <span class="cell-empty">+</span>
                  </template>
                </div>

                <div
                  v-if="x < 3"
                  :key="`active-horizontal-${x}-${y}`"
                  class="link-toggle horizontal readonly"
                  :class="{ active: isHorizontalLinkActiveFor(activeUnits, x, y), disabled: !canToggleHorizontalLink(activeUnits, x, y) }"
                >
                  <span class="link-line"></span>
                </div>
              </template>
            </div>

            <div v-if="y < 3" class="grid-row connector-row">
              <template v-for="x in gridIndexes" :key="`active-connector-${x}-${y}`">
                <div
                  class="link-toggle vertical readonly"
                  :class="{ active: isVerticalLinkActiveFor(activeUnits, x, y), disabled: !canToggleVerticalLink(activeUnits, x, y) }"
                >
                  <span class="link-line"></span>
                </div>

                <div
                  v-if="x < 3"
                  :key="`active-diagonal-${x}-${y}`"
                  class="link-toggle diagonal readonly"
                  :class="[`state-${getDiagonalStateFor(activeUnits, x, y)}`, { disabled: !canToggleDiagonalLink(activeUnits, x, y) }]"
                >
                  <span class="diag-line diag-line-primary"></span>
                  <span class="diag-line diag-line-secondary"></span>
                </div>
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
            <button
              class="btn btn-primary"
              :disabled="saving || !hasDraftChanges"
              @click="storeConfiguration"
            >
              {{ saving ? t('common.loading') : t('buildingDetail.storeConfiguration') }}
            </button>
          </div>
        </div>

        <div class="upgrade-summary">
          <span class="upgrade-summary-pill">{{ t('buildingDetail.currentTickLabel', { tick: currentTick }) }}</span>
          <span class="upgrade-summary-pill">{{ t('buildingDetail.totalUpgradeTicks', { ticks: draftTotalTicks }) }}</span>
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
                  }"
                  :style="getUnitAtFrom(plannedUnits, x, y)
                    ? { borderColor: getUnitColor(getUnitAtFrom(plannedUnits, x, y)!.unitType), background: getUnitColor(getUnitAtFrom(plannedUnits, x, y)!.unitType) + '18' }
                    : {}"
                  @click="clickDraftCell(x, y)"
                >
                  <template v-if="getUnitAtFrom(plannedUnits, x, y)">
                    <span class="cell-type">{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(plannedUnits, x, y)!.unitType}`) }}</span>
                    <span class="cell-level">Lv.{{ getUnitAtFrom(plannedUnits, x, y)!.level }}</span>
                    <span v-if="getDisplayedTicks(getUnitAtFrom(plannedUnits, x, y)!) > 0" class="cell-pending">
                      {{ t('buildingDetail.unitUnavailableFor', { ticks: getDisplayedTicks(getUnitAtFrom(plannedUnits, x, y)!) }) }}
                    </span>
                  </template>
                  <template v-else>
                    <span class="cell-empty">+</span>
                  </template>
                </button>

                <button
                  v-if="x < 3"
                  :key="`planned-horizontal-${x}-${y}`"
                  class="link-toggle horizontal"
                  :class="{ active: isHorizontalLinkActiveFor(plannedUnits, x, y), disabled: !canToggleHorizontalLink(plannedUnits, x, y) }"
                  :disabled="!canToggleHorizontalLink(plannedUnits, x, y)"
                  :aria-label="t('buildingDetail.linkRight')"
                  @click="toggleHorizontalLink(x, y)"
                >
                  <span class="link-line"></span>
                </button>
              </template>
            </div>

            <div v-if="y < 3" class="grid-row connector-row">
              <template v-for="x in gridIndexes" :key="`planned-connector-${x}-${y}`">
                <button
                  class="link-toggle vertical"
                  :class="{ active: isVerticalLinkActiveFor(plannedUnits, x, y), disabled: !canToggleVerticalLink(plannedUnits, x, y) }"
                  :disabled="!canToggleVerticalLink(plannedUnits, x, y)"
                  :aria-label="t('buildingDetail.linkDown')"
                  @click="toggleVerticalLink(x, y)"
                >
                  <span class="link-line"></span>
                </button>

                <button
                  v-if="x < 3"
                  :key="`planned-diagonal-${x}-${y}`"
                  class="link-toggle diagonal"
                  :class="[`state-${getDiagonalStateFor(plannedUnits, x, y)}`, { disabled: !canToggleDiagonalLink(plannedUnits, x, y) }]"
                  :disabled="!canToggleDiagonalLink(plannedUnits, x, y)"
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

        <div class="grid-legend">
          <div v-for="unitType in allowedUnits" :key="unitType" class="legend-item">
            <span class="legend-color" :style="{ background: getUnitColor(unitType) }"></span>
            <span>{{ t(`buildingDetail.unitTypes.${unitType}`) }}</span>
          </div>
        </div>
      </div>

      <div v-if="showUnitPicker && selectedCell && isEditing" class="unit-picker-overlay" @click.self="showUnitPicker = false">
        <div class="unit-picker">
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
              </div>
            </button>
          </div>
        </div>
      </div>

      <div v-if="showPlanningSection && selectedCell && getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y) && !showUnitPicker" class="unit-detail">
        <h3>{{ t(`buildingDetail.unitTypes.${getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.unitType}`) }}</h3>
        <p class="unit-desc">{{ t(`buildingDetail.unitDescriptions.${getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.unitType}`) }}</p>
        <div class="unit-stats">
          <span class="stat">{{ t('common.level') }}: {{ getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.level }}</span>
          <span class="stat">{{ t('buildingDetail.gridPosition', { x: selectedCell.x, y: selectedCell.y }) }}</span>
          <span class="stat" v-if="getDisplayedTicks(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!) > 0">
            {{ t('buildingDetail.unitUnavailableFor', { ticks: getDisplayedTicks(getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!) }) }}
          </span>
        </div>
        <div class="unit-links">
          <span class="link-label">{{ t('buildingDetail.links') }}:</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkUp" class="link-badge">{{ t('buildingDetail.linkUp') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkDown" class="link-badge">{{ t('buildingDetail.linkDown') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkLeft" class="link-badge">{{ t('buildingDetail.linkLeft') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkRight" class="link-badge">{{ t('buildingDetail.linkRight') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkUpLeft" class="link-badge">{{ t('buildingDetail.linkUpLeft') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkUpRight" class="link-badge">{{ t('buildingDetail.linkUpRight') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkDownLeft" class="link-badge">{{ t('buildingDetail.linkDownLeft') }}</span>
          <span v-if="getUnitAtFrom(plannedUnits, selectedCell.x, selectedCell.y)!.linkDownRight" class="link-badge">{{ t('buildingDetail.linkDownRight') }}</span>
        </div>
        <div class="unit-actions" v-if="isEditing">
          <button class="btn btn-danger btn-sm" @click="removeDraftUnit(selectedCell.x, selectedCell.y)">
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
  max-width: 1100px;
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

.building-header,
.grid-section,
.unit-detail,
.upgrade-banner {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  box-shadow: 0 18px 40px rgba(15, 23, 42, 0.06);
}

.building-header,
.grid-section,
.unit-detail {
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

.building-meta,
.upgrade-summary,
.grid-legend,
.unit-stats,
.unit-links,
.unit-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
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
  margin-bottom: 1.5rem;
  background: linear-gradient(135deg, rgba(19, 127, 236, 0.09), rgba(0, 200, 83, 0.08));
}

.upgrade-banner p {
  margin: 0.35rem 0 0;
  font-size: 0.875rem;
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
  gap: 0.9rem;
  margin-bottom: 1rem;
}

.grid-row {
  display: grid;
  grid-template-columns: minmax(92px, 1fr) 46px minmax(92px, 1fr) 46px minmax(92px, 1fr) 46px minmax(92px, 1fr);
  align-items: center;
  justify-items: center;
}

.connector-row {
  min-height: 44px;
}

.grid-cell {
  aspect-ratio: 1;
  width: 100%;
  min-height: 86px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
  padding: 0.6rem;
  border: 2px solid var(--color-border);
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.76), rgba(244, 247, 251, 0.92));
  color: var(--color-text);
  transition: border-color 0.15s ease, box-shadow 0.15s ease, transform 0.15s ease;
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
  text-align: center;
  line-height: 1.2;
}

.cell-level,
.cell-pending {
  font-size: 0.625rem;
}

.cell-pending {
  color: #b45309;
  text-align: center;
}

.cell-empty {
  font-size: 1.35rem;
  opacity: 0.45;
}

.link-toggle {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  background: color-mix(in srgb, var(--color-surface-raised, var(--color-surface)) 94%, white 6%);
  transition: border-color 0.15s ease, background 0.15s ease, box-shadow 0.15s ease;
}

.link-toggle:disabled,
.link-toggle.readonly {
  cursor: default;
}

.link-toggle:not(:disabled):not(.readonly):hover {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(19, 127, 236, 0.12);
}

.link-toggle.disabled {
  opacity: 0.28;
}

.link-toggle.horizontal {
  width: 38px;
  height: 18px;
  border-radius: 999px;
}

.link-toggle.vertical {
  width: 18px;
  height: 38px;
  border-radius: 999px;
}

.link-toggle.diagonal {
  width: 38px;
  height: 38px;
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
  background: color-mix(in srgb, var(--color-border) 78%, transparent);
  opacity: 0.18;
}

.diag-line-primary {
  transform: rotate(45deg);
}

.diag-line-secondary {
  transform: rotate(-45deg);
}

.diagonal.state-tl-br .diag-line-primary,
.diagonal.state-tr-bl .diag-line-secondary,
.diagonal.state-cross .diag-line-primary,
.diagonal.state-cross .diag-line-secondary {
  opacity: 1;
  background: var(--color-primary);
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
  width: min(92vw, 520px);
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
  transition: border-color 0.15s ease, background 0.15s ease;
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

  .link-toggle.diagonal {
    width: 28px;
    height: 28px;
  }

  .diag-line {
    width: 18px;
  }

  .upgrade-banner,
  .grid-header {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
