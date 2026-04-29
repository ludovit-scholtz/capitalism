<script setup lang="ts">
/* eslint-disable @typescript-eslint/no-unused-vars */
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

<template src="./GoldAmmSection.template.html"></template>
