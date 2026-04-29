<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatMoneyByFieldSize, formatCompactMoney, formatCurrencyTitle } from '@/lib/currencyFormat'

interface Props {
  /** Numeric amount to display. */
  amount: number
  /** ISO 4217 currency code (e.g. 'EUR', 'CZK', 'USD'). */
  currency?: string
  /** When true renders compact notation (e.g. "€1.2M"). Defaults to false. */
  compact?: boolean
  /** Optional character budget for the rendered field; when exceeded, compact notation is used. */
  maxChars?: number | null
  /** When true prepends a +/− sign for positive/negative deltas. Defaults to false. */
  sign?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  currency: 'EUR',
  compact: false,
  maxChars: null,
  sign: false,
})

const { locale } = useI18n()

const displayValue = computed<string>(() => {
  const { amount, currency, compact, sign } = props
  let formatted: string

  if (compact) {
    formatted = formatCompactMoney(amount, currency, locale.value)
  } else {
    formatted = formatMoneyByFieldSize(amount, currency, locale.value, props.maxChars)
  }

  if (sign && isFinite(amount) && !isNaN(amount) && amount > 0) {
    formatted = `+${formatted}`
  }

  return formatted
})

/** Full-precision value exposed via title/tooltip so players can inspect exact amounts. */
const titleValue = computed<string>(() => formatCurrencyTitle(props.amount, props.currency, locale.value))
</script>

<template>
  <span class="currency-amount" :title="titleValue">{{ displayValue }}</span>
</template>
