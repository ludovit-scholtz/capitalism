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
    const name = generateOnboardingCompanyName('FURNITURE', 'Bratislava')
    expect(name.split(' ')).toHaveLength(2)
  })

  it('first word is capitalised', () => {
    const name = generateOnboardingCompanyName('FURNITURE', 'Bratislava')
    const firstWord = name.split(' ')[0]!
    expect(firstWord[0]).toBe(firstWord[0]!.toUpperCase())
  })

  it('falls back gracefully when city is missing', () => {
    const name = generateOnboardingCompanyName('HEALTHCARE')
    expect(name).toMatch(/\S+ \S+/)
  })

  it('falls back gracefully for an unknown industry', () => {
    const name = generateOnboardingCompanyName('UNKNOWN_INDUSTRY', 'Bratislava')
    expect(name).toMatch(/\S+ \S+/)
  })

  it('produces at least 10 distinct names in a single session without explicit reset', () => {
    resetNameSession('FURNITURE:Bratislava')
    const names: string[] = []
    for (let i = 0; i < 10; i++) {
      names.push(generateOnboardingCompanyName('FURNITURE', 'Bratislava'))
    }
    const unique = new Set(names)
    expect(unique.size).toBeGreaterThanOrEqual(10)
  })

  it('produces 10 distinct names for FOOD_PROCESSING', () => {
    resetNameSession('FOOD_PROCESSING:Prague')
    const names = Array.from({ length: 10 }, () =>
      generateOnboardingCompanyName('FOOD_PROCESSING', 'Prague'),
    )
    expect(new Set(names).size).toBeGreaterThanOrEqual(10)
  })

  it('produces 10 distinct names for HEALTHCARE', () => {
    resetNameSession('HEALTHCARE:Vienna')
    const names = Array.from({ length: 10 }, () =>
      generateOnboardingCompanyName('HEALTHCARE', 'Vienna'),
    )
    expect(new Set(names).size).toBeGreaterThanOrEqual(10)
  })

  it('two successive calls return different names (session prevents immediate repeat)', () => {
    resetNameSession('FURNITURE:Bratislava')
    const name1 = generateOnboardingCompanyName('FURNITURE', 'Bratislava')
    const name2 = generateOnboardingCompanyName('FURNITURE', 'Bratislava')
    expect(name1).toMatch(/\S+ \S+/)
    expect(name2).toMatch(/\S+ \S+/)
    expect(name1).not.toBe(name2)
  })

  it('resetNameSession clears used-name set so names from the first session can reappear', () => {
    resetNameSession('FURNITURE:Bratislava')
    const firstSession: string[] = []
    for (let i = 0; i < 5; i++) {
      firstSession.push(generateOnboardingCompanyName('FURNITURE', 'Bratislava'))
    }
    // Force a key change then restore to simulate industry/city change and return
    resetNameSession('FURNITURE:Bratislava-temp')
    resetNameSession('FURNITURE:Bratislava')
    const secondSession: string[] = []
    for (let i = 0; i < 5; i++) {
      secondSession.push(generateOnboardingCompanyName('FURNITURE', 'Bratislava'))
    }
    // Second session should also produce 5 unique names (session reset works)
    expect(new Set(secondSession).size).toBe(5)
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
      const name = generateOnboardingCompanyName('FURNITURE', 'Bratislava')
      const suffix = name.split(' ').at(-1)
      expect(businessSuffixes).toContain(suffix)
    }
  })

  it('generates different names for the three starter industries at first call', () => {
    const industries = ['FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE']
    const names = industries.map((ind) => {
      resetNameSession(`${ind}:Bratislava`)
      return generateOnboardingCompanyName(ind, 'Bratislava')
    })
    const unique = new Set(names)
    expect(unique.size).toBe(3)
  })

  it('works for all Pro-tier industries without throwing', () => {
    const proIndustries = ['ELECTRONICS', 'CONSTRUCTION', 'PHARMACEUTICALS', 'ENERGY', 'LOGISTICS']
    for (const ind of proIndustries) {
      resetNameSession(`${ind}:Vienna`)
      const name = generateOnboardingCompanyName(ind, 'Vienna')
      expect(name).toMatch(/\S+ \S+/)
    }
  })

  it('generates at least 10 distinct names for each Pro-tier industry', () => {
    const proIndustries = ['ELECTRONICS', 'CONSTRUCTION', 'PHARMACEUTICALS', 'ENERGY', 'LOGISTICS']
    for (const ind of proIndustries) {
      resetNameSession(`${ind}:Vienna`)
      const names = Array.from({ length: 10 }, () => generateOnboardingCompanyName(ind, 'Vienna'))
      expect(new Set(names).size).toBeGreaterThanOrEqual(10)
    }
  })
})
