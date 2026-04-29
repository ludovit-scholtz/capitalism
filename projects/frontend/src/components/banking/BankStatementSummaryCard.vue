<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import CurrencyAmount from '@/components/numbers/CurrencyAmount.vue'

defineProps<{
  ownerDisplayName: string
  accountNumber: string | null
  balance: number
  currencyCode: string
  totalEntries: number
}>()

const { t } = useI18n()
</script>

<template>
  <div class="flex flex-wrap items-center gap-6 bg-card border border-divider rounded-xl px-6 py-4 mb-6" aria-label="Account summary">
    <div class="flex items-center gap-2">
      <span class="text-2xl">🏢</span>
      <div class="flex flex-col gap-0.5">
        <span class="text-lg font-bold text-body">{{ ownerDisplayName }}</span>
        <span v-if="accountNumber" class="text-xs text-muted">{{ t('bankStatement.accountNumber') }}: {{ accountNumber }}</span>
      </div>
    </div>
    <div class="flex flex-col gap-0 ml-auto sm:ml-auto">
      <span class="text-xs font-semibold text-muted uppercase tracking-wide">
        {{ t('bankStatement.currentBalance') }}
      </span>
      <span class="balance-amount text-2xl font-extrabold" :class="balance >= 0 ? 'text-good' : 'text-bad'">
        <CurrencyAmount :amount="balance" :currency="currencyCode" />
      </span>
    </div>
    <div class="flex gap-4 text-xs text-muted">
      <span>{{ t('bankStatement.totalEntries') }}: {{ totalEntries }}</span>
      <span>{{ t('bankStatement.currency') }}: {{ currencyCode }}</span>
    </div>
  </div>
</template>
