<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const callbackError = ref<string | null>(null)

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
  return (
    url.searchParams.get('error_description') ||
    hashParams.get('error_description') ||
    `Sign-in failed: ${oidcError}`
  )
}

onMounted(async () => {
  // Detect provider-side OIDC errors (e.g. invalid_client, access_denied) before
  // attempting completeBiatecOidcSignIn. These cannot be resolved by retrying.
  const providerError = detectOidcProviderError()
  if (providerError) {
    callbackError.value = providerError
    return
  }

  try {
    const redirectPath = await auth.completeBiatecOidcSignIn()
    await router.replace(redirectPath || '/')
  } catch (e: unknown) {
    callbackError.value = e instanceof Error ? e.message : t('auth.oidcCallbackFailed')
  }
})
</script>

<template>
  <main class="container pb-16 pt-6 lg:pb-20 lg:pt-8">
    <section class="mx-auto flex min-h-[calc(100vh-64px-5rem)] max-w-md items-center justify-center py-4 lg:py-6">
      <div class="flex w-full flex-col gap-5 rounded-2xl border border-divider bg-card p-6 shadow-lg sm:p-8 lg:p-10">
        <h1 class="text-2xl font-bold text-body">{{ t('auth.oidcCallbackTitle') }}</h1>

        <p v-if="!callbackError" class="text-muted">
          {{ t('auth.oidcCallbackLoading') }}
        </p>

        <div v-else class="flex flex-col gap-4">
          <p class="rounded-md bg-bad/10 px-3 py-3 text-sm text-bad" role="alert">
            {{ callbackError }}
          </p>
          <button class="btn btn-primary w-full justify-center" @click="router.replace('/login')">
            {{ t('auth.loginButton') }}
          </button>
        </div>
      </div>
    </section>
  </main>
</template>
