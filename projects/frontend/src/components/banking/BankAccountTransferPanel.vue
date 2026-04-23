<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import ForexBankAccountSelector from '@/components/forex/ForexBankAccountSelector.vue'
import type { PlayerBankAccountSummary, TransferFundsResult } from '@/types'

interface Props {
  /** Bank accounts owned by the player. Already loaded by the parent view. */
  accounts: PlayerBankAccountSummary[]
}
interface Emits {
  /**
   * Emitted after a successful transfer so the parent can refresh the bank-account
   * list (balances) without a full page reload.
   */
  (e: 'transferred', result: TransferFundsResult): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const { t } = useI18n()

const fromAccountId = ref<string>('')
const toAccountId = ref<string>('')
const amount = ref<number | null>(null)
const description = ref<string>('')

const submitting = ref(false)
const errorMessage = ref<string | null>(null)
const successResult = ref<TransferFundsResult | null>(null)

function findAccount(id: string): PlayerBankAccountSummary | undefined {
  return props.accounts.find((a) => a.id === id)
}

const fromAccount = computed(() => findAccount(fromAccountId.value))
const toAccount = computed(() => findAccount(toAccountId.value))

/** Destination accounts must share the source account's currency. */
const destinationAccounts = computed<PlayerBankAccountSummary[]>(() => {
  const from = fromAccount.value
  if (!from) return []
  return props.accounts.filter(
    (a) => a.id !== from.id && a.currencyCode === from.currencyCode,
  )
})

const validationMessage = computed<string | null>(() => {
  if (!fromAccountId.value) return t('bankTransfer.selectSource')
  if (destinationAccounts.value.length === 0) return t('bankTransfer.noMatchingDestination')
  if (!toAccountId.value) return t('bankTransfer.selectDestination')
  if (fromAccountId.value === toAccountId.value) return t('bankTransfer.sameAccount')
  if (fromAccount.value && toAccount.value
    && fromAccount.value.currencyCode !== toAccount.value.currencyCode) {
    return t('bankTransfer.currencyMismatch')
  }
  if (!amount.value || amount.value <= 0) return t('bankTransfer.invalidAmount')
  if (fromAccount.value && amount.value > fromAccount.value.balance) {
    return t('bankTransfer.insufficientFunds')
  }
  return null
})

function formatAmount(val: number): string {
  return new Intl.NumberFormat('en', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(val)
}

const TRANSFER_MUTATION = `
  mutation TransferFunds($input: TransferFundsInput!) {
    transferFunds(input: $input) {
      amount
      currencyCode
      fromAccount { id accountNumber currencyCode currencySymbol balance companyId companyName }
      toAccount { id accountNumber currencyCode currencySymbol balance companyId companyName }
    }
  }
`

async function submitTransfer() {
  if (validationMessage.value || !fromAccount.value || !toAccount.value || !amount.value) {
    return
  }
  submitting.value = true
  errorMessage.value = null
  successResult.value = null
  try {
    const result = await gqlRequest<{ transferFunds: TransferFundsResult }>(
      TRANSFER_MUTATION,
      {
        input: {
          fromBankAccountId: fromAccountId.value,
          toBankAccountId: toAccountId.value,
          amount: amount.value,
          description: description.value.trim() || null,
        },
      },
    )
    successResult.value = result.transferFunds
    amount.value = null
    description.value = ''
    emit('transferred', result.transferFunds)
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e)
  } finally {
    submitting.value = false
  }
}

/** When source changes, reset destination if it no longer matches the new currency. */
function onFromChanged(newId: string) {
  fromAccountId.value = newId
  successResult.value = null
  if (!destinationAccounts.value.some((a) => a.id === toAccountId.value)) {
    toAccountId.value = ''
  }
}
</script>

<template>
  <section
    class="bg-card border border-divider rounded-xl p-6 mb-6"
    :aria-label="t('bankTransfer.tabLabel')"
  >
    <h2 class="text-lg font-semibold text-body mb-2 pb-3 border-b border-divider">
      {{ t('bankTransfer.title') }}
    </h2>
    <p class="text-sm text-muted mb-4">{{ t('bankTransfer.subtitle') }}</p>

    <div v-if="accounts.length < 2" class="text-sm text-muted italic">
      {{ t('bankTransfer.needTwoAccounts') }}
    </div>

    <div v-else class="grid gap-4 md:grid-cols-2">
      <ForexBankAccountSelector
        :model-value="fromAccountId"
        :accounts="accounts"
        :label="t('bankTransfer.fromAccount')"
        id="bank-transfer-from"
        @update:model-value="onFromChanged"
      />
      <ForexBankAccountSelector
        v-model="toAccountId"
        :accounts="destinationAccounts"
        :label="t('bankTransfer.toAccount')"
        id="bank-transfer-to"
        :disabled="!fromAccountId || destinationAccounts.length === 0"
      />

      <div class="flex flex-col gap-1.5">
        <label
          for="bank-transfer-amount"
          class="text-xs font-semibold text-muted uppercase tracking-wide"
        >
          {{ t('bankTransfer.amount') }}
        </label>
        <input
          id="bank-transfer-amount"
          v-model.number="amount"
          type="number"
          step="0.01"
          min="0.01"
          class="bg-page border border-divider rounded-lg px-3 py-2 text-body text-sm focus:outline-none focus:border-brand"
          :placeholder="t('bankTransfer.amountPlaceholder')"
        />
        <span v-if="fromAccount" class="text-xs text-muted">
          {{ t('bankTransfer.available') }}:
          <span class="font-semibold text-body">
            {{ fromAccount.currencySymbol }}{{ formatAmount(fromAccount.balance) }}
          </span>
        </span>
      </div>

      <div class="flex flex-col gap-1.5">
        <label
          for="bank-transfer-description"
          class="text-xs font-semibold text-muted uppercase tracking-wide"
        >
          {{ t('bankTransfer.description') }}
        </label>
        <input
          id="bank-transfer-description"
          v-model="description"
          type="text"
          maxlength="200"
          class="bg-page border border-divider rounded-lg px-3 py-2 text-body text-sm focus:outline-none focus:border-brand"
          :placeholder="t('bankTransfer.descriptionPlaceholder')"
        />
      </div>

      <div class="md:col-span-2 flex flex-col gap-3">
        <p
          v-if="validationMessage"
          class="text-sm text-caution"
          role="alert"
          aria-live="polite"
        >
          {{ validationMessage }}
        </p>
        <p
          v-if="errorMessage"
          class="text-sm text-bad"
          role="alert"
          aria-live="polite"
        >
          {{ errorMessage }}
        </p>
        <p
          v-if="successResult"
          class="text-sm text-good"
          role="status"
          aria-live="polite"
        >
          {{ t('bankTransfer.success', {
            amount: formatAmount(successResult.amount),
            currency: successResult.currencyCode,
            from: successResult.fromAccount.companyName,
            to: successResult.toAccount.companyName,
          }) }}
        </p>
        <button
          type="button"
          class="btn btn-primary self-start disabled:opacity-50 disabled:cursor-not-allowed"
          :disabled="submitting || validationMessage !== null"
          @click="submitTransfer"
        >
          {{ submitting ? t('bankTransfer.submitting') : t('bankTransfer.submit') }}
        </button>
      </div>
    </div>
  </section>
</template>
