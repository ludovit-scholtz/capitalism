<script setup lang="ts">
import { computed, ref, watch, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import BankAccountSelector from '@/components/banking/BankAccountSelector.vue'
import ForexBankAccountSelector from '@/components/forex/ForexBankAccountSelector.vue'
import type { City, FxRate, ForexQuote, ForexTradeResult, CurrencyBalance, PlayerBankAccountSummary } from '@/types'

const props = defineProps<{
  rates: FxRate[]
  balances: CurrencyBalance[]
  contextScopedBankAccounts: PlayerBankAccountSummary[]
  hasBankAccounts: boolean
  selectedCity: City | null
  baseCurrencySymbol: string
  baseCurrencyCode: string
  toBalances: CurrencyBalance[]
  availableCurrencies?: string[]
  initialToCurrency?: string
}>()

const emit = defineEmits<{ refresh: [] }>()

const { t } = useI18n()
const auth = useAuthStore()

const fromCurrency = ref('EUR')
const toCurrency = ref('CZK')
const amount = ref<number | null>(null)
const fromBankAccountId = ref<string>('')
const toBankAccountId = ref<string>('')
const quote = ref<ForexQuote | null>(null)
const quoteError = ref<string | null>(null)
const quoteLoading = ref(false)
const swapResult = ref<ForexTradeResult | null>(null)
const swapError = ref<string | null>(null)
const swapLoading = ref(false)
const showConfirm = ref(false)

// ── Slippage tolerance ────────────────────────────────────────────────────────
const SLIPPAGE_PRESETS = [10, 50, 100, 500] // BPS
const slippageBps = ref<number>(50) // default 0.5%
const customSlippageInput = ref<number | null>(null)
const showCustomSlippage = ref(false)

function setSlippage(bps: number) {
  slippageBps.value = bps
  showCustomSlippage.value = false
  customSlippageInput.value = null
}

function applyCustomSlippage() {
  const v = customSlippageInput.value
  if (v && v > 0 && v <= 5000) {
    slippageBps.value = v
    showCustomSlippage.value = false
  }
}

// ── Quote countdown timer ─────────────────────────────────────────────────────
const quoteSecondsRemaining = ref<number>(0)
let countdownTimer: ReturnType<typeof setInterval> | null = null

function startCountdown(expiresInSeconds: number) {
  stopCountdown()
  quoteSecondsRemaining.value = expiresInSeconds
  countdownTimer = setInterval(() => {
    quoteSecondsRemaining.value = Math.max(0, quoteSecondsRemaining.value - 1)
    if (quoteSecondsRemaining.value === 0) {
      stopCountdown()
      // Auto-dismiss the quote panel and show a gentle expiry notice
      if (showConfirm.value) {
        showConfirm.value = false
        quote.value = null
        quoteError.value = t('forex.quoteExpiredNotice')
      }
    }
  }, 1000)
}

function stopCountdown() {
  if (countdownTimer !== null) {
    clearInterval(countdownTimer)
    countdownTimer = null
  }
}

onUnmounted(stopCountdown)

const countdownColor = computed(() => {
  if (quoteSecondsRemaining.value <= 5) return 'text-bad'
  if (quoteSecondsRemaining.value <= 10) return 'text-caution'
  return 'text-good'
})

// ── Error code translation ────────────────────────────────────────────────────
function translateFxError(raw: string): string {
  if (raw.includes('QUOTE_EXPIRED')) return t('forex.errorQuoteExpired')
  if (raw.includes('QUOTE_ALREADY_USED')) return t('forex.errorQuoteAlreadyUsed')
  if (raw.includes('SLIPPAGE_EXCEEDED')) return t('forex.errorSlippageExceeded')
  return raw
}

function findAccountById(id: string): PlayerBankAccountSummary | undefined {
  return props.contextScopedBankAccounts.find((a) => a.id === id)
}

function ensureScopedAccountSelections() {
  const accounts = props.contextScopedBankAccounts
  if (!accounts.some((account) => account.id === fromBankAccountId.value)) {
    const firstEur = accounts.find((account) => account.currencyCode === 'EUR')
    fromBankAccountId.value = firstEur?.id ?? accounts[0]?.id ?? ''
  }
  if (!accounts.some((account) => account.id === toBankAccountId.value) || toBankAccountId.value === fromBankAccountId.value) {
    const firstCzk = accounts.find((account) => account.currencyCode === 'CZK' && account.id !== fromBankAccountId.value)
    const firstDifferent = accounts.find((account) => account.id !== fromBankAccountId.value)
    toBankAccountId.value = firstCzk?.id ?? firstDifferent?.id ?? ''
  }
}

watch(() => props.contextScopedBankAccounts, () => { ensureScopedAccountSelections() })

watch(() => props.availableCurrencies, (currencies) => {
  if (props.initialToCurrency && currencies && currencies.includes(props.initialToCurrency)) {
    toCurrency.value = props.initialToCurrency
  }
}, { immediate: true })

const resolvedFromCurrency = computed(() => {
  if (props.hasBankAccounts && fromBankAccountId.value) return findAccountById(fromBankAccountId.value)?.currencyCode ?? fromCurrency.value
  return fromCurrency.value
})

const resolvedToCurrency = computed(() => {
  if (props.hasBankAccounts && toBankAccountId.value) return findAccountById(toBankAccountId.value)?.currencyCode ?? toCurrency.value
  return toCurrency.value
})

const fromBalance = computed(() => {
  if (props.hasBankAccounts && fromBankAccountId.value) return findAccountById(fromBankAccountId.value)?.balance ?? 0
  const b = props.balances.find((b) => b.currencyCode === fromCurrency.value)
  return b?.balance ?? 0
})

const fromSymbol = computed(() => {
  if (props.hasBankAccounts && fromBankAccountId.value) return findAccountById(fromBankAccountId.value)?.currencySymbol ?? resolvedFromCurrency.value
  if (fromCurrency.value === 'EUR') return '€'
  const r = props.rates.find((r) => r.quoteCurrencyCode === fromCurrency.value)
  return r?.quoteCurrencySymbol ?? fromCurrency.value
})

const toSymbol = computed(() => {
  if (props.hasBankAccounts && toBankAccountId.value) return findAccountById(toBankAccountId.value)?.currencySymbol ?? resolvedToCurrency.value
  if (toCurrency.value === 'EUR') return '€'
  const r = props.rates.find((r) => r.quoteCurrencyCode === toCurrency.value)
  return r?.quoteCurrencySymbol ?? toCurrency.value
})

const validationError = computed<string | null>(() => {
  if (props.hasBankAccounts) {
    if (!fromBankAccountId.value) return t('forex.selectSourceAccount')
    if (!toBankAccountId.value) return t('forex.selectDestAccount')
    if (fromBankAccountId.value === toBankAccountId.value) return t('forex.sameAccount')
    if (resolvedFromCurrency.value === resolvedToCurrency.value) return t('forex.sameCurrency')
    if (!amount.value || amount.value <= 0) return t('forex.invalidAmount')
    if (amount.value > fromBalance.value) return t('forex.insufficientFunds')
    return null
  }
  if (fromCurrency.value === toCurrency.value) return t('forex.sameCurrency')
  if (!amount.value || amount.value <= 0) return t('forex.invalidAmount')
  if (amount.value > fromBalance.value) return t('forex.insufficientFunds')
  return null
})

function formatAmount(val: number): string {
  return val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })
}

