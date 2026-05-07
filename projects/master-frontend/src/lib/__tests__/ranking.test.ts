import { describe, expect, it } from 'vitest'
import { calculateRankPage, isActivePlayer } from '@/lib/ranking'
import en from '@/i18n/locales/en'
import sk from '@/i18n/locales/sk'
import de from '@/i18n/locales/de'

describe('master ranking helpers', () => {
  it('calculateRankPage handles rank and fallback edge cases', () => {
    expect(calculateRankPage(1, 10)).toBe(1)
    expect(calculateRankPage(10, 10)).toBe(1)
    expect(calculateRankPage(11, 10)).toBe(2)
    expect(calculateRankPage(0, 10)).toBe(1)
    expect(calculateRankPage(undefined, 10)).toBe(1)
  })

  it('isActivePlayer is true only when ids match', () => {
    expect(isActivePlayer('p1', 'p1')).toBe(true)
    expect(isActivePlayer('p1', 'p2')).toBe(false)
    expect(isActivePlayer(undefined, 'p1')).toBe(false)
    expect(isActivePlayer('p1', null)).toBe(false)
  })
})

describe('master ranking i18n keys', () => {
  it('contains active-row and badge translations in all locales', () => {
    expect(en.rankingDashboard.youBadge).toBeTruthy()
    expect(sk.rankingDashboard.youBadge).toBeTruthy()
    expect(de.rankingDashboard.youBadge).toBeTruthy()

    expect(en.rankingDashboard.activePlayerRowAria).toBeTruthy()
    expect(sk.rankingDashboard.activePlayerRowAria).toBeTruthy()
    expect(de.rankingDashboard.activePlayerRowAria).toBeTruthy()
  })
})
