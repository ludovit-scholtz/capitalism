<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { updatePersonalAccountName } from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const draftName = ref('')
const saving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const currentPersonalAccountName = computed(
  () => auth.player?.personalAccountName ?? auth.player?.displayName ?? '',
)
const navItems = computed(() => {
  const items = [
    { label: t('nav.tokenizedGold'), to: '/account' },
    { label: t('nav.gameSettings'), to: '/settings/game' },
  ]

  if (auth.isGameAdmin) {
    items.unshift({ label: t('home.goldAdmin'), to: '/gold-admin' })
  }

  return items
})

watch(
  currentPersonalAccountName,
  (value) => {
    draftName.value = value
  },
  { immediate: true },
)

const trimmedDraftName = computed(() => draftName.value.trim())
const canSave = computed(
  () =>
    trimmedDraftName.value.length > 0 &&
    trimmedDraftName.value !== currentPersonalAccountName.value,
)

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  if (!auth.player) {
    await auth.fetchProfile()
  }
})

async function savePersonalAccountNameSetting() {
  if (!auth.token || !canSave.value) {
    return
  }

  saving.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    const personalAccountName = await updatePersonalAccountName(auth.token, trimmedDraftName.value)
    if (auth.player) {
      auth.player.displayName = personalAccountName
      auth.player.personalAccountName = personalAccountName
    }

    await auth.fetchProfile()
    draftName.value = personalAccountName
    successMessage.value = t('gameSettings.saved')
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : t('gameSettings.saveError')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <ViewJumbotron
      :kicker="t('gameSettings.kicker')"
      :title="t('gameSettings.title')"
      :subtitle="t('gameSettings.subtitle')"
    />

    <main class="container max-w-4xl pb-16 pt-6 lg:pb-20 lg:pt-8">
      <ViewSubnav :items="navItems" aria-label="Game settings navigation" />

      <section class="mt-6 rounded-2xl border border-divider bg-card p-6 shadow-sm shadow-black/10">
        <div class="flex flex-col gap-2">
          <h2 class="text-xl font-semibold text-body">{{ t('gameSettings.cardTitle') }}</h2>
          <p class="text-sm text-muted">{{ t('gameSettings.cardBody') }}</p>
          <p
            class="rounded-xl border border-amber-400/25 bg-amber-400/10 px-4 py-3 text-sm text-amber-300"
          >
            {{ t('gameSettings.warning') }}
          </p>
        </div>

        <form class="mt-5 flex flex-col gap-4" @submit.prevent="savePersonalAccountNameSetting">
          <label class="flex flex-col gap-1.5">
            <span class="text-sm font-semibold text-body">{{ t('gameSettings.nameLabel') }}</span>
            <input
              v-model="draftName"
              type="text"
              maxlength="40"
              :placeholder="t('gameSettings.namePlaceholder')"
              class="rounded-xl border border-divider bg-page px-4 py-3 text-body transition-colors focus:border-brand focus:outline-none"
            />
          </label>

          <button class="btn btn-primary w-fit" type="submit" :disabled="saving || !canSave">
            {{ saving ? t('home.processing') : t('gameSettings.save') }}
          </button>
        </form>

        <p
          v-if="successMessage"
          class="mt-4 rounded-xl bg-green-500/10 px-4 py-3 text-sm text-green-300"
          role="status"
        >
          {{ successMessage }}
        </p>
        <p
          v-if="errorMessage"
          class="mt-4 rounded-xl bg-red-500/10 px-4 py-3 text-sm text-red-300"
          role="alert"
        >
          {{ errorMessage }}
        </p>

        <div class="mt-6 grid gap-3 md:grid-cols-2">
          <article class="rounded-xl border border-divider bg-page px-4 py-4">
            <h3 class="text-sm font-semibold text-body">{{ t('gameSettings.usageTitleShard') }}</h3>
            <p class="mt-1 text-sm text-muted">{{ t('gameSettings.usageBodyShard') }}</p>
          </article>
          <article class="rounded-xl border border-divider bg-page px-4 py-4">
            <h3 class="text-sm font-semibold text-body">
              {{ t('gameSettings.usageTitleMaster') }}
            </h3>
            <p class="mt-1 text-sm text-muted">{{ t('gameSettings.usageBodyMaster') }}</p>
          </article>
        </div>
      </section>
    </main>
  </div>
</template>
