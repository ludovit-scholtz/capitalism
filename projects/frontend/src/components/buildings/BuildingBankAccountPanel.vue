<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { BuildingBankAccountInfo } from '@/types'

interface Props {
  buildingId: string
  companyId: string
  /** ISO 4217 currency code for the building's city */
  currencyCode: string
  /** Loading state while the parent fetches building data */
  loading: boolean
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'funded'): void
}>()

const { t, locale } = useI18n()

// ── Bank account data ──
const accountInfo = ref<BuildingBankAccountInfo | null>(null)
const accountLoading = ref(false)
const fundAmount = ref<number | null>(null)
const fundLoading = ref(false)
const fundError = ref<string | null>(null)
const fundSuccess = ref(false)

// ── Computed ──
const currencySymbol = computed(() => {
  const code = props.currencyCode ?? accountInfo.value?.currencyCode ?? 'EUR'
  try {
    return new Intl.NumberFormat(locale.value, { style: 'currency', currency: code, maximumFractionDigits: 0 })
      .format(0)
      .replace(/[\d,.]/g, '')
      .trim()
  } catch {
    return code
  }
})

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
const hasInsufficientFunds = computed(
  () => accountInfo.value?.suspendedReason?.startsWith('INSUFFICIENT_FUNDS:') === true,
)

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

// ── Data fetching ──
async function fetchAccountInfo() {
  if (!props.buildingId) return
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
    accountInfo.value = result.buildingBankAccount
  } finally {
    accountLoading.value = false
  }
}

// ── Actions ──
async function fundAccount() {
  if (!fundAmount.value || fundAmount.value <= 0) return
  fundLoading.value = true
  fundError.value = null
  fundSuccess.value = false
  try {
    await gqlRequest(
      `mutation FundBuildingBankAccount($input: FundBuildingBankAccountInput!) {
        fundBuildingBankAccount(input: $input) {
          bankAccount {
            buildingId
            balance
            isSuspendedForFunds
            suspendedReason
          }
          remainingCompanyCash
        }
      }`,
      { input: { buildingId: props.buildingId, amount: fundAmount.value } },
    )
    fundSuccess.value = true
    fundAmount.value = null
    await fetchAccountInfo()
    emit('funded')
  } catch (e: unknown) {
    fundError.value = e instanceof Error ? e.message : t('common.unknownError')
  } finally {
    fundLoading.value = false
  }
}

// Load on mount
fetchAccountInfo()
</script>

<template>
  <div class="building-bank-account-panel" :class="{ loading: accountLoading || props.loading }">
    <!-- Loading skeleton -->
    <div v-if="accountLoading" class="bba-skeleton">
      <div class="bba-skeleton-row"></div>
      <div class="bba-skeleton-row bba-skeleton-row-sm"></div>
    </div>

    <template v-else-if="accountInfo">
      <!-- Suspension/warning banner -->
      <div
        v-if="isSuspended || hasMissingAccount"
        class="bba-alert"
        :class="{ 'bba-alert-warning': hasMissingAccount, 'bba-alert-danger': hasInsufficientFunds }"
        role="alert"
        aria-live="polite"
      >
        <span class="bba-alert-icon">{{ hasInsufficientFunds ? '⚠️' : '💡' }}</span>
        <span class="bba-alert-message">{{ suspensionLabel }}</span>
      </div>

      <!-- Account info row -->
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

      <!-- Fund transfer panel -->
      <details class="bba-fund-panel">
        <summary class="bba-fund-summary">
          {{ t('buildingBankAccount.fundAccount') }}
        </summary>
        <div class="bba-fund-body">
          <p class="bba-fund-hint">
            {{ t('buildingBankAccount.fundHint') }}
          </p>
          <div class="bba-fund-form">
            <input
              v-model.number="fundAmount"
              type="number"
              min="1"
              step="1000"
              class="bba-fund-input"
              :placeholder="t('buildingBankAccount.amountPlaceholder', { symbol: currencySymbol })"
              :disabled="fundLoading"
            />
            <button
              class="btn btn-primary btn-sm"
              :disabled="!fundAmount || fundAmount <= 0 || fundLoading"
              @click="fundAccount"
            >
              {{ fundLoading ? t('common.loading') : t('buildingBankAccount.transferBtn') }}
            </button>
          </div>
          <p v-if="fundError" class="bba-fund-error" role="alert">{{ fundError }}</p>
        </div>
      </details>

      <!-- Fund feedback — rendered outside <details> so it stays visible after panel re-renders -->
      <p v-if="fundSuccess" class="bba-fund-success" role="status">
        {{ t('buildingBankAccount.fundSuccess') }}
      </p>

      <!-- Guidance links -->
      <div v-if="isSuspended || hasMissingAccount" class="bba-guidance">
        <span class="bba-guidance-label">{{ t('buildingBankAccount.guidance') }}</span>
        <router-link to="/forex" class="bba-guidance-link">
          {{ t('buildingBankAccount.guidanceForex') }}
        </router-link>
        <router-link to="/bank-management" class="bba-guidance-link">
          {{ t('buildingBankAccount.guidanceBank') }}
        </router-link>
      </div>
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

/* Skeleton */
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
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

/* Alerts */
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

/* Info row */
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

/* Fund panel */
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

.bba-fund-input:focus {
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

/* Guidance */
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
