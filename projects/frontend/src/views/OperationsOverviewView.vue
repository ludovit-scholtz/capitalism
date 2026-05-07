<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useGameAdminStore } from '@/stores/gameAdmin'

const { t, locale } = useI18n()
const auth = useAuthStore()
const adminStore = useGameAdminStore()

const loading = ref(false)
const error = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const actionError = ref<string | null>(null)
const globalAdminEmail = ref('')

const canManageRootFeatures = computed(() => adminStore.session?.isRootAdministrator ?? false)

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatDate(value: string | null | undefined) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

async function loadOverview() {
  loading.value = true
  error.value = null

  try {
    await adminStore.fetchDashboard()
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

async function stopImpersonation() {
  actionError.value = null
  actionMessage.value = null

  try {
    const authPayload = await adminStore.stopImpersonation()
    auth.applyAuthPayload(authPayload)
    await Promise.all([adminStore.fetchSession(), adminStore.fetchDashboard()])
    actionMessage.value = t('admin.impersonationStopped')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.impersonationFailed')
  }
}

async function assignGlobalAdmin() {
  actionError.value = null
  actionMessage.value = null

  try {
    await adminStore.assignGlobalGameAdminRole(globalAdminEmail.value)
    globalAdminEmail.value = ''
    actionMessage.value = t('admin.globalAdminGranted')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.globalAdminFailed')
  }
}

async function removeGlobalAdmin(email: string) {
  actionError.value = null
  actionMessage.value = null

  try {
    await adminStore.removeGlobalGameAdminRole(email)
    actionMessage.value = t('admin.globalAdminRemoved')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.globalAdminFailed')
  }
}

onMounted(loadOverview)
</script>

<template>
  <div class="ops-overview">
    <div class="ops-section-header">
      <div>
        <h2>{{ t('operations.overview.title') }}</h2>
        <p>{{ t('operations.overview.subtitle') }}</p>
      </div>
      <button
        v-if="adminStore.session?.isImpersonating"
        type="button"
        class="btn btn-secondary"
        @click="stopImpersonation"
      >
        {{ t('admin.stopImpersonation') }}
      </button>
    </div>

    <div v-if="actionError" class="ops-banner ops-banner-error">{{ actionError }}</div>
    <div v-else-if="actionMessage" class="ops-banner">{{ actionMessage }}</div>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="loadOverview">{{ t('common.retry') }}</button>
    </div>
    <template v-else-if="adminStore.dashboard">
      <section class="ops-metrics">
        <article class="ops-metric-card card">
          <span>{{ t('admin.moneySupply') }}</span>
          <strong>{{ formatCurrency(adminStore.dashboard.moneySupply) }}</strong>
        </article>
        <article class="ops-metric-card card">
          <span>{{ t('admin.personalCash') }}</span>
          <strong>{{ formatCurrency(adminStore.dashboard.totalPersonalCash) }}</strong>
        </article>
        <article class="ops-metric-card card">
          <span>{{ t('admin.companyCash') }}</span>
          <strong>{{ formatCurrency(adminStore.dashboard.totalCompanyCash) }}</strong>
        </article>
        <article class="ops-metric-card card">
          <span>{{ t('admin.externalInflow') }}</span>
          <strong>{{ formatCurrency(adminStore.dashboard.externalMoneyInflowLast100Ticks) }}</strong>
        </article>
        <article class="ops-metric-card card">
          <span>{{ t('admin.shippingCosts') }}</span>
          <strong>{{ formatCurrency(adminStore.dashboard.totalShippingCostsLast100Ticks) }}</strong>
        </article>
        <article class="ops-metric-card card">
          <span>{{ t('operations.overview.playerCount') }}</span>
          <strong>{{ adminStore.dashboard.players.length }}</strong>
        </article>
      </section>

      <section class="ops-grid">
        <article class="card ops-panel">
          <div class="ops-panel-header">
            <div>
              <h3>{{ t('operations.overview.inflowTitle') }}</h3>
              <p>{{ t('operations.overview.inflowBody') }}</p>
            </div>
          </div>
          <div class="ops-list">
            <div
              v-for="summary in adminStore.dashboard.inflowSummaries"
              :key="summary.category"
              class="ops-list-item"
            >
              <div>
                <strong>{{ summary.category }}</strong>
                <p>{{ summary.description }}</p>
              </div>
              <span>{{ formatCurrency(summary.amount) }}</span>
            </div>
            <div v-if="adminStore.dashboard.inflowSummaries.length === 0" class="ops-empty-state">
              {{ t('operations.overview.inflowEmpty') }}
            </div>
          </div>
          <div class="ops-subsection">
            <div class="ops-subsection-header">
              <h4>{{ t('admin.shippingTitle') }}</h4>
              <span>{{ formatCurrency(adminStore.dashboard.totalShippingCostsLast100Ticks) }}</span>
            </div>
            <div class="ops-list">
              <div
                v-for="summary in adminStore.dashboard.shippingCostSummaries"
                :key="summary.companyId"
                class="ops-list-item"
              >
                <div>
                  <strong>{{ summary.companyName }}</strong>
                  <p>{{ t('admin.shippingEntryCount', { count: summary.entryCount }) }}</p>
                </div>
                <span>{{ formatCurrency(summary.amount) }}</span>
              </div>
              <div v-if="adminStore.dashboard.shippingCostSummaries.length === 0" class="ops-empty-state">
                {{ t('admin.shippingEmpty') }}
              </div>
            </div>
          </div>
        </article>

        <article class="card ops-panel">
          <div class="ops-panel-header">
            <div>
              <h3>{{ t('operations.overview.alertsTitle') }}</h3>
              <p>{{ t('operations.overview.alertsBody') }}</p>
            </div>
          </div>
          <div class="ops-list">
            <div
              v-for="alert in adminStore.dashboard.multiAccountAlerts"
              :key="`${alert.reason}-${alert.supportingEntityName}`"
              class="ops-alert-card"
            >
              <div class="ops-alert-topline">
                <span class="badge badge-warning">{{ alert.reason }}</span>
                <span>{{ alert.confidenceScore.toFixed(2) }}</span>
              </div>
              <p>
                <strong>{{ alert.primaryPlayer.displayName }}</strong>
                {{ t('admin.alertsLinkedTo') }}
                <strong>{{ alert.relatedPlayer.displayName }}</strong>
              </p>
              <p class="ops-alert-meta">{{ alert.supportingEntityType }} · {{ alert.supportingEntityName }}</p>
              <p class="ops-alert-amount">{{ formatCurrency(alert.exposureAmount) }}</p>
            </div>
            <div v-if="adminStore.dashboard.multiAccountAlerts.length === 0" class="ops-empty-state">
              {{ t('admin.alertsEmpty') }}
            </div>
          </div>
        </article>
      </section>

      <section class="ops-grid">
        <article class="card ops-panel">
          <div class="ops-panel-header">
            <div>
              <h3>{{ t('operations.overview.auditTitle') }}</h3>
              <p>{{ t('operations.overview.auditBody') }}</p>
            </div>
          </div>
          <div class="ops-list">
            <div v-for="log in adminStore.dashboard.recentAuditLogs" :key="log.id" class="ops-list-item">
              <div>
                <strong>{{ log.adminActorDisplayName }}</strong>
                <p>{{ log.graphQlOperationName || log.mutationSummary }}</p>
              </div>
              <span>{{ formatDate(log.recordedAtUtc) }}</span>
            </div>
          </div>
        </article>

        <article class="card ops-panel">
          <div class="ops-panel-header">
            <div>
              <h3>{{ t('operations.overview.globalAdminsTitle') }}</h3>
              <p>{{ t('operations.overview.globalAdminsBody') }}</p>
            </div>
          </div>
          <template v-if="canManageRootFeatures">
            <div class="ops-inline-fields">
              <input
                v-model="globalAdminEmail"
                class="form-input"
                :placeholder="t('admin.globalAdminPlaceholder')"
              />
              <button type="button" class="btn btn-primary" @click="assignGlobalAdmin">
                {{ t('admin.grantGlobalAdmin') }}
              </button>
            </div>
            <div class="ops-list">
              <div
                v-for="grant in adminStore.dashboard.globalGameAdminGrants"
                :key="grant.id"
                class="ops-list-item"
              >
                <div>
                  <strong>{{ grant.email }}</strong>
                  <p>{{ grant.grantedByEmail }} · {{ formatDate(grant.updatedAtUtc) }}</p>
                </div>
                <button type="button" class="btn btn-ghost" @click="removeGlobalAdmin(grant.email)">
                  {{ t('admin.removeGlobalAdmin') }}
                </button>
              </div>
              <div v-if="adminStore.dashboard.globalGameAdminGrants.length === 0" class="ops-empty-state">
                {{ t('operations.overview.globalAdminsEmpty') }}
              </div>
            </div>
          </template>
          <div v-else class="ops-empty-state">
            {{ t('operations.overview.rootOnly') }}
          </div>
        </article>
      </section>
    </template>
  </div>
</template>

<style scoped>
.ops-overview {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.ops-section-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
}

.ops-section-header p {
  color: var(--color-text-secondary);
  margin-top: 0.3rem;
}

.ops-banner,
.ops-error {
  padding: 0.9rem 1rem;
}

.ops-banner {
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.1);
}

.ops-banner-error {
  border-color: rgba(248, 113, 113, 0.4);
  background: rgba(248, 113, 113, 0.12);
}

.ops-loading {
  padding: 3rem;
  text-align: center;
  color: var(--color-text-secondary);
}

.ops-metrics {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.85rem;
}

.ops-metric-card {
  padding: 1rem 1.15rem;
  display: grid;
  gap: 0.35rem;
}

.ops-metric-card span {
  color: var(--color-text-secondary);
}

.ops-metric-card strong {
  font-size: 1.4rem;
}

.ops-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.ops-panel {
  padding: 1.25rem;
}

.ops-panel-header {
  margin-bottom: 0.9rem;
}

.ops-panel-header p {
  color: var(--color-text-secondary);
  margin-top: 0.25rem;
}

.ops-list {
  display: grid;
  gap: 0.75rem;
}

.ops-subsection {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.ops-subsection-header {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.ops-list-item {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
  padding: 0.9rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.02);
}

.ops-list-item p,
.ops-empty-state,
.ops-alert-meta {
  color: var(--color-text-secondary);
}

.ops-empty-state {
  padding: 1rem;
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-md);
}

.ops-alert-card {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(255, 138, 0, 0.35);
  background: rgba(255, 138, 0, 0.08);
}

.ops-alert-topline {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.ops-alert-amount {
  font-weight: 700;
  margin-top: 0.45rem;
}

.ops-inline-fields {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 0.75rem;
  margin-bottom: 0.9rem;
}

@media (max-width: 900px) {
  .ops-grid,
  .ops-inline-fields {
    grid-template-columns: 1fr;
  }
}
</style>
