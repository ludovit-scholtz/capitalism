/**
 * Unit tests for the DiagonalConnector rendering helpers.
 *
 * These test the pure arrowhead-coordinate mapping and disabled-state logic that
 * was extracted from DiagonalConnector.vue into src/lib/diagonalConnector.ts.
 * All assertions run in a Node environment — no Vue instance required.
 */

import { describe, expect, it } from 'vitest'
import {
  PRIMARY_ARROW_POINTS,
  SECONDARY_ARROW_POINTS,
  getPrimaryArrowPoints,
  getSecondaryArrowPoints,
  isConnectorDisabled,
} from '../diagonalConnector'

// ---------------------------------------------------------------------------
// PRIMARY_ARROW_POINTS — coordinate table coverage
// ---------------------------------------------------------------------------

describe('PRIMARY_ARROW_POINTS table', () => {
  it('forward (↘ tl→br) polygon tip is at bottom-right corner (33,33)', () => {
    const pts = PRIMARY_ARROW_POINTS.forward.split(' ').map((p) => p.split(',').map(Number))
    // First point is the tip
    expect(pts[0]).toEqual([33, 33])
  })

  it('backward (↖ br→tl) polygon tip is at top-left corner (3,3)', () => {
    const pts = PRIMARY_ARROW_POINTS.backward.split(' ').map((p) => p.split(',').map(Number))
    expect(pts[0]).toEqual([3, 3])
  })

  it('legacy both state uses forward arrow points', () => {
    expect(PRIMARY_ARROW_POINTS.both).toBe(PRIMARY_ARROW_POINTS.forward)
  })

  it('forward arrowhead has 3 polygon points', () => {
    expect(PRIMARY_ARROW_POINTS.forward.split(' ')).toHaveLength(3)
  })

  it('backward arrowhead has 3 polygon points', () => {
    expect(PRIMARY_ARROW_POINTS.backward.split(' ')).toHaveLength(3)
  })
})

// ---------------------------------------------------------------------------
// SECONDARY_ARROW_POINTS — coordinate table coverage
// ---------------------------------------------------------------------------

describe('SECONDARY_ARROW_POINTS table', () => {
  it('forward (↙ tr→bl) polygon tip is at bottom-left corner (3,33)', () => {
    const pts = SECONDARY_ARROW_POINTS.forward.split(' ').map((p) => p.split(',').map(Number))
    expect(pts[0]).toEqual([3, 33])
  })

  it('backward (↗ bl→tr) polygon tip is at top-right corner (33,3)', () => {
    const pts = SECONDARY_ARROW_POINTS.backward.split(' ').map((p) => p.split(',').map(Number))
    expect(pts[0]).toEqual([33, 3])
  })

  it('legacy both state uses forward arrow points', () => {
    expect(SECONDARY_ARROW_POINTS.both).toBe(SECONDARY_ARROW_POINTS.forward)
  })

  it('forward arrowhead has 3 polygon points', () => {
    expect(SECONDARY_ARROW_POINTS.forward.split(' ')).toHaveLength(3)
  })

  it('backward arrowhead has 3 polygon points', () => {
    expect(SECONDARY_ARROW_POINTS.backward.split(' ')).toHaveLength(3)
  })
})

// ---------------------------------------------------------------------------
// Axis symmetry: primary and secondary are distinct (non-overlapping)
// ---------------------------------------------------------------------------

describe('primary vs secondary axis point distinctness', () => {
  it('forward tips are at opposite corners — no overlap between axes', () => {
    // Primary forward tip: bottom-right (33,33); Secondary forward tip: bottom-left (3,33)
    const primaryFwd = PRIMARY_ARROW_POINTS.forward.split(' ')[0]
    const secondaryFwd = SECONDARY_ARROW_POINTS.forward.split(' ')[0]
    expect(primaryFwd).not.toBe(secondaryFwd)
  })

  it('backward tips are at opposite corners', () => {
    const primaryBwd = PRIMARY_ARROW_POINTS.backward.split(' ')[0]
    const secondaryBwd = SECONDARY_ARROW_POINTS.backward.split(' ')[0]
    expect(primaryBwd).not.toBe(secondaryBwd)
  })

  it('all four tip coordinates are distinct (one per corner of the 36×36 viewbox)', () => {
    const primaryFwdTip = PRIMARY_ARROW_POINTS.forward.split(' ')[0]
    const primaryBwdTip = PRIMARY_ARROW_POINTS.backward.split(' ')[0]
    const secondaryFwdTip = SECONDARY_ARROW_POINTS.forward.split(' ')[0]
    const secondaryBwdTip = SECONDARY_ARROW_POINTS.backward.split(' ')[0]

    const tips = new Set([primaryFwdTip, primaryBwdTip, secondaryFwdTip, secondaryBwdTip])
    expect(tips.size).toBe(4)
  })
})

