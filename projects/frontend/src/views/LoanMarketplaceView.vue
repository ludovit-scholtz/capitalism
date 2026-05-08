<template>
  <main class="loan-marketplace-view container mx-auto px-4 pb-16 pt-6 sm:px-6 lg:px-8 lg:pb-20 lg:pt-8">
    <div class="flex flex-col">
      <div class="mb-1 flex flex-col gap-3">
        <h1 class="text-4xl font-black tracking-tight text-body">{{ t('bank.banks') }}</h1>
        <p class="max-w-3xl text-sm text-muted sm:text-base">{{ t('bank.browseBanks') }}</p>
      </div>

      <DashboardTabNav v-model="activeTab" :tabs="tabDefs" />

      <UiStateLoading v-if="loading" :label="t('common.loading')" class="mt-8" />
      <UiStateError v-else-if="error" :message="error" :retry-label="t('common.retry')" class="mt-8" @retry="loadData" />

      <template v-else>
        <!-- BORROW TAB -->
        <div v-if="activeTab === 'borrow'" class="mt-8 flex flex-col gap-10 lg:gap-12">
          <LenderCtaSection
            :is-authenticated="auth.isAuthenticated"
            :has-bank-building="hasBankBuilding"
            :first-bank-building-name="firstBankBuilding?.name"
            @acquire-bank="navigateToAcquireBank"
            @manage-bank="navigateToManageBank"
          />

          <section v-if="auth.isAuthenticated && activeLoans.length > 0" class="rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="mb-6 text-2xl font-bold text-body">{{ t('bank.myLoans') }}</h2>
            <div class="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
              <LoanCard v-for="loan in activeLoans" :key="loan.id" :loan="loan" />
            </div>
          </section>

          <section class="rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <div class="mb-6 flex flex-col gap-2">
              <h2 class="text-2xl font-bold text-body">{{ t('bank.chooseBankToBorrow') }}</h2>
              <p class="max-w-3xl text-sm text-muted sm:text-base">{{ t('bank.chooseBankToBorrowHint') }}</p>
            </div>
            <UiStateEmpty v-if="sortedBanksForBorrow.length === 0">{{ t('bank.noBanksAvailable') }}</UiStateEmpty>
            <div v-else class="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
              <BankBorrowCard v-for="bank in sortedBanksForBorrow" :key="bank.bankBuildingId" :bank="bank" />
            </div>
          </section>
        </div>

        <!-- DEPOSIT TAB -->
        <div v-if="activeTab === 'deposit'" class="mt-8 flex flex-col gap-8 lg:gap-10">
          <section v-if="auth.isAuthenticated" class="rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="mb-6 text-2xl font-bold text-body">{{ t('bank.myBankAccounts') }}</h2>
            <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              <div v-for="account in visibleBankAccounts" :key="account.id" class="deposit-card rounded-2xl border border-divider bg-card-raised p-5 shadow-sm" data-testid="bank-account-row">
                <div class="mb-3 flex items-center justify-between">
                  <span class="font-semibold text-body">{{ account.ownerDisplayName }}</span>
                  <span class="rounded-full bg-success/10 px-2 py-0.5 text-xs font-bold text-success">{{ account.currencyCode }}</span>
                </div>
                <div class="mb-3 grid grid-cols-2 gap-2">
                  <div class="flex flex-col gap-0.5">
                    <span class="text-[0.72rem] uppercase tracking-wider text-muted">{{ t('bank.accountNumber') }}</span>
                    <span class="text-sm font-semibold text-body">{{ account.accountNumber }}</span>
                  </div>
                  <div class="flex flex-col gap-0.5">
                    <span class="text-[0.72rem] uppercase tracking-wider text-muted">{{ t('bank.accountBalance') }}</span>
                    <span class="text-sm font-semibold text-body">{{ formatCurrency(account.balance, account.currencyCode) }}</span>
                  </div>
                </div>
                <p v-if="account.balance == 0 && !account.isDepositAccount" class="account-ready-close mt-2 text-xs font-medium text-success">✓ {{ t('bank.accountReadyToClose') }}</p>
                <p v-else-if="account.balance != 0 && !account.isDepositAccount" class="account-nonzero-hint mt-2 text-xs text-muted">
                  {{ t('bank.closeAccountNonZeroHint') }}
                </p>
                <p v-if="closeAccountErrors[account.id]" class="close-account-error mt-2 text-xs text-error" role="alert">{{ closeAccountErrors[account.id] }}</p>
                <button v-if="account.balance == 0" class="btn btn-danger btn-sm mt-3" :disabled="closingAccountId === account.id" @click="closeBankAccount(account.id, account.isDepositAccount)">
                  {{ closingAccountId === account.id ? '...' : t('bank.closeAccount') }}
                </button>
              </div>
            </div>
          </section>

          <section class="rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
            <h2 class="mb-6 text-2xl font-bold text-body">{{ t('bank.allBanks') }}</h2>
            <UiStateEmpty v-if="allBanks.length === 0">{{ t('bank.noBanksAvailable') }}</UiStateEmpty>
            <template v-else>
              <div class="mb-6 flex flex-col gap-5 rounded-2xl border border-divider bg-card-raised p-5 lg:flex-row lg:items-end lg:justify-between">
                <div class="flex flex-col gap-4 lg:flex-row lg:flex-wrap lg:items-end">
                  <label class="text-sm font-medium text-muted" for="city-filter">{{ t('bank.cityFilter') }}</label>
                  <select id="city-filter" v-model="bankCityFilter" class="rounded-xl border border-divider bg-card px-4 py-3 text-sm text-body">
                    <option value="">{{ t('common.city') }}: All</option>
                    <option v-for="city in availableBankCities" :key="city" :value="city">{{ city }}</option>
                  </select>
                  <label class="filter-check inline-flex cursor-pointer items-center gap-3 rounded-xl border border-divider px-4 py-3 text-sm text-body">
                    <input v-model="bankShowAvailableOnly" type="checkbox" />
                    {{ t('bank.showAvailableOnly') }}
                  </label>
                </div>
                <div class="flex flex-wrap items-center gap-3" role="group" :aria-label="t('bank.sortBy')">
                  <span class="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('bank.sortBy') }}</span>
                  <button
                    v-for="field in bankSortFields"
                    :key="field"
                    class="inline-flex items-center gap-2 rounded-full border border-divider px-4 py-2 text-sm font-medium text-body transition-colors hover:border-brand hover:text-brand"
                    :class="{ 'border-brand bg-card text-brand': bankSortBy === field }"
                    @click="toggleBankSort(field)"
                  >
                    <span v-if="field === 'depositRate'">{{ t('bank.depositInterestRate') }}</span>
                    <span v-else-if="field === 'lendingRate'">{{ t('bank.lendingInterestRate') }}</span>
                    <span v-else-if="field === 'capacity'">{{ t('bank.availableLendingCapacity') }}</span>
                    <span v-else>{{ t('common.city') }}</span>
                    <span v-if="bankSortBy === field" aria-hidden="true">{{ bankSortDir === 'asc' ? '↑' : '↓' }}</span>
                  </button>
                </div>
              </div>
              <UiStateEmpty v-if="filteredAndSortedBanks.length === 0">{{ t('bank.noBanksAvailable') }}</UiStateEmpty>
              <div v-else class="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
                <BankCard
                  v-for="bank in filteredAndSortedBanks"
                  :key="bank.bankBuildingId"
                  :bank="bank"
                  :is-authenticated="auth.isAuthenticated"
                  @navigate-to-bank="navigateToBank"
                  @open-deposit-modal="openDepositModal"
                />
              </div>
            </template>
          </section>
        </div>
      </template>

      <BankDepositModal
        v-if="showDepositModal && selectedBank"
        :bank="selectedBank"
        :loading="depositLoading"
        :error="depositError"
        :success="depositSuccess"
        @close="closeDepositModal"
        @confirm="submitDeposit"
      />
    </div>
  </main>
