<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import { useAuthStore } from '@/stores/auth'

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface SecurityFinding {
  slug: string
  fileStem: string
  filePath: string
  number: number
  title: string
  severity: 'Critical' | 'High' | 'Medium' | 'Low' | string
  status: string
  issues: number[]
  owner: string
}

interface SecurityBoardReport {
  generatedAt: string
  totalFindings: number
  gateStatus: 'pass' | 'fail'
  failingCount: number
  findings: SecurityFinding[]
}

// ---------------------------------------------------------------------------
// Auth guard
// ---------------------------------------------------------------------------

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  if (!auth.gameAdminChecked) {
    await auth.refreshGameAdminAccess()
  }

  if (!auth.isGameAdmin) {
    void router.push('/')
    return
  }

  await fetchBoard()
})

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

const loading = ref(true)
const error = ref<string | null>(null)
const report = ref<SecurityBoardReport | null>(null)

const severityFilter = ref<string>('all')
const statusFilter = ref<string>('all')

// ---------------------------------------------------------------------------
// Data loading — fetches the JSON report from the CI script output file
// ---------------------------------------------------------------------------

const BOARD_JSON_URL = '/security-board-report.json'

async function fetchBoard() {
  loading.value = true
  error.value = null
  try {
    const res = await fetch(BOARD_JSON_URL)
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    const data = (await res.json()) as SecurityBoardReport
    report.value = data
  } catch (err) {
    // Fallback: render empty state with a helpful message
    console.error('[SecurityBoardView] Failed to load report:', err)
    report.value = { generatedAt: '', totalFindings: 0, gateStatus: 'pass', failingCount: 0, findings: [] }
    error.value = t('securityBoard.loadError')
  } finally {
    loading.value = false
  }
}

// ---------------------------------------------------------------------------
// Computed / helpers
// ---------------------------------------------------------------------------

const severityOptions = ['all', 'Critical', 'High', 'Medium', 'Low']
const statusOptions = ['all', 'Open', 'In-Progress', 'Resolved']

const filteredFindings = computed(() => {
  if (!report.value) return []
  return report.value.findings.filter((f) => {
    const matchesSeverity = severityFilter.value === 'all' || f.severity === severityFilter.value
    const matchesStatus = statusFilter.value === 'all' || f.status === statusFilter.value
    return matchesSeverity && matchesStatus
  })
})

const hasFindings = computed(() => filteredFindings.value.length > 0)

const isAllClear = computed(
  () => !loading.value && !error.value && report.value?.gateStatus === 'pass',
)

