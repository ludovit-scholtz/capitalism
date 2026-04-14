<script setup lang="ts">
import { computed } from 'vue'
import type { DirectedPairLinkState } from '@/lib/linkHelpers'
import {
  getPrimaryArrowPoints,
  getSecondaryArrowPoints,
  isConnectorDisabled,
} from '@/lib/diagonalConnector'

/**
 * Single-SVG diagonal connector for the building unit grid.
 *
 * Replaces the old two-element approach (diagonal-primary + diagonal-secondary divs
 * with rotated pill-shaped spans) that caused duplicate strokes and multiple rounded
 * endpoints when both diagonal axes were visible.
 *
 * A single SVG element covers the full connector square and draws both the \ and /
 * diagonal lines cleanly.  Transparent click-hit buttons are layered above the SVG
 * and carry the data-diagonal-axis / data-diagonal-root attributes expected by E2E
 * tests and by accessibility tooling.
 *
 * The arrowhead coordinate mapping is in `src/lib/diagonalConnector.ts` so it can
 * be unit-tested independently of Vue reactivity.
 */

const props = defineProps<{
  /** Column of the top-left cell in the 2×2 block this connector covers. */
  x: number
  /** Row of the top-left cell in the 2×2 block this connector covers. */
  y: number
  /** Directional state for the \ (primary) diagonal. */
  primaryState: DirectedPairLinkState
  /** Directional state for the / (secondary) diagonal. */
  secondaryState: DirectedPairLinkState
  /** Whether the \ diagonal has two occupied corner cells (can be toggled). */
  canTogglePrimary: boolean
  /** Whether the / diagonal has two occupied corner cells (can be toggled). */
  canToggleSecondary: boolean
  /** When true the hit-area elements render as non-interactive divs. */
  isReadonly?: boolean
}>()

defineEmits<{
  togglePrimary: []
  toggleSecondary: []
}>()

const primaryActive = computed(() => props.primaryState !== 'none')
const secondaryActive = computed(() => props.secondaryState !== 'none')
const isDisabled = computed(() => isConnectorDisabled(props.canTogglePrimary, props.canToggleSecondary))

const primaryArrow = computed(() => getPrimaryArrowPoints(props.primaryState))
const secondaryArrow = computed(() => getSecondaryArrowPoints(props.secondaryState))

const diagonalRoot = computed(() => `${props.x},${props.y}`)
</script>

<template>
  <div class="diagonal-connector-group" :class="{ disabled: isDisabled }">
    <!--
      Single SVG: draws BOTH diagonal lines as clean SVG <line> elements.
      This eliminates the duplicate-stroke and multiple-rounded-endpoint artifacts
      that occurred with the old two-div approach.
    -->
    <svg
      class="diag-conn-svg"
      viewBox="0 0 36 36"
      width="36"
      height="36"
      aria-hidden="true"
      focusable="false"
    >
      <!-- Primary \ line: top-left corner → bottom-right corner -->
      <line
        v-if="canTogglePrimary"
        x1="3"
        y1="3"
        x2="33"
        y2="33"
        stroke-width="3"
        stroke-linecap="round"
        :class="['diag-conn-line', { 'diag-conn-active': primaryActive }]"
      />

      <!-- Secondary / line: top-right corner → bottom-left corner -->
      <line
        v-if="canToggleSecondary"
        x1="33"
        y1="3"
        x2="3"
        y2="33"
        stroke-width="3"
        stroke-linecap="round"
        :class="['diag-conn-line', { 'diag-conn-active': secondaryActive }]"
      />

      <!-- Direction arrowhead polygon for primary diagonal -->
      <polygon
        v-if="primaryArrow"
        :points="primaryArrow"
        class="diag-conn-arrowhead"
      />

      <!-- Direction arrowhead polygon for secondary diagonal -->
      <polygon
        v-if="secondaryArrow"
        :points="secondaryArrow"
        class="diag-conn-arrowhead"
      />
    </svg>

    <!--
      Hit-area elements: transparent overlays that handle click events and carry the
      CSS class selectors expected by existing E2E tests (link-state-*, link-toggle.diagonal,
      data-diagonal-axis, data-diagonal-root).  They are visually invisible — all rendering
      is done by the SVG above.
    -->
    <template v-if="canTogglePrimary">
      <button
        v-if="!isReadonly"
        class="link-toggle diagonal diagonal-primary diag-hit-area"
        :class="[`link-state-${primaryState}`, { active: primaryActive }]"
        data-diagonal-axis="primary"
        :data-diagonal-root="diagonalRoot"
        :aria-label="`Diagonal primary link: ${primaryState}`"
        @click="$emit('togglePrimary')"
      />
      <div
        v-else
        class="link-toggle diagonal diagonal-primary diag-hit-area readonly"
        :class="[`link-state-${primaryState}`, { active: primaryActive }]"
        data-diagonal-axis="primary"
        :data-diagonal-root="diagonalRoot"
      />
    </template>

    <template v-if="canToggleSecondary">
      <button
        v-if="!isReadonly"
        class="link-toggle diagonal diagonal-secondary diag-hit-area"
        :class="[`link-state-${secondaryState}`, { active: secondaryActive }]"
        data-diagonal-axis="secondary"
        :data-diagonal-root="diagonalRoot"
        :aria-label="`Diagonal secondary link: ${secondaryState}`"
        @click="$emit('toggleSecondary')"
      />
      <div
        v-else
        class="link-toggle diagonal diagonal-secondary diag-hit-area readonly"
        :class="[`link-state-${secondaryState}`, { active: secondaryActive }]"
        data-diagonal-axis="secondary"
        :data-diagonal-root="diagonalRoot"
      />
    </template>
  </div>