</template>

<script setup lang="ts">
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
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import UiStateEmpty from '@/components/ui/UiStateEmpty.vue'
import DashboardTabNav from '@/components/dashboard/DashboardTabNav.vue'
import type { DashboardTab } from '@/components/dashboard/DashboardTabNav.vue'
import LenderCtaSection from '@/components/bank/LenderCtaSection.vue'
import LoanCard from '@/components/bank/LoanCard.vue'
import BankBorrowCard from '@/components/bank/BankBorrowCard.vue'
import BankCard from '@/components/bank/BankCard.vue'
import BankDepositModal from '@/components/bank/BankDepositModal.vue'
import type { LoanSummary, Company, BankDepositSummary, BankInfoSummary, PlayerBankAccountSummary } from '@/types'
import { formatCurrency } from '@/lib/loanHelpers'

const { t } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const router = useRouter()
const { saveScrollPosition, restoreScrollPosition } = useScrollPreservation()

const loading = ref(true)
const error = ref<string | null>(null)
const myLoans = ref<LoanSummary[]>([])
const myCompanies = ref<Company[]>([])
const activeTab = ref<'borrow' | 'deposit'>('borrow')
const allBanks = ref<BankInfoSummary[]>([])
const myDeposits = ref<BankDepositSummary[]>([])
const myBankAccounts = ref<PlayerBankAccountSummary[]>([])

