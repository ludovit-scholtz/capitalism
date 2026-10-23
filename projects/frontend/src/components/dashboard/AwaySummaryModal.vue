<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AwaySummary } from '@/lib/awaySummary'
import { formatMoney } from '@/lib/currencyFormat'
import { formatTickDuration } from '@/lib/gameTime'

const props = defineProps<{
  open: boolean
  summary: AwaySummary | null
}>()

const emit = defineEmits<{ close: [] }>()

const { t, locale } = useI18n()
const continueButton = ref<HTMLButtonElement | null>(null)

watch(
  () => props.open,
  async (isOpen) => {
    if (!isOpen) return
    await nextTick()
    continueButton.value?.focus()
  },
)

function money(amount: number): string {
  return formatMoney(amount, 'USD', locale.value)
}

function signedMoney(amount: number): string {
  return amount > 0 ? `+${money(amount)}` : money(amount)
}
</script>

<template>
  <teleport to="body">
    <div
      v-if="open && summary"
      class="away-summary-overlay fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      @click.self="emit('close')"
      @keydown.esc="emit('close')"
    >
      <div
        class="away-summary-modal bg-card border border-divider rounded-2xl shadow-2xl w-full max-w-md mx-4 overflow-hidden"
        role="dialog"
        aria-modal="true"
        aria-labelledby="away-summary-title"
      >
        <div class="border-b border-divider px-6 py-4">
          <h2 id="away-summary-title" class="text-lg font-bold">{{ t('awaySummary.title') }}</h2>
          <p class="mt-1 text-[0.8125rem] text-muted" :title="String(summary.ticksAway)">
            {{ t('awaySummary.period', { duration: formatTickDuration(summary.ticksAway, locale) }) }}
          </p>
        </div>

        <dl class="m-0 flex flex-col gap-3 px-6 py-5">
          <div class="flex items-baseline justify-between gap-4">
            <dt class="text-sm text-muted">{{ t('awaySummary.revenue') }}</dt>
            <dd class="m-0 text-sm font-semibold text-[var(--color-secondary)]">{{ signedMoney(summary.revenueUsd) }}</dd>
          </div>
          <div class="flex items-baseline justify-between gap-4">
            <dt class="text-sm text-muted">{{ t('awaySummary.costs') }}</dt>
            <dd class="m-0 text-sm font-semibold text-bad">{{ money(summary.costsUsd) }}</dd>
          </div>
          <div class="flex items-baseline justify-between gap-4 border-t border-divider pt-3">
            <dt class="text-sm font-semibold">{{ t('awaySummary.net') }}</dt>
            <dd class="m-0 text-base font-bold" :class="summary.netUsd >= 0 ? 'text-[var(--color-secondary)]' : 'text-bad'">{{ signedMoney(summary.netUsd) }}</dd>
          </div>
          <div v-if="summary.loanPaymentsCount > 0" class="flex items-baseline justify-between gap-4">
            <dt class="text-sm text-muted">{{ t('awaySummary.loanPayments') }}</dt>
            <dd class="m-0 text-sm font-semibold">{{ summary.loanPaymentsCount }}</dd>
          </div>
        </dl>

        <div class="flex justify-end px-6 pb-5">
          <button ref="continueButton" type="button" class="btn btn-primary" @click="emit('close')">{{ t('awaySummary.continue') }}</button>
        </div>
      </div>
    </div>
  </teleport>
</template>
