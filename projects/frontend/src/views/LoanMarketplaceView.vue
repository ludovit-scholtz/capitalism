<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import type {
  LoanOfferSummary,
  LoanSummary,
  Company,
  BankDepositSummary,
  BankInfoSummary,
  CollateralEligibilitySummary,
} from '@/types'
import {
  formatLoanDuration,
  computeTotalRepayment,
  computePaymentAmount,
  computeTotalPayments,
  loanStatusClass,
  formatCurrency,
  formatPercent,
} from '@/lib/loanHelpers'

const { t } = useI18n()
const auth = useAuthStore()
const router = useRouter()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()
const loading = ref(true)
const error = ref<string | null>(null)
const offers = ref<LoanOfferSummary[]>([])
const myLoans = ref<LoanSummary[]>([])
const myCompanies = ref<Company[]>([])

// Active tab: 'borrow' | 'deposit'
const activeTab = ref<'borrow' | 'deposit'>('borrow')

// Banks list (for deposit tab)
const allBanks = ref<BankInfoSummary[]>([])
const myDeposits = ref<BankDepositSummary[]>([])

// Sort/filter state for bank list
type BankSortField = 'depositRate' | 'lendingRate' | 'capacity' | 'city'
const bankSortBy = ref<BankSortField>('depositRate')
const bankSortDir = ref<'asc' | 'desc'>('desc')
const bankCityFilter = ref('')
const bankShowAvailableOnly = ref(false)

// Deposit modal state
const showDepositModal = ref(false)
const selectedBank = ref<BankInfoSummary | null>(null)
const depositCompanyId = ref('')
const depositAmount = ref(10000)
const depositLoading = ref(false)
const depositError = ref<string | null>(null)
const depositSuccess = ref(false)

// Accept modal state
const showAcceptModal = ref(false)
const selectedOffer = ref<LoanOfferSummary | null>(null)
const selectedCompanyId = ref('')
const principalAmount = ref(0)
const acceptLoading = ref(false)
const acceptError = ref<string | null>(null)

// Collateral selection state
const collateralBuildings = ref<CollateralEligibilitySummary[]>([])
const selectedCollateralBuildingId = ref<string | null>(null)
const collateralLoadError = ref<string | null>(null)

