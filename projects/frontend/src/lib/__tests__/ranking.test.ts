import { describe, expect, it } from 'vitest'
import { calculateRankPage, isActivePlayer } from '@/lib/ranking'
import en from '@/i18n/locales/en'
import sk from '@/i18n/locales/sk'
import de from '@/i18n/locales/de'

describe('ranking helpers', () => {
  it('calculateRankPage handles normal and fallback cases', () => {
    expect(calculateRankPage(1, 10)).toBe(1)
    expect(calculateRankPage(10, 10)).toBe(1)
    expect(calculateRankPage(11, 10)).toBe(2)
    expect(calculateRankPage(0, 10)).toBe(1)
    expect(calculateRankPage(undefined, 10)).toBe(1)
  })

  it('isActivePlayer is true only when ids match', () => {
    expect(isActivePlayer('player-1', 'player-1')).toBe(true)
    expect(isActivePlayer('player-1', 'player-2')).toBe(false)
    expect(isActivePlayer(null, 'player-1')).toBe(false)
    expect(isActivePlayer('player-1', null)).toBe(false)
  })
})

describe('ranking i18n keys', () => {
  it('contains new leaderboard keys in all locales', () => {
    const keys = [
      'viewMasterRanking',
      'activePlayerRowAria',
      'pageLabel',
      'previousPage',
      'nextPage',
    ] as const

    for (const key of keys) {
      expect(en.leaderboard[key]).toBeTruthy()
      expect(sk.leaderboard[key]).toBeTruthy()
      expect(de.leaderboard[key]).toBeTruthy()
    }
  })
})
