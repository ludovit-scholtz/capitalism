<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import type { LoanOfferSummary, LoanSummary, BankDepositSummary, BankInfoSummary, Company, PlayerBankAccountSummary } from '@/types'
import { formatLoanDuration, formatCurrency, formatPercent, loanStatusClass } from '@/lib/loanHelpers'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const bankBuildingId = computed(() => route.params.buildingId as string)

const loading = ref(true)
const error = ref<string | null>(null)
const issuedLoans = ref<LoanSummary[]>([])
const bankDeposits = ref<BankDepositSummary[]>([])
const bankInfo = ref<BankInfoSummary | null>(null)
const userCompanies = ref<Company[]>([])

// Ownership detection uses the navbar-selected company and bank owner company id.
const isOwner = computed(() => {
  if (!auth.isAuthenticated) return false
  const selectedCompany = getActiveCompany(auth.player, userCompanies.value)
  const bankOwnerCompanyId = bankInfo.value?.lenderCompanyId
  if (!selectedCompany || !bankOwnerCompanyId) return false
  return selectedCompany.id === bankOwnerCompanyId
})

// Customer-specific state
const myDepositsHere = ref<BankDepositSummary[]>([])
const customerDepositLoading = ref(false)
const customerDepositError = ref<string | null>(null)
const customerDepositSuccess = ref(false)

// Account-style deposit management (customer view)
const showWithdrawForm = ref(false)
const withdrawAmount = ref(0)
const withdrawLoading = ref(false)
const withdrawError = ref<string | null>(null)
const withdrawSuccess = ref(false)
// My active loans at this bank (customer view)
const myLoansHere = ref<LoanSummary[]>([])
// Operating bank accounts matching the bank's city currency (customer view)
const myOperatingAccountsHere = ref<PlayerBankAccountSummary[]>([])

// Rate configuration form
const showRatesForm = ref(false)
const ratesForm = ref({ depositInterestRatePercent: 3, lendingInterestRatePercent: 8 })
const ratesLoading = ref(false)
const ratesError = ref<string | null>(null)
const ratesSuccess = ref(false)

// Base capital deposit (activation)
const baseDepositLoading = ref(false)
const baseDepositError = ref<string | null>(null)
const baseDepositSuccess = ref(false)

const BANK_LOANS_QUERY = `
  query BankLoans($bankBuildingId: UUID!) {
    bankLoans(bankBuildingId: $bankBuildingId) {
      id
      loanOfferId
      borrowerCompanyId
      borrowerCompanyName
      lenderCompanyId
      lenderCompanyName
      bankBuildingId
      bankBuildingName
      originalPrincipal
      remainingPrincipal
      annualInterestRatePercent
      durationTicks
      startTick
      dueTick
      nextPaymentTick
      paymentAmount
      paymentsMade
      totalPayments
      status
      missedPayments
      accumulatedPenalty
      acceptedAtUtc
      closedAtUtc
      collateralBuildingId
      collateralBuildingName
      collateralAppraisedValue
    }
  }
`

const BANK_INFO_QUERY = `
  query BankInfo($id: UUID!) {
    bankInfo(bankBuildingId: $id) {
      bankBuildingId
      bankBuildingName
      cityId
      cityName
      cityCurrencyCode
      cityCurrencySymbol
      baseCapitalRequirement
      lenderCompanyId
      lenderCompanyName
      depositInterestRatePercent
      lendingInterestRatePercent
      totalDeposits
      lendableCapacity
      outstandingLoanPrincipal
      availableLendingCapacity
      baseCapitalDeposited
      centralBankDebt
      centralBankInterestRatePercent
      reserveRequirement
      availableCash
      reserveShortfall
      liquidityStatus
    }
  }
`

const BANK_DEPOSITS_QUERY = `
  query BankDeposits($id: UUID!) {
    bankDeposits(bankBuildingId: $id) {
      id
      bankBuildingId
      bankBuildingName
      depositorCompanyId
      depositorCompanyName
      amount
      depositInterestRatePercent
      isBaseCapital
      isActive
      depositedAtTick
      depositedAtUtc
      totalInterestPaid
      cityCurrencyCode
    }
  }
`

const SET_BANK_RATES_MUTATION = `
  mutation SetBankRates($input: SetBankRatesInput!) {
    setBankRates(input: $input) {
      bankBuildingId
      cityCurrencyCode
      cityCurrencySymbol
      baseCapitalRequirement
      depositInterestRatePercent
      lendingInterestRatePercent
      totalDeposits
      lendableCapacity
      availableLendingCapacity
      baseCapitalDeposited
      centralBankDebt
      centralBankInterestRatePercent
      reserveRequirement
      availableCash
      reserveShortfall
      liquidityStatus
    }
  }
`

