/**
 * Pure helpers for the "while you were away" summary (#602). The server computes the figures from the player's own ledger;
 * the client only remembers the last tick this browser saw and decides when asking is worthwhile.
 */

export interface AwaySummary {
  sinceTick: number
  currentTick: number
  ticksAway: number
  revenueUsd: number
  /** Running costs as a negative number. */
  costsUsd: number
  netUsd: number
  loanPaymentsCount: number
}

export const AWAY_SUMMARY_QUERY = `
  query AwaySummary($sinceTick: Long!) {
    awaySummary(sinceTick: $sinceTick) {
      sinceTick
      currentTick
      ticksAway
      revenueUsd
      costsUsd
      netUsd
      loanPaymentsCount
    }
  }
`

/** Shorter absences (in-game hours) are not worth interrupting the player for. */
export const AWAY_MIN_TICKS = 6

export const LAST_SEEN_TICK_KEY = 'away_last_seen_tick'
export const SHOWN_THIS_SESSION_KEY = 'away_summary_shown'

/** Reads a stored tick; anything that is not a non-negative whole number means "never seen". */
export function parseLastSeenTick(raw: string | null): number | null {
  if (raw === null || !/^\d+$/.test(raw.trim())) return null
  const tick = Number(raw.trim())
  return Number.isSafeInteger(tick) ? tick : null
}

export function shouldFetchAwaySummary(options: { lastSeenTick: number | null; currentTick: number; shownThisSession: boolean }): boolean {
  const { lastSeenTick, currentTick, shownThisSession } = options
  if (shownThisSession || lastSeenTick === null) return false
  return currentTick - lastSeenTick >= AWAY_MIN_TICKS
}

/** A summary where nothing happened is not shown: an empty modal is noise. */
export function hasNotableChange(summary: AwaySummary): boolean {
  return summary.revenueUsd !== 0 || summary.costsUsd !== 0 || summary.loanPaymentsCount > 0
}
