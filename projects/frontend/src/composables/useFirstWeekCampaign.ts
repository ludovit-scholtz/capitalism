import { computed, onMounted, onUnmounted, ref } from 'vue'
import { gqlRequest } from '@/lib/graphql'
import {
  CLAIM_CAMPAIGN_REWARD_MUTATION,
  FIRST_WEEK_CAMPAIGN_QUERY,
  isCampaignVisible,
  type ClaimCampaignRewardResult,
  type FirstWeekCampaign,
} from '@/lib/firstWeekCampaign'

/** Campaign progress changes as the player acts, so the card re-reads it at this cadence while the dashboard is open. */
const REFRESH_INTERVAL_MS = 60_000

/** Campaign state and actions without lifecycle hooks, so the logic is testable outside a component. */
export function createFirstWeekCampaignState(now: () => number = Date.now) {
  const campaign = ref<FirstWeekCampaign | null>(null)
  const claimingCode = ref<string | null>(null)
  const claimError = ref<string | null>(null)
  const lastClaim = ref<ClaimCampaignRewardResult | null>(null)

  const visible = computed(() => isCampaignVisible(campaign.value, now()))

  /** A failed read hides the card instead of showing an error: the campaign is a bonus, not a blocker for the dashboard. */
  async function load(): Promise<void> {
    try {
      const data = await gqlRequest<{ firstWeekCampaign: FirstWeekCampaign | null }>(FIRST_WEEK_CAMPAIGN_QUERY)
      campaign.value = data?.firstWeekCampaign ?? null
    } catch {
      campaign.value = null
    }
  }

  async function claim(goalCode: string): Promise<ClaimCampaignRewardResult | null> {
    if (claimingCode.value) return null
    claimingCode.value = goalCode
    claimError.value = null
    try {
      const data = await gqlRequest<{ claimCampaignReward: ClaimCampaignRewardResult }>(CLAIM_CAMPAIGN_REWARD_MUTATION, {
        input: { goalCode },
      })
      lastClaim.value = data.claimCampaignReward
      await load()
      return data.claimCampaignReward
    } catch (reason: unknown) {
      claimError.value = reason instanceof Error ? reason.message : 'Failed to claim the reward'
      return null
    } finally {
      claimingCode.value = null
    }
  }

  return { campaign, visible, claimingCode, claimError, lastClaim, load, claim }
}

export function useFirstWeekCampaign(refreshIntervalMs = REFRESH_INTERVAL_MS) {
  const state = createFirstWeekCampaignState()
  let timer: ReturnType<typeof setInterval> | null = null

  onMounted(() => {
    void state.load()
    timer = setInterval(() => void state.load(), refreshIntervalMs)
  })
  onUnmounted(() => {
    if (timer) clearInterval(timer)
    timer = null
  })

  return state
}
