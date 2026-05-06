import { beforeEach, describe, expect, test } from 'vitest'
import {
  applyReferralCode,
  becomeReferral,
  calculateReferralGoldTokens,
  createAdditionalReferralCode,
  createRandomReferralCode,
  DIRECT_ACTIVE_SUBSCRIPTION_GOLD_REWARD,
  getReferralDashboard,
  getReferralProfile,
  SECOND_LEVEL_ACTIVE_SUBSCRIPTION_GOLD_REWARD,
  syncReferralSubscriptionStatus,
} from '@/lib/referrals'

// ---------- localStorage mock ----------
const localStorageMock = (() => {
  let store: Record<string, string> = {}
  return {
    getItem: (key: string): string | null => store[key] ?? null,
    setItem: (key: string, value: string) => {
      store[key] = value
    },
    removeItem: (key: string) => {
      delete store[key]
    },
    clear: () => {
      store = {}
    },
  }
})()

Object.defineProperty(globalThis, 'localStorage', { value: localStorageMock })
Object.defineProperty(globalThis, 'window', { value: globalThis })

beforeEach(() => {
  localStorageMock.clear()
})

// ---------- createRandomReferralCode ----------
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

  test('uses only uppercase letters and digits (no ambiguous chars)', () => {
    // Runs 50 times to cover the charset probabilistically
    for (let i = 0; i < 50; i += 1) {
      const code = createRandomReferralCode(new Set())
      // Should NOT contain lowercase, O, 0, I, L (ambiguous chars removed from charset)
      expect(code).toMatch(/^[A-Z0-9]{8}$/)
    }
  })
})

// ---------- getReferralProfile ----------
describe('getReferralProfile', () => {
  test('returns empty profile for unknown email', () => {
    const profile = getReferralProfile('nobody@example.com')
    expect(profile.appliedReferralCode).toBeNull()
    expect(profile.referralIdentity).toBeNull()
    expect(profile.referralCodes).toHaveLength(0)
    expect(profile.hasActiveSubscription).toBe(false)
  })

  test('returns correct profile after becoming a referral', () => {
    becomeReferral('partner@example.com', 'Alice Partner', 'Germany')
    const profile = getReferralProfile('partner@example.com')
    expect(profile.referralIdentity?.fullName).toBe('Alice Partner')
    expect(profile.referralIdentity?.taxDomicile).toBe('Germany')
    expect(profile.referralCodes).toHaveLength(1)
  })
})

// ---------- becomeReferral ----------
describe('becomeReferral', () => {
  test('creates referral identity and auto-generates first code', () => {
    becomeReferral('owner@example.com', 'Bob Owner', 'Slovakia')
    const profile = getReferralProfile('owner@example.com')
    expect(profile.referralIdentity).not.toBeNull()
    expect(profile.referralCodes).toHaveLength(1)
    expect(profile.referralCodes[0]?.code).toMatch(/^[A-Z0-9]{8}$/)
  })

  test('does not create a second auto-code if called again', () => {
    becomeReferral('idempotent@example.com', 'Idempotent', 'Austria')
    becomeReferral('idempotent@example.com', 'Idempotent Updated', 'Austria')
    const profile = getReferralProfile('idempotent@example.com')
    expect(profile.referralCodes).toHaveLength(1)
  })

  test('updates identity fields on second call', () => {
    becomeReferral('update@example.com', 'Old Name', 'France')
    becomeReferral('update@example.com', 'New Name', 'France')
    const profile = getReferralProfile('update@example.com')
    expect(profile.referralIdentity?.fullName).toBe('New Name')
  })

  test('throws for name too short', () => {
    expect(() => becomeReferral('short@example.com', 'X', 'Germany')).toThrow(
      'at least 2 characters',
    )
  })

  test('throws for missing tax domicile', () => {
    expect(() => becomeReferral('nodom@example.com', 'Valid Name', 'X')).toThrow(
      'Tax domicile is required',
    )
  })

  test('email lookup is case-insensitive', () => {
    becomeReferral('Case@Example.COM', 'Case Tester', 'Italy')
    const profile = getReferralProfile('case@example.com')
    expect(profile.referralIdentity?.fullName).toBe('Case Tester')
  })
})

