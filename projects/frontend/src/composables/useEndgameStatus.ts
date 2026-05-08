import { onMounted, onUnmounted, ref } from 'vue'
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
        name
        wealthUsd
      }
    }
  }
`

export function useEndgameStatus(pollIntervalMs = 30000) {
  const status = ref<EndgameStatus | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  let timer: ReturnType<typeof setInterval> | null = null

  const fetchStatus = async () => {
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

  const start = () => {
    void fetchStatus()
    if (timer) {
      clearInterval(timer)
    }
    timer = setInterval(() => {
      void fetchStatus()
    }, pollIntervalMs)
  }

  const stop = () => {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
  }

  onMounted(start)
  onUnmounted(stop)

  return {
    status,
    loading,
    error,
    fetchStatus,
  }
}
