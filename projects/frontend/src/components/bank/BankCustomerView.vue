<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import UiStateEmpty from '@/components/ui/UiStateEmpty.vue'
import { formatCurrency, formatPercent, formatLoanDuration, loanStatusClass } from '@/lib/loanHelpers'
import type { BankInfoSummary, BankDepositSummary, LoanSummary, LoanOfferSummary, Company, PlayerBankAccountSummary } from '@/types'

const props = defineProps<{
  bankInfo: BankInfoSummary | null
  bankBuildingId: string
  isAuthenticated: boolean
  activeCompany: Company | null
  isCompanyAccountActive: boolean
  isPersonalAccountActive: boolean
  myOperatingAccountsHere: PlayerBankAccountSummary[]
  myLoansHere: LoanSummary[]
  myDepositsHere: BankDepositSummary[]
  currentTick: number
}>()

const emit = defineEmits<{
  'data-changed': []
}>()

const { t } = useI18n()
const router = useRouter()

const showWithdrawForm = ref(false)
const withdrawAmount = ref(0)
const withdrawLoading = ref(false)
const withdrawError = ref<string | null>(null)
const withdrawSuccess = ref(false)

const customerDepositLoading = ref(false)
const customerDepositError = ref<string | null>(null)
const customerDepositSuccess = ref(false)
const repayDebtLoadingByLoanId = ref<Record<string, boolean>>({})
const repayDebtErrorByLoanId = ref<Record<string, string | null>>({})
const repayDebtSuccessByLoanId = ref<Record<string, boolean>>({})

const fmt = computed(() => (amount: number) => formatCurrency(amount, props.bankInfo?.cityCurrencyCode ?? 'EUR'))

const directBorrowingOption = computed<LoanOfferSummary | null>(() => {
  const info = props.bankInfo
  if (!info?.baseCapitalDeposited) return null
  return {
    id: info.bankBuildingId,
    bankBuildingId: info.bankBuildingId,
    bankBuildingName: info.bankBuildingName,
    cityId: info.cityId,
    cityName: info.cityName,
    lenderCompanyId: info.lenderCompanyId,
    lenderCompanyName: info.lenderCompanyName,
    annualInterestRatePercent: info.lendingInterestRatePercent,
    maxPrincipalPerLoan: Math.max(info.availableLendingCapacity, 0),
    totalCapacity: info.lendableCapacity,
    usedCapacity: info.outstandingLoanPrincipal,
    remainingCapacity: Math.max(info.availableLendingCapacity, 0),
    durationTicks: 8760,
    isActive: info.availableLendingCapacity > 0,
    createdAtTick: 0,
    createdAtUtc: '',
  }
})

const myActiveDepositsHere = computed(() => props.myDepositsHere.filter((d) => d.isActive && !d.isBaseCapital))
const hasCustomerAccount = computed(() => myActiveDepositsHere.value.length > 0)
const isEligibleDepositContext = computed(() => props.isCompanyAccountActive || props.isPersonalAccountActive)
const myAccountBalance = computed(() => myActiveDepositsHere.value.reduce((sum, d) => sum + d.amount, 0))
const myAccountInterestEarned = computed(() => myActiveDepositsHere.value.reduce((sum, d) => sum + d.totalInterestPaid, 0))
const myestDeposit = computed<BankDepositSummary | null>(() => {
  const sorted = [...myActiveDepositsHere.value].sort((a, b) => a.depositedAtTick - b.depositedAtTick)
  return sorted[0] ?? null
})

const FORECLOSURE_WINDOW_TICKS = 72
const TICKS_PER_DAY = 24
const REAL_MINUTES_PER_TICK = 10

const overdueDebtLoans = computed(() =>
  props.myLoansHere.filter((loan) => loan.status === 'OVERDUE' || loan.status === 'DEFAULTED'),
)

const totalOverdueDebtAmount = computed(() =>
  overdueDebtLoans.value.reduce((sum, loan) => sum + loan.remainingPrincipal, 0),
)

function getForeclosureTicksRemaining(loan: LoanSummary): number | null {
  if (loan.defaultedAtTick === null || loan.defaultedAtTick === undefined) return null
  return Math.max(0, loan.defaultedAtTick + FORECLOSURE_WINDOW_TICKS - props.currentTick)
}

