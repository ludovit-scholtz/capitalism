<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type {
  GoldAmmPool,
  GoldAmmSwapQuote,
  GoldAmmSwapResult,
  GoldAmmLiquidityResult,
  GoldAmmRemoveLiquidityResult,
  GoldBalanceInfo,
  CurrencyBalance,
} from '@/types'

const { t } = useI18n()

const props = defineProps<{
  availableCurrencies: string[]
  balances: CurrencyBalance[]
}>()

const emit = defineEmits<{
  (e: 'refresh'): void
}>()

// ── State ─────────────────────────────────────────────────────────────────────

const loading = ref(false)
const error = ref<string | null>(null)

const goldBalance = ref<GoldBalanceInfo | null>(null)
const pools = ref<GoldAmmPool[]>([])

// Swap
const swapDirection = ref<'FIAT_TO_GOLD' | 'GOLD_TO_FIAT'>('FIAT_TO_GOLD')
const swapCurrency = ref('EUR')
const swapAmount = ref<number | null>(null)
const swapMinOutput = ref<number>(0)
const swapQuote = ref<GoldAmmSwapQuote | null>(null)
const swapQuoteError = ref<string | null>(null)
const swapQuoteLoading = ref(false)
const swapShowConfirm = ref(false)
const swapResult = ref<GoldAmmSwapResult | null>(null)
const swapError = ref<string | null>(null)
const swapLoading = ref(false)

// Create pool
const createCurrency = ref('EUR')
const createFiatAmount = ref<number | null>(null)
const createGoldAmount = ref<number | null>(null)
const createLoading = ref(false)
const createError = ref<string | null>(null)
const createSuccess = ref<string | null>(null)

// Add liquidity
const addPoolId = ref<string | null>(null)
const addFiatAmount = ref<number | null>(null)
const addMaxGold = ref<number | null>(null)
const addLoading = ref(false)
const addError = ref<string | null>(null)
const addSuccess = ref<string | null>(null)

// Remove liquidity
const removePositionId = ref<string | null>(null)
const removeFraction = ref<number>(1.0)
const removeLoading = ref(false)
const removeError = ref<string | null>(null)
const removeSuccess = ref<string | null>(null)

// ── Computed ──────────────────────────────────────────────────────────────────

const swapInputBalance = computed(() => {
  if (swapDirection.value === 'FIAT_TO_GOLD') {
    const b = props.balances.find((b) => b.currencyCode === swapCurrency.value)
    return b?.balance ?? 0
  }
  return goldBalance.value?.availableBalance ?? 0
})

const swapValidationError = computed<string | null>(() => {
  if (!swapAmount.value || swapAmount.value <= 0) return t('goldAmm.insufficientFunds')
  if (swapAmount.value > swapInputBalance.value) {
    return swapDirection.value === 'FIAT_TO_GOLD'
      ? t('goldAmm.insufficientFunds')
      : t('goldAmm.insufficientGold')
  }
  return null
})

// ── Methods ───────────────────────────────────────────────────────────────────

