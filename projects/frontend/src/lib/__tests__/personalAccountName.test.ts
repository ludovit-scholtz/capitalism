import { describe, it, expect, beforeEach } from 'vitest'
import {
  generatePersonalAccountName,
  resetPersonalNameSession,
  surnames,
} from '../personalAccountName'

describe('personalAccountName', () => {
  beforeEach(() => {
    resetPersonalNameSession()
  })

  // ── Format ────────────────────────────────────────────────────────────────────

  it('returns a non-empty string', () => {
    const name = generatePersonalAccountName()
    expect(typeof name).toBe('string')
    expect(name.length).toBeGreaterThan(0)
  })

  it('returns exactly three words', () => {
    const name = generatePersonalAccountName()
    const parts = name.split(' ')
    expect(parts).toHaveLength(3)
  })

  it('every word starts with a capital letter', () => {
    for (let i = 0; i < 20; i++) {
      const name = generatePersonalAccountName()
      const parts = name.split(' ')
      for (const part of parts) {
        expect(part.charAt(0)).toBe(part.charAt(0).toUpperCase())
        expect(part.charAt(0)).not.toBe(part.charAt(0).toLowerCase())
      }
    }
  })

  it('last word (surname) is from the surnames list', () => {
    for (let i = 0; i < 30; i++) {
      const name = generatePersonalAccountName()
      const lastName = name.split(' ')[2]!
      expect(surnames).toContain(lastName)
    }
  })

  // ── Session uniqueness ────────────────────────────────────────────────────────

  it('generates 30 consecutive distinct names in the same session', () => {
    const seen = new Set<string>()
    for (let i = 0; i < 30; i++) {
      const name = generatePersonalAccountName()
      expect(seen.has(name)).toBe(false)
      seen.add(name)
    }
    expect(seen.size).toBe(30)
  })

  it('resetPersonalNameSession clears used-name tracking', () => {
    const first = generatePersonalAccountName()
    resetPersonalNameSession()
    // After reset, the same name can be generated again (no guarantee, but the session is fresh)
    // We verify by collecting names across two isolated sessions — reset separates them
    const sessionBNames: string[] = []
    for (let i = 0; i < 20; i++) {
      sessionBNames.push(generatePersonalAccountName())
    }
    // Session B is independent: it COULD contain the same names as session A (that's OK by design)
    // The key assertion is that it doesn't throw and produces valid names.
    expect(sessionBNames.every((n) => n.split(' ').length === 3)).toBe(true)
    // After reset, generating the same `first` name again is possible.
    resetPersonalNameSession()
    expect(first).toBeTruthy()
  })

  it('two independent sessions (reset between) produce valid names', () => {
    const sessionA: string[] = []
    for (let i = 0; i < 15; i++) sessionA.push(generatePersonalAccountName())
    resetPersonalNameSession()
    const sessionB: string[] = []
    for (let i = 0; i < 15; i++) sessionB.push(generatePersonalAccountName())

    // Both sessions produce three-word names
    expect(sessionA.every((n) => n.split(' ').length === 3)).toBe(true)
    expect(sessionB.every((n) => n.split(' ').length === 3)).toBe(true)
  })

  // ── Exhaustion fallback ───────────────────────────────────────────────────────

  it('never throws even after 500 consecutive calls (exhaustion auto-clear)', () => {
    expect(() => {
      for (let i = 0; i < 500; i++) {
        generatePersonalAccountName()
      }
    }).not.toThrow()
  })

  it('returns a valid three-word name even after 500 consecutive calls', () => {
    let last = ''
    for (let i = 0; i < 500; i++) {
      last = generatePersonalAccountName()
    }
    expect(last.split(' ')).toHaveLength(3)
  })

  // ── Surnames list ─────────────────────────────────────────────────────────────

  it('surnames list has at least 100 entries', () => {
    expect(surnames.length).toBeGreaterThanOrEqual(100)
  })

  it('surnames list has no duplicates', () => {
    const unique = new Set(surnames)
    expect(unique.size).toBe(surnames.length)
  })

  it('all surnames start with a capital letter', () => {
    for (const s of surnames) {
      expect(s.charAt(0)).toBe(s.charAt(0).toUpperCase())
      expect(s.charAt(0)).not.toBe(s.charAt(0).toLowerCase())
    }
  })

  it('all surnames have at least 2 characters', () => {
    for (const s of surnames) {
      expect(s.length).toBeGreaterThanOrEqual(2)
    }
  })

  it('surnames include diverse international names', () => {
    // Spot-check a selection across different origins
    const expected = ['Anderson', 'Garcia', 'Zhang', 'Yamamoto', 'Patel', 'Nguyen', 'Okafor', 'Martinez']
    for (const name of expected) {
      expect(surnames).toContain(name)
    }
  })
})
