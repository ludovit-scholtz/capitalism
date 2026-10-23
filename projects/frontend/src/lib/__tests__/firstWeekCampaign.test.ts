import { describe, expect, it } from 'vitest'
import {
  CAMPAIGN_DAYS,
  CLAIM_CAMPAIGN_REWARD_MUTATION,
  FIRST_WEEK_CAMPAIGN_QUERY,
  buildDayStrip,
  currentGoal,
  goalActionPath,
  isCampaignVisible,
  type CampaignGoal,
  type FirstWeekCampaign,
} from '../firstWeekCampaign'

const CODES = [
  'FIRST_SALE',
  'SECOND_BUILDING',
  'TAKE_A_LOAN',
  'FIRST_B2B_TRADE',
  'BUILD_A_BRAND',
  'SCOUT_COMPETITORS',
  'GROW_TO_THREE_BUILDINGS',
]

function goal(day: number, overrides: Partial<CampaignGoal> = {}): CampaignGoal {
  return {
    code: CODES[day - 1]!,
    day,
    progress: 0,
    target: 1,
    isUnlocked: false,
    isComplete: false,
    isClaimed: false,
    canClaim: false,
    rewardAmount: day * 2000,
    rewardCurrencyCode: 'USD',
    ...overrides,
  }
}

function campaign(
  dayNumber: number,
  goalOverrides: Record<number, Partial<CampaignGoal>> = {},
  overrides: Partial<FirstWeekCampaign> = {},
): FirstWeekCampaign {
  const goals = CODES.map((_, index) => {
    const day = index + 1
    return goal(day, { isUnlocked: day <= dayNumber, ...goalOverrides[day] })
  })
  return {
    started: dayNumber > 0,
    dayNumber,
    startedAtUtc: '2026-10-01T00:00:00Z',
    claimedCount: goals.filter((g) => g.isClaimed).length,
    allClaimed: goals.every((g) => g.isClaimed),
    goals,
    ...overrides,
  }
}

const DAY_MS = 24 * 60 * 60 * 1000
const START_MS = Date.parse('2026-10-01T00:00:00Z')

describe('query strings', () => {
  it('asks for every field the card renders', () => {
    for (const field of [
      'firstWeekCampaign',
      'started',
      'dayNumber',
      'startedAtUtc',
      'claimedCount',
      'allClaimed',
      'goals',
      'code',
      'day',
      'progress',
      'target',
      'isUnlocked',
      'isComplete',
      'isClaimed',
      'canClaim',
      'rewardAmount',
      'rewardCurrencyCode',
    ]) {
      expect(FIRST_WEEK_CAMPAIGN_QUERY).toContain(field)
    }
  })

  it('has balanced braces so a malformed query cannot silently leave the card empty', () => {
    for (const query of [FIRST_WEEK_CAMPAIGN_QUERY, CLAIM_CAMPAIGN_REWARD_MUTATION]) {
      expect(query.split('{').length).toBe(query.split('}').length)
    }
  })

  it('claims through the server mutation with the input object', () => {
    expect(CLAIM_CAMPAIGN_REWARD_MUTATION).toContain('claimCampaignReward(input: $input)')
    expect(CLAIM_CAMPAIGN_REWARD_MUTATION).toContain('$input: ClaimCampaignRewardInput!')
    for (const field of ['claimed', 'goalCode', 'amount', 'currencyCode']) {
      expect(CLAIM_CAMPAIGN_REWARD_MUTATION).toContain(field)
    }
  })
})

