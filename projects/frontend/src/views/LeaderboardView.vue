<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useGameStateStore } from '@/stores/gameState'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { formatInGameTime } from '@/lib/gameTime'
import { formatCompactMoney } from '@/lib/currencyFormat'
import { calculateRankPage, isActivePlayer } from '@/lib/ranking'
import type { PlayerRanking, CompanyRanking } from '@/types'

const { t, locale } = useI18n()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const gameStateStore = useGameStateStore()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const rankings = ref<PlayerRanking[]>([])
const companyRankings = ref<CompanyRanking[]>([])
const playerLoading = ref(true)
const companyLoading = ref(false)
const playerError = ref<string | null>(null)
const companyError = ref<string | null>(null)
const companyRankingsLoaded = ref(false)
const itemsPerPage = 10
const masterPortalUrl = import.meta.env.VITE_MASTER_WEB_URL || 'http://localhost:5174'
const masterRankingUrl = `${masterPortalUrl}/ranking`

function getInitialTab(): 'players' | 'companies' {
  const queryTab = route.query.tab
  if (queryTab === 'companies') return 'companies'
  return 'players'
}

const activeTab = ref<'players' | 'companies'>(getInitialTab())
const currentPage = ref(1)

const PLAYER_RANKINGS_QUERY = `
  {
    rankings {
      playerId
      displayName
      totalWealth
      totalWealthUsd
      personalCash
      sharesValue
      companyCount
    }
  }
`

const COMPANY_RANKINGS_QUERY = `
  {
    companyRankings {
      companyId
      companyName
      playerId
      ownerDisplayName
      totalWealth
      totalWealthUsd
      currencyCode
      cash
      buildingValue
      inventoryValue
      buildingCount
    }
  }
`

async function fetchPlayerRankings(isRefresh = false) {
  if (!isRefresh) {
    playerLoading.value = true
  }
  playerError.value = null
  try {
    const data = await gqlRequest<{
      rankings: PlayerRanking[]
    }>(PLAYER_RANKINGS_QUERY)
    if (!deepEqual(rankings.value, data.rankings)) {
      rankings.value = data.rankings
    }
  } catch (e) {
    playerError.value = e instanceof Error ? e.message : t('leaderboard.loadFailed')
  } finally {
    playerLoading.value = false
  }
}

async function fetchCompanyRankings(isRefresh = false) {
  if (!isRefresh) {
    companyLoading.value = true
  }
  companyError.value = null
  try {
    const data = await gqlRequest<{
      companyRankings: CompanyRanking[]
    }>(COMPANY_RANKINGS_QUERY)
    if (!deepEqual(companyRankings.value, data.companyRankings)) {
      companyRankings.value = data.companyRankings
    }
    companyRankingsLoaded.value = true
  } catch (e) {
    companyError.value = e instanceof Error ? e.message : t('leaderboard.loadFailed')
  } finally {
    companyLoading.value = false
  }
}

onMounted(async () => {
  auth.initFromStorage()
  if (auth.isAuthenticated) {
    void auth.fetchMe()
  }
  await Promise.allSettled([fetchPlayerRankings(), fetchCompanyRankings()])
  currentPage.value = parsePageQuery(route.query.page)
  if (route.query.page === undefined) {
    setPageToActivePlayer()
  }
  clampCurrentPage()
  await persistRankingQuery()
})

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await Promise.allSettled([fetchPlayerRankings(true), companyRankingsLoaded.value || activeTab.value === 'companies' ? fetchCompanyRankings(true) : Promise.resolve()])
  await restoreScrollPosition(scrollPos)
})

watch(activeTab, (tab: 'players' | 'companies') => {
  if (tab === 'companies' && !companyRankingsLoaded.value && !companyLoading.value) {
    void fetchCompanyRankings()
  }
  if (route.query.page === undefined) {
    setPageToActivePlayer()
  }
  clampCurrentPage()
  void persistRankingQuery()
})

watch(
  () => route.query.page,
  (queryPage) => {
    currentPage.value = parsePageQuery(queryPage)
    clampCurrentPage()
  },
)

function retryActiveTab() {
  if (activeTab.value === 'companies') {
    void fetchCompanyRankings()
    return
  }
  void fetchPlayerRankings()
}

function formatWealth(value: number, currencyCode = 'USD'): string {
  return formatCompactMoney(value, currencyCode, locale.value)
}

