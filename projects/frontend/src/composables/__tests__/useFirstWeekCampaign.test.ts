import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CLAIM_CAMPAIGN_REWARD_MUTATION, FIRST_WEEK_CAMPAIGN_QUERY, type FirstWeekCampaign } from '@/lib/firstWeekCampaign'

const gqlRequestMock = vi.fn()

vi.mock('@/lib/graphql', () => ({
  gqlRequest: (...args: unknown[]) => gqlRequestMock(...args),
}))

const NOW = Date.parse('2026-10-02T12:00:00Z')

function campaignPayload(overrides: Partial<FirstWeekCampaign> = {}): FirstWeekCampaign {
  return {
    started: true,
    dayNumber: 2,
    startedAtUtc: '2026-10-01T00:00:00Z',
    claimedCount: 0,
    allClaimed: false,
    goals: [
      {
        code: 'FIRST_SALE',
        day: 1,
        progress: 1,
        target: 1,
        isUnlocked: true,
        isComplete: true,
        isClaimed: false,
        canClaim: true,
        rewardAmount: 2000,
        rewardCurrencyCode: 'USD',
      },
    ],
    ...overrides,
  }
}

describe('createFirstWeekCampaignState', () => {
  beforeEach(() => {
    gqlRequestMock.mockReset()
  })

  it('loads the campaign with the campaign query', async () => {
    const { createFirstWeekCampaignState } = await import('../useFirstWeekCampaign')
    gqlRequestMock.mockResolvedValueOnce({ firstWeekCampaign: campaignPayload() })

    const state = createFirstWeekCampaignState(() => NOW)
    await state.load()

    expect(gqlRequestMock).toHaveBeenCalledWith(FIRST_WEEK_CAMPAIGN_QUERY)
    expect(state.campaign.value?.dayNumber).toBe(2)
    expect(state.visible.value).toBe(true)
  })

  it('stays hidden when the read fails', async () => {
    const { createFirstWeekCampaignState } = await import('../useFirstWeekCampaign')
    gqlRequestMock.mockRejectedValueOnce(new Error('boom'))

    const state = createFirstWeekCampaignState(() => NOW)
    await state.load()

    expect(state.campaign.value).toBeNull()
    expect(state.visible.value).toBe(false)
  })

  it('stays hidden before onboarding is finished', async () => {
    const { createFirstWeekCampaignState } = await import('../useFirstWeekCampaign')
    gqlRequestMock.mockResolvedValueOnce({ firstWeekCampaign: campaignPayload({ started: false, dayNumber: 0, startedAtUtc: null }) })

    const state = createFirstWeekCampaignState(() => NOW)
    await state.load()

    expect(state.visible.value).toBe(false)
  })

  it('claims a goal with the mutation, then reloads the campaign', async () => {
    const { createFirstWeekCampaignState } = await import('../useFirstWeekCampaign')
    gqlRequestMock
      .mockResolvedValueOnce({ claimCampaignReward: { claimed: true, goalCode: 'FIRST_SALE', amount: 2000, currencyCode: 'USD' } })
      .mockResolvedValueOnce({
        firstWeekCampaign: campaignPayload({
          claimedCount: 1,
          goals: [{ ...campaignPayload().goals[0]!, isClaimed: true, canClaim: false }],
        }),
      })

    const state = createFirstWeekCampaignState(() => NOW)
    const result = await state.claim('FIRST_SALE')

    expect(gqlRequestMock).toHaveBeenNthCalledWith(1, CLAIM_CAMPAIGN_REWARD_MUTATION, { input: { goalCode: 'FIRST_SALE' } })
    expect(gqlRequestMock).toHaveBeenNthCalledWith(2, FIRST_WEEK_CAMPAIGN_QUERY)
    expect(result?.claimed).toBe(true)
    expect(state.lastClaim.value?.amount).toBe(2000)
    expect(state.campaign.value?.goals[0]?.isClaimed).toBe(true)
    expect(state.claimingCode.value).toBeNull()
  })

  it('reports a failed claim without reloading and re-enables claiming', async () => {
    const { createFirstWeekCampaignState } = await import('../useFirstWeekCampaign')
    gqlRequestMock.mockRejectedValueOnce(new Error('Finish the goal before claiming its reward.'))

    const state = createFirstWeekCampaignState(() => NOW)
    const result = await state.claim('FIRST_SALE')

    expect(result).toBeNull()
    expect(state.claimError.value).toBe('Finish the goal before claiming its reward.')
    expect(state.claimingCode.value).toBeNull()
    expect(gqlRequestMock).toHaveBeenCalledTimes(1)
  })

  it('ignores a second claim while one is in flight', async () => {
    const { createFirstWeekCampaignState } = await import('../useFirstWeekCampaign')
    let release: (value: unknown) => void = () => {}
    gqlRequestMock
      .mockReturnValueOnce(new Promise((resolve) => (release = resolve)))
      .mockResolvedValueOnce({ firstWeekCampaign: campaignPayload() })

    const state = createFirstWeekCampaignState(() => NOW)
    const first = state.claim('FIRST_SALE')
    const second = await state.claim('FIRST_SALE')
    release({ claimCampaignReward: { claimed: true, goalCode: 'FIRST_SALE', amount: 2000, currencyCode: 'USD' } })
    await first

    expect(second).toBeNull()
    expect(gqlRequestMock).toHaveBeenCalledTimes(2)
  })
})