type BankSortField = 'depositRate' | 'lendingRate' | 'capacity' | 'city'
const bankSortFields: BankSortField[] = ['depositRate', 'lendingRate', 'capacity', 'city']
const bankSortBy = ref<BankSortField>('depositRate')
const bankSortDir = ref<'asc' | 'desc'>('desc')
const bankCityFilter = ref('')
const bankShowAvailableOnly = ref(false)

const showDepositModal = ref(false)
const selectedBank = ref<BankInfoSummary | null>(null)
const depositLoading = ref(false)
const depositError = ref<string | null>(null)
const depositSuccess = ref(false)

const closingAccountId = ref<string | null>(null)
const closeAccountErrors = ref<Record<string, string>>({})

const MY_LOANS_QUERY = `{
  myLoans {
    id loanOfferId borrowerCompanyId borrowerCompanyName lenderCompanyId lenderCompanyName
    bankBuildingId bankBuildingName loanCurrencyCode originalPrincipal remainingPrincipal annualInterestRatePercent
    durationTicks startTick dueTick nextPaymentTick paymentAmount paymentsMade totalPayments
    status missedPayments accumulatedPenalty acceptedAtUtc closedAtUtc
    collateralBuildingId collateralBuildingName collateralAppraisedValue collateralListingPrice collateralListingCurrencyCode
  }
}`

const MY_COMPANIES_QUERY = `{
  myCompanies { id name cash buildings { id type name } }
}`

const ALL_BANKS_QUERY = `{
  allBanks {
    bankBuildingId bankBuildingName cityId cityName lenderCompanyId lenderCompanyName
    depositInterestRatePercent lendingInterestRatePercent totalDeposits lendableCapacity
    outstandingLoanPrincipal availableLendingCapacity baseCapitalDeposited
  }
}`

const MY_DEPOSITS_QUERY = `{
  myDeposits {
    id bankBuildingId bankBuildingName depositorCompanyId depositorCompanyName amount
    depositInterestRatePercent isBaseCapital isActive depositedAtTick depositedAtUtc totalInterestPaid
  }
}`

const MY_BANK_ACCOUNTS_QUERY = `{
  myBankAccounts {
    id accountNumber currencyCode currencySymbol balance companyId companyName
    ownerType ownerDisplayName bankBuildingId cityId isDepositAccount
  }
}`

const CREATE_DEPOSIT_MUTATION = `
  mutation OpenBankAccount($input: OpenBankAccountInput!) {
    openBankAccount(input: $input) { id amount depositInterestRatePercent isActive }
  }
`

const CLOSE_BANK_ACCOUNT_MUTATION = `
  mutation CloseBankAccountById($input: CloseBankAccountInput!) {
    closeBankAccount(input: $input) { id isActive withdrawnAtUtc }
  }
`

const CLOSE_COMPANY_BANK_ACCOUNT_MUTATION = `
  mutation CloseCompanyBankAccountById($input: CloseCompanyBankAccountInput!) {
    closeCompanyBankAccount(input: $input) { id accountNumber currencyCode closedAtUtc }
  }
`

async function loadData(isRefresh = false) {
  if (!isRefresh) loading.value = true
  error.value = null
  try {
    if (auth.isAuthenticated && !auth.player) await auth.fetchMe()

    const banksResult = await gqlRequest<{ allBanks: BankInfoSummary[] }>(ALL_BANKS_QUERY)
    const newBanks = banksResult.allBanks ?? []
    if (!deepEqual(allBanks.value, newBanks)) allBanks.value = newBanks

    if (auth.isAuthenticated) {
      const [loansResult, companiesResult, depositsResult, accountsResult] = await Promise.all([
        gqlRequest<{ myLoans: LoanSummary[] }>(MY_LOANS_QUERY),
        gqlRequest<{ myCompanies: Company[] }>(MY_COMPANIES_QUERY),
        gqlRequest<{ myDeposits: BankDepositSummary[] }>(MY_DEPOSITS_QUERY),
        gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY),
      ])
      const newLoans = loansResult.myLoans ?? []
      if (!deepEqual(myLoans.value, newLoans)) myLoans.value = newLoans
      myCompanies.value = companiesResult.myCompanies ?? []
      const newDeposits = depositsResult.myDeposits ?? []
      if (!deepEqual(myDeposits.value, newDeposits)) myDeposits.value = newDeposits
      const newAccounts = accountsResult.myBankAccounts ?? []
      if (!deepEqual(myBankAccounts.value, newAccounts)) myBankAccounts.value = newAccounts
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
  if (auth.player?.activeAccountType === 'COMPANY' && auth.player.activeCompanyId) {
    return myBankAccounts.value.filter((a) => a.ownerType === 'COMPANY' && a.companyId === auth.player?.activeCompanyId)
  }
  return myBankAccounts.value.filter((a) => a.ownerType === 'PERSON')
})
const myBankBuildings = computed(() => myCompanies.value.flatMap((c) => (c.buildings ?? []).filter((b) => b.type === 'BANK').map((b) => ({ ...b, companyId: c.id }))))
const hasBankBuilding = computed(() => myBankBuildings.value.length > 0)
const firstBankBuilding = computed(() => myBankBuildings.value[0] ?? null)
const firstCompanyId = computed(() => myCompanies.value[0]?.id ?? null)

