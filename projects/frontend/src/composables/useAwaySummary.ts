import { onMounted, onUnmounted, ref, watch, type Ref } from 'vue'
import { gqlRequest } from '@/lib/graphql'
import {
  AWAY_SUMMARY_QUERY,
  LAST_SEEN_TICK_KEY,
  SHOWN_THIS_SESSION_KEY,
  hasNotableChange,
  parseLastSeenTick,
  shouldFetchAwaySummary,
  type AwaySummary,
} from '@/lib/awaySummary'

export interface AwaySummaryDependencies {
  readLastSeenTick: () => number | null
  readShown: () => boolean
  markShown: () => void
  fetchSummary: (sinceTick: number) => Promise<AwaySummary>
}

/** The decision logic with injected storage and network, so it is testable without a browser. */
export function createAwaySummaryController(deps: AwaySummaryDependencies) {
  const summary = ref<AwaySummary | null>(null)
  const open = ref(false)

  /** Call with every tick the dashboard sees; the first qualifying call of a session may open the summary. */
  async function onTick(tick: number): Promise<void> {
    const lastSeenTick = deps.readLastSeenTick()
    const shouldFetch = shouldFetchAwaySummary({ lastSeenTick, currentTick: tick, shownThisSession: deps.readShown() })
    if (!shouldFetch || lastSeenTick === null) return

    // Marked before the request so ticks arriving meanwhile cannot ask again.
    deps.markShown()
    try {
      const result = await deps.fetchSummary(lastSeenTick)
      summary.value = result
      open.value = hasNotableChange(result)
    } catch {
      // The summary is a convenience; a failed read just means no modal this session.
    }
  }

  function close() {
    open.value = false
  }

  return { summary, open, onTick, close }
}

/**
 * Remembers the tick the player last saw when they leave: the recorder only writes when asked (tab hidden or page unloading),
 * never while they are playing, so "last seen" means "the end of the previous visit".
 */
export function createLastSeenRecorder(deps: { getTick: () => number | null | undefined; writeLastSeenTick: (tick: number) => void }) {
  function onLeave(): void {
    const tick = deps.getTick()
    if (typeof tick === 'number') deps.writeLastSeenTick(tick)
  }

  return { onLeave }
}

function readStorage(storage: () => Storage, key: string): string | null {
  try {
    return storage().getItem(key)
  } catch {
    return null
  }
}

function writeStorage(storage: () => Storage, key: string, value: string): void {
  try {
    storage().setItem(key, value)
  } catch {
    // Storage can be unavailable (private windows); the summary then simply does not appear.
  }
}

/**
 * The tick this visit started from, read from storage once and then only replaced when the player leaves. Reading storage at
 * decision time would mix an earlier tab-hide of the same visit into "away", so the dashboard asks this snapshot instead.
 */
let visitStartTick: number | null | undefined

function visitStartLastSeenTick(): number | null {
  if (visitStartTick === undefined) visitStartTick = parseLastSeenTick(readStorage(() => localStorage, LAST_SEEN_TICK_KEY))
  return visitStartTick
}

/**
 * Installs the app-wide recorder of the last tick this browser saw (local storage: a per-browser convenience, never trusted for
 * anything economic). Call once, from the app root.
 */
export function useLastSeenTickRecorder(currentTick: Ref<number | null | undefined>) {
  visitStartLastSeenTick() // snapshot before this visit can overwrite the stored value
  const recorder = createLastSeenRecorder({
    getTick: () => currentTick.value,
    writeLastSeenTick: (tick) => {
      visitStartTick = tick
      writeStorage(() => localStorage, LAST_SEEN_TICK_KEY, String(tick))
    },
  })
  const onVisibilityChange = () => {
    if (document.visibilityState === 'hidden') recorder.onLeave()
  }

  onMounted(() => {
    document.addEventListener('visibilitychange', onVisibilityChange)
    window.addEventListener('pagehide', recorder.onLeave)
  })
  onUnmounted(() => {
    document.removeEventListener('visibilitychange', onVisibilityChange)
    window.removeEventListener('pagehide', recorder.onLeave)
  })
}

/** Opens the summary once per session when the player comes back after a while. */
export function useAwaySummary(currentTick: Ref<number | null | undefined>) {
  const controller = createAwaySummaryController({
    readLastSeenTick: visitStartLastSeenTick,
    readShown: () => readStorage(() => sessionStorage, SHOWN_THIS_SESSION_KEY) === '1',
    markShown: () => writeStorage(() => sessionStorage, SHOWN_THIS_SESSION_KEY, '1'),
    fetchSummary: async (sinceTick) => (await gqlRequest<{ awaySummary: AwaySummary }>(AWAY_SUMMARY_QUERY, { sinceTick })).awaySummary,
  })

  watch(
    currentTick,
    (tick) => {
      if (typeof tick === 'number') void controller.onTick(tick)
    },
    { immediate: true },
  )

  return controller
}
