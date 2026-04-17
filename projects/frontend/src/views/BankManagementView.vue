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
import type { LoanOfferSummary, LoanSummary, BankDepositSummary, BankInfoSummary, Company } from '@/types'
import {
  formatLoanDuration,
  formatCurrency,
  formatPercent,
  loanStatusClass,
  computeCapacityUsedPercent,
  computeTotalRepayment,
  computePaymentAmount,
  computeTotalPayments,
} from '@/lib/loanHelpers'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const bankBuildingId = computed(() => route.params.buildingId as string)

const loading = ref(true)
const error = ref<string | null>(null)
const myOffers = ref<LoanOfferSummary[]>([])
const issuedLoans = ref<LoanSummary[]>([])
const bankDeposits = ref<BankDepositSummary[]>([])
const bankInfo = ref<BankInfoSummary | null>(null)
const userCompanies = ref<Company[]>([])

// Ownership detection — check if any user company owns this bank building
// (uses company buildings from me query, not bankInfo.lenderCompanyId)
const isOwner = computed(() => {
  if (!auth.isAuthenticated) return false
  return userCompanies.value.some((c) =>
    (c.buildings ?? []).some((b) => b.id === bankBuildingId.value && b.type === 'BANK'),
  )
})

// Customer-specific state
const bankLoanOffers = ref<LoanOfferSummary[]>([])
const myDepositsHere = ref<BankDepositSummary[]>([])
const customerDepositAmount = ref(10_000)
const customerDepositLoading = ref(false)
const customerDepositError = ref<string | null>(null)
const customerDepositSuccess = ref(false)
const customerLoanOffer = ref<LoanOfferSummary | null>(null)
const customerLoanPrincipal = ref(0)
const customerLoanLoading = ref(false)
const customerLoanError = ref<string | null>(null)
const customerLoanSuccess = ref(false)

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

// Publish offer form
const showPublishForm = ref(false)
const publishLoading = ref(false)
const publishError = ref<string | null>(null)
const offerForm = ref({
  annualInterestRatePercent: 10,
  maxPrincipalPerLoan: 50000,
  totalCapacity: 200000,
  durationTicks: 1440,
})

const MY_LOAN_OFFERS_QUERY = `
  {
    myLoanOffers {
      id
      bankBuildingId
      bankBuildingName
      cityId
      cityName
      lenderCompanyId
      lenderCompanyName
      annualInterestRatePercent
      maxPrincipalPerLoan
      totalCapacity
      usedCapacity
      remainingCapacity
      durationTicks
      isActive
      createdAtTick
      createdAtUtc
    }
  }
`

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

const PUBLISH_OFFER_MUTATION = `
  mutation PublishLoanOffer($input: PublishLoanOfferInput!) {
    publishLoanOffer(input: $input) {
      id
      isActive
      annualInterestRatePercent
    }
  }
`

const UPDATE_OFFER_MUTATION = `
  mutation UpdateLoanOffer($input: UpdateLoanOfferInput!) {
    updateLoanOffer(input: $input) {
      id
      isActive
      annualInterestRatePercent
    }
  }
`

const DEACTIVATE_OFFER_MUTATION = `
  mutation DeactivateLoanOffer($id: UUID!) {
    deactivateLoanOffer(loanOfferId: $id) {
      id
      isActive
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
    }
  }
`

const SET_BANK_RATES_MUTATION = `
  mutation SetBankRates($input: SetBankRatesInput!) {
    setBankRates(input: $input) {
      bankBuildingId
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

const PUBLIC_LOAN_OFFERS_QUERY = `
  {
    loanOffers {
      id
      bankBuildingId
      bankBuildingName
      lenderCompanyId
      lenderCompanyName
      annualInterestRatePercent
      maxPrincipalPerLoan
      totalCapacity
      usedCapacity
      remainingCapacity
      durationTicks
      isActive
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
    }
  }
`

const CREATE_DEPOSIT_MUTATION = `
  mutation CreateDeposit($input: CreateDepositInput!) {
    createDeposit(input: $input) {
      id
      amount
      depositInterestRatePercent
      isActive
    }
  }
