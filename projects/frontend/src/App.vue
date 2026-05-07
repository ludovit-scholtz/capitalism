<script setup lang="ts">
import { onMounted, onUnmounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AppHeader from '@/components/layout/AppHeader.vue'
import AppFooter from '@/components/layout/AppFooter.vue'
import ChatSidePanel from '@/components/layout/ChatSidePanel.vue'
import { usePwa } from '@/composables/usePwa'
import { useAuthStore } from '@/stores/auth'
import { useGameStateStore } from '@/stores/gameState'
import { usesStore } from '@/stores/news'
import { useNotificationsStore } from '@/stores/notifications'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { useChatStore } from '@/stores/chat'
import { useReferralStore } from '@/stores/referral'

const { t } = useI18n()
const { isOffline, updateAvailable, acceptUpdate } = usePwa()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const newsStore = usesStore()
const notificationsStore = useNotificationsStore()
const gameAdminStore = useGameAdminStore()
const chatStore = useChatStore()
const referralStore = useReferralStore()
gameStateStore.start()
let citySwitchToastTimer: ReturnType<typeof setTimeout> | null = null

function clearCitySwitchToastTimer() {
  if (citySwitchToastTimer) {
    clearTimeout(citySwitchToastTimer)
    citySwitchToastTimer = null
  }
}

onMounted(() => {
  auth.initFromStorage()
  // Capture referral code from URL before potentially redirecting
  referralStore.initFromUrl()
  referralStore.initFromStorage()
  if (auth.token) {
    void auth.fetchMe({ reconcileCityContext: true })
    void newsStore.fetchUnreadCount()
    void notificationsStore.fetchUnreadCount()
    void gameAdminStore.fetchSession()
  }
})

onUnmounted(() => {
  clearCitySwitchToastTimer()
})

watch(
  () => auth.token,
  (token, previousToken) => {
    if (!token) {
      newsStore.clear()
      notificationsStore.clear()
      gameAdminStore.clear()
      return
    }

    if (token !== previousToken) {
      void newsStore.fetchUnreadCount()
      void notificationsStore.fetchUnreadCount()
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

watch(
  () => auth.autoSwitchedMainCityName,
  (cityName) => {
    clearCitySwitchToastTimer()
    if (!cityName) {
      return
    }

    citySwitchToastTimer = setTimeout(() => {
      auth.clearAutoSwitchedMainCityName()
    }, 4_000)
  },
)
</script>

<template>
  <div class="flex flex-col min-h-screen">
    <AppHeader />

    <div
      v-if="auth.autoSwitchedMainCityName"
      class="city-auto-switch-toast fixed right-4 top-20 z-[220] flex max-w-sm items-start gap-3 rounded-xl border border-brand/35 bg-card-raised px-4 py-3 shadow-2xl"
      role="status"
      aria-live="polite"
    >
      <span aria-hidden="true" class="pt-0.5 text-brand">📍</span>
      <div class="min-w-0 flex-1 text-sm text-body">
        {{ t('auth.autoSwitchedToMainCity', { city: auth.autoSwitchedMainCityName }) }}
      </div>
      <button class="btn btn-ghost btn-sm shrink-0" :aria-label="t('common.close')" @click="auth.clearAutoSwitchedMainCityName()">
        {{ t('common.close') }}
      </button>
    </div>

    <!-- Offline banner: shown when the browser loses connectivity -->
    <div v-if="isOffline" role="status" aria-live="polite" class="flex items-center gap-3 px-4 py-2.5 text-sm font-medium sticky top-0 z-90 bg-card-raised text-caution border-b border-divider">
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
      <button class="btn btn-primary shrink-0" style="padding: 0.35rem 0.9rem; font-size: 0.8rem" @click="acceptUpdate">
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
