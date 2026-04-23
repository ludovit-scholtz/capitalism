<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppHeader from '@/components/layout/AppHeader.vue'
import AppFooter from '@/components/layout/AppFooter.vue'
import ChatSidePanel from '@/components/layout/ChatSidePanel.vue'
import { usePwa } from '@/composables/usePwa'
import { useAuthStore } from '@/stores/auth'
import { useGameStateStore } from '@/stores/gameState'
import { useNewsStore } from '@/stores/news'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { useChatStore } from '@/stores/chat'

const { t } = useI18n()
const { isOffline, updateAvailable, acceptUpdate } = usePwa()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const newsStore = useNewsStore()
const gameAdminStore = useGameAdminStore()
const chatStore = useChatStore()
gameStateStore.start()

onMounted(() => {
  auth.initFromStorage()
  if (auth.token) {
    void auth.fetchMe()
    void newsStore.fetchUnreadCount()
    void gameAdminStore.fetchSession()
  }
})

watch(
  () => auth.token,
  (token, previousToken) => {
    if (!token) {
      newsStore.clear()
      gameAdminStore.clear()
      return
    }

    if (token !== previousToken) {
      void newsStore.fetchUnreadCount()
      void gameAdminStore.fetchSession()
    }
  },
)

// Close chat when user logs out
watch(
  () => auth.isAuthenticated,
  (isAuthenticated) => {
    if (!isAuthenticated) {
      chatStore.clear()
    }
  },
)
</script>

<template>
  <div class="flex flex-col min-h-screen">
    <AppHeader />

    <!-- Offline banner: shown when the browser loses connectivity -->
    <div
      v-if="isOffline"
      role="status"
      aria-live="polite"
      class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium sticky top-0 z-90 bg-card-raised text-caution border-b border-divider"
    >
      <span aria-hidden="true" class="text-base">📡</span>
      {{ t('banners.offline') }}
    </div>

    <!-- Update prompt: shown when a new service-worker version is waiting -->
    <div
      v-if="updateAvailable"
      role="status"
      aria-live="polite"
      class="flex items-center justify-between gap-3 px-4 py-2.5 text-sm font-medium sticky top-0 z-90 bg-brand-subtle text-brand border-b border-brand"
    >
      <span>{{ t('banners.updateAvailable') }}</span>
      <button
        class="btn btn-primary shrink-0"
        style="padding: 0.35rem 0.9rem; font-size: 0.8rem"
        @click="acceptUpdate"
      >
        {{ t('banners.refreshToUpdate') }}
      </button>
    </div>

    <main class="flex-1">
      <RouterView />
    </main>
    <AppFooter />
    <!-- Global chat side panel — rendered via Teleport to body inside the component -->
    <ChatSidePanel v-if="auth.isAuthenticated" />
  </div>
</template>