async function loadData() {
  loading.value = true
  error.value = null
  try {
    const [poolsResult, balanceResult] = await Promise.all([
      gqlRequest<{ goldAmmPools: GoldAmmPool[] }>(`
        query {
          goldAmmPools {
            id currencyCode currencySymbol fiatReserve goldReserve
            totalLiquidityShares impliedGoldPrice
            myPosition {
              id poolId currencyCode liquidityShares sharePercent
              claimableFiat claimableGold fiatProvided goldProvided
            }
          }
        }
      `),
      gqlRequest<{ myGoldBalance: GoldBalanceInfo }>(`
        query { myGoldBalance { balance blockedInPools availableBalance } }
      `),
    ])
    pools.value = poolsResult.goldAmmPools ?? []
    goldBalance.value = balanceResult.myGoldBalance
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

async function fetchSwapQuote() {
  if (swapValidationError.value) return
  swapQuoteLoading.value = true
  swapQuoteError.value = null
  swapQuote.value = null
  swapShowConfirm.value = false

  try {
    const result = await gqlRequest<{ goldAmmSwapQuote: GoldAmmSwapQuote }>(
      `
      query Quote($input: GetGoldAmmSwapQuoteInput!) {
        goldAmmSwapQuote(input: $input) {
          direction currencyCode currencySymbol inputAmount outputAmount feeAmount feePercent
          impliedPrice slippagePercent poolFiatReserve poolGoldReserve availableInputBalance
        }
      }
    `,
      {
        input: {
          direction: swapDirection.value,
          currencyCode: swapCurrency.value,
          amount: swapAmount.value,
        },
      },
    )
    swapQuote.value = result.goldAmmSwapQuote
    swapShowConfirm.value = true
  } catch (e: unknown) {
    swapQuoteError.value = e instanceof Error ? e.message : t('goldAmm.swapFailed')
  } finally {
    swapQuoteLoading.value = false
  }
}

async function executeSwap() {
  if (!swapQuote.value) return
  swapLoading.value = true
  swapError.value = null

  try {
    const result = await gqlRequest<{ executeGoldAmmSwap: GoldAmmSwapResult }>(
      `
      mutation Swap($input: ExecuteGoldAmmSwapInput!) {
        executeGoldAmmSwap(input: $input) {
          tradeId direction currencyCode inputAmount outputAmount feeAmount
          impliedPrice newFiatBalance newGoldBalance
        }
      }
    `,
      {
        input: {
          direction: swapDirection.value,
          currencyCode: swapCurrency.value,
          amount: swapAmount.value,
          minOutputAmount: swapMinOutput.value ?? 0,
        },
      },
    )
    swapResult.value = result.executeGoldAmmSwap
    swapShowConfirm.value = false
    swapQuote.value = null
    swapAmount.value = null
    await loadData()
    emit('refresh')
  } catch (e: unknown) {
    swapError.value = e instanceof Error ? e.message : t('goldAmm.swapFailed')
  } finally {
    swapLoading.value = false
  }
}

function cancelSwapQuote() {
  swapShowConfirm.value = false
  swapQuote.value = null
}

async function createPool() {
  createLoading.value = true
  createError.value = null
  createSuccess.value = null

  try {
    await gqlRequest<{ createGoldAmmPool: GoldAmmLiquidityResult }>(
      `
      mutation CreatePool($input: CreateGoldAmmPoolInput!) {
        createGoldAmmPool(input: $input) {
          poolId positionId currencyCode fiatProvided goldProvided liquidityShares
        }
      }
    `,
      {
        input: {
          currencyCode: createCurrency.value,
          fiatAmount: createFiatAmount.value,
          goldAmount: createGoldAmount.value,
        },
      },
    )
    createSuccess.value = t('goldAmm.createPoolSuccess')
    createFiatAmount.value = null
    createGoldAmount.value = null
    await loadData()
    emit('refresh')
  } catch (e: unknown) {
    createError.value = e instanceof Error ? e.message : t('goldAmm.swapFailed')
  } finally {
    createLoading.value = false
  }
}

async function addLiquidity() {
  addLoading.value = true
  addError.value = null
  addSuccess.value = null

  try {
    await gqlRequest<{ addGoldAmmLiquidity: GoldAmmLiquidityResult }>(
      `
      mutation AddLiq($input: AddGoldAmmLiquidityInput!) {
        addGoldAmmLiquidity(input: $input) {
          poolId positionId fiatProvided goldProvided poolFiatReserve poolGoldReserve
        }
      }
    `,
      {
        input: {
          poolId: addPoolId.value,
          fiatAmount: addFiatAmount.value,
          maxGoldAmount: addMaxGold.value ?? 0,
        },
      },
    )
    addSuccess.value = t('goldAmm.addLiquiditySuccess')
    addFiatAmount.value = null
    addMaxGold.value = null
    await loadData()
    emit('refresh')
  } catch (e: unknown) {
    addError.value = e instanceof Error ? e.message : t('goldAmm.swapFailed')
  } finally {
    addLoading.value = false
  }
}

async function removeLiquidity(positionId: string) {
  removeLoading.value = true
  removeError.value = null
  removeSuccess.value = null
  removePositionId.value = positionId

  try {
    await gqlRequest<{ removeGoldAmmLiquidity: GoldAmmRemoveLiquidityResult }>(
      `
      mutation RemoveLiq($input: RemoveGoldAmmLiquidityInput!) {
        removeGoldAmmLiquidity(input: $input) {
          positionId fiatReturned goldReturned remainingShares
        }
      }
    `,
      {
        input: {
          positionId,
          shareFraction: removeFraction.value,
        },
      },
    )
    removeSuccess.value = t('goldAmm.removeLiquiditySuccess')
    await loadData()
    emit('refresh')
  } catch (e: unknown) {
    removeError.value = e instanceof Error ? e.message : t('goldAmm.swapFailed')
  } finally {
    removeLoading.value = false
    removePositionId.value = null
  }
}

function formatGold(val: number): string {
  return val.toLocaleString(undefined, { minimumFractionDigits: 4, maximumFractionDigits: 8 })
}

function formatFiat(val: number): string {
  return val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })
}

