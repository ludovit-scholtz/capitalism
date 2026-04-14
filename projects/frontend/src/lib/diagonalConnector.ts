/**
 * Pure functions for DiagonalConnector rendering state.
 *
 * Extracted from DiagonalConnector.vue so the arrowhead coordinate mapping and
 * disabled/active derivations can be unit-tested without a Vue instance.
 */

import type { DirectedPairLinkState } from './linkHelpers'

/**
 * SVG `points` string for each active primary (\ axis: tl→br) arrowhead state.
 * ViewBox is 36×36.  The polygon tip lands at the destination corner; the two
 * base points step back ≈9 px along the diagonal and ±4 px perpendicular.
 *
 *   forward  (↘ tl→br): tip at bottom-right (33,33)
 *   backward (↖ br→tl): tip at top-left (3,3)
 *   both     (legacy)  : treated as forward
 */
export const PRIMARY_ARROW_POINTS: Record<Exclude<DirectedPairLinkState, 'none'>, string> = {
  forward: '33,33 24,30 30,24', // ↘
  backward: '3,3 12,7 7,12', // ↖
  both: '33,33 24,30 30,24', // legacy → forward
}

/**
 * SVG `points` string for each active secondary (/ axis: tr→bl) arrowhead state.
 *
 *   forward  (↙ tr→bl): tip at bottom-left (3,33)
 *   backward (↗ bl→tr): tip at top-right (33,3)
 *   both     (legacy)  : treated as forward
 */
export const SECONDARY_ARROW_POINTS: Record<Exclude<DirectedPairLinkState, 'none'>, string> = {
  forward: '3,33 12,30 7,24', // ↙
  backward: '33,3 30,12 24,7', // ↗
  both: '3,33 12,30 7,24', // legacy → forward
}

/**
 * Returns the SVG `points` string for the primary diagonal arrowhead,
 * or `null` when the link is inactive (`none` state).
 */
export function getPrimaryArrowPoints(state: DirectedPairLinkState): string | null {
  if (state === 'none') return null
  return PRIMARY_ARROW_POINTS[state]
}

/**
 * Returns the SVG `points` string for the secondary diagonal arrowhead,
 * or `null` when the link is inactive (`none` state).
 */
export function getSecondaryArrowPoints(state: DirectedPairLinkState): string | null {
  if (state === 'none') return null
  return SECONDARY_ARROW_POINTS[state]
}

/**
 * Whether the connector group should be rendered in the disabled (low-opacity)
 * state.  Both axes must be untoggleable for this to be true.
 */
export function isConnectorDisabled(canTogglePrimary: boolean, canToggleSecondary: boolean): boolean {
  return !canTogglePrimary && !canToggleSecondary
}

/**
 * Determines which hit-area expansion mode applies for a connector cell.
 *
 *  'both'            – both diagonals are available; split 50/50 (left=primary, right=secondary)
 *  'primary-only'    – only the \ diagonal is available; primary hit-area expands to full width
 *  'secondary-only'  – only the / diagonal is available; secondary hit-area expands to full width
 *  'none'            – neither diagonal is available (connector is fully disabled)
 *
 * This is the pure-function companion to the `isSoloPrimary` / `isSoloSecondary`
 * computeds in DiagonalConnector.vue and is exposed here so the logic can be
 * unit-tested without a Vue instance.
 */
export type HitAreaMode = 'both' | 'primary-only' | 'secondary-only' | 'none'

export function getHitAreaMode(canTogglePrimary: boolean, canToggleSecondary: boolean): HitAreaMode {
  if (canTogglePrimary && canToggleSecondary) return 'both'
  if (canTogglePrimary) return 'primary-only'
  if (canToggleSecondary) return 'secondary-only'
  return 'none'
}
