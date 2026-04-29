<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import type { GoldAmmPool, GoldAmmSwapQuote, GoldAmmSwapResult, GoldAmmLiquidityResult, GoldAmmRemoveLiquidityResult, GoldBalanceInfo, CurrencyBalance } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  availableCurrencies: string[]
  balances: CurrencyBalance[]
}>()

const emit = defineEmits<{
  (e: 'refresh'): void
}>()

// ÔöÇÔöÇ State ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

type AmmTab = 'swap' | 'positions' | 'addLiquidity'

const loading = ref(false)
const error = ref<string | null>(null)

const activeTab = ref<AmmTab>('swap')
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

// Add liquidity
const addPoolId = ref<string | null>(null)
const addFiatAmount = ref<number | null>(null)
const addMaxGold = ref<number | null>(null)
const addLoading = ref(false)
const addError = ref<string | null>(null)
const addSuccess = ref<string | null>(null)

// Create pool
const createCurrency = ref('EUR')
const createFiatAmount = ref<number | null>(null)
const createGoldAmount = ref<number | null>(null)
const createLoading = ref(false)
const createError = ref<string | null>(null)
const createSuccess = ref<string | null>(null)

// Remove liquidity
const removePositionId = ref<string | null>(null)
const removeFraction = ref<number>(1.0)
const removeLoading = ref(false)
const removeError = ref<string | null>(null)
const removeSuccess = ref<string | null>(null)

// ÔöÇÔöÇ Computed ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

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
    return swapDirection.value === 'FIAT_TO_GOLD' ? t('goldAmm.insufficientFunds') : t('goldAmm.insufficientGold')
  }
  return null
})

/** Pools with active player positions */
const myPositions = computed(() => pools.value.filter((p) => p.myPosition != null))

/** Pool selected for adding liquidity */
const selectedAddPool = computed(() => pools.value.find((p) => p.id === addPoolId.value) ?? null)

/** Whether the addLiquidity tab should show create-pool form */
const showCreateForm = computed(() => pools.value.length < props.availableCurrencies.length)

