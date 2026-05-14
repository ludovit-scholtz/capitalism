<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest as gqlGameRequest } from '@/lib/graphql'
import { gqlRequest as gqlMasterRequest } from '@/lib/graphqlMasterServer'
import GenderPicker from '@/components/profile/GenderPicker.vue'
import { generatePersonalAccountName, type PlayerGender } from '@/lib/personalAccountName'
import { useAuthStore } from '@/stores/auth'
import DashboardApiKeysPanel from '@/components/dashboard/DashboardApiKeysPanel.vue'

const emit = defineEmits<{
  (e: 'saved'): void
}>()

const auth = useAuthStore()
const { t } = useI18n()

const currentPersonalAccountName = computed(() => auth.player?.personalAccountName ?? auth.player?.displayName ?? '')
const currentGender = computed<PlayerGender>(() => {
  const value = auth.player?.gender
  if (value === 'MALE' || value === 'FEMALE' || value === 'UNSPECIFIED') return value
  return 'UNSPECIFIED'
})
const draftName = ref('')
const selectedGender = ref<PlayerGender>('UNSPECIFIED')
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

watch(
  currentGender,
  (value) => {
    selectedGender.value = value
  },
  { immediate: true },
)

const trimmedDraftName = computed(() => draftName.value.trim())
const canSave = computed(
  () =>
    trimmedDraftName.value.length > 0
    && (trimmedDraftName.value !== currentPersonalAccountName.value
      || selectedGender.value !== currentGender.value),
)

function handleGenderSelect(gender: PlayerGender) {
  selectedGender.value = gender
  draftName.value = generatePersonalAccountName(gender)
}

function regeneratePersonalName() {
  draftName.value = generatePersonalAccountName(selectedGender.value)
}

async function savePersonalAccountName() {
  if (!canSave.value) {
    return
  }

  saving.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    await gqlMasterRequest<{
      updatePersonalAccountName: {
        personalAccountName: string
        gender: string
      }
    }>(
      `mutation UpdatePersonalAccountName($input: UpdatePersonalAccountNameInput!) {
        updatePersonalAccountName(input: $input) {
          personalAccountName
          gender
        }
      }`,
      {
        input: {
          personalAccountName: trimmedDraftName.value,
          gender: selectedGender.value,
        },
      },
    )

    const data = await gqlGameRequest<{
      updateDisplayName: {
        displayName: string
        gender: string
      }
    }>(
      `mutation UpdateDisplayName($displayName: String!, $gender: String) {
        updateDisplayName(input: { displayName: $displayName, gender: $gender }) {
          displayName
          gender
        }
      }`,
      {
        displayName: trimmedDraftName.value,
        gender: selectedGender.value,
      },
    )

    const personalAccountName = data.updateDisplayName.displayName
    if (auth.player) {
      auth.player.displayName = personalAccountName
      auth.player.personalAccountName = personalAccountName
      auth.player.gender = selectedGender.value
    }

    draftName.value = personalAccountName
    successMessage.value = t('dashboard.personalSettingsSaved')
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
        <GenderPicker
          v-model="selectedGender"
          :female-label="t('dashboard.selectFemale')"
          :male-label="t('dashboard.selectMale')"
          @update:model-value="handleGenderSelect"
        />
      </label>
      <label class="flex flex-col gap-1.5">
        <span class="text-sm font-semibold">{{ t('dashboard.personalSettingsNameLabel') }}</span>
        <input
          v-model="draftName"
          type="text"
          maxlength="40"
          :placeholder="t('dashboard.personalSettingsPlaceholder')"
          class="px-3.5 py-3 border border-divider rounded bg-page text-body focus:outline-none focus:border-brand transition-colors"
        />
      </label>

      <div class="flex flex-wrap gap-3">
        <button
          class="btn btn-secondary"
          type="button"
          :title="t('dashboard.personalSettingsRegenerate')"
          @click="regeneratePersonalName"
        >
          🎲
        </button>
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

    <DashboardApiKeysPanel />
  </div>
</template>
