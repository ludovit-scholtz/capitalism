<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { computed, ref } from 'vue'
import { usesStore } from '@/stores/news'
import { useNotificationsStore } from '@/stores/notifications'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { useChatStore } from '@/stores/chat'
import ContextSwitcher from '@/components/layout/ContextSwitcher.vue'
import GameTimeChip from '@/components/layout/GameTimeChip.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import { useThemeStore } from '@/stores/theme'

const themeStore = useThemeStore()
themeStore.init()

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const newsStore = usesStore()
const notificationsStore = useNotificationsStore()
const gameAdminStore = useGameAdminStore()
const chatStore = useChatStore()
const { unreadCount } = storeToRefs(newsStore)
const { inbox: notificationsInbox, unreadCount: notificationUnreadCount, loading: notificationsLoading } = storeToRefs(notificationsStore)
const { session } = storeToRefs(gameAdminStore)
const { isChatOpen, unreadCount: chatUnreadCount } = storeToRefs(chatStore)
const isMenuOpen = ref(false)
const isNotificationsOpen = ref(false)

const showUnreadBadge = computed(() => auth.isAuthenticated && unreadCount.value > 0)
const showNotificationBadge = computed(() => auth.isAuthenticated && notificationUnreadCount.value > 0)

const impersonationLabel = computed(() => {
  if (!session.value?.isImpersonating || !session.value.effectivePlayer) {
    return null
  }

  return t('admin.impersonationBanner', {
    player: session.value.effectivePlayer.displayName,
    account: session.value.effectiveCompanyName ?? session.value.effectivePlayer.displayName,
  })
})

const toggleMenu = () => {
  isMenuOpen.value = !isMenuOpen.value
}

const closeMenu = () => {
  isMenuOpen.value = false
}

function handleChatToggle() {
  closeMenu()
  chatStore.toggleChat()
}

async function toggleNotificationsPanel() {
  isNotificationsOpen.value = !isNotificationsOpen.value
  if (isNotificationsOpen.value) {
    await notificationsStore.fetchInbox(20)
  }
}

function closeNotificationsPanel() {
  isNotificationsOpen.value = false
}

async function handleNotificationClick(notificationId: string, isRead: boolean, buildingId: string | null, type: string) {
  if (!isRead) {
    await notificationsStore.markRead([notificationId])
  }

  if (buildingId) {
    await router.push(`/building/${buildingId}`)
  } else if (type === 'LOAN_REPAYMENT_DUE_SOON') {
    await router.push('/banking')
  } else if (type === 'BANK_ACCOUNT_LOW_BALANCE') {
    await router.push('/bank-statement')
  } else {
    await router.push('/dashboard')
  }

  closeNotificationsPanel()
}

async function markAllNotificationsRead() {
  await notificationsStore.markAllRead()
}
</script>

