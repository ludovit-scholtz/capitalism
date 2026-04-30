<template>
  <div class="bank-borrow-card flex flex-col gap-4 rounded-2xl border border-divider bg-card-raised p-5 shadow-sm">
    <div class="bank-borrow-card-header flex items-start justify-between gap-2">
      <div class="bank-borrow-identity flex items-center gap-2">
        <span class="bank-borrow-icon shrink-0 text-2xl" aria-hidden="true">🏦</span>
        <div>
          <span class="bank-borrow-name block font-bold text-body" style="font-size: 0.9375rem">{{ bank.bankBuildingName }}</span>
          <span class="bank-borrow-lender block text-xs text-muted">{{ bank.lenderCompanyName }}</span>
        </div>
      </div>
      <div class="bank-borrow-rate shrink-0 text-right">
        <span class="rate-value block text-xl font-bold text-brand">{{ formatPercent(bank.lendingInterestRatePercent) }}</span>
        <span class="rate-label text-xs text-muted">{{ t('bank.perYear') }}</span>
      </div>
    </div>
    <div class="bank-borrow-stats flex gap-4">
      <div class="borrow-stat flex flex-1 flex-col gap-0.5">
        <span class="stat-label text-[0.72rem] uppercase tracking-wider text-muted">{{ t('bank.availableCapacity') }}</span>
        <span class="stat-value text-sm font-semibold" :class="bank.availableLendingCapacity > 0 ? 'text-body' : 'text-error'">
          {{ formatCurrency(bank.availableLendingCapacity) }}
        </span>
      </div>
      <div class="borrow-stat flex flex-1 flex-col gap-0.5">
        <span class="stat-label text-[0.72rem] uppercase tracking-wider text-muted">{{ t('common.city') }}</span>
        <span class="stat-value text-sm font-semibold text-body">{{ bank.cityName }}</span>
      </div>
    </div>
    <div class="bank-borrow-card-footer mt-auto">
      <router-link :to="`/bank/${bank.bankBuildingId}`" class="btn btn-primary btn-sm">
        {{ t('bank.visitBankToBorrow') }}
      </router-link>
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
}>()
</script>
