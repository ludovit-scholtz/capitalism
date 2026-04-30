<template>
  <div class="modal-overlay fixed inset-0 z-[1000] flex items-center justify-center bg-black/60 p-4" @click.self="$emit('close')">
    <div class="w-full max-w-xl overflow-y-auto rounded-[28px] border border-divider bg-card shadow-2xl" role="dialog" :aria-label="t('bank.makeDeposit')">
      <div class="flex items-center justify-between border-b border-divider px-6 py-5 sm:px-8 sm:py-6">
        <h2 class="text-2xl font-bold text-body">{{ t('bank.makeDeposit') }}</h2>
        <button class="text-muted hover:text-body text-xl" :aria-label="t('common.close')" @click="$emit('close')">×</button>
      </div>
      <div class="flex flex-col gap-6 px-6 py-6 sm:px-8 sm:py-8">
        <div class="loan-summary rounded-2xl border border-divider bg-card-raised p-5">
          <div class="flex items-center justify-between gap-4 py-1.5 text-sm">
            <span class="text-muted">Bank</span>
            <strong>{{ bank.bankBuildingName }}</strong>
          </div>
          <div class="flex items-center justify-between gap-4 py-1.5 text-sm">
            <span class="text-muted">{{ t('bank.depositInterestRate') }}</span>
            <strong>{{ formatPercent(bank.depositInterestRatePercent) }} {{ t('bank.perYear') }}</strong>
          </div>
        </div>
        <p class="rounded-2xl border border-divider bg-card-raised px-4 py-3 text-sm text-muted">
          {{ t('bank.zeroBalanceFundingHint') }}
        </p>
        <div v-if="success" class="text-sm text-success">{{ t('bank.depositCreated') }}</div>
        <div v-if="error" class="rounded-lg bg-error/10 p-3 text-sm text-error">{{ error }}</div>
      </div>
      <div class="flex justify-end gap-3 border-t border-divider px-6 py-5 sm:px-8 sm:py-6">
        <button class="btn btn-secondary" @click="$emit('close')">{{ t('common.cancel') }}</button>
        <button class="btn btn-primary" :disabled="loading" @click="$emit('confirm')">
          <span v-if="loading">{{ t('common.loading') }}</span>
          <span v-else>{{ t('bank.confirmDeposit') }}</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { BankInfoSummary } from '@/types'
import { formatPercent } from '@/lib/loanHelpers'

const { t } = useI18n()

defineProps<{
  bank: BankInfoSummary
  loading: boolean
  error: string | null
  success: boolean
}>()

defineEmits<{
  close: []
  confirm: []
}>()
</script>
