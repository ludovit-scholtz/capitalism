<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
  fetchMyRankingSummary,
  fetchRankingLeaderboard,
  type RankingLeaderboardEntryInfo,
  type RankingSummaryInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const summary = ref<RankingSummaryInfo | null>(null)
const leaderboard = ref<RankingLeaderboardEntryInfo[]>([])

const topThree = computed(() => leaderboard.value.slice(0, 3))

function movementClass(movement: number) {
  if (movement > 0) return 'movement-up'
  if (movement < 0) return 'movement-down'
  return 'movement-flat'
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
    leaderboard.value = await fetchRankingLeaderboard(100, 0)
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

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  await loadData()
})
</script>

<template>
  <main class="ranking-shell">
    <header class="ranking-header">
      <h1>{{ t('rankingDashboard.title') }}</h1>
      <p>{{ t('rankingDashboard.subtitle') }}</p>
      <div class="ranking-nav-links">
        <a href="/ranking/bounties" class="nav-link">{{ t('rankingDashboard.historyLink') }}</a>
        <a href="/" class="nav-link">← {{ t('common.backToPortal') }}</a>
      </div>
    </header>

    <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>

    <section v-if="summary" class="summary-grid" aria-label="Ranking summary cards">
      <article class="summary-card">
        <p>{{ t('rankingDashboard.totalPoints') }}</p>
        <strong>{{ formatPoints(summary.totalPoints) }}</strong>
      </article>
      <article class="summary-card">
        <p>{{ t('rankingDashboard.globalRank') }}</p>
        <strong>#{{ summary.globalRank || '-' }}</strong>
      </article>
      <article class="summary-card">
        <p>{{ t('rankingDashboard.movement') }}</p>
        <strong :class="movementClass(summary.rankMovement)">
          {{ movementLabel(summary.rankMovement) }}
        </strong>
      </article>
      <article class="summary-card">
        <p>{{ t('rankingDashboard.updatedAt') }}</p>
        <strong>{{ formatDate(summary.updatedAtUtc) }}</strong>
      </article>
    </section>

    <section class="panel" aria-label="Top competitors">
      <h2>{{ t('rankingDashboard.topCompetitors') }}</h2>
      <p v-if="loading" class="state-message">{{ t('common.loading') }}</p>
      <div v-else class="podium-grid">
        <article v-for="entry in topThree" :key="entry.playerId" class="podium-card">
          <p class="podium-rank">#{{ entry.globalRank }}</p>
          <h3>{{ entry.displayName }}</h3>
          <p>{{ formatPoints(entry.totalPoints) }} pts</p>
          <p :class="movementClass(entry.rankMovement)">
            {{ t('rankingDashboard.delta') }} {{ movementLabel(entry.rankMovement) }}
          </p>
        </article>
      </div>
    </section>

    <section class="panel" aria-label="Leaderboard table">
      <div class="panel-row">
        <h2>{{ t('rankingDashboard.leaderboard') }}</h2>
        <button type="button" @click="loadData">{{ t('common.refresh') }}</button>
      </div>

      <p v-if="loading" class="state-message">{{ t('common.loading') }}</p>
      <table v-else class="leaderboard-table" aria-label="Master ranking leaderboard table">
        <thead>
          <tr>
            <th>{{ t('rankingDashboard.rank') }}</th>
            <th>{{ t('rankingDashboard.player') }}</th>
            <th>{{ t('rankingDashboard.points') }}</th>
            <th>{{ t('rankingDashboard.movement') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="entry in leaderboard" :key="entry.playerId">
            <td>#{{ entry.globalRank }}</td>
            <td>{{ entry.displayName }}</td>
            <td>{{ formatPoints(entry.totalPoints) }}</td>
            <td :class="movementClass(entry.rankMovement)">
              {{ movementLabel(entry.rankMovement) }}
            </td>
          </tr>
        </tbody>
      </table>
    </section>
  </main>
</template>

<style scoped>
.ranking-shell {
  max-width: 1200px;
  margin: 2rem auto;
  padding: 0 1rem 2.5rem;
  color: #efeef6;
}

.ranking-header {
  margin-bottom: 1.5rem;
}

.ranking-header h1 {
  margin: 0;
  font-size: 2rem;
}

.ranking-nav-links {
  display: flex;
  gap: 0.8rem;
  margin-top: 0.75rem;
}

.nav-link {
  color: #ffd479;
  text-decoration: none;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 0.8rem;
  margin-bottom: 1rem;
}

.summary-card,
.panel,
.podium-card {
  background: rgba(10, 11, 21, 0.88);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 14px;
}

.summary-card {
  padding: 0.9rem;
}

.summary-card strong {
  font-size: 1.2rem;
}

.panel {
  padding: 1rem;
  margin-bottom: 1rem;
}

.panel-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.podium-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.8rem;
}

.podium-card {
  padding: 0.75rem;
}

.podium-rank {
  margin: 0;
  color: #ffd479;
  font-weight: 700;
}

.leaderboard-table {
  width: 100%;
  border-collapse: collapse;
}

.leaderboard-table th,
.leaderboard-table td {
  padding: 0.55rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.12);
  text-align: left;
}

.movement-up {
  color: #67efac;
}

.movement-down {
  color: #ff8f8f;
}

.movement-flat {
  color: #b7b7cb;
}

.state-error {
  color: #ff9e9e;
}
</style>