`

const INITIATE_BASE_DEPOSIT_MUTATION = `
  mutation InitiateBaseDeposit($bankBuildingId: UUID!) {
    initiateBaseDeposit(bankBuildingId: $bankBuildingId) {
      bankBuildingId
      bankBuildingName
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

const ACCEPT_LOAN_MUTATION = `
  mutation AcceptLoan($input: AcceptLoanInput!) {
    acceptLoan(input: $input) {
      id
      status
      originalPrincipal
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
      auth.isAuthenticated
        ? gqlRequest<{ me: { companies: Company[] } }>(MY_COMPANIES_QUERY)
        : Promise.resolve({ me: { companies: [] } }),
    ])
    bankInfo.value = infoResult.bankInfo ?? null
    userCompanies.value = companiesResult.me?.companies ?? []

    const ownerDetected = userCompanies.value.some((c) =>
      (c.buildings ?? []).some((b) => b.id === bankBuildingId.value && b.type === 'BANK'),
    )

    if (ownerDetected) {
      // Owner view: load full management data
      const [offersResult, loansResult, depositsResult] = await Promise.all([
        gqlRequest<{ myLoanOffers: LoanOfferSummary[] }>(MY_LOAN_OFFERS_QUERY),
        gqlRequest<{ bankLoans: LoanSummary[] }>(BANK_LOANS_QUERY, {
          bankBuildingId: bankBuildingId.value,
        }),
        gqlRequest<{ bankDeposits: BankDepositSummary[] }>(BANK_DEPOSITS_QUERY, {
          id: bankBuildingId.value,
        }),
      ])
      const filtered = (offersResult.myLoanOffers ?? []).filter(
        (o) => o.bankBuildingId === bankBuildingId.value,
      )
      if (!deepEqual(myOffers.value, filtered)) {
        myOffers.value = filtered
      }
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
      // Customer view: load public loan offers for this bank, and my deposits here
      const [offersResult, depositsResult] = await Promise.all([
        gqlRequest<{ loanOffers: LoanOfferSummary[] }>(PUBLIC_LOAN_OFFERS_QUERY),
        auth.isAuthenticated
          ? gqlRequest<{ myDeposits: BankDepositSummary[] }>(MY_DEPOSITS_QUERY)
          : Promise.resolve({ myDeposits: [] }),
      ])
      const bankOffers = (offersResult.loanOffers ?? []).filter(
        (o) => o.bankBuildingId === bankBuildingId.value && o.isActive,
      )
      if (!deepEqual(bankLoanOffers.value, bankOffers)) {
        bankLoanOffers.value = bankOffers
      }
      const myDeposits = (depositsResult.myDeposits ?? []).filter(
        (d) => d.bankBuildingId === bankBuildingId.value,
      )
      if (!deepEqual(myDepositsHere.value, myDeposits)) {
        myDepositsHere.value = myDeposits
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

const totalIssuedCapacity = computed(() =>
  issuedLoans.value
    .filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE')
    .reduce((sum, l) => sum + l.remainingPrincipal, 0),
)

const expectedMonthlyIncome = computed(() =>
  issuedLoans.value
    .filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE')
    .reduce((sum, l) => sum + l.paymentAmount, 0),
)

async function publishOffer() {
  if (!bankBuildingId.value) return
  publishLoading.value = true
  publishError.value = null
  try {
    await gqlRequest(PUBLISH_OFFER_MUTATION, {
      input: {
        bankBuildingId: bankBuildingId.value,
        ...offerForm.value,
      },
    })
    showPublishForm.value = false
    offerForm.value = { annualInterestRatePercent: 10, maxPrincipalPerLoan: 50000, totalCapacity: 200000, durationTicks: 1440 }
    await loadData()
  } catch (err) {
    publishError.value = err instanceof Error ? err.message : String(err)
  } finally {
    publishLoading.value = false
  }
}

async function toggleOfferActive(offer: LoanOfferSummary) {
  try {
    if (offer.isActive) {
      await gqlRequest(DEACTIVATE_OFFER_MUTATION, { id: offer.id })
    } else {
      await gqlRequest(UPDATE_OFFER_MUTATION, {
        input: { loanOfferId: offer.id, isActive: true },
      })
    }
    await loadData()
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  }
}

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
    const result = await gqlRequest<{ initiateBaseDeposit: BankInfoSummary }>(
      INITIATE_BASE_DEPOSIT_MUTATION,
      { bankBuildingId: bankBuildingId.value },
    )
    bankInfo.value = result.initiateBaseDeposit
    baseDepositSuccess.value = true
    await loadData(true)
  } catch (err) {
    baseDepositError.value = err instanceof Error ? err.message : String(err)
  } finally {
    baseDepositLoading.value = false
  }
}

// ── Customer view helpers ─────────────────────────────────────────────────────

const activeCompany = computed(() => getActiveCompany(auth.player, userCompanies.value))
const isCompanyAccountActive = computed(
  () => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value,
)

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
        amount: customerDepositAmount.value,
      },
    })
    customerDepositSuccess.value = true
    await loadData()
    setTimeout(() => { customerDepositSuccess.value = false }, 3000)
  } catch (err) {
    customerDepositError.value = err instanceof Error ? err.message : String(err)
  } finally {
    customerDepositLoading.value = false
  }
}

async function submitCustomerLoan() {
  if (!customerLoanOffer.value || !activeCompany.value) return
  customerLoanLoading.value = true
  customerLoanError.value = null
  customerLoanSuccess.value = false
  try {
    await gqlRequest(ACCEPT_LOAN_MUTATION, {
      input: {
        loanOfferId: customerLoanOffer.value.id,
        borrowerCompanyId: activeCompany.value.id,
        principalAmount: customerLoanPrincipal.value,
      },
    })
    customerLoanSuccess.value = true
    customerLoanOffer.value = null
    await loadData()
    setTimeout(() => { customerLoanSuccess.value = false }, 3000)
  } catch (err) {
    customerLoanError.value = err instanceof Error ? err.message : String(err)
  } finally {
    customerLoanLoading.value = false
  }
}

function selectLoanOffer(offer: LoanOfferSummary) {
  customerLoanOffer.value = offer
  customerLoanPrincipal.value = Math.min(offer.maxPrincipalPerLoan, offer.remainingCapacity)
  customerLoanError.value = null
}

const estimatedCustomerTotalRepayment = computed(() => {
  if (!customerLoanOffer.value || customerLoanPrincipal.value <= 0) return 0
  return computeTotalRepayment(
    customerLoanPrincipal.value,
    customerLoanOffer.value.annualInterestRatePercent,
    customerLoanOffer.value.durationTicks,
  )
})

const estimatedCustomerPaymentAmount = computed(() => {
  if (!customerLoanOffer.value || customerLoanPrincipal.value <= 0) return 0
  return computePaymentAmount(
    customerLoanPrincipal.value,
    customerLoanOffer.value.annualInterestRatePercent,
    customerLoanOffer.value.durationTicks,
  )
})

const estimatedCustomerTotalPayments = computed(() => {
  if (!customerLoanOffer.value) return 0
  return computeTotalPayments(customerLoanOffer.value.durationTicks)
})
</script>

<template>
  <div class="bank-management-view">
    <div class="page-header">
      <!-- Show different titles based on ownership -->
      <template v-if="!loading && isOwner">
        <h1 class="page-title">{{ t('bank.configureBank') }}</h1>
        <p class="page-subtitle">{{ t('bank.lending') }}</p>
      </template>
      <template v-else-if="!loading">
        <div class="customer-nav">
          <button class="btn-back" @click="router.push('/loans')">← {{ t('bank.backToMarketplace') }}</button>
        </div>
        <h1 class="page-title">{{ bankInfo?.bankBuildingName ?? t('bank.customerView') }}</h1>
        <p class="page-subtitle">{{ bankInfo?.lenderCompanyName }} · {{ bankInfo?.cityName }}</p>
      </template>
      <template v-else>
        <h1 class="page-title">{{ t('bank.customerView') }}</h1>
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
      <!-- ── OWNER VIEW ─────────────────────────────────────────────── -->
      <template v-if="isOwner">

      <!-- ── Base Capital Deposit Required ────────────────────────── -->
      <div v-if="bankInfo && !bankInfo.baseCapitalDeposited" class="base-deposit-required">
        <div class="base-deposit-icon" aria-hidden="true">🏦</div>
        <div class="base-deposit-body">
          <h2 class="base-deposit-title">{{ t('bank.baseDepositRequired') }}</h2>
          <p class="base-deposit-description">{{ t('bank.baseDepositRequiredBody') }}</p>
          <p class="base-deposit-hint">{{ t('bank.baseCapitalRequired') }}</p>
        </div>
        <div v-if="baseDepositError" class="error-message">{{ baseDepositError }}</div>
        <div v-if="baseDepositSuccess" class="success-message">{{ t('bank.baseDepositSuccess') }}</div>
        <button
          class="btn btn-primary base-deposit-btn"
          :disabled="baseDepositLoading"
          @click="submitBaseDeposit"
        >
          {{ baseDepositLoading ? t('common.loading') : t('bank.makeBaseDeposit') }}
        </button>
      </div>

      <!-- Bank Info & Rate Configuration (only when activated) -->
      <div v-if="bankInfo && bankInfo.baseCapitalDeposited" class="bank-info-section">
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
              <input
                id="deposit-rate"
                v-model.number="ratesForm.depositInterestRatePercent"
                type="number"
                min="0"
                max="100"
                step="0.1"
                class="form-input"
              />
            </div>
            <div class="form-group">
              <label for="lending-rate">{{ t('bank.lendingInterestRate') }} (%)</label>
              <input
                id="lending-rate"
                v-model.number="ratesForm.lendingInterestRatePercent"
                type="number"
                min="0.1"
                max="200"
                step="0.1"
                class="form-input"
              />
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
            <span class="bank-stat-value">{{ formatCurrency(bankInfo.totalDeposits) }}</span>
          </div>
          <div class="bank-stat">
            <span class="bank-stat-label">{{ t('bank.lendableCapacity') }}</span>
            <span class="bank-stat-value">{{ formatCurrency(bankInfo.lendableCapacity) }}</span>
            <span class="bank-stat-hint">{{ t('bank.reserveInfo') }}</span>
          </div>
          <div class="bank-stat">
            <span class="bank-stat-label">{{ t('bank.availableLendingCapacity') }}</span>
            <span class="bank-stat-value" :class="bankInfo.availableLendingCapacity > 0 ? 'positive' : 'negative'">
              {{ formatCurrency(bankInfo.availableLendingCapacity) }}
            </span>
          </div>
        </div>
      </div>

      <!-- ── Liquidity Health Panel (owner view) ──────────────────────────── -->
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
              {{ formatCurrency(bankInfo.availableCash ?? 0) }}
            </span>
          </div>
          <div class="liquidity-stat">
            <span class="liquidity-stat-label">{{ t('bank.reserveRequirement') }}</span>
            <span class="liquidity-stat-value">{{ formatCurrency(bankInfo.reserveRequirement ?? 0) }}</span>
            <span class="liquidity-stat-hint">{{ t('bank.reserveInfo') }}</span>
          </div>
          <div class="liquidity-stat">
            <span class="liquidity-stat-label">{{ t('bank.reserveShortfall') }}</span>
            <span class="liquidity-stat-value" :class="(bankInfo.reserveShortfall ?? 0) > 0 ? 'negative' : 'positive'">
              {{ (bankInfo.reserveShortfall ?? 0) > 0 ? formatCurrency(bankInfo.reserveShortfall) : t('bank.noReserveShortfall') }}
            </span>
          </div>
          <div class="liquidity-stat" :class="{ 'liquidity-stat-warning': (bankInfo.centralBankDebt ?? 0) > 0 }">
            <span class="liquidity-stat-label">{{ t('bank.centralBankDebt') }}</span>
            <span class="liquidity-stat-value" :class="(bankInfo.centralBankDebt ?? 0) > 0 ? 'negative' : 'positive'">
              {{ (bankInfo.centralBankDebt ?? 0) > 0 ? formatCurrency(bankInfo.centralBankDebt) : '$0' }}
            </span>
            <span v-if="(bankInfo.centralBankDebt ?? 0) > 0" class="liquidity-stat-hint">
              {{ t('bank.centralBankRate') }}: {{ formatPercent(bankInfo.centralBankInterestRatePercent ?? 2) }} p.a.
            </span>
          </div>
        </div>

        <!-- Central-bank debt context -->
        <div v-if="(bankInfo.centralBankDebt ?? 0) > 0" class="central-bank-notice">
          <div class="notice-icon">⚠</div>
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
      <!-- ── end liquidity panel ─────────────────────────────────────────── -->

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
                <td>{{ formatCurrency(dep.amount) }}</td>
                <td>{{ formatPercent(dep.depositInterestRatePercent) }}</td>
                <td>{{ formatCurrency(dep.totalInterestPaid) }}</td>
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
          <span class="stat-value">{{ formatCurrency(totalIssuedCapacity) }}</span>
        </div>
        <div class="stat-card" :class="{ 'stat-card-warning': overdueLoans.length > 0 }">
          <span class="stat-label">Overdue/Defaulted</span>
          <span class="stat-value">{{ overdueLoans.length }}</span>
        </div>
        <div class="stat-card">
          <span class="stat-label">Expected Income/Payment</span>
          <span class="stat-value">{{ formatCurrency(expectedMonthlyIncome) }}</span>
        </div>
      </div>

      <!-- Loan Offers Management -->
      <section v-if="bankInfo?.baseCapitalDeposited" class="offers-section">
        <div class="section-header">
          <h2 class="section-title">{{ t('bank.loanOffers') }}</h2>
          <button class="btn btn-primary btn-sm" @click="showPublishForm = !showPublishForm">
            {{ showPublishForm ? t('common.cancel') : t('bank.publishOffer') }}
          </button>
        </div>

        <!-- Publish Form -->
        <div v-if="showPublishForm" class="publish-form">
          <h3>{{ t('bank.publishOffer') }}</h3>
          <div class="form-grid">
            <div class="form-group">
              <label for="offer-interest-rate">{{ t('bank.interestRate') }} (%)</label>
              <input id="offer-interest-rate" v-model.number="offerForm.annualInterestRatePercent" type="number" min="0.1" max="200" step="0.1" class="form-input" />
            </div>
            <div class="form-group">
              <label for="offer-max-principal">{{ t('bank.maxPrincipal') }} ($)</label>
              <input id="offer-max-principal" v-model.number="offerForm.maxPrincipalPerLoan" type="number" min="1000" step="1000" class="form-input" />
            </div>
            <div class="form-group">
              <label for="offer-total-capacity">{{ t('bank.totalCapacity') }} ($)</label>
              <input id="offer-total-capacity" v-model.number="offerForm.totalCapacity" type="number" min="1000" step="1000" class="form-input" />
            </div>
            <div class="form-group">
              <label for="offer-duration">{{ t('bank.durationTicks') }}</label>
              <input id="offer-duration" v-model.number="offerForm.durationTicks" type="number" min="24" max="87600" step="24" class="form-input" />
              <span class="form-hint">{{ formatLoanDuration(offerForm.durationTicks) }}</span>
            </div>
          </div>
          <div v-if="publishError" class="error-message">{{ publishError }}</div>
          <button class="btn btn-primary" :disabled="publishLoading" @click="publishOffer">
            {{ publishLoading ? t('common.loading') : t('bank.publishOffer') }}
          </button>
        </div>

        <!-- Offers list -->
        <div v-if="myOffers.length === 0" class="empty-state">
          <p>No loan offers published for this bank yet.</p>
        </div>
        <div v-else class="offers-table">
          <table>
            <thead>
              <tr>
                <th>{{ t('bank.interestRate') }}</th>
                <th>{{ t('bank.maxPrincipal') }}</th>
                <th>{{ t('bank.remainingCapacity') }}</th>
                <th>{{ t('bank.duration') }}</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="offer in myOffers" :key="offer.id">
                <td>{{ formatPercent(offer.annualInterestRatePercent) }}</td>
                <td>{{ formatCurrency(offer.maxPrincipalPerLoan) }}</td>
                <td>
                  {{ formatCurrency(offer.remainingCapacity) }}
                  <div class="capacity-bar">
                    <div class="capacity-fill" :style="{ width: `${computeCapacityUsedPercent(offer)}%` }" />
                  </div>
                </td>
                <td>{{ formatLoanDuration(offer.durationTicks) }}</td>
                <td>
                  <span class="status-pill" :class="offer.isActive ? 'status-active' : 'status-inactive'">
                    {{ offer.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td>
                  <button class="btn btn-sm btn-secondary" @click="toggleOfferActive(offer)">
                    {{ offer.isActive ? t('bank.deactivateOffer') : t('bank.activateOffer') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

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
                <td>{{ formatCurrency(loan.originalPrincipal) }}</td>
                <td>{{ formatCurrency(loan.remainingPrincipal) }}</td>
                <td>{{ formatCurrency(loan.paymentAmount) }}</td>
                <td>{{ loan.paymentsMade }} / {{ loan.totalPayments }}</td>
                <td>
                  <span class="loan-status-badge" :class="loanStatusClass(loan.status)">
                    {{ t(`bank.statusBadge.${loan.status}`) }}
                  </span>
                  <div v-if="loan.missedPayments > 0" class="missed-hint">
                    {{ loan.missedPayments }} missed
                  </div>
                  <div v-if="loan.collateralBuildingId" class="collateral-inline">
                    🏛 {{ loan.collateralBuildingName }}
                    <span v-if="loan.collateralAppraisedValue" class="collateral-inline-value">
                      ({{ formatCurrency(loan.collateralAppraisedValue) }})
                    </span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      </template><!-- end owner view -->

      <!-- ── CUSTOMER VIEW ──────────────────────────────────────────── -->
      <template v-else>

        <!-- Bank profile card (rates + capacity) -->
        <div v-if="bankInfo" class="customer-bank-profile">
          <div class="customer-rates-grid">
            <div class="customer-rate-card deposit">
              <span class="customer-rate-label">{{ t('bank.depositInterestRate') }}</span>
              <span class="customer-rate-value">{{ formatPercent(bankInfo.depositInterestRatePercent) }}</span>
              <span class="customer-rate-hint">{{ t('bank.perYear') }}</span>
            </div>
            <div class="customer-rate-card lending">
              <span class="customer-rate-label">{{ t('bank.lendingInterestRate') }}</span>
              <span class="customer-rate-value">{{ formatPercent(bankInfo.lendingInterestRatePercent) }}</span>
              <span class="customer-rate-hint">{{ t('bank.perYear') }}</span>
            </div>
            <div class="customer-rate-card capacity">
              <span class="customer-rate-label">{{ t('bank.availableLendingCapacity') }}</span>
              <span class="customer-rate-value" :class="bankInfo.availableLendingCapacity > 0 ? 'positive' : 'muted'">
                {{ formatCurrency(bankInfo.availableLendingCapacity) }}
              </span>
              <span class="customer-rate-hint">{{ t('bank.reserveInfo') }}</span>
            </div>
          </div>
        </div>

        <!-- My deposits at this bank -->
        <section v-if="auth.isAuthenticated && myDepositsHere.length > 0" class="customer-deposits-section">
          <h2 class="section-title">{{ t('bank.yourDepositsHere') }}</h2>
          <div class="deposits-list">
            <div v-for="dep in myDepositsHere" :key="dep.id" class="deposit-card">
              <div class="deposit-card-header">
                <span class="deposit-company">{{ dep.depositorCompanyName }}</span>
                <span class="deposit-rate-badge">{{ formatPercent(dep.depositInterestRatePercent) }} p.a.</span>
              </div>
              <div class="deposit-stats">
                <div class="deposit-stat">
                  <span class="deposit-stat-label">{{ t('bank.depositAmount') }}</span>
                  <span class="deposit-stat-value">{{ formatCurrency(dep.amount) }}</span>
                </div>
                <div class="deposit-stat">
                  <span class="deposit-stat-label">{{ t('bank.depositInterestEarned') }}</span>
                  <span class="deposit-stat-value positive">+{{ formatCurrency(dep.totalInterestPaid) }}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- Deposit form -->
        <section class="customer-deposit-form-section">
          <h2 class="section-title">{{ t('bank.makeDeposit') }}</h2>
          <div v-if="!auth.isAuthenticated" class="auth-prompt">
            <p>{{ t('bank.loginToLendDescription') }}</p>
            <router-link to="/login" class="btn btn-primary">{{ t('auth.login') }}</router-link>
          </div>
          <div v-else-if="!isCompanyAccountActive" class="auth-prompt">
            <p>{{ t('bank.companyAccountRequired') }}</p>
          </div>
          <div v-else class="customer-deposit-form">
            <div class="form-group">
              <label>{{ t('common.company') }}</label>
              <div class="active-company-display">
                <strong>{{ activeCompany?.name }}</strong>
                <span>{{ formatCurrency(activeCompany?.cash ?? 0) }}</span>
              </div>
            </div>
            <div class="form-group">
              <label for="customer-deposit-amount">{{ t('bank.depositAmount') }}</label>
              <input
                id="customer-deposit-amount"
                v-model.number="customerDepositAmount"
                type="number"
                min="1000"
                step="1000"
                class="form-input"
              />
              <span class="form-hint">{{ t('bank.depositAmountHint') }}</span>
            </div>
            <div v-if="bankInfo" class="repayment-preview">
              <div class="preview-row">
                <span>{{ t('bank.depositInterestRate') }}</span>
                <strong>{{ formatPercent(bankInfo.depositInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
              </div>
            </div>
            <div v-if="customerDepositSuccess" class="success-message">{{ t('bank.depositCreated') }}</div>
            <div v-if="customerDepositError" class="error-message">{{ customerDepositError }}</div>
            <button
              class="btn btn-primary"
              :disabled="customerDepositLoading || customerDepositAmount < 1000"
              @click="submitCustomerDeposit"
            >
              {{ customerDepositLoading ? t('common.loading') : t('bank.confirmDeposit') }}
            </button>
          </div>
        </section>

        <!-- Available loan offers -->
        <section class="customer-loans-section">
          <h2 class="section-title">{{ t('bank.loanOffers') }}</h2>
          <div v-if="bankLoanOffers.length === 0" class="empty-state">
            <p>{{ t('bank.noOffersFromBank') }}</p>
          </div>
          <div v-else class="customer-offers-grid">
            <div v-for="offer in bankLoanOffers" :key="offer.id" class="customer-offer-card">
              <div class="customer-offer-header">
                <span class="offer-rate-big">{{ formatPercent(offer.annualInterestRatePercent) }}</span>
                <span class="offer-rate-hint">{{ t('bank.perYear') }}</span>
              </div>
              <div class="customer-offer-stats">
                <div class="offer-stat-row">
                  <span>{{ t('bank.maxPrincipal') }}</span>
                  <strong>{{ formatCurrency(offer.maxPrincipalPerLoan) }}</strong>
                </div>
                <div class="offer-stat-row">
                  <span>{{ t('bank.remainingCapacity') }}</span>
                  <strong :class="offer.remainingCapacity > 0 ? 'positive' : 'muted'">
                    {{ formatCurrency(offer.remainingCapacity) }}
                  </strong>
                </div>
                <div class="offer-stat-row">
                  <span>{{ t('bank.duration') }}</span>
                  <strong>{{ formatLoanDuration(offer.durationTicks) }}</strong>
                </div>
              </div>

              <!-- Loan request form for selected offer -->
              <div v-if="customerLoanOffer?.id === offer.id" class="customer-loan-request">
                <div class="form-group">
                  <label for="customer-loan-amount">{{ t('bank.principalAmount') }}</label>
                  <input
                    id="customer-loan-amount"
                    v-model.number="customerLoanPrincipal"
                    type="number"
                    :min="1000"
                    :max="Math.min(offer.maxPrincipalPerLoan, offer.remainingCapacity)"
                    step="1000"
                    class="form-input"
                  />
                </div>
                <div class="repayment-preview">
                  <div class="preview-row">
                    <span>{{ t('bank.paymentAmount') }}</span>
                    <strong>{{ formatCurrency(estimatedCustomerPaymentAmount) }} × {{ estimatedCustomerTotalPayments }}</strong>
                  </div>
                  <div class="preview-row total-row">
                    <span>{{ t('bank.totalRepayment') }}</span>
                    <strong>{{ formatCurrency(estimatedCustomerTotalRepayment) }}</strong>
                  </div>
                </div>
                <p class="risk-warning">⚠ {{ t('bank.riskWarning') }}</p>
                <div v-if="customerLoanSuccess" class="success-message">Loan accepted successfully.</div>
                <div v-if="customerLoanError" class="error-message">{{ customerLoanError }}</div>
                <div class="loan-request-actions">
                  <button class="btn btn-secondary btn-sm" @click="customerLoanOffer = null">{{ t('common.cancel') }}</button>
                  <button
                    class="btn btn-primary"
                    :disabled="customerLoanLoading || customerLoanPrincipal <= 0"
                    @click="submitCustomerLoan"
                  >
                    {{ customerLoanLoading ? t('common.loading') : t('bank.acceptLoan') }}
                  </button>
                </div>
              </div>

              <div v-else-if="auth.isAuthenticated && isCompanyAccountActive && offer.remainingCapacity > 0">
                <button class="btn btn-primary btn-sm" @click="selectLoanOffer(offer)">
                  {{ t('bank.acceptLoan') }}
                </button>
              </div>
              <div v-else-if="!auth.isAuthenticated">
                <router-link to="/login" class="btn btn-secondary btn-sm">{{ t('auth.login') }}</router-link>
              </div>
              <p v-else-if="!isCompanyAccountActive" class="offer-context-hint">{{ t('bank.companyAccountRequired') }}</p>
              <p v-else class="offer-context-hint muted">No capacity available</p>
            </div>
          </div>
        </section>

      </template><!-- end customer view -->

    </template>
  </div>
</template>

<style scoped>
.bank-management-view {
  max-width: 1100px;
  margin: 0 auto;
  padding: var(--spacing-lg);
}

.page-header {
  margin-bottom: var(--spacing-xl);
}

.page-title {
  font-size: 1.8rem;
  font-weight: 700;
}

.page-subtitle {
  color: var(--color-text-secondary);
}

.loading-state,
.empty-state,
.error-state {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  padding: var(--spacing-xl);
  color: var(--color-text-secondary);
  justify-content: center;
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-xl);
}

.stat-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
}

.stat-card-warning {
  border-color: var(--color-warning, #f59e0b);
}

.stat-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  color: var(--color-text-secondary);
  letter-spacing: 0.05em;
}

.stat-value {
  font-size: 1.4rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--spacing-md);
}

.section-title {
  font-size: 1.2rem;
  font-weight: 600;
}

.offers-section,
.loans-section {
  margin-bottom: var(--spacing-xl);
}

.publish-form {
  background: var(--color-background, #f9fafb);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
}

.form-group label {
  font-size: 0.85rem;
  font-weight: 500;
}

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

.offers-table,
.loans-table {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

th, td {
  padding: var(--spacing-sm);
  text-align: left;
  border-bottom: 1px solid var(--color-border);
}

th {
  font-weight: 600;
  color: var(--color-text-secondary);
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.capacity-bar {
  height: 4px;
  background: var(--color-border);
  border-radius: 2px;
  margin-top: 4px;
  overflow: hidden;
}

.capacity-fill {
  height: 100%;
  background: var(--color-primary, #3b82f6);
  border-radius: 2px;
  transition: width 0.3s;
}

.status-pill {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
}

.status-pill.status-active {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}

.status-pill.status-inactive {
  background: rgba(148, 163, 184, 0.12);
  color: #94a3b8;
}

.loan-status-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
}

.loan-status-badge.status-active {
  background: rgba(52, 211, 153, 0.15);
  color: #4ade80;
}

.loan-status-badge.status-overdue {
  background: rgba(251, 191, 36, 0.15);
  color: #fbbf24;
}

.loan-status-badge.status-defaulted {
  background: rgba(248, 113, 113, 0.15);
  color: #f87171;
}

.loan-status-badge.status-repaid {
  background: rgba(96, 165, 250, 0.15);
  color: #60a5fa;
}

.missed-hint {
  font-size: 0.7rem;
  color: #fbbf24;
  margin-top: 2px;
}

.collateral-inline {
  font-size: 0.7rem;
  color: var(--color-primary, #3b82f6);
  margin-top: 2px;
}

.collateral-inline-value {
  color: var(--color-text-secondary);
}

.error-message {
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  font-size: 0.85rem;
  margin-bottom: var(--spacing-sm);
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--spacing-xs) var(--spacing-md);
  border-radius: var(--radius-sm);
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: background-color 0.2s;
}

.btn-sm {
  padding: 4px var(--spacing-sm);
  font-size: 0.8rem;
}

.btn-primary {
  background: var(--color-primary, #3b82f6);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--color-primary-hover, #2563eb);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--color-surface);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border);
}

.spinner {
  width: 20px;
  height: 20px;
  border: 2px solid var(--color-border);
  border-top-color: var(--color-primary, #3b82f6);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Bank info and rate configuration */
.bank-info-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.bank-info-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.bank-info-header h2 {
  font-size: 1.1rem;
  font-weight: 600;
  margin: 0;
}

.bank-stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 1rem;
  margin-top: 1rem;
}

.bank-stat {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 0.75rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.bank-stat-label {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.bank-stat-value {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--color-text);
}

.bank-stat-value.deposit-rate { color: var(--color-success, #22c55e); }
.bank-stat-value.lending-rate { color: var(--color-warning, #f59e0b); }
.bank-stat-value.positive { color: var(--color-success, #22c55e); }
.bank-stat-value.negative { color: var(--color-error, #ef4444); }

.bank-stat-hint {
  font-size: 0.72rem;
  color: var(--color-text-muted);
}

.rates-form {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 1rem;
  margin-bottom: 1rem;
}

/* Depositors section */
.depositors-section {
  margin-bottom: 1.5rem;
}

.depositors-table table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.depositors-table th,
.depositors-table td {
  padding: 0.5rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
}

.depositors-table th {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 600;
}

.badge-info {
  background: var(--color-primary-bg, rgba(99, 102, 241, 0.15));
  color: var(--color-primary, #6366f1);
}

.badge-success {
  background: rgba(34, 197, 94, 0.15);
  color: #22c55e;
}

.success-message {
  color: var(--color-success, #22c55e);
  font-size: 0.875rem;
  padding: 0.5rem 0;
}

/* Customer view styles */
.customer-nav {
  margin-bottom: 0.5rem;
}

.btn-back {
  background: none;
  border: none;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 0.875rem;
  padding: 0;
  text-decoration: underline;
}

.btn-back:hover {
  color: var(--color-text-primary);
}

.customer-bank-profile {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.customer-rates-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 1rem;
}

.customer-rate-card {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1rem 1.25rem;
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

.customer-rate-card.deposit .customer-rate-value { color: #22c55e; }
.customer-rate-card.lending .customer-rate-value { color: #f59e0b; }
.customer-rate-card.capacity .customer-rate-value.positive { color: #22c55e; }
.customer-rate-card.capacity .customer-rate-value.muted { color: var(--color-text-muted); }

.customer-rate-hint {
  font-size: 0.8rem;
  color: var(--color-text-muted);
}

.customer-deposits-section {
  margin-bottom: 1.5rem;
}

.deposits-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.deposit-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 0.875rem 1rem;
}

.deposit-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.deposit-company {
  font-weight: 600;
}

.deposit-rate-badge {
  font-size: 0.82rem;
  background: rgba(34, 197, 94, 0.12);
  color: #22c55e;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-weight: 600;
}

.deposit-stats {
  display: flex;
  gap: 1.5rem;
}

.deposit-stat {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.deposit-stat-label {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.deposit-stat-value {
  font-weight: 600;
  font-size: 0.95rem;
}

.deposit-stat-value.positive { color: #22c55e; }

.customer-deposit-form-section,
.customer-loans-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 1.25rem 1.5rem;
  margin-bottom: 1.5rem;
}

.auth-prompt {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  align-items: flex-start;
  padding: 0.75rem 0;
}

.customer-deposit-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 420px;
}

.active-company-display {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 4px;
  font-size: 0.9rem;
}

.repayment-preview {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 0.75rem 1rem;
}

.preview-row {
  display: flex;
  justify-content: space-between;
  font-size: 0.875rem;
  padding: 0.2rem 0;
}

.preview-row.total-row {
  border-top: 1px solid var(--color-border);
  margin-top: 0.25rem;
  padding-top: 0.25rem;
  font-weight: 600;
}

.customer-offers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
  margin-top: 0.75rem;
}

.customer-offer-card {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.customer-offer-header {
  display: flex;
  align-items: baseline;
  gap: 0.4rem;
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
}

.offer-stat-row {
  display: flex;
  justify-content: space-between;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.offer-stat-row strong.positive { color: #22c55e; }
.offer-stat-row strong.muted { color: var(--color-text-muted); }

.customer-loan-request {
  border-top: 1px solid var(--color-border);
  padding-top: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.risk-warning {
  font-size: 0.78rem;
  color: #f59e0b;
}

.loan-request-actions {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

.offer-context-hint {
  font-size: 0.82rem;
  color: var(--color-text-muted);
}

.offer-context-hint.muted {
  font-style: italic;
}

/* ── Liquidity Health Panel ──────────────────────────────────────────── */
.liquidity-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--spacing-lg);
  margin-bottom: var(--spacing-xl);
}

.liquidity-status-banner {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
  padding: var(--spacing-sm) var(--spacing-md);
  border-radius: var(--radius-sm);
  margin-bottom: var(--spacing-md);
  font-size: 0.9rem;
}

.liquidity-healthy {
  background: rgba(34, 197, 94, 0.1);
  border: 1px solid rgba(34, 197, 94, 0.3);
  color: #16a34a;
}

.liquidity-pressured {
  background: rgba(245, 158, 11, 0.1);
  border: 1px solid rgba(245, 158, 11, 0.3);
  color: #b45309;
}

.liquidity-critical {
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  color: #dc2626;
}

.liquidity-status-label {
  font-weight: 700;
  white-space: nowrap;
}

.liquidity-status-hint {
  font-size: 0.82rem;
}

.liquidity-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

.liquidity-stat {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  background: var(--color-surface-secondary, rgba(0,0,0,0.03));
}

.liquidity-stat-warning {
  border: 1px solid rgba(239, 68, 68, 0.3);
  background: rgba(239, 68, 68, 0.05);
}

.liquidity-stat-label {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
}

.liquidity-stat-value {
  font-size: 1.1rem;
  font-weight: 600;
}

.liquidity-stat-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.central-bank-notice {
  display: flex;
  gap: var(--spacing-sm);
  background: rgba(239, 68, 68, 0.07);
  border: 1px solid rgba(239, 68, 68, 0.25);
  border-radius: var(--radius-sm);
  padding: var(--spacing-md);
  margin-bottom: var(--spacing-md);
  font-size: 0.85rem;
}

.notice-icon {
  font-size: 1.1rem;
  flex-shrink: 0;
}

.notice-body p {
  margin: 0.25rem 0 0;
  color: var(--color-text-secondary);
}

.recommended-actions {
  margin-top: var(--spacing-md);
  padding: var(--spacing-md);
  background: rgba(245, 158, 11, 0.06);
  border: 1px solid rgba(245, 158, 11, 0.2);
  border-radius: var(--radius-sm);
}

.actions-title {
  font-size: 0.85rem;
  font-weight: 600;
  margin: 0 0 var(--spacing-xs);
}

.actions-list {
  margin: 0;
  padding-left: 1.25rem;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  line-height: 1.7;
}

.capitalization-info {
  margin-top: var(--spacing-md);
  font-size: 0.78rem;
  color: var(--color-text-muted);
  font-style: italic;
}

/* ── Base Capital Deposit Required ───────────────────────────────────────── */
.base-deposit-required {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--spacing-md);
  padding: var(--spacing-xl);
  background: var(--color-surface-secondary, rgba(0, 0, 0, 0.03));
  border: 2px dashed var(--color-border, rgba(0, 0, 0, 0.15));
  border-radius: var(--radius-md);
  text-align: center;
  margin-bottom: var(--spacing-lg);
}

.base-deposit-icon {
  font-size: 3rem;
}

.base-deposit-title {
  font-size: 1.25rem;
  font-weight: 700;
  margin: 0;
}

.base-deposit-description {
  color: var(--color-text-secondary);
  margin: 0;
  max-width: 480px;
}

.base-deposit-hint {
  font-size: 0.85rem;
  color: var(--color-text-muted);
  margin: 0;
}

.base-deposit-btn {
  min-width: 260px;
  font-size: 1rem;
  padding: var(--spacing-sm) var(--spacing-lg);
}
</style>
