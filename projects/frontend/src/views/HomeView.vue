<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useGameStateStore } from '@/stores/gameState'
import { deepEqual } from '@/lib/utils'
import { formatInGameTime } from '@/lib/gameTime'
import { formatCompactMoney } from '@/lib/currencyFormat'
import type { PlayerRanking, GameState } from '@/types'

const { t, locale } = useI18n()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const { gameState } = storeToRefs(gameStateStore)

const rankings = ref<PlayerRanking[]>([])
const loading = ref(true)
const formattedGameTime = computed(() =>
  gameState.value?.currentGameTimeUtc ? formatInGameTime(gameState.value.currentGameTimeUtc, locale.value) : '',
)

async function loadHomeData(isRefresh = false) {
  if (!isRefresh) {
    loading.value = true
  }
  try {
    const [rankData, stateData] = await Promise.all([
      gqlRequest<{ rankings: PlayerRanking[] }>('{ rankings { playerId displayName totalWealth totalWealthUsd personalCash sharesValue companyCount } }'),
      gqlRequest<{ gameState: GameState }>(
        '{ gameState { currentTick lastTickAtUtc tickIntervalSeconds taxCycleTicks taxRate currentGameYear currentGameTimeUtc ticksPerDay ticksPerYear nextTaxTick nextTaxGameTimeUtc nextTaxGameYear } }',
      ),
    ])
    if (!deepEqual(rankings.value, rankData.rankings)) {
      rankings.value = rankData.rankings
    }
    if (!deepEqual(gameState.value, stateData.gameState)) {
      gameState.value = stateData.gameState
    }
  } catch {
    // Silently fail on home page
  } finally {
    if (!isRefresh) {
      loading.value = false
    }
  }
}

onMounted(async () => {
  auth.initFromStorage()
  if (auth.isAuthenticated) {
    void auth.fetchMe()
  }

  await loadHomeData()
})

useTickRefresh(() => loadHomeData(true))
</script>

<template>
  <div>
    <!-- Hero Section -->
    <section class="relative border-b border-divider overflow-hidden py-20 pb-16">
      <!-- Background video + overlay -->
      <div class="absolute inset-0 -z-10">
        <div class="absolute inset-0 bg-[rgba(13,17,23,0.6)] z-10"></div>
        <video autoplay muted playsinline class="w-full h-full object-cover">
          <source src="../assets/hero-video.webm" type="video/webm" />
        </video>
      </div>

      <div class="container hero-content text-center relative z-20">
        <h1 class="hero-title">{{ t('home.heroTitle') }}</h1>
        <p class="text-lg max-w-xl mx-auto mb-8 text-[var(--color-hero-subtitle)]">
          {{ t('home.heroDescription') }}
        </p>
        <div class="flex justify-center gap-4">
          <RouterLink v-if="!auth.isAuthenticated" to="/onboarding" class="btn btn-primary">
            {{ t('home.getStarted') }}
          </RouterLink>
          <RouterLink
            v-else-if="auth.player && !auth.player.onboardingCompletedAtUtc"
            to="/onboarding"
            class="btn btn-primary"
          >
            {{ t('home.startOnboarding') }}
          </RouterLink>
          <RouterLink v-else to="/dashboard" class="btn btn-primary">
            {{ t('home.goToDashboard') }}
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Game Status Cards -->
    <section v-if="gameState" class="container py-8">
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div
          class="bg-card border border-divider rounded-lg p-5 text-center"
          :title="t('home.currentTick', { tick: gameState.currentTick })"
        >
          <span class="block text-xs text-muted uppercase tracking-wide mb-2">{{
            t('home.currentTime')
          }}</span>
          <span class="text-2xl font-bold text-body">{{ formattedGameTime }}</span>
        </div>
        <div class="bg-card border border-divider rounded-lg p-5 text-center">
          <span class="block text-xs text-muted uppercase tracking-wide mb-2">{{
            t('home.taxRate')
          }}</span>
          <span class="text-2xl font-bold text-body">{{ gameState.taxRate }}%</span>
        </div>
        <div class="bg-card border border-divider rounded-lg p-5 text-center">
          <span class="block text-xs text-muted uppercase tracking-wide mb-2">{{
            t('home.activePlayers')
          }}</span>
          <span class="text-2xl font-bold text-body">{{ rankings.length }}</span>
        </div>
      </div>
    </section>

    <!-- Leaderboard -->
    <section class="container pb-16 pt-2">
      <div class="flex items-center justify-between mb-4 gap-4">
        <h2 class="text-2xl font-bold text-body">{{ t('home.leaderboard') }}</h2>
        <RouterLink to="/leaderboard" class="btn btn-secondary text-sm whitespace-nowrap">
          {{ t('home.viewFullLeaderboard') }}
        </RouterLink>
      </div>

      <div v-if="loading" class="text-center py-8 text-muted">{{ t('common.loading') }}</div>
      <div v-else-if="rankings.length === 0" class="text-center py-8 text-muted">
        {{ t('home.noPlayers') }}
      </div>
      <table v-else class="w-full border-collapse">
        <thead>
          <tr>
            <th
              class="text-left px-4 py-3 text-xs text-muted font-semibold uppercase border-b border-divider"
            >
              #
            </th>
            <th
              class="text-left px-4 py-3 text-xs text-muted font-semibold uppercase border-b border-divider"
            >
              {{ t('home.playerName') }}
            </th>
            <th
              class="text-left px-4 py-3 text-xs text-muted font-semibold uppercase border-b border-divider"
            >
              {{ t('home.wealth') }}
            </th>
            <th
              class="text-left px-4 py-3 text-xs text-muted font-semibold uppercase border-b border-divider"
            >
              {{ t('home.companies') }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(rank, index) in rankings.slice(0, 5)"
            :key="rank.playerId"
            class="hover:bg-[var(--color-hover)] transition-colors"
          >
            <td class="px-4 py-3 font-bold text-brand border-b border-divider">
              {{ index + 1 }}
            </td>
            <td class="px-4 py-3 text-body border-b border-divider">{{ rank.displayName }}</td>
            <!-- wealth class kept for E2E test selector backward compatibility -->
            <td class="px-4 py-3 font-semibold text-good wealth border-b border-divider">
              {{ formatCompactMoney(rank.totalWealthUsd, 'USD', locale) }}
            </td>
            <td class="px-4 py-3 text-body border-b border-divider">{{ rank.companyCount }}</td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>

<style scoped>
.hero-title {
  font-size: clamp(2rem, 5vw, 3.5rem);
  background: linear-gradient(135deg, rgba(246, 235, 17), rgb(246, 189, 17));
  border-top: 1px solid rgba(246, 235, 17, 0.6);
  border-bottom: 1px solid rgba(246, 235, 17, 0.6);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  margin-bottom: 1rem;
  font-weight: 800;
  text-transform: uppercase;
  font-family: system-ui, -apple-system, sans-serif;
}

.hero-content {
  position: relative;
}
</style>
