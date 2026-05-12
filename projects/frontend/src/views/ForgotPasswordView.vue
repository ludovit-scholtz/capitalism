<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { PasswordResetError, requestPasswordReset } from '@/lib/passwordReset'

const { t } = useI18n()
const email = ref('')
const loading = ref(false)
const successMessage = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const oidcOnly = import.meta.env.VITE_AUTH_PASSWORD_ENABLED !== 'true'

async function handleSubmit() {
  loading.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    successMessage.value = await requestPasswordReset(email.value.trim())
  } catch (error: unknown) {
    if (error instanceof PasswordResetError && error.code === 'METHOD_NOT_ALLOWED') {
      errorMessage.value = t('auth.oidcOnlyBanner')
    } else if (error instanceof Error) {
      errorMessage.value = error.message
    } else {
      errorMessage.value = t('auth.forgotPasswordGenericError')
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
        <h1 class="text-2xl font-bold text-body">{{ t('auth.forgotPasswordTitle') }}</h1>
        <p class="text-sm text-muted">{{ t('auth.forgotPasswordDescription') }}</p>

        <div
          v-if="oidcOnly"
          class="rounded-md border border-brand/25 bg-brand/10 px-3 py-3 text-sm text-body"
          role="status"
        >
          {{ t('auth.oidcOnlyBanner') }}
        </div>

        <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
          <label for="forgot-email" class="text-sm font-medium text-muted">{{ t('auth.email') }}</label>
          <input
            id="forgot-email"
            v-model="email"
            type="email"
            required
            autocomplete="email"
            class="form-input"
          />

          <button type="submit" class="btn btn-primary w-full justify-center" :disabled="loading || oidcOnly">
            {{ loading ? t('common.loading') : t('auth.forgotPasswordSubmit') }}
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
