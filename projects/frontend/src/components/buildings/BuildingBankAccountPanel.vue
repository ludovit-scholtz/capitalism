<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { BuildingBankAccountInfo, CompanyBankAccountSummary, FundBuildingBankAccountResult } from '@/types'

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
/** Monotonic counter used to discard stale fetchAccountInfo() responses when a
 * direct mutation result (fund / assign) has already provided fresher data. */
const accountInfoVersion = ref(0)
const selectedBankAccountId = ref<string | null>(null)
const assignmentLoading = ref(false)
const createLoading = ref(false)
const assignmentError = ref<string | null>(null)
const assignmentSuccess = ref<string | null>(null)
const isFundPanelOpen = ref(false)
const fundAmount = ref<string | number>('')
const fundLoading = ref(false)
const fundError = ref<string | null>(null)
const fundSuccess = ref<string | null>(null)

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
const availableCompanyAccounts = computed(() => {
  const currencyCode = (props.currencyCode ?? accountInfo.value?.currencyCode ?? 'EUR').toUpperCase()
  return companyAccounts.value.filter((account) => account.currencyCode.toUpperCase() === currencyCode)
})
const canAssignSelectedAccount = computed(() => Boolean(selectedBankAccountId.value) && selectedBankAccountId.value !== accountInfo.value?.bankAccountId)
const canCreateCompanyAccount = computed(() => availableCompanyAccounts.value.length === 0)
const accountSelectId = computed(() => `building-bank-account-select-${props.buildingId}`)

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

function formatAccountOption(account: CompanyBankAccountSummary): string {
  return `${account.accountNumber} - ${formatCurrency(account.balance)}`
}

function buildAssignedAccountInfo(bankAccountId: string): BuildingBankAccountInfo | null {
  const companyAccount = companyAccounts.value.find((account) => account.id === bankAccountId)
  if (!companyAccount) {
    return null
  }

  return {
    buildingId: props.buildingId,
    buildingName: accountInfo.value?.buildingName ?? '',
    cityName: accountInfo.value?.cityName ?? '',
    currencyCode: companyAccount.currencyCode,
    hasBankAccount: true,
    bankAccountId: companyAccount.id,
    accountNumber: companyAccount.accountNumber,
    balance: companyAccount.balance,
    isSuspendedForFunds: false,
    suspendedReason: null,
  }
}

function syncSelectedAccount() {
  const assignedBankAccountId = accountInfo.value?.bankAccountId
  if (assignedBankAccountId && availableCompanyAccounts.value.some((account) => account.id === assignedBankAccountId)) {
    selectedBankAccountId.value = assignedBankAccountId
    return
  }

  selectedBankAccountId.value = availableCompanyAccounts.value[0]?.id ?? null
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
    selectedBankAccountId.value = null
    return
  }

  if (!props.companyId) {
    companyAccounts.value = []
    selectedBankAccountId.value = null
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
  syncSelectedAccount()
}

async function assignBankAccount(bankAccountId: string, successMessage: string) {
  assignmentLoading.value = true
  assignmentError.value = null
  assignmentSuccess.value = null
  fundError.value = null
  fundSuccess.value = null
  try {
    const result = await gqlRequest<{ assignBuildingBankAccount: { bankAccount: BuildingBankAccountInfo } }>(
      `mutation AssignBuildingBankAccount($input: AssignBuildingBankAccountInput!) {
        assignBuildingBankAccount(input: $input) {
          bankAccount {
            buildingId
            buildingName
            cityName
            currencyCode
            hasBankAccount
            bankAccountId
            accountNumber
            balance
            isSuspendedForFunds
            suspendedReason
          }
        }
      }`,
      { input: { buildingId: props.buildingId, bankAccountId } },
    )
    const nextAccountInfo = result.assignBuildingBankAccount?.bankAccount ?? buildAssignedAccountInfo(bankAccountId)
    if (!nextAccountInfo) {
      throw new Error(t('common.unknownError'))
    }

    // Bump version so any in-flight fetchAccountInfo() with stale data is discarded.
    ++accountInfoVersion.value
    accountLoading.value = false
    accountInfo.value = nextAccountInfo
    assignmentSuccess.value = successMessage
    await fetchCompanyAccounts()
    syncSelectedAccount()
    emit('updated')
  } catch (error: unknown) {
    assignmentError.value = error instanceof Error ? error.message : t('common.unknownError')
  } finally {
    assignmentLoading.value = false
  }
}

async function assignSelectedAccount() {
  if (!selectedBankAccountId.value || !canAssignSelectedAccount.value) {
    return
  }

  await assignBankAccount(selectedBankAccountId.value, t('buildingBankAccount.assignSuccess'))
}

