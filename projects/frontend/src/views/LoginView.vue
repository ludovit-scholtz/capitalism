<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const isRegister = ref(false)
const email = ref('')
const displayName = ref('')
const password = ref('')
const formError = ref<string | null>(null)

async function handleSubmit() {
  formError.value = null
  try {
    if (isRegister.value) {
      await auth.register(email.value, displayName.value, password.value)
    } else {
      await auth.login(email.value, password.value)
    }
    router.push('/')
  } catch (e: unknown) {
    formError.value = e instanceof Error ? e.message : 'An error occurred'
  }
}
</script>

<template>
  <div class="container flex justify-center items-center min-h-[calc(100vh-128px)] py-8 px-4">
    <div class="bg-card border border-divider rounded-xl p-10 w-full max-w-sm shadow-md">
      <h1 class="text-2xl font-bold mb-6 text-body">
        {{ isRegister ? t('auth.registerTitle') : t('auth.loginTitle') }}
      </h1>

      <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <div
          v-if="formError"
          class="bg-bad/10 text-bad px-3 py-3 rounded-md text-sm"
          role="alert"
        >
          {{ formError }}
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-sm font-medium text-muted">{{ t('auth.email') }}</label>
          <input
            id="email"
            v-model="email"
            type="email"
            required
            autocomplete="email"
            class="form-input"
          />
        </div>

        <div v-if="isRegister" class="flex flex-col gap-1.5">
          <label for="displayName" class="text-sm font-medium text-muted">{{
            t('auth.displayName')
          }}</label>
          <input
            id="displayName"
            v-model="displayName"
            type="text"
            required
            autocomplete="name"
            class="form-input"
          />
        </div>

        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-sm font-medium text-muted">{{
            t('auth.password')
          }}</label>
          <input
            id="password"
            v-model="password"
            type="password"
            required
            minlength="8"
            autocomplete="current-password"
            class="form-input"
          />
        </div>

        <button type="submit" class="btn btn-primary w-full justify-center mt-2" :disabled="auth.loading">
          {{
            auth.loading
              ? t('common.loading')
              : isRegister
                ? t('auth.registerButton')
                : t('auth.loginButton')
          }}
        </button>
      </form>

      <p class="mt-5 text-center text-sm text-muted">
        {{ isRegister ? t('auth.haveAccount') : t('auth.noAccount') }}
        <button
          class="bg-transparent border-0 text-brand cursor-pointer text-sm underline"
          @click="isRegister = !isRegister"
        >
          {{ isRegister ? t('auth.loginButton') : t('auth.registerButton') }}
        </button>
      </p>
    </div>
  </div>
</template>