const availableBankCities = computed(() => [...new Set(allBanks.value.map((b) => b.cityName))].sort())

const filteredAndSortedBanks = computed(() => {
  let banks = allBanks.value
  if (selectedCityId.value) banks = banks.filter((b) => b.cityId === selectedCityId.value)
  if (bankCityFilter.value) banks = banks.filter((b) => b.cityName === bankCityFilter.value)
  if (bankShowAvailableOnly.value) banks = banks.filter((b) => b.availableLendingCapacity > 0)
  return [...banks].sort((a, b) => {
    let aVal: number | string, bVal: number | string
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

const sortedBanksForBorrow = computed(() => {
  let banks = allBanks.value.filter((b) => b.baseCapitalDeposited)
  if (selectedCityId.value) banks = banks.filter((b) => b.cityId === selectedCityId.value)
  return [...banks].sort((a, b) => a.lendingInterestRatePercent - b.lendingInterestRatePercent)
})

const tabDefs = computed<DashboardTab[]>(() => [
  { key: 'borrow', label: t('bank.borrowTab') },
  { key: 'deposit', label: t('bank.depositTab'), badge: myDeposits.value.length > 0 ? myDeposits.value.length : undefined },
])

function toggleBankSort(field: BankSortField) {
  if (bankSortBy.value === field) bankSortDir.value = bankSortDir.value === 'asc' ? 'desc' : 'asc'
  else {
    bankSortBy.value = field
    bankSortDir.value = 'desc'
  }
}

function navigateToAcquireBank() {
  if (firstCompanyId.value) router.push(`/buy-building/${firstCompanyId.value}?type=BANK`)
  else router.push('/dashboard')
}

function navigateToManageBank() {
  if (firstBankBuilding.value) router.push(`/bank/${firstBankBuilding.value.id}`)
}

function navigateToBank(bankBuildingId: string) {
  router.push(`/bank/${bankBuildingId}`)
}

function openDepositModal(bank: BankInfoSummary) {
  selectedBank.value = bank
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

function formatOpenAccountError(errorMessage: string) {
  if (!errorMessage.includes('Insufficient company funds to open this bank account.')) return errorMessage
  return `${errorMessage} ${t('bank.zeroBalanceFundingHint')}`
}

async function submitDeposit() {
  if (!selectedBank.value) return
  depositLoading.value = true
  depositError.value = null
  depositSuccess.value = false
  const contextCompanyId = isCompanyAccountActive.value ? (activeCompany.value?.id ?? null) : null
  try {
    await gqlRequest(CREATE_DEPOSIT_MUTATION, {
      input: { bankBuildingId: selectedBank.value.bankBuildingId, depositorCompanyId: contextCompanyId, amount: 0 },
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
    if (msg.includes('ACCOUNT_IN_USE')) friendlyMsg = t('bank.closeAccountBlockedInUse')
    else if (msg.includes('NON_ZERO_BALANCE')) friendlyMsg = t('bank.closeAccountNonZeroHint')
    else if (msg.includes('ACTIVE_LOAN_REPAYMENT_ACCOUNT')) friendlyMsg = t('bank.closeAccountBlockedActiveLoan')
    closeAccountErrors.value = { ...closeAccountErrors.value, [accountId]: friendlyMsg }
  } finally {
    closingAccountId.value = null
  }
}
</script>

<style scoped>
.loan-marketplace-view {
  min-height: 100vh;
}
</style>