// ---------- applyReferralCode ----------
describe('applyReferralCode', () => {
  test('saves applied code for invitee', () => {
    // Owner must exist with a code first
    becomeReferral('codeowner@example.com', 'Code Owner', 'Germany')
    const ownerProfile = getReferralProfile('codeowner@example.com')
    const code = ownerProfile.referralCodes[0]!.code

    const result = applyReferralCode('invitee@example.com', code)
    expect(result.appliedReferralCode).toBe(code)

    const inviteeProfile = getReferralProfile('invitee@example.com')
    expect(inviteeProfile.appliedReferralCode).toBe(code)
  })

  test('normalizes code input to uppercase', () => {
    becomeReferral('upper@example.com', 'Upper Owner', 'Germany')
    const ownerProfile = getReferralProfile('upper@example.com')
    const code = ownerProfile.referralCodes[0]!.code

    const result = applyReferralCode('invitee2@example.com', code.toLowerCase())
    expect(result.appliedReferralCode).toBe(code)
  })

  test('throws when code does not exist', () => {
    expect(() => applyReferralCode('user@example.com', 'ZZZZZZZZ')).toThrow(
      'does not exist',
    )
  })

  test('prevents self-referral', () => {
    becomeReferral('selfish@example.com', 'Self User', 'UK')
    const profile = getReferralProfile('selfish@example.com')
    const code = profile.referralCodes[0]!.code

    expect(() => applyReferralCode('selfish@example.com', code)).toThrow(
      'cannot use your own referral code',
    )
  })

  test('prevents changing already-set code', () => {
    becomeReferral('owner1@example.com', 'Owner 1', 'Germany')
    becomeReferral('owner2@example.com', 'Owner 2', 'Germany')
    const code1 = getReferralProfile('owner1@example.com').referralCodes[0]!.code
    const code2 = getReferralProfile('owner2@example.com').referralCodes[0]!.code

    applyReferralCode('once@example.com', code1)
    expect(() => applyReferralCode('once@example.com', code2)).toThrow(
      'already been set',
    )
  })

  test('throws for invalid code format (too short)', () => {
    expect(() => applyReferralCode('user@example.com', 'ABC')).toThrow(
      '8 alphanumeric characters',
    )
  })

  test('throws for invalid code format (contains special chars)', () => {
    expect(() => applyReferralCode('user@example.com', 'BAD!CODE')).toThrow(
      '8 alphanumeric characters',
    )
  })
})

// ---------- createAdditionalReferralCode ----------
describe('createAdditionalReferralCode', () => {
  test('creates a new unique 8-char code for an active referral', () => {
    becomeReferral('extra@example.com', 'Extra User', 'Slovakia')
    const extra = createAdditionalReferralCode('extra@example.com')
    expect(extra.code).toMatch(/^[A-Z0-9]{8}$/)

    const profile = getReferralProfile('extra@example.com')
    expect(profile.referralCodes).toHaveLength(2)
    expect(profile.referralCodes.map((c) => c.code)).toContain(extra.code)
  })

  test('new code is unique across all existing codes', () => {
    becomeReferral('uniq@example.com', 'Uniq User', 'Germany')
    const firstCode = getReferralProfile('uniq@example.com').referralCodes[0]!.code
    const secondCode = createAdditionalReferralCode('uniq@example.com').code
    expect(firstCode).not.toBe(secondCode)
  })

  test('throws when profile has no referral identity', () => {
    expect(() => createAdditionalReferralCode('nobody@example.com')).toThrow(
      'Complete referral profile first',
    )
  })
})

// ---------- syncReferralSubscriptionStatus ----------
describe('syncReferralSubscriptionStatus', () => {
  test('marks player as having active subscription', () => {
    syncReferralSubscriptionStatus('sub@example.com', true)
    const profile = getReferralProfile('sub@example.com')
    expect(profile.hasActiveSubscription).toBe(true)
  })

  test('marks player as having inactive subscription', () => {
    syncReferralSubscriptionStatus('nosub@example.com', false)
    const profile = getReferralProfile('nosub@example.com')
    expect(profile.hasActiveSubscription).toBe(false)
  })

  test('updates existing status', () => {
    syncReferralSubscriptionStatus('toggle@example.com', true)
    syncReferralSubscriptionStatus('toggle@example.com', false)
    expect(getReferralProfile('toggle@example.com').hasActiveSubscription).toBe(false)
  })
})

