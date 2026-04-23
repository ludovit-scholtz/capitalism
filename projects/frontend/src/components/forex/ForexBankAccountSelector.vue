<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PlayerBankAccountSummary } from '@/types'

interface Props {
  modelValue: string
  accounts: PlayerBankAccountSummary[]
  label?: string
  id?: string
  disabled?: boolean
}

interface Emits {
  (e: 'update:modelValue', value: string): void
}

const props = withDefaults(defineProps<Props>(), {
  label: '',
  id: 'forex-bank-account-selector',
  disabled: false,
})
const emit = defineEmits<Emits>()

const { t } = useI18n()

const selectedAccount = computed<PlayerBankAccountSummary | null>(
  () => props.accounts.find((a) => a.id === props.modelValue) ?? null,
)

function formatAmount(val: number): string {
  return new Intl.NumberFormat('en', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(val)
}

function accountLabel(a: PlayerBankAccountSummary): string {
  const last4 = a.accountNumber.slice(-4)
  return `${a.companyName} — ${a.currencySymbol}${formatAmount(a.balance)} (${a.currencyCode}) #${last4}`
}

function onSelect(event: Event) {
  const target = event.target as HTMLSelectElement
  emit('update:modelValue', target.value)
}
</script>

<template>
  <div class="forex-ba-selector flex flex-col gap-1.5">
    <label v-if="label" :for="id" class="text-xs font-semibold text-muted uppercase tracking-wide">
      {{ label }}
    </label>
    <div class="relative">
      <select
        :id="id"
        :value="modelValue"
        :disabled="disabled"
        class="account-select w-full bg-page border border-divider rounded-lg px-3 py-2 pr-8 text-body text-sm font-medium cursor-pointer appearance-none disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:border-brand"
        :aria-label="label || t('forex.selectAccount')"
        @change="onSelect"
      >
        <option v-if="accounts.length === 0" value="" disabled>
          {{ t('forex.noAccounts') }}
        </option>
        <option v-for="acc in accounts" :key="acc.id" :value="acc.id">
          {{ accountLabel(acc) }}
        </option>
      </select>
    </div>
    <div
      v-if="selectedAccount"
      class="balance-display inline-flex items-baseline gap-1.5 flex-wrap px-2 py-1 bg-card-raised border border-divider rounded-md w-fit"
      aria-live="polite"
    >
      <span class="font-bold text-brand text-sm">{{ selectedAccount.currencySymbol }}</span>
      <span class="font-bold text-body text-sm">{{ formatAmount(selectedAccount.balance) }}</span>
      <span class="text-xs text-muted font-medium">{{ selectedAccount.currencyCode }}</span>
      <span class="text-xs text-muted ml-1">{{ selectedAccount.companyName }}</span>
    </div>
  </div>
</template>

<style scoped>
/* Custom dropdown arrow — cannot be expressed as a Tailwind utility */
.account-select {
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20' fill='%236b7280'%3E%3Cpath fill-rule='evenodd' d='M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z' clip-rule='evenodd'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 0.6rem center;
  background-size: 1.1rem;
}
</style>
