<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const callbackError = ref<string | null>(null)

onMounted(async () => {
  try {
    const redirectPath = await auth.completeBiatecOidcSignIn()
    await router.replace(redirectPath || '/')
  } catch (e: unknown) {
    const didReset = auth.resetBiatecSessionForRetry('drive_access')
    if (!didReset) {
      callbackError.value = e instanceof Error ? e.message : t('auth.oidcCallbackFailed')
    }
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
