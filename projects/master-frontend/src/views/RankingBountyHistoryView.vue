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
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

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
const navItems = ref<Array<{ label: string; to: string }>>([])

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

  navItems.value = [
    { label: t('rankingBounties.title'), to: '/ranking/bounties' },
    { label: t('rankingHistory.title'), to: '/ranking/bounties/history' },
  ]

  await loadHistory()
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.bounties')"
      :title="t('rankingHistory.title')"
      :subtitle="t('rankingHistory.subtitle')"
      variant="gold"
    />
    <ViewSubnav :items="navItems" aria-label="Ranking history navigation" />

    <section class="container pb-16 pt-4 lg:pb-20 lg:pt-6">
      <section class="card p-6" aria-label="Proof submission panel">
        <h2>{{ t('rankingHistory.submitProofTitle') }}</h2>
        <p>{{ t('rankingHistory.submitProofHint') }}</p>
        <div class="proof-grid mt-4">
          <label>
            {{ t('rankingHistory.bountyCode') }}
            <select v-model="proofBountyCode" class="form-input mt-1">
              <option value="RETWEET_X_POST">RETWEET_X_POST</option>
              <option value="DISCORD_PLAYER">DISCORD_PLAYER</option>
            </select>
          </label>
          <label>
            {{ t('rankingHistory.proofReference') }}
            <input v-model="proofReference" type="text" class="form-input mt-1" />
          </label>
          <label>
            {{ t('rankingHistory.uniqueScopeKey') }}
            <input v-model="proofUniqueScopeKey" type="text" class="form-input mt-1" />
          </label>
        </div>
        <button
          type="button"
          class="btn btn-primary mt-4"
          :disabled="proofLoading"
          @click="submitProof"
        >
          {{ proofLoading ? t('rankingHistory.submittingProof') : t('rankingHistory.submitProof') }}
        </button>
      </section>

      <section class="card mt-5 p-6" aria-label="Bounty history filters">
        <div class="filters">
          <input
            v-model="filterBountyCode"
            type="text"
            :placeholder="t('rankingHistory.filterBountyCode')"
            class="form-input"
          />
          <input
            v-model="filterServerKey"
            type="text"
            :placeholder="t('rankingHistory.filterServerKey')"
            class="form-input"
          />
          <select v-model="filterStatus" class="form-input">
            <option value="">{{ t('common.allStatuses') }}</option>
            <option value="AWARDED">AWARDED</option>
            <option value="REJECTED">REJECTED</option>
          </select>
          <button type="button" class="btn btn-secondary" @click="loadHistory">
            {{ t('common.apply') }}
          </button>
        </div>
        <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
      </section>

      <section class="card mt-5 p-6" aria-label="Bounty history table">
        <h2>{{ t('rankingHistory.historyTableTitle') }}</h2>
        <p v-if="loading">{{ t('common.loading') }}</p>
        <p v-else-if="items.length === 0" class="state-message">{{ t('common.noData') }}</p>
        <div v-else class="overflow-auto">
          <table
            class="min-w-full border-collapse text-sm"
            aria-label="Ranking bounty history table"
          >
            <thead>
              <tr class="border-b border-divider text-left text-muted">
                <th class="px-4 py-3">{{ t('rankingHistory.awardedAt') }}</th>
                <th class="px-4 py-3">{{ t('rankingHistory.bounty') }}</th>
                <th class="px-4 py-3">{{ t('rankingHistory.points') }}</th>
                <th class="px-4 py-3">{{ t('rankingHistory.status') }}</th>
                <th class="px-4 py-3">{{ t('rankingHistory.server') }}</th>
                <th class="px-4 py-3">{{ t('rankingHistory.eventDate') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in items" :key="item.id" class="border-b border-divider/70">
                <td class="px-4 py-3">{{ formatDate(item.awardedAtUtc) }}</td>
                <td class="px-4 py-3">{{ item.bountyDisplayName }}</td>
                <td class="px-4 py-3">{{ formatPoints(item.pointsAwarded) }}</td>
                <td class="px-4 py-3">{{ item.status }}</td>
                <td class="px-4 py-3">{{ item.serverKey ?? '-' }}</td>
                <td class="px-4 py-3">{{ formatDate(item.eventDateUtc) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </section>
  </main>
</template>

<style scoped>
.filters,
.proof-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.65rem;
  margin-bottom: 0.6rem;
}

.state-error {
  color: var(--color-danger);
}

.state-success {
  color: var(--color-success);
}
</style>
