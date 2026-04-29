<template src="./LoanMarketplaceView.template.html"></template>

<script setup lang="ts">
/* oxlint-disable no-unused-vars */
/* eslint-disable @typescript-eslint/no-unused-vars */
// Split-file SFC: script symbols are consumed by LoanMarketplaceView.template.html.
 
 
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useScrollPreservation } from '@/composables/useScrollPreservation'
import { deepEqual } from '@/lib/utils'
import { getActiveCompany } from '@/lib/accountContext'
import type { LoanOfferSummary, LoanSummary, Company, BankDepositSummary, BankInfoSummary, CollateralEligibilitySummary, PlayerBankAccountSummary } from '@/types'
import { formatLoanDuration, computeTotalRepayment, computePaymentAmount, computeTotalPayments, loanStatusClass, formatCurrency, formatPercent } from '@/lib/loanHelpers'

const { t } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const router = useRouter()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()
const loading = ref(true)
const error = ref<string | null>(null)
const myLoans = ref<LoanSummary[]>([])
const myCompanies = ref<Company[]>([])

// Active tab: 'borrow' | 'deposit'
const activeTab = ref<'borrow' | 'deposit'>('borrow')

// Banks list (for deposit tab)
const allBanks = ref<BankInfoSummary[]>([])
const myDeposits = ref<BankDepositSummary[]>([])
const myBankAccounts = ref<PlayerBankAccountSummary[]>([])

// Sort/filter state for bank list
type BankSortField = 'depositRate' | 'lendingRate' | 'capacity' | 'city'
const bankSortBy = ref<BankSortField>('depositRate')
const bankSortDir = ref<'asc' | 'desc'>('desc')
const bankCityFilter = ref('')
const bankShowAvailableOnly = ref(false)

// Deposit modal state
const showDepositModal = ref(false)
const selectedBank = ref<BankInfoSummary | null>(null)
const depositLoading = ref(false)
const depositError = ref<string | null>(null)
const depositSuccess = ref(false)