const MY_COMPANIES_QUERY = `
  {
    me {
      companies {
        id
        name
        cash
        playerId
        buildings { id type }
      }
    }
  }
`

const MY_BANK_ACCOUNTS_QUERY = `
  {
    myBankAccounts {
      id
      accountNumber
      currencyCode
      currencySymbol
      balance
      companyId
      companyName
      ownerType
      ownerDisplayName
      bankBuildingId
    }
  }
`

const MY_DEPOSITS_QUERY = `
  {
    myDeposits {
      id
      bankBuildingId
      bankBuildingName
      depositorCompanyId
      depositorCompanyName
      amount
      depositInterestRatePercent
      isBaseCapital
      isActive
      depositedAtTick
      depositedAtUtc
      totalInterestPaid
      cityCurrencyCode
    }
  }
`

const MY_LOANS_QUERY = `
  {
    myLoans {
      id
      bankBuildingId
      bankBuildingName
      originalPrincipal
      remainingPrincipal
      annualInterestRatePercent
      status
      collateralBuildingId
      collateralBuildingName
      collateralAppraisedValue
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

const INITIATE_BASE_DEPOSIT_MUTATION = `
  mutation InitiateBaseDeposit($bankBuildingId: UUID!) {
    initiateBaseDeposit(bankBuildingId: $bankBuildingId) {
      bankBuildingId
      bankBuildingName
      cityCurrencyCode
      cityCurrencySymbol
      baseCapitalRequirement
      depositInterestRatePercent
      lendingInterestRatePercent
      totalDeposits
      lendableCapacity
      outstandingLoanPrincipal
      availableLendingCapacity
      baseCapitalDeposited
      centralBankDebt
      centralBankInterestRatePercent
      reserveRequirement
      availableCash
      reserveShortfall
      liquidityStatus
    }
  }