function formatForeclosureCountdown(loan: LoanSummary): string {
  const ticksRemaining = getForeclosureTicksRemaining(loan)
  if (ticksRemaining === null) return t('bank.overdueCountdownUnknown')

  const days = Math.floor(ticksRemaining / TICKS_PER_DAY)
  const hours = ticksRemaining % TICKS_PER_DAY
  const realMinutes = ticksRemaining * REAL_MINUTES_PER_TICK
  return t('bank.overdueCountdownValue', { days, hours, realMinutes })
}

const REPAY_LOAN_DEBT_MUTATION = `
  mutation RepayLoanDebt($input: RepayLoanDebtInput!) {
    repayLoanDebt(input: $input) {
      id
      status
      remainingPrincipal
      missedPayments
      defaultedAtTick
      closedAtUtc
    }
  }
`

const CREATE_DEPOSIT_MUTATION = `
  mutation OpenBankAccount($input: OpenBankAccountInput!) {
    openBankAccount(input: $input) {
      id
      amount
      depositInterestRatePercent
      isActive
    }
  }
`

const WITHDRAW_DEPOSIT_MUTATION = `
  mutation CloseBankAccount($input: CloseBankAccountInput!) {
    closeBankAccount(input: $input) {
      id
      amount
      isActive
    }
  }
`

function formatOpenAccountError(errorMessage: string) {
  if (!errorMessage.includes('Insufficient company funds to open this bank account.')) {
    return errorMessage
  }
  return `${errorMessage} ${t('bank.zeroBalanceFundingHint')}`
}

async function submitCustomerDeposit() {
  if (!props.bankBuildingId || !isEligibleDepositContext.value) return
  customerDepositLoading.value = true
  customerDepositError.value = null
  customerDepositSuccess.value = false
  try {
    await gqlRequest(CREATE_DEPOSIT_MUTATION, {
      input: {
        bankBuildingId: props.bankBuildingId,
        depositorCompanyId: props.isCompanyAccountActive ? props.activeCompany?.id : null,
        amount: 0,
      },
    })
    customerDepositSuccess.value = true
    emit('data-changed')
    setTimeout(() => {
      customerDepositSuccess.value = false
    }, 3000)
  } catch (err) {
    customerDepositError.value = formatOpenAccountError(err instanceof Error ? err.message : String(err))
  } finally {
    customerDepositLoading.value = false
  }
}

async function submitWithdraw() {
  const deposit = myestDeposit.value
  if (!deposit || !props.activeCompany) {
    withdrawError.value = 'No active deposit found to withdraw from.'
    return
  }
  const amount = Math.min(withdrawAmount.value, deposit.amount)
  withdrawLoading.value = true
  withdrawError.value = null
  withdrawSuccess.value = false
  try {
    await gqlRequest(WITHDRAW_DEPOSIT_MUTATION, {
      input: { depositId: deposit.id, amount },
    })
    withdrawSuccess.value = true
    showWithdrawForm.value = false
    withdrawAmount.value = 0
    emit('data-changed')
    setTimeout(() => {
      withdrawSuccess.value = false
    }, 3000)
  } catch (err) {
    withdrawError.value = err instanceof Error ? err.message : String(err)
  } finally {
    withdrawLoading.value = false
  }
}

async function repayLoanDebt(loanId: string) {
  repayDebtLoadingByLoanId.value = { ...repayDebtLoadingByLoanId.value, [loanId]: true }
  repayDebtErrorByLoanId.value = { ...repayDebtErrorByLoanId.value, [loanId]: null }
  repayDebtSuccessByLoanId.value = { ...repayDebtSuccessByLoanId.value, [loanId]: false }

  try {
    await gqlRequest(REPAY_LOAN_DEBT_MUTATION, { input: { loanId } })
    repayDebtSuccessByLoanId.value = { ...repayDebtSuccessByLoanId.value, [loanId]: true }
    emit('data-changed')
    setTimeout(() => {
      const currentSuccess = { ...repayDebtSuccessByLoanId.value }
      currentSuccess[loanId] = false
      repayDebtSuccessByLoanId.value = currentSuccess
    }, 3000)
  } catch (err) {
    repayDebtErrorByLoanId.value = {
      ...repayDebtErrorByLoanId.value,
      [loanId]: err instanceof Error ? err.message : String(err),
    }
  } finally {
    repayDebtLoadingByLoanId.value = { ...repayDebtLoadingByLoanId.value, [loanId]: false }
  }
}

