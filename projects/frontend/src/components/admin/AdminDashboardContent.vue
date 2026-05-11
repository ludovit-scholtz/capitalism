<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { usesStore } from '@/stores/news'
import AdminNewsComposer from '@/components/admin/AdminNewsComposer.vue'
import AdminPlayerManagement from '@/components/admin/AdminPlayerManagement.vue'

const { t, locale } = useI18n()
const auth = useAuthStore()
const adminStore = useGameAdminStore()
const newsStore = usesStore()

const globalAdminEmail = ref('')
const actionError = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const showShippingCosts = ref(false)
const pendingBenchmarkSaveId = ref<string | null>(null)

const canManageRootFeatures = computed(() => adminStore.session?.isRootAdministrator ?? false)

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatDate(value: string | null) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

async function stopImpersonation() {
  actionError.value = null
  actionMessage.value = null

  try {
    const authPayload = await adminStore.stopImpersonation()
    auth.applyAuthPayload(authPayload)
    await Promise.all([adminStore.fetchSession(), adminStore.fetchDashboard(), newsStore.fetchUnreadCount()])
    actionMessage.value = t('admin.impersonationStopped')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.impersonationFailed')
  }
}

async function assignGlobalAdmin() {
  actionError.value = null

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

  try {
    await adminStore.removeGlobalGameAdminRole(email)
    actionMessage.value = t('admin.globalAdminRemoved')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.globalAdminFailed')
  }
}

async function saveBillionaire(row: { id: string; rank: number; name: string; wealthUsd: number }) {
  actionError.value = null
  pendingBenchmarkSaveId.value = row.id

  try {
    await adminStore.updateRealWorldBillionaire({
      id: row.id,
      rank: Number(row.rank),
      name: row.name,
      wealthUsd: Number(row.wealthUsd),
    })
    actionMessage.value = t('admin.billionaireSaved')
  } catch (caughtError) {
    actionError.value =
      caughtError instanceof Error ? caughtError.message : t('admin.billionaireSaveFailed')
  } finally {
    pendingBenchmarkSaveId.value = null
  }
}

const endShardReason = ref('')
const endShardPending = ref(false)
const endShardConfirmOpen = ref(false)

async function confirmEndShard() {
  endShardConfirmOpen.value = false
  endShardPending.value = true
  actionError.value = null
  actionMessage.value = null

  try {
    await adminStore.endShardManually(endShardReason.value || undefined)
    actionMessage.value = t('endgame.endShardSuccess')
    endShardReason.value = ''
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('endgame.endShardFailed')
  } finally {
    endShardPending.value = false
  }
}
</script>

