<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import {
  fetchMyRankingSummary,
  fetchRankingLeaderboard,
  type RankingLeaderboardEntryInfo,
  type RankingSummaryInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'
import { calculateRankPage, isActivePlayer } from '@/lib/ranking'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

const auth = useAuthStore()
const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const loading = ref(false)
const errorMessage = ref('')
const summary = ref<RankingSummaryInfo | null>(null)
const leaderboard = ref<RankingLeaderboardEntryInfo[]>([])
const currentPage = ref(1)
const pageSize = ref(10)
const nameFilter = ref('')

const pageSizeOptions = [10, 25, 50]
const navItems = computed(() => {
  const items = [{ label: t('rankingDashboard.historyLink'), to: '/ranking/bounties' }]

  if (auth.isGameAdmin) {
    items.unshift({ label: t('home.rankingAdmin'), to: '/ranking/admin' })
  }

  return items
})

const hasPreviousPage = computed(() => currentPage.value > 1)
const hasNextPage = computed(() => leaderboard.value.length === pageSize.value)
const currentOffset = computed(() => (currentPage.value - 1) * pageSize.value)
const currentPlayerId = computed(() => auth.player?.id ?? null)

function getPlayerAlias(entry: RankingLeaderboardEntryInfo): string {
  return entry.personalAccountName || entry.displayName
}

const filteredLeaderboard = computed(() => {
  const filter = nameFilter.value.trim().toLowerCase()
  if (!filter) {
    return leaderboard.value
  }

  return leaderboard.value.filter((entry) => getPlayerAlias(entry).toLowerCase().includes(filter))
})

const topThree = computed(() => filteredLeaderboard.value.slice(0, 3))

function movementClass(movement: number) {
  if (movement > 0) return 'text-good'
  if (movement < 0) return 'text-bad'
  return 'text-muted'
}

function movementLabel(movement: number) {
  if (movement > 0) return `+${movement}`
  return `${movement}`
}

function formatPoints(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

async function loadData() {
  loading.value = true
  errorMessage.value = ''

  try {
    leaderboard.value = await fetchRankingLeaderboard(pageSize.value, currentOffset.value)
    if (auth.token) {
      summary.value = await fetchMyRankingSummary(auth.token)
    } else {
      summary.value = null
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('rankingDashboard.loadError')
  } finally {
    loading.value = false
  }
}

function parsePageQuery(value: unknown): number {
  const raw = Array.isArray(value) ? value[0] : value
  const parsed = Number.parseInt(String(raw ?? ''), 10)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1
}

async function persistPageQuery() {
  await router.replace({
    query: {
      ...route.query,
      page: currentPage.value > 1 ? String(currentPage.value) : undefined,
    },
  })
}

async function changePage(page: number) {
  if (page < 1 || page === currentPage.value) {
    return
  }

  currentPage.value = page
  await persistPageQuery()
  await loadData()
}

async function handlePageSizeChange() {
  currentPage.value = 1
  await persistPageQuery()
  await loadData()
}

onMounted(async () => {
  currentPage.value = parsePageQuery(route.query.page)
  const hasExplicitPage = route.query.page !== undefined
  if (!hasExplicitPage && auth.token) {
    try {
      summary.value = await fetchMyRankingSummary(auth.token)
      currentPage.value = calculateRankPage(summary.value?.globalRank, pageSize.value)
    } catch {
      currentPage.value = 1
    }
  }
  await persistPageQuery()
  await loadData()
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.ranking')"
      :title="t('rankingDashboard.title')"
      :subtitle="t('rankingDashboard.subtitle')"
      variant="ranking"
    />
    <ViewSubnav :items="navItems" aria-label="Ranking navigation" />

    <section class="container pb-16 pt-2 lg:pb-20 lg:pt-2">
      <p v-if="errorMessage" class="state-error mt-5" role="alert">{{ errorMessage }}</p>

      <section
        v-if="summary"
        class="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4"
        aria-label="Ranking summary cards"
      >
        <article class="card p-5">
          <p class="text-sm text-muted">{{ t('rankingDashboard.totalPoints') }}</p>
          <strong class="mt-2 block text-2xl">{{ formatPoints(summary.totalPoints) }}</strong>
        </article>
        <article class="card p-5">
          <p class="text-sm text-muted">{{ t('rankingDashboard.globalRank') }}</p>
          <strong class="mt-2 block text-2xl">#{{ summary.globalRank || '-' }}</strong>
        </article>
        <article class="card p-5">
          <p class="text-sm text-muted">{{ t('rankingDashboard.movement') }}</p>
          <strong class="mt-2 block text-2xl" :class="movementClass(summary.rankMovement)">
            {{ movementLabel(summary.rankMovement) }}
          </strong>
        </article>
        <article class="card p-5">
          <p class="text-sm text-muted">{{ t('rankingDashboard.updatedAt') }}</p>
          <strong class="mt-2 block text-lg">{{ formatDate(summary.updatedAtUtc) }}</strong>
        </article>
      </section>

      <section class="card mt-5 p-6" aria-label="Top competitors">
        <h2 class="text-2xl font-semibold">{{ t('rankingDashboard.topCompetitors') }}</h2>
        <p v-if="loading" class="state-message mt-3">{{ t('common.loading') }}</p>
        <div v-else class="mt-4 grid gap-4 md:grid-cols-3">
          <article
            v-for="entry in topThree"
            :key="entry.playerId"
            class="rounded-xl border border-divider bg-card-raised p-4"
          >
            <p class="text-sm font-semibold text-brand">#{{ entry.globalRank }}</p>
            <h3 class="mt-1 text-lg font-semibold">{{ getPlayerAlias(entry) }}</h3>
            <p class="mt-2 text-sm text-muted">{{ formatPoints(entry.totalPoints) }} pts</p>
            <p :class="movementClass(entry.rankMovement)">
              {{ t('rankingDashboard.delta') }} {{ movementLabel(entry.rankMovement) }}
            </p>
          </article>
        </div>
      </section>

      <section class="card mt-5 p-6" aria-label="Leaderboard table">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <h2 class="text-2xl font-semibold">{{ t('rankingDashboard.leaderboard') }}</h2>
          <button type="button" class="btn btn-secondary" @click="loadData">
            {{ t('common.refresh') }}
          </button>
        </div>

        <div class="mt-4 flex flex-wrap items-end gap-3">
          <label class="grid gap-1 text-sm text-muted" for="ranking-filter">
            {{ t('rankingDashboard.playerFilter') }}
            <input
              id="ranking-filter"
              v-model="nameFilter"
              type="text"
              class="form-input min-w-[220px]"
              :placeholder="t('rankingDashboard.playerFilterPlaceholder')"
            />
          </label>

          <label class="grid gap-1 text-sm text-muted" for="ranking-page-size">
            {{ t('rankingDashboard.pageSize') }}
            <select
              id="ranking-page-size"
              v-model.number="pageSize"
              class="form-input min-w-[120px]"
              @change="handlePageSizeChange"
            >
              <option v-for="size in pageSizeOptions" :key="size" :value="size">{{ size }}</option>
            </select>
          </label>
        </div>

        <p v-if="loading" class="state-message mt-4">{{ t('common.loading') }}</p>
        <div v-else class="mt-4 overflow-x-auto rounded-xl border border-divider">
          <table
            class="min-w-full border-collapse text-sm"
            aria-label="Master ranking leaderboard table"
          >
            <thead>
              <tr class="bg-overlay/40 text-left text-xs uppercase tracking-[0.08em] text-muted">
                <th class="px-5 py-3">{{ t('rankingDashboard.rank') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.player') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.points') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.movement') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="entry in filteredLeaderboard"
                :key="entry.playerId"
                class="border-t border-divider/70"
                :class="{
                  'bg-blue-50 dark:bg-blue-900/30 font-semibold ring-1 ring-inset ring-blue-400 border-l-4 border-l-blue-500':
                    isActivePlayer(entry.playerId, currentPlayerId),
                }"
                :aria-current="isActivePlayer(entry.playerId, currentPlayerId) ? 'true' : undefined"
                :aria-label="
                  isActivePlayer(entry.playerId, currentPlayerId)
                    ? t('rankingDashboard.activePlayerRowAria')
                    : undefined
                "
              >
                <td class="px-5 py-3 font-semibold">#{{ entry.globalRank }}</td>
                <td class="px-5 py-3">
                  {{ getPlayerAlias(entry) }}
                  <span
                    v-if="isActivePlayer(entry.playerId, currentPlayerId)"
                    class="ml-2 rounded bg-blue-100 px-1.5 py-0.5 text-xs font-semibold text-blue-700"
                  >
                    {{ t('rankingDashboard.youBadge') }}
                  </span>
                </td>
                <td class="px-5 py-3">{{ formatPoints(entry.totalPoints) }}</td>
                <td class="px-5 py-3" :class="movementClass(entry.rankMovement)">
                  {{ movementLabel(entry.rankMovement) }}
                </td>
              </tr>
              <tr v-if="filteredLeaderboard.length === 0">
                <td colspan="4" class="px-5 py-4 text-sm text-muted">
                  {{ t('rankingDashboard.noRows') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="mt-4 flex flex-wrap items-center justify-between gap-3">
          <p class="text-sm text-muted">
            {{ t('rankingDashboard.pageLabel', { page: currentPage }) }}
          </p>
          <div class="flex items-center gap-2">
            <button
              type="button"
              class="btn btn-secondary"
              :disabled="!hasPreviousPage || loading"
              @click="changePage(currentPage - 1)"
            >
              {{ t('rankingDashboard.previousPage') }}
            </button>
            <button
              type="button"
              class="btn btn-secondary"
              :disabled="!hasNextPage || loading"
              @click="changePage(currentPage + 1)"
            >
              {{ t('rankingDashboard.nextPage') }}
            </button>
          </div>
        </div>
      </section>
    </section>
  </main>
</template>
