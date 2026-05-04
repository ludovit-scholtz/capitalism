<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const isRegister = ref(false)
const email = ref('')
const displayName = ref('')
const password = ref('')
const formError = ref<string | null>(null)
const navItems = [
  { label: t('nav.home'), to: '/' },
  { label: t('nav.gameServers'), to: '/game-servers' },
]

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

function handleBiatecSignIn() {
  const redirectPath = router.currentRoute.value.query.redirect
  const targetPath =
    typeof redirectPath === 'string' && redirectPath.length > 0 ? redirectPath : '/'
  auth.startBiatecOidcSignIn(targetPath)
}
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="isRegister ? t('login.createAccount') : t('login.signIn')"
      :title="isRegister ? t('login.createAccount') : t('login.signIn')"
      :subtitle="isRegister ? t('login.createSub') : t('login.signInSub')"
      variant="default"
    />
    <ViewSubnav :items="navItems" aria-label="Authentication navigation" />

    <section class="container pb-16 pt-2 lg:pb-20 lg:pt-2">
      <section
        class="mx-auto flex min-h-[calc(100vh-64px-5rem)] max-w-md items-center justify-center py-4 lg:py-6"
      >
        <div
          class="flex w-full flex-col gap-6 rounded-2xl border border-divider bg-card p-6 shadow-lg sm:p-8 lg:p-10"
        >
          <div class="flex flex-col gap-2">
            <h1 class="text-2xl font-bold text-body">
              {{ isRegister ? t('login.createAccount') : t('login.signIn') }}
            </h1>
            <p class="text-sm text-muted">
              {{ isRegister ? t('login.createSub') : t('login.signInSub') }}
            </p>
          </div>

          <form class="flex flex-col gap-5" @submit.prevent="handleSubmit">
            <div
              v-if="formError"
              class="rounded-md bg-bad/10 px-3 py-3 text-sm text-bad"
              role="alert"
            >
              {{ formError }}
            </div>

            <div class="flex flex-col gap-1.5">
              <label for="email" class="text-sm font-medium text-muted">{{
                t('login.email')
              }}</label>
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
                t('login.displayName')
              }}</label>
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
              <label for="password" class="text-sm font-medium text-muted">{{
                t('login.password')
              }}</label>
              <input
                id="password"
                v-model="password"
                type="password"
                required
                autocomplete="current-password"
                class="form-input"
              />
            </div>

            <button
              type="submit"
              class="btn btn-primary w-full justify-center gap-2"
              :disabled="auth.loading"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
                <path d="M10 17l5-5-5-5" />
                <path d="M15 12H3" />
              </svg>
              {{
                auth.loading
                  ? t('login.wait')
                  : isRegister
                    ? t('login.createAccount')
                    : t('login.signIn')
              }}
            </button>

            <button
              type="button"
              class="btn btn-ghost w-full justify-center gap-2"
              :disabled="auth.loading"
              @click="handleBiatecSignIn"
            >
              <svg class="h-4 w-4" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                <path
                  fill="#EA4335"
                  d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.7-6-6s2.7-6 6-6c1.9 0 3.2.8 3.9 1.5l2.7-2.6C16.9 3.3 14.7 2.4 12 2.4 6.7 2.4 2.4 6.7 2.4 12S6.7 21.6 12 21.6c6.9 0 9.6-4.8 9.6-7.2 0-.5-.1-.9-.1-1.2H12z"
                />
                <path fill="#34A853" d="M2.4 7.7l3.2 2.3C6.5 8 9 6 12 6c1.9 0 3.2.8 3.9 1.5l2.7-2.6C16.9 3.3 14.7 2.4 12 2.4 8.1 2.4 4.7 4.6 2.4 7.7z" />
                <path fill="#FBBC05" d="M12 21.6c2.6 0 4.8-.9 6.5-2.5l-3-2.5c-.8.6-1.9 1.1-3.5 1.1-3.9 0-5.3-2.6-5.5-3.9l-3.2 2.5c2.2 3.2 5.7 5.3 8.7 5.3z" />
                <path fill="#4285F4" d="M21.6 12.4c0-.8-.1-1.3-.2-1.9H12v3.9h5.5c-.3 1.5-1.6 2.8-2.8 3.6l3 2.5c1.8-1.6 2.9-4 2.9-8.1z" />
              </svg>
              {{ t('login.signInWithBiatec') }}
            </button>
          </form>

          <div class="text-center text-sm text-muted">
            {{ isRegister ? t('login.haveAccount') : t('login.noAccount') }}
            <button
              class="border-0 bg-transparent text-sm text-brand underline"
              type="button"
              @click="isRegister = !isRegister"
            >
              {{ isRegister ? t('login.signIn') : t('login.register') }}
            </button>
          </div>

          <div class="text-center text-sm text-muted">
            <RouterLink class="transition-colors hover:text-body" to="/"
              >← {{ t('login.backToDirectory') }}</RouterLink
            >
          </div>
        </div>
      </section>
    </section>
  </main>
</template>