</template>

<style scoped>
.diagonal-connector-group {
  --diagonal-link-group-size: 36px;
  position: relative;
  width: var(--diagonal-link-group-size);
  height: var(--diagonal-link-group-size);
  display: flex;
  align-items: center;
  justify-content: center;
}

.diagonal-connector-group.disabled {
  opacity: 0.28;
}

/* SVG fills the connector square and sits beneath the transparent hit-area buttons */
.diag-conn-svg {
  position: absolute;
  inset: 0;
  pointer-events: none;
}

/* Inactive diagonal line — muted, communicates that a connection is possible here */
.diag-conn-line {
  stroke: color-mix(in srgb, var(--color-border) 78%, transparent);
  transition: stroke 0.15s ease;
}

/* Active diagonal line — primary brand color + subtle glow */
.diag-conn-active {
  stroke: var(--color-primary);
  filter: drop-shadow(0 0 3px color-mix(in srgb, var(--color-primary) 45%, transparent));
}

/* Arrowhead polygon — inherits active color, adds a soft shadow for legibility */
.diag-conn-arrowhead {
  fill: var(--color-primary);
  filter: drop-shadow(0 1px 3px rgba(8, 15, 28, 0.55));
}

/*
  Hover feedback: when the connector group is hovered and is not disabled,
  inactive lines receive a subtle primary-tinted highlight so players can see
  what they're about to interact with.
*/
.diagonal-connector-group:not(.disabled):hover .diag-conn-line:not(.diag-conn-active) {
  stroke: color-mix(in srgb, var(--color-primary) 35%, var(--color-border));
}

/* Hit-area buttons/divs — transparent overlays for click delegation.
   Parent BuildingDetailView .link-toggle styles are excluded from .diag-hit-area
   elements via :not(.diag-hit-area) qualifiers on the parent selectors, so no
   !important overrides are needed here. */
.diag-hit-area {
  position: absolute;
  top: 0;
  bottom: 0;
  padding: 0;
  border: none;
  background: transparent;
  cursor: pointer;
  box-shadow: none;
  outline: none;
  border-radius: 0;
}

.diag-hit-area:hover {
  border: none;
  box-shadow: none;
  background: transparent;
}

.diag-hit-area.diagonal-primary {
  left: 0;
  width: 50%;
}

.diag-hit-area.diagonal-secondary {
  right: 0;
  width: 50%;
}

.diag-hit-area.readonly {
  cursor: default;
}

/* Focus ring on keyboard navigation */
.diag-hit-area:focus-visible {
  outline: 2px solid var(--color-primary);
  outline-offset: 1px;
  border-radius: 4px;
}
</style>
