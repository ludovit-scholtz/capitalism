<template>
  <div class="modal-overlay fixed inset-0 z-[1000] flex items-center justify-center bg-black/50 p-4" @click.self="$emit('close')">
    <div class="modal w-full max-w-lg overflow-y-auto rounded-2xl border border-divider bg-card shadow-2xl" style="max-height: 90vh" role="dialog" :aria-label="t('bank.confirmAccept')">
      <div class="modal-header flex items-center justify-between border-b border-divider px-6 py-5">
        <h2 class="text-xl font-bold text-body">{{ t('bank.confirmAccept') }}</h2>
        <button class="text-muted hover:text-body text-xl" :aria-label="t('common.close')" @click="$emit('close')">×</button>
      </div>

      <div class="flex flex-col gap-5 px-6 py-6">
        <!-- Loan summary -->
        <div class="loan-summary rounded-xl border border-divider bg-card-raised p-4">
          <div class="summary-row flex items-center justify-between py-1 text-sm">
            <span class="text-muted">{{ t('bank.lender') }}</span>
            <strong>{{ offer.lenderCompanyName }}</strong>
          </div>
          <div class="summary-row flex items-center justify-between py-1 text-sm">
            <span class="text-muted">{{ t('bank.interestRate') }}</span>
            <strong>{{ formatPercent(offer.annualInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
          </div>
          <div class="summary-row flex items-center justify-between py-1 text-sm">
            <span class="text-muted">{{ t('bank.duration') }}</span>
            <strong>{{ formatLoanDuration(offer.durationTicks) }}</strong>
          </div>
        </div>

        <!-- Borrower company -->
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-body" for="borrow-company">{{ t('bank.borrower') }}</label>
          <div id="borrow-company" class="active-borrower-company flex items-center justify-between gap-3 rounded-lg border border-divider bg-card-raised px-3 py-3">
            <strong>{{ activeCompany?.name ?? t('bank.activeBorrowerCompany') }}</strong>
            <span class="text-sm text-muted">{{ activeCompany ? formatCurrency(activeCompany.cash) : '' }}</span>
          </div>
          <span class="text-xs text-muted">{{ t('bank.borrowerHint') }}</span>
        </div>

        <!-- Principal amount -->
        <div class="flex flex-col gap-1">
          <label class="text-sm font-medium text-body" for="principal-amount">{{ t('bank.principalAmount') }}</label>
          <input
            id="principal-amount"
            v-model.number="principalAmount"
            type="number"
            :min="1000"
            :max="Math.min(offer.maxPrincipalPerLoan, offer.remainingCapacity)"
            step="1000"
            class="rounded-lg border border-divider bg-card px-3 py-2 text-sm text-body focus:border-brand focus:outline-none"
          />
          <span class="text-xs text-muted">{{ t('bank.companyCashAvailable', { amount: formatCurrency(selectedCompanyCash) }) }}</span>
        </div>

        <!-- Repayment summary -->
        <div class="repayment-summary rounded-xl border border-divider bg-card-raised p-4">
          <div class="summary-row flex items-center justify-between py-1 text-sm">
            <span class="text-muted">{{ t('bank.originalPrincipal') }}</span>
            <strong>{{ formatCurrency(principalAmount) }}</strong>
          </div>
          <div class="summary-row flex items-center justify-between py-1 text-sm">
            <span class="text-muted">{{ t('bank.paymentAmount') }}</span>
            <strong>{{ formatCurrency(estimatedPaymentAmount) }} × {{ estimatedTotalPayments }}</strong>
          </div>
          <div class="summary-row total-row border-t border-divider pt-2 mt-2 flex items-center justify-between py-1 text-sm font-semibold">
            <span>{{ t('bank.totalRepayment') }}</span>
            <strong>{{ formatCurrency(estimatedTotalRepayment) }}</strong>
          </div>
        </div>

        <!-- Collateral selection -->
        <div class="collateral-group flex flex-col gap-2 border-t border-divider pt-4">
          <label class="text-sm font-medium text-body">{{ t('bank.collateralOptional') }}</label>
          <p class="text-xs text-muted">{{ t('bank.collateralHint') }}</p>
          <div v-if="collateralLoadError" class="text-xs text-error">{{ collateralLoadError }}</div>
          <div v-else-if="collateralBuildings.length === 0" class="text-xs italic text-muted">{{ t('bank.noBuildingsForCollateral') }}</div>
          <div v-else class="collateral-list flex max-h-52 flex-col gap-1 overflow-y-auto">
            <!-- None option -->
            <label class="collateral-option flex cursor-pointer items-start gap-2 rounded-lg border border-divider bg-card px-3 py-2" :class="{ selected: selectedCollateralBuildingId === null }">
              <input type="radio" :value="null" v-model="selectedCollateralBuildingId" class="collateral-radio mt-0.5 shrink-0" />
              <span class="text-sm">{{ t('bank.collateralNone') }}</span>
            </label>
            <!-- Buildings -->
            <label
              v-for="b in collateralBuildings"
              :key="b.buildingId"
              class="collateral-option flex cursor-pointer items-start gap-2 rounded-lg border border-divider bg-card px-3 py-2"
              :class="{ selected: selectedCollateralBuildingId === b.buildingId, ineligible: !b.isEligible }"
            >
              <input type="radio" :value="b.buildingId" v-model="selectedCollateralBuildingId" :disabled="!b.isEligible" class="collateral-radio mt-0.5 shrink-0" />
              <span class="flex flex-col gap-0.5 min-w-0">
                <span class="text-sm font-semibold text-body">{{ b.buildingName }}</span>
                <span class="text-xs text-muted">{{ b.buildingType }} · Lv{{ b.level }}</span>
                <span v-if="!b.isEligible" class="ineligible-tag text-[0.72rem] text-error">{{ t('bank.collateralAlreadyPledged') }}</span>
                <span v-else class="flex flex-wrap gap-2 mt-0.5 text-xs text-muted">
                  <span>{{ t('bank.collateralAppraisedValue') }}: {{ formatCurrency(b.appraisedValue) }}</span>
                  <span class="text-brand font-semibold">{{ t('bank.collateralMaxBorrowable') }}: {{ formatCurrency(b.maxBorrowable) }}</span>
                  <span v-if="b.existingSecuredExposure > 0" class="text-warning"> {{ t('bank.collateralExistingExposure') }}: {{ formatCurrency(b.existingSecuredExposure) }} </span>
                  <span class="text-success font-semibold">{{ t('bank.collateralRemainingCapacity') }}: {{ formatCurrency(b.remainingBorrowingCapacity) }}</span>
                </span>
              </span>
            </label>
          </div>

          <!-- Collateral warnings -->
          <p v-if="collateralRequiredWarning" class="risk-warning rounded-lg p-3 text-sm">⚠ {{ collateralRequiredWarning }}</p>
          <p v-if="collateralCapacityWarning" class="risk-warning collateral-warning rounded-lg p-3 text-sm">⚠ {{ collateralCapacityWarning }}</p>

          <!-- Selected collateral summary bar -->
          <div v-if="selectedCollateral" class="collateral-selected-summary flex flex-wrap items-center gap-2 rounded-lg border border-brand/20 bg-brand/5 p-3 text-xs">
            <span class="text-muted">{{ t('bank.collateralBuilding') }}:</span>
            <strong>{{ selectedCollateral.buildingName }}</strong>
            <span class="capacity-bar-wrap h-1.5 min-w-20 flex-1 overflow-hidden rounded-full bg-divider">
              <span
                class="capacity-bar-fill block h-full rounded-full bg-brand transition-[width]"
                :style="{ width: Math.min(100, (principalAmount / selectedCollateral.maxBorrowable) * 100).toFixed(1) + '%' }"
                :class="{ 'bg-error': principalAmount > selectedCollateral.remainingBorrowingCapacity }"
              ></span>
            </span>
            <span class="whitespace-nowrap text-muted">
              {{ formatCurrency(principalAmount) }} / {{ formatCurrency(selectedCollateral.maxBorrowable) }} ({{
                Math.min(100, Math.round((principalAmount / selectedCollateral.maxBorrowable) * 100))
              }}% LTV)
            </span>
          </div>
        </div>

        <!-- Risk warning -->
        <p class="risk-warning rounded-lg p-3 text-sm">⚠ {{ t('bank.riskWarning') }}</p>

        <div v-if="error" class="rounded-lg bg-error/10 p-3 text-sm text-error">{{ error }}</div>
      </div>

      <div class="flex justify-end gap-3 border-t border-divider px-6 py-5">
        <button class="btn btn-secondary" @click="$emit('close')">{{ t('common.cancel') }}</button>
        <button class="btn btn-primary" :disabled="loading || principalAmount <= 0 || !!collateralRequiredWarning || !!collateralCapacityWarning" @click="handleConfirm">
          <span v-if="loading">{{ t('common.loading') }}</span>
          <span v-else>{{ t('bank.acceptLoan') }}</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { LoanOfferSummary, Company, CollateralEligibilitySummary } from '@/types'
import { formatCurrency, formatPercent, formatLoanDuration, computeTotalRepayment, computePaymentAmount, computeTotalPayments } from '@/lib/loanHelpers'

const { t } = useI18n()

const props = defineProps<{
  offer: LoanOfferSummary
  activeCompany: Company | null
  selectedCompanyCash: number
  collateralBuildings: CollateralEligibilitySummary[]
  collateralLoadError: string | null
  loading: boolean
  error: string | null
}>()

const emit = defineEmits<{
  close: []
  confirm: [{ principalAmount: number; collateralBuildingId: string | null }]
}>()

const principalAmount = ref<number>(Math.min(1000, props.offer.maxPrincipalPerLoan))
const selectedCollateralBuildingId = ref<string | null>(null)

const estimatedTotalRepayment = computed(() => (principalAmount.value > 0 ? computeTotalRepayment(principalAmount.value, props.offer.annualInterestRatePercent, props.offer.durationTicks) : 0))
const estimatedPaymentAmount = computed(() => (principalAmount.value > 0 ? computePaymentAmount(principalAmount.value, props.offer.annualInterestRatePercent, props.offer.durationTicks) : 0))
const estimatedTotalPayments = computed(() => computeTotalPayments(props.offer.durationTicks))
const selectedCollateral = computed(() => props.collateralBuildings.find((b) => b.buildingId === selectedCollateralBuildingId.value) ?? null)

const collateralCapacityWarning = computed(() => {
  if (!selectedCollateral.value || principalAmount.value <= 0) return null
  if (principalAmount.value > selectedCollateral.value.remainingBorrowingCapacity) return t('bank.collateralExceedsLimit')
  return null
})
const collateralRequiredWarning = computed(() => {
  if (principalAmount.value <= 0) return null
  if (!selectedCollateralBuildingId.value) return t('bank.collateralRequired')
  return null
})

function handleConfirm() {
  if (principalAmount.value <= 0 || collateralRequiredWarning.value || collateralCapacityWarning.value) return
  emit('confirm', { principalAmount: principalAmount.value, collateralBuildingId: selectedCollateralBuildingId.value })
}
</script>

<style scoped>
.collateral-option.selected {
  border-color: var(--color-primary, #3b82f6);
  background: rgba(59, 130, 246, 0.08);
}
.collateral-option.ineligible {
  opacity: 0.55;
  cursor: not-allowed;
}
.risk-warning {
  background: rgba(251, 191, 36, 0.12);
  color: #fbbf24;
}
.collateral-warning {
  color: #ef4444;
  background: rgba(239, 68, 68, 0.08);
}
</style>
