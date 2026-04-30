<template>
  <Teleport to="body">
    <div v-if="modelValue" class="merge-overlay" role="dialog" aria-modal="true" :aria-label="t('stockExchange.mergeDialogTitle')">
      <div class="merge-dialog">
        <h2 class="merge-dialog__title">{{ t('stockExchange.mergeDialogTitle') }}</h2>
        <template v-if="mergeSuccess">
          <p class="merge-dialog__success" role="status">
            {{
              t('stockExchange.mergeSuccessMsg', {
                absorbed: mergeSuccess.absorbedCompanyName,
                destination: mergeSuccess.destinationCompanyName,
                buildings: mergeSuccess.buildingsTransferred,
                cash: formatCurrency(mergeSuccess.cashTransferred),
              })
            }}
          </p>
          <div class="merge-dialog__actions">
            <button class="btn btn-primary" @click="emit('close')">{{ t('common.close') }}</button>
          </div>
        </template>
        <template v-else>
          <p class="merge-dialog__desc">{{ t('stockExchange.mergeDialogDesc') }}</p>
          <p class="merge-dialog__eligibility">{{ t('stockExchange.mergeEligibilityHint') }}</p>
          <label class="trade-field">
            <span>{{ t('stockExchange.mergeDestinationLabel') }}</span>
            <select
              :value="destinationCompanyId"
              class="trade-select"
              :aria-label="t('stockExchange.mergeDestinationLabel')"
              @change="emit('update:destinationCompanyId', ($event.target as HTMLSelectElement).value)"
            >
              <option v-for="company in controlledCompanies" :key="company.id" :value="company.id">
                {{ company.name }}
              </option>
            </select>
          </label>
          <p v-if="mergeError" class="trade-feedback trade-feedback--error" role="alert">{{ mergeError }}</p>
          <div class="merge-dialog__actions">
            <button class="btn btn-warning" :disabled="mergeLoading || !destinationCompanyId" @click="emit('confirm')">
              {{ mergeLoading ? t('common.loading') : t('stockExchange.mergeConfirm') }}
            </button>
            <button class="btn btn-ghost" :disabled="mergeLoading" @click="emit('close')">
              {{ t('common.cancel') }}
            </button>
          </div>
        </template>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { MergeCompanyResult } from '@/types'

type ControlledCompany = { id: string; name: string; cash: number | null }

const props = defineProps<{
  modelValue: boolean
  destinationCompanyId: string
  controlledCompanies: ControlledCompany[]
  mergeLoading: boolean
  mergeError: string | null
  mergeSuccess: MergeCompanyResult | null
  locale: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'update:destinationCompanyId', value: string): void
  (e: 'confirm'): void
  (e: 'close'): void
}>()

const { t } = useI18n()

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(props.locale, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 2,
  }).format(value)
}
</script>

<style scoped>
.merge-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.merge-dialog {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  padding: 2rem;
  max-width: 480px;
  width: 90%;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  box-shadow: var(--shadow-lg, 0 20px 40px rgba(0, 0, 0, 0.4));
}

.merge-dialog__title {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.merge-dialog__desc {
  color: var(--color-text-secondary);
  font-size: 0.95rem;
  margin: 0;
}

.merge-dialog__eligibility {
  background: color-mix(in srgb, var(--color-warning, #f59e0b) 10%, var(--color-surface));
  border: 1px solid color-mix(in srgb, var(--color-warning, #f59e0b) 30%, transparent);
  border-radius: 8px;
  padding: 0.6rem 0.9rem;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.merge-dialog__success {
  color: var(--color-success, #22c55e);
  font-size: 0.95rem;
  margin: 0;
}

.merge-dialog__actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
}

.trade-field {
  display: grid;
  gap: 0.35rem;
}

.trade-select {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-background);
  color: var(--color-text);
  padding: 0.6rem 0.8rem;
  font-size: 0.9rem;
  min-width: 200px;
}

.trade-feedback {
  margin: 0;
  padding: 0.65rem 0.9rem;
  border-radius: 10px;
  font-size: 0.88rem;
}

.trade-feedback--error {
  background: color-mix(in srgb, var(--color-danger, #ef4444) 14%, var(--color-surface));
  color: var(--color-danger, #ef4444);
}
</style>