describe('isCampaignVisible', () => {
  it('is hidden when the campaign did not load', () => {
    expect(isCampaignVisible(null, START_MS)).toBe(false)
  })

  it('is hidden before onboarding is finished', () => {
    expect(isCampaignVisible(campaign(0, {}, { started: false, startedAtUtc: null }), START_MS)).toBe(false)
  })

  it('is shown on day 1 and through day 7', () => {
    expect(isCampaignVisible(campaign(1), START_MS + 1000)).toBe(true)
    expect(isCampaignVisible(campaign(7), START_MS + 6.9 * DAY_MS)).toBe(true)
  })

  it('is hidden once the seven days are over', () => {
    expect(isCampaignVisible(campaign(7), START_MS + CAMPAIGN_DAYS * DAY_MS)).toBe(false)
    expect(isCampaignVisible(campaign(7), START_MS + 30 * DAY_MS)).toBe(false)
  })

  it('is hidden when every goal has been claimed', () => {
    const all = campaign(7, Object.fromEntries(CODES.map((_, i) => [i + 1, { isComplete: true, isClaimed: true }])))
    expect(isCampaignVisible(all, START_MS + 2 * DAY_MS)).toBe(false)
  })

  it('is hidden for an unparseable start time', () => {
    expect(isCampaignVisible(campaign(1, {}, { startedAtUtc: 'nope' }), START_MS)).toBe(false)
  })
})

describe('currentGoal', () => {
  it('is the unlocked goal still to do on day 1', () => {
    expect(currentGoal(campaign(1))?.code).toBe('FIRST_SALE')
  })

  it('prefers a goal that can be claimed over one still in progress', () => {
    const c = campaign(3, { 1: {}, 2: { isComplete: true, canClaim: true } })
    expect(currentGoal(c)?.code).toBe('SECOND_BUILDING')
  })

  it('moves on to the next open goal after a claim', () => {
    const c = campaign(2, { 1: { isComplete: true, isClaimed: true } })
    expect(currentGoal(c)?.code).toBe('SECOND_BUILDING')
  })

  it('is null when all unlocked goals are claimed (come back tomorrow)', () => {
    const c = campaign(2, { 1: { isComplete: true, isClaimed: true }, 2: { isComplete: true, isClaimed: true } })
    expect(currentGoal(c)).toBeNull()
  })

  it('is null without a campaign', () => {
    expect(currentGoal(null)).toBeNull()
  })
})

describe('buildDayStrip', () => {
  it('has one entry per day in order', () => {
    expect(buildDayStrip(campaign(1)).map((d) => d.day)).toEqual([1, 2, 3, 4, 5, 6, 7])
  })

  it('classifies each day', () => {
    const strip = buildDayStrip(
      campaign(4, {
        1: { isComplete: true, isClaimed: true },
        2: { isComplete: true, canClaim: true },
      }),
    )
    expect(strip.map((d) => d.state)).toEqual(['claimed', 'claimable', 'open', 'open', 'locked', 'locked', 'locked'])
  })

  it('marks the current day', () => {
    const strip = buildDayStrip(campaign(3))
    expect(strip.filter((d) => d.isToday).map((d) => d.day)).toEqual([3])
  })

  it('is empty without a campaign', () => {
    expect(buildDayStrip(null)).toEqual([])
  })
})

describe('goalActionPath', () => {
  it('sends building goals to the buy-building page of the company', () => {
    expect(goalActionPath('SECOND_BUILDING', 'comp-1')).toBe('/buy-building/comp-1')
    expect(goalActionPath('GROW_TO_THREE_BUILDINGS', 'comp-1')).toBe('/buy-building/comp-1')
  })

  it('has no company-specific link without a company', () => {
    expect(goalActionPath('SECOND_BUILDING', null)).toBeNull()
  })

  it('links the loan, trade and scouting goals to their pages', () => {
    expect(goalActionPath('TAKE_A_LOAN', null)).toBe('/banking')
    expect(goalActionPath('FIRST_B2B_TRADE', null)).toBe('/contracts')
    expect(goalActionPath('SCOUT_COMPETITORS', null)).toBe('/cities')
  })

  it('has no link for goals that happen on their own or are unknown', () => {
    expect(goalActionPath('FIRST_SALE', 'comp-1')).toBeNull()
    expect(goalActionPath('SOMETHING_NEW', 'comp-1')).toBeNull()
  })
})