// Close account state
const closingAccountId = ref<string | null>(null)
const closeAccountErrors = ref<Record<string, string>>({})

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
    myCompanies {
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
      cityId
      isDepositAccount
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

const CLOSE_BANK_ACCOUNT_MUTATION = `
  mutation CloseBankAccountById($input: CloseBankAccountInput!) {
    closeBankAccount(input: $input) {
      id
      isActive
      withdrawnAtUtc
    }
  }
`

const CLOSE_COMPANY_BANK_ACCOUNT_MUTATION = `
  mutation CloseCompanyBankAccountById($input: CloseCompanyBankAccountInput!) {
    closeCompanyBankAccount(input: $input) {
      id
      accountNumber
      currencyCode
      closedAtUtc
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

    const banksResult = await gqlRequest<{ allBanks: BankInfoSummary[] }>(ALL_BANKS_QUERY)
    const newBanks = banksResult.allBanks ?? []
    if (!deepEqual(allBanks.value, newBanks)) {
      allBanks.value = newBanks
    }

    if (auth.isAuthenticated) {
      const [loansResult, companiesResult, depositsResult, accountsResult] = await Promise.all([
        gqlRequest<{ myLoans: LoanSummary[] }>(MY_LOANS_QUERY),
        gqlRequest<{ myCompanies: Company[] }>(MY_COMPANIES_QUERY),
        gqlRequest<{ myDeposits: BankDepositSummary[] }>(MY_DEPOSITS_QUERY),
        gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY),
      ])
      const newLoans = loansResult.myLoans ?? []
      if (!deepEqual(myLoans.value, newLoans)) {
        myLoans.value = newLoans
      }
      myCompanies.value = companiesResult.myCompanies ?? []
      const newDeposits = depositsResult.myDeposits ?? []
      if (!deepEqual(myDeposits.value, newDeposits)) {
        myDeposits.value = newDeposits
      }
      const newAccounts = accountsResult.myBankAccounts ?? []
      if (!deepEqual(myBankAccounts.value, newAccounts)) {
        myBankAccounts.value = newAccounts
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
const visibleBankAccounts = computed(() => {
  let accounts: PlayerBankAccountSummary[] = []

  if (auth.player?.activeAccountType === 'COMPANY' && auth.player.activeCompanyId) {
    accounts = myBankAccounts.value.filter((account) => account.ownerType === 'COMPANY' && account.companyId === auth.player?.activeCompanyId)
  } else {
    accounts = myBankAccounts.value.filter((account) => account.ownerType === 'PERSON')
  }

  return accounts
})

// Lender eligibility: detect BANK buildings across all companies
const myBankBuildings = computed(() => myCompanies.value.flatMap((c) => (c.buildings ?? []).filter((b) => b.type === 'BANK').map((b) => ({ ...b, companyId: c.id }))))
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

  // Filter by selected city from navbar
  if (selectedCityId.value) {
    banks = banks.filter((b) => b.cityId === selectedCityId.value)
  }

  // Filter by manual city filter if set
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
    router.push(`/buy-building/${firstCompanyId.value}?type=BANK`)
  } else {
    router.push('/dashboard')
  }
}

// Banks sorted for the borrow section: all open banks sorted by lowest lending rate, filtered by selected city
const sortedBanksForBorrow = computed(() => {
  let banks = allBanks.value.filter((b) => b.baseCapitalDeposited)

  // Filter by selected city from navbar
  if (selectedCityId.value) {
    banks = banks.filter((b) => b.cityId === selectedCityId.value)
  }

  return [...banks].sort((a, b) => a.lendingInterestRatePercent - b.lendingInterestRatePercent)
})

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

const selectedCollateral = computed(() => collateralBuildings.value.find((b) => b.buildingId === selectedCollateralBuildingId.value) ?? null)

const collateralCapacityWarning = computed(() => {
  if (!selectedCollateral.value || principalAmount.value <= 0) return null
  if (principalAmount.value > selectedCollateral.value.remainingBorrowingCapacity) {
    return t('bank.collateralExceedsLimit')
  }
  return null
})

const collateralRequiredWarning = computed(() => {
  if (principalAmount.value <= 0) return null
  if (!selectedCollateralBuildingId.value) {
    return t('bank.collateralRequired')
  }
  return null
})

async function confirmAcceptLoan() {
  if (!selectedOffer.value || !selectedCompanyId.value || principalAmount.value <= 0) return
  if (!selectedCollateralBuildingId.value) {
    acceptError.value = t('bank.collateralRequired')
    return
  }
  acceptLoading.value = true
  acceptError.value = null
  try {
    await gqlRequest(ACCEPT_LOAN_MUTATION, {
      input: {
        loanOfferId: selectedOffer.value.id,
        borrowerCompanyId: selectedCompanyId.value,
        principalAmount: principalAmount.value,
        collateralBuildingId: selectedCollateralBuildingId.value,
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
  depositError.value = null
  depositSuccess.value = false
  showDepositModal.value = true
}

function formatOpenAccountError(errorMessage: string) {
  if (!errorMessage.includes('Insufficient company funds to open this bank account.')) {
    return errorMessage
  }

  return `${errorMessage} ${t('bank.zeroBalanceFundingHint')}`
}

function closeDepositModal() {
  showDepositModal.value = false
  selectedBank.value = null
  depositError.value = null
  depositSuccess.value = false
}

async function submitDeposit() {
  if (!selectedBank.value) return
  depositLoading.value = true
  depositError.value = null
  depositSuccess.value = false
  const contextCompanyId = isCompanyAccountActive.value ? (activeCompany.value?.id ?? null) : null
  try {
    await gqlRequest(CREATE_DEPOSIT_MUTATION, {
      input: {
        bankBuildingId: selectedBank.value.bankBuildingId,
        depositorCompanyId: contextCompanyId,
        amount: 0,
      },
    })
    depositSuccess.value = true
    await loadData()
    setTimeout(closeDepositModal, 1500)
  } catch (err) {
    depositError.value = formatOpenAccountError(err instanceof Error ? err.message : String(err))
  } finally {
    depositLoading.value = false
  }
}

async function closeBankAccount(accountId: string, isDepositAccount: boolean) {
  if (!confirm(t('bank.confirmCloseAccount'))) return
  closingAccountId.value = accountId
  closeAccountErrors.value = { ...closeAccountErrors.value, [accountId]: '' }
  try {
    if (isDepositAccount) {
      await gqlRequest(CLOSE_BANK_ACCOUNT_MUTATION, { input: { depositId: accountId, amount: 0 } })
    } else {
      await gqlRequest(CLOSE_COMPANY_BANK_ACCOUNT_MUTATION, { input: { bankAccountId: accountId } })
    }
    await loadData()
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err)
    let friendlyMsg = msg
    if (msg.includes('ACCOUNT_IN_USE')) {
      friendlyMsg = t('bank.closeAccountBlockedInUse')
    } else if (msg.includes('NON_ZERO_BALANCE')) {
      friendlyMsg = t('bank.closeAccountNonZeroHint')
    } else if (msg.includes('ACTIVE_LOAN_REPAYMENT_ACCOUNT')) {
      friendlyMsg = t('bank.closeAccountBlockedActiveLoan')
    }
    closeAccountErrors.value = { ...closeAccountErrors.value, [accountId]: friendlyMsg }
  } finally {
    closingAccountId.value = null
  }
}


</script>

<style scoped src="./LoanMarketplaceView.styles.css"></style>