async function createAndAssignCompanyAccount() {
  if (!props.companyId || !props.currencyCode) {
    return
  }

  createLoading.value = true
  assignmentError.value = null
  assignmentSuccess.value = null
  fundError.value = null
  fundSuccess.value = null
  try {
    const result = await gqlRequest<{ createCompanyBankAccount: { account: CompanyBankAccountSummary } }>(
      `mutation CreateCompanyBankAccount($input: CreateCompanyBankAccountInput!) {
        createCompanyBankAccount(input: $input) {
          account {
            id
            accountNumber
            currencyCode
            balance
          }
        }
      }`,
      { input: { companyId: props.companyId, currencyCode: props.currencyCode } },
    )

    const createdAccount = result.createCompanyBankAccount.account
    companyAccounts.value = [...companyAccounts.value, createdAccount]
    selectedBankAccountId.value = createdAccount.id
    await assignBankAccount(createdAccount.id, t('buildingBankAccount.createSuccess'))
  } catch (error: unknown) {
    assignmentError.value = error instanceof Error ? error.message : t('common.unknownError')
  } finally {
    createLoading.value = false
  }
}

async function fundBuildingAccount() {
  if (!accountInfo.value?.hasBankAccount) {
    return
  }

  const rawAmount = fundAmount.value
  const normalizedAmount = typeof rawAmount === 'number' ? String(rawAmount) : rawAmount.trim()
  if (!/^\d+(?:\.\d{1,2})?$/.test(normalizedAmount)) {
    fundError.value = t('buildingBankAccount.fundInvalidAmount')
    fundSuccess.value = null
    return
  }

  const amount = Number(normalizedAmount)
  if (!Number.isFinite(amount) || amount <= 0) {
    fundError.value = t('buildingBankAccount.fundInvalidAmount')
    fundSuccess.value = null
    return
  }

  fundLoading.value = true
  fundError.value = null
  fundSuccess.value = null
  assignmentError.value = null
  assignmentSuccess.value = null
  try {
    const result = await gqlRequest<{ fundBuildingBankAccount: FundBuildingBankAccountResult }>(
      `mutation FundBuildingBankAccount($input: FundBuildingBankAccountInput!) {
        fundBuildingBankAccount(input: $input) {
          bankAccount {
            buildingId
            buildingName
            cityName
            currencyCode
            hasBankAccount
            bankAccountId
            accountNumber
            balance
            isSuspendedForFunds
            suspendedReason
          }
          remainingCompanyCash
        }
      }`,
      { input: { buildingId: props.buildingId, amount } },
    )

    const fundedAccount = result.fundBuildingBankAccount?.bankAccount
    if (fundedAccount) {
      // Bump version so any in-flight fetchAccountInfo() with stale data is discarded.
      ++accountInfoVersion.value
      accountLoading.value = false
      accountInfo.value = fundedAccount
    } else {
      await fetchAccountInfo()
    }
    isFundPanelOpen.value = true
    fundAmount.value = ''
    fundSuccess.value = t('buildingBankAccount.fundSuccess')
  } catch (error: unknown) {
    fundError.value = error instanceof Error ? error.message : t('common.unknownError')
  } finally {
    fundLoading.value = false
  }
}

