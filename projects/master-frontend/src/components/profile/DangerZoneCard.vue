<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { requestAccountDeletion, cancelAccountDeletion } from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const { t } = useI18n()

const showConfirm = ref(false)
const confirmationEmail = ref('')
const processing = ref(false)
const errorMessage = ref('')

const isPendingDeletion = computed(() => auth.player?.isPendingDeletion === true)
const scheduledAtUtc = computed(() => auth.player?.deletionScheduledAtUtc ?? null)
const scheduledLabel = computed(() => {
  const value = scheduledAtUtc.value
  if (!value) {
    return ''
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(parsed)
})

const lossItems = computed(() => [
  t('dangerZone.lossProgress'),
  t('dangerZone.lossDeposits'),
  t('dangerZone.lossRewards'),
  t('dangerZone.lossData'),
])

const accountEmail = computed(() => auth.player?.email ?? '')
const canConfirm = computed(
  () =>
    confirmationEmail.value.trim().toLowerCase() === accountEmail.value.trim().toLowerCase() &&
    accountEmail.value.length > 0,
)

function openConfirm() {
  showConfirm.value = true
  confirmationEmail.value = ''
  errorMessage.value = ''
}

function closeConfirm() {
  showConfirm.value = false
  confirmationEmail.value = ''
  errorMessage.value = ''
}

async function confirmDeletion() {
  if (!auth.token || !canConfirm.value) {
    return
  }

  processing.value = true
  errorMessage.value = ''

  try {
    await requestAccountDeletion(auth.token, confirmationEmail.value.trim())
    await auth.fetchProfile()
    showConfirm.value = false
    confirmationEmail.value = ''
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : t('dangerZone.requestError')
  } finally {
    processing.value = false
  }
}

async function cancelDeletion() {
  if (!auth.token) {
    return
  }

  processing.value = true
  errorMessage.value = ''

  try {
    await cancelAccountDeletion(auth.token)
    await auth.fetchProfile()
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : t('dangerZone.cancelError')
  } finally {
    processing.value = false
  }
}
</script>

<template>
  <section
    class="mt-6 rounded-2xl border border-red-500/40 bg-red-500/5 p-6 shadow-sm shadow-black/10"
    data-testid="danger-zone"
  >
    <div class="flex flex-col gap-2">
      <h2 class="text-xl font-semibold text-red-300">{{ t('dangerZone.title') }}</h2>
      <p class="text-sm text-muted">{{ t('dangerZone.body') }}</p>
    </div>

    <!-- Pending deletion state -->
    <div v-if="isPendingDeletion" class="mt-5 flex flex-col gap-4" data-testid="deletion-pending">
      <p
        class="rounded-xl border border-amber-400/25 bg-amber-400/10 px-4 py-3 text-sm text-amber-200"
      >
        {{ t('dangerZone.pendingNotice', { date: scheduledLabel }) }}
      </p>
      <button
        class="btn btn-secondary w-fit"
        type="button"
        :disabled="processing"
        data-testid="cancel-deletion"
        @click="cancelDeletion"
      >
        {{ processing ? t('home.processing') : t('dangerZone.cancelButton') }}
      </button>
    </div>

    <!-- Active state -->
    <div v-else class="mt-5 flex flex-col gap-4">
      <ul class="flex flex-col gap-1.5 text-sm text-muted">
        <li v-for="item in lossItems" :key="item" class="flex items-start gap-2">
          <span class="mt-0.5 text-red-300" aria-hidden="true">•</span>
          <span>{{ item }}</span>
        </li>
      </ul>

      <button
        v-if="!showConfirm"
        class="btn btn-danger w-fit"
        type="button"
        data-testid="open-delete-account"
        @click="openConfirm"
      >
        {{ t('dangerZone.deleteButton') }}
      </button>

      <form
        v-else
        class="flex flex-col gap-3 rounded-xl border border-red-500/30 bg-page p-4"
        data-testid="delete-account-form"
        @submit.prevent="confirmDeletion"
      >
        <p class="text-sm text-body">{{ t('dangerZone.confirmInstruction') }}</p>
        <label class="flex flex-col gap-1.5">
          <span class="text-sm font-semibold text-body">{{ t('dangerZone.confirmEmailLabel') }}</span>
          <input
            v-model="confirmationEmail"
            type="email"
            autocomplete="off"
            :placeholder="accountEmail"
            data-testid="confirm-email-input"
            class="rounded-xl border border-divider bg-card px-4 py-3 text-body transition-colors focus:border-red-400 focus:outline-none"
          />
        </label>
        <div class="flex flex-wrap gap-3">
          <button
            class="btn btn-danger w-fit"
            type="submit"
            :disabled="processing || !canConfirm"
            data-testid="confirm-delete-account"
          >
            {{ processing ? t('home.processing') : t('dangerZone.confirmButton') }}
          </button>
          <button
            class="btn btn-secondary w-fit"
            type="button"
            :disabled="processing"
            data-testid="cancel-delete-account"
            @click="closeConfirm"
          >
            {{ t('dangerZone.abortButton') }}
          </button>
        </div>
      </form>
    </div>

    <p
      v-if="errorMessage"
      class="mt-4 rounded-xl bg-red-500/10 px-4 py-3 text-sm text-red-300"
      role="alert"
      data-testid="danger-zone-error"
    >
      {{ errorMessage }}
    </p>
  </section>
</template>
