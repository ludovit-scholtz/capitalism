<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useGameStateStore } from '@/stores/gameState'
import { deepEqual } from '@/lib/utils'
import { formatCompactMoney } from '@/lib/currencyFormat'
import type { PlayerRanking, GameState } from '@/types'

const { t, locale } = useI18n()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const { gameState } = storeToRefs(gameStateStore)

const rankings = ref<PlayerRanking[]>([])
const loading = ref(true)

function getPlayerAlias(rank: PlayerRanking): string {
  return rank.personalAccountName || rank.displayName
}

async function loadHomeData(isRefresh = false) {
  if (!isRefresh) {
    loading.value = true
  }
  try {
    const [rankData, stateData] = await Promise.all([
      gqlRequest<{ rankings: PlayerRanking[] }>('{ rankings { playerId displayName personalAccountName totalWealth totalWealthUsd personalCash sharesValue companyCount } }'),
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
  <div class="flex flex-col mb-8">
    <!-- Hero Section -->
    <section class="relative overflow-hidden border border-divider shadow-lg">
      <!-- Background video + overlay -->
      <div class="absolute inset-0 -z-10">
        <div class="absolute inset-0 bg-[rgba(13,17,23,0.6)] z-10"></div>
        <video autoplay muted playsinline class="w-full h-full object-cover">
          <source src="../assets/hero-video.webm" type="video/webm" />
        </video>
      </div>

      <div class="relative z-20 flex min-h-[23rem] flex-col items-center justify-center gap-6 px-6 py-14 text-center sm:px-10 lg:min-h-[27rem] lg:px-16 lg:py-20">
        <h1 class="hero-title">{{ t('home.heroTitle') }}</h1>
        <p class="max-w-3xl text-lg leading-relaxed text-(--color-hero-subtitle) lg:text-[1.45rem] lg:leading-8">
          {{ t('home.heroDescription') }}
        </p>
        <div class="mt-2 flex flex-wrap justify-center gap-4">
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
  </div>
  <div class="container flex flex-col gap-12 pb-20 pt-6 lg:gap-14 lg:pb-24 lg:pt-8">
    <!-- Game Status Cards -->

    <!-- Leaderboard -->
    <section class="overflow-hidden rounded-2xl border border-divider bg-card shadow-sm">
      <div class="flex flex-wrap items-center justify-between gap-4 px-6 py-5 sm:px-8 sm:py-6">
        <h2 class="text-3xl font-bold text-body">{{ t('home.leaderboard') }}</h2>
        <RouterLink to="/leaderboard" class="btn btn-secondary text-sm whitespace-nowrap">
          {{ t('home.viewFullLeaderboard') }}
        </RouterLink>
      </div>

      <div v-if="loading" class="border-t border-divider px-6 py-10 text-center text-muted sm:px-8">
        {{ t('common.loading') }}
      </div>
      <div v-else-if="rankings.length === 0" class="border-t border-divider px-6 py-10 text-center text-muted sm:px-8">
        {{ t('home.noPlayers') }}
      </div>
      <div v-else class="overflow-x-auto border-t border-divider">
        <table class="w-full min-w-[38rem] border-collapse">
          <thead class="bg-card-raised">
            <tr>
              <th class="w-20 border-b border-divider px-8 py-5 text-left text-xs font-semibold uppercase tracking-[0.16em] text-muted">#</th>
              <th class="border-b border-divider px-8 py-5 text-left text-xs font-semibold uppercase tracking-[0.16em] text-muted">
                {{ t('home.playerName') }}
              </th>
              <th class="border-b border-divider px-8 py-5 text-left text-xs font-semibold uppercase tracking-[0.16em] text-muted">
                {{ t('home.wealth') }}
              </th>
              <th class="border-b border-divider px-8 py-5 text-left text-xs font-semibold uppercase tracking-[0.16em] text-muted">
                {{ t('home.companies') }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(rank, index) in rankings.slice(0, 5)" :key="rank.playerId" class="hover:bg-overlay transition-colors">
              <td class="border-b border-divider px-8 py-5 align-middle font-bold text-brand">
                {{ index + 1 }}
              </td>
              <td class="border-b border-divider px-8 py-5 align-middle text-body">{{ getPlayerAlias(rank) }}</td>
              <!-- wealth class kept for E2E test selector backward compatibility -->
              <td class="wealth border-b border-divider px-8 py-5 align-middle font-semibold text-good">
                {{ formatCompactMoney(rank.totalWealthUsd, 'USD', locale) }}
              </td>
              <td class="border-b border-divider px-8 py-5 align-middle text-body">{{ rank.companyCount }}</td>
            </tr>
          </tbody>
        </table>
      </div>
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
  font-weight: 800;
  text-transform: uppercase;
  font-family:
    system-ui,
    -apple-system,
    sans-serif;
}
</style>