<template>
  <header class="app-header bg-card border-b border-divider sticky top-0 z-[100] backdrop-blur-sm">
    <div class="container flex items-center gap-8 h-16">
      <!-- Logo -->
      <RouterLink to="/" class="logo-link shrink-0" @click="closeMenu">
        <span class="logo-text">CAPITALISM V</span>
      </RouterLink>

      <!-- Mobile menu toggle -->
      <button class="menu-toggle ml-auto md:hidden text-muted hover:text-body p-2 rounded-md transition-colors" @click="toggleMenu" :aria-expanded="isMenuOpen" aria-label="Toggle navigation menu">
        <font-awesome-icon :icon="['fas', 'bars']" />
      </button>

      <!-- Navigation links -->
      <nav class="nav-links" :class="{ 'nav-open': isMenuOpen }">
        <RouterLink to="/" :title="t('nav.home')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'home']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.home') }}</span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/dashboard" :title="t('nav.dashboard')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'tachometer-alt']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.dashboard') }}</span>
        </RouterLink>
        <RouterLink to="/leaderboard" :title="t('nav.leaderboard')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'trophy']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.leaderboard') }}</span>
        </RouterLink>
        <RouterLink to="/cities" :title="t('nav.cities')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'globe']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.cities') }}</span>
        </RouterLink>
        <RouterLink to="/buildings/market" :title="t('nav.buildingMarket')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'store']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.buildingMarket') }}</span>
        </RouterLink>
        <RouterLink to="/encyclopedia" :title="t('nav.encyclopedia')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'book']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.encyclopedia') }}</span>
        </RouterLink>
        <RouterLink to="/exchange" :title="t('nav.exchange')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'chart-bar']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.exchange') }}</span>
        </RouterLink>
        <RouterLink to="/stocks" :title="t('nav.stocks')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'wallet']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.stocks') }}</span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/forex" :title="t('nav.forex')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'coins']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.forex') }}</span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/bank-statement" :title="t('nav.bankStatement')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'file-invoice-dollar']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.bankStatement') }}</span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/market-intelligence" :title="t('nav.campaignAnalytics')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'bullhorn']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.campaignAnalytics') }}</span>
        </RouterLink>
        <RouterLink to="/banking" :title="t('nav.banking')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'landmark']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.banking') }}</span>
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/trade-routes" :title="t('tradeRoutes.nav')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'route']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('tradeRoutes.nav') }}</span>
        </RouterLink>
        <RouterLink to="/news" :title="t('nav.news')" :aria-label="t('nav.news')" class="nav-link nav-link-badge-host" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'newspaper']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.news') }}</span>
          <span v-if="showUnreadBadge" class="nav-badge nav-badge-news news-badge">{{ unreadCount }}</span>
        </RouterLink>
        <button
          v-if="auth.isAuthenticated"
          class="nav-link nav-chat-btn nav-link-badge-host"
          :class="{ 'nav-link-active': isChatOpen }"
          :title="t('nav.chat')"
          :aria-label="t('nav.chat')"
          :aria-pressed="isChatOpen"
          @click="handleChatToggle"
        >
          <font-awesome-icon :icon="['fas', 'comments']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.chat') }}</span>
          <span v-if="chatUnreadCount > 0" class="nav-badge nav-badge-chat chat-badge">{{ chatUnreadCount }}</span>
        </button>
        <RouterLink v-if="session?.canAccessAdminDashboard" to="/admin" :title="t('nav.admin')" :aria-label="t('nav.admin')" class="nav-link" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'shield-halved']" class="mr-2" />
          <span class="inline-block md:hidden">{{ t('nav.admin') }}</span>
        </RouterLink>
      </nav>

      <!-- Right-side actions -->
      <div class="header-actions flex items-center gap-3 shrink-0">
        <GameTimeChip />

        <!-- Impersonation chip -->
        <div
          v-if="impersonationLabel"
          class="impersonation-chip hidden sm:block max-w-[17rem] px-3 py-1.5 rounded-full border border-[rgba(255,138,0,0.5)] bg-[rgba(255,138,0,0.14)] text-[#ffd7a3] text-[0.72rem] leading-tight"
        >
          {{ impersonationLabel }}
        </div>

        <template v-if="auth.isAuthenticated">
          <button
            class="btn btn-secondary h-9 w-9 p-0 justify-center relative notification-bell-btn"
            :title="t('notifications.title')"
            :aria-label="t('notifications.title')"
            :aria-expanded="isNotificationsOpen"
            @click="toggleNotificationsPanel"
          >
            <font-awesome-icon :icon="['fas', 'bell']" />
            <span v-if="showNotificationBadge" class="notification-badge">{{ notificationUnreadCount }}</span>
          </button>
          <ContextSwitcher @switched="closeMenu" />
          <button
            class="btn btn-secondary h-9 w-9 p-0 justify-center"
            @click="
              () => {
                auth.logout({ federated: true })
                closeMenu()
              }
            "
            :title="t('common.logout')"
          >
            <font-awesome-icon :icon="['fas', 'sign-out-alt']" />
          </button>
        </template>
        <RouterLink v-else to="/login" class="btn btn-primary" :title="t('common.login')" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'sign-in-alt']" />
        </RouterLink>
        <ThemeToggle />
      </div>
    </div>

    <div v-if="isNotificationsOpen" class="notification-overlay" @click="closeNotificationsPanel"></div>
    <aside v-if="isNotificationsOpen" class="notification-panel" aria-live="polite">
      <header class="notification-panel-header">
        <div>
          <h3>{{ t('notifications.title') }}</h3>
          <p>{{ t('notifications.subtitle') }}</p>
        </div>
        <button class="btn btn-ghost btn-sm" :disabled="notificationUnreadCount === 0" @click="markAllNotificationsRead">
          {{ t('notifications.markAllRead') }}
        </button>
      </header>

      <div v-if="notificationsLoading" class="notification-panel-state">{{ t('common.loading') }}</div>
      <div v-else-if="!notificationsInbox || notificationsInbox.items.length === 0" class="notification-panel-state">
        {{ t('notifications.empty') }}
      </div>
      <ul v-else class="notification-list">
        <li v-for="item in notificationsInbox.items" :key="item.id" class="notification-item" :class="{ 'notification-item-unread': !item.isRead }">
          <button class="notification-item-btn" @click="handleNotificationClick(item.id, item.isRead, item.buildingId, item.type)">
            <span class="notification-item-title">{{ item.title }}</span>
            <span class="notification-item-message">{{ item.message }}</span>
            <span class="notification-item-time">{{ new Date(item.createdAtUtc).toLocaleString() }}</span>
          </button>
        </li>
      </ul>
    </aside>
  </header>
</template>

<style scoped>
/* ── Logo ──────────────────────────────────────────────────────────────────── */
.logo-link {
  display: flex;
  align-items: center;
  text-decoration: none;
}

.logo-text {
  font-size: 1.25rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  background: linear-gradient(135deg, gold, orange);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  border-top: 1px solid gold;
  border-bottom: 1px solid gold;
  white-space: nowrap;
  font-family:
    system-ui,
    -apple-system,
    sans-serif;
}