function severityClass(severity: string): string {
  switch (severity) {
    case 'Critical':
      return 'severity-critical'
    case 'High':
      return 'severity-high'
    case 'Medium':
      return 'severity-medium'
    case 'Low':
      return 'severity-low'
    default:
      return 'severity-unknown'
  }
}
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('securityBoard.kicker')"
      :title="t('securityBoard.title')"
      :subtitle="t('securityBoard.subtitle')"
      variant="admin"
    />

    <section class="container py-8">
      <!-- Loading -->
      <div v-if="loading" class="state-loading">
        {{ t('common.loading') }}
      </div>

      <!-- Error -->
      <div v-else-if="error" class="state-error">
        {{ error }}
      </div>

      <!-- All clear -->
      <div v-else-if="isAllClear" class="all-clear-banner">
        <span class="all-clear-icon">🎉</span>
        <div>
          <h2>{{ t('securityBoard.allClearTitle') }}</h2>
          <p v-if="report?.generatedAt">
            {{ t('securityBoard.lastRun', { date: report.generatedAt.slice(0, 10) }) }}
          </p>
        </div>
      </div>

      <!-- Board -->
      <template v-else-if="report">
        <!-- Gate warning -->
        <div v-if="report.gateStatus === 'fail'" class="gate-warning">
          <strong>⚠️ {{ t('securityBoard.gateWarning', { count: report.failingCount }) }}</strong>
          <p>{{ t('securityBoard.gateHint') }}</p>
        </div>

        <!-- Filters -->
        <div class="board-filters">
          <label class="filter-group">
            <span>{{ t('securityBoard.filterSeverity') }}</span>
            <select v-model="severityFilter" class="filter-select">
              <option v-for="opt in severityOptions" :key="opt" :value="opt">
                {{ opt === 'all' ? t('common.allTypes') : opt }}
              </option>
            </select>
          </label>

          <label class="filter-group">
            <span>{{ t('securityBoard.filterStatus') }}</span>
            <select v-model="statusFilter" class="filter-select">
              <option v-for="opt in statusOptions" :key="opt" :value="opt">
                {{ opt === 'all' ? t('common.allStatuses') : opt }}
              </option>
            </select>
          </label>

          <span class="finding-count">
            {{ t('securityBoard.findingCount', { count: filteredFindings.length, total: report.totalFindings }) }}
          </span>
        </div>

        <!-- Empty state after filter -->
        <div v-if="!hasFindings" class="empty-state-message">
          {{ t('common.noData') }}
        </div>

        <!-- Findings table (scrollable on mobile) -->
        <div v-else class="table-wrap">
          <table class="board-table" aria-label="Security findings">
            <thead>
              <tr>
                <th>{{ t('securityBoard.colSeverity') }}</th>
                <th>{{ t('securityBoard.colFinding') }}</th>
                <th>{{ t('securityBoard.colStatus') }}</th>
                <th>{{ t('securityBoard.colOwner') }}</th>
                <th>{{ t('securityBoard.colIssues') }}</th>
                <th>{{ t('securityBoard.colSource') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="finding in filteredFindings" :key="finding.slug" class="finding-row">
                <td>
                  <span :class="['severity-badge', severityClass(finding.severity)]">
                    {{ finding.severity }}
                  </span>
                </td>
                <td class="finding-title">{{ finding.title }}</td>
                <td>{{ finding.status }}</td>
                <td class="finding-owner">{{ finding.owner || '—' }}</td>
                <td class="finding-issues">
                  <template v-if="finding.issues.length > 0">
                    <span
                      v-for="num in finding.issues"
                      :key="num"
                      class="issue-ref"
                    >#{{ num }}</span>
                  </template>
                  <span v-else class="no-issue">—</span>
                </td>
                <td>
                  <span class="audit-source" :title="finding.filePath">{{ finding.fileStem }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <p v-if="report.generatedAt" class="board-timestamp">
          {{ t('securityBoard.lastRun', { date: report.generatedAt.slice(0, 10) }) }}
        </p>
      </template>
    </section>
  </main>
</template>

<style scoped>
/* Gate warning */
.gate-warning {
  border: 1px solid var(--color-warning, #f59e0b);
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-warning, #f59e0b) 10%, transparent);
  padding: 1rem 1.25rem;
  margin-bottom: 1.5rem;
  color: var(--color-text);
}

.gate-warning p {
  margin-top: 0.25rem;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

/* All-clear banner */
.all-clear-banner {
  display: flex;
  align-items: center;
  gap: 1rem;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  padding: 1.5rem;
}

.all-clear-icon {
  font-size: 2.5rem;
  line-height: 1;
}

.all-clear-banner h2 {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--color-text);
}

.all-clear-banner p {
  margin-top: 0.25rem;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

/* Filters */
.board-filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1rem;
}

.filter-group {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

.filter-select {
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text);
  padding: 0.25rem 0.5rem;
  font-size: 0.875rem;
}

.finding-count {
  margin-left: auto;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

/* Table */
.table-wrap {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: 10px;
}

.board-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
  color: var(--color-text);
}

.board-table th {
  background: var(--color-surface);
  padding: 0.75rem 1rem;
  text-align: left;
  font-weight: 600;
  border-bottom: 1px solid var(--color-border);
  white-space: nowrap;
}

.finding-row td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--color-border);
  vertical-align: top;
}

.finding-row:last-child td {
  border-bottom: none;
}

/* Severity badges */
.severity-badge {
  display: inline-block;
  padding: 0.2rem 0.6rem;
  border-radius: 20px;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  white-space: nowrap;
}

.severity-critical {
  background: #fee2e2;
  color: #b91c1c;
}

.severity-high {
  background: #fef3c7;
  color: #92400e;
}

.severity-medium {
  background: #fef9c3;
  color: #713f12;
}

.severity-low {
  background: #dcfce7;
  color: #166534;
}

.severity-unknown {
  background: var(--color-surface);
  color: var(--color-text-secondary);
}

.finding-title {
  max-width: 32ch;
  font-weight: 500;
}

.finding-owner {
  white-space: nowrap;
  color: var(--color-text-secondary);
}

.finding-issues {
  white-space: nowrap;
}

.issue-ref {
  display: inline-block;
  color: var(--color-primary, #6366f1);
  margin-right: 0.25rem;
}

.no-issue {
  color: var(--color-text-secondary);
}

.audit-source {
  color: var(--color-primary, #6366f1);
  font-size: 0.8rem;
}

/* Loading / error / empty */
.state-loading,
.state-error,
.empty-state-message {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--color-text-secondary);
}

.state-error {
  color: #b91c1c;
}

.board-timestamp {
  margin-top: 1rem;
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}
</style>
