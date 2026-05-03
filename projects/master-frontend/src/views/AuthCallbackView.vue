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

onMounted(async () => {
  try {
    const redirectPath = await auth.completeBiatecOidcSignIn()
    await router.replace(redirectPath || '/')
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : t('login.biatecCallbackError')
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
      <p v-else-if="error" class="mt-4 text-sm text-bad" role="alert">{{ error }}</p>
      <p v-else class="mt-4 text-sm text-good">{{ t('login.biatecCallbackSuccess') }}</p>
    </section>
  </main>
</template>