// ÔöÇÔöÇ Methods ÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇÔöÇ

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

    // Default swap/create currency to first available
    if (!swapCurrency.value && props.availableCurrencies.length > 0) {
      swapCurrency.value = props.availableCurrencies[0] ?? 'EUR'
    }
    if (!createCurrency.value && props.availableCurrencies.length > 0) {
      createCurrency.value = props.availableCurrencies[0] ?? 'EUR'
    }
    // Default add pool to first existing pool
    if (!addPoolId.value && pools.value.length > 0) {
      addPoolId.value = pools.value[0]?.id ?? null
    }
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
  <section class="mt-2" aria-label="Gold AMM Exchange">
    <!-- Hero -->
    <div class="mb-5">
      <h2 class="text-2xl font-bold text-body mb-1">{{ t('goldAmm.title') }}</h2>
      <p class="text-muted text-sm">{{ t('goldAmm.subtitle') }}</p>
    </div>

    <div v-if="loading" class="text-center py-8 text-muted">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="text-center py-8 text-bad">{{ error }}</div>

    <template v-else>
      <!-- Gold Balance Card (always visible) -->
      <div v-if="goldBalance" class="rounded-xl p-5 mb-5 text-[#1a1200]" style="background: linear-gradient(135deg, #f5c842 0%, #e5a800 100%)" aria-label="Gold Balance">
        <span class="inline-block text-xs font-bold uppercase tracking-wide bg-black/15 rounded px-2 py-0.5 mb-3">
          {{ t('goldAmm.goldBadge') }}
        </span>
        <div class="flex flex-wrap gap-x-8 gap-y-1">
          <div class="flex flex-col">
            <span class="text-xs font-semibold opacity-70">{{ t('goldAmm.goldBalance') }}</span>
            <span class="font-bold text-lg">{{ formatGold(goldBalance.balance) }} XAU</span>
          </div>
          <div class="flex flex-col">
            <span class="text-xs font-semibold opacity-70">{{ t('goldAmm.availableGold') }}</span>
            <span class="font-bold text-[#1a5c00]">{{ formatGold(goldBalance.availableBalance) }} XAU</span>
          </div>
          <div v-if="goldBalance.blockedInPools > 0" class="flex flex-col">
            <span class="text-xs font-semibold opacity-70">{{ t('goldAmm.blockedInPools') }}</span>
            <span class="font-bold">{{ formatGold(goldBalance.blockedInPools) }} XAU</span>
          </div>
        </div>
        <div v-if="goldBalance.blockedInPools > 0" class="mt-3 text-xs font-semibold bg-black/15 rounded px-2 py-1.5">├ö├ť├í┬┤┼×─ć {{ t('goldAmm.blockedGoldWarning') }}</div>
      </div>

      <!-- Inner Tab Bar -->
      <div class="flex flex-wrap gap-2 mb-5" role="tablist" :aria-label="t('goldAmm.title')">
        <button
          role="tab"
          :aria-selected="activeTab === 'swap'"
          class="amm-tab border rounded-full px-4 py-1.5 text-sm font-semibold cursor-pointer transition-colors"
          :class="activeTab === 'swap' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
          @click="activeTab = 'swap'"
        >
          {{ t('goldAmm.tabSwap') }}
        </button>
        <button
          role="tab"
          :aria-selected="activeTab === 'positions'"
          class="amm-tab border rounded-full px-4 py-1.5 text-sm font-semibold cursor-pointer transition-colors"
          :class="activeTab === 'positions' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
          @click="activeTab = 'positions'"
        >
          {{ t('goldAmm.tabPositions') }}
          <span v-if="myPositions.length > 0" class="ml-1 inline-flex items-center justify-center w-4 h-4 text-xs rounded-full bg-good text-white">{{ myPositions.length }}</span>
        </button>
        <button
          role="tab"
          :aria-selected="activeTab === 'addLiquidity'"
          class="amm-tab border rounded-full px-4 py-1.5 text-sm font-semibold cursor-pointer transition-colors"
          :class="activeTab === 'addLiquidity' ? 'bg-brand border-brand text-white' : 'bg-card border-divider text-muted hover:bg-card-raised hover:text-body'"
          @click="activeTab = 'addLiquidity'"
        >
          {{ t('goldAmm.tabAddLiquidity') }}
        </button>
      </div>

      <!-- ├ö├Â├ç├ö├Â├ç Swap Tab ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->
      <div v-if="activeTab === 'swap'" class="bg-card border border-divider rounded-xl p-6" aria-label="Gold Swap">
        <h3 class="text-base font-semibold text-body mb-1 pb-3 border-b border-divider">
          {{ t('goldAmm.swapTitle') }}
        </h3>
        <p class="text-xs text-muted mb-4">{{ t('goldAmm.swapHint') }}</p>

        <div v-if="swapResult" class="bg-good/10 border border-good text-good font-semibold rounded-lg px-4 py-3 mb-4" role="status">
          ├ö┼ą├┤ {{ t('goldAmm.swapSuccess') }}
          <span v-if="swapResult.direction === 'FIAT_TO_GOLD'"> +{{ formatGold(swapResult.outputAmount) }} XAU</span>
          <span v-else> +{{ formatFiat(swapResult.outputAmount) }} {{ swapResult.currencyCode }}</span>
        </div>

        <div class="flex flex-col gap-4">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium text-muted">{{ t('goldAmm.direction') }}</label>
              <select v-model="swapDirection" class="form-select">
                <option value="FIAT_TO_GOLD">{{ t('goldAmm.fiatToGold') }}</option>
                <option value="GOLD_TO_FIAT">{{ t('goldAmm.goldToFiat') }}</option>
              </select>
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium text-muted">{{ t('goldAmm.currency') }}</label>
              <select v-model="swapCurrency" class="form-select">
                <option v-for="c in availableCurrencies" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>
          </div>

          <div class="flex flex-col gap-1.5">
            <label class="text-sm font-medium text-muted">
              {{ t('goldAmm.amount') }}
              <span class="text-xs font-normal opacity-70 ml-1">
                ({{ t('goldAmm.availableGold') }}: {{ formatFiat(swapInputBalance) }} {{ swapDirection === 'FIAT_TO_GOLD' ? swapCurrency : 'XAU' }})
              </span>
            </label>
            <input v-model.number="swapAmount" type="number" min="0" step="any" class="form-input" :placeholder="t('goldAmm.amountPlaceholder')" />
          </div>

          <div v-if="swapQuoteError" class="text-sm text-bad px-3 py-2 bg-bad/10 rounded-md" role="alert">
            {{ swapQuoteError }}
          </div>

          <div v-if="!swapShowConfirm">
            <button class="btn btn-primary" :disabled="swapQuoteLoading || !!swapValidationError || !swapAmount" @click="fetchSwapQuote">
              {{ swapQuoteLoading ? t('common.loading') : t('goldAmm.getQuote') }}
            </button>
          </div>
        </div>

        <!-- Quote confirmation -->
        <div v-if="swapShowConfirm && swapQuote" class="bg-card-raised border border-divider rounded-lg p-4 mt-5" role="region" aria-label="Swap Quote">
          <h4 class="text-sm font-semibold text-body mb-3">{{ t('goldAmm.quoteTitle') }}</h4>
          <table class="w-full border-collapse text-sm mb-4">
            <tbody>
              <tr>
                <td class="py-1.5 text-muted w-[45%]">{{ t('goldAmm.inputAmount') }}</td>
                <td class="py-1.5 font-semibold text-body">
                  {{ swapQuote.direction === 'FIAT_TO_GOLD' ? `${formatFiat(swapQuote.inputAmount)} ${swapQuote.currencyCode}` : `${formatGold(swapQuote.inputAmount)} XAU` }}
                </td>
              </tr>
              <tr>
                <td class="py-1.5 text-muted">{{ t('goldAmm.outputAmount') }}</td>
                <td class="py-1.5 font-semibold text-good">
                  {{ swapQuote.direction === 'FIAT_TO_GOLD' ? `${formatGold(swapQuote.outputAmount)} XAU` : `${formatFiat(swapQuote.outputAmount)} ${swapQuote.currencyCode}` }}
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
                <td class="py-1.5 font-semibold" :class="swapQuote.slippagePercent > 1 ? 'text-bad' : 'text-body'">{{ swapQuote.slippagePercent.toFixed(2) }}%</td>
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

      <!-- ├ö├Â├ç├ö├Â├ç My Positions Tab ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->
      <div v-else-if="activeTab === 'positions'" class="bg-card border border-divider rounded-xl p-6" aria-label="My Liquidity Positions">
        <h3 class="text-base font-semibold text-body mb-1 pb-3 border-b border-divider">
          {{ t('goldAmm.myPositionsTitle') }}
        </h3>
        <p class="text-xs text-muted mb-4">{{ t('goldAmm.feeAccrual') }}</p>

        <div v-if="myPositions.length === 0" class="py-8 text-center">
          <p class="text-sm text-muted italic mb-3">{{ t('goldAmm.noPositions') }}</p>
          <button class="btn btn-primary" @click="activeTab = 'addLiquidity'">{{ t('goldAmm.tabAddLiquidity') }} ├ö─ç─║</button>
        </div>

        <div v-else class="flex flex-col gap-5">
          <div v-for="pool in myPositions" :key="pool.id" class="border border-divider rounded-lg p-4">
            <div class="flex justify-between items-center mb-3">
              <span class="text-base font-bold text-body">{{ pool.currencyCode }}/XAU</span>
              <span class="text-xs bg-brand/10 text-brand rounded-full px-2.5 py-0.5 font-semibold"> {{ (pool.myPosition?.sharePercent ?? 0).toFixed(2) }}% {{ t('goldAmm.positionShares') }} </span>
            </div>

            <div class="grid grid-cols-2 gap-3 text-sm mb-4">
              <div>
                <span class="text-xs text-muted uppercase">{{ pool.currencyCode }} {{ t('goldAmm.poolFiatReserve') }}</span>
                <div class="font-semibold">{{ formatFiat(pool.fiatReserve) }}</div>
              </div>
              <div>
                <span class="text-xs text-muted uppercase">{{ t('goldAmm.poolGoldReserve') }}</span>
                <div class="font-semibold">{{ formatGold(pool.goldReserve) }} XAU</div>
              </div>
              <div>
                <span class="text-xs text-muted uppercase">{{ t('goldAmm.impliedPrice') }}</span>
                <div class="font-semibold">{{ formatFiat(pool.impliedGoldPrice) }} {{ pool.currencyCode }}/XAU</div>
              </div>
            </div>

            <div class="bg-[rgba(245,200,66,0.08)] border border-[#f5c842] rounded-lg p-3 mb-4">
              <div class="text-xs font-bold uppercase text-[#b45309] mb-2">{{ t('goldAmm.positionClaimable') }}</div>
              <div class="flex gap-6 text-sm">
                <div>
                  <span class="text-muted">{{ pool.currencyCode }}:</span>
                  <span class="font-semibold ml-1">{{ formatFiat(pool.myPosition?.claimableFiat ?? 0) }}</span>
                </div>
                <div>
                  <span class="text-muted">XAU:</span>
                  <span class="font-semibold ml-1">{{ formatGold(pool.myPosition?.claimableGold ?? 0) }}</span>
                </div>
              </div>
              <div class="mt-1 text-xs text-muted">{{ t('goldAmm.claimableHint') }}</div>
            </div>

            <div class="flex flex-wrap items-end gap-3">
              <div class="flex flex-col gap-1">
                <label class="text-xs font-medium text-muted">{{ t('goldAmm.removeFraction') }}</label>
                <input v-model.number="removeFraction" type="number" min="0.01" max="1" step="0.01" class="form-input w-28" />
              </div>
              <button class="btn btn-danger" :disabled="removeLoading && removePositionId === pool.myPosition?.id" @click="pool.myPosition?.id && removeLiquidity(pool.myPosition.id)">
                {{ removeLoading && removePositionId === pool.myPosition?.id ? t('common.loading') : t('goldAmm.removeLiquidity') }}
              </button>
            </div>
            <div v-if="removeSuccess && removePositionId === null" class="text-sm text-good mt-2" role="status">{{ removeSuccess }}</div>
            <div v-if="removeError && removePositionId === null" class="text-sm text-bad mt-2" role="alert">{{ removeError }}</div>
          </div>
        </div>
      </div>

      <!-- ├ö├Â├ç├ö├Â├ç Add Liquidity Tab ├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç├ö├Â├ç -->
      <div v-else-if="activeTab === 'addLiquidity'" class="flex flex-col gap-5" aria-label="Add Liquidity">
        <!-- Add to existing pool -->
        <div v-if="pools.length > 0" class="bg-card border border-divider rounded-xl p-6">
          <h3 class="text-base font-semibold text-body mb-1 pb-3 border-b border-divider">
            {{ t('goldAmm.addLiquidityTitle') }}
          </h3>
          <p class="text-xs text-muted mb-4">{{ t('goldAmm.addLiquidityHint') }}</p>

          <div class="flex flex-col gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium text-muted">{{ t('goldAmm.addLiquidityPool') }}</label>
              <select v-model="addPoolId" class="form-select">
                <option v-for="p in pools" :key="p.id" :value="p.id">{{ p.currencyCode }}/XAU ├ö├ç├Â {{ formatFiat(p.fiatReserve) }} {{ p.currencyCode }} + {{ formatGold(p.goldReserve) }} XAU</option>
              </select>
            </div>

            <div v-if="selectedAddPool" class="text-xs text-muted rounded-lg border border-divider bg-card-raised px-3 py-2">
              {{ t('goldAmm.impliedPrice') }}: {{ formatFiat(selectedAddPool.impliedGoldPrice) }} {{ selectedAddPool.currencyCode }}/XAU
              <span v-if="selectedAddPool.myPosition"> ÔöČ─Ü {{ t('goldAmm.positionShares') }}: {{ selectedAddPool.myPosition.sharePercent.toFixed(2) }}%</span>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-medium text-muted">
                  {{ t('goldAmm.addLiquidityFiat') }}<span v-if="selectedAddPool"> ({{ selectedAddPool.currencyCode }})</span>
                </label>
                <input v-model.number="addFiatAmount" type="number" min="0" step="any" class="form-input" :placeholder="t('goldAmm.amountPlaceholder')" />
              </div>
              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-medium text-muted">{{ t('goldAmm.addLiquidityMaxGold') }}</label>
                <input v-model.number="addMaxGold" type="number" min="0" step="any" class="form-input" :placeholder="t('goldAmm.amountPlaceholder')" />
              </div>
            </div>

            <div v-if="goldBalance && goldBalance.blockedInPools > 0" class="text-xs text-caution px-3 py-2 bg-caution/10 rounded-md" role="note">
              ├ö├ť├í┬┤┼×─ć {{ t('goldAmm.blockedGoldWarning') }}
            </div>

            <div v-if="addSuccess" class="text-sm text-good" role="status">{{ addSuccess }}</div>
            <div v-if="addError" class="text-sm text-bad" role="alert">{{ addError }}</div>

            <div>
              <button class="btn btn-primary" :disabled="addLoading || !addFiatAmount || !addPoolId" @click="addLiquidity">
                {{ addLoading ? t('common.loading') : t('goldAmm.addLiquidity') }}
              </button>
            </div>
          </div>
        </div>

        <!-- Create new pool -->
        <div v-if="showCreateForm" class="bg-card border border-divider rounded-xl p-6">
          <h3 class="text-base font-semibold text-body mb-1 pb-3 border-b border-divider">
            {{ t('goldAmm.createPoolTitle') }}
          </h3>
          <p class="text-xs text-muted mb-4">{{ t('goldAmm.createPoolHint') }}</p>

          <div class="flex flex-col gap-4">
            <div class="flex flex-col gap-1.5">
              <label class="text-sm font-medium text-muted">{{ t('goldAmm.createPoolCurrency') }}</label>
              <select v-model="createCurrency" class="form-select">
                <option v-for="c in availableCurrencies" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-medium text-muted">{{ t('goldAmm.createPoolFiat') }} ({{ createCurrency }})</label>
                <input v-model.number="createFiatAmount" type="number" min="0" step="any" class="form-input" :placeholder="t('goldAmm.amountPlaceholder')" />
              </div>
              <div class="flex flex-col gap-1.5">
                <label class="text-sm font-medium text-muted">{{ t('goldAmm.createPoolGold') }}</label>
                <input v-model.number="createGoldAmount" type="number" min="0" step="any" class="form-input" :placeholder="t('goldAmm.amountPlaceholder')" />
              </div>
            </div>

            <div v-if="goldBalance && goldBalance.blockedInPools > 0" class="text-xs text-caution px-3 py-2 bg-caution/10 rounded-md" role="note">
              ├ö├ť├í┬┤┼×─ć {{ t('goldAmm.blockedGoldWarning') }}
            </div>

            <div v-if="createSuccess" class="text-sm text-good" role="status">{{ createSuccess }}</div>
            <div v-if="createError" class="text-sm text-bad" role="alert">{{ createError }}</div>

            <div>
              <button class="btn btn-primary" :disabled="createLoading || !createFiatAmount || !createGoldAmount" @click="createPool">
                {{ createLoading ? t('common.loading') : t('goldAmm.createPool') }}
              </button>
            </div>
          </div>
        </div>

        <div v-if="pools.length === 0 && !showCreateForm" class="text-center py-8 text-muted italic text-sm">
          {{ t('goldAmm.noPools') }}
        </div>
      </div>
    </template>
  </section>
</template>
