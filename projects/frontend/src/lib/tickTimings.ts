export interface TickPhaseTiming {
  name: string
  lastMs: number
  averageMs: number
  maxMs: number
  aboveBudget: boolean
}

export type TickPhaseSortKey = 'name' | 'lastMs' | 'averageMs' | 'maxMs'
export type SortDirection = 'asc' | 'desc'

export const TICK_TIMINGS_QUERY = `query TickPhaseTimings {
  tickPhaseTimings {
    sampledTicks
    lastTick
    lastTotalMs
    averageTotalMs
    maxTotalMs
    phaseBudgetMs
    phases {
      name
      lastMs
      averageMs
      maxMs
      aboveBudget
    }
  }
}`

/** Returns a sorted copy of the phases; ties fall back to the phase name so the order is stable. */
export function sortTickPhases(
  phases: readonly TickPhaseTiming[],
  key: TickPhaseSortKey,
  direction: SortDirection,
): TickPhaseTiming[] {
  const factor = direction === 'asc' ? 1 : -1
  return [...phases].sort((a, b) => {
    const primary = key === 'name' ? a.name.localeCompare(b.name) : a[key] - b[key]
    return primary !== 0 ? primary * factor : a.name.localeCompare(b.name)
  })
}

/** Toggles direction when the same column is clicked again; new columns start slowest-first (or A-Z for names). */
export function nextSort(
  current: { key: TickPhaseSortKey; direction: SortDirection },
  clicked: TickPhaseSortKey,
): { key: TickPhaseSortKey; direction: SortDirection } {
  if (current.key === clicked) {
    return { key: clicked, direction: current.direction === 'asc' ? 'desc' : 'asc' }
  }
  return { key: clicked, direction: clicked === 'name' ? 'asc' : 'desc' }
}

/** Formats a millisecond duration for the phase table, e.g. "12 ms" or "1.3 s". */
export function formatPhaseDuration(ms: number): string {
  if (ms >= 1000) return `${(ms / 1000).toFixed(1)} s`
  return `${Math.round(ms)} ms`
}
