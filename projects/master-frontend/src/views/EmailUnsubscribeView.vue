<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { unsubscribeFromWeeklyReportEmail } from '@/lib/masterApi'

const { t } = useI18n()
const route = useRoute()

const loading = ref(true)
const success = ref(false)
const error = ref<string | null>(null)

onMounted(async () => {
  const rawToken = route.query.token
  const token = Array.isArray(rawToken) ? rawToken[0] : rawToken
  if (!token) {
    loading.value = false
    error.value = t('emailUnsubscribe.missingToken')
    return
  }

  try {
    await unsubscribeFromWeeklyReportEmail(token)
    success.value = true
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : t('emailUnsubscribe.error')
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <main class="container py-16">
    <section
      class="mx-auto max-w-xl rounded-2xl border border-divider bg-card p-8 text-center shadow-lg"
    >
      <h1 class="text-2xl font-bold text-body">{{ t('emailUnsubscribe.title') }}</h1>
      <p v-if="loading" class="mt-4 text-sm text-muted">{{ t('emailUnsubscribe.loading') }}</p>
      <template v-else-if="error">
        <p class="mt-4 text-sm text-bad" role="alert">{{ error }}</p>
      </template>
      <template v-else-if="success">
        <p class="mt-4 text-sm text-good">{{ t('emailUnsubscribe.success') }}</p>
        <p class="mt-2 text-sm text-muted">{{ t('emailUnsubscribe.successHint') }}</p>
        <RouterLink
          to="/account"
          class="mt-6 inline-block rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white hover:bg-primary-dark"
          >{{ t('emailUnsubscribe.manageLink') }}</RouterLink
        >
      </template>
    </section>
  </main>
</template>
