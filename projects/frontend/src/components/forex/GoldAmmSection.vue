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
  <section class="mt-8" aria-label="Gold AMM Exchange">
    <!-- Hero -->
    <div class="mb-6">
      <h2 class="text-2xl font-bold text-body mb-1">{{ t('goldAmm.title') }}</h2>
      <p class="text-muted text-sm">{{ t('goldAmm.subtitle') }}</p>
    </div>

    <div v-if="loading" class="text-center py-8 text-muted">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="text-center py-8 text-bad">{{ error }}</div>

    <template v-else>
      <!-- Gold Balance Card -->
      <div
        v-if="goldBalance"
        class="rounded-xl p-5 mb-6 text-[#1a1200]"
        style="background: linear-gradient(135deg, #f5c842 0%, #e5a800 100%)"
        aria-label="Gold Balance"
      >
        <span class="inline-block text-xs font-bold uppercase tracking-wide bg-black/15 rounded px-2 py-0.5 mb-3">
          {{ t('goldAmm.goldBadge') }}
        </span>
        <div class="flex justify-between items-center mb-1">
          <span class="font-semibold">{{ t('goldAmm.goldBalance') }}</span>
          <span class="font-bold text-lg">{{ formatGold(goldBalance.balance) }} XAU</span>
        </div>
        <div class="flex justify-between items-center opacity-80 text-sm mb-1">
          <span class="font-semibold">{{ t('goldAmm.availableGold') }}</span>
          <span class="font-bold text-[#1a5c00]">{{ formatGold(goldBalance.availableBalance) }} XAU</span>
        </div>
        <div v-if="goldBalance.blockedInPools > 0" class="flex justify-between items-center opacity-80 text-sm">
          <span class="font-semibold">{{ t('goldAmm.blockedInPools') }}</span>
          <span class="font-bold">{{ formatGold(goldBalance.blockedInPools) }} XAU</span>
        </div>
      </div>

      <!-- ── Swap ─────────────────────────────────────────────────────────── -->
      <div class="bg-card border border-divider rounded-xl p-6 mb-6" aria-label="Gold Swap">
        <h3 class="text-base font-semibold text-body mb-4 pb-3 border-b border-divider">
          {{ t('goldAmm.swapTitle') }}
        </h3>

        <!-- Success banner -->
        <div v-if="swapResult" class="bg-good/10 border border-good text-good font-semibold rounded-lg px-4 py-3 mb-4" role="status">
          ✓ {{ t('goldAmm.swapSuccess') }}
          <span v-if="swapResult.direction === 'FIAT_TO_GOLD'">
            +{{ formatGold(swapResult.outputAmount) }} XAU
          </span>
          <span v-else>
            +{{ formatFiat(swapResult.outputAmount) }} {{ swapResult.currencyCode }}
          </span>
        </div>

        <div class="flex flex-col gap-1.5 mb-4">
          <label class="text-sm font-medium text-muted">{{ t('goldAmm.direction') }}</label>
          <select v-model="swapDirection" class="form-select">
            <option value="FIAT_TO_GOLD">{{ t('goldAmm.fiatToGold') }}</option>
            <option value="GOLD_TO_FIAT">{{ t('goldAmm.goldToFiat') }}</option>
          </select>
        </div>

        <div class="flex flex-col gap-1.5 mb-4">
          <label class="text-sm font-medium text-muted">{{ t('goldAmm.currency') }}</label>
          <select v-model="swapCurrency" class="form-select">
            <option v-for="c in availableCurrencies" :key="c" :value="c">{{ c }}</option>
          </select>
        </div>

        <div class="flex flex-col gap-1.5 mb-4">
          <label class="text-sm font-medium text-muted">
            {{ t('goldAmm.amount') }}
            <span class="text-xs font-normal opacity-70 ml-1">
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

        <div v-if="swapQuoteError" class="text-sm text-bad px-3 py-2 bg-bad/10 rounded-md mb-3" role="alert">
          {{ swapQuoteError }}
        </div>

        <div v-if="!swapShowConfirm" class="flex gap-3 mt-2">
          <button
            class="btn btn-primary"
            :disabled="swapQuoteLoading || !!swapValidationError || !swapAmount"
            @click="fetchSwapQuote"
          >
            {{ swapQuoteLoading ? t('common.loading') : t('goldAmm.getQuote') }}
          </button>
        </div>

        <!-- Quote confirmation -->
        <div
          v-if="swapShowConfirm && swapQuote"
          class="bg-card-raised border border-divider rounded-lg p-4 mt-4"
          role="region"
          aria-label="Swap Quote"
        >
          <h4 class="text-sm font-semibold text-body mb-3">{{ t('goldAmm.quoteTitle') }}</h4>
          <table class="w-full border-collapse text-sm mb-4">
            <tbody>
              <tr>
                <td class="py-1.5 text-muted w-[45%]">{{ t('goldAmm.inputAmount') }}</td>
                <td class="py-1.5 font-semibold text-body">
                  {{ swapQuote.direction === 'FIAT_TO_GOLD'
                    ? `${formatFiat(swapQuote.inputAmount)} ${swapQuote.currencyCode}`
                    : `${formatGold(swapQuote.inputAmount)} XAU` }}
                </td>
              </tr>
              <tr>
                <td class="py-1.5 text-muted">{{ t('goldAmm.outputAmount') }}</td>
                <td class="py-1.5 font-semibold text-good">
                  {{ swapQuote.direction === 'FIAT_TO_GOLD'
                    ? `${formatGold(swapQuote.outputAmount)} XAU`
                    : `${formatFiat(swapQuote.outputAmount)} ${swapQuote.currencyCode}` }}
                </td>
              </tr>
              <tr>
                <td class="py-1.5 text-muted">{{ t('goldAmm.fee') }}</td>
                <td class="py-1.5 font-semibold text-caution">{{ formatGold(swapQuote.feeAmount) }}</td>
              </tr>
              <tr>
                <td class="py-1.5 text-muted">{{ t('goldAmm.impliedPrice') }}</td>
                <td class="py-1.5 font-semibold text-body">{{ formatFiat(swapQuote.impliedPrice) }} {{ swapQuote.currencyCode }}/XAU</td>
              </tr>
              <tr>
                <td class="py-1.5 text-muted">{{ t('goldAmm.slippage') }}</td>
                <td
                  class="py-1.5 font-semibold"
                  :class="swapQuote.slippagePercent > 1 ? 'text-bad' : 'text-body'"
                >
                  {{ swapQuote.slippagePercent.toFixed(2) }}%
                </td>
              </tr>
            </tbody>
          </table>

          <div v-if="swapError" class="text-sm text-bad px-3 py-2 bg-bad/10 rounded-md mb-3" role="alert">
            {{ swapError }}
          </div>

          <div class="flex gap-3 justify-end">
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
      <div class="bg-card border border-divider rounded-xl p-6 mb-6" aria-label="Liquidity Pools">
        <h3 class="text-base font-semibold text-body mb-4 pb-3 border-b border-divider">
          {{ t('goldAmm.liquidityTitle') }}
        </h3>

        <div v-if="pools.length === 0" class="text-sm text-muted italic py-4">
          {{ t('goldAmm.noPools') }}
        </div>
        <div v-else class="flex flex-col gap-4">
          <div
            v-for="pool in pools"
            :key="pool.id"
            class="border border-divider rounded-lg p-4"
          >
            <div class="flex justify-between items-center mb-2">
              <span class="text-base font-bold text-body">{{ pool.currencyCode }}/XAU</span>
              <span class="text-xs text-muted">
                {{ t('goldAmm.poolImpliedPrice') }}: {{ formatFiat(pool.impliedGoldPrice) }} {{ pool.currencyCode }}/XAU
              </span>
            </div>
            <div class="flex gap-6 text-sm text-muted mb-3">
              <span>{{ pool.currencyCode }}: {{ formatFiat(pool.fiatReserve) }}</span>
              <span>XAU: {{ formatGold(pool.goldReserve) }}</span>
            </div>

            <!-- Player's position -->
            <div
              v-if="pool.myPosition"
              class="bg-[rgba(245,200,66,0.08)] border border-[#f5c842] rounded-lg p-3 mb-3"
            >
              <div class="text-xs font-bold uppercase text-[#b45309] mb-1">
                {{ t('goldAmm.positionShares') }}: {{ pool.myPosition.sharePercent.toFixed(2) }}%
              </div>
              <div class="text-sm text-body mb-3">
                {{ t('goldAmm.positionClaimable') }}:
                {{ formatFiat(pool.myPosition.claimableFiat) }} {{ pool.currencyCode }} +
                {{ formatGold(pool.myPosition.claimableGold) }} XAU
              </div>
              <div class="flex flex-col gap-1.5 mb-2">
                <label class="text-sm font-medium text-muted">{{ t('goldAmm.removeFraction') }}</label>
                <input
                  v-model.number="removeFraction"
                  type="number"
                  min="0.01"
                  max="1"
                  step="0.01"
                  class="form-input max-w-[160px]"
                />
              </div>
              <div v-if="removeSuccess" class="text-sm text-good my-2">{{ removeSuccess }}</div>
              <div v-if="removeError" class="text-sm text-bad my-2">{{ removeError }}</div>
              <button
                class="btn btn-danger"
                :disabled="removeLoading"
                @click="removeLiquidity(pool.myPosition.id)"
              >
                {{ removeLoading ? t('common.loading') : t('goldAmm.removeLiquidity') }}
              </button>
            </div>

            <!-- Add liquidity -->
            <details class="mt-2">
              <summary class="text-sm text-brand cursor-pointer select-none mb-2">
                {{ t('goldAmm.addLiquidityTitle') }}
              </summary>
              <div class="pt-3 flex flex-col gap-3">
                <div class="flex flex-col gap-1.5">
                  <label class="text-sm font-medium text-muted">
                    {{ t('goldAmm.addLiquidityFiat') }} ({{ pool.currencyCode }})
                  </label>
                  <input
                    v-model.number="addFiatAmount"
                    type="number"
                    min="0"
                    step="any"
                    class="form-input max-w-[160px]"
                    @focus="addPoolId = pool.id"
                  />
                </div>
                <div class="flex flex-col gap-1.5">
                  <label class="text-sm font-medium text-muted">{{ t('goldAmm.addLiquidityMaxGold') }}</label>
                  <input
                    v-model.number="addMaxGold"
                    type="number"
                    min="0"
                    step="any"
                    class="form-input max-w-[160px]"
                  />
                </div>
                <div v-if="addSuccess && addPoolId === pool.id" class="text-sm text-good">{{ addSuccess }}</div>
                <div v-if="addError && addPoolId === pool.id" class="text-sm text-bad">{{ addError }}</div>
                <button
                  class="btn btn-primary self-start"
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
      <div class="bg-card border border-divider rounded-xl p-6 mb-6" aria-label="Create Pool">
        <h3 class="text-base font-semibold text-body mb-4 pb-3 border-b border-divider">
          {{ t('goldAmm.createPoolTitle') }}
        </h3>
        <div class="flex flex-col gap-1.5 mb-4">
          <label class="text-sm font-medium text-muted">{{ t('goldAmm.createPoolCurrency') }}</label>
          <select v-model="createCurrency" class="form-select">
            <option v-for="c in availableCurrencies" :key="c" :value="c">{{ c }}</option>
          </select>
        </div>
        <div class="flex flex-col gap-1.5 mb-4">
          <label class="text-sm font-medium text-muted">
            {{ t('goldAmm.createPoolFiat') }} ({{ createCurrency }})
          </label>
          <input
            v-model.number="createFiatAmount"
            type="number"
            min="0"
            step="any"
            class="form-input"
            :placeholder="t('goldAmm.amountPlaceholder')"
          />
        </div>
        <div class="flex flex-col gap-1.5 mb-4">
          <label class="text-sm font-medium text-muted">{{ t('goldAmm.createPoolGold') }}</label>
          <input
            v-model.number="createGoldAmount"
            type="number"
            min="0"
            step="any"
            class="form-input"
            :placeholder="t('goldAmm.amountPlaceholder')"
          />
        </div>
        <div v-if="createSuccess" class="text-sm text-good mb-3" role="status">{{ createSuccess }}</div>
        <div v-if="createError" class="text-sm text-bad mb-3" role="alert">{{ createError }}</div>
        <div class="flex gap-3 mt-2">
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