<template>
  <button v-if="adminStore.session?.isImpersonating" type="button" class="btn btn-secondary admin-stop-impersonation" @click="stopImpersonation">{{ t('admin.stopImpersonation') }}</button>

  <div v-if="actionError" class="admin-banner admin-banner-error">{{ actionError }}</div>
  <div v-else-if="actionMessage" class="admin-banner">{{ actionMessage }}</div>

  <section v-if="adminStore.dashboard" class="admin-metrics">
    <article class="admin-metric-card">
      <span>{{ t('admin.moneySupply') }}</span>
      <strong>{{ formatCurrency(adminStore.dashboard.moneySupply) }}</strong>
    </article>
    <article class="admin-metric-card">
      <span>{{ t('admin.personalCash') }}</span>
      <strong>{{ formatCurrency(adminStore.dashboard.totalPersonalCash) }}</strong>
    </article>
    <article class="admin-metric-card">
      <span>{{ t('admin.companyCash') }}</span>
      <strong>{{ formatCurrency(adminStore.dashboard.totalCompanyCash) }}</strong>
    </article>
    <article class="admin-metric-card admin-metric-highlight">
      <span>{{ t('admin.externalInflow') }}</span>
      <strong>{{ formatCurrency(adminStore.dashboard.externalMoneyInflowLast100Ticks) }}</strong>
    </article>
    <button type="button" class="admin-metric-card admin-metric-button" @click="showShippingCosts = !showShippingCosts">
      <span>{{ t('admin.shippingCosts') }}</span>
      <strong>{{ formatCurrency(adminStore.dashboard.totalShippingCostsLast100Ticks) }}</strong>
    </button>
  </section>

  <section class="admin-grid">
    <article class="card admin-panel">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.inflowTitle') }}</h2>
          <p>{{ t('admin.inflowBody') }}</p>
        </div>
      </div>
      <div class="admin-list">
        <div v-for="summary in adminStore.dashboard?.inflowSummaries ?? []" :key="summary.category" class="admin-list-item">
          <div>
            <strong>{{ summary.category }}</strong>
            <p>{{ summary.description }}</p>
          </div>
          <span>{{ formatCurrency(summary.amount) }}</span>
        </div>
      </div>
    </article>

    <article class="card admin-panel">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.shippingTitle') }}</h2>
          <p>{{ t('admin.shippingBody') }}</p>
        </div>
        <button type="button" class="btn btn-secondary" @click="showShippingCosts = !showShippingCosts">{{ showShippingCosts ? t('common.close') : t('admin.viewShippingCosts') }}</button>
      </div>
      <div v-if="!showShippingCosts" class="admin-empty-state">{{ t('admin.shippingClosed') }}</div>
      <div v-else-if="(adminStore.dashboard?.shippingCostSummaries.length ?? 0) === 0" class="admin-empty-state">{{ t('admin.shippingEmpty') }}</div>
      <div v-else class="admin-list">
        <div v-for="summary in adminStore.dashboard?.shippingCostSummaries ?? []" :key="summary.companyId" class="admin-list-item">
          <div>
            <strong>{{ summary.companyName }}</strong>
            <p>{{ t('admin.shippingEntryCount', { count: summary.entryCount }) }}</p>
          </div>
          <span>{{ formatCurrency(summary.amount) }}</span>
        </div>
      </div>
    </article>

    <article class="card admin-panel">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.alertsTitle') }}</h2>
          <p>{{ t('admin.alertsBody') }}</p>
        </div>
      </div>
      <div v-if="(adminStore.dashboard?.multiAccountAlerts.length ?? 0) === 0" class="admin-empty-state">{{ t('admin.alertsEmpty') }}</div>
      <div v-else class="admin-alert-list">
        <div v-for="alert in adminStore.dashboard?.multiAccountAlerts ?? []" :key="`${alert.reason}-${alert.supportingEntityName}`" class="admin-alert-card">
          <div class="admin-alert-topline">
            <span class="badge badge-warning">{{ alert.reason }}</span>
            <span>{{ alert.confidenceScore.toFixed(2) }}</span>
          </div>
          <p class="admin-alert-body">
            <strong>{{ alert.primaryPlayer.displayName }}</strong>
            {{ t('admin.alertsLinkedTo') }}
            <strong>{{ alert.relatedPlayer.displayName }}</strong>
          </p>
          <p class="admin-alert-meta">{{ alert.supportingEntityType }} · {{ alert.supportingEntityName }}</p>
          <p class="admin-alert-amount">{{ formatCurrency(alert.exposureAmount) }}</p>
        </div>
      </div>
    </article>
  </section>

  <AdminPlayerManagement />

  <section class="admin-grid admin-grid-wide">
    <AdminNewsComposer />
  </section>

  <section class="admin-grid admin-grid-wide">
    <article class="card admin-panel admin-panel-wide">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.billionaireTitle') }}</h2>
          <p>{{ t('admin.billionaireBody') }}</p>
        </div>
      </div>

      <div class="admin-list">
        <div
          v-for="row in adminStore.dashboard?.realWorldBillionaires ?? []"
          :key="row.id"
          class="admin-list-item admin-list-item-form"
        >
          <div class="admin-inline-fields admin-inline-fields-benchmark">
            <input v-model.number="row.rank" class="form-input" type="number" min="1" max="10" />
            <input v-model="row.name" class="form-input" type="text" maxlength="120" />
            <input v-model.number="row.wealthUsd" class="form-input" type="number" min="1" step="1000000" />
          </div>
          <button
            type="button"
            class="btn btn-secondary"
            :disabled="pendingBenchmarkSaveId === row.id"
            @click="() => void saveBillionaire(row)"
          >
            {{ pendingBenchmarkSaveId === row.id ? t('common.loading') : t('common.save') }}
          </button>
        </div>
      </div>
    </article>

    <!-- End Shard section -->
    <article class="card admin-panel admin-panel-wide">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('endgame.endShardTitle') }}</h2>
          <p>{{ t('endgame.endShardBody') }}</p>
        </div>
      </div>

      <div v-if="!endShardConfirmOpen" class="admin-list-item" style="padding: 1rem 0 0;">
        <div class="flex flex-col gap-3">
          <label class="text-sm text-muted" for="endShardReasonInput">{{ t('endgame.endShardReason') }}</label>
          <input
            id="endShardReasonInput"
            v-model="endShardReason"
            class="form-input"
            type="text"
            maxlength="500"
            :placeholder="t('common.optional')"
          />
          <button
            type="button"
            class="btn btn-secondary self-start"
            :disabled="endShardPending"
            @click="endShardConfirmOpen = true"
          >
            {{ endShardPending ? t('common.loading') : t('endgame.endShardButton') }}
          </button>
        </div>
      </div>

      <!-- Confirm dialog -->
      <div v-else class="admin-list-item" style="padding: 1rem 0 0;">
        <p class="text-sm text-muted mb-4">{{ t('endgame.endShardConfirm') }}</p>
        <div class="flex gap-3">
          <button type="button" class="btn btn-secondary" :disabled="endShardPending" @click="() => void confirmEndShard()">
            {{ t('common.confirm') }}
          </button>
          <button type="button" class="btn btn-ghost" :disabled="endShardPending" @click="endShardConfirmOpen = false">
            {{ t('common.cancel') }}
          </button>
        </div>
      </div>
    </article>
  </section>

  <section v-if="canManageRootFeatures" class="admin-grid admin-grid-wide">
    <article class="card admin-panel admin-panel-wide">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.globalAdminsTitle') }}</h2>
          <p>{{ t('admin.globalAdminsBody') }}</p>
        </div>
      </div>

      <div class="admin-global-admins">
        <div class="admin-inline-fields">
          <input v-model="globalAdminEmail" class="form-input" :placeholder="t('admin.globalAdminPlaceholder')" />
          <button type="button" class="btn btn-primary" @click="assignGlobalAdmin">{{ t('admin.grantGlobalAdmin') }}</button>
        </div>

        <div class="admin-list">
          <div v-for="grant in adminStore.dashboard?.globalGameAdminGrants ?? []" :key="grant.id" class="admin-list-item">
            <div>
              <strong>{{ grant.email }}</strong>
              <p>{{ grant.grantedByEmail }} · {{ formatDate(grant.updatedAtUtc) }}</p>
            </div>
            <button type="button" class="btn btn-ghost" @click="removeGlobalAdmin(grant.email)">{{ t('admin.removeGlobalAdmin') }}</button>
          </div>
        </div>
      </div>
    </article>
  </section>

  <section class="admin-grid admin-grid-wide">
    <article class="card admin-panel admin-panel-wide">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.auditTitle') }}</h2>
          <p>{{ t('admin.auditBody') }}</p>
        </div>
      </div>
      <div class="admin-audit-list">
        <div v-for="log in adminStore.dashboard?.recentAuditLogs ?? []" :key="log.id" class="admin-list-item">
          <div>
            <strong>{{ log.adminActorDisplayName }}</strong>
            <p>
              {{ log.effectivePlayerDisplayName }} · {{ log.effectiveAccountType }}
              <span v-if="log.effectiveCompanyName">· {{ log.effectiveCompanyName }}</span>
            </p>
            <p>{{ log.graphQlOperationName || log.mutationSummary }}</p>
          </div>
          <span>{{ formatDate(log.recordedAtUtc) }}</span>
        </div>
      </div>
    </article>
  </section>
