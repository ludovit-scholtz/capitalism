import { describe, expect, it } from 'vitest'

import { generateOnboardingCompanyName } from '../onboardingCompanyName'

describe('generateOnboardingCompanyName', () => {
  it('generates a stable name for the same industry and city', () => {
    expect(generateOnboardingCompanyName('FURNITURE', 'Bratislava')).toBe(generateOnboardingCompanyName('FURNITURE', 'Bratislava'))
  })

  it('changes when the city changes', () => {
    expect(generateOnboardingCompanyName('FURNITURE', 'Bratislava')).not.toBe(generateOnboardingCompanyName('FURNITURE', 'Prague'))
  })

  it('falls back gracefully when city is missing', () => {
    expect(generateOnboardingCompanyName('HEALTHCARE')).toMatch(/Capital/)
  })
})
