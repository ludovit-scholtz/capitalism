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
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const dashboard = ref<RankingAdminDashboardInfo | null>(null)

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

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
      reason: approve ? 'Approved via admin dashboard' : 'Rejected via admin dashboard',
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

const TWO_HOURS_MS = 2 * 60 * 60 * 1000

const healthStatus = computed(() => {
  const runs = dashboard.value?.recentRuns ?? []
  if (!runs.length) return { level: 'unknown', ageMinutes: 0 }

  const lastRun = runs[0]
  if (!lastRun) return { level: 'unknown', ageMinutes: 0 }
  const ageMs = Date.now() - new Date(lastRun.finishedAtUtc).getTime()
  const ageMinutes = Math.floor(ageMs / 60000)

  if (lastRun.status === 'FAILED') return { level: 'critical', ageMinutes }
  if (ageMs > TWO_HOURS_MS) return { level: 'warning', ageMinutes }
  return { level: 'healthy', ageMinutes }
})

const avgRewardsPerRun = computed(() => {
  const runs = dashboard.value?.recentRuns ?? []
  if (runs.length < 2) return null
  const evalRuns = runs.filter((r) => r.runType === 'EVALUATION')
  if (!evalRuns.length) return null
  const total = evalRuns.reduce((sum, r) => sum + r.rewardRecordsCreated, 0)
  return Math.round(total / evalRuns.length)
})

const rewardSpikeDetected = computed(() => {
  const runs = dashboard.value?.recentRuns ?? []
  const avg = avgRewardsPerRun.value
  if (avg === null || avg === 0 || !runs.length) return false
  const lastEval = runs.find((r) => r.runType === 'EVALUATION')
  if (!lastEval) return false
  return lastEval.rewardRecordsCreated > avg * 5
})

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  await loadDashboard()
})
</script>