</template>

<style scoped>
.admin-stop-impersonation {
  margin-bottom: 1rem;
}

.admin-banner {
  margin-bottom: 1rem;
  padding: 0.85rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.14);
}

.admin-banner-error {
  border-color: rgba(248, 113, 113, 0.45);
  background: rgba(248, 113, 113, 0.12);
}

.admin-metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
}

.admin-metric-card {
  padding: 1.2rem;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-border);
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.03), rgba(255, 255, 255, 0));
  display: grid;
  gap: 0.45rem;
}

.admin-metric-button {
  width: 100%;
  text-align: left;
  cursor: pointer;
}

.admin-metric-card span {
  color: var(--color-text-secondary);
}

.admin-metric-card strong {
  font-size: 1.55rem;
}

.admin-metric-highlight {
  border-color: rgba(255, 138, 0, 0.5);
}

.admin-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
}

.admin-grid-wide {
  grid-template-columns: minmax(0, 1fr);
}

.admin-panel {
  padding: 1.25rem;
}

.admin-panel-wide {
  padding: 1.4rem;
}

.admin-panel-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.admin-panel-header p {
  color: var(--color-text-secondary);
  margin-top: 0.3rem;
}

.admin-list,
.admin-audit-list {
  display: grid;
  gap: 0.75rem;
}

.admin-list-item {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
  padding: 0.95rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.02);
}

.admin-list-item-form {
  align-items: flex-end;
}

.admin-list-item p {
  color: var(--color-text-secondary);
  margin-top: 0.2rem;
}

.admin-empty-state {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px dashed var(--color-border);
  color: var(--color-text-secondary);
}

.admin-alert-list {
  display: grid;
  gap: 0.75rem;
}

.admin-alert-card {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(255, 138, 0, 0.35);
  background: rgba(255, 138, 0, 0.08);
}

.admin-alert-topline {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.7rem;
}

.admin-alert-body {
  margin-bottom: 0.35rem;
}

.admin-alert-meta {
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

.admin-alert-amount {
  margin-top: 0.45rem;
  font-weight: 700;
}

.admin-global-admins {
  display: grid;
  gap: 1rem;
}

.admin-inline-fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.admin-inline-fields-benchmark {
  grid-template-columns: minmax(0, 110px) minmax(0, 1.1fr) minmax(0, 0.9fr);
}

@media (max-width: 1080px) {
  .admin-metrics,
  .admin-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 720px) {
  .admin-panel-header,
  .admin-list-item {
    flex-direction: column;
    align-items: stretch;
  }

  .admin-metrics,
  .admin-grid,
  .admin-inline-fields {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
