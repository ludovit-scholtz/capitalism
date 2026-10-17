import { describe, expect, it } from 'vitest'
import {
  TICK_TIMINGS_QUERY,
  formatPhaseDuration,
  nextSort,
  sortTickPhases,
  type TickPhaseTiming,
} from '../tickTimings'

const phases: TickPhaseTiming[] = [
  { name: 'Manufacturing', lastMs: 40, averageMs: 35, maxMs: 90, aboveBudget: false },
  { name: 'PublicSales', lastMs: 150, averageMs: 140, maxMs: 200, aboveBudget: true },
  { name: 'Tax', lastMs: 5, averageMs: 5, maxMs: 6, aboveBudget: false },
]

describe('sortTickPhases', () => {
  it('sorts numeric columns descending without mutating the input', () => {
    const sorted = sortTickPhases(phases, 'averageMs', 'desc')
    expect(sorted.map((p) => p.name)).toEqual(['PublicSales', 'Manufacturing', 'Tax'])
    expect(phases[0]?.name).toBe('Manufacturing')
  })

  it('sorts by name ascending', () => {
    expect(sortTickPhases(phases, 'name', 'asc').map((p) => p.name)).toEqual(['Manufacturing', 'PublicSales', 'Tax'])
  })

  it('breaks ties by name', () => {
    const tied = [
      { ...phases[0]!, name: 'B', maxMs: 10 },
      { ...phases[0]!, name: 'A', maxMs: 10 },
    ]
    expect(sortTickPhases(tied, 'maxMs', 'desc').map((p) => p.name)).toEqual(['A', 'B'])
  })
})

describe('nextSort', () => {
  it('toggles direction on the same column', () => {
    expect(nextSort({ key: 'averageMs', direction: 'desc' }, 'averageMs')).toEqual({ key: 'averageMs', direction: 'asc' })
  })

  it('starts numeric columns slowest-first and name A-Z', () => {
    expect(nextSort({ key: 'averageMs', direction: 'desc' }, 'maxMs')).toEqual({ key: 'maxMs', direction: 'desc' })
    expect(nextSort({ key: 'averageMs', direction: 'desc' }, 'name')).toEqual({ key: 'name', direction: 'asc' })
  })
})

describe('formatPhaseDuration', () => {
  it('formats milliseconds and seconds', () => {
    expect(formatPhaseDuration(12.4)).toBe('12 ms')
    expect(formatPhaseDuration(1300)).toBe('1.3 s')
  })
})

describe('TICK_TIMINGS_QUERY', () => {
  it('requests every field the panel renders', () => {
    for (const field of ['tickPhaseTimings', 'sampledTicks', 'lastTotalMs', 'averageTotalMs', 'phaseBudgetMs', 'aboveBudget']) {
      expect(TICK_TIMINGS_QUERY).toContain(field)
    }
    expect(TICK_TIMINGS_QUERY.split('{').length).toBe(TICK_TIMINGS_QUERY.split('}').length)
  })
})