function formatSlippageBps(bps: number): string {
  return (bps / 100).toFixed(bps % 100 === 0 ? 1 : 2) + '%'
}

async function fetchQuote() {
  if (validationError.value) return
  quoteLoading.value = true; quoteError.value = null; quote.value = null; showConfirm.value = false; swapResult.value = null; swapError.value = null
  stopCountdown()
  try {
    const inputVars: Record<string, unknown> = { fromCurrencyCode: resolvedFromCurrency.value, toCurrencyCode: resolvedToCurrency.value, amount: amount.value }
    if (props.hasBankAccounts && fromBankAccountId.value) inputVars.fromBankAccountId = fromBankAccountId.value
    if (props.hasBankAccounts && toBankAccountId.value) inputVars.toBankAccountId = toBankAccountId.value
    const result = await gqlRequest<{ forexQuote: ForexQuote }>(`
      query ForexQuote($input: GetForexQuoteInput!) {
        forexQuote(input: $input) {
          fromCurrencyCode toCurrencyCode fromAmount toAmount feeAmount feePercent rate
          availableFromBalance fromCurrencySymbol toCurrencySymbol
          quoteNonce quotedAtUtc quoteExpiresInSeconds
        }
      }
    `, { input: inputVars })
    quote.value = result.forexQuote
    showConfirm.value = true
    startCountdown(result.forexQuote.quoteExpiresInSeconds ?? 30)
  } catch (e: unknown) {
    quoteError.value = e instanceof Error ? e.message : t('forex.swapFailed')
  } finally { quoteLoading.value = false }
}