function rankBadge(rankNumber: number): string {
  if (rankNumber === 1) return '🥇'
  if (rankNumber === 2) return '🥈'
  if (rankNumber === 3) return '🥉'
  return `${rankNumber}`
}

const currentPlayerId = computed(() => auth.player?.id ?? null)
const currentTick = computed(() => gameStateStore.gameState?.currentTick ?? null)
const currentGameTime = computed(() => {
  const utc = gameStateStore.gameState?.currentGameTimeUtc
  return utc ? formatInGameTime(utc, locale.value) : null
})

function getRankClasses(index: number, ownerId: string | null) {
  return {
    'border-l-4 border-l-[#ffd700]': index === 1,
    'border-l-4 border-l-[#c0c0c0]': index === 2,
    'border-l-4 border-l-[#cd7f32]': index === 3,
    'bg-blue-50/90 dark:bg-blue-900/30 font-semibold ring-1 ring-blue-400 border-l-4 border-l-blue-500':
      isActivePlayer(ownerId, currentPlayerId.value),
  }
}

function getRankGradient(index: number): string | undefined {
  if (index === 1) return 'background: linear-gradient(90deg, rgba(255,215,0,0.06) 0%, var(--color-surface) 40%)'
  if (index === 2) return 'background: linear-gradient(90deg, rgba(192,192,192,0.06) 0%, var(--color-surface) 40%)'
  if (index === 3) return 'background: linear-gradient(90deg, rgba(205,127,50,0.06) 0%, var(--color-surface) 40%)'
  return undefined
}

const hasPreviousPage = computed(() => currentPage.value > 1)
const allRowsForActiveTab = computed(() =>
  activeTab.value === 'companies' ? companyRankings.value : rankings.value,
)
const totalPages = computed(() =>
  Math.max(1, Math.ceil(allRowsForActiveTab.value.length / itemsPerPage)),
)
const pageStartIndex = computed(() => (currentPage.value - 1) * itemsPerPage)
const pagedRankings = computed(() =>
  rankings.value.slice(pageStartIndex.value, pageStartIndex.value + itemsPerPage),
)
const pagedCompanyRankings = computed(() =>
  companyRankings.value.slice(pageStartIndex.value, pageStartIndex.value + itemsPerPage),
)
const hasNextPage = computed(() => currentPage.value < totalPages.value)

function parsePageQuery(value: unknown): number {
  const raw = Array.isArray(value) ? value[0] : value
  const parsed = Number.parseInt(String(raw ?? ''), 10)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1
}

function clampCurrentPage() {
  if (currentPage.value > totalPages.value) {
    currentPage.value = totalPages.value
  }
  if (currentPage.value < 1) {
    currentPage.value = 1
  }
}

function getActivePlayerRank(): number | null {
  const ownerRows =
    activeTab.value === 'companies'
      ? companyRankings.value.map((entry) => entry.playerId)
      : rankings.value.map((entry) => entry.playerId)
  const index = ownerRows.findIndex((id) => isActivePlayer(id, currentPlayerId.value))
  return index >= 0 ? index + 1 : null
}

function setPageToActivePlayer() {
  currentPage.value = calculateRankPage(getActivePlayerRank(), itemsPerPage)
}

async function persistRankingQuery() {
  await router.replace({
    query: {
      ...route.query,
      tab: activeTab.value === 'players' ? undefined : activeTab.value,
      page: currentPage.value > 1 ? String(currentPage.value) : undefined,
    },
  })
}

async function changePage(nextPage: number) {
  if (nextPage < 1 || nextPage > totalPages.value || nextPage === currentPage.value) {
    return
  }
  currentPage.value = nextPage
  await persistRankingQuery()
}
</script>

