<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const loading = ref(true)
const error = ref<string | null>(null)
const isProviderError = ref(false)

function detectOidcProviderError(): string | null {
  if (typeof window === 'undefined') return null
  const url = new URL(window.location.href)
  const queryError = url.searchParams.get('error')
  const hashParams = new URLSearchParams(
    window.location.hash.startsWith('#') ? window.location.hash.slice(1) : '',
  )
  const hashError = hashParams.get('error')
  const oidcError = queryError || hashError
  if (!oidcError) return null
  const description =
    url.searchParams.get('error_description') ||
    hashParams.get('error_description') ||
    `OIDC login failed: ${oidcError}`
  return description
}

onMounted(async () => {
  // Check for provider-side OIDC errors before attempting sign-in.
  // These errors (e.g. invalid_client, access_denied) will not be resolved
  // by retrying — stop here and let the user see the error.
  const providerError = detectOidcProviderError()
  if (providerError) {
    isProviderError.value = true
    error.value = providerError
    loading.value = false
    return
  }

  try {
    const redirectPath = await auth.completeBiatecOidcSignIn()
    await router.replace(redirectPath || '/')
  } catch (err: unknown) {
    const didReset = auth.resetBiatecSessionForRetry('drive_access')
    if (!didReset) {
      error.value = err instanceof Error ? err.message : t('login.biatecCallbackError')
    }
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <main class="container py-16">
    <section class="mx-auto max-w-xl rounded-2xl border border-divider bg-card p-8 text-center shadow-lg">
      <h1 class="text-2xl font-bold text-body">{{ t('login.biatecCallbackTitle') }}</h1>
      <p v-if="loading" class="mt-4 text-sm text-muted">{{ t('login.biatecCallbackLoading') }}</p>
      <template v-else-if="error">
        <p class="mt-4 text-sm text-bad" role="alert">{{ error }}</p>
        <RouterLink
          v-if="isProviderError"
          to="/login"
          class="mt-6 inline-block rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white hover:bg-primary-dark"
        >{{ t('login.backToLogin') }}</RouterLink>
      </template>
      <p v-else class="mt-4 text-sm text-good">{{ t('login.biatecCallbackSuccess') }}</p>
    </section>
  </main>
</template>
