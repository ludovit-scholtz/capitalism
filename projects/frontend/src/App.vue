<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
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
import { useEndgameStatus } from '@/composables/useEndgameStatus'

const { t } = useI18n()
const { isOffline, updateAvailable, acceptUpdate } = usePwa()
const auth = useAuthStore()
const gameStateStore = useGameStateStore()
const newsStore = usesStore()
const notificationsStore = useNotificationsStore()
const gameAdminStore = useGameAdminStore()
const chatStore = useChatStore()
const referralStore = useReferralStore()
const { status: endgameStatus } = useEndgameStatus()
const endgameOverlayDismissed = ref(false)
const signedOutNoticeVisible = ref(false)
const SIGNED_OUT_TOAST_DURATION_MS = 4_000
gameStateStore.start()
let citySwitchToastTimer: ReturnType<typeof setTimeout> | null = null
let signedOutToastTimer: ReturnType<typeof setTimeout> | null = null

function clearCitySwitchToastTimer() {
  if (citySwitchToastTimer) {
    clearTimeout(citySwitchToastTimer)
    citySwitchToastTimer = null
  }
}

function clearSignedOutToastTimer() {
  if (signedOutToastTimer) {
    clearTimeout(signedOutToastTimer)
    signedOutToastTimer = null
  }
}

function showSignedOutToastIfPending() {
  if (typeof sessionStorage === 'undefined' || sessionStorage.getItem('auth_signed_out_notice') !== '1') {
    return
  }

  signedOutNoticeVisible.value = true
  sessionStorage.removeItem('auth_signed_out_notice')
  clearSignedOutToastTimer()
  signedOutToastTimer = setTimeout(() => {
    signedOutNoticeVisible.value = false
  }, SIGNED_OUT_TOAST_DURATION_MS)
}

onMounted(() => {
  auth.initFromStorage()
  // Capture referral code from URL before potentially redirecting
  referralStore.initFromUrl()
  referralStore.initFromStorage()
  void auth
    .fetchMe({ reconcileCityContext: true })
    .then(() => {
      if (!auth.isAuthenticated) {
        return
      }
      void newsStore.fetchUnreadCount()
      void notificationsStore.fetchUnreadCount()
      void gameAdminStore.fetchSession()
    })
    .catch(() => undefined)

  showSignedOutToastIfPending()
})

onUnmounted(() => {
  clearCitySwitchToastTimer()
  clearSignedOutToastTimer()
})

watch(
  () => auth.token,
  (token, previousToken) => {
    if (!token) {
      newsStore.clear()
      notificationsStore.clear()
      gameAdminStore.clear()
      showSignedOutToastIfPending()
      return
    }

    if (token !== previousToken) {
      void newsStore.fetchUnreadCount()
      void notificationsStore.fetchUnreadCount()
      void gameAdminStore.fetchSession()
    }
  },
)

watch(
  () => auth.player?.appliedReferralCode,
  (appliedReferralCode) => {
    if (appliedReferralCode && referralStore.pendingCode) {
      referralStore.clearPendingCode()
    }
  },
)

watch(
  () => endgameStatus.value?.gameEnded,
  (gameEnded) => {
    if (!gameEnded) {
      endgameOverlayDismissed.value = false
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
      v-if="endgameStatus?.gameEnded"
      class="sticky top-0 z-[210] flex items-center justify-center gap-2 border-b border-brand bg-brand-subtle px-4 py-2 text-sm font-semibold text-brand"
      role="status"
      aria-live="polite"
    >
      <span aria-hidden="true">🏆</span>
      <span>{{ t('endgame.readOnlyBanner', { winner: endgameStatus?.winnerDisplayName ?? t('endgame.unknownWinner') }) }}</span>
    </div>

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

    <div
      v-if="signedOutNoticeVisible"
      class="signed-out-toast fixed right-4 top-20 z-[220] flex max-w-sm items-start gap-3 rounded-xl border border-brand/35 bg-card-raised px-4 py-3 shadow-2xl"
      role="status"
      aria-live="polite"
    >
      <span aria-hidden="true" class="pt-0.5 text-brand">✅</span>
      <div class="min-w-0 flex-1 text-sm text-body">
        {{ t('auth.signedOut') }}
      </div>
      <button class="btn btn-ghost btn-sm shrink-0" :aria-label="t('common.close')" @click="signedOutNoticeVisible = false">
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
    <div
      v-if="endgameStatus?.gameEnded && !endgameOverlayDismissed"
      class="fixed inset-0 z-[230] flex items-center justify-center bg-[rgba(5,10,22,0.86)] p-6"
    >
      <div class="max-w-xl rounded-2xl border border-brand bg-card px-6 py-7 text-center shadow-2xl">
        <p class="text-sm font-bold uppercase tracking-[0.08em] text-brand">{{ t('endgame.overlayEyebrow') }}</p>
        <h2 class="mt-2 text-3xl font-extrabold">{{ t('endgame.overlayTitle') }}</h2>
        <p class="mt-3 text-base text-muted">
          {{ t('endgame.overlayWinner', { winner: endgameStatus?.winnerDisplayName ?? t('endgame.unknownWinner') }) }}
        </p>
        <p v-if="endgameStatus?.winnerCompanyName" class="mt-1 text-sm text-muted">
          {{ t('endgame.overlayCompany', { company: endgameStatus.winnerCompanyName }) }}
        </p>
        <div class="mt-5 flex justify-center gap-3">
          <RouterLink to="/leaderboard" class="btn btn-primary">{{ t('endgame.viewFinalRankings') }}</RouterLink>
          <button class="btn btn-secondary" @click="endgameOverlayDismissed = true">{{ t('common.close') }}</button>
        </div>
      </div>
    </div>
    <AppFooter />
    <!-- Global chat side panel — rendered via Teleport to body inside the component -->
    <ChatSidePanel v-if="auth.isAuthenticated" />
  </div>
</template>
