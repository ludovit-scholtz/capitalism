import { describe, expect, it } from 'vitest'

import { generateOnboardingCompanyName, NAME_GENERATOR_CYCLE_LENGTH } from '../onboardingCompanyName'

describe('generateOnboardingCompanyName', () => {
  it('generates a two-word name (word + suffix)', () => {
    const name = generateOnboardingCompanyName('FURNITURE', 'Bratislava')
    expect(name.split(' ')).toHaveLength(2)
  })

  it('generates a stable name for the same inputs and same offset', () => {
    expect(generateOnboardingCompanyName('FURNITURE', 'Bratislava', 0)).toBe(
      generateOnboardingCompanyName('FURNITURE', 'Bratislava', 0),
    )
  })

  it('generates a different name when offset is incremented', () => {
    const name0 = generateOnboardingCompanyName('FURNITURE', 'Bratislava', 0)
    const name1 = generateOnboardingCompanyName('FURNITURE', 'Bratislava', 1)
    expect(name0).not.toBe(name1)
  })

  it('changes when the city changes (offset=0)', () => {
    expect(generateOnboardingCompanyName('FURNITURE', 'Bratislava')).not.toBe(
      generateOnboardingCompanyName('FURNITURE', 'Prague'),
    )
  })

  it('falls back gracefully when city is missing', () => {
    const name = generateOnboardingCompanyName('HEALTHCARE')
    expect(name).toMatch(/\S+ \S+/)
  })

  it('falls back gracefully for an unknown industry', () => {
    const name = generateOnboardingCompanyName('UNKNOWN_INDUSTRY', 'Bratislava')
    expect(name).toMatch(/\S+ \S+/)
  })

  it('produces at least 10 distinct names in sequence (no duplicates within first 10 offsets)', () => {
    const industry = 'FURNITURE'
    const city = 'Bratislava'
    const names = Array.from({ length: 10 }, (_, i) => generateOnboardingCompanyName(industry, city, i))
    const uniqueNames = new Set(names)
    expect(uniqueNames.size).toBeGreaterThanOrEqual(10)
  })

  it('produces 10 distinct names for FOOD_PROCESSING', () => {
    const names = Array.from({ length: 10 }, (_, i) => generateOnboardingCompanyName('FOOD_PROCESSING', 'Prague', i))
    expect(new Set(names).size).toBeGreaterThanOrEqual(10)
  })

  it('produces 10 distinct names for HEALTHCARE', () => {
    const names = Array.from({ length: 10 }, (_, i) => generateOnboardingCompanyName('HEALTHCARE', 'Vienna', i))
    expect(new Set(names).size).toBeGreaterThanOrEqual(10)
  })

  it('NAME_GENERATOR_CYCLE_LENGTH is at least 12', () => {
    expect(NAME_GENERATOR_CYCLE_LENGTH).toBeGreaterThanOrEqual(12)
  })

  it('generates thematic names for economic simulation (contains known business terms)', () => {
    const businessTerms = [
      'Industries',
      'Ventures',
      'Capital',
      'Works',
      'Corp',
      'Solutions',
      'Group',
      'Holdings',
      'Dynamics',
      'Partners',
      'Collective',
      'House',
    ]
    for (let i = 0; i < NAME_GENERATOR_CYCLE_LENGTH; i++) {
      const name = generateOnboardingCompanyName('FURNITURE', 'Bratislava', i)
      const suffix = name.split(' ').at(-1)
      expect(businessTerms).toContain(suffix)
    }
  })

  it('generates distinct names for all starter industries at offset 0', () => {
    const industries = ['FURNITURE', 'FOOD_PROCESSING', 'HEALTHCARE']
    const names = industries.map((ind) => generateOnboardingCompanyName(ind, 'Bratislava', 0))
    const uniqueNames = new Set(names)
    // All three starter industries should produce different names
    expect(uniqueNames.size).toBe(3)
  })
})
