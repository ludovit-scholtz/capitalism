<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { formatInGameTime } from '@/lib/gameTime'
import { useGameStateStore } from '@/stores/gameState'

const { t, locale } = useI18n()
const gameStateStore = useGameStateStore()
const { gameState } = storeToRefs(gameStateStore)

const tickProgressPercent = ref(0)
const cycleStartedAtMs = ref<number | null>(null)

let tickProgressTimer: ReturnType<typeof setInterval> | null = null

const formattedGameTime = computed(() => {
  if (!gameState.value?.currentGameTimeUtc) {
    return null
  }

  return formatInGameTime(gameState.value.currentGameTimeUtc, locale.value)
})

const isVisible = computed(() => gameState.value != null && formattedGameTime.value != null)

const tickIntervalMs = computed(() => {
  const seconds = gameState.value?.tickIntervalSeconds ?? 10
  const safeSeconds = seconds > 0 ? seconds : 10
  return safeSeconds * 1000
})

function resetTickProgressCycle() {
  // Reset to far-left exactly when a new tick value is received from the backend.
  tickProgressPercent.value = 0
  cycleStartedAtMs.value = Date.now()
}

function updateTickProgress() {
  if (!isVisible.value) {
    tickProgressPercent.value = 0
    cycleStartedAtMs.value = null
    return
  }

  if (cycleStartedAtMs.value === null) {
    cycleStartedAtMs.value = Date.now()
  }

  const elapsedMs = Date.now() - cycleStartedAtMs.value
  const normalized = Math.min(Math.max(elapsedMs / tickIntervalMs.value, 0), 1)
  tickProgressPercent.value = Math.round(normalized * 1000) / 10
}

function startTickProgressTimer() {
  stopTickProgressTimer()
  updateTickProgress()
  tickProgressTimer = setInterval(updateTickProgress, 100)
}

function stopTickProgressTimer() {
  if (tickProgressTimer) {
    clearInterval(tickProgressTimer)
    tickProgressTimer = null
  }
}

watch(
  () => gameState.value?.currentTick,
  (nextTick, previousTick) => {
    if (nextTick == null) {
      tickProgressPercent.value = 0
      cycleStartedAtMs.value = null
      return
    }

    if (previousTick == null || nextTick !== previousTick) {
      resetTickProgressCycle()
    }
  },
  { immediate: true },
)

watch(
  () => gameState.value?.tickIntervalSeconds,
  (nextValue, previousValue) => {
    if (nextValue !== previousValue) {
      resetTickProgressCycle()
    }
  },
)

onMounted(() => {
  startTickProgressTimer()
})

onUnmounted(() => {
  stopTickProgressTimer()
})
</script>

<template>
  <div v-if="isVisible && formattedGameTime" class="game-time-chip hidden sm:inline-flex items-center gap-1.5 px-2.5 border border-divider rounded-md h-9" :title="t('nav.gameTime')">
    <span class="game-time-chip-progress" :style="{ width: `${tickProgressPercent}%` }" aria-hidden="true" />
    <font-awesome-icon :icon="['fas', 'clock']" class="game-time-chip-content text-muted text-[0.75rem]" />
    <span class="game-time-chip-content text-[0.75rem] text-muted tabular-nums whitespace-nowrap">{{ formattedGameTime }}</span>
    <span
      v-if="gameState?.currentQuarterLabel"
      class="game-time-chip-content game-quarter-badge text-[0.65rem] font-bold px-1.5 py-0.5 rounded bg-primary/15 text-primary"
      :title="t('nav.currentQuarter')"
    >{{ gameState.currentQuarterLabel }}</span>
  </div>
</template>

<style scoped>
.game-time-chip {
  position: relative;
  overflow: hidden;
}

.game-time-chip-progress {
  position: absolute;
  inset: 0 auto 0 0;
  background: linear-gradient(90deg, rgba(47, 122, 255, 0.2), rgba(47, 122, 255, 0.08));
  transition: width 120ms linear;
}

.game-time-chip-content {
  position: relative;
  z-index: 1;
}
</style>