onMounted(loadData)
</script>

<template>
  <section class="gold-amm-section" aria-label="Gold AMM Exchange">
    <div class="gold-hero">
      <h2 class="gold-title">{{ t('goldAmm.title') }}</h2>
      <p class="gold-subtitle">{{ t('goldAmm.subtitle') }}</p>
    </div>

    <div v-if="loading" class="gold-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="gold-error">{{ error }}</div>

    <template v-else>
      <!-- Gold Balance Card -->
      <div v-if="goldBalance" class="gold-balance-card" aria-label="Gold Balance">
        <span class="gold-badge">{{ t('goldAmm.goldBadge') }}</span>
        <div class="gold-balance-row">
          <span class="gold-balance-label">{{ t('goldAmm.goldBalance') }}</span>
          <span class="gold-balance-value">{{ formatGold(goldBalance.balance) }} XAU</span>
        </div>
        <div class="gold-balance-row secondary">
          <span class="gold-balance-label">{{ t('goldAmm.availableGold') }}</span>
          <span class="gold-balance-value available">{{ formatGold(goldBalance.availableBalance) }} XAU</span>
        </div>
        <div v-if="goldBalance.blockedInPools > 0" class="gold-balance-row secondary">
          <span class="gold-balance-label">{{ t('goldAmm.blockedInPools') }}</span>
          <span class="gold-balance-value blocked">{{ formatGold(goldBalance.blockedInPools) }} XAU</span>
        </div>
      </div>

      <!-- ── Swap ─────────────────────────────────────────────────────────── -->
      <div class="gold-card" aria-label="Gold Swap">
        <h3 class="gold-card-title">{{ t('goldAmm.swapTitle') }}</h3>

        <!-- Success banner -->
        <div v-if="swapResult" class="gold-success-banner" role="status">
          ✓ {{ t('goldAmm.swapSuccess') }}
          <span v-if="swapResult.direction === 'FIAT_TO_GOLD'">
            +{{ formatGold(swapResult.outputAmount) }} XAU
          </span>
          <span v-else>
            +{{ formatFiat(swapResult.outputAmount) }} {{ swapResult.currencyCode }}
          </span>
        </div>

        <div class="form-row">
          <label class="form-label">{{ t('goldAmm.direction') }}</label>
          <select v-model="swapDirection" class="form-select">
            <option value="FIAT_TO_GOLD">{{ t('goldAmm.fiatToGold') }}</option>
            <option value="GOLD_TO_FIAT">{{ t('goldAmm.goldToFiat') }}</option>
          </select>
        </div>

        <div class="form-row">
          <label class="form-label">{{ t('goldAmm.currency') }}</label>
          <select v-model="swapCurrency" class="form-select">
            <option v-for="c in availableCurrencies" :key="c" :value="c">{{ c }}</option>
          </select>
        </div>

        <div class="form-row">
          <label class="form-label">
            {{ t('goldAmm.amount') }}
            <span class="balance-hint">
              ({{ t('goldAmm.availableGold') }}: {{ formatFiat(swapInputBalance) }}
              {{ swapDirection === 'FIAT_TO_GOLD' ? swapCurrency : 'XAU' }})
            </span>
          </label>
          <input
            v-model.number="swapAmount"
            type="number"
            min="0"
            step="any"
            class="form-input"
            :placeholder="t('goldAmm.amountPlaceholder')"
          />
        </div>

        <div v-if="swapQuoteError" class="gold-error-msg" role="alert">{{ swapQuoteError }}</div>

        <div v-if="!swapShowConfirm" class="form-actions">
          <button
            class="btn btn-primary"
            :disabled="swapQuoteLoading || !!swapValidationError || !swapAmount"
            @click="fetchSwapQuote"
          >
            {{ swapQuoteLoading ? t('common.loading') : t('goldAmm.getQuote') }}
          </button>
        </div>

        <!-- Quote confirmation -->
        <div v-if="swapShowConfirm && swapQuote" class="gold-quote-card" role="region" aria-label="Swap Quote">
          <h4 class="gold-quote-title">{{ t('goldAmm.quoteTitle') }}</h4>
          <table class="gold-quote-table">
            <tbody>
              <tr>
                <td class="quote-label">{{ t('goldAmm.inputAmount') }}</td>
                <td class="quote-value">
                  {{ swapQuote.direction === 'FIAT_TO_GOLD'
                    ? `${formatFiat(swapQuote.inputAmount)} ${swapQuote.currencyCode}`
                    : `${formatGold(swapQuote.inputAmount)} XAU` }}
                </td>
              </tr>
              <tr>
                <td class="quote-label">{{ t('goldAmm.outputAmount') }}</td>
                <td class="quote-value receive-value">
                  {{ swapQuote.direction === 'FIAT_TO_GOLD'
                    ? `${formatGold(swapQuote.outputAmount)} XAU`
                    : `${formatFiat(swapQuote.outputAmount)} ${swapQuote.currencyCode}` }}
                </td>
              </tr>
              <tr>
                <td class="quote-label">{{ t('goldAmm.fee') }}</td>
                <td class="quote-value fee-value">{{ formatGold(swapQuote.feeAmount) }}</td>
              </tr>
              <tr>
                <td class="quote-label">{{ t('goldAmm.impliedPrice') }}</td>
                <td class="quote-value">{{ formatFiat(swapQuote.impliedPrice) }} {{ swapQuote.currencyCode }}/XAU</td>
              </tr>
              <tr>
                <td class="quote-label">{{ t('goldAmm.slippage') }}</td>
                <td class="quote-value" :class="{ 'high-slippage': swapQuote.slippagePercent > 1 }">
                  {{ swapQuote.slippagePercent.toFixed(2) }}%
                </td>
              </tr>
            </tbody>
          </table>

          <div v-if="swapError" class="gold-error-msg" role="alert">{{ swapError }}</div>

          <div class="form-actions">
            <button class="btn btn-secondary" :disabled="swapLoading" @click="cancelSwapQuote">
              {{ t('goldAmm.cancel') }}
            </button>
            <button class="btn btn-primary" :disabled="swapLoading" @click="executeSwap">
              {{ swapLoading ? t('common.loading') : t('goldAmm.confirmSwap') }}
            </button>
          </div>
        </div>
      </div>

      <!-- ── Liquidity Pools ──────────────────────────────────────────────── -->
      <div class="gold-card" aria-label="Liquidity Pools">
        <h3 class="gold-card-title">{{ t('goldAmm.liquidityTitle') }}</h3>

        <div v-if="pools.length === 0" class="gold-empty">{{ t('goldAmm.noPools') }}</div>
        <div v-else class="pools-list">
          <div v-for="pool in pools" :key="pool.id" class="pool-card">
            <div class="pool-header">
              <span class="pool-pair">{{ pool.currencyCode }}/XAU</span>
              <span class="pool-price">
                {{ t('goldAmm.poolImpliedPrice') }}: {{ formatFiat(pool.impliedGoldPrice) }} {{ pool.currencyCode }}/XAU
              </span>
            </div>
            <div class="pool-reserves">
              <span>{{ pool.currencyCode }}: {{ formatFiat(pool.fiatReserve) }}</span>
              <span>XAU: {{ formatGold(pool.goldReserve) }}</span>
            </div>

            <!-- Player's position in this pool -->
            <div v-if="pool.myPosition" class="pool-position">
              <div class="position-badge">
                {{ t('goldAmm.positionShares') }}: {{ pool.myPosition.sharePercent.toFixed(2) }}%
              </div>
              <div class="position-claimable">
                {{ t('goldAmm.positionClaimable') }}:
                {{ formatFiat(pool.myPosition.claimableFiat) }} {{ pool.currencyCode }} +
                {{ formatGold(pool.myPosition.claimableGold) }} XAU
              </div>
              <div class="form-row compact">
                <label class="form-label">{{ t('goldAmm.removeFraction') }}</label>
                <input
                  v-model.number="removeFraction"
                  type="number"
                  min="0.01"
                  max="1"
                  step="0.01"
                  class="form-input compact-input"
                />
              </div>
              <div v-if="removeSuccess" class="gold-success-msg">{{ removeSuccess }}</div>
              <div v-if="removeError" class="gold-error-msg">{{ removeError }}</div>
              <button
                class="btn btn-danger"
                :disabled="removeLoading"
                @click="removeLiquidity(pool.myPosition.id)"
              >
                {{ removeLoading ? t('common.loading') : t('goldAmm.removeLiquidity') }}
              </button>
            </div>

            <!-- Add liquidity to this pool -->
            <details class="add-liq-details">
              <summary class="add-liq-summary">{{ t('goldAmm.addLiquidityTitle') }}</summary>
              <div class="add-liq-form">
                <div class="form-row compact">
                  <label class="form-label">{{ t('goldAmm.addLiquidityFiat') }} ({{ pool.currencyCode }})</label>
                  <input
                    v-model.number="addFiatAmount"
                    type="number"
                    min="0"
                    step="any"
                    class="form-input compact-input"
                    @focus="addPoolId = pool.id"
                  />
                </div>
                <div class="form-row compact">
                  <label class="form-label">{{ t('goldAmm.addLiquidityMaxGold') }}</label>
                  <input
                    v-model.number="addMaxGold"
                    type="number"
                    min="0"
                    step="any"
                    class="form-input compact-input"
                  />
                </div>
                <div v-if="addSuccess && addPoolId === pool.id" class="gold-success-msg">{{ addSuccess }}</div>
                <div v-if="addError && addPoolId === pool.id" class="gold-error-msg">{{ addError }}</div>
                <button
                  class="btn btn-primary"
                  :disabled="addLoading || !addFiatAmount"
                  @click="addPoolId = pool.id; addLiquidity()"
                >
                  {{ addLoading ? t('common.loading') : t('goldAmm.addLiquidity') }}
                </button>
              </div>
            </details>
          </div>
        </div>
      </div>

      <!-- ── Create New Pool ─────────────────────────────────────────────── -->
      <div class="gold-card" aria-label="Create Pool">
        <h3 class="gold-card-title">{{ t('goldAmm.createPoolTitle') }}</h3>
        <div class="form-row">
          <label class="form-label">{{ t('goldAmm.createPoolCurrency') }}</label>
          <select v-model="createCurrency" class="form-select">
            <option v-for="c in availableCurrencies" :key="c" :value="c">{{ c }}</option>
          </select>
        </div>
        <div class="form-row">
          <label class="form-label">{{ t('goldAmm.createPoolFiat') }} ({{ createCurrency }})</label>
          <input
            v-model.number="createFiatAmount"
            type="number"
            min="0"
            step="any"
            class="form-input"
            :placeholder="t('goldAmm.amountPlaceholder')"
          />
        </div>
        <div class="form-row">
          <label class="form-label">{{ t('goldAmm.createPoolGold') }}</label>
          <input
            v-model.number="createGoldAmount"
            type="number"
            min="0"
            step="any"
            class="form-input"
            :placeholder="t('goldAmm.amountPlaceholder')"
          />
        </div>
        <div v-if="createSuccess" class="gold-success-msg" role="status">{{ createSuccess }}</div>
        <div v-if="createError" class="gold-error-msg" role="alert">{{ createError }}</div>
        <div class="form-actions">
          <button
            class="btn btn-primary"
            :disabled="createLoading || !createFiatAmount || !createGoldAmount"
            @click="createPool"
          >
            {{ createLoading ? t('common.loading') : t('goldAmm.createPool') }}
          </button>
        </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.gold-amm-section {
  margin-top: 2rem;
}

