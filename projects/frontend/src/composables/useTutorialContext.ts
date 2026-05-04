import { ref, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import type { TutorialMilestoneStatus } from '@/types'

// ─── Milestone constants (mirror the backend TutorialMilestone class) ─────────

export const MILESTONE_FIRST_RESOURCE_SOLD = 'FIRST_RESOURCE_SOLD'
export const MILESTONE_FIRST_B2B_TRADE = 'FIRST_B2B_TRADE'
export const MILESTONE_FIRST_LOAN_TAKEN = 'FIRST_LOAN_TAKEN'
export const MILESTONE_FIRST_COMPETITOR_OBSERVED = 'FIRST_COMPETITOR_OBSERVED'
export const MILESTONE_FIRST_BRAND_ESTABLISHED = 'FIRST_BRAND_ESTABLISHED'

export const ALL_MILESTONES = [
  MILESTONE_FIRST_RESOURCE_SOLD,
  MILESTONE_FIRST_B2B_TRADE,
  MILESTONE_FIRST_LOAN_TAKEN,
  MILESTONE_FIRST_COMPETITOR_OBSERVED,
  MILESTONE_FIRST_BRAND_ESTABLISHED,
] as const

// ─── GraphQL ──────────────────────────────────────────────────────────────────

const TUTORIAL_PROGRESS_QUERY = `
  {
    tutorialProgress {
      milestone
      isCompleted
      completedAtUtc
    }
  }
`

const MARK_MILESTONE_MUTATION = `
  mutation MarkTutorialMilestoneComplete($input: MarkTutorialMilestoneCompleteInput!) {
    markTutorialMilestoneComplete(input: $input) {
      milestone
      isCompleted
      completedAtUtc
    }
  }
`

// ─── Composable ───────────────────────────────────────────────────────────────

/**
 * Provides tutorial milestone state management for the authenticated player.
 * Fetches milestone completion status from the backend and supports marking
 * individual milestones complete.
 */
export function useTutorialContext() {
  const auth = useAuthStore()

  const milestones = ref<TutorialMilestoneStatus[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  /** Whether all 5 milestones are completed. */
  const allCompleted = computed(
    () => milestones.value.length > 0 && milestones.value.every((m) => m.isCompleted),
  )

  /** Count of completed milestones. */
  const completedCount = computed(() => milestones.value.filter((m) => m.isCompleted).length)

  /** Returns true if the given milestone has been completed. */
  function isMilestoneCompleted(milestone: string): boolean {
    return milestones.value.find((m) => m.milestone === milestone)?.isCompleted ?? false
  }

  /** Fetches the player's tutorial progress from the backend. */
  async function fetchProgress(): Promise<void> {
    if (!auth.isAuthenticated) return
    loading.value = true
    error.value = null
    try {
      const data = await gqlRequest<{ tutorialProgress: TutorialMilestoneStatus[] }>(
        TUTORIAL_PROGRESS_QUERY,
      )
      milestones.value = data.tutorialProgress
    } catch {
      error.value = 'Failed to load tutorial progress.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Marks the given milestone as completed on the backend and updates local state.
   * Idempotent: safe to call multiple times for the same milestone.
   */
  async function completeMilestone(milestone: string): Promise<void> {
    if (!auth.isAuthenticated) return
    if (isMilestoneCompleted(milestone)) return
    try {
      const data = await gqlRequest<{ markTutorialMilestoneComplete: TutorialMilestoneStatus }>(
        MARK_MILESTONE_MUTATION,
        { input: { milestone } },
      )
      const updated = data.markTutorialMilestoneComplete
      const idx = milestones.value.findIndex((m) => m.milestone === milestone)
      if (idx >= 0) {
        milestones.value[idx] = updated
      } else {
        milestones.value.push(updated)
      }
    } catch {
      // Silently swallow errors to avoid interrupting user flow
    }
  }

  return {
    milestones,
    loading,
    error,
    allCompleted,
    completedCount,
    isMilestoneCompleted,
    fetchProgress,
    completeMilestone,
  }
}