async function executeSwap() {
  if (!quote.value) return
  swapLoading.value = true; swapError.value = null; swapResult.value = null
  stopCountdown()
  try {
    const inputVars: Record<string, unknown> = {
      fromCurrencyCode: resolvedFromCurrency.value,
      toCurrencyCode: resolvedToCurrency.value,
      amount: amount.value,
      quoteNonce: quote.value.quoteNonce,
      acceptedSlippageBps: slippageBps.value,
    }
    if (props.hasBankAccounts && fromBankAccountId.value) inputVars.fromBankAccountId = fromBankAccountId.value
    if (props.hasBankAccounts && toBankAccountId.value) inputVars.toBankAccountId = toBankAccountId.value
    const result = await gqlRequest<{ executeForexSwap: ForexTradeResult }>(`
      mutation ExecuteForexSwap($input: ExecuteForexSwapInput!) {
        executeForexSwap(input: $input) {
          tradeId fromCurrencyCode toCurrencyCode fromAmount toAmount feeAmount rate
          newFromBalance newToBalance fromCurrencySymbol toCurrencySymbol
        }
      }
    `, { input: inputVars })
    swapResult.value = result.executeForexSwap
    showConfirm.value = false; quote.value = null; amount.value = null
    emit('refresh')
  } catch (e: unknown) {
    const rawMsg = e instanceof Error ? e.message : t('forex.swapFailed')
    swapError.value = translateFxError(rawMsg)
    // If the quote expired or was already used, dismiss the stale quote panel.
    if (rawMsg.includes('QUOTE_EXPIRED') || rawMsg.includes('QUOTE_ALREADY_USED')) {
      showConfirm.value = false
      quote.value = null
    }
  } finally { swapLoading.value = false }
}

function cancelQuote() { stopCountdown(); showConfirm.value = false; quote.value = null }

function swapCurrencies() {
  if (props.hasBankAccounts) { const tmp = fromBankAccountId.value; fromBankAccountId.value = toBankAccountId.value; toBankAccountId.value = tmp }
  else { const tmp = fromCurrency.value; fromCurrency.value = toCurrency.value; toCurrency.value = tmp }
  quote.value = null; showConfirm.value = false; stopCountdown()
}
</script>

