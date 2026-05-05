<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatPercent } from '@/lib/loanHelpers'
import type { BankDepositRateHistorySummary } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  currentRatePercent: number
  pendingRatePercent: number | null
  pendingRateEffectiveTick: number | null
  currentTick: number
  rateHistory: BankDepositRateHistorySummary[]
  loading: boolean
  error: string | null
  success: boolean
  depositCount: number
}>()

const emit = defineEmits<{
  (e: 'save-deposit-rate', newRate: number): void
}>()

const showForm = ref(false)
const newRateInput = ref(props.currentRatePercent)
const showConfirm = ref(false)

const rateColorClass = computed(() => {
  if (props.currentRatePercent >= 4) return 'rate-green'
  if (props.currentRatePercent >= 2) return 'rate-yellow'
  return 'rate-red'
})

const ticksUntilEffective = computed(() => {
  if (!props.pendingRateEffectiveTick) return null
  return Math.max(0, props.pendingRateEffectiveTick - props.currentTick)
})

function openForm() {
  newRateInput.value = props.pendingRatePercent ?? props.currentRatePercent
  showConfirm.value = false
  showForm.value = true
}

function cancelForm() {
  showForm.value = false
  showConfirm.value = false
}

function requestConfirm() {
  if (newRateInput.value < 0 || newRateInput.value > 50) return
  showConfirm.value = true
}

function confirmSave() {
  emit('save-deposit-rate', newRateInput.value)
  showConfirm.value = false
  showForm.value = false
}

function formatDate(utcStr: string) {
  return new Date(utcStr).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}
</script>

