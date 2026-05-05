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
  showAssignmentControls: boolean
  accountInfo: BuildingBankAccountInfo | null
  companyAccounts: CompanyBankAccountSummary[]
  accountsLoading: boolean
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'updated'): void
}>()

const { t, locale } = useI18n()

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

const availableCompanyAccounts = computed(() => {
  const currencyCode = (props.currencyCode ?? props.accountInfo?.currencyCode ?? 'EUR').toUpperCase()
  return props.companyAccounts.filter((account) => account.currencyCode.toUpperCase() === currencyCode)
})
const canAssignSelectedAccount = computed(() => Boolean(selectedBankAccountId.value) && selectedBankAccountId.value !== props.accountInfo?.bankAccountId)
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

function syncSelectedAccount() {
  const assignedBankAccountId = props.accountInfo?.bankAccountId
  if (assignedBankAccountId && availableCompanyAccounts.value.some((account) => account.id === assignedBankAccountId)) {
    selectedBankAccountId.value = assignedBankAccountId
    return
  }
  selectedBankAccountId.value = availableCompanyAccounts.value[0]?.id ?? null
}

watch(
  () => [props.accountInfo, props.companyAccounts] as const,
  () => syncSelectedAccount(),
  { immediate: true },
)

async function assignBankAccount(bankAccountId: string, successMessage: string) {
  assignmentLoading.value = true
  assignmentError.value = null
  assignmentSuccess.value = null
  fundError.value = null
  fundSuccess.value = null
  try {
    await gqlRequest<{ assignBuildingBankAccount: { bankAccount: BuildingBankAccountInfo } }>(
      `mutation AssignBuildingBankAccount($input: AssignBuildingBankAccountInput!) {
        assignBuildingBankAccount(input: $input) {
          bankAccount {
            buildingId
          }
        }
      }`,
      { input: { buildingId: props.buildingId, bankAccountId } },
    )
    assignmentSuccess.value = successMessage
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
            alertMinBalanceThreshold
          }
        }
      }`,
      { input: { companyId: props.companyId, currencyCode: props.currencyCode } },
    )

    const createdAccount = result.createCompanyBankAccount.account
    selectedBankAccountId.value = createdAccount.id
    await assignBankAccount(createdAccount.id, t('buildingBankAccount.createSuccess'))
  } catch (error: unknown) {
    assignmentError.value = error instanceof Error ? error.message : t('common.unknownError')
  } finally {
    createLoading.value = false
  }
}

async function fundBuildingAccount() {
  if (!props.accountInfo?.hasBankAccount) {
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
    await gqlRequest<{ fundBuildingBankAccount: FundBuildingBankAccountResult }>(
      `mutation FundBuildingBankAccount($input: FundBuildingBankAccountInput!) {
        fundBuildingBankAccount(input: $input) {
          bankAccount {
            buildingId
          }
          remainingCompanyCash
        }
      }`,
      { input: { buildingId: props.buildingId, amount } },
    )
    isFundPanelOpen.value = true
    fundAmount.value = ''
    fundSuccess.value = t('buildingBankAccount.fundSuccess')
    emit('updated')
  } catch (error: unknown) {
    fundError.value = error instanceof Error ? error.message : t('common.unknownError')
  } finally {
    fundLoading.value = false
  }
}
</script>

<template>
  <div class="bba-transfer-form">
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

    <div v-if="props.accountInfo && (props.accountInfo.isSuspendedForFunds || props.accountInfo.suspendedReason === 'MISSING_BANK_ACCOUNT')" class="bba-guidance">
      <span class="bba-guidance-label">{{ t('buildingBankAccount.guidance') }}</span>
      <router-link to="/forex" class="bba-guidance-link">
        {{ t('buildingBankAccount.guidanceForex') }}
      </router-link>
      <router-link to="/bank-management" class="bba-guidance-link">
        {{ t('buildingBankAccount.guidanceBank') }}
      </router-link>
    </div>

    <details v-if="props.accountInfo?.hasBankAccount" class="bba-fund-panel" :open="isFundPanelOpen" @toggle="isFundPanelOpen = ($event.target as HTMLDetailsElement).open">
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
  </div>
</template>

<style scoped>
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
