<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatMoney } from '@/lib/currencyFormat'
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

const { t, locale } = useI18n()

const selectedAccount = computed<PlayerBankAccountSummary | null>(() => props.accounts.find((a) => a.id === props.modelValue) ?? null)

function formatAmount(val: number, currencyCode: string): string {
  return formatMoney(val, currencyCode, locale.value)
}

function accountLabel(a: PlayerBankAccountSummary): string {
  const last4 = a.accountNumber.slice(-4)
  return `${a.ownerDisplayName} — ${formatAmount(a.balance, a.currencyCode)} (${a.currencyCode}) #${last4}`
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
        class="w-full appearance-none rounded-lg border border-divider bg-page px-3 py-2 pr-10 text-sm font-medium text-body cursor-pointer disabled:cursor-not-allowed disabled:opacity-50 focus:border-brand focus:outline-none"
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
      <span class="pointer-events-none absolute inset-y-0 right-3 flex items-center text-muted" aria-hidden="true">
        <svg class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
          <path
            fill-rule="evenodd"
            d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z"
            clip-rule="evenodd"
          />
        </svg>
      </span>
    </div>
    <div v-if="selectedAccount" class="balance-display inline-flex items-baseline gap-1.5 flex-wrap px-2 py-1 bg-card-raised border border-divider rounded-md w-fit" aria-live="polite">
      <span class="font-bold text-brand text-sm">{{ selectedAccount.currencySymbol }}</span>
      <span
        class="font-bold text-body text-sm"
        :title="`${formatAmount(selectedAccount.balance, selectedAccount.currencyCode)} ${selectedAccount.currencyCode}`"
      >{{ formatAmount(selectedAccount.balance, selectedAccount.currencyCode) }}</span>
      <span class="text-xs text-muted font-medium">{{ selectedAccount.currencyCode }}</span>
      <span class="text-xs text-muted ml-1">{{ selectedAccount.ownerDisplayName }}</span>
    </div>
  </div>
</template>
