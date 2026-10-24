import { watch } from 'vue'

const STALE = Symbol('stale-building-request')

/**
 * Tracks which building a composable is working for. Every change of the watched building id
 * bumps a generation and runs `onReset` synchronously (before any load for the new building can
 * start). Requests started under an older generation are rejected with a stale marker via
 * `settle`, so a slow response for building A can never write into building B's state (#687).
 */
export function createBuildingScope(buildingId?: () => string | null | undefined, onReset?: () => void) {
  let generation = 0

  if (buildingId) {
    watch(
      buildingId,
      () => {
        generation++
        onReset?.()
      },
      { flush: 'sync' },
    )
  }

  return {
    /** Generation to remember before starting async work. */
    token: () => generation,
    isStale: (token: number) => token !== generation,
    /** Resolves with the request result, or rejects with the stale marker if the scope moved on. */
    async settle<T>(token: number, request: Promise<T>): Promise<T> {
      try {
        const result = await request
        if (token !== generation) throw STALE
        return result
      } catch (error) {
        if (token !== generation) throw STALE
        throw error
      }
    },
    isStaleError: (error: unknown) => error === STALE,
    /** False while the routed building differs from the given (possibly not yet reloaded) building. */
    matches: (id: string) => !buildingId || (buildingId() ?? '').toLowerCase() === id.toLowerCase(),
  }
}
