<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useReferralStore } from '@/stores/referral'
import {
  generatePersonalAccountName,
  resetPersonalNameSession,
} from '@/lib/personalAccountName'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const referralStore = useReferralStore()

const isRegister = ref(false)
const email = ref('')
const displayName = ref('')
const password = ref('')
const formError = ref<string | null>(null)
const requiresConsentRetry = computed(() => route.query.oidc_retry === 'consent')
const showsDriveAccessHint = computed(
  () => requiresConsentRetry.value && route.query.oidc_reason === 'drive_access',
)

const hasReferralCode = computed(() => !auth.isAuthenticated && !auth.player && !!referralStore.pendingCode)

// Auto-fill display name when switching to registration mode.
watch(isRegister, (nowRegister) => {
  if (nowRegister && !displayName.value) {
    displayName.value = generatePersonalAccountName()
  }
  if (!nowRegister) {
    resetPersonalNameSession()
  }
})

function handleGenerateName() {
  displayName.value = generatePersonalAccountName()
}

async function handleSubmit() {
  formError.value = null
  try {
    if (isRegister.value) {
      await auth.register(email.value, displayName.value, password.value, referralStore.pendingCode)
      referralStore.clearPendingCode()
    } else {
      await auth.login(email.value, password.value)
    }
    router.push('/')
  } catch (e: unknown) {
    formError.value = e instanceof Error ? e.message : 'An error occurred'
  }
}

function handleBiatecSignIn() {
  formError.value = null
  const redirectPath = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
  auth.startBiatecOidcSignIn(redirectPath, requiresConsentRetry.value ? { prompt: 'consent' } : undefined)
}
</script>

<template>
  <main class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
    <section class="mx-auto flex min-h-[calc(100vh-64px-5rem)] max-w-md items-center justify-center py-4 lg:py-6">
      <div class="flex w-full flex-col gap-6 rounded-2xl border border-divider bg-card p-6 shadow-lg sm:p-8 lg:p-10">
        <h1 class="text-2xl font-bold text-body">
          {{ isRegister ? t('auth.registerTitle') : t('auth.loginTitle') }}
        </h1>

        <!-- Referral code banner -->
        <div
          v-if="hasReferralCode"
          class="referral-banner flex items-start gap-3 rounded-md border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm"
          role="status"
          aria-live="polite"
        >
          <span aria-hidden="true" class="mt-0.5 shrink-0 text-base">🎁</span>
          <div>
            <p class="font-semibold text-amber-400">{{ t('auth.referralBannerTitle') }}</p>
            <p class="mt-0.5 text-amber-200/80">
              {{ t('auth.referralBannerCode') }}:
              <span class="font-mono font-bold text-amber-300">{{ referralStore.pendingCode }}</span>
            </p>
            <p class="mt-1 text-amber-200/70">{{ t('auth.referralBannerDiscount') }}</p>
          </div>
        </div>

        <form class="flex flex-col gap-5" @submit.prevent="handleSubmit">
          <div
            v-if="showsDriveAccessHint"
            class="rounded-md border border-brand/25 bg-brand/10 px-3 py-3 text-sm text-body"
            role="status"
          >
            {{ t('auth.oidcRetryDriveAccessHint') }}
          </div>

          <div v-if="formError" class="bg-bad/10 text-bad rounded-md px-3 py-3 text-sm" role="alert">
            {{ formError }}
          </div>

          <div class="flex flex-col gap-1.5">
            <label for="email" class="text-sm font-medium text-muted">{{ t('auth.email') }}</label>
            <input id="email" v-model="email" type="email" required autocomplete="email" class="form-input" />
          </div>

          <div v-if="isRegister" class="flex flex-col gap-2">
            <label for="displayName" class="text-sm font-medium text-muted">
              {{ t('auth.displayNameGenerated') }}
            </label>
            <div class="flex gap-2">
              <input
                id="displayName"
                v-model="displayName"
                type="text"
                required
                autocomplete="off"
                class="form-input flex-1"
                :placeholder="t('auth.displayNamePlaceholder')"
              />
              <button
                type="button"
                class="btn btn-secondary shrink-0 px-3 text-xs"
                :title="t('auth.displayNameGenerateAnother')"
                @click="handleGenerateName"
              >
                🎲
              </button>
            </div>
            <p class="personal-name-warning text-xs text-amber-400">
              {{ t('auth.displayNameRealNameWarning') }}
            </p>
            <p class="text-xs text-muted">{{ t('auth.displayNameHint') }}</p>
          </div>

          <div class="flex flex-col gap-1.5">
            <label for="password" class="text-sm font-medium text-muted">{{ t('auth.password') }}</label>
            <input id="password" v-model="password" type="password" required minlength="8" autocomplete="current-password" class="form-input" />
          </div>

          <button type="submit" class="btn btn-primary w-full justify-center" :disabled="auth.loading">
            {{ auth.loading ? t('common.loading') : isRegister ? t('auth.registerButton') : t('auth.loginButton') }}
          </button>

          <button type="button" class="btn btn-secondary w-full justify-center" :disabled="auth.loading" @click="handleBiatecSignIn">
            <svg class="mr-2 h-4 w-4" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
              <path
                fill="#EA4335"
                d="M12 10.2v3.9h5.5c-.2 1.3-1.5 3.9-5.5 3.9-3.3 0-6-2.7-6-6s2.7-6 6-6c1.9 0 3.2.8 3.9 1.5l2.7-2.6C16.9 3.3 14.7 2.4 12 2.4 6.7 2.4 2.4 6.7 2.4 12S6.7 21.6 12 21.6c6.9 0 9.6-4.8 9.6-7.2 0-.5-.1-.9-.1-1.2H12z"
              />
              <path fill="#34A853" d="M2.4 7.7l3.2 2.3C6.5 8 9 6 12 6c1.9 0 3.2.8 3.9 1.5l2.7-2.6C16.9 3.3 14.7 2.4 12 2.4 8.1 2.4 4.7 4.6 2.4 7.7z" />
              <path fill="#FBBC05" d="M12 21.6c2.6 0 4.8-.9 6.5-2.5l-3-2.5c-.8.6-1.9 1.1-3.5 1.1-3.9 0-5.3-2.6-5.5-3.9l-3.2 2.5c2.2 3.2 5.7 5.3 8.7 5.3z" />
              <path fill="#4285F4" d="M21.6 12.4c0-.8-.1-1.3-.2-1.9H12v3.9h5.5c-.3 1.5-1.6 2.8-2.8 3.6l3 2.5c1.8-1.6 2.9-4 2.9-8.1z" />
            </svg>
            {{ t('auth.loginWithBiatec') }}
          </button>
        </form>

        <div class="text-center text-sm text-muted">
          {{ isRegister ? t('auth.haveAccount') : t('auth.noAccount') }}
          <button class="border-0 bg-transparent text-sm text-brand underline" @click="isRegister = !isRegister">
            {{ isRegister ? t('auth.loginButton') : t('auth.registerButton') }}
          </button>
        </div>
      </div>
    </section>
  </main>
</template>
