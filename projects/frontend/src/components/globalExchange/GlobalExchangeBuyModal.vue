<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { GlobalExchangeOffer, PlayerBankAccountSummary } from '@/types'

interface StorageUnit {
  id: string
  unitType: string
  buildingName: string
  buildingId: string
}

interface BuyFromExchangeResult {
  success: boolean
  errorCode: string | null
  errorMessage: string | null
  resourceName: string
  quantityPurchased: number
  totalCost: number
  newBankBalance: number
  currencyCode: string
}

const props = defineProps<{
  offer: (GlobalExchangeOffer & { resourceName: string; unitSymbol: string }) | null
  open: boolean
}>()

const emit = defineEmits<{
  close: []
  'buy-success': [result: BuyFromExchangeResult]
}>()

const { t, locale } = useI18n()

const quantity = ref<string>('')
const selectedBankAccountId = ref<string>('')
const selectedUnitId = ref<string>('')
const bankAccounts = ref<PlayerBankAccountSummary[]>([])
const storageUnits = ref<StorageUnit[]>([])
const loadingAccounts = ref(false)
const loadingUnits = ref(false)
const submitting = ref(false)
const errorMessage = ref<string | null>(null)

const MY_BANK_ACCOUNTS_QUERY = `
  query MyBankAccounts {
    myBankAccounts {
      id
      accountNumber
      currencyCode
      balance
      companyId
      companyName
      ownerType
      ownerDisplayName
    }
  }
`

const MY_STORAGE_UNITS_QUERY = `
  query MyStorageUnits {
    myCompanies {
      id
      name
      buildings {
        id
        name
        units {
          id
          unitType
        }
      }
    }
  }
`

const BUY_MUTATION = `
  mutation BuyFromExchange($input: BuyFromExchangeInput!) {
    buyFromExchange(input: $input) {
      success
      errorCode
      errorMessage
      resourceName
      quantityPurchased
      totalCost
      newBankBalance
      currencyCode
    }
  }
`

async function loadAccountsAndUnits() {
  if (!props.offer) return
  loadingAccounts.value = true
  loadingUnits.value = true
  errorMessage.value = null
  try {
    const [accountsData, unitsData] = await Promise.all([
      gqlRequest<{ myBankAccounts: PlayerBankAccountSummary[] }>(MY_BANK_ACCOUNTS_QUERY),
      gqlRequest<{
        myCompanies: Array<{
          id: string
          name: string
          buildings: Array<{
            id: string
            name: string
            units: Array<{ id: string; unitType: string }>
          }>
        }>
      }>(MY_STORAGE_UNITS_QUERY),
    ])
    bankAccounts.value = accountsData.myBankAccounts.filter((a) => a.ownerType === 'COMPANY')
    const units: StorageUnit[] = []
    for (const company of unitsData.myCompanies) {
      for (const building of company.buildings) {
        for (const unit of building.units) {
          if (unit.unitType === 'STORAGE' || unit.unitType === 'PURCHASE') {
            units.push({ id: unit.id, unitType: unit.unitType, buildingName: building.name, buildingId: building.id })
          }
        }
      }
    }
    storageUnits.value = units
    if (bankAccounts.value.length > 0 && !selectedBankAccountId.value) {
      selectedBankAccountId.value = bankAccounts.value[0]!.id
    }
    if (units.length > 0 && !selectedUnitId.value) {
      selectedUnitId.value = units[0]!.id
    }
  } finally {
    loadingAccounts.value = false
    loadingUnits.value = false
  }
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      quantity.value = ''
      selectedBankAccountId.value = ''
      selectedUnitId.value = ''
      errorMessage.value = null
      void loadAccountsAndUnits()
    }
  },
)

const quantityNum = computed(() => {
  const n = parseFloat(quantity.value)
  return isNaN(n) ? 0 : n
})

const selectedAccount = computed(() => bankAccounts.value.find((a) => a.id === selectedBankAccountId.value))

