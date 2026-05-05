<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface BuyerCompany {
  id: string
  name: string
}

const props = defineProps<{
  buildingName: string
  offerAmount: number | null
  offerNote: string
  offerBuyerCompanyId: string
  offerBuyerCompanies: BuyerCompany[]
  offerSubmitting: boolean
  actionError: string | null
}>()

const emit = defineEmits<{
  'update:offerAmount': [value: number | null]
  'update:offerNote': [value: string]
  'update:offerBuyerCompanyId': [value: string]
  close: []
  submit: []
}>()
</script>

<template>
  <div class="modal-overlay" @click.self="emit('close')">
    <div class="modal-panel" role="dialog" :aria-label="t('buildingMarket.makeOffer')">
      <h2 class="modal-title">{{ t('buildingMarket.makeOffer') }}</h2>
      <p class="modal-building-name">{{ buildingName }}</p>
      <p class="offer-tip">{{ t('buildingMarket.offerTip') }}</p>

      <label class="form-label" for="offerAmount">{{ t('buildingMarket.offerAmount') }}</label>
      <input
        id="offerAmount"
        :value="offerAmount"
        type="number"
        class="form-input"
        :placeholder="t('buildingMarket.offerAmountPlaceholder')"
        min="1"
        @input="emit('update:offerAmount', ($event.target as HTMLInputElement).valueAsNumber || null)"
      />

      <label class="form-label" for="buyerCompany">{{ t('buildingMarket.buyerCompany') }}</label>
      <select
        id="buyerCompany"
        :value="offerBuyerCompanyId"
        class="form-input"
        @change="emit('update:offerBuyerCompanyId', ($event.target as HTMLSelectElement).value)"
      >
        <option v-for="co in offerBuyerCompanies" :key="co.id" :value="co.id">{{ co.name }}</option>
      </select>

      <label class="form-label" for="offerNote">{{ t('buildingMarket.offerNote') }}</label>
      <textarea
        id="offerNote"
        :value="offerNote"
        class="form-input"
        :placeholder="t('buildingMarket.offerNotePlaceholder')"
        rows="3"
        @input="emit('update:offerNote', ($event.target as HTMLTextAreaElement).value)"
      />

      <div v-if="actionError" class="alert alert-error">{{ actionError }}</div>

      <div class="modal-actions">
        <button
          class="btn btn-primary"
          :disabled="offerSubmitting || !offerAmount || !offerBuyerCompanyId"
          @click="emit('submit')"
        >
          {{ offerSubmitting ? t('common.saving') : t('buildingMarket.submitOffer') }}
        </button>
        <button class="btn btn-secondary" @click="emit('close')">
          {{ t('buildingMarket.cancelOffer') }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-panel {
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 2rem;
  max-width: 480px;
  width: 90%;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.modal-title {
  font-size: 1.2rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.modal-building-name {
  color: var(--color-text-secondary);
  font-size: 0.95rem;
  margin: 0;
}

.offer-tip {
  background: var(--color-info-bg, #eff6ff);
  color: var(--color-info, #1d4ed8);
  border-radius: 6px;
  padding: 0.5rem 0.75rem;
  font-size: 0.85rem;
  margin: 0;
}

.form-label {
  font-weight: 500;
  font-size: 0.9rem;
  color: var(--color-text-secondary);
}

.form-input {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg);
  color: var(--color-text-primary);
  font-size: 0.95rem;
  box-sizing: border-box;
}

.modal-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.5rem;
}

.alert {
  padding: 0.75rem 1rem;
  border-radius: 8px;
  font-size: 0.9rem;
}

.alert-error {
  background: var(--color-error-bg, #fee2e2);
  color: var(--color-error, #dc2626);
}

.btn {
  padding: 0.5rem 1.25rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 600;
  transition: opacity 0.15s;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-primary {
  background: var(--color-primary);
  color: #fff;
}

.btn-secondary {
  background: var(--color-bg-subtle);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border);
}
</style>
