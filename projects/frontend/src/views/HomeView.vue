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
const formattedGameTime = computed(() => (gameState.value?.currentGameTimeUtc ? formatInGameTime(gameState.value.currentGameTimeUtc, locale.value) : ''))

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
  <div class="pb-20">
    <!-- Hero Section -->
    <section class="relative overflow-hidden border-b border-divider py-24 pb-20">
      <!-- Background video + overlay -->
      <div class="absolute inset-0 -z-10">
        <div class="absolute inset-0 bg-[rgba(13,17,23,0.6)] z-10"></div>
        <video autoplay muted playsinline class="w-full h-full object-cover">
          <source src="../assets/hero-video.webm" type="video/webm" />
        </video>
      </div>

      <div class="container relative z-20 flex flex-col items-center gap-5 text-center">
        <h1 class="hero-title">{{ t('home.heroTitle') }}</h1>
        <p class="max-w-2xl text-lg leading-relaxed text-(--color-hero-subtitle)">
          {{ t('home.heroDescription') }}
        </p>
        <div class="mt-4 flex flex-wrap justify-center gap-4">
          <RouterLink v-if="!auth.isAuthenticated" to="/onboarding" class="btn btn-primary">
            {{ t('home.getStarted') }}
          </RouterLink>
          <RouterLink v-else-if="auth.player && !auth.player.onboardingCompletedAtUtc" to="/onboarding" class="btn btn-primary">
            {{ t('home.startOnboarding') }}
          </RouterLink>
          <RouterLink v-else to="/dashboard" class="btn btn-primary">
            {{ t('home.goToDashboard') }}
          </RouterLink>
        </div>
      </div>
    </section>

    <div class="container flex flex-col gap-12 py-10 lg:py-12">
      <!-- Game Status Cards -->
      <section v-if="gameState" class="space-y-5">
        <div class="grid grid-cols-1 gap-5 sm:grid-cols-3">
          <div class="rounded-xl border border-divider bg-card px-6 py-5 text-center shadow-sm" :title="t('home.currentTick', { tick: gameState.currentTick })">
            <span class="mb-2 block text-xs uppercase tracking-wide text-muted">{{ t('home.currentTime') }}</span>
            <span class="text-2xl font-bold text-body">{{ formattedGameTime }}</span>
          </div>
          <div class="rounded-xl border border-divider bg-card px-6 py-5 text-center shadow-sm">
            <span class="mb-2 block text-xs uppercase tracking-wide text-muted">{{ t('home.taxRate') }}</span>
            <span class="text-2xl font-bold text-body">{{ gameState.taxRate }}%</span>
          </div>
          <div class="rounded-xl border border-divider bg-card px-6 py-5 text-center shadow-sm">
            <span class="mb-2 block text-xs uppercase tracking-wide text-muted">{{ t('home.activePlayers') }}</span>
            <span class="text-2xl font-bold text-body">{{ rankings.length }}</span>
          </div>
        </div>
      </section>

      <!-- Leaderboard -->
      <section class="flex flex-col gap-5 rounded-2xl border border-divider bg-card px-5 py-5 shadow-sm sm:px-6 sm:py-6">
        <div class="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-divider bg-card-raised px-5 py-4">
          <h2 class="text-2xl font-bold text-body">{{ t('home.leaderboard') }}</h2>
          <RouterLink to="/leaderboard" class="btn btn-secondary text-sm whitespace-nowrap">
            {{ t('home.viewFullLeaderboard') }}
          </RouterLink>
        </div>

        <div v-if="loading" class="text-center py-8 text-muted">{{ t('common.loading') }}</div>
        <div v-else-if="rankings.length === 0" class="text-center py-8 text-muted">
          {{ t('home.noPlayers') }}
        </div>
        <div v-else class="overflow-x-auto rounded-xl border border-divider bg-card-raised">
          <table class="w-full border-collapse">
            <thead class="bg-card">
              <tr>
                <th class="border-b border-divider px-6 py-4 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">#</th>
                <th class="border-b border-divider px-6 py-4 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">
                  {{ t('home.playerName') }}
                </th>
                <th class="border-b border-divider px-6 py-4 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">
                  {{ t('home.wealth') }}
                </th>
                <th class="border-b border-divider px-6 py-4 text-left text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">
                  {{ t('home.companies') }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(rank, index) in rankings.slice(0, 5)" :key="rank.playerId" class="hover:bg-overlay transition-colors">
                <td class="border-b border-divider px-6 py-4 font-bold text-brand">
                  {{ index + 1 }}
                </td>
                <td class="border-b border-divider px-6 py-4 text-body">{{ rank.displayName }}</td>
                <!-- wealth class kept for E2E test selector backward compatibility -->
                <td class="wealth border-b border-divider px-6 py-4 font-semibold text-good">
                  {{ formatCompactMoney(rank.totalWealthUsd, 'USD', locale) }}
                </td>
                <td class="border-b border-divider px-6 py-4 text-body">{{ rank.companyCount }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
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
  font-weight: 800;
  text-transform: uppercase;
  font-family:
    system-ui,
    -apple-system,
    sans-serif;
}
</style>