.gold-hero {
  margin-bottom: 1.5rem;
}

.gold-title {
  font-size: 1.4rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 0.4rem;
}

.gold-subtitle {
  color: var(--color-text-muted);
  font-size: 0.95rem;
}

.gold-loading,
.gold-error {
  padding: 2rem;
  text-align: center;
  color: var(--color-text-muted);
}

.gold-balance-card {
  background: linear-gradient(135deg, #f5c842 0%, #e5a800 100%);
  border-radius: var(--radius-lg, 12px);
  padding: 1.25rem 1.5rem;
  margin-bottom: 1.5rem;
  color: #1a1200;
}

.gold-badge {
  display: inline-block;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  background: rgba(0, 0, 0, 0.15);
  border-radius: 4px;
  padding: 0.2rem 0.5rem;
  margin-bottom: 0.75rem;
}

.gold-balance-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.3rem;
}

.gold-balance-row.secondary {
  font-size: 0.9rem;
  opacity: 0.8;
}

.gold-balance-label {
  font-weight: 600;
}

.gold-balance-value {
  font-weight: 700;
  font-size: 1.1rem;
}

.gold-balance-value.available {
  color: #1a5c00;
}

.gold-balance-value.blocked {
  font-size: 0.95rem;
}

.gold-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg, 12px);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.gold-card-title {
  font-size: 1.05rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--color-border);
}