<template>
  <main class="ranking-admin-shell">
    <header class="admin-header">
      <h1>{{ t('rankingAdmin.title') }}</h1>
      <p>{{ t('rankingAdmin.subtitle') }}</p>
      <div class="nav-links">
        <a href="/ranking" class="nav-link">{{ t('rankingAdmin.dashboardLink') }}</a>
        <a href="/" class="nav-link">← {{ t('common.backToPortal') }}</a>
      </div>
    </header>

    <section
      class="panel health-panel"
      :class="`health-${healthStatus.level}`"
      aria-label="Evaluator health status"
    >
      <h2>{{ t('rankingAdmin.health') }}</h2>
      <div class="health-status-row">
        <span class="health-badge" :class="`badge-${healthStatus.level}`">
          {{
            t(
              `rankingAdmin.health${healthStatus.level.charAt(0).toUpperCase() + healthStatus.level.slice(1)}`,
            )
          }}
        </span>
        <span v-if="healthStatus.level !== 'unknown'" class="health-age">
          {{ t('rankingAdmin.healthLastRun', { minutes: healthStatus.ageMinutes }) }}
        </span>
        <span v-else class="health-age">{{ t('rankingAdmin.healthNoRuns') }}</span>
      </div>
      <p
        v-if="healthStatus.level === 'critical'"
        class="health-alert health-alert-critical"
        role="alert"
      >
        {{ t('rankingAdmin.healthFailed') }}
      </p>
      <p
        v-else-if="healthStatus.level === 'warning'"
        class="health-alert health-alert-warning"
        role="alert"
      >
        {{ t('rankingAdmin.healthDelayed', { minutes: healthStatus.ageMinutes }) }}
      </p>
      <p v-if="rewardSpikeDetected" class="health-alert health-alert-warning" role="alert">
        {{ t('rankingAdmin.rewardSpike', { avg: avgRewardsPerRun }) }}
      </p>
    </section>

    <section class="panel" aria-label="Ranking scheduler controls">
      <div class="actions">
        <button type="button" @click="runEvaluator">{{ t('rankingAdmin.runEvaluator') }}</button>
        <button type="button" @click="runDecay">{{ t('rankingAdmin.runDecay') }}</button>
        <button type="button" @click="loadDashboard">{{ t('common.refresh') }}</button>
      </div>
      <p v-if="errorMessage" class="state-error" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="state-success" role="status">{{ successMessage }}</p>
    </section>

    <section class="panel" aria-label="Pending moderation events">
      <h2>{{ t('rankingAdmin.pendingModeration') }}</h2>
      <p v-if="loading">{{ t('common.loading') }}</p>
      <table v-else class="moderation-table" aria-label="Ranking moderation queue table">
        <thead>
          <tr>
            <th>{{ t('rankingAdmin.eventType') }}</th>
            <th>{{ t('rankingAdmin.playerEmail') }}</th>
            <th>{{ t('rankingAdmin.server') }}</th>
            <th>{{ t('rankingAdmin.proof') }}</th>
            <th>{{ t('rankingAdmin.occurredAt') }}</th>
            <th>{{ t('rankingAdmin.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in dashboard?.pendingModerationEvents ?? []" :key="item.id">
            <td>{{ item.eventType }}</td>
            <td>{{ item.playerEmail }}</td>
            <td>{{ item.serverKey ?? '-' }}</td>
            <td class="proof-cell">{{ item.proofReference ?? '-' }}</td>
            <td>{{ formatDate(item.occurredAtUtc) }}</td>
            <td>
              <div class="action-buttons">
                <button type="button" @click="moderate(item.id, true)">
                  {{ t('rankingAdmin.approve') }}
                </button>
                <button type="button" @click="moderate(item.id, false)">
                  {{ t('rankingAdmin.reject') }}
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <section class="panel" aria-label="Bounty configuration table">
      <h2>{{ t('rankingAdmin.bountyConfig') }}</h2>
      <table class="definitions-table" aria-label="Ranking bounty configuration table">
        <thead>
          <tr>
            <th>{{ t('rankingAdmin.code') }}</th>
            <th>{{ t('rankingAdmin.rewardPoints') }}</th>
            <th>{{ t('rankingAdmin.enabled') }}</th>
            <th>{{ t('rankingAdmin.requiresModeration') }}</th>
            <th>{{ t('rankingAdmin.cooldownMode') }}</th>
            <th>{{ t('rankingAdmin.save') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="definition in dashboard?.bounties ?? []" :key="definition.id">
            <td>{{ definition.code }}</td>
            <td>
              <input v-model.number="definition.rewardPoints" type="number" step="0.1" />
            </td>
            <td>
              <input v-model="definition.isEnabled" type="checkbox" />
            </td>
            <td>
              <input v-model="definition.requiresModeration" type="checkbox" />
            </td>
            <td>
              <select v-model="definition.cooldownMode">
                <option value="NONE">NONE</option>
                <option value="UTC_DAY">UTC_DAY</option>
                <option value="UTC_DAY_PER_SERVER">UTC_DAY_PER_SERVER</option>
                <option value="ONCE">ONCE</option>
                <option value="PER_UNIQUE_KEY">PER_UNIQUE_KEY</option>
              </select>
            </td>
            <td>
              <button type="button" @click="saveDefinition(definition)">
                {{ t('rankingAdmin.save') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <section class="panel" aria-label="Recent ranking runs">
      <h2>{{ t('rankingAdmin.recentRuns') }}</h2>
      <table class="runs-table" aria-label="Ranking run history table">
        <thead>
          <tr>
            <th>{{ t('rankingAdmin.runType') }}</th>
            <th>{{ t('rankingAdmin.status') }}</th>
            <th>{{ t('rankingAdmin.startedAt') }}</th>
            <th>{{ t('rankingAdmin.finishedAt') }}</th>
            <th>{{ t('rankingAdmin.processedEvents') }}</th>
            <th>{{ t('rankingAdmin.rewardRecordsCreated') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="run in dashboard?.recentRuns ?? []" :key="run.id">
            <td>{{ run.runType }}</td>
            <td>{{ run.status }}</td>
            <td>{{ formatDate(run.startedAtUtc) }}</td>
            <td>{{ formatDate(run.finishedAtUtc) }}</td>
            <td>{{ run.processedEvents }}</td>
            <td>{{ run.rewardRecordsCreated }}</td>
          </tr>
        </tbody>
      </table>
    </section>
  </main>
</template>

<style scoped>
.ranking-admin-shell {
  max-width: 1300px;
  margin: 2rem auto;
  padding: 0 1rem 2.5rem;
  color: #efeef6;
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

.actions,
.action-buttons {
  display: flex;
  gap: 0.5rem;
}

.moderation-table,
.definitions-table,
.runs-table {
  width: 100%;
  border-collapse: collapse;
}

.moderation-table th,
.moderation-table td,
.definitions-table th,
.definitions-table td,
.runs-table th,
.runs-table td {
  border-bottom: 1px solid rgba(255, 255, 255, 0.12);
  padding: 0.55rem;
  text-align: left;
}

.proof-cell {
  max-width: 360px;
  overflow-wrap: anywhere;
}

.state-error {
  color: #ff9e9e;
}

.state-success {
  color: #8bffb5;
}

.health-status-row {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin: 0.5rem 0;
}

.health-badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 99px;
  font-size: 0.85rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.badge-healthy {
  background: rgba(139, 255, 181, 0.18);
  color: #8bffb5;
  border: 1px solid #8bffb5;
}

.badge-warning {
  background: rgba(255, 212, 121, 0.18);
  color: #ffd479;
  border: 1px solid #ffd479;
}

.badge-critical {
  background: rgba(255, 100, 100, 0.18);
  color: #ff9e9e;
  border: 1px solid #ff9e9e;
}

.badge-unknown {
  background: rgba(150, 150, 150, 0.18);
  color: #aaa;
  border: 1px solid #aaa;
}

.health-age {
  color: rgba(255, 255, 255, 0.65);
  font-size: 0.9rem;
}

.health-alert {
  padding: 0.5rem 0.75rem;
  border-radius: 8px;
  margin-top: 0.5rem;
  font-size: 0.9rem;
}

.health-alert-critical {
  background: rgba(255, 100, 100, 0.12);
  color: #ff9e9e;
  border: 1px solid rgba(255, 100, 100, 0.3);
}

.health-alert-warning {
  background: rgba(255, 212, 121, 0.12);
  color: #ffd479;
  border: 1px solid rgba(255, 212, 121, 0.3);
}
</style>