// ---------------------------------------------------------------------------
// getPrimaryArrowPoints
// ---------------------------------------------------------------------------

describe('getPrimaryArrowPoints', () => {
  it('returns null for none state (no arrowhead when link is inactive)', () => {
    expect(getPrimaryArrowPoints('none')).toBeNull()
  })

  it('returns forward polygon points for forward state', () => {
    expect(getPrimaryArrowPoints('forward')).toBe(PRIMARY_ARROW_POINTS.forward)
  })

  it('returns backward polygon points for backward state', () => {
    expect(getPrimaryArrowPoints('backward')).toBe(PRIMARY_ARROW_POINTS.backward)
  })

  it('returns non-null polygon points for legacy both state', () => {
    expect(getPrimaryArrowPoints('both')).toBe(PRIMARY_ARROW_POINTS.both)
  })

  it('is null only for none — all other states produce a string', () => {
    const allStates = ['none', 'forward', 'backward', 'both'] as const
    for (const s of allStates) {
      if (s === 'none') expect(getPrimaryArrowPoints(s)).toBeNull()
      else expect(typeof getPrimaryArrowPoints(s)).toBe('string')
    }
  })
})

// ---------------------------------------------------------------------------
// getSecondaryArrowPoints
// ---------------------------------------------------------------------------

describe('getSecondaryArrowPoints', () => {
  it('returns null for none state', () => {
    expect(getSecondaryArrowPoints('none')).toBeNull()
  })

  it('returns forward polygon points for forward state', () => {
    expect(getSecondaryArrowPoints('forward')).toBe(SECONDARY_ARROW_POINTS.forward)
  })

  it('returns backward polygon points for backward state', () => {
    expect(getSecondaryArrowPoints('backward')).toBe(SECONDARY_ARROW_POINTS.backward)
  })

  it('returns non-null polygon points for legacy both state', () => {
    expect(getSecondaryArrowPoints('both')).toBe(SECONDARY_ARROW_POINTS.both)
  })

  it('primary and secondary forward point sets differ (no overlapping geometry)', () => {
    const p = getPrimaryArrowPoints('forward')
    const s = getSecondaryArrowPoints('forward')
    expect(p).not.toBe(s)
    expect(p).not.toEqual(s)
  })
})

// ---------------------------------------------------------------------------
// isConnectorDisabled
// ---------------------------------------------------------------------------

describe('isConnectorDisabled', () => {
  it('returns true when neither axis can be toggled', () => {
    expect(isConnectorDisabled(false, false)).toBe(true)
  })

  it('returns false when primary can be toggled', () => {
    expect(isConnectorDisabled(true, false)).toBe(false)
  })

  it('returns false when secondary can be toggled', () => {
    expect(isConnectorDisabled(false, true)).toBe(false)
  })

  it('returns false when both axes can be toggled', () => {
    expect(isConnectorDisabled(true, true)).toBe(false)
  })
})

// ---------------------------------------------------------------------------
// Geometry sanity: arrowhead points fit within the 36×36 viewBox
// ---------------------------------------------------------------------------

describe('geometry sanity — all arrowhead points within 0–36 viewBox', () => {
  const allTables = [
    { name: 'PRIMARY', table: PRIMARY_ARROW_POINTS },
    { name: 'SECONDARY', table: SECONDARY_ARROW_POINTS },
  ] as const

  for (const { name, table } of allTables) {
    for (const [state, pointsStr] of Object.entries(table)) {
      it(`${name} ${state}: all polygon points are within [0,36]`, () => {
        const coords = pointsStr
          .split(' ')
          .flatMap((pair) => pair.split(',').map(Number))
        for (const coord of coords) {
          expect(coord).toBeGreaterThanOrEqual(0)
          expect(coord).toBeLessThanOrEqual(36)
        }
      })
    }
  }
})
