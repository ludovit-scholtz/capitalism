<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { BuildingBankAccountInfo, CompanyBankAccountSummary } from '@/types'
import BuildingBankTransferForm from '@/components/buildings/BuildingBankTransferForm.vue'

interface Props {
  buildingId: string
  companyId: string
  /** ISO 4217 currency code for the building's city */
  currencyCode: string
  /** Loading state while the parent fetches building data */
  loading: boolean
  /** Render the account-assignment controls. */
  showAssignmentControls?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  showAssignmentControls: true,
})
const emit = defineEmits<{
  (e: 'updated'): void
}>()

const { t, locale } = useI18n()

const accountInfo = ref<BuildingBankAccountInfo | null>(null)
const companyAccounts = ref<CompanyBankAccountSummary[]>([])
const accountLoading = ref(false)
const accountsLoading = ref(false)
/** Monotonic counter used to discard stale fetchAccountInfo() responses. */
const accountInfoVersion = ref(0)
const thresholdInput = ref<string | number>('')
const thresholdSaving = ref(false)
const thresholdError = ref<string | null>(null)
const thresholdSuccess = ref<string | null>(null)

const suspensionLabel = computed(() => {
  const reason = accountInfo.value?.suspendedReason ?? null
  if (!reason) return null
  if (reason === 'MISSING_BANK_ACCOUNT') {
    return t('buildingBankAccount.missingAccountAdvisory')
  }
  if (reason.startsWith('INSUFFICIENT_FUNDS:')) {
    const amount = reason.split(':')[1] ?? '0'
    const formatted = formatCurrency(parseFloat(amount))
    return t('buildingBankAccount.insufficientFunds', { amount: formatted, currency: props.currencyCode })
  }
  return reason
})

const isSuspended = computed(() => accountInfo.value?.isSuspendedForFunds === true)
const hasMissingAccount = computed(() => accountInfo.value?.suspendedReason === 'MISSING_BANK_ACCOUNT')
const hasInsufficientFunds = computed(() => accountInfo.value?.suspendedReason?.startsWith('INSUFFICIENT_FUNDS:') === true)

