<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { fetchMyRankingBountyDashboard, type RankingBountyDashboardItemInfo } from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const bounties = ref<RankingBountyDashboardItemInfo[]>([])

const navItems = computed(() => [
  { label: t('rankingBounties.title'), to: '/ranking/bounties' },
  { label: t('rankingHistory.title'), to: '/ranking/bounties/history' },
])

const sortedBounties = computed(() => {
  return [...bounties.value].sort((left, right) => {
    if (left.isAvailableNow !== right.isAvailableNow) {
      return left.isAvailableNow ? -1 : 1
    }

    return right.rewardPoints - left.rewardPoints
  })
})

function formatDate(value: string | null): string {
  if (!value) {
    return '-'
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(new Date(value))
}

function formatPoints(value: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}

function getAvailabilityLabel(item: RankingBountyDashboardItemInfo): string {
  if (item.isAvailableNow) {
    return t('rankingBounties.availableNow')
  }

  if (item.nextAvailableAtUtc) {
    return t('rankingBounties.availableAgainAt', { date: formatDate(item.nextAvailableAtUtc) })
  }

  return t('rankingBounties.currentlyUnavailable')
}

async function loadDashboard() {
  if (!auth.token) {
    return
  }

  loading.value = true
  errorMessage.value = ''

  try {
    bounties.value = await fetchMyRankingBountyDashboard(auth.token)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('rankingBounties.loadError')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  await loadDashboard()
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.bounties')"
      :title="t('rankingBounties.title')"
      :subtitle="t('rankingBounties.subtitle')"
      variant="gold"
    />
    <ViewSubnav :items="navItems" aria-label="Ranking bounties navigation" />

    <section class="container pb-16 pt-2 lg:pb-20 lg:pt-2">
      <div class="card mt-5 p-6" aria-label="Bounty dashboard table panel">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <h2 class="text-2xl font-semibold">{{ t('rankingBounties.tableTitle') }}</h2>
          <button
            type="button"
            class="btn btn-secondary"
            :disabled="loading"
            @click="loadDashboard"
          >
            {{ t('common.refresh') }}
          </button>
        </div>

        <p v-if="errorMessage" class="state-error mt-4" role="alert">{{ errorMessage }}</p>
        <p v-else-if="loading" class="state-message mt-4">{{ t('common.loading') }}</p>

        <div v-else class="mt-4 overflow-x-auto rounded-xl border border-divider">
          <table
            class="min-w-full border-collapse text-sm"
            aria-label="Ranking bounties dashboard table"
          >
            <thead>
              <tr class="bg-overlay/40 text-left text-xs uppercase tracking-[0.08em] text-muted">
                <th class="px-5 py-3">{{ t('rankingBounties.bounty') }}</th>
                <th class="px-5 py-3">{{ t('rankingBounties.points') }}</th>
                <th class="px-5 py-3">{{ t('rankingBounties.status') }}</th>
                <th class="px-5 py-3">{{ t('rankingBounties.lastGrantedAt') }}</th>
                <th class="px-5 py-3">{{ t('rankingBounties.nextAvailableAt') }}</th>
                <th class="px-5 py-3">{{ t('rankingBounties.totalAwards') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="item in sortedBounties"
                :key="item.id"
                class="border-t border-divider/70"
                :class="item.isAvailableNow ? 'bg-success/5' : ''"
              >
                <td class="px-5 py-3 align-top">
                  <div class="font-semibold">{{ item.displayName }}</div>
                  <p class="mt-1 text-xs text-muted">{{ item.description }}</p>
                  <div
                    class="mt-2 flex flex-wrap gap-2 text-[11px] uppercase tracking-[0.08em] text-muted"
                  >
                    <span class="rounded-full border border-divider px-2 py-1">
                      {{ item.cooldownMode }}
                    </span>
                    <span
                      v-if="item.proofRequirement !== 'NONE'"
                      class="rounded-full border border-warning px-2 py-1 text-warning"
                    >
                      {{ t('rankingBounties.proofRequired') }}
                    </span>
                    <span
                      v-if="item.awardedToday"
                      class="rounded-full border border-good px-2 py-1 text-good"
                    >
                      {{ t('rankingBounties.grantedToday') }}
                    </span>
                  </div>
                </td>
                <td class="px-5 py-3 font-semibold">{{ formatPoints(item.rewardPoints) }}</td>
                <td class="px-5 py-3">
                  <span
                    class="font-semibold"
                    :class="item.isAvailableNow ? 'text-good' : 'text-warning'"
                  >
                    {{ getAvailabilityLabel(item) }}
                  </span>
                </td>
                <td class="px-5 py-3">{{ formatDate(item.lastAwardedAtUtc) }}</td>
                <td class="px-5 py-3">{{ formatDate(item.nextAvailableAtUtc) }}</td>
                <td class="px-5 py-3">{{ item.totalAwards }}</td>
              </tr>
              <tr v-if="sortedBounties.length === 0">
                <td colspan="6" class="px-5 py-4 text-sm text-muted">
                  {{ t('rankingBounties.noRows') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>
  </main>
</template>
