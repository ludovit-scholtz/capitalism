<script setup lang="ts">
import { onMounted } from 'vue'
import AppHeader from '@/components/layout/AppHeader.vue'
import { useAuthStore } from '@/stores/auth'
import { useThemeStore } from '@/stores/theme'

const auth = useAuthStore()
const themeStore = useThemeStore()

themeStore.init()

// Restore auth immediately so views and header can render correct nav state.
auth.initFromStorage()

onMounted(() => {
  if (auth.isAuthenticated) {
    void auth.fetchProfile()
    void auth.fetchSubscription()
  }
})
</script>

<template>
  <div class="flex min-h-screen flex-col bg-page text-body">
    <AppHeader />
    <main class="flex-1">
      <RouterView />
    </main>
  </div>
</template>
