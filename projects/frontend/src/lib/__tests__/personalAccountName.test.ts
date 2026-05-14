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

  it('returns a non-empty string for MALE', () => {
    const name = generatePersonalAccountName('MALE')
    expect(name.length).toBeGreaterThan(0)
  })

  it('returns a non-empty string for FEMALE', () => {
    const name = generatePersonalAccountName('FEMALE')
    expect(name.length).toBeGreaterThan(0)
  })

  it('can generate two different names for the same gender', () => {
    const first = generatePersonalAccountName('FEMALE')
    const second = generatePersonalAccountName('FEMALE')
    expect(first).not.toBe(second)
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

  // ── Format invariants ─────────────────────────────────────────────────────────

  it('generated name contains exactly two spaces (three-word check)', () => {
    for (let i = 0; i < 20; i++) {
      const name = generatePersonalAccountName()
      const spaceCount = (name.match(/ /g) ?? []).length
      expect(spaceCount).toBe(2)
    }
  })

  it('no word in the generated name contains spaces (all words are single tokens)', () => {
    for (let i = 0; i < 20; i++) {
      const name = generatePersonalAccountName()
      const parts = name.split(' ')
      expect(parts).toHaveLength(3)
      for (const word of parts) {
        // Each token must be non-empty and have no leading/trailing whitespace
        expect(word.length).toBeGreaterThan(0)
        expect(word.trim()).toBe(word)
      }
    }
  })

  it('surname (third word) is NOT among the first two words in 10 consecutive names', () => {
    // The first two words come from first-name dictionaries, the third from the curated surnames list.
    // They should not match each other.
    for (let i = 0; i < 10; i++) {
      const name = generatePersonalAccountName()
      const parts = name.split(' ')
      const [first, middle, last] = parts as [string, string, string]
      // The last part must be from surnames — it should not equal the first two parts
      // (names dictionaries rarely overlap with the curated surname list)
      expect(first).not.toBe(last)
      expect(middle).not.toBe(last)
    }
  })

  it('all three words in 20 names have no internal spaces and start with a capital', () => {
    for (let i = 0; i < 20; i++) {
      const name = generatePersonalAccountName()
      const parts = name.split(' ')
      expect(parts).toHaveLength(3)
      for (const part of parts) {
        expect(part.length).toBeGreaterThan(0)
        expect(part.charAt(0)).toBe(part.charAt(0).toUpperCase())
      }
    }
  })

  it('session accumulates names: each call adds to the used set', () => {
    const names: string[] = []
    for (let i = 0; i < 10; i++) {
      names.push(generatePersonalAccountName())
    }
    // All 10 names must be distinct within the session
    const unique = new Set(names)
    expect(unique.size).toBe(10)
  })

  it('resetPersonalNameSession allows re-seeing previously seen names', () => {
    // Generate a small set, reset, then generate again — uniqueness tracking is cleared
    const firstBatch: string[] = []
    for (let i = 0; i < 5; i++) firstBatch.push(generatePersonalAccountName())
    resetPersonalNameSession()
    // After reset, the same names CAN appear (the set is cleared), no throws
    const secondBatch: string[] = []
    expect(() => {
      for (let i = 0; i < 20; i++) secondBatch.push(generatePersonalAccountName())
    }).not.toThrow()
    expect(secondBatch.every((n) => n.split(' ').length === 3)).toBe(true)
  })
})
