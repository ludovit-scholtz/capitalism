<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { PasswordResetError, resetPassword } from '@/lib/passwordReset'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const newPassword = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const successMessage = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const oidcOnly = import.meta.env.VITE_AUTH_PASSWORD_ENABLED !== 'true'
const token = computed(() =>
  typeof route.query.token === 'string' ? route.query.token.trim() : '',
)

const passwordStrength = computed(() => {
  const length = newPassword.value.length
  if (length >= 12) return t('auth.passwordStrengthStrong')
  if (length >= 8) return t('auth.passwordStrengthMedium')
  return t('auth.passwordStrengthWeak')
})

async function handleSubmit() {
  errorMessage.value = null
  successMessage.value = null

  if (!token.value) {
    errorMessage.value = t('auth.resetTokenMissing')
    return
  }

  if (newPassword.value !== confirmPassword.value) {
    errorMessage.value = t('auth.resetPasswordMismatch')
    return
  }

  loading.value = true
  try {
    successMessage.value = await resetPassword(token.value, newPassword.value)
    setTimeout(() => {
      router.push('/login')
    }, 2000)
  } catch (error: unknown) {
    if (error instanceof PasswordResetError && error.code === 'METHOD_NOT_ALLOWED') {
      errorMessage.value = t('auth.oidcOnlyBanner')
    } else if (error instanceof Error) {
      errorMessage.value = error.message
    } else {
      errorMessage.value = t('auth.resetPasswordGenericError')
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <main class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
    <section class="mx-auto flex min-h-[calc(100vh-64px-5rem)] max-w-md items-center justify-center py-4 lg:py-6">
      <div class="flex w-full flex-col gap-4 rounded-2xl border border-divider bg-card p-6 shadow-lg sm:p-8">
        <h1 class="text-2xl font-bold text-body">{{ t('auth.resetPasswordTitle') }}</h1>
        <p class="text-sm text-muted">{{ t('auth.resetPasswordDescription') }}</p>

        <div
          v-if="oidcOnly"
          class="rounded-md border border-brand/25 bg-brand/10 px-3 py-3 text-sm text-body"
          role="status"
        >
          {{ t('auth.oidcOnlyBanner') }}
        </div>

        <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
          <label for="new-password" class="text-sm font-medium text-muted">{{ t('auth.resetNewPassword') }}</label>
          <input
            id="new-password"
            v-model="newPassword"
            type="password"
            required
            minlength="8"
            autocomplete="new-password"
            class="form-input"
          />

          <p class="text-xs text-muted">
            {{ t('auth.passwordStrengthLabel') }}: <span class="font-medium">{{ passwordStrength }}</span>
          </p>

          <label for="confirm-password" class="text-sm font-medium text-muted">{{ t('auth.resetConfirmPassword') }}</label>
          <input
            id="confirm-password"
            v-model="confirmPassword"
            type="password"
            required
            minlength="8"
            autocomplete="new-password"
            class="form-input"
          />

          <button type="submit" class="btn btn-primary w-full justify-center" :disabled="loading || oidcOnly">
            {{ loading ? t('common.loading') : t('auth.resetPasswordSubmit') }}
          </button>
        </form>

        <p v-if="successMessage" class="rounded-md bg-good/10 px-3 py-3 text-sm text-good" role="status">
          {{ successMessage }}
        </p>
        <p v-if="errorMessage" class="rounded-md bg-bad/10 px-3 py-3 text-sm text-bad" role="alert">
          {{ errorMessage }}
        </p>
      </div>
    </section>
  </main>
</template>
