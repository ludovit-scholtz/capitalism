import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { gqlRequest } from '@/lib/graphql'
import type { EndgameStatus } from '@/types'

const ENDGAME_STATUS_QUERY = `
  {
    endgameStatus {
      gameEnded
      winnerPlayerId
      winnerDisplayName
      winnerCompanyName
      gameEndedAtUtc
      winningThresholdUsd
      topRealWorldRichest {
        id
        rank
        name
        wealthUsd
      }
      leaderDisplayName
      leaderNetWorthUsd
    }
  }
`

/** Milestone thresholds (as fractions, e.g. 0.01 = 1%) */
export const ENDGAME_MILESTONES = [0.01, 0.1, 0.25, 0.5, 0.75, 0.9] as const
export type EndgameMilestone = (typeof ENDGAME_MILESTONES)[number]

/**
 * Pinia store for endgame status and "Race to the Top" benchmark progress.
 * Polls the `endgameStatus` query every 60 seconds and tracks which
 * milestone toast notifications have been shown this session.
 */
export const useEndgameStore = defineStore('endgame', () => {
  const status = ref<EndgameStatus | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  /** Set of milestone fractions already triggered this session (persisted in memory only). */
  const triggeredMilestones = ref<Set<number>>(new Set())

  let pollTimer: ReturnType<typeof setInterval> | null = null

  const isGameEnded = computed(() => status.value?.gameEnded ?? false)
  const winnerDisplayName = computed(() => status.value?.winnerDisplayName ?? null)
  const winningThresholdUsd = computed(() => status.value?.winningThresholdUsd ?? 0)

  /** Display name of the current server-wide leader (highest personal net worth). */
  const leaderDisplayName = computed(() => status.value?.leaderDisplayName ?? null)

  /** Server-wide leader's personal net worth in USD. */
  const leaderNetWorthUsd = computed(() => status.value?.leaderNetWorthUsd ?? 0)

  /**
   * True when the server-wide leader is within 10% of the winning threshold.
   * Used to show the Race to the Top banner in the header.
   */
  const isLeaderCloseToBenchmark = computed(() => {
    const threshold = winningThresholdUsd.value
    if (threshold <= 0 || isGameEnded.value) return false
    return leaderNetWorthUsd.value >= threshold * 0.9
  })

  /**
   * Computes progress percentage towards the endgame benchmark for a given net worth in USD.
   * Clamped to [0, 100].
   */
  function progressPercent(playerNetWorthUsd: number): number {
    const threshold = winningThresholdUsd.value
    if (threshold <= 0) return 0
    return Math.min(100, Math.max(0, Math.round((playerNetWorthUsd / threshold) * 100)))
  }

  /**
   * Returns the list of milestone fractions that have been newly crossed but not yet triggered.
   * Call this after updating player net worth to get toasts that need to be shown.
   */
  function checkMilestones(playerNetWorthUsd: number): EndgameMilestone[] {
    const threshold = winningThresholdUsd.value
    if (threshold <= 0 || isGameEnded.value) return []

    const ratio = playerNetWorthUsd / threshold
    const newMilestones: EndgameMilestone[] = []

    for (const milestone of ENDGAME_MILESTONES) {
      if (ratio >= milestone && !triggeredMilestones.value.has(milestone)) {
        triggeredMilestones.value.add(milestone)
        newMilestones.push(milestone)
      }
    }

    return newMilestones
  }

  async function fetchStatus() {
    loading.value = true
    try {
      const data = await gqlRequest<{ endgameStatus: EndgameStatus }>(ENDGAME_STATUS_QUERY)
      status.value = data.endgameStatus
      error.value = null
    } catch (reason: unknown) {
      error.value = reason instanceof Error ? reason.message : 'Failed to load endgame status'
    } finally {
      loading.value = false
    }
  }

  function startPolling(intervalMs = 60_000) {
    void fetchStatus()
    if (pollTimer) {
      clearInterval(pollTimer)
    }
    pollTimer = setInterval(() => {
      void fetchStatus()
    }, intervalMs)
  }

  function stopPolling() {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }

  function resetMilestones() {
    triggeredMilestones.value = new Set()
  }

  return {
    status,
    loading,
    error,
    isGameEnded,
    winnerDisplayName,
    winningThresholdUsd,
    leaderDisplayName,
    leaderNetWorthUsd,
    isLeaderCloseToBenchmark,
    triggeredMilestones,
    progressPercent,
    checkMilestones,
    fetchStatus,
    startPolling,
    stopPolling,
    resetMilestones,
  }
})
