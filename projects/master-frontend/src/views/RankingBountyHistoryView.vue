<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
  fetchMyRankingBountyHistory,
  submitRankingProofEvent,
  type RankingRewardHistoryItem,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const items = ref<RankingRewardHistoryItem[]>([])
const filterBountyCode = ref('')
const filterServerKey = ref('')
const filterStatus = ref('')

const proofBountyCode = ref('RETWEET_X_POST')
const proofReference = ref('')
const proofUniqueScopeKey = ref('')
const proofLoading = ref(false)

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function formatPoints(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}

async function loadHistory() {
  if (!auth.token) return

  loading.value = true
  errorMessage.value = ''
  try {
    items.value = await fetchMyRankingBountyHistory(auth.token, {
      bountyCode: filterBountyCode.value || null,
      serverKey: filterServerKey.value || null,
      status: filterStatus.value || null,
      limit: 200,
      offset: 0,
    })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('rankingHistory.loadError')
  } finally {
    loading.value = false
  }
}

async function submitProof() {
  if (!auth.token) return

  if (!proofReference.value.trim()) {
    errorMessage.value = t('rankingHistory.proofRequired')
    return
  }

  proofLoading.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await submitRankingProofEvent(
      auth.token,
      proofBountyCode.value,
      proofReference.value.trim(),
      proofUniqueScopeKey.value.trim() || undefined,
    )
    successMessage.value = t('rankingHistory.proofSubmitted')
    proofReference.value = ''
    proofUniqueScopeKey.value = ''
    await loadHistory()
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t('rankingHistory.proofSubmitError')
  } finally {
    proofLoading.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  await loadHistory()
})
</script>

<template>
  <main class="ranking-history-shell">
    <header class="history-header">
      <h1>{{ t('rankingHistory.title') }}</h1>
      <p>{{ t('rankingHistory.subtitle') }}</p>
      <div class="nav-links">
        <a href="/ranking" class="nav-link">{{ t('rankingHistory.dashboardLink') }}</a>
        <a href="/" class="nav-link">← {{ t('common.backToPortal') }}</a>
      </div>
    </header>

    <section class="panel" aria-label="Proof submission panel">
      <h2>{{ t('rankingHistory.submitProofTitle') }}</h2>
      <p>{{ t('rankingHistory.submitProofHint') }}</p>
      <div class="proof-grid">
        <label>
          {{ t('rankingHistory.bountyCode') }}
          <select v-model="proofBountyCode">
            <option value="RETWEET_X_POST">RETWEET_X_POST</option>
            <option value="DISCORD_PLAYER">DISCORD_PLAYER</option>
          </select>
        </label>
        <label>
          {{ t('rankingHistory.proofReference') }}
          <input v-model="proofReference" type="text" />
        </label>
        <label>
          {{ t('rankingHistory.uniqueScopeKey') }}
          <input v-model="proofUniqueScopeKey" type="text" />
        </label>
      </div>
      <button type="button" :disabled="proofLoading" @click="submitProof">
        {{ proofLoading ? t('rankingHistory.submittingProof') : t('rankingHistory.submitProof') }}
      </button>
    </section>

    <section class="panel" aria-label="Bounty history filters">
      <div class="filters">
        <input
          v-model="filterBountyCode"
          type="text"
          :placeholder="t('rankingHistory.filterBountyCode')"
        />
        <input
          v-model="filterServerKey"
          type="text"
          :placeholder="t('rankingHistory.filterServerKey')"
        />
        <select v-model="filterStatus">
          <option value="">{{ t('common.allStatuses') }}</option>
          <option value="AWARDED">AWARDED</option>
          <option value="REJECTED">REJECTED</option>
        </select>
        <button type="button" @click="loadHistory">{{ t('common.apply') }}</button>
      </div>
      <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
    </section>

    <section class="panel" aria-label="Bounty history table">
      <h2>{{ t('rankingHistory.historyTableTitle') }}</h2>
      <p v-if="loading">{{ t('common.loading') }}</p>
      <table v-else class="history-table" aria-label="Ranking bounty history table">
        <thead>
          <tr>
            <th>{{ t('rankingHistory.awardedAt') }}</th>
            <th>{{ t('rankingHistory.bounty') }}</th>
            <th>{{ t('rankingHistory.points') }}</th>
            <th>{{ t('rankingHistory.status') }}</th>
            <th>{{ t('rankingHistory.server') }}</th>
            <th>{{ t('rankingHistory.eventDate') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in items" :key="item.id">
            <td>{{ formatDate(item.awardedAtUtc) }}</td>
            <td>{{ item.bountyDisplayName }}</td>
            <td>{{ formatPoints(item.pointsAwarded) }}</td>
            <td>{{ item.status }}</td>
            <td>{{ item.serverKey ?? '-' }}</td>
            <td>{{ formatDate(item.eventDateUtc) }}</td>
          </tr>
        </tbody>
      </table>
    </section>
  </main>
</template>

<style scoped>
.ranking-history-shell {
  max-width: 1200px;
  margin: 2rem auto;
  padding: 0 1rem 2.5rem;
  color: #efeef6;
}

.history-header {
  margin-bottom: 1rem;
}

.nav-links {
  display: flex;
  gap: 0.75rem;
}

.nav-link {
  color: #ffd479;
  text-decoration: none;
}

.panel {
  background: rgba(10, 11, 21, 0.88);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 14px;
  padding: 1rem;
  margin-bottom: 1rem;
}

.filters,
.proof-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.65rem;
  margin-bottom: 0.6rem;
}

.history-table {
  width: 100%;
  border-collapse: collapse;
}

.history-table th,
.history-table td {
  padding: 0.55rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.12);
  text-align: left;
}

.state-error {
  color: #ff9e9e;
}

.state-success {
  color: #8bffb5;
}
</style>
