import { describe, expect, test } from 'vitest'
import { createRandomReferralCode } from '@/lib/referrals'

describe('createRandomReferralCode', () => {
  test('returns an 8-character uppercase alphanumeric code', () => {
    const result = createRandomReferralCode(new Set())
    expect(result).toMatch(/^[A-Z0-9]{8}$/)
    expect(result).toHaveLength(8)
  })

  test('avoids existing codes', () => {
    const existing = new Set(['ABCDEFGH'])

    for (let i = 0; i < 20; i += 1) {
      const value = createRandomReferralCode(existing)
      expect(existing.has(value)).toBe(false)
    }
  })
})