<template>
  <div class="drate-panel">
    <!-- Current rate row -->
    <div class="drate-header">
      <div class="drate-stat">
        <span class="drate-label">{{ t('bank.depositInterestRate') }}</span>
        <span class="drate-value" :class="rateColorClass">{{ formatPercent(currentRatePercent) }}</span>
      </div>

      <!-- Pending change badge -->
      <div v-if="pendingRatePercent !== null && pendingRateEffectiveTick !== null" class="drate-pending">
        <span class="pending-badge">⏳ {{ t('bank.pendingRate') }}</span>
        <span class="pending-value">{{ formatPercent(pendingRatePercent) }}</span>
        <span class="pending-eta">
          {{ t('bank.effectiveAtTick', { tick: pendingRateEffectiveTick, eta: ticksUntilEffective }) }}
        </span>
      </div>

      <!-- Adjust rate button -->
      <button class="btn btn-primary btn-sm" :disabled="loading" @click="openForm">
        {{ t('bank.adjustDepositRate') }}
      </button>
    </div>

    <!-- Rate adjustment form -->
    <div v-if="showForm" class="drate-form">
      <div class="form-row">
        <label class="form-label" for="new-deposit-rate">
          {{ t('bank.newDepositRate') }} (0–50%)
        </label>
        <input
          id="new-deposit-rate"
          v-model.number="newRateInput"
          type="number"
          min="0"
          max="50"
          step="0.1"
          class="form-input"
          @keydown.enter="requestConfirm"
        />
      </div>

      <p class="form-hint">
        {{ t('bank.depositRateHint', { count: depositCount, ticks: 24 }) }}
      </p>

      <!-- Confirmation step -->
      <div v-if="showConfirm" class="confirm-box">
        <p class="confirm-text">
          {{ t('bank.depositRateConfirm', { from: formatPercent(currentRatePercent), to: formatPercent(newRateInput), count: depositCount }) }}
        </p>
        <div class="confirm-actions">
          <button class="btn btn-danger btn-sm" :disabled="loading" @click="confirmSave">
            {{ loading ? t('common.loading') : t('bank.updateRate') }}
          </button>
          <button class="btn btn-secondary btn-sm" @click="showConfirm = false">{{ t('common.cancel') }}</button>
        </div>
      </div>

      <div v-if="error" class="error-message">{{ error }}</div>
      <div v-if="success && !showForm" class="success-message">{{ t('bank.depositRateUpdated') }}</div>

      <div v-if="!showConfirm" class="form-actions">
        <button class="btn btn-primary btn-sm" :disabled="newRateInput < 0 || newRateInput > 50" @click="requestConfirm">
          {{ t('bank.reviewChange') }}
        </button>
        <button class="btn btn-secondary btn-sm" @click="cancelForm">{{ t('common.cancel') }}</button>
      </div>
    </div>

    <div v-if="success && !showForm" class="success-message success-message--standalone">
      {{ t('bank.depositRateUpdated') }}
    </div>

    <!-- Rate history table -->
    <div v-if="rateHistory.length > 0" class="drate-history">
      <h3 class="history-title">{{ t('bank.rateChangeHistory') }}</h3>
      <div class="history-table-wrap">
        <table class="history-table">
          <thead>
            <tr>
              <th>{{ t('bank.historyDate') }}</th>
              <th>{{ t('bank.historyPrevRate') }}</th>
              <th>{{ t('bank.historyNewRate') }}</th>
              <th>{{ t('bank.historyEffectiveTick') }}</th>
              <th>{{ t('bank.historyAffected') }}</th>
              <th>{{ t('bank.historyStatus') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="entry in rateHistory" :key="entry.id" :class="entry.isApplied ? 'row-applied' : 'row-pending'">
              <td>{{ formatDate(entry.scheduledAtUtc) }}</td>
              <td>{{ formatPercent(entry.previousRatePercent) }}</td>
              <td class="new-rate-cell">{{ formatPercent(entry.newRatePercent) }}</td>
              <td>{{ entry.effectiveTick }}</td>
              <td>{{ entry.isApplied ? entry.affectedDepositCount : '—' }}</td>
              <td>
                <span class="status-badge" :class="entry.isApplied ? 'status-applied' : 'status-pending'">
                  {{ entry.isApplied ? t('bank.statusApplied') : t('bank.statusPending') }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.drate-panel {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1.25rem 1.5rem;
  margin-bottom: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.drate-header {
  display: flex;
  align-items: center;
  gap: 1.25rem;
  flex-wrap: wrap;
}

.drate-stat {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.drate-label {
  font-size: 0.78rem;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.drate-value {
  font-size: 1.4rem;
  font-weight: 800;
}

.rate-green { color: var(--color-success, #22c55e); }
.rate-yellow { color: var(--color-warning, #f59e0b); }
.rate-red { color: var(--color-error, #ef4444); }

.drate-pending {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  background: rgba(245, 158, 11, 0.08);
  border: 1px solid rgba(245, 158, 11, 0.3);
  border-radius: 6px;
  padding: 0.4rem 0.75rem;
  font-size: 0.85rem;
}

.pending-badge {
  font-size: 0.75rem;
  color: var(--color-warning, #f59e0b);
  font-weight: 600;
}

.pending-value {
  font-weight: 700;
  color: var(--color-text-primary);
}

.pending-eta {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

/* Form */
.drate-form {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.form-row {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.form-label {
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.form-input {
  padding: 6px var(--spacing-sm);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text-primary);
  max-width: 180px;
}

.form-hint {
  font-size: 0.8rem;
  color: var(--color-text-muted);
  margin: 0;
}

.form-actions {
  display: flex;
  gap: 0.5rem;
}

.confirm-box {
  background: rgba(239, 68, 68, 0.06);
  border: 1px solid rgba(239, 68, 68, 0.25);
  border-radius: 6px;
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.confirm-text {
  font-size: 0.85rem;
  margin: 0;
}

.confirm-actions {
  display: flex;
  gap: 0.5rem;
}

/* Buttons */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--spacing-xs) var(--spacing-md);
  border-radius: var(--radius-sm);
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: background-color 0.2s;
}

.btn-sm {
  padding: 4px var(--spacing-sm);
  font-size: 0.8rem;
}

.btn-primary {
  background: var(--color-primary, #3b82f6);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--color-primary-hover, #2563eb);
}

.btn-primary:disabled,
.btn-danger:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--color-surface);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border);
}

.btn-danger {
  background: var(--color-error, #ef4444);
  color: white;
}

/* Messages */
.error-message {
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
  padding: var(--spacing-sm);
  border-radius: var(--radius-sm);
  font-size: 0.85rem;
}

.success-message {
  color: var(--color-success, #22c55e);
  font-size: 0.875rem;
}

.success-message--standalone {
  padding: 0.25rem 0;
}

/* History */
.drate-history {
  margin-top: 0.5rem;
}

.history-title {
  font-size: 0.95rem;
  font-weight: 600;
  margin: 0 0 0.5rem;
}

.history-table-wrap {
  overflow-x: auto;
}

.history-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.82rem;
}

.history-table th,
.history-table td {
  padding: 0.4rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
}

.history-table th {
  font-weight: 600;
  color: var(--color-text-muted);
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.row-applied {
  opacity: 0.75;
}

.new-rate-cell {
  font-weight: 700;
  color: var(--color-text-primary);
}

.status-badge {
  display: inline-block;
  padding: 0.1em 0.5em;
  border-radius: 9999px;
  font-size: 0.72rem;
  font-weight: 700;
}

.status-applied {
  background: rgba(34, 197, 94, 0.12);
  color: var(--color-success, #22c55e);
}

.status-pending {
  background: rgba(245, 158, 11, 0.12);
  color: var(--color-warning, #f59e0b);
}
</style>
