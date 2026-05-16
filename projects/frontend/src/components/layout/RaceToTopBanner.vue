<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useEndgameStore } from '@/stores/endgame'

const { t } = useI18n()
const endgameStore = useEndgameStore()

const dismissed = ref(false)

function dismiss() {
  dismissed.value = true
}

function formatUsdShort(value: number): string {
  if (value >= 1e12) return `$${(value / 1e12).toFixed(1)}T`
  if (value >= 1e9) return `$${(value / 1e9).toFixed(1)}B`
  if (value >= 1e6) return `$${(value / 1e6).toFixed(1)}M`
  return `$${value.toFixed(0)}`
}
</script>

<template>
  <div
    v-if="endgameStore.isLeaderCloseToBenchmark && !dismissed"
    class="race-to-top-banner"
    role="status"
    aria-live="polite"
  >
    <font-awesome-icon :icon="['fas', 'trophy']" class="banner-icon" aria-hidden="true" />
    <span class="banner-text">
      {{ t('endgame.raceToTopBannerTitle') }}
      <strong>{{ endgameStore.leaderDisplayName }}</strong>
      {{ t('endgame.raceToTopBannerLeader') }}
      <strong>{{ formatUsdShort(endgameStore.leaderNetWorthUsd) }}</strong>
      &mdash;
      {{
        t('endgame.raceToTopBannerDistance', {
          pct: Math.round((endgameStore.leaderNetWorthUsd / endgameStore.winningThresholdUsd) * 100),
        })
      }}
    </span>
    <button
      class="banner-dismiss"
      :aria-label="t('endgame.raceToTopBannerDismiss')"
      @click="dismiss"
    >
      <font-awesome-icon :icon="['fas', 'xmark']" aria-hidden="true" />
    </button>
  </div>
</template>

<style scoped>
.race-to-top-banner {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.375rem 0.75rem;
  border-radius: 9999px;
  background: rgba(255, 179, 0, 0.15);
  border: 1px solid rgba(255, 179, 0, 0.45);
  color: #ffd166;
  font-size: 0.75rem;
  line-height: 1.4;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 32rem;
}

.banner-icon {
  flex-shrink: 0;
  color: #ffd166;
}

.banner-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
}

.banner-dismiss {
  flex-shrink: 0;
  background: transparent;
  border: none;
  cursor: pointer;
  color: inherit;
  opacity: 0.7;
  padding: 0;
  display: flex;
  align-items: center;
  transition: opacity 0.15s;
}

.banner-dismiss:hover {
  opacity: 1;
}
</style>
