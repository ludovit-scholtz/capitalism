import { describe, expect, it } from 'vitest'
import {
  computeGameYearFromTick,
  computeInGameTimeUtcFromTick,
  computeNextTaxTick,
  formatGameTickTime,
  formatInGameTime,
  formatTickDuration,
} from '../gameTime'

describe('gameTime helpers', () => {
  it('formats backend in-game timestamps in UTC', () => {
    expect(formatInGameTime('2000-01-01T13:00:00.000Z', 'en')).toContain('2000')
    expect(formatInGameTime('2000-01-01T13:00:00.000Z', 'en')).toContain('13:00')
  })

  it('returns empty string for invalid timestamps', () => {
    expect(formatInGameTime('not-a-date', 'en')).toBe('')
  })

  it('computes the game year from ticks', () => {
    expect(computeGameYearFromTick(0)).toBe(2000)
    expect(computeGameYearFromTick(8759)).toBe(2000)
    expect(computeGameYearFromTick(8760)).toBe(2001)
  })

  it('computes the next tax tick on cycle boundaries', () => {
    expect(computeNextTaxTick(0, 8760)).toBe(8760)
    expect(computeNextTaxTick(8759, 8760)).toBe(8760)
    expect(computeNextTaxTick(8760, 8760)).toBe(17520)
  })

  it('converts ticks into in-game UTC timestamps', () => {
    expect(computeInGameTimeUtcFromTick(0)).toBe('2000-01-01T00:00:00.000Z')
    expect(computeInGameTimeUtcFromTick(24)).toBe('2000-01-02T00:00:00.000Z')
  })

  it('formats a game tick as in-game time', () => {
    expect(formatGameTickTime(24, 'en')).toContain('2000')
    expect(formatGameTickTime(24, 'en')).toContain('02')
  })

  describe('formatGameTickTime — tick-to-readable-time conversion (player-facing display)', () => {
    it('formats tick 0 as year-2000 start date', () => {
      const result = formatGameTickTime(0, 'en')
      expect(result).toContain('2000')
      expect(result).toContain('00:00')
    })

    it('formats tick 24 (1 game day) as Jan 02 2000', () => {
      const result = formatGameTickTime(24, 'en')
      expect(result).toContain('2000')
      expect(result).toContain('02')
    })

    it('formats tick 8760 (365 game days) into year 2000', () => {
      // 1 game year = 8760 ticks = 365 days; since 2000 is a leap year (366 days),
      // tick 8760 falls on Dec 31, 2000 (not Jan 1, 2001).
      const result = formatGameTickTime(8760, 'en')
      expect(result).toContain('2000')
    })

    it('never includes raw tick numbers in formatted output', () => {
      // Tick 42 → formatted time should not contain "42" as a bare tick label
      const result = formatGameTickTime(42, 'en')
      // The formatted date should contain year, month, day — not the bare integer 42
      // (day "Jan 12" = tick 264, so tick 42 = hour 42 = Jan 02 18:00)
      expect(result).not.toBe('42')
      expect(result).toContain('2000')
    })

    it('works with German locale', () => {
      const result = formatGameTickTime(24, 'de')
      expect(result).toContain('2000')
    })

    it('works with Slovak locale', () => {
      const result = formatGameTickTime(24, 'sk')
      expect(result).toContain('2000')
    })

    it('handles large tick values without error', () => {
      const result = formatGameTickTime(876000, 'en') // ~100 game years
      expect(result).toContain('209') // somewhere in the 2090s decade
    })
  })

  describe('formatTickDuration', () => {
    it('returns "0" for zero ticks', () => {
      expect(formatTickDuration(0, 'en')).toBe('0')
    })

    it('formats hours-only durations (< 24 ticks)', () => {
      const result = formatTickDuration(10, 'en')
      expect(result).toContain('10')
      expect(result.toLowerCase()).toContain('hour')
    })

    it('formats single hour correctly', () => {
      const result = formatTickDuration(1, 'en')
      expect(result).toContain('1')
      expect(result.toLowerCase()).toContain('hour')
    })

    it('formats exact-day durations (multiple of 24)', () => {
      const result = formatTickDuration(48, 'en') // 2 days
      expect(result).toContain('2')
      expect(result.toLowerCase()).toContain('day')
      expect(result.toLowerCase()).not.toContain('hour')
    })

    it('formats days + hours durations', () => {
      const result = formatTickDuration(100, 'en') // 4 days 4 hours
      expect(result).toContain('4')
      expect(result.toLowerCase()).toContain('day')
      expect(result.toLowerCase()).toContain('hour')
    })

    it('formats large upgrade durations (1000 ticks = 41 days 16 hours)', () => {
      const result = formatTickDuration(1000, 'en')
      expect(result).toContain('41')
      expect(result.toLowerCase()).toContain('day')
    })

    it('handles negative ticks gracefully (clamps to 0)', () => {
      expect(formatTickDuration(-5, 'en')).toBe('0')
    })

    it('works with non-English locales', () => {
      const deDuration = formatTickDuration(10, 'de')
      expect(deDuration).toContain('10')
      // German should contain "Stunde" (singular) or "Stunden" (plural)
      expect(deDuration.toLowerCase()).toMatch(/stunde/)
    })
  })
})

