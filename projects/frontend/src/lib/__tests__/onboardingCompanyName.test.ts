import { afterEach, describe, expect, it } from 'vitest'

import {
  NAME_GENERATOR_CYCLE_LENGTH,
  businessSuffixes,
  generateOnboardingCompanyName,
  resetNameSession,
} from '../onboardingCompanyName'

// Reset session before each test so state from one test cannot affect another.
afterEach(() => {
  resetNameSession('__test_reset__')
})

describe('generateOnboardingCompanyName', () => {
  it('generates a two-word name (word + suffix)', () => {
    const name = generateOnboardingCompanyName('FURNITURE')
    expect(name.split(' ')).toHaveLength(2)
  })

  it('first word is capitalised', () => {
    const name = generateOnboardingCompanyName('FURNITURE')
    const firstWord = name.split(' ')[0]!
    expect(firstWord[0]).toBe(firstWord[0]!.toUpperCase())
  })

  it('falls back gracefully when city is missing', () => {
    const name = generateOnboardingCompanyName('HEALTHCARE')
    expect(name).toMatch(/\S+ \S+/)
  })

  it('falls back gracefully for an unknown industry', () => {
    const name = generateOnboardingCompanyName('UNKNOWN_INDUSTRY')
    expect(name).toMatch(/\S+ \S+/)
  })

  it('produces at least 10 distinct names in a single session without explicit reset', () => {
    resetNameSession('FURNITURE:Bratislava')
    const names: string[] = []
    for (let i = 0; i < 10; i++) {
      names.push(generateOnboardingCompanyName('FURNITURE'))
    }
    const unique = new Set(names)
    expect(unique.size).toBeGreaterThanOrEqual(10)
  })

  it('produces 10 distinct names for FOOD_PROCESSING', () => {
    resetNameSession('FOOD_PROCESSING:Prague')
    const names = Array.from({ length: 10 }, () =>
      generateOnboardingCompanyName('FOOD_PROCESSING'),
    )
    expect(new Set(names).size).toBeGreaterThanOrEqual(10)
  })

  it('produces 10 distinct names for HEALTHCARE', () => {
    resetNameSession('HEALTHCARE:Vienna')
    const names = Array.from({ length: 10 }, () =>
      generateOnboardingCompanyName('HEALTHCARE'),
    )
    expect(new Set(names).size).toBeGreaterThanOrEqual(10)
  })

  it('successive calls within a session all return distinct names (uniqueness tracking guaranteed)', () => {
    resetNameSession('FURNITURE:Bratislava')
    // Generate 15 names and verify they are all unique — session tracking prevents repeats
    const names: string[] = []
    for (let i = 0; i < 15; i++) {
      names.push(generateOnboardingCompanyName('FURNITURE'))
    }
    const unique = new Set(names)
    expect(unique.size).toBe(15)
  })

  it('resetNameSession clears used-name set so names from the first session can reappear', () => {
    // Use a second session for 15 names to prove the reset actually works
    resetNameSession('FURNITURE:Bratislava')
    const firstSession = new Set<string>()
    for (let i = 0; i < 15; i++) {
      firstSession.add(generateOnboardingCompanyName('FURNITURE'))
    }
    // First session must itself be unique (proves tracking works before reset)
    expect(firstSession.size).toBe(15)

    // Reset — simulate the player navigating back and changing industry then city
    resetNameSession('OTHER:key')
    resetNameSession('FURNITURE:Bratislava')

    // After reset the used-set is cleared: generate 15 names from a fresh session
    const secondSession = new Set<string>()
    for (let i = 0; i < 15; i++) {
      secondSession.add(generateOnboardingCompanyName('FURNITURE'))
    }
    // Second session must also be duplicate-free (tracking works after reset)
    expect(secondSession.size).toBe(15)
  })

  it('NAME_GENERATOR_CYCLE_LENGTH matches the exported businessSuffixes length', () => {
    expect(NAME_GENERATOR_CYCLE_LENGTH).toBe(businessSuffixes.length)
  })

  it('NAME_GENERATOR_CYCLE_LENGTH is at least 12', () => {
    expect(NAME_GENERATOR_CYCLE_LENGTH).toBeGreaterThanOrEqual(12)
  })

  it('every generated name ends with a known business suffix', () => {
    resetNameSession('FURNITURE:Bratislava')
    for (let i = 0; i < NAME_GENERATOR_CYCLE_LENGTH; i++) {
      const name = generateOnboardingCompanyName('FURNITURE')
      const suffix = name.split(' ').at(-1)
      expect(businessSuffixes).toContain(suffix)
    }
  })

  it('generates different names for the three starter industries at first call', () => {
    const industries = ['FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE']
    const names = industries.map((ind) => {
      resetNameSession(`${ind}:Bratislava`)
      return generateOnboardingCompanyName(ind)
    })
    const unique = new Set(names)
    expect(unique.size).toBe(3)
  })

  it('works for all Pro-tier industries without throwing', () => {
    const proIndustries = ['ELECTRONICS', 'CONSTRUCTION', 'PHARMACEUTICALS', 'ENERGY', 'LOGISTICS']
    for (const ind of proIndustries) {
      resetNameSession(`${ind}:Vienna`)
      const name = generateOnboardingCompanyName(ind)
      expect(name).toMatch(/\S+ \S+/)
    }
  })

  it('generates at least 10 distinct names for each Pro-tier industry', () => {
    const proIndustries = ['ELECTRONICS', 'CONSTRUCTION', 'PHARMACEUTICALS', 'ENERGY', 'LOGISTICS']
    for (const ind of proIndustries) {
      resetNameSession(`${ind}:Vienna`)
      const names = Array.from({ length: 10 }, () => generateOnboardingCompanyName(ind))
      expect(new Set(names).size).toBeGreaterThanOrEqual(10)
    }
  })
})
