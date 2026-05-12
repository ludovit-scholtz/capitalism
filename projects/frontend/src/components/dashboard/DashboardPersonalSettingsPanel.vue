<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphqlMasterServer'
import { useAuthStore } from '@/stores/auth'

const emit = defineEmits<{
  (e: 'saved'): void
}>()

const auth = useAuthStore()
const { t } = useI18n()

const currentPersonalAccountName = computed(() => auth.player?.personalAccountName ?? auth.player?.displayName ?? '')
const draftName = ref('')
const saving = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

watch(
  currentPersonalAccountName,
  (value) => {
    draftName.value = value
  },
  { immediate: true },
)

const trimmedDraftName = computed(() => draftName.value.trim())
const canSave = computed(() => trimmedDraftName.value.length > 0 && trimmedDraftName.value !== currentPersonalAccountName.value)

async function savePersonalAccountName() {
  if (!canSave.value) {
    return
  }

  saving.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const data = await gqlRequest<{
      updatePersonalAccountName: {
        personalAccountName: string
      }
    }>(
      `mutation UpdatePersonalAccountName($input: UpdatePersonalAccountNameInput!) {
        updatePersonalAccountName(input: $input) {
          personalAccountName
        }
      }`,
      {
        input: {
          personalAccountName: trimmedDraftName.value,
        },
      },
    )

    const personalAccountName = data.updatePersonalAccountName.personalAccountName
    if (auth.player) {
      auth.player.displayName = personalAccountName
      auth.player.personalAccountName = personalAccountName
    }

    draftName.value = personalAccountName
    successMessage.value = t('dashboard.personalSettingsSaved')
    await auth.fetchMe()
    emit('saved')
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : t('dashboard.personalSettingsSaveError')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="person-account-settings-panel flex flex-col gap-4">
    <div class="flex flex-col gap-2">
      <h3 class="text-[0.9375rem] font-bold">{{ t('dashboard.personalSettingsTitle') }}</h3>
      <p class="m-0 text-sm text-muted">{{ t('dashboard.personalSettingsBody') }}</p>
      <p class="m-0 rounded-lg border border-amber-400/25 bg-amber-400/10 px-3 py-2 text-sm text-amber-300">
        {{ t('dashboard.personalSettingsWarning') }}
      </p>
      <p class="m-0 text-xs text-muted">{{ t('dashboard.personalSettingsSharedHint') }}</p>
    </div>

    <form class="flex flex-col gap-3" @submit.prevent="savePersonalAccountName">
      <label class="flex flex-col gap-1.5">
        <span class="text-sm font-semibold">{{ t('dashboard.personalSettingsLabel') }}</span>
        <input
          v-model="draftName"
          type="text"
          maxlength="40"
          :placeholder="t('dashboard.personalSettingsPlaceholder')"
          class="px-3.5 py-3 border border-divider rounded bg-page text-body focus:outline-none focus:border-brand transition-colors"
        />
      </label>

      <div class="flex flex-wrap gap-3">
        <button class="btn btn-primary" type="submit" :disabled="saving || !canSave">
          {{ saving ? t('common.loading') : t('dashboard.personalSettingsSave') }}
        </button>
      </div>
    </form>

    <p v-if="successMessage" class="m-0 rounded-lg bg-[rgba(34,197,94,0.12)] px-3 py-3 text-sm text-good" role="status">
      {{ successMessage }}
    </p>
    <p v-if="errorMessage" class="m-0 rounded-lg bg-[rgba(248,113,113,0.12)] px-3 py-3 text-sm text-bad" role="alert">
      {{ errorMessage }}
    </p>
  </div>
</template>
