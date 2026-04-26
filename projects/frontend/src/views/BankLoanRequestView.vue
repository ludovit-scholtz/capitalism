<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { getActiveCompany } from '@/lib/accountContext'
import type { BankInfoSummary, CollateralEligibilitySummary, Company } from '@/types'
import { computePaymentAmount, computeTotalPayments, computeTotalRepayment, formatCurrency, formatLoanDuration, formatPercent } from '@/lib/loanHelpers'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const bankBuildingId = computed(() => route.params.buildingId as string)

const loading = ref(true)
const submitting = ref(false)
const error = ref<string | null>(null)
const success = ref(false)

const bankInfo = ref<BankInfoSummary | null>(null)
const userCompanies = ref<Company[]>([])
const collateralBuildings = ref<CollateralEligibilitySummary[]>([])

const principalAmount = ref(0)
const durationTicks = ref(8760)
const selectedCollateralBuildingId = ref<string | null>(null)

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

const MY_COLLATERAL_BUILDINGS_QUERY = `
  {
    myCollateralBuildings {
      buildingId
      buildingName
      buildingType
      level
      appraisedValue
      maxBorrowable
      existingSecuredExposure
      remainingBorrowingCapacity
      isEligible
      ineligibilityReason
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

const cityCurrency = computed(() => bankInfo.value?.cityCurrencyCode ?? 'EUR')
const fmt = (amount: number) => formatCurrency(amount, cityCurrency.value)

const activeCompany = computed(() => getActiveCompany(auth.player, userCompanies.value))
const isCompanyAccountActive = computed(() => auth.player?.activeAccountType === 'COMPANY' && !!activeCompany.value)
const maxPrincipal = computed(() => Math.max(bankInfo.value?.availableLendingCapacity ?? 0, 0))

const selectedCollateral = computed<CollateralEligibilitySummary | null>(
  () => collateralBuildings.value.find((b) => b.buildingId === selectedCollateralBuildingId.value) ?? null,
)

const normalizedDurationTicks = computed(() => {
  const value = Number(durationTicks.value)
  if (!Number.isFinite(value)) {
    return 8760
  }

  return Math.min(87600, Math.max(24, Math.round(value)))
})

const collateralRequiredWarning = computed(() => {
  if (principalAmount.value <= 0) return null
  if (!selectedCollateralBuildingId.value) {
    return t('bank.collateralRequired')
  }

  return null
})

const collateralCapacityWarning = computed(() => {
  if (!selectedCollateral.value || principalAmount.value <= 0) return null
  if (principalAmount.value > selectedCollateral.value.remainingBorrowingCapacity) {
    return t('bank.collateralExceedsLimit')
  }

  return null
})

const estimatedPaymentAmount = computed(() => {
  if (!bankInfo.value || principalAmount.value <= 0) return 0
  return computePaymentAmount(principalAmount.value, bankInfo.value.lendingInterestRatePercent, normalizedDurationTicks.value)
})

const estimatedTotalRepayment = computed(() => {
  if (!bankInfo.value || principalAmount.value <= 0) return 0
  return computeTotalRepayment(principalAmount.value, bankInfo.value.lendingInterestRatePercent, normalizedDurationTicks.value)
})

const estimatedTotalPayments = computed(() => computeTotalPayments(normalizedDurationTicks.value))

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const [infoResult, companiesResult, collateralResult] = await Promise.all([
      gqlRequest<{ bankInfo: BankInfoSummary }>(BANK_INFO_QUERY, { id: bankBuildingId.value }),
      auth.isAuthenticated ? gqlRequest<{ me: { companies: Company[] } }>(MY_COMPANIES_QUERY) : Promise.resolve({ me: { companies: [] } }),
      auth.isAuthenticated ? gqlRequest<{ myCollateralBuildings: CollateralEligibilitySummary[] }>(MY_COLLATERAL_BUILDINGS_QUERY) : Promise.resolve({ myCollateralBuildings: [] }),
    ])

    bankInfo.value = infoResult.bankInfo ?? null
    userCompanies.value = companiesResult.me?.companies ?? []
    collateralBuildings.value = collateralResult.myCollateralBuildings ?? []

    principalAmount.value = Math.max(0, Math.min(100000, maxPrincipal.value))
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  } finally {
    loading.value = false
  }
}

async function submitLoanRequest() {
  if (!bankInfo.value || !activeCompany.value || !selectedCollateralBuildingId.value) return

  submitting.value = true
  error.value = null
  try {
    await gqlRequest(ACCEPT_LOAN_MUTATION, {
      input: {
        loanOfferId: bankInfo.value.bankBuildingId,
        borrowerCompanyId: activeCompany.value.id,
        principalAmount: principalAmount.value,
        durationTicks: normalizedDurationTicks.value,
        collateralBuildingId: selectedCollateralBuildingId.value,
      },
    })

    success.value = true
    await router.push({ name: 'bank-management', params: { buildingId: bankBuildingId.value } })
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  } finally {
    submitting.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <main class="container mx-auto px-4 pb-16 pt-6 sm:px-6 lg:px-8 lg:pb-20 lg:pt-8">
    <div class="mb-8 flex flex-col gap-3 lg:mb-10">
      <button class="btn-back w-fit" @click="router.push({ name: 'bank-management', params: { buildingId: bankBuildingId } })">← {{ t('bank.viewBankDetail') }}</button>
      <h1 class="text-4xl font-black tracking-tight text-body">{{ t('bank.confirmAccept') }}</h1>
      <p v-if="bankInfo" class="text-sm text-muted sm:text-base">
        {{ bankInfo.bankBuildingName }} · {{ bankInfo.cityName }} · {{ formatPercent(bankInfo.lendingInterestRatePercent) }} {{ t('bank.perYear') }}
      </p>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="spinner" />
      <span>{{ t('common.loading') }}</span>
    </div>

    <div v-else-if="error" class="error-state">
      <p class="error-message">{{ error }}</p>
      <button class="btn btn-secondary" @click="loadData">{{ t('common.retry') }}</button>
    </div>

    <section v-else-if="bankInfo" class="loan-request-form-card rounded-3xl border border-divider bg-card p-6 shadow-sm sm:p-8">
      <div class="mb-6 rounded-2xl border border-divider bg-card-raised p-4">
        <div class="summary-row flex items-center justify-between gap-4 text-sm">
          <span>{{ t('bank.maxPrincipal') }}</span>
          <strong>{{ fmt(maxPrincipal) }}</strong>
        </div>
        <div class="summary-row flex items-center justify-between gap-4 text-sm">
          <span>{{ t('bank.duration') }}</span>
          <strong>{{ formatLoanDuration(normalizedDurationTicks) }}</strong>
        </div>
      </div>

      <div class="form-group mb-5 flex flex-col gap-2">
        <label for="principal-amount" class="text-sm font-semibold text-body">{{ t('bank.principalAmount') }}</label>
        <input
          id="principal-amount"
          v-model.number="principalAmount"
          type="number"
          min="1000"
          :max="maxPrincipal"
          step="1000"
          class="form-input rounded-2xl border border-divider bg-card px-4 py-3 text-base text-body"
        />
      </div>

      <div class="form-group mb-5 flex flex-col gap-2">
        <label for="duration-ticks" class="text-sm font-semibold text-body">{{ t('bank.durationTicks') }}</label>
        <input
          id="duration-ticks"
          v-model.number="durationTicks"
          type="number"
          min="24"
          max="87600"
          step="24"
          class="form-input rounded-2xl border border-divider bg-card px-4 py-3 text-base text-body"
        />
      </div>

      <div class="repayment-summary mb-5 rounded-2xl border border-divider bg-card-raised p-4">
        <div class="summary-row flex items-center justify-between gap-4 text-sm">
          <span>{{ t('bank.paymentAmount') }}</span>
          <strong>{{ fmt(estimatedPaymentAmount) }} × {{ estimatedTotalPayments }}</strong>
        </div>
        <div class="summary-row total-row mt-2 flex items-center justify-between gap-4 text-sm">
          <span>{{ t('bank.totalRepayment') }}</span>
          <strong>{{ fmt(estimatedTotalRepayment) }}</strong>
        </div>
      </div>

      <div class="form-group collateral-group mb-5 flex flex-col gap-2 border-t border-divider pt-5">
        <label class="text-sm font-semibold text-body">{{ t('bank.collateral') }}</label>
        <p class="form-hint text-sm text-muted">{{ t('bank.collateralHint') }}</p>

        <div v-if="collateralBuildings.length === 0" class="form-hint muted-hint text-sm text-muted">{{ t('bank.noBuildingsForCollateral') }}</div>
        <div v-else class="collateral-list mt-2 flex flex-col gap-2">
          <label
            v-for="b in collateralBuildings"
            :key="b.buildingId"
            class="collateral-option flex items-start gap-3 rounded-xl border border-divider p-3"
            :class="{ selected: selectedCollateralBuildingId === b.buildingId, ineligible: !b.isEligible }"
          >
            <input type="radio" :value="b.buildingId" v-model="selectedCollateralBuildingId" class="collateral-radio mt-1" :disabled="!b.isEligible" />
            <div class="collateral-option-info flex flex-col gap-1">
              <span class="collateral-option-name font-semibold text-body">{{ b.buildingName }}</span>
              <span v-if="!b.isEligible" class="ineligible-tag text-xs text-danger">{{ b.ineligibilityReason ?? t('bank.collateralAlreadyPledged') }}</span>
              <div class="collateral-stats flex flex-col gap-1 text-xs text-muted sm:flex-row sm:gap-4">
                <span>{{ t('bank.collateralAppraisedValue') }}: {{ fmt(b.appraisedValue) }}</span>
                <span>{{ t('bank.collateralMaxBorrowable') }}: {{ fmt(b.maxBorrowable) }}</span>
              </div>
            </div>
          </label>
        </div>
      </div>

      <div v-if="selectedCollateral" class="collateral-selected-summary mb-4 flex flex-col gap-2 rounded-2xl border border-divider bg-card-raised p-4">
        <strong class="text-body">{{ selectedCollateral.buildingName }}</strong>
        <span class="text-sm text-muted">{{ t('bank.remainingCapacity') }}: {{ fmt(selectedCollateral.remainingBorrowingCapacity) }}</span>
        <div class="capacity-bar-wrap h-2 rounded-full bg-surface-low">
          <div class="capacity-bar h-2 rounded-full bg-accent" :style="{ width: Math.min(100, (principalAmount / selectedCollateral.maxBorrowable) * 100) + '%' }" />
        </div>
      </div>

      <div v-if="collateralRequiredWarning" class="error-inline mb-2 text-sm text-danger">{{ collateralRequiredWarning }}</div>
      <div v-if="collateralCapacityWarning" class="error-inline mb-2 text-sm text-danger">{{ collateralCapacityWarning }}</div>
      <p class="risk-warning mb-4 text-sm text-muted">⚠ {{ t('bank.riskWarning') }}</p>
      <div v-if="success" class="success-message mb-3">{{ t('bank.loanAcceptedSuccess') }}</div>

      <button
        class="btn btn-primary"
        :disabled="
          submitting ||
          !auth.isAuthenticated ||
          !isCompanyAccountActive ||
          principalAmount <= 0 ||
          principalAmount > maxPrincipal ||
          durationTicks < 24 ||
          durationTicks > 87600 ||
          !!collateralRequiredWarning ||
          !!collateralCapacityWarning
        "
        @click="submitLoanRequest"
      >
        {{ submitting ? t('common.loading') : t('bank.acceptLoan') }}
      </button>
    </section>
  </main>
</template>

<style scoped>
.btn-back {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  border: none;
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  padding: 0;
}

.loading-state,
.error-state {
  display: flex;
  align-items: center;
  gap: var(--spacing-sm);
  padding: var(--spacing-xl);
  justify-content: center;
}

.collateral-option.selected {
  border-color: var(--color-primary, #3b82f6);
  background: rgba(59, 130, 246, 0.06);
}

.collateral-option.ineligible {
  opacity: 0.55;
  cursor: not-allowed;
}
</style>
