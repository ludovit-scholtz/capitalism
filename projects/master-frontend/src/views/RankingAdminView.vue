<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
  fetchRankingAdminDashboard,
  moderateRankingEvent,
  runRankingDailyDecayNow,
  runRankingEvaluationNow,
  upsertRankingBountyDefinition,
  type RankingAdminDashboardInfo,
  type RankingBountyDefinitionInfo,
} from '@/lib/masterApi'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const dashboard = ref<RankingAdminDashboardInfo | null>(null)

const navItems = computed(() => [
  { label: t('rankingAdmin.dashboardLink'), to: '/ranking' },
  { label: t('home.supportAdmin'), to: '/support/admin' },
  { label: t('common.backToPortal'), to: '/' },
])

function parseJsonOrEmpty(value: string) {
  const trimmed = value.trim()
  if (!trimmed) return '{}'

  try {
    JSON.parse(trimmed)
    return trimmed
  } catch {
    return '{}'
  }
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

async function loadDashboard() {
  if (!auth.token) return

  loading.value = true
  errorMessage.value = ''

  try {
    dashboard.value = await fetchRankingAdminDashboard(auth.token)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('rankingAdmin.loadError')
  } finally {
    loading.value = false
  }
}

async function runEvaluator() {
  if (!auth.token) return

  errorMessage.value = ''
  successMessage.value = ''

  try {
    const run = await runRankingEvaluationNow(auth.token)
    successMessage.value = t('rankingAdmin.runEvaluatorSuccess', { id: run.id })
    await loadDashboard()
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t('rankingAdmin.runEvaluatorError')
  }
}

async function runDecay() {
  if (!auth.token) return

  errorMessage.value = ''
  successMessage.value = ''

  try {
    const run = await runRankingDailyDecayNow(auth.token)
    successMessage.value = t('rankingAdmin.runDecaySuccess', { id: run.id })
    await loadDashboard()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('rankingAdmin.runDecayError')
  }
}

async function moderate(eventId: string, approve: boolean) {
  if (!auth.token) return

  errorMessage.value = ''
  successMessage.value = ''

  try {
    await moderateRankingEvent(auth.token, {
      eventId,
      approve,
      reason: approve ? 'Approved in dashboard' : 'Rejected in dashboard',
    })
    successMessage.value = approve
      ? t('rankingAdmin.approveSuccess')
      : t('rankingAdmin.rejectSuccess')
    await loadDashboard()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('rankingAdmin.moderationError')
  }
}

async function saveDefinition(definition: RankingBountyDefinitionInfo) {
  if (!auth.token) return

  errorMessage.value = ''
  successMessage.value = ''

  try {
    await upsertRankingBountyDefinition(auth.token, {
      id: definition.id,
      code: definition.code,
      displayName: definition.displayName,
      description: definition.description,
      rewardPoints: Number(definition.rewardPoints),
      isEnabled: definition.isEnabled,
      isVisibleToPlayers: definition.isVisibleToPlayers,
      requiresModeration: definition.requiresModeration,
      cooldownMode: definition.cooldownMode,
      sourceEventType: definition.sourceEventType,
      proofRequirement: definition.proofRequirement,
      visibilityScope: definition.visibilityScope,
      validationSettingsJson: parseJsonOrEmpty(definition.validationSettingsJson),
    })
    successMessage.value = t('rankingAdmin.saveDefinitionSuccess')
    await loadDashboard()
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t('rankingAdmin.saveDefinitionError')
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  if (!auth.isGameAdmin) {
    void router.push('/')
    return
  }

  await loadDashboard()
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.rankingAdmin')"
      :title="t('rankingAdmin.title')"
      :subtitle="t('rankingAdmin.subtitle')"
      variant="admin"
    />
    <ViewSubnav :items="navItems" aria-label="Ranking admin navigation" />

    <section class="container pb-16 pt-2 lg:pb-20 lg:pt-2">
      <section class="card p-6" aria-label="Ranking scheduler controls">
        <div class="flex flex-wrap items-center gap-2">
          <button type="button" class="btn btn-secondary" @click="runEvaluator">
            {{ t('rankingAdmin.runEvaluator') }}
          </button>
          <button type="button" class="btn btn-secondary" @click="runDecay">
            {{ t('rankingAdmin.runDecay') }}
          </button>
          <button type="button" class="btn btn-secondary" @click="loadDashboard">
            {{ t('common.refresh') }}
          </button>
        </div>
        <p v-if="errorMessage" class="state-error mt-3" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="mt-3 text-good" role="status">{{ successMessage }}</p>
      </section>

      <section class="card mt-5 p-6" aria-label="Pending moderation events">
        <h2 class="text-xl font-semibold">{{ t('rankingAdmin.pendingModeration') }}</h2>
        <p v-if="loading" class="state-message mt-3">{{ t('common.loading') }}</p>
        <p
          v-else-if="(dashboard?.pendingModerationEvents?.length ?? 0) === 0"
          class="state-message mt-3"
        >
          {{ t('common.noData') }}
        </p>
        <div v-else class="mt-4 overflow-x-auto rounded-xl border border-divider">
          <table
            class="min-w-full border-collapse text-sm"
            aria-label="Ranking moderation queue table"
          >
            <thead>
              <tr class="bg-overlay/40 text-left text-xs uppercase tracking-[0.08em] text-muted">
                <th class="px-4 py-3">{{ t('rankingAdmin.eventType') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.playerEmail') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.server') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.proof') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.occurredAt') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="item in dashboard?.pendingModerationEvents ?? []"
                :key="item.id"
                class="border-t border-divider/70"
              >
                <td class="px-4 py-3">{{ item.eventType }}</td>
                <td class="px-4 py-3">{{ item.playerEmail }}</td>
                <td class="px-4 py-3">{{ item.serverKey ?? '-' }}</td>
                <td class="px-4 py-3">{{ item.proofReference ?? '-' }}</td>
                <td class="px-4 py-3">{{ formatDate(item.occurredAtUtc) }}</td>
                <td class="px-4 py-3">
                  <div class="flex items-center gap-2">
                    <button
                      class="btn btn-secondary"
                      type="button"
                      @click="moderate(item.id, true)"
                    >
                      {{ t('rankingAdmin.approve') }}
                    </button>
                    <button
                      class="btn btn-secondary"
                      type="button"
                      @click="moderate(item.id, false)"
                    >
                      {{ t('rankingAdmin.reject') }}
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="card mt-5 p-6" aria-label="Bounty definitions table">
        <h2 class="text-xl font-semibold">{{ t('rankingAdmin.bountyDefinitions') }}</h2>
        <p v-if="(dashboard?.bounties?.length ?? 0) === 0" class="state-message mt-3">
          {{ t('common.noData') }}
        </p>
        <div v-else class="mt-4 overflow-x-auto rounded-xl border border-divider">
          <table
            class="min-w-full border-collapse text-sm"
            aria-label="Ranking bounty configuration table"
          >
            <thead>
              <tr class="bg-overlay/40 text-left text-xs uppercase tracking-[0.08em] text-muted">
                <th class="px-4 py-3">{{ t('rankingAdmin.code') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.displayName') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.rewardPoints') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.enabled') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="definition in dashboard?.bounties ?? []"
                :key="definition.id"
                class="border-t border-divider/70"
              >
                <td class="px-4 py-3">{{ definition.code }}</td>
                <td class="px-4 py-3">{{ definition.displayName }}</td>
                <td class="px-4 py-3">{{ definition.rewardPoints }}</td>
                <td class="px-4 py-3">{{ definition.isEnabled ? 'YES' : 'NO' }}</td>
                <td class="px-4 py-3">
                  <button
                    class="btn btn-secondary"
                    type="button"
                    @click="saveDefinition(definition)"
                  >
                    {{ t('rankingAdmin.saveDefinition') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="card mt-5 p-6" aria-label="Recent scheduler runs">
        <h2 class="text-xl font-semibold">{{ t('rankingAdmin.recentRuns') }}</h2>
        <p v-if="(dashboard?.recentRuns?.length ?? 0) === 0" class="state-message mt-3">
          {{ t('common.noData') }}
        </p>
        <div v-else class="mt-4 overflow-x-auto rounded-xl border border-divider">
          <table class="min-w-full border-collapse text-sm" aria-label="Ranking run history table">
            <thead>
              <tr class="bg-overlay/40 text-left text-xs uppercase tracking-[0.08em] text-muted">
                <th class="px-4 py-3">{{ t('rankingAdmin.runType') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.status') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.finishedAt') }}</th>
                <th class="px-4 py-3">{{ t('rankingAdmin.processedEvents') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="run in dashboard?.recentRuns ?? []"
                :key="run.id"
                class="border-t border-divider/70"
              >
                <td class="px-4 py-3">{{ run.runType }}</td>
                <td class="px-4 py-3">{{ run.status }}</td>
                <td class="px-4 py-3">{{ formatDate(run.finishedAtUtc) }}</td>
                <td class="px-4 py-3">{{ run.processedEvents }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </section>
  </main>
</template>
