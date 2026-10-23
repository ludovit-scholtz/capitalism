/**
 * Pure helpers for the guided first-week campaign card (#602). The server owns day number, progress, completion
 * and rewards; this module only decides what to show.
 */

export const CAMPAIGN_DAYS = 7

const DAY_MS = 24 * 60 * 60 * 1000

export interface CampaignGoal {
  code: string
  day: number
  progress: number
  target: number
  isUnlocked: boolean
  isComplete: boolean
  isClaimed: boolean
  canClaim: boolean
  rewardAmount: number
  rewardCurrencyCode: string
}

export interface FirstWeekCampaign {
  started: boolean
  dayNumber: number
  startedAtUtc: string | null
  claimedCount: number
  allClaimed: boolean
  goals: CampaignGoal[]
}

export interface ClaimCampaignRewardResult {
  claimed: boolean
  goalCode: string
  amount: number
  currencyCode: string
}

export const FIRST_WEEK_CAMPAIGN_QUERY = `
  query FirstWeekCampaign {
    firstWeekCampaign {
      started
      dayNumber
      startedAtUtc
      claimedCount
      allClaimed
      goals {
        code
        day
        progress
        target
        isUnlocked
        isComplete
        isClaimed
        canClaim
        rewardAmount
        rewardCurrencyCode
      }
    }
  }
`

export const CLAIM_CAMPAIGN_REWARD_MUTATION = `
  mutation ClaimCampaignReward($input: ClaimCampaignRewardInput!) {
    claimCampaignReward(input: $input) {
      claimed
      goalCode
      amount
      currencyCode
    }
  }
`

/**
 * The card is for players who finished onboarding, are inside their first seven days and still have something to
 * claim. It disappears once the week is over (the server keeps counting real days since onboarding finished).
 */
export function isCampaignVisible(campaign: FirstWeekCampaign | null, nowMs: number): boolean {
  if (!campaign || !campaign.started || campaign.allClaimed || !campaign.startedAtUtc) return false
  const startedMs = Date.parse(campaign.startedAtUtc)
  if (Number.isNaN(startedMs)) return false
  return nowMs - startedMs < CAMPAIGN_DAYS * DAY_MS
}

/** The goal to feature: a claimable reward first, otherwise the earliest unlocked goal still to do. */
export function currentGoal(campaign: FirstWeekCampaign | null): CampaignGoal | null {
  if (!campaign) return null
  const open = campaign.goals.filter((goal) => goal.isUnlocked && !goal.isClaimed).sort((a, b) => a.day - b.day)
  return open.find((goal) => goal.canClaim) ?? open[0] ?? null
}

export type DayState = 'claimed' | 'claimable' | 'open' | 'locked'

export interface DayStripEntry {
  day: number
  code: string
  state: DayState
  isToday: boolean
}

/** One entry per campaign day for the progress strip. */
export function buildDayStrip(campaign: FirstWeekCampaign | null): DayStripEntry[] {
  if (!campaign) return []
  return [...campaign.goals]
    .sort((a, b) => a.day - b.day)
    .map((goal) => ({
      day: goal.day,
      code: goal.code,
      state: goal.isClaimed ? 'claimed' : goal.canClaim ? 'claimable' : goal.isUnlocked ? 'open' : 'locked',
      isToday: goal.day === campaign.dayNumber,
    }))
}

/** Where the player can act on a goal; null when the goal needs no navigation or the target needs a company that is not known. */
export function goalActionPath(code: string, companyId: string | null): string | null {
  switch (code) {
    case 'SECOND_BUILDING':
    case 'GROW_TO_THREE_BUILDINGS':
      return companyId ? `/buy-building/${companyId}` : null
    case 'TAKE_A_LOAN':
      return '/banking'
    case 'FIRST_B2B_TRADE':
      return '/contracts'
    case 'SCOUT_COMPETITORS':
      return '/cities'
    default:
      return null
  }
}
