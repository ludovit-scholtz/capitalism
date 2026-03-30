import { storeToRefs } from 'pinia'
import { onUnmounted, watch } from 'vue'
import { useGameStateStore } from '@/stores/gameState'

type TickRefreshOptions = {
  enabled?: () => boolean
}

export function useTickRefresh(refresh: () => Promise<void> | void, options: TickRefreshOptions = {}) {
  const gameStateStore = useGameStateStore()
  const { gameState } = storeToRefs(gameStateStore)
  let lastSeenTick: number | null = null

  const stop = watch(
    () => gameState.value?.currentTick ?? null,
    (tick) => {
      if (tick === null) {
        return
      }

      if (lastSeenTick === null) {
        lastSeenTick = tick
        return
      }

      if (tick === lastSeenTick) {
        return
      }

      lastSeenTick = tick
      if (options.enabled && !options.enabled()) {
        return
      }

      void refresh()
    },
  )

  onUnmounted(stop)
}