watch(
  () => [props.buildingId, props.companyId, props.currencyCode],
  () => {
    void refreshPanel()
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

      <div v-if="props.showAssignmentControls" class="bba-manage-panel">
        <p class="bba-manage-title">{{ t('buildingBankAccount.assignmentTitle') }}</p>
        <p class="bba-manage-hint">{{ t('buildingBankAccount.assignmentHint', { currency: props.currencyCode }) }}</p>

        <div v-if="availableCompanyAccounts.length > 0" class="bba-assign-form">
          <label class="bba-manage-label" :for="accountSelectId">{{ t('buildingBankAccount.accountSelectLabel') }}</label>
          <div class="bba-assign-controls">
            <select :id="accountSelectId" v-model="selectedBankAccountId" class="bba-account-select" :disabled="accountsLoading || assignmentLoading || createLoading">
              <option v-for="account in availableCompanyAccounts" :key="account.id" :value="account.id">
                {{ formatAccountOption(account) }}
              </option>
            </select>
            <button class="btn btn-secondary btn-sm" :disabled="!canAssignSelectedAccount || assignmentLoading || createLoading" @click="assignSelectedAccount">
              {{ assignmentLoading ? t('common.loading') : t('buildingBankAccount.assignBtn') }}
            </button>
          </div>
        </div>
        <p v-else class="bba-manage-empty">{{ t('buildingBankAccount.noCompanyAccountAvailable', { currency: props.currencyCode }) }}</p>

        <button v-if="canCreateCompanyAccount" class="btn btn-secondary btn-sm bba-create-btn" :disabled="createLoading || assignmentLoading" @click="createAndAssignCompanyAccount">
          {{ createLoading ? t('common.loading') : t('buildingBankAccount.createBtn', { currency: props.currencyCode }) }}
        </button>

        <p v-if="assignmentError" class="bba-manage-error" role="alert">{{ assignmentError }}</p>
        <p v-if="assignmentSuccess" class="bba-manage-success" role="status">{{ assignmentSuccess }}</p>
      </div>
      <div v-if="isSuspended || hasMissingAccount" class="bba-guidance">
        <span class="bba-guidance-label">{{ t('buildingBankAccount.guidance') }}</span>
        <router-link to="/forex" class="bba-guidance-link">
          {{ t('buildingBankAccount.guidanceForex') }}
        </router-link>
        <router-link to="/bank-management" class="bba-guidance-link">
          {{ t('buildingBankAccount.guidanceBank') }}
        </router-link>
      </div>

      <details v-if="accountInfo.hasBankAccount" class="bba-fund-panel" :open="isFundPanelOpen" @toggle="isFundPanelOpen = ($event.target as HTMLDetailsElement).open">
        <summary class="bba-fund-summary">{{ t('buildingBankAccount.fundTitle') }}</summary>
        <div class="bba-fund-body">
          <p class="bba-fund-hint">{{ t('buildingBankAccount.fundHint', { currency: props.currencyCode }) }}</p>
          <form class="bba-fund-form" @submit.prevent="fundBuildingAccount">
            <input
              v-model="fundAmount"
              type="number"
              min="0"
              step="0.01"
              class="bba-fund-input"
              :placeholder="t('buildingBankAccount.fundAmountPlaceholder')"
              :aria-label="t('buildingBankAccount.fundAmountLabel')"
            />
            <button type="submit" class="btn btn-secondary btn-sm" :disabled="fundLoading">
              {{ fundLoading ? t('common.loading') : t('buildingBankAccount.fundSubmit') }}
            </button>
          </form>
          <p v-if="fundError" class="bba-fund-error" role="alert">{{ fundError }}</p>
          <p v-if="fundSuccess" class="bba-fund-success" role="status">{{ fundSuccess }}</p>
        </div>
      </details>
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

.bba-manage-panel {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--color-border, #2d3447);
}

.bba-manage-title {
  margin: 0 0 4px;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-primary, #f3f5f8);
}

.bba-manage-hint,
.bba-manage-empty,
.bba-manage-label {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--color-text-muted, #8b95a8);
}

.bba-manage-label {
  display: block;
  margin-bottom: 6px;
}

.bba-assign-form {
  margin-top: 10px;
}

.bba-assign-controls {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.bba-account-select {
  flex: 1 1 240px;
  min-height: 36px;
  border: 1px solid var(--color-border, #2d3447);
  border-radius: 6px;
  background: var(--color-surface-3, #2a3040);
  color: var(--color-text-primary, #f3f5f8);
  padding: 0 10px;
}

.bba-create-btn {
  margin-top: 10px;
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

.bba-fund-panel {
  margin-top: 10px;
}

.bba-fund-summary {
  cursor: pointer;
  font-size: 0.875rem;
  color: var(--color-accent, #6366f1);
  user-select: none;
  padding: 4px 0;
}

.bba-fund-summary:hover {
  color: var(--color-accent-hover, #818cf8);
}

.bba-fund-body {
  padding: 10px 0 0;
}

.bba-fund-hint {
  font-size: 0.8rem;
  color: var(--color-text-muted, #8b95a8);
  margin-bottom: 8px;
}

.bba-fund-form {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.bba-fund-input {
  flex: 1;
  min-width: 140px;
  padding: 6px 10px;
  border: 1px solid var(--color-border, #2d3447);
  border-radius: 4px;
  background: var(--color-surface-1, #141824);
  color: var(--color-text-primary, #e2e8f0);
  font-size: 0.9rem;
}

.bba-fund-input:focus,
.bba-account-select:focus {
  outline: 2px solid var(--color-accent, #6366f1);
  outline-offset: -2px;
}

.bba-fund-error {
  font-size: 0.8rem;
  color: #ef4444;
  margin-top: 6px;
}

.bba-fund-success {
  font-size: 0.8rem;
  color: #34d399;
  margin-top: 6px;
}

.bba-guidance {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  margin-top: 10px;
  padding-top: 8px;
  border-top: 1px solid var(--color-border, #2d3447);
  font-size: 0.8rem;
  color: var(--color-text-muted, #8b95a8);
}

.bba-guidance-link {
  color: var(--color-accent, #6366f1);
  text-decoration: none;
  font-size: 0.8rem;
}

.bba-guidance-link:hover {
  text-decoration: underline;
}
</style>