<template>
  <div class="min-h-screen">
    <!-- Hero -->
    <div
      class="border-b border-divider py-12 text-center"
      style="background: linear-gradient(160deg, #0d1117 0%, rgba(0, 71, 255, 0.14) 100%)"
    >
      <div class="container mx-auto px-4">
        <p class="text-[0.75rem] font-bold tracking-[0.1em] uppercase text-brand mb-2">
          {{ t('leaderboard.eyebrow') }}
        </p>
        <h1
          class="text-4xl sm:text-[2.25rem] font-extrabold mb-3"
          style="background: linear-gradient(135deg, var(--color-primary), var(--color-secondary)); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text"
        >
          {{ t('leaderboard.title') }}
        </h1>
        <p class="text-base text-muted max-w-[540px] mx-auto">{{ t('leaderboard.subtitle') }}</p>
        <div class="flex justify-center mt-4 gap-3 flex-wrap">
          <span
            class="leaderboard-tick-chip inline-flex items-center gap-1.5 bg-white/[0.07] border border-white/[0.12] rounded-full px-3 py-1 text-[0.78rem] text-muted cursor-default select-none"
            :title="currentTick !== null ? t('leaderboard.tickHint') + ' #' + currentTick : t('leaderboard.tickHint')"
          >
            <span class="font-semibold text-brand uppercase tracking-[0.04em] text-[0.72rem]">
              {{ t('leaderboard.tick') }}
            </span>
            <span class="leaderboard-tick-value tabular-nums font-bold text-body">
              {{ currentGameTime !== null ? currentGameTime : '—' }}
            </span>
          </span>
        </div>
      </div>
    </div>

    <!-- Content -->
    <div class="container mx-auto px-4 pt-10 pb-16">
      <!-- Tab switcher -->
      <div class="flex gap-2 max-w-[800px] mx-auto mb-6" role="tablist">
        <button
          role="tab"
          :aria-selected="activeTab === 'players'"
          class="flex-1 py-3 px-4 border border-divider rounded-xl bg-card font-semibold text-muted cursor-pointer transition-colors hover:border-brand hover:text-body"
          :class="{ 'bg-brand !text-white border-brand': activeTab === 'players' }"
          @click="activeTab = 'players'"
        >
          👤 {{ t('leaderboard.tabPlayers') }}
        </button>
        <button
          role="tab"
          :aria-selected="activeTab === 'companies'"
          class="flex-1 py-3 px-4 border border-divider rounded-xl bg-card font-semibold text-muted cursor-pointer transition-colors hover:border-brand hover:text-body"
          :class="{ 'bg-brand !text-white border-brand': activeTab === 'companies' }"
          @click="activeTab = 'companies'"
        >
          🏢 {{ t('leaderboard.tabCompanies') }}
        </button>
      </div>

      <div class="max-w-[800px] mx-auto mb-6 flex justify-end">
        <a
          class="btn btn-secondary"
          :href="masterRankingUrl"
          target="_blank"
          rel="noopener noreferrer"
          aria-label="Open global master ranking in a new tab"
        >
          🏆 {{ t('leaderboard.viewMasterRanking') }}
        </a>
      </div>

      <!-- Player rankings tab -->
      <template v-if="activeTab === 'players'">
        <div v-if="playerLoading" class="flex flex-col items-center gap-3 py-12 text-center">
          <span class="text-4xl">⏳</span>
          <p>{{ t('common.loading') }}</p>
        </div>

        <div v-else-if="playerError" class="flex flex-col items-center gap-3 py-12 text-center text-bad">
          <span class="text-4xl">⚠️</span>
          <p>{{ playerError }}</p>
          <button class="btn btn-secondary" aria-label="Retry loading leaderboard" @click="retryActiveTab">
            {{ t('common.tryAgain') }}
          </button>
        </div>

        <div v-else-if="rankings.length === 0" class="flex flex-col items-center gap-3 py-12 text-center">
          <span class="text-4xl">🏆</span>
          <p class="text-xl font-bold">{{ t('leaderboard.emptyTitle') }}</p>
          <p class="text-muted max-w-[400px]">{{ t('leaderboard.emptyDesc') }}</p>
          <RouterLink to="/onboarding" class="btn btn-primary">{{ t('leaderboard.startEmpire') }}</RouterLink>
        </div>

        <div v-else class="flex flex-col gap-3 max-w-[800px] mx-auto mb-12">
          <div
            v-for="(rank, index) in pagedRankings"
            :key="rank.playerId"
            class="rank-card flex items-center flex-wrap sm:flex-nowrap gap-4 bg-card border border-divider rounded-xl p-4 md:p-5 hover:border-brand transition-colors"
            :class="getRankClasses(pageStartIndex + index + 1, rank.playerId)"
            :style="getRankGradient(pageStartIndex + index + 1)"
            :aria-current="isActivePlayer(rank.playerId, currentPlayerId) ? 'true' : undefined"
            :aria-label="isActivePlayer(rank.playerId, currentPlayerId) ? t('leaderboard.activePlayerRowAria') : undefined"
          >
            <div class="text-2xl font-extrabold min-w-[2.5rem] text-center text-brand leading-none">
              {{ rankBadge(pageStartIndex + index + 1) }}
            </div>
            <div class="flex-1 min-w-0">
              <div class="text-base font-bold flex items-center gap-2 whitespace-nowrap overflow-hidden text-ellipsis">
                {{ rank.displayName }}
                <span
                  v-if="isActivePlayer(rank.playerId, currentPlayerId)"
                  class="you-badge text-[0.6875rem] font-bold bg-[color:var(--color-secondary)] text-black px-[0.4rem] py-[0.1rem] rounded-full tracking-[0.04em] uppercase shrink-0"
                >{{ t('leaderboard.you') }}</span>
              </div>
              <div class="text-[0.8125rem] text-muted mt-0.5">
                {{ t('leaderboard.companiesCount', { n: rank.companyCount }) }}
              </div>
            </div>
            <div class="w-full sm:w-auto text-left sm:text-right pl-[calc(2.5rem+1rem)] sm:pl-0">
              <div class="total-wealth text-xl font-extrabold text-[color:var(--color-secondary)]">
                {{ formatWealth(rank.totalWealthUsd) }}
              </div>
              <div class="text-xs text-muted mt-1 flex gap-1 flex-wrap justify-start sm:justify-end">
                <span :title="t('leaderboard.cashTooltip')"> 💵 {{ formatWealth(rank.personalCash) }} </span>
                <span class="opacity-40">·</span>
                <span :title="t('leaderboard.stocksTooltip')"> 📈 {{ formatWealth(rank.sharesValue) }} </span>
              </div>
              <RouterLink
                :to="`/player/${rank.playerId}`"
                class="view-profile-link mt-1 inline-block text-xs text-brand hover:underline"
              >
                {{ t('leaderboard.viewProfile') }} →
              </RouterLink>
            </div>
          </div>
        </div>
      </template>

      <!-- Company rankings tab -->
      <template v-else-if="activeTab === 'companies'">
        <div v-if="companyLoading" class="flex flex-col items-center gap-3 py-12 text-center">
          <span class="text-4xl">⏳</span>
          <p>{{ t('common.loading') }}</p>
        </div>

        <div v-else-if="companyError" class="flex flex-col items-center gap-3 py-12 text-center text-bad">
          <span class="text-4xl">⚠️</span>
          <p>{{ companyError }}</p>
          <button class="btn btn-secondary" aria-label="Retry loading leaderboard" @click="retryActiveTab">
            {{ t('common.tryAgain') }}
          </button>
        </div>

        <div v-else-if="companyRankings.length === 0" class="flex flex-col items-center gap-3 py-12 text-center">
          <span class="text-4xl">🏢</span>
          <p class="text-xl font-bold">{{ t('leaderboard.emptyCompanyTitle') }}</p>
          <p class="text-muted max-w-[400px]">{{ t('leaderboard.emptyCompanyDesc') }}</p>
          <RouterLink to="/onboarding" class="btn btn-primary">{{ t('leaderboard.startEmpire') }}</RouterLink>
        </div>

        <div v-else class="flex flex-col gap-3 max-w-[800px] mx-auto mb-12">
          <div
            v-for="(rank, index) in pagedCompanyRankings"
            :key="rank.companyId"
            class="rank-card flex items-center flex-wrap sm:flex-nowrap gap-4 bg-card border border-divider rounded-xl p-4 md:p-5 hover:border-brand transition-colors"
            :class="getRankClasses(pageStartIndex + index + 1, rank.playerId)"
            :style="getRankGradient(pageStartIndex + index + 1)"
            :aria-current="isActivePlayer(rank.playerId, currentPlayerId) ? 'true' : undefined"
            :aria-label="isActivePlayer(rank.playerId, currentPlayerId) ? t('leaderboard.activePlayerRowAria') : undefined"
          >
            <div class="text-2xl font-extrabold min-w-[2.5rem] text-center text-brand leading-none">
              {{ rankBadge(pageStartIndex + index + 1) }}
            </div>
            <div class="flex-1 min-w-0">
              <div class="text-base font-bold flex items-center gap-2 whitespace-nowrap overflow-hidden text-ellipsis">
                {{ rank.companyName }}
                <span
                  v-if="isActivePlayer(rank.playerId, currentPlayerId)"
                  class="you-badge text-[0.6875rem] font-bold bg-[color:var(--color-secondary)] text-black px-[0.4rem] py-[0.1rem] rounded-full tracking-[0.04em] uppercase shrink-0"
                >{{ t('leaderboard.you') }}</span>
              </div>
              <div class="text-[0.8125rem] text-muted mt-0.5">
                {{ t('leaderboard.ownedBy', { name: rank.ownerDisplayName }) }} ·
                {{ t('leaderboard.buildingsCount', { n: rank.buildingCount }) }}
              </div>
            </div>
            <div class="w-full sm:w-auto text-left sm:text-right pl-[calc(2.5rem+1rem)] sm:pl-0">
              <div class="total-wealth text-xl font-extrabold text-[color:var(--color-secondary)]">
                {{ formatWealth(rank.totalWealthUsd) }}
              </div>
              <div class="text-xs text-muted mt-1 flex gap-1 flex-wrap justify-start sm:justify-end">
                <span :title="t('leaderboard.cashTooltip')"> 💵 {{ formatWealth(rank.cash, rank.currencyCode) }} </span>
                <span class="opacity-40">·</span>
                <span :title="t('leaderboard.buildingsTooltip')"> 🏗️ {{ formatWealth(rank.buildingValue, rank.currencyCode) }} </span>
                <span class="opacity-40">·</span>
                <span :title="t('leaderboard.inventoryTooltip')"> 📦 {{ formatWealth(rank.inventoryValue, rank.currencyCode) }} </span>
              </div>
              <RouterLink
                :to="`/player/${rank.playerId}`"
                class="view-profile-link mt-1 inline-block text-xs text-brand hover:underline"
              >
                {{ t('leaderboard.viewProfile') }} →
              </RouterLink>
            </div>
          </div>
        </div>
      </template>

      <div
        v-if="!playerLoading && !companyLoading && !playerError && !companyError && allRowsForActiveTab.length > 0"
        class="max-w-[800px] mx-auto mb-8 flex flex-wrap items-center justify-between gap-3"
      >
        <p class="text-sm text-muted">{{ t('leaderboard.pageLabel', { page: currentPage, pages: totalPages }) }}</p>
        <div class="flex items-center gap-2">
          <button
            type="button"
            class="btn btn-secondary"
            :disabled="!hasPreviousPage"
            @click="changePage(currentPage - 1)"
          >
            {{ t('leaderboard.previousPage') }}
          </button>
          <button
            type="button"
            class="btn btn-secondary"
            :disabled="!hasNextPage"
            @click="changePage(currentPage + 1)"
          >
            {{ t('leaderboard.nextPage') }}
          </button>
        </div>
      </div>

      <!-- How it works -->
      <div class="max-w-[800px] mx-auto bg-card border border-divider rounded-xl p-6">
        <h3 class="text-base font-bold mb-2">{{ t('leaderboard.howItWorksTitle') }}</h3>
        <p class="text-[0.9rem] text-muted mb-3">
          {{ activeTab === 'players' ? t('leaderboard.playerHowItWorksBody') : t('leaderboard.companyHowItWorksBody') }}
        </p>
        <ul v-if="activeTab === 'players'" class="formula-list list-none p-0 flex flex-col gap-1.5 text-sm text-muted">
          <li>
            💵 <strong class="text-body">{{ t('leaderboard.cashLabel') }}</strong> — {{ t('leaderboard.personalCashExplain') }}
          </li>
          <li>
            📈 <strong class="text-body">{{ t('leaderboard.stocksLabel') }}</strong> — {{ t('leaderboard.stocksExplain') }}
          </li>
        </ul>
        <ul v-else class="formula-list list-none p-0 flex flex-col gap-1.5 text-sm text-muted">
          <li>
            💵 <strong class="text-body">{{ t('leaderboard.cashLabel') }}</strong> — {{ t('leaderboard.cashExplain') }}
          </li>
          <li>
            🏗️ <strong class="text-body">{{ t('leaderboard.buildingsLabel') }}</strong> — {{ t('leaderboard.buildingsExplain') }}
          </li>
          <li>
            📦 <strong class="text-body">{{ t('leaderboard.inventoryLabel') }}</strong> — {{ t('leaderboard.inventoryExplain') }}
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>
