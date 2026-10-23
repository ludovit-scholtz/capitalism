import type { Page } from '@playwright/test'

/** Campaign goal codes in day order, matching the server catalog (`FirstWeekCampaignCatalog`). */
const GOAL_CODES = [
  'FIRST_SALE',
  'SECOND_BUILDING',
  'TAKE_A_LOAN',
  'FIRST_B2B_TRADE',
  'BUILD_A_BRAND',
  'SCOUT_COMPETITORS',
  'GROW_TO_THREE_BUILDINGS',
]

export interface MockCampaignGoal {
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

export interface MockCampaignState {
  started: boolean
  startedAtUtc: string | null
  dayNumber: number
  goals: MockCampaignGoal[]
  claimRequests: string[]
}

/** A campaign that began `hoursAgo` hours ago; goals up to the current day are unlocked and none is complete. */
export function makeCampaignState(options: { dayNumber?: number; hoursAgo?: number; started?: boolean } = {}): MockCampaignState {
  const { dayNumber = 1, hoursAgo = 2, started = true } = options
  return {
    started,
    startedAtUtc: started ? new Date(Date.now() - hoursAgo * 3_600_000).toISOString() : null,
    dayNumber: started ? dayNumber : 0,
    claimRequests: [],
    goals: GOAL_CODES.map((code, index) => ({
      code,
      day: index + 1,
      progress: 0,
      target: code === 'SECOND_BUILDING' ? 2 : code === 'GROW_TO_THREE_BUILDINGS' ? 3 : 1,
      isUnlocked: started && index + 1 <= dayNumber,
      isComplete: false,
      isClaimed: false,
      canClaim: false,
      rewardAmount: (index + 1) * 2000,
      rewardCurrencyCode: 'USD',
    })),
  }
}

/** Marks a goal finished on the "server" so its reward can be claimed. */
export function completeCampaignGoal(state: MockCampaignState, code: string): void {
  const goal = state.goals.find((g) => g.code === code)
  if (!goal) throw new Error(`Unknown campaign goal ${code}`)
  goal.progress = goal.target
  goal.isComplete = true
  goal.canClaim = goal.isUnlocked && !goal.isClaimed
}

/**
 * Serves the first-week campaign operations on top of the shared mock API without touching it: Playwright runs the most
 * recently registered route first, and everything that is not a campaign operation falls through to `setupMockApi`.
 * Call after `setupMockApi` and before navigating. Matches the full operation names, not substrings of other operations.
 */
export async function mockFirstWeekCampaign(page: Page, state: MockCampaignState): Promise<void> {
  await page.route('**/graphql', async (route) => {
    const body = route.request().postDataJSON() as { query?: string; variables?: { input?: { goalCode?: string } } } | null
    const query = body?.query ?? ''

    if (/\bclaimCampaignReward\s*\(/.test(query)) {
      const code = body?.variables?.input?.goalCode ?? ''
      state.claimRequests.push(code)
      const goal = state.goals.find((g) => g.code === code)
      const claimable = !!goal && goal.canClaim && !goal.isClaimed
      if (goal && claimable) {
        goal.isClaimed = true
        goal.canClaim = false
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            claimCampaignReward: {
              claimed: claimable,
              goalCode: code,
              amount: goal?.rewardAmount ?? 0,
              currencyCode: 'USD',
            },
          },
        }),
      })
      return
    }

    if (/\bfirstWeekCampaign\s*\{/.test(query)) {
      const claimedCount = state.goals.filter((g) => g.isClaimed).length
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: {
            firstWeekCampaign: {
              started: state.started,
              dayNumber: state.dayNumber,
              startedAtUtc: state.startedAtUtc,
              claimedCount,
              allClaimed: claimedCount === state.goals.length,
              goals: state.goals,
            },
          },
        }),
      })
      return
    }

    await route.fallback()
  })
}