`

async function loadData(isRefresh = false) {
  if (!isRefresh) {
    loading.value = true
  }
  error.value = null
  try {
    // Always load bank info (public) and user companies (for ownership check)
    const [infoResult, companiesResult] = await Promise.all([
      gqlRequest<{ bankInfo: BankInfoSummary }>(BANK_INFO_QUERY, {
        id: bankBuildingId.value,
      }),
      auth.isAuthenticated ? gqlRequest<{ me: { companies: Company[] } }>(MY_COMPANIES_QUERY) : Promise.resolve({ me: { companies: [] } }),
    ])
    bankInfo.value = infoResult.bankInfo ?? null
    userCompanies.value = companiesResult.me?.companies ?? []

    const selectedCompany = getActiveCompany(auth.player, userCompanies.value)
    const ownerDetected = !!selectedCompany && selectedCompany.id === bankInfo.value?.lenderCompanyId

    if (ownerDetected) {
      // Owner view: load full management data
      const [loansResult, depositsResult] = await Promise.all([
        gqlRequest<{ bankLoans: LoanSummary[] }>(BANK_LOANS_QUERY, {
          bankBuildingId: bankBuildingId.value,
        }),
        gqlRequest<{ bankDeposits: BankDepositSummary[] }>(BANK_DEPOSITS_QUERY, {
          id: bankBuildingId.value,
        }),
      ])
      const loans = loansResult.bankLoans ?? []
      if (!deepEqual(issuedLoans.value, loans)) {
        issuedLoans.value = loans
      }
      const deposits = depositsResult.bankDeposits ?? []
      if (!deepEqual(bankDeposits.value, deposits)) {
        bankDeposits.value = deposits
      }
      if (bankInfo.value && !showRatesForm.value) {
        ratesForm.value.depositInterestRatePercent = bankInfo.value.depositInterestRatePercent
        ratesForm.value.lendingInterestRatePercent = bankInfo.value.lendingInterestRatePercent
      }
    } else {
      // Customer view: load deposit relationship, active loans, and operating accounts at this bank.
      const [depositsResult, myLoansResult, accountsResult] = await Promise.all([
        auth.isAuthenticated ? gqlRequest<{ myDeposits: BankDepositSummary[] }>(MY_DEPOSITS_QUERY) : Promise.resolve({ myDeposits: [] }),
        auth.isAuthenticated ? gqlRequest<{ myLoans: LoanSummary[] }>(MY_LOANS_QUERY) : Promise.resolve({ myLoans: [] }),
        auth.isAuthenticated ? gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY) : Promise.resolve({ myBankAccounts: [] }),
      ])
      const myDeposits = (depositsResult.myDeposits ?? []).filter((d) => d.bankBuildingId === bankBuildingId.value)
      if (!deepEqual(myDepositsHere.value, myDeposits)) {
        myDepositsHere.value = myDeposits
      }
      const loansHere = (myLoansResult.myLoans ?? []).filter((l: LoanSummary) => l.bankBuildingId === bankBuildingId.value)
      if (!deepEqual(myLoansHere.value, loansHere)) {
        myLoansHere.value = loansHere
      }
      // Operating accounts for the active company matching this bank's city currency
      const bankCurrency = bankInfo.value?.cityCurrencyCode
      const activeComp = getActiveCompany(auth.player, userCompanies.value)
      const operatingAccounts = (accountsResult.myBankAccounts ?? []).filter(
        (a) => a.ownerType === 'COMPANY' && a.companyId === activeComp?.id && bankCurrency && a.currencyCode.toUpperCase() === bankCurrency.toUpperCase() && a.bankBuildingId === bankBuildingId.value,
      )
      if (!deepEqual(myOperatingAccountsHere.value, operatingAccounts)) {
        myOperatingAccountsHere.value = operatingAccounts
      }
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  } finally {
    loading.value = false
  }
}

onMounted(loadData)

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await loadData(true)
  await restoreScrollPosition(scrollPos)
})

const overdueLoans = computed(() => issuedLoans.value.filter((l) => l.status !== 'ACTIVE' && l.status !== 'REPAID'))

/** The active city currency code ÔÇö from bankInfo when available, fallback to EUR. */
const cityCurrency = computed(() => bankInfo.value?.cityCurrencyCode ?? 'EUR')

/** Helper: format an amount in the bank's local city currency. */
const fmt = (amount: number) => formatCurrency(amount, cityCurrency.value)

const totalIssuedCapacity = computed(() => issuedLoans.value.filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE').reduce((sum, l) => sum + l.remainingPrincipal, 0))

const expectedMonthlyIncome = computed(() => issuedLoans.value.filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE').reduce((sum, l) => sum + l.paymentAmount, 0))

async function saveRates() {
  if (!bankBuildingId.value) return
  ratesLoading.value = true
  ratesError.value = null
  ratesSuccess.value = false
  try {
    const result = await gqlRequest<{ setBankRates: BankInfoSummary }>(SET_BANK_RATES_MUTATION, {
      input: {
        bankBuildingId: bankBuildingId.value,
        depositInterestRatePercent: ratesForm.value.depositInterestRatePercent,
        lendingInterestRatePercent: ratesForm.value.lendingInterestRatePercent,
      },
    })
    bankInfo.value = result.setBankRates
    ratesSuccess.value = true
    showRatesForm.value = false
  } catch (err) {
    ratesError.value = err instanceof Error ? err.message : String(err)
  } finally {
    ratesLoading.value = false
  }
}

async function submitBaseDeposit() {
  if (!bankBuildingId.value) return
  baseDepositLoading.value = true
  baseDepositError.value = null
  baseDepositSuccess.value = false
  try {
    const result = await gqlRequest<{ initiateBaseDeposit: BankInfoSummary }>(INITIATE_BASE_DEPOSIT_MUTATION, { bankBuildingId: bankBuildingId.value })
    bankInfo.value = result.initiateBaseDeposit
    baseDepositSuccess.value = true
    await loadData(true)
  } catch (err) {
    baseDepositError.value = err instanceof Error ? err.message : String(err)
  } finally {
    baseDepositLoading.value = false
  }
}

// ÔöÇÔöÇ Customer view helpers ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

const activeCompany = computed(() => getActiveCompany(auth.player, userCompanies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)
const directBorrowingOption = computed<LoanOfferSummary | null>(() => {
  if (!bankInfo.value?.baseCapitalDeposited) {
    return null
  }

  return {
    id: bankInfo.value.bankBuildingId,
    bankBuildingId: bankInfo.value.bankBuildingId,
    bankBuildingName: bankInfo.value.bankBuildingName,
    cityId: bankInfo.value.cityId,
    cityName: bankInfo.value.cityName,
    lenderCompanyId: bankInfo.value.lenderCompanyId,
    lenderCompanyName: bankInfo.value.lenderCompanyName,
    annualInterestRatePercent: bankInfo.value.lendingInterestRatePercent,
    maxPrincipalPerLoan: Math.max(bankInfo.value.availableLendingCapacity, 0),
    totalCapacity: bankInfo.value.lendableCapacity,
    usedCapacity: bankInfo.value.outstandingLoanPrincipal,
    remainingCapacity: Math.max(bankInfo.value.availableLendingCapacity, 0),
    durationTicks: 8760,
    isActive: bankInfo.value.availableLendingCapacity > 0,
    createdAtTick: 0,
    createdAtUtc: '',
  }
})

// Account-style aggregation: treat all active non-base-capital deposits from active company as one account
const myActiveDepositsHere = computed(() => myDepositsHere.value.filter((d) => d.isActive && !d.isBaseCapital))
const hasCustomerAccount = computed(() => myActiveDepositsHere.value.length > 0)
const myAccountBalance = computed(() => myActiveDepositsHere.value.reduce((sum, d) => sum + d.amount, 0))
const myAccountInterestEarned = computed(() => myActiveDepositsHere.value.reduce((sum, d) => sum + d.totalInterestPaid, 0))
// The oldest tranche is used first for partial withdrawals.
const myOldestDeposit = computed<BankDepositSummary | null>(() => {
  const sorted = [...myActiveDepositsHere.value].sort((a, b) => a.depositedAtTick - b.depositedAtTick)
  return sorted[0] ?? null
})

function formatOpenAccountError(errorMessage: string) {
  if (!errorMessage.includes('Insufficient company funds to open this bank account.')) {
    return errorMessage
  }

  return `${errorMessage} ${t('bank.zeroBalanceFundingHint')}`
}

async function submitCustomerDeposit() {
  if (!activeCompany.value || !bankBuildingId.value) return
  customerDepositLoading.value = true
  customerDepositError.value = null
  customerDepositSuccess.value = false
  try {
    await gqlRequest(CREATE_DEPOSIT_MUTATION, {
      input: {
        bankBuildingId: bankBuildingId.value,
        depositorCompanyId: activeCompany.value.id,
        amount: 0,
      },
    })
    customerDepositSuccess.value = true
    await loadData()
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
  const deposit = myOldestDeposit.value
  if (!deposit || !activeCompany.value) {
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
    await loadData()
    setTimeout(() => {
      withdrawSuccess.value = false
    }, 3000)
  } catch (err) {
    withdrawError.value = err instanceof Error ? err.message : String(err)
  } finally {
    withdrawLoading.value = false
  }
}

function navigateToForexTransfer() {
  router.push('/forex?tab=transfer')
}
</script>

<template>
<main class="bank-management-view container mx-auto px-4 pb-16 pt-6 sm:px-6 lg:px-8 lg:pb-20 lg:pt-8">
    <div class="page-header mb-10 flex flex-col gap-3 lg:mb-12">
      <!-- Show different titles based on ownership -->
      <template v-if="!loading && isOwner">
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ t('bank.configureBank') }}</h1>
        <p class="page-subtitle text-sm text-muted sm:text-base">{{ t('bank.lending') }}</p>
      </template>
      <template v-else-if="!loading">
        <div class="customer-nav">
          <button class="btn-back" @click="router.push('/banking')">├ö─ç├ë {{ t('bank.backToMarketplace') }}</button>
        </div>
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ bankInfo?.bankBuildingName ?? t('bank.customerView') }}</h1>
        <p class="page-subtitle flex flex-wrap items-center gap-2 text-sm text-muted sm:text-base">
          {{ bankInfo?.lenderCompanyName }} ÔöČ─Ü {{ bankInfo?.cityName }}
          <span v-if="bankInfo?.cityCurrencyCode" class="currency-badge">{{ bankInfo.cityCurrencyCode }}</span>
        </p>
      </template>
      <template v-else>
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ t('bank.customerView') }}</h1>
      </template>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="spinner" />
      <span>{{ t('common.loading') }}</span>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="error-message">{{ error }}</p>
      <button class="btn btn-secondary" @click="() => loadData()">{{ t('common.retry') }}</button>
    </div>

    <template v-else>
      <!-- ├ö├Â├ç├ö├Â├ç OWNER VIEW ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->
      <template v-if="isOwner">
        <!-- ├ö├Â├ç├ö├Â├ç Base Capital Deposit Required ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->
        <div v-if="bankInfo && !bankInfo.baseCapitalDeposited" class="base-deposit-required">
          <div class="base-deposit-icon" aria-hidden="true">┬ş─Ź─ć┼Ż</div>
          <div class="base-deposit-body">
            <h2 class="base-deposit-title">{{ t('bank.baseDepositRequired') }}</h2>
            <p class="base-deposit-description">
              {{
                t('bank.baseDepositRequiredBody', {
                  amount: fmt(bankInfo.baseCapitalRequirement ?? 10_000_000),
                })
              }}
            </p>
            <p class="base-deposit-hint">
              {{
                t('bank.baseCapitalRequired', {
                  amount: fmt(bankInfo.baseCapitalRequirement ?? 10_000_000),
                  currency: bankInfo.cityCurrencyCode ?? 'EUR',
                })
              }}
            </p>
            <p v-if="bankInfo.cityCurrencyCode && bankInfo.cityCurrencyCode !== 'EUR'" class="base-deposit-currency-note">
              <span class="currency-badge">{{ bankInfo.cityCurrencyCode }}</span>
              {{ bankInfo.cityCurrencySymbol }}{{ (bankInfo.baseCapitalRequirement ?? 0).toLocaleString() }}
              {{ t('bank.localCurrencyNote') }}
            </p>
          </div>
          <div v-if="baseDepositError" class="error-message">{{ baseDepositError }}</div>
          <div v-if="baseDepositSuccess" class="success-message">{{ t('bank.baseDepositSuccess') }}</div>
          <button class="btn btn-primary base-deposit-btn" :disabled="baseDepositLoading" @click="submitBaseDeposit">
            {{ baseDepositLoading ? t('common.loading') : t('bank.makeBaseDeposit') }}
          </button>
        </div>

        <!-- Bank Info & Rate Configuration (visible to owner regardless of activation status) -->
        <div v-if="bankInfo" class="bank-info-section">
          <div class="bank-info-header">
            <h2>{{ t('bank.bankRates') }}</h2>
            <button class="btn btn-secondary btn-sm" @click="showRatesForm = !showRatesForm">
              {{ showRatesForm ? t('common.cancel') : t('bank.setBankRates') }}
            </button>
          </div>

          <!-- Rates form -->
          <div v-if="showRatesForm" class="rates-form">
            <div class="form-grid">
              <div class="form-group">
                <label for="deposit-rate">{{ t('bank.depositInterestRate') }} (%)</label>
                <input id="deposit-rate" v-model.number="ratesForm.depositInterestRatePercent" type="number" min="0" max="100" step="0.1" class="form-input" />
              </div>
              <div class="form-group">
                <label for="lending-rate">{{ t('bank.lendingInterestRate') }} (%)</label>
                <input id="lending-rate" v-model.number="ratesForm.lendingInterestRatePercent" type="number" min="0.1" max="200" step="0.1" class="form-input" />
              </div>
            </div>
            <div v-if="ratesError" class="error-message">{{ ratesError }}</div>
            <button class="btn btn-primary" :disabled="ratesLoading" @click="saveRates">
              {{ ratesLoading ? t('common.loading') : t('bank.setBankRates') }}
            </button>
          </div>
          <!-- Success message shown outside the form so it persists after the form closes -->
          <div v-if="ratesSuccess && !showRatesForm" class="success-message rates-success">
            {{ t('bank.ratesUpdated') }}
          </div>

          <!-- Bank stats panel -->
          <div class="bank-stats-grid">
            <div class="bank-stat">
              <span class="bank-stat-label">{{ t('bank.depositInterestRate') }}</span>
              <span class="bank-stat-value deposit-rate">{{ formatPercent(bankInfo.depositInterestRatePercent) }}</span>
            </div>
            <div class="bank-stat">
              <span class="bank-stat-label">{{ t('bank.lendingInterestRate') }}</span>
              <span class="bank-stat-value lending-rate">{{ formatPercent(bankInfo.lendingInterestRatePercent) }}</span>
            </div>
            <div class="bank-stat">
              <span class="bank-stat-label">{{ t('bank.totalDeposits') }}</span>
              <span class="bank-stat-value">{{ fmt(bankInfo.totalDeposits) }}</span>
            </div>
            <div class="bank-stat">
              <span class="bank-stat-label">{{ t('bank.lendableCapacity') }}</span>
              <span class="bank-stat-value">{{ fmt(bankInfo.lendableCapacity) }}</span>
              <span class="bank-stat-hint">{{ t('bank.reserveInfo') }}</span>
            </div>
            <div class="bank-stat">
              <span class="bank-stat-label">{{ t('bank.availableLendingCapacity') }}</span>
              <span class="bank-stat-value" :class="bankInfo.availableLendingCapacity > 0 ? 'positive' : 'negative'">
                {{ fmt(bankInfo.availableLendingCapacity) }}
              </span>
            </div>
          </div>
        </div>

        <!-- ├ö├Â├ç├ö├Â├ç Liquidity Health Panel (owner view) ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->
        <section v-if="bankInfo && bankInfo.baseCapitalDeposited && bankInfo.liquidityStatus" class="liquidity-section">
          <h2 class="section-title">{{ t('bank.liquidityHealth') }}</h2>
          <div class="liquidity-status-banner" :class="`liquidity-${bankInfo.liquidityStatus.toLowerCase()}`">
            <span class="liquidity-status-label">{{ t(`bank.liquidityStatus.${bankInfo.liquidityStatus}`) }}</span>
            <span class="liquidity-status-hint">{{ t(`bank.liquidityStatusHint.${bankInfo.liquidityStatus}`) }}</span>
          </div>

          <div class="liquidity-grid">
            <div class="liquidity-stat">
              <span class="liquidity-stat-label">{{ t('bank.availableCash') }}</span>
              <span class="liquidity-stat-value" :class="(bankInfo.availableCash ?? 0) >= (bankInfo.reserveRequirement ?? 0) ? 'positive' : 'negative'">
                {{ fmt(bankInfo.availableCash ?? 0) }}
              </span>
            </div>
            <div class="liquidity-stat">
              <span class="liquidity-stat-label">{{ t('bank.reserveRequirement') }}</span>
              <span class="liquidity-stat-value">{{ fmt(bankInfo.reserveRequirement ?? 0) }}</span>
              <span class="liquidity-stat-hint">{{ t('bank.reserveInfo') }}</span>
            </div>
            <div class="liquidity-stat">
              <span class="liquidity-stat-label">{{ t('bank.reserveShortfall') }}</span>
              <span class="liquidity-stat-value" :class="(bankInfo.reserveShortfall ?? 0) > 0 ? 'negative' : 'positive'">
                {{ (bankInfo.reserveShortfall ?? 0) > 0 ? fmt(bankInfo.reserveShortfall) : t('bank.noReserveShortfall') }}
              </span>
            </div>
            <div class="liquidity-stat" :class="{ 'liquidity-stat-warning': (bankInfo.centralBankDebt ?? 0) > 0 }">
              <span class="liquidity-stat-label">{{ t('bank.centralBankDebt') }}</span>
              <span class="liquidity-stat-value" :class="(bankInfo.centralBankDebt ?? 0) > 0 ? 'negative' : 'positive'">
                {{ (bankInfo.centralBankDebt ?? 0) > 0 ? fmt(bankInfo.centralBankDebt) : fmt(0) }}
              </span>
              <span v-if="(bankInfo.centralBankDebt ?? 0) > 0" class="liquidity-stat-hint">
                {{ t('bank.centralBankRate') }}: {{ formatPercent(bankInfo.centralBankInterestRatePercent ?? 2) }} p.a.
              </span>
            </div>
          </div>

          <!-- Central-bank debt context -->
          <div v-if="(bankInfo.centralBankDebt ?? 0) > 0" class="central-bank-notice">
            <div class="notice-icon">├ö├ť├í</div>
            <div class="notice-body">
              <strong>{{ t('bank.centralBankDebt') }}</strong>
              <p>{{ t('bank.centralBankDebtHint', { rate: (bankInfo.centralBankInterestRatePercent ?? 2).toFixed(2) }) }}</p>
            </div>
          </div>

          <!-- Recommended actions when under pressure -->
          <div v-if="bankInfo.liquidityStatus !== 'HEALTHY'" class="recommended-actions">
            <h3 class="actions-title">{{ t('bank.recommendedActions') }}</h3>
            <ul class="actions-list">
              <li v-if="(bankInfo.reserveShortfall ?? 0) > 0">{{ t('bank.actionAddDeposits') }}</li>
              <li v-if="bankInfo.outstandingLoanPrincipal > 0">{{ t('bank.actionReduceLending') }}</li>
              <li v-if="(bankInfo.centralBankDebt ?? 0) > 0">{{ t('bank.actionRecapitalize') }}</li>
            </ul>
          </div>

          <p class="capitalization-info">{{ t('bank.capitalRequirementInfo') }}</p>
        </section>
        <!-- ├ö├Â├ç├ö├Â├ç end liquidity panel ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->

        <!-- Depositors section -->
        <section v-if="bankInfo?.baseCapitalDeposited" class="depositors-section">
          <h2 class="section-title">{{ t('bank.bankDepositors') }}</h2>
          <div v-if="bankDeposits.length === 0" class="empty-state">
            <p>{{ t('bank.noBankDepositors') }}</p>
          </div>
          <div v-else class="depositors-table">
            <table>
              <thead>
                <tr>
                  <th>{{ t('common.company') }}</th>
                  <th>{{ t('bank.depositAmount') }}</th>
                  <th>{{ t('bank.depositInterestRate') }}</th>
                  <th>{{ t('bank.depositInterestEarned') }}</th>
                  <th>Type</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="dep in bankDeposits" :key="dep.id">
                  <td>{{ dep.depositorCompanyName }}</td>
                  <td>{{ formatCurrency(dep.amount, dep.cityCurrencyCode || cityCurrency) }}</td>
                  <td>{{ formatPercent(dep.depositInterestRatePercent) }}</td>
                  <td>{{ formatCurrency(dep.totalInterestPaid, dep.cityCurrencyCode || cityCurrency) }}</td>
                  <td>
                    <span v-if="dep.isBaseCapital" class="badge badge-info">{{ t('bank.baseCapital') }}</span>
                    <span v-else class="badge badge-success">Depositor</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Overview stats -->
        <div v-if="bankInfo?.baseCapitalDeposited" class="stats-row">
          <div class="stat-card">
            <span class="stat-label">Active Loans</span>
            <span class="stat-value">{{ issuedLoans.filter((l) => l.status === 'ACTIVE').length }}</span>
          </div>
          <div class="stat-card">
            <span class="stat-label">Capital Outstanding</span>
            <span class="stat-value">{{ fmt(totalIssuedCapacity) }}</span>
          </div>
          <div class="stat-card" :class="{ 'stat-card-warning': overdueLoans.length > 0 }">
            <span class="stat-label">Overdue/Defaulted</span>
            <span class="stat-value">{{ overdueLoans.length }}</span>
          </div>
          <div class="stat-card">
            <span class="stat-label">Expected Income/Payment</span>
            <span class="stat-value">{{ fmt(expectedMonthlyIncome) }}</span>
          </div>
        </div>

        <!-- Issued Loans -->
        <section v-if="bankInfo?.baseCapitalDeposited" class="loans-section">
          <h2 class="section-title">{{ t('bank.issuedLoans') }}</h2>
          <div v-if="issuedLoans.length === 0" class="empty-state">
            <p>{{ t('bank.noIssuedLoans') }}</p>
          </div>
          <div v-else class="loans-table">
            <table>
              <thead>
                <tr>
                  <th>{{ t('bank.borrower') }}</th>
                  <th>{{ t('bank.originalPrincipal') }}</th>
                  <th>{{ t('bank.remainingPrincipal') }}</th>
                  <th>{{ t('bank.paymentAmount') }}</th>
                  <th>Payments</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="loan in issuedLoans" :key="loan.id" :class="loanStatusClass(loan.status)">
                  <td>{{ loan.borrowerCompanyName }}</td>
                  <td>{{ fmt(loan.originalPrincipal) }}</td>
                  <td>{{ fmt(loan.remainingPrincipal) }}</td>
                  <td>{{ fmt(loan.paymentAmount) }}</td>
                  <td>{{ loan.paymentsMade }} / {{ loan.totalPayments }}</td>
                  <td>
                    <span class="loan-status-badge" :class="loanStatusClass(loan.status)">
                      {{ t(`bank.statusBadge.${loan.status}`) }}
                    </span>
                    <div v-if="loan.missedPayments > 0" class="missed-hint">{{ loan.missedPayments }} missed</div>
                    <div v-if="loan.collateralBuildingId" class="collateral-inline">
                      <span aria-hidden="true">┬ş─Ź─ć┼Ą</span> {{ loan.collateralBuildingName }}
                      <span v-if="loan.collateralAppraisedValue" class="collateral-inline-value"> ({{ fmt(loan.collateralAppraisedValue) }}) </span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section> </template
      ><!-- end owner view -->

      <!-- ├ö├Â├ç├ö├Â├ç CUSTOMER VIEW ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->
      <template v-else>
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
        <div class="flex flex-row gap-4">
          <!-- Account-style deposit relationship -->
          <section v-if="auth.isAuthenticated && isCompanyAccountActive" class="customer-account-section rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <div class="account-header flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div class="account-header-info flex flex-row gap-2">
                <h2 class="section-title text-2xl font-bold text-body grow">{{ t('bank.myAccount') }}</h2>
                <span
                  class="account-company-tag inline-flex w-fit items-center rounded-full border border-divider bg-card-raised px-3 py-1 text-xs font-semibold uppercase tracking-[0.12em] text-muted"
                >
                  {{ activeCompany?.name }}
                </span>
              </div>
              <div class="account-actions flex flex-wrap gap-3" v-if="hasCustomerAccount && myAccountBalance > 0">
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
                class="operating-account-row flex items-center justify-between gap-4 rounded-2xl border border-divider bg-card-raised px-5 py-4 shadow-sm"
              >
                <div class="flex flex-col gap-0.5">
                  <span class="text-xs text-muted">{{ t('bank.accountNumber') }}</span>
                  <span class="font-mono text-sm font-semibold text-body">{{ account.accountNumber }}</span>
                </div>
                <div class="flex flex-col items-end gap-0.5">
                  <span class="text-xs text-muted">{{ t('bank.accountBalance') }}</span>
                  <span class="text-base font-bold text-body">{{ formatCurrency(account.balance, account.currencyCode) }}</span>
                </div>
                <router-link :to="`/bank-statement/${account.companyId}`" class="btn btn-outline btn-sm shrink-0">
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
              <p class="account-empty-hint text-sm text-muted sm:text-base">{{ t('bank.openAccountHint', { rate: formatPercent(bankInfo?.depositInterestRatePercent ?? 0) }) }}</p>
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
          <section v-else class="customer-deposit-form-section rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.makeDeposit') }}</h2>
            <div v-if="!auth.isAuthenticated" class="auth-prompt mt-4 flex flex-col gap-4">
              <p>{{ t('bank.loginToLendDescription') }}</p>
              <router-link to="/login" class="btn btn-primary">{{ t('auth.login') }}</router-link>
            </div>
            <div v-else class="auth-prompt mt-4">
              <p>{{ t('bank.companyAccountRequired') }}</p>
            </div>
          </section>

          <!-- Direct loan request -->
          <section class="customer-loans-section rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.borrowFromThisBank') }}</h2>
            <div v-if="!directBorrowingOption" class="empty-state">
              <p>{{ t('bank.noOffersFromBank') }}</p>
            </div>
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

                <!-- Loan request: dedicated full-page form -->
                <div v-if="auth.isAuthenticated && isCompanyAccountActive && directBorrowingOption.remainingCapacity > 0">
                  <button class="btn btn-primary btn-sm" @click="router.push({ name: 'bank-loan-request', params: { buildingId: bankBuildingId } })">
                    {{ t('bank.acceptLoan') }}
                  </button>
                </div>
                <div v-else-if="!auth.isAuthenticated">
                  <router-link to="/login" class="btn btn-secondary btn-sm">{{ t('auth.login') }}</router-link>
                </div>
                <p v-else-if="!isCompanyAccountActive" class="offer-context-hint">{{ t('bank.companyAccountRequired') }}</p>
                <p v-else class="offer-context-hint muted">{{ t('bank.noCapacityAvailable') }}</p>
              </div>
            </div>
          </section>
        </div>
        <!-- My Loans at This Bank -->
        <section v-if="auth.isAuthenticated && myLoansHere.length > 0" class="my-loans-here-section mt-8 rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
          <h2 class="section-title text-2xl font-bold text-body">{{ t('bank.myLoans') }}</h2>
          <div class="loans-list mt-6 grid gap-4">
            <div v-for="loan in myLoansHere" :key="loan.id" class="loan-row rounded-2xl border border-divider bg-card-raised p-4 shadow-sm">
              <div class="loan-row-main">
                <span class="loan-amount">{{ fmt(loan.remainingPrincipal) }}</span>
                <span :class="['loan-status', loanStatusClass(loan.status)]">{{ loan.status }}</span>
              </div>
              <div v-if="loan.collateralBuildingId" class="collateral-badge">
                <span aria-hidden="true">┬ş─Ź─ć┼Ą</span> {{ t('bank.securedLoan') }}: {{ loan.collateralBuildingName }}
                <span v-if="loan.collateralAppraisedValue" class="collateral-badge-value"> ({{ fmt(loan.collateralAppraisedValue) }}) </span>
              </div>
            </div>
          </div>
        </section> </template
      ><!-- end customer view -->
    </template>
  </main>
</template>

<style scoped src="./BankManagementView.styles.css"></style>