const totalCost = computed(() => {
  if (!props.offer || quantityNum.value <= 0) return 0
  return quantityNum.value * props.offer.deliveredPricePerUnit
})

function formatCurrency(val: number): string {
  return formatMoney(val, selectedAccount.value?.currencyCode ?? 'EUR', locale.value)
}

const isValid = computed(
  () => quantityNum.value > 0 && !!selectedBankAccountId.value && !!selectedUnitId.value,
)

async function confirmBuy() {
  if (!props.offer || !isValid.value) return
  submitting.value = true
  errorMessage.value = null
  try {
    const result = await gqlRequest<{ buyFromExchange: BuyFromExchangeResult }>(BUY_MUTATION, {
      input: {
        sourceCityId: props.offer.cityId,
        resourceTypeId: props.offer.resourceTypeId,
        quantity: quantityNum.value,
        targetBuildingUnitId: selectedUnitId.value,
        bankAccountId: selectedBankAccountId.value,
      },
    })
    if (result.buyFromExchange.success) {
      emit('buy-success', result.buyFromExchange)
      emit('close')
    } else {
      errorMessage.value = result.buyFromExchange.errorMessage ?? t('globalExchange.buyErrorTitle')
    }
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : t('globalExchange.buyErrorTitle')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open && offer"
      class="buy-modal-backdrop fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
      role="dialog"
      :aria-modal="true"
      :aria-label="t('globalExchange.buyModalTitle', { resourceName: offer.resourceName })"
      @click.self="emit('close')"
    >
      <div class="buy-modal-panel w-full max-w-md overflow-y-auto rounded-xl border border-divider bg-card shadow-2xl" style="max-height: 90vh">
        <!-- Header -->
        <div class="buy-modal-header flex items-center justify-between border-b border-divider px-5 py-4">
          <div>
            <h2 class="buy-modal-title text-lg font-bold text-body">
              {{ t('globalExchange.buyModalTitle', { resourceName: offer.resourceName }) }}
            </h2>
            <p class="buy-modal-source text-xs text-muted">{{ t('globalExchange.buyFrom', { cityName: offer.cityName }) }}</p>
          </div>
          <button
            class="buy-modal-close rounded-full p-1.5 text-muted transition-colors hover:bg-brand/10 hover:text-brand"
            :aria-label="t('globalExchange.cancelButton')"
            @click="emit('close')"
          >
            ✕
          </button>
        </div>

        <!-- Price breakdown -->
        <div class="buy-modal-price-breakdown border-b border-divider bg-brand/5 px-5 py-3">
          <div class="flex justify-between text-xs text-muted">
            <span>{{ t('globalExchange.exchangePrice') }}</span>
            <span class="exchange-price-row font-medium text-body">{{ formatCurrency(offer.exchangePricePerUnit) }}/{{ offer.unitSymbol }}</span>
          </div>
          <div class="mt-1 flex justify-between text-xs text-muted">
            <span>{{ t('globalExchange.transitCost') }}</span>
            <span class="transit-cost-row font-medium text-body">+{{ formatCurrency(offer.transitCostPerUnit) }}/{{ offer.unitSymbol }}</span>
          </div>
          <div class="mt-1.5 flex justify-between border-t border-divider pt-1.5 text-sm font-bold">
            <span class="text-body">{{ t('globalExchange.deliveredPrice') }}</span>
            <span class="delivered-price-row text-brand">{{ formatCurrency(offer.deliveredPricePerUnit) }}/{{ offer.unitSymbol }}</span>
          </div>
        </div>

        <!-- Form -->
        <div class="buy-modal-form flex flex-col gap-4 px-5 py-4">
          <!-- Quantity -->
          <div class="form-field">
            <label for="buy-quantity" class="mb-1 block text-xs font-semibold uppercase tracking-[0.04em] text-muted">
              {{ t('globalExchange.quantity') }}
            </label>
            <input
              id="buy-quantity"
              v-model="quantity"
              type="number"
              min="0.01"
              step="0.01"
              class="buy-quantity-input w-full rounded-md border border-divider bg-page px-3 py-2 text-sm text-body"
              :placeholder="t('globalExchange.quantityPlaceholder')"
            />
          </div>

          <!-- Bank account -->
          <div class="form-field">
            <label for="buy-bank-account" class="mb-1 block text-xs font-semibold uppercase tracking-[0.04em] text-muted">
              {{ t('globalExchange.bankAccount') }}
            </label>
            <div v-if="loadingAccounts" class="text-xs text-muted">{{ t('globalExchange.loadingAccounts') }}</div>
            <div v-else-if="bankAccounts.length === 0" class="text-xs text-amber-500">{{ t('globalExchange.noCompanyAccounts') }}</div>
            <select
              v-else
              id="buy-bank-account"
              v-model="selectedBankAccountId"
              class="buy-account-select w-full rounded-md border border-divider bg-page px-3 py-2 text-sm text-body"
            >
              <option value="">{{ t('globalExchange.selectBankAccount') }}</option>
              <option v-for="acct in bankAccounts" :key="acct.id" :value="acct.id">
                {{ acct.ownerDisplayName || acct.companyName }} · {{ acct.currencyCode }} {{ formatMoney(acct.balance, acct.currencyCode, locale) }}
              </option>
            </select>
          </div>

          <!-- Target storage unit -->
          <div class="form-field">
            <label for="buy-unit" class="mb-1 block text-xs font-semibold uppercase tracking-[0.04em] text-muted">
              {{ t('globalExchange.targetUnit') }}
            </label>
            <div v-if="loadingUnits" class="text-xs text-muted">{{ t('globalExchange.loadingUnits') }}</div>
            <div v-else-if="storageUnits.length === 0" class="text-xs text-amber-500">{{ t('globalExchange.noStorageUnits') }}</div>
            <select
              v-else
              id="buy-unit"
              v-model="selectedUnitId"
              class="buy-unit-select w-full rounded-md border border-divider bg-page px-3 py-2 text-sm text-body"
            >
              <option value="">{{ t('globalExchange.selectTargetUnit') }}</option>
              <option v-for="unit in storageUnits" :key="unit.id" :value="unit.id">
                {{ unit.buildingName }} — {{ unit.unitType }}
              </option>
            </select>
          </div>

          <!-- Total cost preview -->
          <div v-if="quantityNum > 0" class="buy-total-cost rounded-lg border border-brand/20 bg-brand/5 px-4 py-3">
            <div class="flex items-center justify-between">
              <span class="text-sm font-semibold text-muted">{{ t('globalExchange.totalCost') }}</span>
              <span class="buy-total-value text-lg font-bold text-brand">{{ formatCurrency(totalCost) }}</span>
            </div>
          </div>

          <!-- Error -->
          <div v-if="errorMessage" class="buy-error rounded-lg border border-danger/20 bg-danger/10 px-4 py-2 text-sm text-danger">
            {{ errorMessage }}
          </div>

          <!-- Actions -->
          <div class="buy-modal-actions flex gap-3 pt-1">
            <button
              class="buy-cancel-btn flex-1 rounded-md border border-divider bg-card py-2 text-sm font-semibold text-muted transition-colors hover:border-brand hover:text-brand"
              @click="emit('close')"
            >
              {{ t('globalExchange.cancelButton') }}
            </button>
            <button
              class="buy-confirm-btn flex-1 rounded-md bg-brand py-2 text-sm font-bold text-white transition-colors hover:bg-brand-hover disabled:cursor-not-allowed disabled:opacity-50"
              :disabled="!isValid || submitting"
              @click="confirmBuy"
            >
              {{ submitting ? '...' : t('globalExchange.confirmBuy') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.buy-modal-backdrop {
  backdrop-filter: blur(4px);
}
</style>
