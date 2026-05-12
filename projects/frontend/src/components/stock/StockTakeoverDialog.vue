<template>
  <Teleport to="body">
    <div v-if="modelValue" class="takeover-overlay" role="dialog" aria-modal="true" :aria-label="t('stockExchange.takeoverDialogTitle')">
      <div class="takeover-dialog">
        <h2 class="takeover-dialog__title">{{ t('stockExchange.takeoverDialogTitle') }}</h2>
        <template v-if="takeoverSuccess">
          <p class="takeover-dialog__success" role="status">
            {{ t('stockExchange.takeoverSuccessMsg', { company: takeoverSuccess.companyName }) }}
          </p>
          <div class="takeover-dialog__actions">
            <button class="btn btn-primary" @click="emit('close')">{{ t('common.close') }}</button>
          </div>
        </template>
        <template v-else>
          <p class="takeover-dialog__desc">{{ t('stockExchange.takeoverDialogDesc', { company: companyName }) }}</p>
          <p class="takeover-dialog__eligibility">{{ t('stockExchange.takeoverEligibilityHint') }}</p>
          <p v-if="takeoverError" class="trade-feedback trade-feedback--error" role="alert">{{ takeoverError }}</p>
          <div class="takeover-dialog__actions">
            <button class="btn btn-primary" :disabled="takeoverLoading" @click="emit('confirm')">
              {{ takeoverLoading ? t('common.loading') : t('stockExchange.takeoverConfirm') }}
            </button>
            <button class="btn btn-ghost" :disabled="takeoverLoading" @click="emit('close')">
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
import type { ReplaceCeoResult } from '@/types'

defineProps<{
  modelValue: boolean
  companyName: string
  takeoverLoading: boolean
  takeoverError: string | null
  takeoverSuccess: ReplaceCeoResult | null
}>()

const emit = defineEmits<{
  (e: 'confirm'): void
  (e: 'close'): void
}>()

const { t } = useI18n()
</script>

<style scoped>
.takeover-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.takeover-dialog {
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

.takeover-dialog__title {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.takeover-dialog__desc {
  color: var(--color-text-secondary);
  font-size: 0.95rem;
  margin: 0;
}

.takeover-dialog__eligibility {
  background: color-mix(in srgb, var(--color-brand, #38bdf8) 10%, var(--color-surface));
  border: 1px solid color-mix(in srgb, var(--color-brand, #38bdf8) 30%, transparent);
  border-radius: 8px;
  padding: 0.6rem 0.9rem;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  margin: 0;
}

.takeover-dialog__success {
  color: var(--color-success, #22c55e);
  font-size: 0.95rem;
  margin: 0;
}

.takeover-dialog__actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
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
