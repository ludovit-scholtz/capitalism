<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

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

    await auth.fetchSubscription()
    await router.push('/')
  } catch (error: unknown) {
    formError.value = error instanceof Error ? error.message : t('login.genericError')
  }
}
</script>

<template>
  <main class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
    <section class="mx-auto flex min-h-[calc(100vh-64px-5rem)] max-w-md items-center justify-center py-4 lg:py-6">
      <div class="flex w-full flex-col gap-6 rounded-2xl border border-divider bg-card p-6 shadow-lg sm:p-8 lg:p-10">
        <div class="flex flex-col gap-2">
          <h1 class="text-2xl font-bold text-body">
            {{ isRegister ? t('login.createAccount') : t('login.signIn') }}
          </h1>
          <p class="text-sm text-muted">
            {{ isRegister ? t('login.createSub') : t('login.signInSub') }}
          </p>
        </div>

        <form class="flex flex-col gap-5" @submit.prevent="handleSubmit">
          <div v-if="formError" class="rounded-md bg-bad/10 px-3 py-3 text-sm text-bad" role="alert">
            {{ formError }}
          </div>

          <div class="flex flex-col gap-1.5">
            <label for="email" class="text-sm font-medium text-muted">{{ t('login.email') }}</label>
            <input id="email" v-model="email" type="email" required autocomplete="email" class="form-input" />
          </div>

          <div v-if="isRegister" class="flex flex-col gap-1.5">
            <label for="displayName" class="text-sm font-medium text-muted">{{ t('login.displayName') }}</label>
            <input
              id="displayName"
              v-model="displayName"
              type="text"
              required
              autocomplete="name"
              :placeholder="t('login.displayNamePlaceholder')"
              class="form-input"
            />
          </div>

          <div class="flex flex-col gap-1.5">
            <label for="password" class="text-sm font-medium text-muted">{{ t('login.password') }}</label>
            <input id="password" v-model="password" type="password" required autocomplete="current-password" class="form-input" />
          </div>

          <button type="submit" class="btn btn-primary w-full justify-center" :disabled="auth.loading">
            {{ auth.loading ? t('login.wait') : isRegister ? t('login.createAccount') : t('login.signIn') }}
          </button>
        </form>

        <div class="text-center text-sm text-muted">
          {{ isRegister ? t('login.haveAccount') : t('login.noAccount') }}
          <button class="border-0 bg-transparent text-sm text-brand underline" type="button" @click="isRegister = !isRegister">
            {{ isRegister ? t('login.signIn') : t('login.register') }}
          </button>
        </div>

        <div class="text-center text-sm text-muted">
          <RouterLink class="transition-colors hover:text-body" to="/">← {{ t('login.backToDirectory') }}</RouterLink>
        </div>
      </div>
    </section>
  </main>
</template>
