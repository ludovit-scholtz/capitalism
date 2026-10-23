import { describe, expect, it } from 'vitest'
import { AWAY_MIN_TICKS, AWAY_SUMMARY_QUERY, hasNotableChange, parseLastSeenTick, shouldFetchAwaySummary, type AwaySummary } from '../awaySummary'

function summary(overrides: Partial<AwaySummary> = {}): AwaySummary {
  return { sinceTick: 100, currentTick: 140, ticksAway: 40, revenueUsd: 0, costsUsd: 0, netUsd: 0, loanPaymentsCount: 0, ...overrides }
}

describe('AWAY_SUMMARY_QUERY', () => {
  it('asks the server for the window and every figure the modal shows', () => {
    expect(AWAY_SUMMARY_QUERY).toContain('awaySummary(sinceTick: $sinceTick)')
    expect(AWAY_SUMMARY_QUERY).toContain('$sinceTick: Long!')
    for (const field of ['sinceTick', 'currentTick', 'ticksAway', 'revenueUsd', 'costsUsd', 'netUsd', 'loanPaymentsCount']) {
      expect(AWAY_SUMMARY_QUERY).toContain(field)
    }
  })

  it('has balanced braces', () => {
    expect(AWAY_SUMMARY_QUERY.split('{').length).toBe(AWAY_SUMMARY_QUERY.split('}').length)
  })
})

describe('parseLastSeenTick', () => {
  it('reads whole non-negative numbers', () => {
    expect(parseLastSeenTick('0')).toBe(0)
    expect(parseLastSeenTick(' 1234 ')).toBe(1234)
  })

  it('treats missing or junk as never seen', () => {
    for (const raw of [null, '', 'abc', '-5', '1.5', '12abc', '99999999999999999999']) {
      expect(parseLastSeenTick(raw)).toBeNull()
    }
  })
})

describe('shouldFetchAwaySummary', () => {
  it('asks once the player has been away for the minimum', () => {
    expect(shouldFetchAwaySummary({ lastSeenTick: 100, currentTick: 100 + AWAY_MIN_TICKS, shownThisSession: false })).toBe(true)
  })

  it('does not ask for a short absence', () => {
    expect(shouldFetchAwaySummary({ lastSeenTick: 100, currentTick: 100 + AWAY_MIN_TICKS - 1, shownThisSession: false })).toBe(false)
  })

  it('does not ask a browser that has never seen a tick (a new player has nothing to catch up on)', () => {
    expect(shouldFetchAwaySummary({ lastSeenTick: null, currentTick: 5_000, shownThisSession: false })).toBe(false)
  })

  it('asks only once per session', () => {
    expect(shouldFetchAwaySummary({ lastSeenTick: 0, currentTick: 500, shownThisSession: true })).toBe(false)
  })

  it('does not ask when the stored tick is ahead of the server (a different shard or a reset)', () => {
    expect(shouldFetchAwaySummary({ lastSeenTick: 900, currentTick: 100, shownThisSession: false })).toBe(false)
  })
})

describe('hasNotableChange', () => {
  it('is false when nothing happened', () => {
    expect(hasNotableChange(summary())).toBe(false)
  })

  it('is true for revenue, costs or a loan payment', () => {
    expect(hasNotableChange(summary({ revenueUsd: 10 }))).toBe(true)
    expect(hasNotableChange(summary({ costsUsd: -10 }))).toBe(true)
    expect(hasNotableChange(summary({ loanPaymentsCount: 1 }))).toBe(true)
  })
})
