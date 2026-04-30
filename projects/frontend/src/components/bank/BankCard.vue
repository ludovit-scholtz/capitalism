<template>
  <div class="bank-card flex flex-col gap-5 rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
    <div class="bank-card-header flex items-start justify-between">
      <div>
        <h3 class="bank-card-name m-0 text-base font-semibold text-body">{{ bank.bankBuildingName }}</h3>
        <span class="bank-card-city text-xs text-muted">{{ bank.cityName }} · {{ bank.lenderCompanyName }}</span>
      </div>
    </div>
    <div class="bank-card-rates grid grid-cols-2 gap-2">
      <div class="bank-rate deposit-rate flex flex-col gap-0.5">
        <span class="rate-label text-[0.72rem] uppercase tracking-wider text-muted">{{ t('bank.depositInterestRate') }}</span>
        <span class="rate-value text-base font-bold text-success">{{ formatPercent(bank.depositInterestRatePercent) }}</span>
      </div>
      <div class="bank-rate lending-rate flex flex-col gap-0.5">
        <span class="rate-label text-[0.72rem] uppercase tracking-wider text-muted">{{ t('bank.lendingInterestRate') }}</span>
        <span class="rate-value text-base font-bold text-warning">{{ formatPercent(bank.lendingInterestRatePercent) }}</span>
      </div>
    </div>
    <div class="bank-card-capacity flex items-center justify-between text-sm">
      <span class="capacity-label text-muted">{{ t('bank.availableLendingCapacity') }}</span>
      <span class="capacity-value font-semibold" :class="bank.availableLendingCapacity > 0 ? 'text-success' : 'text-muted'">
        {{ formatCurrency(bank.availableLendingCapacity) }}
      </span>
    </div>
    <div class="bank-card-actions flex flex-col gap-2">
      <button class="btn btn-secondary btn-sm" @click="$emit('navigate-to-bank', bank.bankBuildingId)">
        {{ t('bank.viewBankDetail') }}
      </button>
      <button v-if="isAuthenticated" class="btn btn-primary btn-sm bank-deposit-btn w-full" @click="$emit('open-deposit-modal', bank)">
        {{ t('bank.makeDeposit') }}
      </button>
      <router-link v-else to="/login" class="btn btn-primary btn-sm">{{ t('auth.login') }}</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { BankInfoSummary } from '@/types'
import { formatCurrency, formatPercent } from '@/lib/loanHelpers'

const { t } = useI18n()

defineProps<{
  bank: BankInfoSummary
  isAuthenticated: boolean
}>()

defineEmits<{
  'navigate-to-bank': [bankBuildingId: string]
  'open-deposit-modal': [bank: BankInfoSummary]
}>()
</script>