.form-row {
  margin-bottom: 1rem;
}

.form-row.compact {
  margin-bottom: 0.5rem;
}

.form-label {
  display: block;
  font-size: 0.9rem;
  font-weight: 500;
  color: var(--color-text-muted);
  margin-bottom: 0.4rem;
}

.balance-hint {
  font-size: 0.8rem;
  font-weight: 400;
  opacity: 0.7;
}

.form-input,
.form-select {
  width: 100%;
  padding: 0.6rem 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm, 6px);
  background: var(--color-background);
  color: var(--color-text-primary);
  font-size: 0.95rem;
}

.form-input.compact-input {
  width: auto;
  max-width: 160px;
}

.form-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 1rem;
}

.gold-success-banner {
  background: var(--color-success-bg, #dcfce7);
  color: var(--color-success, #166534);
  border: 1px solid var(--color-success, #16a34a);
  border-radius: var(--radius-sm, 6px);
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
  font-weight: 600;
}

.gold-success-msg {
  color: var(--color-success, #16a34a);
  font-size: 0.9rem;
  margin: 0.5rem 0;
}

.gold-error-msg {
  color: var(--color-error, #dc2626);
  font-size: 0.9rem;
  margin: 0.5rem 0;
}

.gold-quote-card {
  background: var(--color-surface-raised, var(--color-surface));
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm, 6px);
  padding: 1rem;
  margin-top: 1rem;
}

.gold-quote-title {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 0.75rem;
}

.gold-quote-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.gold-quote-table td {
  padding: 0.4rem 0.5rem;
}

.quote-label {
  color: var(--color-text-muted);
  width: 45%;
}

.quote-value {
  font-weight: 600;
  color: var(--color-text-primary);
}

.receive-value {
  color: var(--color-success, #16a34a);
}

.fee-value {
  color: var(--color-warning, #d97706);
}

.high-slippage {
  color: var(--color-error, #dc2626);
}

.gold-empty {
  color: var(--color-text-muted);
  font-style: italic;
  padding: 1rem 0;
}

.pools-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.pool-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm, 6px);
  padding: 1rem;
}

.pool-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.pool-pair {
  font-size: 1rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.pool-price {
  font-size: 0.85rem;
  color: var(--color-text-muted);
}

.pool-reserves {
  display: flex;
  gap: 1.5rem;
  font-size: 0.9rem;
  color: var(--color-text-muted);
  margin-bottom: 0.75rem;
}

.pool-position {
  background: var(--color-surface-raised, rgba(245, 200, 66, 0.08));
  border: 1px solid #f5c842;
  border-radius: var(--radius-sm, 6px);
  padding: 0.75rem;
  margin-bottom: 0.75rem;
}

.position-badge {
  font-size: 0.8rem;
  font-weight: 700;
  text-transform: uppercase;
  color: #b45309;
  margin-bottom: 0.4rem;
}

.position-claimable {
  font-size: 0.9rem;
  color: var(--color-text-primary);
  margin-bottom: 0.75rem;
}

.add-liq-details {
  margin-top: 0.5rem;
}

.add-liq-summary {
  font-size: 0.9rem;
  color: var(--color-primary, #3b82f6);
  cursor: pointer;
  user-select: none;
  margin-bottom: 0.5rem;
}

.add-liq-form {
  padding-top: 0.75rem;
}

.btn {
  padding: 0.55rem 1.25rem;
  border: none;
  border-radius: var(--radius-sm, 6px);
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.15s;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: var(--color-primary, #3b82f6);
  color: #fff;
}

.btn-secondary {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
}

.btn-danger {
  background: var(--color-error, #dc2626);
  color: #fff;
  font-size: 0.85rem;
  padding: 0.4rem 0.9rem;
}
</style>
