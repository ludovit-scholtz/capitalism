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
  <div class="forex-ba-selector">
    <label v-if="label" :for="id" class="selector-label">{{ label }}</label>
    <div class="selector-wrap">
      <select
        :id="id"
        :value="modelValue"
        :disabled="disabled"
        class="account-select"
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
    <div v-if="selectedAccount" class="balance-display" aria-live="polite">
      <span class="balance-symbol">{{ selectedAccount.currencySymbol }}</span>
      <span class="balance-value">{{ formatAmount(selectedAccount.balance) }}</span>
      <span class="balance-code">{{ selectedAccount.currencyCode }}</span>
      <span class="company-name">{{ selectedAccount.companyName }}</span>
    </div>
  </div>
</template>

<style scoped>
.forex-ba-selector {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.selector-label {
  font-size: 0.82rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.selector-wrap {
  position: relative;
}

.account-select {
  width: 100%;
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 0.55rem 0.75rem;
  color: var(--color-text-primary);
  font-size: 0.93rem;
  font-weight: 500;
  cursor: pointer;
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20' fill='%236b7280'%3E%3Cpath fill-rule='evenodd' d='M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z' clip-rule='evenodd'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 0.6rem center;
  background-size: 1.1rem;
  padding-right: 2rem;
}

.account-select:focus {
  outline: 2px solid var(--color-accent, #4f8ef7);
  outline-offset: 1px;
}

.account-select:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.balance-display {
  display: flex;
  align-items: baseline;
  gap: 0.25rem;
  padding: 0.3rem 0.5rem;
  background: var(--color-surface-alt, #1e2a3a);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  width: fit-content;
  flex-wrap: wrap;
}

.balance-symbol {
  font-weight: 700;
  color: var(--color-accent, #4f8ef7);
  font-size: 0.95rem;
}

.balance-value {
  font-weight: 700;
  color: var(--color-text-primary);
  font-size: 0.95rem;
}

.balance-code {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  font-weight: 500;
}

.company-name {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  font-weight: 400;
  margin-left: 0.25rem;
}
</style>