/* ── Navigation links ─────────────────────────────────────────────────────── */
.nav-links {
  display: flex;
  gap: 1.5rem;
  flex: 1;
}

.nav-link {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-secondary);
  text-decoration: none;
  transition: color 0.15s;
  position: relative;
  padding-bottom: 2px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: none;
  border: none;
  cursor: pointer;
  padding-inline: 0;
}

.nav-link svg {
  font-size: 1.25rem;
}

/* Underline indicator — slides in on hover / active */
.nav-link::after {
  content: '';
  position: absolute;
  bottom: -2px;
  left: 0;
  right: 0;
  height: 2px;
  background: var(--color-primary);
  border-radius: 1px;
  transform: scaleX(0);
  transition: transform 0.15s;
}

.nav-link:hover,
.nav-link.router-link-active,
.nav-link.nav-link-active {
  color: var(--color-text);
  text-decoration: none;
}

/* ── Notification badges ──────────────────────────────────────────────────── */
.nav-link-badge-host {
  position: relative;
}

.nav-badge {
  position: absolute;
  top: -0.45rem;
  right: -0.55rem;
  min-width: 1.2rem;
  height: 1.2rem;
  border-radius: 999px;
  color: white;
  font-size: 0.65rem;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0 0.3rem;
}

.nav-badge-news {
  background: linear-gradient(135deg, #ff8a00, #ff3d00);
  box-shadow: 0 4px 12px rgba(255, 97, 0, 0.35);
}

.nav-badge-chat {
  background: linear-gradient(135deg, #2196f3, #1565c0);
  box-shadow: 0 4px 12px rgba(33, 150, 243, 0.4);
}

.notification-bell-btn {
  z-index: 130;
}

.notification-badge {
  position: absolute;
  top: -0.38rem;
  right: -0.4rem;
  min-width: 1.1rem;
  height: 1.1rem;
  border-radius: 999px;
  color: #fff;
  background: linear-gradient(135deg, #f43f5e, #e11d48);
  font-size: 0.62rem;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0 0.28rem;
  box-shadow: 0 4px 10px rgba(244, 63, 94, 0.35);
}

.notification-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.35);
  z-index: 119;
}

.notification-panel {
  position: fixed;
  top: 4.4rem;
  right: 1rem;
  width: min(30rem, calc(100vw - 2rem));
  max-height: calc(100vh - 6rem);
  overflow: auto;
  border: 1px solid var(--color-divider);
  border-radius: 0.9rem;
  background: var(--color-card);
  box-shadow: 0 22px 50px rgba(15, 23, 42, 0.35);
  z-index: 120;
  padding: 0.9rem;
}

.notification-panel-header {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  align-items: flex-start;
  margin-bottom: 0.75rem;
}

.notification-panel-header h3 {
  font-size: 0.95rem;
  font-weight: 700;
  margin: 0;
}

.notification-panel-header p {
  margin: 0.2rem 0 0;
  color: var(--color-text-secondary);
  font-size: 0.78rem;
}

.notification-panel-state {
  border: 1px dashed var(--color-divider);
  border-radius: 0.75rem;
  padding: 1rem;
  color: var(--color-text-secondary);
  text-align: center;
}

.notification-list {
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
}

.notification-item {
  border: 1px solid var(--color-divider);
  border-radius: 0.75rem;
  overflow: hidden;
}

.notification-item-unread {
  border-color: color-mix(in srgb, var(--color-primary) 40%, var(--color-divider));
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
}

.notification-item-btn {
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  padding: 0.7rem 0.8rem;
  display: grid;
  gap: 0.18rem;
}

.notification-item-title {
  font-size: 0.86rem;
  font-weight: 700;
  color: var(--color-text);
}

.notification-item-message {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  line-height: 1.35;
}

.notification-item-time {
  font-size: 0.7rem;
  color: var(--color-text-muted);
}

/* ── Mobile hamburger ─────────────────────────────────────────────────────── */
.menu-toggle {
  display: none;
  background: none;
  border: none;
  font-size: 1.25rem;
}

@media (max-width: 768px) {
  .menu-toggle {
    display: block;
    order: 2;
  }

  .nav-links {
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    background: var(--color-surface);
    border-bottom: 1px solid var(--color-border);
    flex-direction: column;
    gap: 0;
    padding: 1rem 0;
    transform: translateY(-100%);
    opacity: 0;
    visibility: hidden;
    transition: all 0.3s ease;
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
    max-height: calc(100vh - 64px);
    overflow-y: auto;
  }

  .nav-links.nav-open {
    transform: translateY(0);
    opacity: 1;
    visibility: visible;
  }

  .nav-link {
    padding: 1rem 2rem;
    justify-content: flex-start;
    gap: 0.75rem;
    font-size: 1rem;
    width: 100%;
  }

  .nav-link svg {
    font-size: 1.5rem;
  }

  .nav-link::after {
    display: none;
  }

  .header-actions {
    order: 3;
  }
}

@media (max-width: 640px) {
  .header-actions {
    gap: 0.5rem;
  }
}
</style>