function navigateToForexTransfer() {
  router.push('/forex?tab=transfer')
}
</script>

<template>
  <!-- Bank profile card (rates + capacity) -->
  <div v-if="bankInfo" class="customer-bank-profile rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
    <div class="customer-rates-grid grid gap-4 md:grid-cols-3">
      <div class="customer-rate-card deposit rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
        <span class="customer-rate-label">{{ t('bank.depositInterestRate') }}</span>
        <span class="customer-rate-value">{{ formatPercent(bankInfo.depositInterestRatePercent) }}</span>
        <span class="customer-rate-hint">{{ t('bank.perYear') }}</span>
      </div>
      <div class="customer-rate-card lending rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
        <span class="customer-rate-label">{{ t('bank.lendingInterestRate') }}</span>
        <span class="customer-rate-value">{{ formatPercent(bankInfo.lendingInterestRatePercent) }}</span>
        <span class="customer-rate-hint">{{ t('bank.perYear') }}</span>
      </div>
      <div class="customer-rate-card capacity rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
        <span class="customer-rate-label">{{ t('bank.availableLendingCapacity') }}</span>
        <span class="customer-rate-value" :class="bankInfo.availableLendingCapacity > 0 ? 'positive' : 'muted'">
          {{ fmt(bankInfo.availableLendingCapacity) }}
        </span>
        <span class="customer-rate-hint">{{ t('bank.reserveInfo') }}</span>
      </div>
    </div>
  </div>

  <section
    v-if="isAuthenticated && overdueDebtLoans.length > 0"
    class="pending-debt-warning mt-6 rounded-2xl border border-red-300/60 bg-red-500/10 p-5 shadow-sm"
    role="alert"
    aria-live="polite"
  >
    <h2 class="text-lg font-bold text-red-700 dark:text-red-300">{{ t('bank.pendingDebtTitle') }}</h2>
    <p class="mt-1 text-sm text-red-700 dark:text-red-300">
      {{ t('bank.pendingDebtSummary', { amount: fmt(totalOverdueDebtAmount) }) }}
    </p>
    <div class="mt-4 grid gap-3">
      <div
        v-for="loan in overdueDebtLoans"
        :key="loan.id"
        class="rounded-xl border border-red-300/50 bg-red-500/5 p-4"
      >
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div class="text-sm font-semibold text-body">
            {{ loan.collateralBuildingName ?? bankInfo?.bankBuildingName }}
          </div>
          <span class="rounded-full border border-red-300/60 bg-red-500/15 px-2 py-0.5 text-xs font-semibold text-red-700 dark:text-red-300">
            {{ loan.status }}
          </span>
        </div>
        <p class="mt-1 text-sm text-body">{{ t('bank.pendingDebtAmountLine', { amount: fmt(loan.remainingPrincipal) }) }}</p>
        <p class="mt-1 text-xs text-muted">{{ formatForeclosureCountdown(loan) }}</p>
        <div class="mt-3 flex flex-wrap items-center gap-3">
          <button
            class="btn btn-primary btn-sm"
            :disabled="!!repayDebtLoadingByLoanId[loan.id]"
            @click="repayLoanDebt(loan.id)"
          >
            {{ repayDebtLoadingByLoanId[loan.id] ? t('common.loading') : t('bank.repayDebtNow') }}
          </button>
          <span v-if="repayDebtSuccessByLoanId[loan.id]" class="text-xs text-green-500">{{ t('bank.repayDebtSuccess') }}</span>
          <span v-if="repayDebtErrorByLoanId[loan.id]" class="text-xs text-red-500">{{ repayDebtErrorByLoanId[loan.id] }}</span>
        </div>
      </div>
    </div>
  </section>

  <div class="flex flex-col gap-6 lg:flex-row">
    <!-- Account-style deposit relationship -->
    <section v-if="isAuthenticated && isEligibleDepositContext" class="customer-account-section grow rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
      <div class="account-header flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div class="account-header-info flex flex-row gap-2">
          <h2 class="section-title text-2xl font-bold text-body grow">{{ t('bank.myAccount') }}</h2>
          <span class="account-company-tag inline-flex w-fit items-center rounded-full border border-divider bg-card-raised px-3 py-1 text-xs font-semibold uppercase tracking-[0.12em] text-muted">
            {{ isCompanyAccountActive ? activeCompany?.name : t('accountSwitcher.personalAccountHint') }}
          </span>
        </div>
        <div v-if="hasCustomerAccount && myAccountBalance > 0" class="account-actions flex flex-wrap gap-3">
          <button class="btn btn-secondary btn-sm" @click="navigateToForexTransfer">
            {{ t('bank.addFundsViaForex') }}
          </button>
          <button class="btn btn-outline btn-sm" @click="showWithdrawForm = !showWithdrawForm">
            {{ showWithdrawForm ? t('common.cancel') : t('bank.withdraw') }}
          </button>
        </div>
      </div>

      <!-- Operating bank accounts for this company in the bank's city currency -->
      <div v-if="myOperatingAccountsHere.length > 0" class="mt-6 flex flex-col gap-3">
        <h3 class="text-sm font-semibold uppercase tracking-wide text-muted">{{ t('bank.operatingAccounts') }}</h3>
        <div
          v-for="account in myOperatingAccountsHere"
          :key="account.id"
          class="operating-account-row flex flex-col gap-3 rounded-2xl border border-divider bg-card-raised px-5 py-4 shadow-sm sm:flex-row sm:items-center sm:justify-between"
        >
          <div class="flex flex-col gap-0.5 sm:flex-1">
            <span class="text-xs text-muted">{{ t('bank.accountNumber') }}</span>
            <span class="font-mono text-sm font-semibold text-body">{{ account.accountNumber }}</span>
          </div>
          <div class="flex flex-col gap-0.5 sm:items-end">
            <span class="text-xs text-muted">{{ t('bank.accountBalance') }}</span>
            <span class="text-base font-bold text-body">{{ formatCurrency(account.balance, account.currencyCode) }}</span>
          </div>
          <router-link :to="`/bank-statement/${account.companyId}`" class="btn btn-outline btn-sm shrink-0 self-start sm:self-auto">
            {{ t('bankStatement.title') }}
          </router-link>
        </div>
      </div>

      <!-- Account balance card -->
      <div v-if="hasCustomerAccount" class="account-balance-card mt-6 rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
        <div class="account-balance-main flex flex-col gap-1">
          <span class="account-balance-label">{{ t('bank.accountBalance') }}</span>
          <span class="account-balance-value">{{ fmt(myAccountBalance) }}</span>
        </div>
        <div class="account-balance-meta mt-4 flex flex-wrap items-center gap-3 text-sm">
          <span class="account-interest-label">{{ t('bank.totalInterestEarned') }}</span>
          <span class="account-interest-value positive">+{{ fmt(myAccountInterestEarned) }}</span>
          <span v-if="bankInfo" class="account-rate-badge"> {{ formatPercent(bankInfo.depositInterestRatePercent) }} {{ t('bank.perYear') }} </span>
        </div>
      </div>

      <!-- Withdraw form -->
      <div v-if="showWithdrawForm" class="account-action-form mt-6 rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
        <h3 class="action-form-title text-lg font-semibold text-body">{{ t('bank.withdraw') }}</h3>
        <div class="form-group mt-4 flex flex-col gap-3">
          <label for="withdraw-amount" class="text-sm font-semibold text-body">{{ t('bank.withdrawAmount') }}</label>
          <input
            id="withdraw-amount"
            v-model.number="withdrawAmount"
            type="number"
            :min="1"
            :max="myAccountBalance"
            step="1000"
            class="form-input rounded-2xl border border-divider bg-card px-4 py-3 text-base text-body"
          />
          <span class="form-hint text-sm text-muted">{{ t('bank.maxWithdraw') }}: {{ fmt(myAccountBalance) }}</span>
        </div>
        <div v-if="withdrawError" class="error-message mt-4">{{ withdrawError }}</div>
        <button class="btn btn-primary mt-4" :disabled="withdrawLoading || withdrawAmount <= 0 || withdrawAmount > myAccountBalance" @click="submitWithdraw">
          {{ withdrawLoading ? t('common.loading') : t('bank.confirmWithdraw') }}
        </button>
      </div>

      <!-- Success messages -->
      <div v-if="withdrawSuccess" class="success-message">{{ t('bank.withdrawSuccess') }}</div>
      <div v-if="customerDepositSuccess" class="success-message">{{ t('bank.depositCreated') }}</div>

      <!-- First deposit (no account yet) -->
      <div v-if="!hasCustomerAccount && !customerDepositSuccess" class="account-empty-state mt-6 flex flex-col gap-5 rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
        <p class="account-empty-hint text-sm text-muted sm:text-base">
          {{ t('bank.openAccountHint', { rate: formatPercent(bankInfo?.depositInterestRatePercent ?? 0) }) }}
        </p>
        <p class="rounded-2xl border border-divider bg-card px-4 py-3 text-sm text-muted">{{ t('bank.zeroBalanceFundingHint') }}</p>
        <div v-if="bankInfo" class="repayment-preview rounded-2xl border border-divider bg-card px-4 py-4">
          <div class="preview-row flex items-center justify-between gap-4 text-sm">
            <span>{{ t('bank.depositInterestRate') }}</span>
            <strong>{{ formatPercent(bankInfo.depositInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
          </div>
        </div>
        <div v-if="customerDepositError" class="error-message">{{ customerDepositError }}</div>
        <button class="btn btn-primary" :disabled="customerDepositLoading" @click="submitCustomerDeposit">
          {{ customerDepositLoading ? t('common.loading') : t('bank.openAccount') }}
        </button>
      </div>
    </section>

    <!-- Prompt to log in or switch account if not in company mode -->
    <section v-else class="customer-deposit-form-section grow rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
      <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.makeDeposit') }}</h2>
      <div v-if="!isAuthenticated" class="auth-prompt mt-4 flex flex-col gap-4">
        <p>{{ t('bank.loginToLendDescription') }}</p>
        <router-link to="/login" class="btn btn-primary">{{ t('auth.login') }}</router-link>
      </div>
      <div v-else class="auth-prompt mt-4">
        <p>{{ t('bank.companyAccountRequired') }}</p>
      </div>
    </section>

    <!-- Direct loan request -->
    <section class="customer-loans-section grow rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
      <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.borrowFromThisBank') }}</h2>
      <UiStateEmpty v-if="!directBorrowingOption">
        {{ t('bank.noOffersFromBank') }}
      </UiStateEmpty>
      <div v-else class="customer-offers-grid mt-6 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        <div class="customer-offer-card rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
          <div class="customer-offer-header">
            <span class="offer-rate-big">{{ formatPercent(directBorrowingOption.annualInterestRatePercent) }}</span>
            <span class="offer-rate-hint">{{ t('bank.perYear') }}</span>
          </div>
          <div class="customer-offer-stats">
            <div class="offer-stat-row">
              <span>{{ t('bank.maxPrincipal') }}</span>
              <strong>{{ fmt(directBorrowingOption.maxPrincipalPerLoan) }}</strong>
            </div>
            <div class="offer-stat-row">
              <span>{{ t('bank.remainingCapacity') }}</span>
              <strong :class="directBorrowingOption.remainingCapacity > 0 ? 'positive' : 'muted'">
                {{ fmt(directBorrowingOption.remainingCapacity) }}
              </strong>
            </div>
            <div class="offer-stat-row">
              <span>{{ t('bank.duration') }}</span>
              <strong>{{ formatLoanDuration(directBorrowingOption.durationTicks) }}</strong>
            </div>
          </div>
          <p class="offer-context-hint">{{ t('bank.directBorrowingHint') }}</p>

          <div v-if="isAuthenticated && isCompanyAccountActive && directBorrowingOption.remainingCapacity > 0">
            <button class="btn btn-primary btn-sm" @click="router.push({ name: 'bank-loan-request', params: { buildingId: bankBuildingId } })">
              {{ t('bank.acceptLoan') }}
            </button>
          </div>
          <div v-else-if="!isAuthenticated">
            <router-link to="/login" class="btn btn-secondary btn-sm">{{ t('auth.login') }}</router-link>
          </div>
          <p v-else-if="!isCompanyAccountActive" class="offer-context-hint">{{ t('bank.companyAccountRequired') }}</p>
          <p v-else class="offer-context-hint muted">{{ t('bank.noCapacityAvailable') }}</p>
        </div>
      </div>
    </section>
  </div>

  <!-- My Loans at This Bank -->
  <section v-if="isAuthenticated && myLoansHere.length > 0" class="my-loans-here-section mt-8 rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
    <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.myLoans') }}</h2>
    <div class="loans-list mt-6 grid gap-4">
      <div v-for="loan in myLoansHere" :key="loan.id" class="loan-row rounded-2xl border border-divider bg-card-raised p-4 shadow-sm">
        <div class="loan-row-main">
          <span class="loan-amount">{{ fmt(loan.remainingPrincipal) }}</span>
          <span :class="['loan-status', loanStatusClass(loan.status)]">{{ loan.status }}</span>
        </div>
        <div v-if="loan.collateralBuildingId" class="collateral-badge">
          <span aria-hidden="true">🔒</span> {{ t('bank.securedLoan') }}: {{ loan.collateralBuildingName }}
          <span v-if="loan.collateralAppraisedValue" class="collateral-badge-value"> ({{ fmt(loan.collateralAppraisedValue) }}) </span>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
/* Rate cards – E2E hooks must be preserved */
.customer-rate-card {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.customer-rate-label {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.customer-rate-value {
  font-size: 1.6rem;
  font-weight: 700;
}

.customer-rate-card.deposit .customer-rate-value {
  color: #22c55e;
}

.customer-rate-card.lending .customer-rate-value {
  color: #f59e0b;
}

.customer-rate-card.capacity .customer-rate-value.positive {
  color: #22c55e;
}

.customer-rate-card.capacity .customer-rate-value.muted {
  color: var(--color-text-muted);
}

.customer-rate-hint {
  font-size: 0.8rem;
  color: var(--color-text-muted);
}

/* Account balance */
.account-balance-label {
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.account-balance-value {
  font-size: 2rem;
  font-weight: 700;
  color: var(--color-text-primary, #111);
  line-height: 1.1;
}

.account-interest-label {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.account-interest-value {
  font-size: 0.9rem;
  font-weight: 600;
}

.account-interest-value.positive {
  color: #22c55e;
}

.account-rate-badge {
  font-size: 0.8rem;
  padding: 2px 8px;
  border-radius: 99px;
  background: var(--color-primary-bg, rgba(59, 130, 246, 0.1));
  color: var(--color-primary, #3b82f6);
  font-weight: 600;
}

.action-form-title {
  margin: 0;
}

/* Loan offer card */
.customer-offer-header {
  display: flex;
  align-items: baseline;
  gap: 0.4rem;
  margin-bottom: 0.75rem;
}

.offer-rate-big {
  font-size: 1.75rem;
  font-weight: 700;
  color: #f59e0b;
}

.offer-rate-hint {
  font-size: 0.82rem;
  color: var(--color-text-muted);
}

.customer-offer-stats {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-bottom: 0.75rem;
}

.offer-stat-row {
  display: flex;
  justify-content: space-between;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.offer-stat-row strong.positive {
  color: #22c55e;
}

.offer-stat-row strong.muted {
  color: var(--color-text-muted);
}

.offer-context-hint {
  font-size: 0.82rem;
  color: var(--color-text-muted);
  margin-bottom: 0.5rem;
}

.offer-context-hint.muted {
  font-style: italic;
}

/* Loan rows – E2E hook .loan-row must be preserved */
.loan-row-main {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.loan-amount {
  font-weight: 600;
}

.collateral-badge {
  margin-top: 4px;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.collateral-badge-value {
  color: var(--color-text-muted);
}

/* Forms */
.form-input {
  padding: 6px var(--spacing-sm);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-primary);
}

.form-hint {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.error-message {
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  font-size: 0.85rem;
}

.success-message {
  color: var(--color-success, #22c55e);
  font-size: 0.875rem;
  padding: 0.5rem 0;
}
</style>