const LOAN_OFFERS_QUERY = `
  {
    loanOffers {
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

const MY_LOANS_QUERY = `
  {
    myLoans {
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

const MY_COMPANIES_QUERY = `
  {
    me {
      companies {
        id
        name
        cash
        buildings {
          id
          type
          name
        }
      }
    }
  }
`

const ACCEPT_LOAN_MUTATION = `
  mutation AcceptLoan($input: AcceptLoanInput!) {
    acceptLoan(input: $input) {
      id
      status
      originalPrincipal
      remainingPrincipal
      paymentAmount
      totalPayments
      collateralBuildingId
      collateralAppraisedValue
    }
  }
`

const ALL_BANKS_QUERY = `
  {
    allBanks {
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

const WITHDRAW_DEPOSIT_MUTATION = `
  mutation WithdrawDeposit($input: WithdrawDepositInput!) {
    withdrawDeposit(input: $input) {
      id
      amount
      isActive
    }
  }
`

async function loadData(isRefresh = false) {
  if (!isRefresh) {
    loading.value = true
  }
  error.value = null
  try {
    if (auth.isAuthenticated && !auth.player) {
      await auth.fetchMe()
    }

    const [offersResult, banksResult] = await Promise.all([
      gqlRequest<{ loanOffers: LoanOfferSummary[] }>(LOAN_OFFERS_QUERY),
      gqlRequest<{ allBanks: BankInfoSummary[] }>(ALL_BANKS_QUERY),
    ])
    const newOffers = offersResult.loanOffers ?? []
    if (!deepEqual(offers.value, newOffers)) {
      offers.value = newOffers
    }
    const newBanks = banksResult.allBanks ?? []
    if (!deepEqual(allBanks.value, newBanks)) {
      allBanks.value = newBanks
    }

    if (auth.isAuthenticated) {
      const [loansResult, companiesResult, depositsResult] = await Promise.all([
        gqlRequest<{ myLoans: LoanSummary[] }>(MY_LOANS_QUERY),
        gqlRequest<{ me: { companies: Company[] } }>(MY_COMPANIES_QUERY),
        gqlRequest<{ myDeposits: BankDepositSummary[] }>(MY_DEPOSITS_QUERY),
      ])
      const newLoans = loansResult.myLoans ?? []
      if (!deepEqual(myLoans.value, newLoans)) {
        myLoans.value = newLoans
      }
      myCompanies.value = companiesResult.me?.companies ?? []
      const newDeposits = depositsResult.myDeposits ?? []
      if (!deepEqual(myDeposits.value, newDeposits)) {
        myDeposits.value = newDeposits
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

const activeLoans = computed(() => myLoans.value.filter((l) => l.status === 'ACTIVE' || l.status === 'OVERDUE'))
const activeCompany = computed(() => getActiveCompany(auth.player, myCompanies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)

// Lender eligibility: detect BANK buildings across all companies
const myBankBuildings = computed(() =>
  myCompanies.value.flatMap((c) => (c.buildings ?? []).filter((b) => b.type === 'BANK').map((b) => ({ ...b, companyId: c.id }))),
)
const hasBankBuilding = computed(() => myBankBuildings.value.length > 0)
const firstBankBuilding = computed(() => myBankBuildings.value[0] ?? null)
const firstCompanyId = computed(() => myCompanies.value[0]?.id ?? null)

// Bank list sort/filter computeds
const availableBankCities = computed(() => {
  const cities = new Set(allBanks.value.map((b) => b.cityName))
  return [...cities].sort()
})

const filteredAndSortedBanks = computed(() => {
  let banks = allBanks.value
  if (bankCityFilter.value) {
    banks = banks.filter((b) => b.cityName === bankCityFilter.value)
  }
  if (bankShowAvailableOnly.value) {
    banks = banks.filter((b) => b.availableLendingCapacity > 0)
  }
  return [...banks].sort((a, b) => {
    let aVal: number | string
    let bVal: number | string
    if (bankSortBy.value === 'depositRate') {
      aVal = a.depositInterestRatePercent
      bVal = b.depositInterestRatePercent
    } else if (bankSortBy.value === 'lendingRate') {
      aVal = a.lendingInterestRatePercent
      bVal = b.lendingInterestRatePercent
    } else if (bankSortBy.value === 'capacity') {
      aVal = a.availableLendingCapacity
      bVal = b.availableLendingCapacity
    } else {
      aVal = a.cityName
      bVal = b.cityName
    }
    const dir = bankSortDir.value === 'asc' ? 1 : -1
    return aVal < bVal ? -dir : aVal > bVal ? dir : 0
  })
})

function toggleBankSort(field: BankSortField) {
  if (bankSortBy.value === field) {
    bankSortDir.value = bankSortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    bankSortBy.value = field
    bankSortDir.value = 'desc'
  }
}

function navigateToAcquireBank() {
  if (firstCompanyId.value) {
    router.push(`/buy-building/${firstCompanyId.value}`)
  } else {
    router.push('/dashboard')
  }
}

// Banks sorted for the borrow section (by lending rate ascending = lowest interest first)
const sortedBanksForBorrow = computed(() =>
  [...allBanks.value]
    .filter((b) => b.baseCapitalDeposited && b.availableLendingCapacity > 0)
    .sort((a, b) => a.lendingInterestRatePercent - b.lendingInterestRatePercent),
)

function navigateToManageBank() {
  if (firstBankBuilding.value) {
    router.push(`/bank/${firstBankBuilding.value.id}`)
  }
}

function navigateToBank(bankBuildingId: string) {
  router.push(`/bank/${bankBuildingId}`)
}

function closeAcceptModal() {
  showAcceptModal.value = false
  selectedOffer.value = null
  acceptError.value = null
  selectedCollateralBuildingId.value = null
  collateralBuildings.value = []
}

const estimatedTotalRepayment = computed(() => {
  if (!selectedOffer.value || principalAmount.value <= 0) return 0
  return computeTotalRepayment(principalAmount.value, selectedOffer.value.annualInterestRatePercent, selectedOffer.value.durationTicks)
})

const estimatedPaymentAmount = computed(() => {
  if (!selectedOffer.value || principalAmount.value <= 0) return 0
  return computePaymentAmount(principalAmount.value, selectedOffer.value.annualInterestRatePercent, selectedOffer.value.durationTicks)
})

const estimatedTotalPayments = computed(() => {
  if (!selectedOffer.value) return 0
  return computeTotalPayments(selectedOffer.value.durationTicks)
})

const selectedCompanyCash = computed(() => {
  const company = myCompanies.value.find((c) => c.id === selectedCompanyId.value)
  return company?.cash ?? 0
})

const selectedCollateral = computed(() =>
  collateralBuildings.value.find((b) => b.buildingId === selectedCollateralBuildingId.value) ?? null,
)

const collateralCapacityWarning = computed(() => {
  if (!selectedCollateral.value || principalAmount.value <= 0) return null
  if (principalAmount.value > selectedCollateral.value.remainingBorrowingCapacity) {
    return t('bank.collateralExceedsLimit')
  }
  return null
})

async function confirmAcceptLoan() {
  if (!selectedOffer.value || !selectedCompanyId.value || principalAmount.value <= 0) return
  acceptLoading.value = true
  acceptError.value = null
  try {
    await gqlRequest(ACCEPT_LOAN_MUTATION, {
      input: {
        loanOfferId: selectedOffer.value.id,
        borrowerCompanyId: selectedCompanyId.value,
        principalAmount: principalAmount.value,
        collateralBuildingId: selectedCollateralBuildingId.value ?? undefined,
      },
    })
    closeAcceptModal()
    await loadData()
  } catch (err) {
    acceptError.value = err instanceof Error ? err.message : String(err)
  } finally {
    acceptLoading.value = false
  }
}

// ── Deposit functions ─────────────────────────────────────────────────────────

function openDepositModal(bank: BankInfoSummary) {
  selectedBank.value = bank
  depositCompanyId.value = activeCompany.value?.id ?? ''
  depositAmount.value = 10_000
  depositError.value = null
  depositSuccess.value = false
  showDepositModal.value = true
}

function closeDepositModal() {
  showDepositModal.value = false
  selectedBank.value = null
  depositError.value = null
  depositSuccess.value = false
}

async function submitDeposit() {
  if (!selectedBank.value || !depositCompanyId.value) return
  depositLoading.value = true
  depositError.value = null
  depositSuccess.value = false
  try {
    await gqlRequest(CREATE_DEPOSIT_MUTATION, {
      input: {
        bankBuildingId: selectedBank.value.bankBuildingId,
        depositorCompanyId: depositCompanyId.value,
        amount: depositAmount.value,
      },
    })
    depositSuccess.value = true
    await loadData()
    setTimeout(closeDepositModal, 1500)
  } catch (err) {
    depositError.value = err instanceof Error ? err.message : String(err)
  } finally {
    depositLoading.value = false
  }
}

async function withdrawDeposit(deposit: BankDepositSummary) {
  if (!confirm(t('bank.confirmWithdraw'))) return
  try {
    await gqlRequest(WITHDRAW_DEPOSIT_MUTATION, {
      input: { depositId: deposit.id, amount: deposit.amount },
    })
    await loadData()
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  }
}
</script>

<template>
  <div class="loan-marketplace-view">
    <div class="page-header">
      <h1 class="page-title">{{ t('bank.loanOffers') }}</h1>
      <p class="page-subtitle">{{ t('bank.browseMarketplace') }}</p>
    </div>

    <!-- Tab switcher -->
    <div class="marketplace-tabs" role="tablist">
      <button
        role="tab"
        :aria-selected="activeTab === 'borrow'"
        :class="['tab-btn', { 'tab-active': activeTab === 'borrow' }]"
        @click="activeTab = 'borrow'"
      >
        {{ t('bank.borrowTab') }}
      </button>
      <button
        role="tab"
        :aria-selected="activeTab === 'deposit'"
        :class="['tab-btn', { 'tab-active': activeTab === 'deposit' }]"
        @click="activeTab = 'deposit'"
      >
        {{ t('bank.depositTab') }}
        <span v-if="myDeposits.length > 0" class="tab-badge">{{ myDeposits.length }}</span>
      </button>
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
      <!-- ── BORROW TAB ────────────────────────────────────────────────────── -->
      <div v-if="activeTab === 'borrow'">
      <!-- Lender action panel: context-aware CTA for offering loans -->
      <section class="lender-cta-section" aria-label="Lender action">
        <h2 class="section-title">{{ t('bank.becomeALender') }}</h2>

        <!-- Unauthenticated: prompt login -->
        <div v-if="!auth.isAuthenticated" class="lender-cta-card lender-cta-login">
          <div class="lender-cta-icon" aria-hidden="true">🏦</div>
          <div class="lender-cta-body">
            <h3 class="lender-cta-title">{{ t('bank.loginToLendTitle') }}</h3>
            <p class="lender-cta-description">{{ t('bank.loginToLendDescription') }}</p>
          </div>
          <router-link to="/login" class="btn btn-secondary lender-cta-btn" aria-label="Log in to offer loans">
            {{ t('bank.loginToLend') }}
          </router-link>
        </div>

        <!-- Authenticated, no bank building: acquire CTA -->
        <div v-else-if="!hasBankBuilding" class="lender-cta-card lender-cta-acquire">
          <div class="lender-cta-icon" aria-hidden="true">🏦</div>
          <div class="lender-cta-body">
            <h3 class="lender-cta-title">{{ t('bank.noBankCTATitle') }}</h3>
            <p class="lender-cta-description">{{ t('bank.noBankCTADescription') }}</p>
          </div>
          <button class="btn btn-primary lender-cta-btn" @click="navigateToAcquireBank" aria-label="Acquire a Bank building">
            {{ t('bank.acquireBank') }}
          </button>
        </div>

        <!-- Authenticated, has bank: manage bank CTA -->
        <div v-else class="lender-cta-card lender-cta-manage">
          <div class="lender-cta-icon" aria-hidden="true">🏦</div>
          <div class="lender-cta-body">
            <h3 class="lender-cta-title">{{ t('bank.hasBankCTATitle') }}</h3>
            <p class="lender-cta-description">{{ t('bank.hasBankCTADescription') }}</p>
            <span class="lender-bank-name">{{ firstBankBuilding?.name }}</span>
          </div>
          <button class="btn btn-primary lender-cta-btn" @click="navigateToManageBank">
            {{ t('bank.manageBank') }}
          </button>
        </div>
      </section>

      <!-- Active loans section (authenticated borrowers) -->
      <section v-if="auth.isAuthenticated && activeLoans.length > 0" class="my-loans-section">
        <h2 class="section-title">{{ t('bank.myLoans') }}</h2>
        <div class="loans-grid">
          <div v-for="loan in activeLoans" :key="loan.id" class="loan-card" :class="loanStatusClass(loan.status)">
            <div class="loan-card-header">
              <span class="lender-name">{{ loan.lenderCompanyName }}</span>
              <span class="loan-status-badge" :class="loanStatusClass(loan.status)">
                {{ t(`bank.statusBadge.${loan.status}`) }}
              </span>
            </div>
            <div class="loan-card-body">
              <div class="loan-stat">
                <span class="stat-label">{{ t('bank.remainingPrincipal') }}</span>
                <span class="stat-value">{{ formatCurrency(loan.remainingPrincipal) }}</span>
              </div>
              <div class="loan-stat">
                <span class="stat-label">{{ t('bank.nextPayment') }}</span>
                <span class="stat-value">{{ formatCurrency(loan.paymentAmount) }}</span>
              </div>
              <div class="loan-stat">
                <span class="stat-label">{{ t('bank.paymentsMade') }}</span>
                <span class="stat-value">{{ loan.paymentsMade }} / {{ loan.totalPayments }}</span>
              </div>
              <div class="loan-stat">
                <span class="stat-label">{{ t('bank.interestRate') }}</span>
                <span class="stat-value">{{ formatPercent(loan.annualInterestRatePercent) }}</span>
              </div>
            </div>
            <div v-if="loan.missedPayments > 0" class="overdue-warning">⚠ {{ loan.missedPayments }} missed payment(s) — penalty accumulated: {{ formatCurrency(loan.accumulatedPenalty) }}</div>
            <div v-if="loan.collateralBuildingId" class="collateral-badge">
              🏛 {{ t('bank.securedLoan') }}: {{ loan.collateralBuildingName }}
              <span v-if="loan.collateralAppraisedValue" class="collateral-badge-value">
                ({{ t('bank.collateralAppraisedValue') }}: {{ formatCurrency(loan.collateralAppraisedValue) }})
              </span>
            </div>
          </div>
        </div>
      </section>

      <!-- Bank discovery section for borrowers: choose a bank first, then create loan on bank page -->
      <section class="offers-section">
        <h2 class="section-title">{{ t('bank.chooseBankToBorrow') }}</h2>
        <p class="section-subtitle">{{ t('bank.chooseBankToBorrowHint') }}</p>
        <div v-if="sortedBanksForBorrow.length === 0" class="empty-state">
          <p>{{ t('bank.noBanksAvailable') }}</p>
        </div>
        <div v-else class="banks-for-borrow-grid">
          <div v-for="bank in sortedBanksForBorrow" :key="bank.bankBuildingId" class="bank-borrow-card">
            <div class="bank-borrow-card-header">
              <div class="bank-borrow-identity">
                <span class="bank-borrow-icon">🏦</span>
                <div>
                  <span class="bank-borrow-name">{{ bank.bankBuildingName }}</span>
                  <span class="bank-borrow-lender">{{ bank.lenderCompanyName }}</span>
                </div>
              </div>
              <div class="bank-borrow-rate">
                <span class="rate-value">{{ formatPercent(bank.lendingInterestRatePercent) }}</span>
                <span class="rate-label">{{ t('bank.perYear') }}</span>
              </div>
            </div>
            <div class="bank-borrow-stats">
              <div class="borrow-stat">
                <span class="stat-label">{{ t('bank.availableCapacity') }}</span>
                <span class="stat-value" :class="bank.availableLendingCapacity > 0 ? '' : 'stat-zero'">
                  {{ formatCurrency(bank.availableLendingCapacity) }}
                </span>
              </div>
              <div class="borrow-stat">
                <span class="stat-label">{{ t('common.city') }}</span>
                <span class="stat-value">{{ bank.cityName }}</span>
              </div>
            </div>
            <div class="bank-borrow-card-footer">
              <router-link :to="`/bank/${bank.bankBuildingId}`" class="btn btn-primary btn-sm">
                {{ t('bank.visitBankToBorrow') }}
              </router-link>
            </div>
          </div>
        </div>
      </section>
      </div><!-- end borrow tab -->

      <!-- ── DEPOSIT TAB ─────────────────────────────────────────────────────── -->
      <div v-if="activeTab === 'deposit'" class="deposit-tab">

        <!-- My Deposits -->
        <section v-if="auth.isAuthenticated && myDeposits.length > 0" class="my-deposits-section">
          <h2 class="section-title">{{ t('bank.myDeposits') }}</h2>
          <div class="deposits-list">
            <div v-for="dep in myDeposits" :key="dep.id" class="deposit-card">
              <div class="deposit-card-header">
                <span class="deposit-bank-name">{{ dep.bankBuildingName }}</span>
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
                <div class="deposit-stat">
                  <span class="deposit-stat-label">{{ t('common.company') }}</span>
                  <span class="deposit-stat-value">{{ dep.depositorCompanyName }}</span>
                </div>
              </div>
              <button
                v-if="!dep.isBaseCapital"
                class="btn btn-secondary btn-sm"
                @click="withdrawDeposit(dep)"
              >
                {{ t('bank.withdrawDeposit') }}
              </button>
            </div>
          </div>
        </section>

        <!-- Banks List -->
        <section class="banks-list-section">
          <h2 class="section-title">{{ t('bank.allBanks') }}</h2>

          <div v-if="allBanks.length === 0" class="empty-state">
            <p>{{ t('bank.noBanksAvailable') }}</p>
          </div>

          <template v-else>
            <!-- Sort/filter controls -->
            <div class="banks-controls">
              <div class="banks-filter">
                <label class="filter-label" for="city-filter">{{ t('bank.cityFilter') }}</label>
                <select id="city-filter" v-model="bankCityFilter" class="filter-select">
                  <option value="">{{ t('common.city') }}: All</option>
                  <option v-for="city in availableBankCities" :key="city" :value="city">{{ city }}</option>
                </select>
                <label class="filter-check">
                  <input v-model="bankShowAvailableOnly" type="checkbox" />
                  {{ t('bank.showAvailableOnly') }}
                </label>
              </div>
              <div class="banks-sort" role="group" :aria-label="t('bank.sortBy')">
                <span class="sort-label">{{ t('bank.sortBy') }}</span>
                <button
                  v-for="field in (['depositRate', 'lendingRate', 'capacity', 'city'] as BankSortField[])"
                  :key="field"
                  class="sort-btn"
                  :class="{ 'sort-active': bankSortBy === field }"
                  @click="toggleBankSort(field)"
                >
                  <span v-if="field === 'depositRate'">{{ t('bank.depositInterestRate') }}</span>
                  <span v-else-if="field === 'lendingRate'">{{ t('bank.lendingInterestRate') }}</span>
                  <span v-else-if="field === 'capacity'">{{ t('bank.availableLendingCapacity') }}</span>
                  <span v-else>{{ t('common.city') }}</span>
                  <span v-if="bankSortBy === field" class="sort-dir-icon" aria-hidden="true">
                    {{ bankSortDir === 'asc' ? '↑' : '↓' }}
                  </span>
                </button>
              </div>
            </div>

            <div v-if="filteredAndSortedBanks.length === 0" class="empty-state">
              <p>{{ t('bank.noBanksAvailable') }}</p>
            </div>

            <div v-else class="banks-grid">
              <div v-for="bank in filteredAndSortedBanks" :key="bank.bankBuildingId" class="bank-card">
                <div class="bank-card-header">
                  <div>
                    <h3 class="bank-card-name">{{ bank.bankBuildingName }}</h3>
                    <span class="bank-card-city">{{ bank.cityName }} · {{ bank.lenderCompanyName }}</span>
                  </div>
                </div>
                <div class="bank-card-rates">
                  <div class="bank-rate deposit-rate">
                    <span class="rate-label">{{ t('bank.depositInterestRate') }}</span>
                    <span class="rate-value green">{{ formatPercent(bank.depositInterestRatePercent) }}</span>
                  </div>
                  <div class="bank-rate lending-rate">
                    <span class="rate-label">{{ t('bank.lendingInterestRate') }}</span>
                    <span class="rate-value orange">{{ formatPercent(bank.lendingInterestRatePercent) }}</span>
                  </div>
                </div>
                <div class="bank-card-capacity">
                  <span class="capacity-label">{{ t('bank.availableLendingCapacity') }}</span>
                  <span class="capacity-value" :class="bank.availableLendingCapacity > 0 ? 'positive' : 'zero'">
                    {{ formatCurrency(bank.availableLendingCapacity) }}
                  </span>
                </div>
                <div class="bank-card-actions">
                  <button
                    class="btn btn-secondary btn-sm"
                    @click="navigateToBank(bank.bankBuildingId)"
                  >
                    {{ t('bank.viewBankDetail') }}
                  </button>
                  <button
                    v-if="auth.isAuthenticated && isCompanyAccountActive"
                    class="btn btn-primary btn-sm bank-deposit-btn"
                    @click="openDepositModal(bank)"
                  >
                    {{ t('bank.makeDeposit') }}
                  </button>
                  <router-link
                    v-else-if="!auth.isAuthenticated"
                    to="/login"
                    class="btn btn-primary btn-sm"
                  >
                    {{ t('auth.login') }}
                  </router-link>
                  <p v-else class="offer-context-hint">{{ t('bank.companyAccountRequired') }}</p>
                </div>
              </div>
            </div>
          </template>
        </section>

      </div><!-- end deposit tab -->

    </template>

    <!-- Accept Loan Modal -->
    <div v-if="showAcceptModal && selectedOffer" class="modal-overlay" @click.self="closeAcceptModal">
      <div class="modal" role="dialog" :aria-label="t('bank.confirmAccept')">
        <div class="modal-header">
          <h2>{{ t('bank.confirmAccept') }}</h2>
          <button class="modal-close" @click="closeAcceptModal" :aria-label="t('common.close')">✕</button>
        </div>
        <div class="modal-body">
          <div class="loan-summary">
            <div class="summary-row">
              <span>{{ t('bank.lender') }}</span>
              <strong>{{ selectedOffer.lenderCompanyName }}</strong>
            </div>
            <div class="summary-row">
              <span>{{ t('bank.interestRate') }}</span>
              <strong>{{ formatPercent(selectedOffer.annualInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
            </div>
            <div class="summary-row">
              <span>{{ t('bank.duration') }}</span>
              <strong>{{ formatLoanDuration(selectedOffer.durationTicks) }}</strong>
            </div>
          </div>

          <div class="form-group">
            <label for="borrow-company">{{ t('bank.borrower') }}</label>
            <div id="borrow-company" class="active-borrower-company">
              <strong>{{ activeCompany?.name ?? t('bank.activeBorrowerCompany') }}</strong>
              <span>{{ activeCompany ? formatCurrency(activeCompany.cash) : '' }}</span>
            </div>
            <span class="form-hint">{{ t('bank.borrowerHint') }}</span>
          </div>

          <div class="form-group">
            <label for="principal-amount">{{ t('bank.principalAmount') }}</label>
            <input
              id="principal-amount"
              v-model.number="principalAmount"
              type="number"
              :min="1000"
              :max="Math.min(selectedOffer.maxPrincipalPerLoan, selectedOffer.remainingCapacity)"
              step="1000"
              class="form-input"
            />
            <span class="form-hint">{{ t('bank.companyCashAvailable', { amount: formatCurrency(selectedCompanyCash) }) }}</span>
          </div>

          <div class="repayment-summary">
            <div class="summary-row">
              <span>{{ t('bank.originalPrincipal') }}</span>
              <strong>{{ formatCurrency(principalAmount) }}</strong>
            </div>
            <div class="summary-row">
              <span>{{ t('bank.paymentAmount') }}</span>
              <strong>{{ formatCurrency(estimatedPaymentAmount) }} × {{ estimatedTotalPayments }}</strong>
            </div>
            <div class="summary-row total-row">
              <span>{{ t('bank.totalRepayment') }}</span>
              <strong>{{ formatCurrency(estimatedTotalRepayment) }}</strong>
            </div>
          </div>

          <!-- Collateral selection -->
          <div class="form-group collateral-group">
            <label>{{ t('bank.collateralOptional') }}</label>
            <p class="form-hint">{{ t('bank.collateralHint') }}</p>
            <div v-if="collateralLoadError" class="form-hint error-inline">{{ collateralLoadError }}</div>
            <div v-else-if="collateralBuildings.length === 0" class="form-hint muted-hint">{{ t('bank.noBuildingsForCollateral') }}</div>
            <div v-else class="collateral-list">
              <!-- None option -->
              <label
                class="collateral-option"
                :class="{ selected: selectedCollateralBuildingId === null }"
              >
                <input
                  type="radio"
                  :value="null"
                  v-model="selectedCollateralBuildingId"
                  class="collateral-radio"
                />
                <span class="collateral-option-name">{{ t('bank.collateralNone') }}</span>
              </label>
              <!-- Buildings -->
              <label
                v-for="b in collateralBuildings"
                :key="b.buildingId"
                class="collateral-option"
                :class="{ selected: selectedCollateralBuildingId === b.buildingId, ineligible: !b.isEligible }"
              >
                <input
                  type="radio"
                  :value="b.buildingId"
                  v-model="selectedCollateralBuildingId"
                  :disabled="!b.isEligible"
                  class="collateral-radio"
                />
                <span class="collateral-option-body">
                  <span class="collateral-option-name">{{ b.buildingName }}</span>
                  <span class="collateral-option-type">{{ b.buildingType }} · Lv{{ b.level }}</span>
                  <span v-if="!b.isEligible" class="collateral-tag ineligible-tag">{{ t('bank.collateralAlreadyPledged') }}</span>
                  <span v-else class="collateral-stats">
                    <span>{{ t('bank.collateralAppraisedValue') }}: {{ formatCurrency(b.appraisedValue) }}</span>
                    <span class="stat-highlight">{{ t('bank.collateralMaxBorrowable') }}: {{ formatCurrency(b.maxBorrowable) }}</span>
                    <span v-if="b.existingSecuredExposure > 0" class="stat-warn">
                      {{ t('bank.collateralExistingExposure') }}: {{ formatCurrency(b.existingSecuredExposure) }}
                    </span>
                    <span class="stat-capacity">{{ t('bank.collateralRemainingCapacity') }}: {{ formatCurrency(b.remainingBorrowingCapacity) }}</span>
                  </span>
                </span>
              </label>
            </div>
            <!-- Collateral-specific warning -->
            <p v-if="collateralCapacityWarning" class="risk-warning collateral-warning">⚠ {{ collateralCapacityWarning }}</p>
            <!-- Selected collateral summary -->
            <div v-if="selectedCollateral" class="collateral-selected-summary">
              <span class="collateral-selected-label">{{ t('bank.collateralBuilding') }}:</span>
              <strong>{{ selectedCollateral.buildingName }}</strong>
              <span class="capacity-bar-wrap">
                <span
                  class="capacity-bar-fill"
                  :style="{ width: Math.min(100, (principalAmount / selectedCollateral.maxBorrowable) * 100).toFixed(1) + '%' }"
                  :class="{ 'capacity-bar-danger': principalAmount > selectedCollateral.remainingBorrowingCapacity }"
                ></span>
              </span>
              <span class="capacity-bar-label">
                {{ formatCurrency(principalAmount) }} / {{ formatCurrency(selectedCollateral.maxBorrowable) }}
                ({{ Math.min(100, Math.round((principalAmount / selectedCollateral.maxBorrowable) * 100)) }}% LTV)
              </span>
            </div>
          </div>

          <p class="risk-warning">⚠ {{ t('bank.riskWarning') }}</p>

          <div v-if="acceptError" class="error-message">{{ acceptError }}</div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="closeAcceptModal">{{ t('common.cancel') }}</button>
          <button
            class="btn btn-primary"
            :disabled="acceptLoading || principalAmount <= 0 || !!collateralCapacityWarning"
            @click="confirmAcceptLoan"
          >
            <span v-if="acceptLoading">{{ t('common.loading') }}</span>
            <span v-else>{{ t('bank.acceptLoan') }}</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Deposit Modal -->
    <div v-if="showDepositModal && selectedBank" class="modal-overlay" @click.self="closeDepositModal">
      <div class="modal" role="dialog" :aria-label="t('bank.makeDeposit')">
        <div class="modal-header">
          <h2>{{ t('bank.makeDeposit') }}</h2>
          <button class="modal-close" :aria-label="t('common.close')" @click="closeDepositModal">✕</button>
        </div>
        <div class="modal-body">
          <div class="loan-summary">
            <div class="summary-row">
              <span>Bank</span>
              <strong>{{ selectedBank.bankBuildingName }}</strong>
            </div>
            <div class="summary-row">
              <span>{{ t('bank.depositInterestRate') }}</span>
              <strong>{{ formatPercent(selectedBank.depositInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
            </div>
          </div>
          <div class="form-group">
            <label for="deposit-amount">{{ t('bank.depositAmount') }}</label>
            <input
              id="deposit-amount"
              v-model.number="depositAmount"
              type="number"
              min="1000"
              step="1000"
              class="form-input"
            />
            <span class="form-hint">{{ t('bank.depositAmountHint') }}</span>
          </div>
          <div v-if="depositSuccess" class="success-message">{{ t('bank.depositCreated') }}</div>
          <div v-if="depositError" class="error-message">{{ depositError }}</div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="closeDepositModal">{{ t('common.cancel') }}</button>
          <button
            class="btn btn-primary"
            :disabled="depositLoading || depositAmount < 1000"
            @click="submitDeposit"
          >
            <span v-if="depositLoading">{{ t('common.loading') }}</span>
            <span v-else>{{ t('bank.confirmDeposit') }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.loan-marketplace-view {
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
  color: var(--color-text-primary);
}

.page-subtitle {
  color: var(--color-text-secondary);
  margin-top: var(--spacing-xs);
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

.section-title {
  font-size: 1.2rem;
  font-weight: 600;
  margin-bottom: var(--spacing-md);
  color: var(--color-text-primary);
}

.my-loans-section {
  margin-bottom: var(--spacing-xl);
}

.loans-grid,
.offers-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: var(--spacing-md);
}

/* Bank borrow card grid */
.banks-for-borrow-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--spacing-md);
  margin-top: var(--spacing-md);
}

.bank-borrow-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
  transition: box-shadow 0.2s, border-color 0.2s;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.bank-borrow-card:hover {
  border-color: var(--color-primary);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.bank-borrow-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.bank-borrow-identity {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.bank-borrow-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.bank-borrow-name {
  display: block;
  font-weight: 700;
  font-size: 0.9375rem;
}

.bank-borrow-lender {
  display: block;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.bank-borrow-rate {
  text-align: right;
  flex-shrink: 0;
}

.bank-borrow-stats {
  display: flex;
  gap: 1rem;
}

.borrow-stat {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.borrow-stat .stat-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.borrow-stat .stat-value {
  font-size: 0.875rem;
  font-weight: 600;
}

.borrow-stat .stat-zero {
  color: var(--color-danger);
}

.bank-borrow-card-footer {
  margin-top: auto;
}

.section-subtitle {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.5rem;
}

.loan-card,
.offer-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
  transition: box-shadow 0.2s;
}

.loan-card:hover,
.offer-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.offer-context-hint {
  margin: 0.75rem 0 0;
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.active-borrower-company {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--spacing-sm);
  padding: 0.75rem 0.9rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface-hover);
}

.loan-card.status-overdue {
  border-color: var(--color-warning, #f59e0b);
}

.loan-card.status-defaulted {
  border-color: var(--color-danger, #ef4444);
}

.loan-card-header,
.offer-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: var(--spacing-sm);
}

.loan-status-badge {
  font-size: 0.7rem;
  padding: 2px 8px;
  border-radius: 12px;
  font-weight: 600;
  text-transform: uppercase;
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

.loan-card-body,
.offer-card-body {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--spacing-xs);
  margin-bottom: var(--spacing-sm);
}

.loan-stat,
.offer-stat {
  display: flex;
  flex-direction: column;
}

.stat-label {
  font-size: 0.7rem;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.stat-value {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.overdue-warning {
  background: rgba(251, 191, 36, 0.12);
  color: #fbbf24;
  padding: var(--spacing-xs);
  border-radius: var(--radius-sm);
  font-size: 0.8rem;
  margin-top: var(--spacing-xs);
}

.offer-card-header {
  align-items: center;
}

.offer-lender {
  display: flex;
  flex-direction: column;
}

.offer-bank-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.offer-lender-name {
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.offer-rate {
  text-align: right;
}

.rate-value {
  display: block;
  font-size: 1.4rem;
  font-weight: 700;
  color: var(--color-primary, #3b82f6);
}

.rate-label {
  font-size: 0.7rem;
  color: var(--color-text-secondary);
}

.offer-card-footer {
  margin-top: var(--spacing-sm);
}

/* Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: var(--spacing-md);
}

.modal {
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  max-width: 480px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--spacing-md);
  border-bottom: 1px solid var(--color-border);
}

.modal-close {
  background: none;
  border: none;
  cursor: pointer;
  font-size: 1.2rem;
  color: var(--color-text-secondary);
}

.modal-body {
  padding: var(--spacing-md);
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: var(--spacing-sm);
  padding: var(--spacing-md);
  border-top: 1px solid var(--color-border);
}

.loan-summary,
.repayment-summary {
  background: var(--color-background, #f9fafb);
  border-radius: var(--radius-sm);
  padding: var(--spacing-sm);
}

.summary-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 0;
  font-size: 0.9rem;
}

.total-row {
  border-top: 1px solid var(--color-border);
  margin-top: var(--spacing-xs);
  padding-top: var(--spacing-xs);
  font-weight: 600;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
}

.form-group label {
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.form-select,
.form-input {
  padding: var(--spacing-xs) var(--spacing-sm);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 0.9rem;
}

.risk-warning {
  background: rgba(251, 191, 36, 0.12);
  color: #fbbf24;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  font-size: 0.8rem;
}

.error-message {
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  font-size: 0.85rem;
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
  text-decoration: none;
  width: 100%;
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
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 600px) {
  .loans-grid,
  .offers-grid {
    grid-template-columns: 1fr;
  }
}

/* Lender CTA panel */
.lender-cta-section {
  margin-bottom: var(--spacing-xl);
}

.lender-cta-card {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--spacing-md) var(--spacing-lg);
  transition: box-shadow 0.2s;
}

.lender-cta-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.lender-cta-manage {
  border-color: var(--color-primary, #3b82f6);
  background: rgba(59, 130, 246, 0.04);
}

.lender-cta-icon {
  font-size: 2rem;
  flex-shrink: 0;
}

.lender-cta-body {
  flex: 1;
  min-width: 0;
}

.lender-cta-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 var(--spacing-xs);
}

.lender-cta-description {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  margin: 0 0 var(--spacing-xs);
  line-height: 1.4;
}

.lender-bank-name {
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--color-primary, #3b82f6);
}

.lender-cta-btn {
  flex-shrink: 0;
  width: auto;
  white-space: nowrap;
}

@media (max-width: 600px) {
  .lender-cta-card {
    flex-direction: column;
    align-items: flex-start;
  }

  .lender-cta-btn {
    width: 100%;
    justify-content: center;
  }
}

/* Tabs */
.marketplace-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
  border-bottom: 2px solid var(--color-border);
}

.tab-btn {
  padding: 0.6rem 1.25rem;
  border: none;
  background: none;
  cursor: pointer;
  font-size: 0.95rem;
  font-weight: 500;
  color: var(--color-text-muted);
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  display: flex;
  align-items: center;
  gap: 0.4rem;
  transition: color 0.15s, border-color 0.15s;
}

.tab-btn.tab-active {
  color: var(--color-primary, #3b82f6);
  border-bottom-color: var(--color-primary, #3b82f6);
}

.tab-badge {
  background: var(--color-primary, #3b82f6);
  color: white;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.1rem 0.45rem;
  min-width: 1.2rem;
  text-align: center;
}

/* Deposit tab content */
.deposit-tab {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.my-deposits-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1.25rem;
}

.deposits-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
  margin-top: 1rem;
}

.deposit-card {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.deposit-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.deposit-bank-name {
  font-weight: 600;
  font-size: 0.95rem;
}

.deposit-rate-badge {
  background: rgba(34, 197, 94, 0.15);
  color: #22c55e;
  border-radius: 999px;
  font-size: 0.78rem;
  font-weight: 700;
  padding: 0.15rem 0.55rem;
}

.deposit-stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
}

.deposit-stat {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.deposit-stat-label {
  font-size: 0.72rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.deposit-stat-value {
  font-size: 0.9rem;
  font-weight: 600;
}

.deposit-stat-value.positive { color: #22c55e; }

/* Banks grid */
.banks-list-section {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1.25rem;
}

.banks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
  margin-top: 1rem;
}

.bank-card {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.bank-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.bank-card-name {
  font-size: 1rem;
  font-weight: 600;
  margin: 0;
}

.bank-card-city {
  font-size: 0.8rem;
  color: var(--color-text-muted);
}

.bank-card-rates {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}

.bank-rate {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.rate-label {
  font-size: 0.72rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.rate-value {
  font-size: 1rem;
  font-weight: 700;
}

.rate-value.green { color: #22c55e; }
.rate-value.orange { color: #f59e0b; }

.bank-card-capacity {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.875rem;
}

.capacity-label {
  color: var(--color-text-muted);
}

.capacity-value {
  font-weight: 600;
}

.capacity-value.positive { color: #22c55e; }
.capacity-value.zero { color: var(--color-text-muted); }

.bank-deposit-btn {
  width: 100%;
}

.bank-card-actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

/* Sort/filter controls */
.banks-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 1rem;
  padding: 0.75rem;
  background: var(--color-surface-hover, rgba(255,255,255,0.04));
  border-radius: 6px;
}

.banks-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
}

.filter-label {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  font-weight: 500;
}

.filter-select {
  padding: 0.3rem 0.6rem;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 0.85rem;
  cursor: pointer;
}

.filter-check {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  cursor: pointer;
  user-select: none;
}

.banks-sort {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  align-items: center;
}

.sort-label {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  font-weight: 500;
  margin-right: 0.25rem;
}

.sort-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.25rem 0.6rem;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 0.78rem;
  cursor: pointer;
  transition: background 0.15s, color 0.15s, border-color 0.15s;
}

.sort-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.sort-btn.sort-active {
  background: var(--color-primary, #3b82f6);
  color: #fff;
  border-color: var(--color-primary, #3b82f6);
}

.sort-dir-icon {
  font-size: 0.9rem;
}

.success-message {
  color: #22c55e;
  font-size: 0.875rem;
  padding: 0.5rem 0;
}

/* ── Collateral selection styles ─────────────────────────────────────────── */
.collateral-group {
  border-top: 1px solid var(--color-border);
  padding-top: var(--spacing-sm);
  margin-top: var(--spacing-sm);
}

.collateral-list {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-xs);
  max-height: 220px;
  overflow-y: auto;
  margin-top: var(--spacing-xs);
}

.collateral-option {
  display: flex;
  align-items: flex-start;
  gap: var(--spacing-xs);
  padding: var(--spacing-xs) var(--spacing-sm);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  cursor: pointer;
  background: var(--color-surface);
  transition: border-color 0.15s, background 0.15s;
}

.collateral-option.selected {
  border-color: var(--color-primary, #3b82f6);
  background: rgba(59, 130, 246, 0.08);
}

.collateral-option.ineligible {
  opacity: 0.55;
  cursor: not-allowed;
}

.collateral-radio {
  margin-top: 3px;
  flex-shrink: 0;
}

.collateral-option-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.collateral-option-name {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 0.9rem;
}

.collateral-option-type {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.collateral-stats {
  display: flex;
  flex-wrap: wrap;
  gap: var(--spacing-xs);
  margin-top: 2px;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.stat-highlight {
  color: var(--color-primary, #3b82f6);
  font-weight: 600;
}

.stat-warn {
  color: #fbbf24;
}

.stat-capacity {
  color: #22c55e;
  font-weight: 600;
}

.ineligible-tag {
  font-size: 0.72rem;
  color: #f87171;
  background: rgba(248, 113, 113, 0.1);
  padding: 1px 5px;
  border-radius: 3px;
}

.collateral-selected-summary {
  display: flex;
  align-items: center;
  gap: var(--spacing-xs);
  padding: var(--spacing-xs);
  background: rgba(59, 130, 246, 0.07);
  border-radius: var(--radius-sm);
  border: 1px solid rgba(59, 130, 246, 0.2);
  margin-top: var(--spacing-xs);
  font-size: 0.82rem;
  flex-wrap: wrap;
}

.collateral-selected-label {
  color: var(--color-text-secondary);
}

.capacity-bar-wrap {
  flex: 1;
  min-width: 80px;
  height: 6px;
  background: var(--color-border);
  border-radius: 3px;
  overflow: hidden;
}

.capacity-bar-fill {
  height: 100%;
  background: var(--color-primary, #3b82f6);
  border-radius: 3px;
  transition: width 0.2s;
}

.capacity-bar-fill.capacity-bar-danger {
  background: #ef4444;
}

.capacity-bar-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.collateral-warning {
  color: #ef4444;
  border-color: rgba(239, 68, 68, 0.3);
  background: rgba(239, 68, 68, 0.08);
}

.collateral-badge {
  background: rgba(59, 130, 246, 0.1);
  color: var(--color-primary, #3b82f6);
  padding: var(--spacing-xs);
  border-radius: var(--radius-sm);
  font-size: 0.8rem;
  margin-top: var(--spacing-xs);
  border: 1px solid rgba(59, 130, 246, 0.2);
}

.collateral-badge-value {
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.error-inline {
  color: #f87171;
}

.muted-hint {
  color: var(--color-text-secondary);
  font-style: italic;
}
</style>
