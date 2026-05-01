<script setup lang="ts">
import { computed } from 'vue'
import type { BuildingSupplyChainDiagram } from '@/types'

interface Props {
  diagram: BuildingSupplyChainDiagram
}

defineProps<Props>()

const CELL_SIZE = 80
const PADDING = 20
const ARROW_WIDTH = 2

const svgWidth = computed(() => 4 * CELL_SIZE + 2 * PADDING)
const svgHeight = computed(() => 4 * CELL_SIZE + 2 * PADDING)

// Get unit color based on fill percentage
function getUnitColor(fillPercent: number): string {
  if (fillPercent === 0) return '#ef4444' // red-500
  if (fillPercent < 33) return '#f97316' // orange-500
  if (fillPercent < 66) return '#eab308' // yellow-500
  return '#22c55e' // green-500
}

// Get unit status color
function getStatusColor(status: string): string {
  switch (status) {
    case 'ACTIVE':
      return '#22c55e' // green
    case 'BLOCKED':
      return '#ef4444' // red
    case 'FULL':
      return '#3b82f6' // blue
    case 'IDLE':
      return '#f59e0b' // amber
    case 'UNCONFIGURED':
      return '#9ca3af' // gray
    default:
      return '#9ca3af'
  }
}

// Calculate SVG coordinates for a unit
function getUnitCoords(gridX: number, gridY: number) {
  return {
    x: PADDING + gridX * CELL_SIZE,
    y: PADDING + gridY * CELL_SIZE,
  }
}
</script>

<template>
  <div class="supply-chain-diagram flex flex-col items-center gap-4">
    <svg :width="svgWidth" :height="svgHeight" viewBox="0 0 180 180" class="border border-gray-300 rounded bg-white" xmlns="http://www.w3.org/2000/svg">
      <!-- Draw links (arrows between units) -->
      <g class="links">
        <template v-for="link in diagram.links" :key="`link-${link.fromUnitId}-${link.toUnitId}`">
          <template v-for="unit in diagram.units" :key="`unit-${unit.buildingUnitId}`">
            <template v-if="unit.buildingUnitId === link.fromUnitId">
              <template v-for="targetUnit in diagram.units" :key="`target-${targetUnit.buildingUnitId}`">
                <template v-if="targetUnit.buildingUnitId === link.toUnitId">
                  <g>
                    <!-- Arrow line -->
                    <line
                      :x1="getUnitCoords(unit.gridX, unit.gridY).x + CELL_SIZE / 2"
                      :y1="getUnitCoords(unit.gridX, unit.gridY).y + CELL_SIZE / 2"
                      :x2="getUnitCoords(targetUnit.gridX, targetUnit.gridY).x + CELL_SIZE / 2"
                      :y2="getUnitCoords(targetUnit.gridX, targetUnit.gridY).y + CELL_SIZE / 2"
                      :stroke="'#6b7280'"
                      :stroke-width="ARROW_WIDTH"
                      marker-end="url(#arrowhead)"
                    />
                    <!-- Transit cost label -->
                    <text
                      v-if="link.estimatedTransitCost"
                      :x="(getUnitCoords(unit.gridX, unit.gridY).x + getUnitCoords(targetUnit.gridX, targetUnit.gridY).x) / 2"
                      :y="(getUnitCoords(unit.gridX, unit.gridY).y + getUnitCoords(targetUnit.gridX, targetUnit.gridY).y) / 2 - 5"
                      class="text-xs fill-gray-600"
                      text-anchor="middle"
                    >
                      {{ link.estimatedTransitCost.toFixed(2) }}
                    </text>
                  </g>
                </template>
              </template>
            </template>
          </template>
        </template>
      </g>

      <!-- Define arrow marker -->
      <defs>
        <marker id="arrowhead" markerWidth="10" markerHeight="10" refX="9" refY="3" orient="auto">
          <polygon points="0 0, 10 3, 0 6" fill="#6b7280" />
        </marker>
      </defs>

      <!-- Draw units (boxes) -->
      <g class="units">
        <template v-for="unit in diagram.units" :key="`unit-box-${unit.buildingUnitId}`">
          <g>
            <!-- Unit background -->
            <rect
              :x="getUnitCoords(unit.gridX, unit.gridY).x + 2"
              :y="getUnitCoords(unit.gridX, unit.gridY).y + 2"
              :width="CELL_SIZE - 4"
              :height="CELL_SIZE - 4"
              :fill="getUnitColor(unit.fillPercent)"
              rx="4"
              class="opacity-30"
            />

            <!-- Unit border -->
            <rect
              :x="getUnitCoords(unit.gridX, unit.gridY).x + 2"
              :y="getUnitCoords(unit.gridX, unit.gridY).y + 2"
              :width="CELL_SIZE - 4"
              :height="CELL_SIZE - 4"
              :stroke="getStatusColor(unit.status)"
              :stroke-width="2"
              fill="none"
              rx="4"
            />

            <!-- Unit type label -->
            <text :x="getUnitCoords(unit.gridX, unit.gridY).x + CELL_SIZE / 2" :y="getUnitCoords(unit.gridX, unit.gridY).y + 20" class="text-xs font-semibold fill-gray-800" text-anchor="middle">
              {{ unit.unitType.substring(0, 4) }}
            </text>

            <!-- Fill percentage -->
            <text :x="getUnitCoords(unit.gridX, unit.gridY).x + CELL_SIZE / 2" :y="getUnitCoords(unit.gridX, unit.gridY).y + 36" class="text-xs font-bold fill-gray-800" text-anchor="middle">
              {{ unit.fillPercent.toFixed(0) }}%
            </text>

            <!-- Idle ticks indicator (if idle) -->
            <text
              v-if="unit.idleTicks > 5"
              :x="getUnitCoords(unit.gridX, unit.gridY).x + CELL_SIZE / 2"
              :y="getUnitCoords(unit.gridX, unit.gridY).y + 52"
              class="text-xs fill-red-600 font-semibold"
              text-anchor="middle"
            >
              ⚠{{ unit.idleTicks }}
            </text>
          </g>
        </template>
      </g>
    </svg>

    <!-- Legend -->
    <div class="legend grid grid-cols-2 gap-3 text-xs">
      <div class="flex items-center gap-2">
        <div class="h-4 w-4 rounded bg-green-500"></div>
        <span>Active / Full Stock</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="h-4 w-4 rounded bg-yellow-500"></div>
        <span>Partial Stock</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="h-4 w-4 rounded bg-orange-500"></div>
        <span>Low Stock</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="h-4 w-4 rounded bg-red-500"></div>
        <span>Empty / Critical</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="h-1 w-4 bg-gray-400"></div>
        <span>Material Flow</span>
      </div>
      <div class="flex items-center gap-2">
        <div class="text-xs font-semibold text-red-600">⚠</div>
        <span>Stalled > 5 Ticks</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.supply-chain-diagram {
  padding: 16px;
  background: #f9fafb;
  border-radius: 8px;
}

svg text {
  pointer-events: none;
  user-select: none;
}
</style>
