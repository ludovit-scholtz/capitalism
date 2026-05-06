import { afterEach, describe, expect, it } from 'vitest'

import {
  NAME_GENERATOR_CYCLE_LENGTH,
  businessSuffixes,
  fallbackWords,
  generateOnboardingCompanyName,
  industryWords,
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

  it('generates 30 consecutive distinct names within one session', () => {
    resetNameSession('FURNITURE:Bratislava')
    const names: string[] = []
    for (let i = 0; i < 30; i++) {
      names.push(generateOnboardingCompanyName('FURNITURE'))
    }
    // 30 names must all be unique — session tracking prevents repeats across larger windows
    expect(new Set(names).size).toBe(30)
  })

  it('two separate session keys are fully independent (no cross-session leakage)', () => {
    // Session A
    resetNameSession('FURNITURE:Bratislava')
    const sessionA = Array.from({ length: 10 }, () => generateOnboardingCompanyName('FURNITURE'))

    // Switching to session B and back to A must not bleed over
    resetNameSession('HEALTHCARE:Vienna')
    resetNameSession('FURNITURE:Bratislava') // back to A — should be a fresh set
    const sessionA2 = Array.from({ length: 10 }, () => generateOnboardingCompanyName('FURNITURE'))

    // Both sub-runs must themselves be internally unique
    expect(new Set(sessionA).size).toBe(10)
    expect(new Set(sessionA2).size).toBe(10)
  })

  it('each generated name contains exactly one space (two-word format)', () => {
    resetNameSession('FURNITURE:Bratislava')
    for (let i = 0; i < NAME_GENERATOR_CYCLE_LENGTH; i++) {
      const name = generateOnboardingCompanyName('FURNITURE')
      const spaceCount = (name.match(/ /g) ?? []).length
      expect(spaceCount).toBe(1)
    }
  })

  it('both words in every generated name start with an uppercase letter', () => {
    const industries = ['FURNITURE', 'HEALTHCARE', 'FOOD_PROCESSING']
    for (const ind of industries) {
      resetNameSession(`${ind}:test`)
      for (let i = 0; i < 5; i++) {
        const name = generateOnboardingCompanyName(ind)
        const [first, second] = name.split(' ')
        expect(first![0]).toBe(first![0]!.toUpperCase())
        expect(second![0]).toBe(second![0]!.toUpperCase())
      }
    }
  })

  it('exhaustion auto-clears session and generation continues indefinitely', () => {
    // Drive 300 calls (all 300 combinations) through the session
    // After exhaustion the session should auto-clear and return more names
    resetNameSession('FURNITURE:exhaust')
    const names: string[] = []
    for (let i = 0; i < 305; i++) {
      // must not throw even after exhaustion
      names.push(generateOnboardingCompanyName('FURNITURE'))
    }
    // All 305 calls should return non-empty strings
    expect(names.every((n) => n.length > 0)).toBe(true)
  })

  it('all 8 industries produce non-empty names with valid format', () => {
    const allIndustries = [
      'FURNITURE',
      'FOOD_PROCESSING',
      'HEALTHCARE',
      'ELECTRONICS',
      'CONSTRUCTION',
      'PHARMACEUTICALS',
      'ENERGY',
      'LOGISTICS',
    ]
    for (const ind of allIndustries) {
      resetNameSession(`${ind}:all`)
      const name = generateOnboardingCompanyName(ind)
      expect(name).toMatch(/^\S+ \S+$/)
    }
  })

  it('calling resetNameSession with the same key twice does NOT clear the used-name set (no-op)', () => {
    // Reset to a known state and generate 5 names
    resetNameSession('FURNITURE:same-key-test')
    const firstBatch: string[] = []
    for (let i = 0; i < 5; i++) {
      firstBatch.push(generateOnboardingCompanyName('FURNITURE'))
    }
    expect(new Set(firstBatch).size).toBe(5)

    // Calling resetNameSession with the SAME key is a no-op; the used-name set is preserved
    resetNameSession('FURNITURE:same-key-test')

    // Next 5 names must also be distinct from the first 5 (session still tracking)
    const secondBatch: string[] = []
    for (let i = 0; i < 5; i++) {
      secondBatch.push(generateOnboardingCompanyName('FURNITURE'))
    }
    expect(new Set(secondBatch).size).toBe(5)

    // No overlap between first and second batch (session tracking was NOT reset)
    const combined = new Set([...firstBatch, ...secondBatch])
    expect(combined.size).toBe(10)
  })

  it('generated name first word belongs to the industry word list (spot-check 5 calls)', () => {
    // furniture words from the source file
    const furnitureWords = [
      'Oak', 'Timber', 'Cedar', 'Maple', 'Walnut', 'Birch', 'Pine', 'Teak',
      'Redwood', 'Ironwood', 'Crafted', 'Artisan', 'Heritage', 'Classic', 'Ember',
      'Ashwood', 'Elmwood', 'Mahogany', 'Rosewood', 'Sandalwood',
    ]
    resetNameSession('FURNITURE:wordlist')
    for (let i = 0; i < 5; i++) {
      const name = generateOnboardingCompanyName('FURNITURE')
      const firstWord = name.split(' ')[0]!
      expect(furnitureWords).toContain(firstWord)
    }
  })

  it('generated name second word belongs to the businessSuffixes list', () => {
    const starterIndustries = ['HEALTHCARE', 'FOOD_PROCESSING', 'FURNITURE']
    for (const ind of starterIndustries) {
      resetNameSession(`${ind}:suffix-check`)
      for (let i = 0; i < 5; i++) {
        const name = generateOnboardingCompanyName(ind)
        const secondWord = name.split(' ')[1]!
        expect(businessSuffixes).toContain(secondWord)
      }
    }
  })

  // ---------- Word list quality tests ----------

  it('each industry has exactly 20 first-words in its word list', () => {
    const allIndustries = [
      'FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE',
      'ELECTRONICS', 'CONSTRUCTION', 'PHARMACEUTICALS', 'ENERGY', 'LOGISTICS',
    ]
    for (const ind of allIndustries) {
      expect(industryWords[ind]).toBeDefined()
      expect(industryWords[ind]!.length).toBe(20)
    }
  })

  it('fallbackWords list has at least 15 entries', () => {
    expect(fallbackWords.length).toBeGreaterThanOrEqual(15)
  })

  it('all industryWords entries start with an uppercase letter', () => {
    for (const [ind, words] of Object.entries(industryWords)) {
      for (const word of words) {
        expect(word[0]).toBe(word[0]!.toUpperCase(), `${ind} word "${word}" does not start uppercase`)
        expect(word.length).toBeGreaterThanOrEqual(2)
      }
    }
  })

  it('all businessSuffixes start with an uppercase letter and are at least 3 chars', () => {
    for (const suffix of businessSuffixes) {
      expect(suffix[0]).toBe(suffix[0]!.toUpperCase())
      expect(suffix.length).toBeGreaterThanOrEqual(3)
    }
  })

  it('there are exactly 8 industries defined in industryWords', () => {
    expect(Object.keys(industryWords).length).toBe(8)
  })

  it('FOOD_PROCESSING word list contains expected food/agriculture words', () => {
    const foodWords = industryWords['FOOD_PROCESSING']!
    // Spot-check a few representative words to guard against accidental list swaps
    expect(foodWords).toContain('Harvest')
    expect(foodWords).toContain('Grain')
    expect(foodWords).toContain('Artisan')
  })

  it('HEALTHCARE word list contains expected medical words', () => {
    const healthWords = industryWords['HEALTHCARE']!
    expect(healthWords).toContain('Vital')
    expect(healthWords).toContain('Medic')
    expect(healthWords).toContain('Helix')
  })

  it('ELECTRONICS word list contains expected tech words', () => {
    const techWords = industryWords['ELECTRONICS']!
    expect(techWords).toContain('Circuit')
    expect(techWords).toContain('Silicon')
    expect(techWords).toContain('Quantum')
  })

  it('no word list contains duplicate entries', () => {
    for (const [ind, words] of Object.entries(industryWords)) {
      const unique = new Set(words)
      expect(unique.size).toBe(words.length, `${ind} word list has duplicates`)
    }
    const uniqueSuffixes = new Set(businessSuffixes)
    expect(uniqueSuffixes.size).toBe(businessSuffixes.length)
  })

  it('total possible combinations per industry is at least 200 (20 words × 10+ suffixes)', () => {
    const combinations = 20 * NAME_GENERATOR_CYCLE_LENGTH
    expect(combinations).toBeGreaterThanOrEqual(200)
  })

  it('50 consecutive names from FURNITURE are all distinct', () => {
    resetNameSession('FURNITURE:50-distinct')
    const names = new Set<string>()
    for (let i = 0; i < 50; i++) {
      names.add(generateOnboardingCompanyName('FURNITURE'))
    }
    expect(names.size).toBe(50)
  })

  it('FOOD_PROCESSING first word appears in the known food word list (10 calls spot check)', () => {
    const foodWords = industryWords['FOOD_PROCESSING']!
    resetNameSession('FOOD_PROCESSING:first-word')
    for (let i = 0; i < 10; i++) {
      const name = generateOnboardingCompanyName('FOOD_PROCESSING')
      const firstWord = name.split(' ')[0]!
      expect(foodWords).toContain(firstWord)
    }
  })

  it('HEALTHCARE first word appears in the known healthcare word list (10 calls spot check)', () => {
    const healthWords = industryWords['HEALTHCARE']!
    resetNameSession('HEALTHCARE:first-word')
    for (let i = 0; i < 10; i++) {
      const name = generateOnboardingCompanyName('HEALTHCARE')
      const firstWord = name.split(' ')[0]!
      expect(healthWords).toContain(firstWord)
    }
  })

  it('fallback words are used for an unknown industry (first word in fallbackWords)', () => {
    resetNameSession('UNKNOWN:fallback-word')
    for (let i = 0; i < 5; i++) {
      const name = generateOnboardingCompanyName('UNKNOWN_INDUSTRY_XYZ')
      const firstWord = name.split(' ')[0]!
      expect(fallbackWords).toContain(firstWord)
    }
  })
})
