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
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import UiStateEmpty from '@/components/ui/UiStateEmpty.vue'
import type { LoanSummary, BankDepositSummary, BankInfoSummary, Company, PlayerBankAccountSummary } from '@/types'
import { formatCurrency, formatPercent, loanStatusClass } from '@/lib/loanHelpers'
import BankLiquidityPanel from '@/components/bank/BankLiquidityPanel.vue'
import BankCustomerView from '@/components/bank/BankCustomerView.vue'

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
      // Operating accounts for the active context (company/person) matching this bank's city currency
      const bankCurrency = bankInfo.value?.cityCurrencyCode
      const activeComp = getActiveCompany(auth.player, userCompanies.value)
      const operatingAccounts = (accountsResult.myBankAccounts ?? []).filter((a) => {
        if (!bankCurrency || a.currencyCode.toUpperCase() !== bankCurrency.toUpperCase() || a.bankBuildingId !== bankBuildingId.value) {
          return false
        }

        if (auth.player?.activeAccountType === 'COMPANY') {
          return a.ownerType === 'COMPANY' && a.companyId === activeComp?.id
        }

        return a.ownerType === 'PERSON'
      })
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

onMounted(async () => {
  auth.initFromStorage()
  if (auth.isAuthenticated && !auth.player) {
    try {
      await auth.fetchMe()
    } catch {
      // loadData will surface any user-visible auth/data errors.
    }
  }

  await loadData()
})

useTickRefresh(async () => {
  const scrollPos = saveScrollPosition()
  await loadData(true)
  await restoreScrollPosition(scrollPos)
})

const overdueLoans = computed(() => issuedLoans.value.filter((l) => l.status !== 'ACTIVE' && l.status !== 'REPAID'))

/** The active city currency code — from bankInfo when available, fallback to EUR. */
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

const activeCompany = computed(() => getActiveCompany(auth.player, userCompanies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)
const isPersonalAccountActive = computed(() => auth.player?.activeAccountType === 'PERSON')
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
          <button class="btn-back" @click="router.push('/banking')">← {{ t('bank.backToMarketplace') }}</button>
        </div>
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ bankInfo?.bankBuildingName ?? t('bank.customerView') }}</h1>
        <p class="page-subtitle flex flex-wrap items-center gap-2 text-sm text-muted sm:text-base">
          {{ bankInfo?.lenderCompanyName }} • {{ bankInfo?.cityName }}
          <span v-if="bankInfo?.cityCurrencyCode" class="currency-badge">{{ bankInfo.cityCurrencyCode }}</span>
        </p>
      </template>
      <template v-else>
        <h1 class="page-title text-4xl font-black tracking-tight text-body">{{ t('bank.customerView') }}</h1>
      </template>
    </div>

    <UiStateLoading v-if="loading" :label="t('common.loading')" />

    <UiStateError v-else-if="error" :message="error" :retry-label="t('common.retry')" @retry="loadData" />

    <template v-else>
      <!-- ── OWNER VIEW ─────────────────────────────────────────────── -->
      <template v-if="isOwner">
        <!-- ── Base Capital Deposit Required ────────────────────────── -->
        <div v-if="bankInfo && !bankInfo.baseCapitalDeposited" class="base-deposit-required">
          <div class="base-deposit-icon" aria-hidden="true">🏦</div>
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

        <!-- Liquidity Health Panel -->
        <BankLiquidityPanel v-if="bankInfo && bankInfo.baseCapitalDeposited && bankInfo.liquidityStatus" :bank-info="bankInfo" :currency-code="cityCurrency" />

        <!-- Depositors section -->
        <section v-if="bankInfo?.baseCapitalDeposited" class="depositors-section">
          <h2 class="section-title">{{ t('bank.bankDepositors') }}</h2>
          <UiStateEmpty v-if="bankDeposits.length === 0">
            {{ t('bank.noBankDepositors') }}
          </UiStateEmpty>
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
          <UiStateEmpty v-if="issuedLoans.length === 0">
            {{ t('bank.noIssuedLoans') }}
          </UiStateEmpty>
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
                      <span aria-hidden="true">🔒</span> {{ loan.collateralBuildingName }}
                      <span v-if="loan.collateralAppraisedValue" class="collateral-inline-value"> ({{ fmt(loan.collateralAppraisedValue) }}) </span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section> </template
      ><!-- end owner view -->

      <!-- Customer View -->
      <template v-else>
        <BankCustomerView
          :bank-info="bankInfo"
          :bank-building-id="bankBuildingId"
          :is-authenticated="auth.isAuthenticated"
          :active-company="activeCompany"
          :is-company-account-active="isCompanyAccountActive"
          :is-personal-account-active="isPersonalAccountActive"
          :my-operating-accounts-here="myOperatingAccountsHere"
          :my-loans-here="myLoansHere"
          :my-deposits-here="myDepositsHere"
          @data-changed="loadData(true)"
        />
      </template>
    </template>
  </main>
</template>

<style scoped>
.bank-management-view {
  margin: 0 auto;
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

.offers-table,
.loans-table {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

th,
td {
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

.bank-stat-value.deposit-rate {
  color: var(--color-success, #22c55e);
}

.bank-stat-value.lending-rate {
  color: var(--color-warning, #f59e0b);
}

.bank-stat-value.positive {
  color: var(--color-success, #22c55e);
}

.bank-stat-value.negative {
  color: var(--color-error, #ef4444);
}

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

.ineligible-tag {
  font-size: 0.78rem;
  color: var(--color-danger, #ef4444);
}

.currency-badge {
  display: inline-block;
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  padding: 0.1em 0.45em;
  border-radius: var(--radius-sm, 4px);
  background: var(--color-accent-alpha, rgba(59, 130, 246, 0.15));
  color: var(--color-accent, #3b82f6);
  vertical-align: middle;
}

.base-deposit-currency-note {
  display: flex;
  align-items: center;
  gap: var(--spacing-xs, 4px);
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  margin-top: var(--spacing-xs, 4px);
}
</style>