function formatCurrency(value: number): string {
  try {
    return new Intl.NumberFormat(locale.value, {
      style: 'currency',
      currency: props.currencyCode ?? 'EUR',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value)
  } catch {
    return `${value.toFixed(2)} ${props.currencyCode}`
  }
}

async function fetchAccountInfo() {
  if (!props.buildingId) return
  const v = ++accountInfoVersion.value
  accountLoading.value = true
  try {
    const result = await gqlRequest<{ buildingBankAccount: BuildingBankAccountInfo | null }>(
      `query BuildingBankAccount($buildingId: UUID!) {
        buildingBankAccount(buildingId: $buildingId) {
          buildingId
          buildingName
          cityName
          currencyCode
          hasBankAccount
          bankAccountId
          accountNumber
          balance
          alertMinBalanceThreshold
          isSuspendedForFunds
          suspendedReason
        }
      }`,
      { buildingId: props.buildingId },
    )
    // Only apply the response if no fresher data was written while this was in-flight.
    if (v === accountInfoVersion.value) {
      accountInfo.value = result.buildingBankAccount
    }
  } catch {
    if (v === accountInfoVersion.value) {
      accountInfo.value = null
    }
  } finally {
    if (v === accountInfoVersion.value) {
      accountLoading.value = false
    }
  }
}

async function fetchCompanyAccounts() {
  if (!props.showAssignmentControls) {
    companyAccounts.value = []
    return
  }

  if (!props.companyId) {
    companyAccounts.value = []
    return
  }

  accountsLoading.value = true
  try {
    const result = await gqlRequest<{ companyBankAccounts: CompanyBankAccountSummary[] }>(
      `query CompanyBankAccounts($companyId: UUID!) {
        companyBankAccounts(companyId: $companyId) {
          id
          accountNumber
          currencyCode
          balance
          alertMinBalanceThreshold
        }
      }`,
      { companyId: props.companyId },
    )
    companyAccounts.value = result.companyBankAccounts ?? []
  } catch {
    companyAccounts.value = []
  } finally {
    accountsLoading.value = false
  }
}

async function refreshPanel() {
  await Promise.all([fetchAccountInfo(), fetchCompanyAccounts()])
}

async function onChildUpdated() {
  await refreshPanel()
  emit('updated')
}

async function saveLowBalanceThreshold() {
  if (!accountInfo.value?.bankAccountId) {
    return
  }

  const rawThreshold = thresholdInput.value
  const normalized = typeof rawThreshold === 'number' ? String(rawThreshold) : rawThreshold.trim()
  const threshold = normalized.length === 0 ? null : Number(normalized)
  if (threshold !== null && (!Number.isFinite(threshold) || threshold < 0)) {
    thresholdError.value = t('buildingBankAccount.thresholdInvalid')
    thresholdSuccess.value = null
    return
  }

  thresholdSaving.value = true
  thresholdError.value = null
  thresholdSuccess.value = null

  try {
    const result = await gqlRequest<{ setBankAccountAlertThreshold: { bankAccountId: string; alertMinBalanceThreshold: number | null } }>(
      `mutation SetBankAccountAlertThreshold($input: SetBankAccountAlertThresholdInput!) {
        setBankAccountAlertThreshold(input: $input) {
          bankAccountId
          alertMinBalanceThreshold
        }
      }`,
      {
        input: {
          bankAccountId: accountInfo.value.bankAccountId,
          minBalanceThreshold: threshold,
        },
      },
    )

    const updatedThreshold = result.setBankAccountAlertThreshold.alertMinBalanceThreshold
    accountInfo.value = {
      ...accountInfo.value,
      alertMinBalanceThreshold: updatedThreshold,
    }
    thresholdInput.value = updatedThreshold === null ? '' : String(updatedThreshold)
    thresholdSuccess.value = t('buildingBankAccount.thresholdSaved')
  } catch (error: unknown) {
    thresholdError.value = error instanceof Error ? error.message : t('common.unknownError')
  } finally {
    thresholdSaving.value = false
  }
}

watch(
  () => [props.buildingId, props.companyId, props.currencyCode],
  () => {
    void refreshPanel()
  },
  { immediate: true },
)

watch(
  () => accountInfo.value?.alertMinBalanceThreshold,
  (value) => {
    thresholdInput.value = value === null || value === undefined ? '' : String(value)
  },
  { immediate: true },
)
</script>

<template>
  <div class="building-bank-account-panel" :class="{ loading: accountLoading || props.loading }">
    <div v-if="accountLoading" class="bba-skeleton">
      <div class="bba-skeleton-row"></div>
      <div class="bba-skeleton-row bba-skeleton-row-sm"></div>
    </div>

    <template v-else-if="accountInfo">
      <div v-if="isSuspended || hasMissingAccount" class="bba-alert" :class="{ 'bba-alert-warning': hasMissingAccount, 'bba-alert-danger': hasInsufficientFunds }" role="alert" aria-live="polite">
        <span class="bba-alert-icon">{{ hasInsufficientFunds ? '⚠️' : '💡' }}</span>
        <span class="bba-alert-message">{{ suspensionLabel }}</span>
      </div>

      <div class="bba-info-row">
        <span v-if="accountInfo.hasBankAccount" class="bba-account-number">
          {{ t('buildingBankAccount.accountLabel') }}
          <code>{{ accountInfo.accountNumber }}</code>
        </span>
        <span v-else class="bba-no-account">
          {{ t('buildingBankAccount.noAccountAssigned') }}
        </span>

        <span v-if="accountInfo.hasBankAccount" class="bba-balance" :class="{ 'bba-balance-low': (accountInfo.balance ?? 0) < 100 }">
          {{ formatCurrency(accountInfo.balance ?? 0) }}
        </span>
      </div>

      <div v-if="accountInfo.hasBankAccount" class="bba-threshold-panel">
        <label class="bba-manage-label" :for="`bba-threshold-${props.buildingId}`">{{ t('buildingBankAccount.thresholdLabel', { currency: accountInfo.currencyCode }) }}</label>
        <div class="bba-threshold-controls">
          <input
            :id="`bba-threshold-${props.buildingId}`"
            v-model="thresholdInput"
            type="number"
            min="0"
            step="0.01"
            class="bba-threshold-input"
            :placeholder="t('buildingBankAccount.thresholdPlaceholder')"
          />
          <button class="btn btn-secondary btn-sm" :disabled="thresholdSaving" @click="saveLowBalanceThreshold">
            {{ thresholdSaving ? t('common.loading') : t('buildingBankAccount.thresholdSave') }}
          </button>
        </div>
        <p class="bba-threshold-hint">{{ t('buildingBankAccount.thresholdHint') }}</p>
        <p v-if="thresholdError" class="bba-manage-error" role="alert">{{ thresholdError }}</p>
        <p v-if="thresholdSuccess" class="bba-manage-success" role="status">{{ thresholdSuccess }}</p>
      </div>

      <BuildingBankTransferForm
        :building-id="props.buildingId"
        :company-id="props.companyId"
        :currency-code="props.currencyCode"
        :show-assignment-controls="props.showAssignmentControls ?? true"
        :account-info="accountInfo"
        :company-accounts="companyAccounts"
        :accounts-loading="accountsLoading"
        @updated="onChildUpdated"
      />
    </template>
  </div>
</template>

<style scoped>
.building-bank-account-panel {
  background: var(--color-surface-2, #1e2330);
  border: 1px solid var(--color-border, #2d3447);
  border-radius: 8px;
  padding: 12px 16px;
  margin-bottom: 12px;
  transition: opacity 0.2s;
}

.building-bank-account-panel.loading {
  opacity: 0.6;
}

.bba-skeleton {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.bba-skeleton-row {
  height: 18px;
  border-radius: 4px;
  background: var(--color-surface-3, #2a3040);
  animation: pulse 1.5s infinite;
  width: 80%;
}

.bba-skeleton-row-sm {
  width: 50%;
  height: 14px;
}

@keyframes pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}

.bba-alert {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 6px;
  margin-bottom: 10px;
  font-size: 0.875rem;
  line-height: 1.4;
}

.bba-alert-warning {
  background: rgba(251, 191, 36, 0.12);
  border: 1px solid rgba(251, 191, 36, 0.4);
  color: #fbbf24;
}

.bba-alert-danger {
  background: rgba(239, 68, 68, 0.12);
  border: 1px solid rgba(239, 68, 68, 0.4);
  color: #ef4444;
}

.bba-alert-icon {
  flex-shrink: 0;
}

.bba-alert-message {
  flex: 1;
}

.bba-info-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.bba-account-number {
  font-size: 0.8rem;
  color: var(--color-text-muted, #8b95a8);
}

.bba-account-number code {
  font-family: monospace;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary, #c8d0e0);
  background: var(--color-surface-3, #2a3040);
  padding: 2px 6px;
  border-radius: 3px;
}

.bba-no-account {
  font-size: 0.875rem;
  color: var(--color-text-muted, #8b95a8);
  font-style: italic;
}

.bba-balance {
  font-size: 1rem;
  font-weight: 600;
  color: #34d399;
}

.bba-balance.bba-balance-low {
  color: #f59e0b;
}

.bba-threshold-panel {
  margin-top: 12px;
  display: grid;
  gap: 6px;
}

.bba-threshold-controls {
  display: flex;
  gap: 8px;
  align-items: center;
}

.bba-threshold-input {
  width: 100%;
  max-width: 220px;
  background: var(--color-surface-3, #2a3040);
  border: 1px solid var(--color-border, #2d3447);
  border-radius: 6px;
  padding: 7px 9px;
  color: var(--color-text-secondary, #c8d0e0);
}

.bba-threshold-hint {
  margin: 0;
  font-size: 0.75rem;
  color: var(--color-text-muted, #8b95a8);
}

.bba-manage-label {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--color-text-muted, #8b95a8);
  display: block;
  margin-bottom: 6px;
}

.bba-manage-error,
.bba-manage-success {
  margin: 8px 0 0;
  font-size: 0.8125rem;
}

.bba-manage-error {
  color: #ef4444;
}

.bba-manage-success {
  color: #34d399;
}
</style>