<template>
  <section class="space-y-6 rounded-2xl border border-divider bg-card p-6 shadow-sm sm:p-8" aria-label="Forex Swap">
    <div class="flex flex-wrap items-start justify-between gap-3 border-b border-divider pb-3">
      <h2 class="text-lg font-semibold text-body">{{ t('forex.tabSwap') }}</h2>
      <div v-if="selectedCity" class="swap-city-badge flex items-center gap-1.5 rounded-lg border border-divider bg-card-raised px-3 py-1.5 text-xs text-muted">
        <span class="font-bold text-brand">{{ baseCurrencySymbol }}</span>
        <span class="font-semibold text-body">{{ baseCurrencyCode }}</span>
        <span class="text-subtle">- {{ selectedCity.name }}</span>
      </div>
    </div>

    <div v-if="hasBankAccounts" class="ba-notice flex items-center gap-2 rounded-lg border border-divider bg-card-raised px-4 py-2.5 text-sm text-muted" role="note">
      <span class="text-lg">🏦</span>
      <span>{{ t('forex.bankAccountMode') }}</span>
      <RouterLink v-if="auth.player?.companies?.length" :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`" class="statement-link ml-1 text-xs font-semibold text-brand hover:underline">
        {{ t('forex.viewBankStatement') }} ->
      </RouterLink>
    </div>

    <div v-if="!hasBankAccounts" class="flex flex-col gap-3">
      <h3 class="text-sm font-semibold text-muted">{{ t('forex.balancesTitle') }}</h3>
      <RouterLink v-if="auth.player?.companies?.length" :to="`/bank-statement/${auth.player.companies[0]?.id ?? ''}`" class="statement-link inline-block text-xs font-semibold text-brand hover:underline">
        {{ t('forex.viewBankStatement') }} ->
      </RouterLink>
      <div v-if="balances.length === 0" class="text-sm italic text-muted">{{ t('forex.balancesEmpty') }}</div>
      <div v-else class="flex flex-wrap gap-3">
        <div v-for="b in balances" :key="b.currencyCode" class="balance-card flex items-center gap-1.5 rounded-lg border border-divider bg-card-raised px-3 py-2">
          <span class="font-semibold text-brand">{{ b.currencySymbol }}</span>
          <span class="text-sm font-semibold text-muted">{{ b.currencyCode }}</span>
          <span class="text-sm font-bold text-body">{{ formatAmount(b.balance) }}</span>
        </div>
      </div>
    </div>

    <div v-if="swapResult" class="swap-result-banner flex flex-col gap-1.5 rounded-lg border border-good bg-good/10 px-4 py-3 font-semibold text-good" role="status">
      <div class="flex items-center gap-2">
        <span>✓</span>
        <span>{{ t('forex.swapResultDetail', { fromAmount: formatAmount(swapResult.fromAmount), fromSymbol: swapResult.fromCurrencySymbol, toAmount: formatAmount(swapResult.toAmount), toSymbol: swapResult.toCurrencySymbol }) }}</span>
      </div>
      <div class="flex gap-3 text-xs font-normal text-muted">
        <span class="rounded border border-divider bg-card-raised px-2 py-0.5"> {{ swapResult.fromCurrencyCode }}: {{ formatAmount(swapResult.newFromBalance) }} </span>
        <span class="rounded border border-divider bg-card-raised px-2 py-0.5"> {{ swapResult.toCurrencyCode }}: {{ formatAmount(swapResult.newToBalance) }} </span>
      </div>
    </div>

    <div class="flex flex-col gap-4">
      <div class="grid grid-cols-[1fr_2fr] gap-4 items-start">
        <div class="flex flex-col gap-1">
          <template v-if="hasBankAccounts">
            <ForexBankAccountSelector v-model="fromBankAccountId" :accounts="contextScopedBankAccounts" :label="t('forex.sourceAccount')" id="from-bank-account" @update:model-value="() => { quote = null; showConfirm = false }" />
          </template>
          <template v-else>
            <BankAccountSelector v-model="fromCurrency" :balances="toBalances" :label="t('forex.sourceCurrency')" id="from-currency" @update:model-value="() => { quote = null; showConfirm = false }" />
          </template>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-xs font-semibold text-muted uppercase tracking-wide" for="swap-amount">{{ t('forex.amount') }}</label>
          <div class="flex items-center border border-divider rounded-lg overflow-hidden bg-page">
            <span class="px-3 py-2.5 bg-card-raised border-r border-divider font-bold text-brand min-w-10 text-center text-sm">{{ fromSymbol }}</span>
            <input id="swap-amount" v-model.number="amount" type="number" min="0" step="any" :placeholder="t('forex.amountPlaceholder')" class="flex-1 bg-transparent border-none px-3 py-2.5 text-body text-base font-semibold focus:outline-none" />
          </div>
          <span class="field-hint text-xs text-muted mt-0.5"> {{ t('forex.availableBalance') }}: {{ fromSymbol }}{{ formatAmount(fromBalance) }} </span>
        </div>
      </div>

      <div class="flex justify-center">
        <button class="w-9 h-9 rounded-full bg-card-raised border border-divider text-muted text-lg flex items-center justify-center transition-colors hover:bg-brand hover:text-white hover:border-brand" :title="'Swap currencies'" aria-label="Swap currencies" @click="swapCurrencies">⇅</button>
      </div>

      <div class="grid grid-cols-[1fr_2fr] gap-4 items-start">
        <div class="flex flex-col gap-1">
          <template v-if="hasBankAccounts">
            <ForexBankAccountSelector v-model="toBankAccountId" :accounts="contextScopedBankAccounts" :label="t('forex.destAccount')" id="to-bank-account" @update:model-value="() => { quote = null; showConfirm = false }" />
          </template>
          <template v-else>
            <BankAccountSelector v-model="toCurrency" :balances="toBalances" :label="t('forex.targetCurrency')" id="to-currency" @update:model-value="() => { quote = null; showConfirm = false }" />
          </template>
        </div>
        <div class="flex flex-col gap-1">
          <label class="text-xs font-semibold text-muted uppercase tracking-wide">{{ t('forex.youReceive') }}</label>
          <div class="flex items-center border border-divider rounded-lg overflow-hidden bg-page">
            <span class="px-3 py-2.5 bg-card-raised border-r border-divider font-bold text-brand min-w-10 text-center text-sm">{{ toSymbol }}</span>
            <div class="flex-1 px-3 py-2.5 text-base font-bold text-good">
              <span v-if="quote">{{ formatAmount(quote.toAmount) }}</span>
              <span v-else class="text-muted font-normal">-</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Slippage tolerance selector -->
      <div class="slippage-selector flex flex-col gap-1.5">
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold text-muted uppercase tracking-wide">{{ t('forex.slippageTolerance') }}</span>
          <span class="text-xs text-brand font-bold">{{ formatSlippageBps(slippageBps) }}</span>
          <span class="text-xs text-subtle italic">{{ t('forex.slippageHint') }}</span>
        </div>
        <div class="flex flex-wrap gap-2 items-center">
          <button
            v-for="preset in SLIPPAGE_PRESETS"
            :key="preset"
            class="slippage-preset rounded-full border px-3 py-0.5 text-xs font-semibold transition-colors"
            :class="slippageBps === preset && !showCustomSlippage ? 'border-brand bg-brand text-white' : 'border-divider bg-card-raised text-muted hover:border-brand hover:text-brand'"
            @click="setSlippage(preset)"
          >{{ formatSlippageBps(preset) }}</button>
          <button
            class="slippage-custom-btn rounded-full border px-3 py-0.5 text-xs font-semibold transition-colors"
            :class="showCustomSlippage ? 'border-brand bg-brand text-white' : 'border-divider bg-card-raised text-muted hover:border-brand hover:text-brand'"
            @click="showCustomSlippage = !showCustomSlippage"
          >{{ t('forex.slippageCustom') }}</button>
        </div>
        <div v-if="showCustomSlippage" class="flex items-center gap-2 mt-1">
          <input
            v-model.number="customSlippageInput"
            type="number"
            min="1"
            max="5000"
            :placeholder="t('forex.slippageCustomPlaceholder')"
            class="w-24 rounded-md border border-divider bg-page px-2 py-1 text-sm text-body focus:outline-none focus:border-brand"
          />
          <span class="text-xs text-muted">BPS (1 BPS = 0.01%)</span>
          <button class="btn btn-primary py-1 px-3 text-xs" @click="applyCustomSlippage">{{ t('common.apply') }}</button>
        </div>
      </div>

      <div v-if="validationError && amount" class="validation-error text-sm text-bad px-3 py-2 bg-bad/10 rounded-md" role="alert">{{ validationError }}</div>
      <div v-if="quoteError" class="text-sm text-bad px-3 py-2 bg-bad/10 rounded-md" role="alert">{{ quoteError }}</div>

      <div v-if="!showConfirm">
        <button class="btn btn-primary" :disabled="quoteLoading || !!validationError || !amount" @click="fetchQuote">
          {{ quoteLoading ? t('common.loading') : t('forex.getQuote') }}
        </button>
      </div>
    </div>

    <div v-if="showConfirm && quote" class="space-y-4 rounded-2xl border border-brand bg-card-raised p-6 sm:p-7" role="region" aria-label="Exchange Quote">
      <div class="flex items-center justify-between">
        <h3 class="text-base font-bold text-body">{{ t('forex.quoteTitle') }}</h3>
        <!-- Quote countdown timer -->
        <div class="quote-timer flex items-center gap-1.5 text-sm font-semibold" :class="countdownColor" aria-label="Quote expires in">
          <span>⏱</span>
          <span class="quote-countdown">{{ quoteSecondsRemaining }}s</span>
        </div>
      </div>
      <table class="quote-table w-full border-collapse text-sm">
        <tbody>
          <tr>
            <td class="py-1.5 text-muted w-2/5">{{ t('forex.rate') }}</td>
            <td class="py-1.5 font-semibold text-body text-right">1 {{ quote.fromCurrencyCode }} = {{ formatAmount(quote.rate) }} {{ quote.toCurrencyCode }}</td>
          </tr>
          <tr>
            <td class="py-1.5 text-muted">{{ t('forex.fee') }}</td>
            <td class="py-1.5 font-semibold text-caution text-right">{{ quote.fromCurrencySymbol }}{{ formatAmount(quote.feeAmount) }}</td>
          </tr>
          <tr>
            <td class="py-1.5 text-muted">{{ t('forex.youReceive') }}</td>
            <td class="py-1.5 text-right text-base font-bold text-good">{{ quote.toCurrencySymbol }}{{ formatAmount(quote.toAmount) }}</td>
          </tr>
          <tr>
            <td class="py-1.5 text-muted">{{ t('forex.slippageTolerance') }}</td>
            <td class="py-1.5 text-right text-sm font-semibold text-body">{{ formatSlippageBps(slippageBps) }}</td>
          </tr>
        </tbody>
      </table>
      <div v-if="swapError" class="rounded-md bg-bad/10 px-3 py-2 text-sm text-bad" role="alert">{{ swapError }}</div>
      <div class="flex justify-end gap-3">
        <button class="btn btn-secondary" :disabled="swapLoading" @click="cancelQuote">{{ t('forex.cancel') }}</button>
        <button class="btn btn-primary" :disabled="swapLoading || quoteSecondsRemaining === 0" @click="executeSwap">{{ swapLoading ? t('common.loading') : t('forex.confirmSwap') }}</button>
      </div>
    </div>
  </section>
</template>