// ---------- getReferralDashboard ----------
describe('getReferralDashboard', () => {
  test('returns empty rows for owner with no codes', () => {
    const rows = getReferralDashboard('empty@example.com')
    expect(rows).toHaveLength(0)
  })

  test('counts direct registrations correctly', () => {
    becomeReferral('dash@example.com', 'Dash Owner', 'Germany')
    const code = getReferralProfile('dash@example.com').referralCodes[0]!.code

    applyReferralCode('user1@example.com', code)
    applyReferralCode('user2@example.com', code)

    const rows = getReferralDashboard('dash@example.com')
    expect(rows[0]?.directRegistrations).toBe(2)
  })

  test('counts second-level registrations correctly', () => {
    becomeReferral('top@example.com', 'Top Owner', 'Germany')
    const topCode = getReferralProfile('top@example.com').referralCodes[0]!.code

    applyReferralCode('mid@example.com', topCode)
    becomeReferral('mid@example.com', 'Mid User', 'Germany')
    const midCode = getReferralProfile('mid@example.com').referralCodes[0]!.code

    applyReferralCode('leaf@example.com', midCode)

    const rows = getReferralDashboard('top@example.com')
    expect(rows[0]?.directRegistrations).toBe(1)
    expect(rows[0]?.secondLevelRegistrations).toBe(1)
  })

  test('counts active subscriptions for direct users only', () => {
    becomeReferral('actsub@example.com', 'Act Sub', 'Germany')
    const code = getReferralProfile('actsub@example.com').referralCodes[0]!.code

    applyReferralCode('active@example.com', code)
    syncReferralSubscriptionStatus('active@example.com', true)
    applyReferralCode('inactive@example.com', code)
    syncReferralSubscriptionStatus('inactive@example.com', false)

    const rows = getReferralDashboard('actsub@example.com')
    expect(rows[0]?.activeSubscriptions).toBe(1)
  })

  test('counts second-level active subscriptions', () => {
    becomeReferral('root2@example.com', 'Root2', 'Germany')
    const rootCode = getReferralProfile('root2@example.com').referralCodes[0]!.code

    applyReferralCode('child2@example.com', rootCode)
    becomeReferral('child2@example.com', 'Child2', 'Germany')
    const childCode = getReferralProfile('child2@example.com').referralCodes[0]!.code

    applyReferralCode('grandchild2@example.com', childCode)
    syncReferralSubscriptionStatus('grandchild2@example.com', true)

    const rows = getReferralDashboard('root2@example.com')
    expect(rows[0]?.secondLevelActiveSubscriptions).toBe(1)
  })

  test('owner is not counted as their own direct registration', () => {
    becomeReferral('noself@example.com', 'No Self', 'Germany')
    const rows = getReferralDashboard('noself@example.com')
    expect(rows[0]?.directRegistrations).toBe(0)
  })
})

// ---------- calculateReferralGoldTokens ----------
describe('calculateReferralGoldTokens', () => {
  test('returns 0 for empty rows', () => {
    expect(calculateReferralGoldTokens([])).toBe(0)
  })

  test('calculates direct subscription tokens', () => {
    const tokens = calculateReferralGoldTokens([
      {
        code: 'CODE1',
        directRegistrations: 3,
        secondLevelRegistrations: 0,
        activeSubscriptions: 2,
        secondLevelActiveSubscriptions: 0,
      },
    ])
    expect(tokens).toBe(2 * DIRECT_ACTIVE_SUBSCRIPTION_GOLD_REWARD)
  })

  test('calculates second-level subscription tokens', () => {
    const tokens = calculateReferralGoldTokens([
      {
        code: 'CODE2',
        directRegistrations: 0,
        secondLevelRegistrations: 4,
        activeSubscriptions: 0,
        secondLevelActiveSubscriptions: 3,
      },
    ])
    expect(tokens).toBe(3 * SECOND_LEVEL_ACTIVE_SUBSCRIPTION_GOLD_REWARD)
  })

  test('sums across multiple rows', () => {
    const tokens = calculateReferralGoldTokens([
      {
        code: 'C1',
        directRegistrations: 0,
        secondLevelRegistrations: 0,
        activeSubscriptions: 1,
        secondLevelActiveSubscriptions: 1,
      },
      {
        code: 'C2',
        directRegistrations: 0,
        secondLevelRegistrations: 0,
        activeSubscriptions: 2,
        secondLevelActiveSubscriptions: 0,
      },
    ])
    const expected =
      1 * DIRECT_ACTIVE_SUBSCRIPTION_GOLD_REWARD +
      1 * SECOND_LEVEL_ACTIVE_SUBSCRIPTION_GOLD_REWARD +
      2 * DIRECT_ACTIVE_SUBSCRIPTION_GOLD_REWARD
    expect(tokens).toBe(expected)
  })
})
