<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { usesStore } from '@/stores/news'
import { useNotificationsStore } from '@/stores/notifications'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { useChatStore } from '@/stores/chat'
import { gqlRequest } from '@/lib/graphql'
import { resolveNotificationCopy } from '@/lib/notificationText'
import type { PlayerNotificationItem } from '@/types'
import ContextSwitcher from '@/components/layout/ContextSwitcher.vue'
import GameTimeChip from '@/components/layout/GameTimeChip.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import GlobalEventBanner from '@/components/market/GlobalEventBanner.vue'
import { useThemeStore } from '@/stores/theme'
import { useTickRefresh } from '@/composables/useTickRefresh'
import { useEndgameStore } from '@/stores/endgame'

const themeStore = useThemeStore()
themeStore.init()

const { t, te } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const newsStore = usesStore()
const notificationsStore = useNotificationsStore()
const gameAdminStore = useGameAdminStore()
const chatStore = useChatStore()
const endgameStore = useEndgameStore()
const { unreadCount } = storeToRefs(newsStore)
const { inbox: notificationsInbox, unreadCount: notificationUnreadCount, loading: notificationsLoading } = storeToRefs(notificationsStore)
const { session } = storeToRefs(gameAdminStore)
const { isChatOpen, unreadCount: chatUnreadCount } = storeToRefs(chatStore)
const isMenuOpen = ref(false)
const isNotificationsOpen = ref(false)
const openMobileSection = ref<'main' | 'economy' | 'build' | 'social' | 'admin'>('main')

const showUnreadBadge = computed(() => auth.isAuthenticated && unreadCount.value > 0)
const showNotificationBadge = computed(() => auth.isAuthenticated && notificationUnreadCount.value > 0)
const stockProposalBadgeCount = ref(0)
const showStockProposalBadge = computed(() => auth.isAuthenticated && stockProposalBadgeCount.value > 0)

type NotificationPanelItem = PlayerNotificationItem & {
  displayTitle: string
  displayMessage: string
}

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
  openMobileSection.value = 'main'
}

function toggleMobileSection(section: 'main' | 'economy' | 'build' | 'social' | 'admin') {
  openMobileSection.value = openMobileSection.value === section ? 'main' : section
}

function handleChatToggle() {
  closeMenu()
  chatStore.toggleChat()
}

async function toggleNotificationsPanel() {
  isNotificationsOpen.value = !isNotificationsOpen.value
  if (isNotificationsOpen.value) {
    await notificationsStore.fetchInbox(20)
    const unreadIds = notificationsInbox.value?.items.filter((item) => !item.isRead).map((item) => item.id) ?? []
    if (unreadIds.length > 0) {
      await notificationsStore.markRead(unreadIds)
    }
  }
}

function closeNotificationsPanel() {
  isNotificationsOpen.value = false
}

async function handleNotificationClick(
  notificationId: string,
  isRead: boolean,
  item: {
    buildingId: string | null
    companyId: string | null
    loanId: string | null
    bankAccountId: string | null
    type: string
  },
) {
  if (!isRead) {
    await notificationsStore.markRead([notificationId])
  }

  if (item.buildingId) {
    await router.push(`/building/${item.buildingId}`)
  } else if (item.type === 'SHIPMENT_ARRIVED' || item.type === 'LOGISTICS_MARGIN_EROSION') {
    await router.push('/trade-routes')
  } else if (item.type === 'LOAN_REPAYMENT_DUE_SOON' || item.type === 'LOAN_PAYMENT_DUE' || item.type === 'LOAN_DEFAULT' || item.loanId) {
    await router.push('/banking')
  } else if (item.type === 'BANK_ACCOUNT_LOW_BALANCE' || item.bankAccountId) {
    await router.push('/bank-statement')
  } else if (item.type === 'TAKEOVER_ALERT' || item.companyId) {
    await router.push('/stocks')
  } else {
    await router.push('/dashboard')
  }

  closeNotificationsPanel()
}

function getNotificationIcon(type: string) {
  if (type === 'SHIPMENT_ARRIVED') {
    return {
      symbol: '✅',
      className: 'notification-icon-shipment',
    }
  }
  if (type === 'LOGISTICS_MARGIN_EROSION') {
    return {
      symbol: '⚠️',
      className: 'notification-icon-margin',
    }
  }
  return {
    symbol: '🔔',
    className: 'notification-icon-default',
  }
}

function getNotificationSeverityClass(severity: string) {
  if (severity === 'CRITICAL') {
    return 'notification-severity-critical'
  }
  if (severity === 'WARNING') {
    return 'notification-severity-warning'
  }
  return 'notification-severity-info'
}

function formatNotificationAge(createdAtUtc: string) {
  const createdAt = new Date(createdAtUtc).getTime()
  const diffMs = Math.max(0, Date.now() - createdAt)
  const diffMinutes = Math.floor(diffMs / 60000)
  if (diffMinutes < 1) return t('notifications.justNow')
  if (diffMinutes < 60) return t('notifications.minutesAgo', { count: diffMinutes })
  const diffHours = Math.floor(diffMinutes / 60)
  if (diffHours < 24) return t('notifications.hoursAgo', { count: diffHours })
  const diffDays = Math.floor(diffHours / 24)
  return t('notifications.daysAgo', { count: diffDays })
}

const groupedNotifications = computed(() => {
  const items: NotificationPanelItem[] = (notificationsInbox.value?.items ?? []).map((item) => {
    const localizedCopy = resolveNotificationCopy(item, t, te)
    return {
      ...item,
      displayTitle: localizedCopy.title,
      displayMessage: localizedCopy.message,
    }
  })
  const groups = new Map<string, NotificationPanelItem[]>()
  for (const item of items) {
    const day = new Date(item.createdAtUtc).toLocaleDateString()
    const existing = groups.get(day) ?? []
    existing.push(item)
    groups.set(day, existing)
  }
  return Array.from(groups.entries()).map(([day, itemsForDay]) => ({ day, items: itemsForDay }))
})

function handleNotificationsEsc(event: KeyboardEvent) {
  if (event.key === 'Escape' && isNotificationsOpen.value) {
    closeNotificationsPanel()
  }
}

async function markAllNotificationsRead() {
  await notificationsStore.markAllRead()
}

async function refreshStockProposalBadgeCount() {
  if (!auth.isAuthenticated) {
    stockProposalBadgeCount.value = 0
    return
  }

  try {
    const result = await gqlRequest<{ myOpenDividendProposalCount: number }>(
      `
        query MyOpenDividendProposalCount {
          myOpenDividendProposalCount
        }
      `,
    )
    stockProposalBadgeCount.value = Math.max(0, Number(result.myOpenDividendProposalCount ?? 0))
  } catch {
    stockProposalBadgeCount.value = 0
  }
}

async function handleLogout() {
  const federatedRedirectStarted = auth.logout({ federated: true })
  closeMenu()
  if (!federatedRedirectStarted) {
    await router.push('/')
  }
}

const mobileNavSections = computed(() => {
  const sections = [
    {
      key: 'main' as const,
      label: t('nav.sectionMain'),
      links: [
        { key: 'home', label: t('nav.home'), to: '/', icon: ['fas', 'home'], visible: true, badge: 0 },
        { key: 'dashboard', label: t('nav.dashboard'), to: '/dashboard', icon: ['fas', 'tachometer-alt'], visible: auth.isAuthenticated, badge: 0 },
        { key: 'leaderboard', label: t('nav.leaderboard'), to: '/leaderboard', icon: ['fas', 'trophy'], visible: true, badge: 0 },
        { key: 'cities', label: t('nav.cities'), to: '/cities', icon: ['fas', 'globe'], visible: true, badge: 0 },
        { key: 'news', label: t('nav.news'), to: '/news', icon: ['fas', 'newspaper'], visible: true, badge: showUnreadBadge.value ? unreadCount.value : 0 },
        { key: 'tutorial', label: t('nav.tutorial'), to: '/tutorial', icon: ['fas', 'graduation-cap'], visible: true, badge: 0 },
      ],
    },
    {
      key: 'economy' as const,
      label: t('nav.sectionEconomy'),
      links: [
        { key: 'exchange', label: t('nav.exchange'), to: '/exchange', icon: ['fas', 'chart-bar'], visible: true, badge: 0 },
        { key: 'stocks', label: t('nav.stocks'), to: '/stocks', icon: ['fas', 'wallet'], visible: true, badge: showStockProposalBadge.value ? stockProposalBadgeCount.value : 0 },
        { key: 'forex', label: t('nav.forex'), to: '/forex', icon: ['fas', 'coins'], visible: auth.isAuthenticated, badge: 0 },
        { key: 'banking', label: t('nav.banking'), to: '/banking', icon: ['fas', 'landmark'], visible: true, badge: 0 },
        { key: 'bank-statement', label: t('nav.bankStatement'), to: '/bank-statement', icon: ['fas', 'file-invoice-dollar'], visible: auth.isAuthenticated, badge: 0 },
        { key: 'campaigns', label: t('nav.campaignAnalytics'), to: '/market-intelligence', icon: ['fas', 'bullhorn'], visible: auth.isAuthenticated, badge: 0 },
        { key: 'market-dashboard', label: t('nav.marketDashboard'), to: '/market', icon: ['fas', 'chart-pie'], visible: true, badge: 0 },
        { key: 'energy-market', label: t('nav.energy'), to: '/energy-market', icon: ['fas', 'chart-bar'], visible: true, badge: 0 },
        { key: 'trade-routes', label: t('tradeRoutes.nav'), to: '/trade-routes', icon: ['fas', 'route'], visible: auth.isAuthenticated, badge: 0 },
      ],
    },
    {
      key: 'build' as const,
      label: t('nav.sectionBuild'),
      links: [
        { key: 'building-market', label: t('nav.buildingMarket'), to: '/buildings/market', icon: ['fas', 'store'], visible: true, badge: 0 },
        { key: 'encyclopedia', label: t('nav.encyclopedia'), to: '/encyclopedia', icon: ['fas', 'book-open'], visible: true, badge: 0 },
      ],
    },
    {
      key: 'social' as const,
      label: t('nav.sectionSocial'),
      links: [
        {
          key: 'chat',
          label: t('nav.chat'),
          icon: ['fas', 'comments'],
          visible: auth.isAuthenticated,
          action: handleChatToggle,
          active: isChatOpen.value,
          badge: chatUnreadCount.value,
        },
      ],
    },
    {
      key: 'admin' as const,
      label: t('nav.sectionAdmin'),
      links: [{ key: 'operations', label: t('nav.operations'), to: '/operations/statistics', icon: ['fas', 'chart-line'], visible: !!session.value?.canAccessAdminDashboard, badge: 0 }],
    },
  ]

  return sections
    .map((section) => ({
      ...section,
      links: section.links.filter((link) => link.visible),
    }))
    .filter((section) => section.links.length > 0)
})

onMounted(() => {
  void refreshStockProposalBadgeCount()
  endgameStore.startPolling()
  window.addEventListener('keydown', handleNotificationsEsc)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleNotificationsEsc)
})

useTickRefresh(async () => {
  await refreshStockProposalBadgeCount()
})
</script>

<template>
  <header class="app-header bg-card border-b border-divider sticky top-0 z-[100] backdrop-blur-sm">
    <div class="container flex items-center gap-8 h-16">
      <!-- Logo -->
      <RouterLink to="/" class="logo-link shrink-0" @click="closeMenu">
        <span class="logo-text">{{ t('home.heroTitle') }}</span>
      </RouterLink>

      <!-- Mobile menu toggle -->
      <button
        class="menu-toggle ml-auto lg:hidden text-muted hover:text-body p-2 rounded-md transition-colors tap-target-44"
        @click="toggleMenu"
        :aria-expanded="isMenuOpen"
        aria-label="Toggle navigation menu"
      >
        <font-awesome-icon :icon="['fas', 'bars']" />
      </button>

      <!-- Navigation links -->
      <nav class="nav-links" :class="{ 'nav-open': isMenuOpen }">
        <div class="desktop-nav-links">
          <RouterLink to="/" :title="t('nav.home')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'home']" class="mr-2" />
          </RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/dashboard" :title="t('nav.dashboard')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'tachometer-alt']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/leaderboard" :title="t('nav.leaderboard')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'trophy']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/cities" :title="t('nav.cities')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'globe']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/buildings/market" :title="t('nav.buildingMarket')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'store']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/encyclopedia" :title="t('nav.encyclopedia')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'book-open']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/exchange" :title="t('nav.exchange')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'chart-bar']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/stocks" :title="t('nav.stocks')" class="nav-link nav-link-badge-host" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'wallet']" class="mr-2" />
            <span v-if="showStockProposalBadge" class="nav-badge nav-badge-news">{{ stockProposalBadgeCount }}</span>
          </RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/forex" :title="t('nav.forex')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'coins']" class="mr-2" />
          </RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/bank-statement" :title="t('nav.bankStatement')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'file-invoice-dollar']" class="mr-2" />
          </RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/market-intelligence" :title="t('nav.campaignAnalytics')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'bullhorn']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/market" :title="t('nav.market')" :aria-label="t('nav.marketDashboard')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'chart-pie']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/energy-market" :title="t('nav.energy')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'chart-bar']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/banking" :title="t('nav.banking')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'landmark']" class="mr-2" />
          </RouterLink>
          <RouterLink v-if="auth.isAuthenticated" to="/trade-routes" :title="t('tradeRoutes.nav')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'route']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/tutorial" :title="t('nav.tutorial')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'graduation-cap']" class="mr-2" />
          </RouterLink>
          <RouterLink to="/news" :title="t('nav.news')" :aria-label="t('nav.news')" class="nav-link nav-link-badge-host" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'newspaper']" class="mr-2" />
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
            <span v-if="chatUnreadCount > 0" class="nav-badge nav-badge-chat chat-badge">{{ chatUnreadCount }}</span>
          </button>
          <RouterLink v-if="session?.canAccessAdminDashboard" to="/operations/statistics" :title="t('nav.operations')" :aria-label="t('nav.operations')" class="nav-link" @click="closeMenu">
            <font-awesome-icon :icon="['fas', 'chart-line']" class="mr-2" />
          </RouterLink>
        </div>

        <div v-if="isMenuOpen" class="mobile-nav-sections" :aria-label="t('nav.mobileMenuLabel')" role="navigation">
          <section v-for="section in mobileNavSections" :key="section.key" class="mobile-nav-section">
            <button class="mobile-section-toggle tap-target-44" :aria-expanded="openMobileSection === section.key" @click="toggleMobileSection(section.key)">
              <span>{{ section.label }}</span>
              <font-awesome-icon :icon="['fas', openMobileSection === section.key ? 'chevron-up' : 'chevron-down']" />
            </button>
            <div v-show="openMobileSection === section.key" class="mobile-section-links">
              <template v-for="link in section.links" :key="link.key">
                <RouterLink v-if="'to' in link" :to="link.to" class="mobile-sub-link tap-target-44" @click="closeMenu">
                  <font-awesome-icon :icon="link.icon" />
                  <span>{{ link.label }}</span>
                  <span v-if="link.badge && link.badge > 0" class="mobile-sub-badge">{{ link.badge }}</span>
                </RouterLink>
                <button v-else class="mobile-sub-link mobile-sub-button tap-target-44" :class="{ 'mobile-sub-link-active': link.active }" @click="link.action">
                  <font-awesome-icon :icon="link.icon" />
                  <span>{{ link.label }}</span>
                  <span v-if="link.badge && link.badge > 0" class="mobile-sub-badge">{{ link.badge }}</span>
                </button>
              </template>
            </div>
          </section>
        </div>
      </nav>

      <!-- Right-side actions -->
      <div class="header-actions flex items-center gap-3 shrink-0">
        <div class="hidden xl:block">
          <GameTimeChip />
        </div>

        <!-- Game-ended lock indicator -->
        <div
          v-if="endgameStore.isGameEnded"
          class="hidden sm:flex items-center gap-1.5 rounded-full border border-divider bg-card-raised px-3 py-1.5 text-xs text-muted"
          :title="t('endgame.readOnlyNavHint', { winner: endgameStore.winnerDisplayName ?? '' })"
        >
          <font-awesome-icon :icon="['fas', 'lock']" class="text-muted" aria-hidden="true" />
          <span>{{ t('endgame.readOnlyNavHint', { winner: endgameStore.winnerDisplayName ?? '' }) }}</span>
        </div>

        <!-- Impersonation chip -->
        <div
          v-if="impersonationLabel"
          class="impersonation-chip hidden sm:block max-w-[17rem] px-3 py-1.5 rounded-full border border-[rgba(255,138,0,0.5)] bg-[rgba(255,138,0,0.14)] text-[#ffd7a3] text-[0.72rem] leading-tight"
        >
          {{ impersonationLabel }}
        </div>

        <!-- Global economic event banner -->
        <GlobalEventBanner class="hidden sm:flex" />

        <template v-if="auth.isAuthenticated">
          <button
            class="btn btn-secondary h-11 w-11 p-0 justify-center relative notification-bell-btn tap-target-44"
            :title="t('notifications.title')"
            :aria-label="t('notifications.title')"
            :aria-expanded="isNotificationsOpen"
            @click="toggleNotificationsPanel"
          >
            <font-awesome-icon :icon="['fas', 'bell']" />
            <span v-if="showNotificationBadge" class="notification-badge">{{ notificationUnreadCount }}</span>
          </button>
          <ContextSwitcher @switched="closeMenu" />
          <button class="btn btn-secondary h-11 w-11 p-0 justify-center tap-target-44" @click="handleLogout" :title="t('common.logout')">
            <font-awesome-icon :icon="['fas', 'sign-out-alt']" />
          </button>
        </template>
        <RouterLink v-else to="/login" class="btn btn-primary" :title="t('common.login')" @click="closeMenu">
          <font-awesome-icon :icon="['fas', 'sign-in-alt']" />
        </RouterLink>
        <div class="theme-toggle-wrap">
          <ThemeToggle />
        </div>
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
      <div v-else class="notification-groups">
        <section v-for="group in groupedNotifications" :key="group.day" class="notification-group">
          <h4 class="notification-group-day">{{ group.day }}</h4>
          <ul class="notification-list">
            <li v-for="item in group.items" :key="item.id" class="notification-item" :class="{ 'notification-item-unread': !item.isRead }">
              <button
                class="notification-item-btn"
                @click="
                  handleNotificationClick(item.id, item.isRead, {
                    buildingId: item.buildingId,
                    companyId: item.companyId,
                    loanId: item.loanId,
                    bankAccountId: item.bankAccountId,
                    type: item.type,
                  })
                "
              >
                <div class="notification-item-top">
                  <span class="notification-item-icon" :class="[getNotificationIcon(item.type).className, getNotificationSeverityClass(item.severity)]">
                    {{ getNotificationIcon(item.type).symbol }}
                  </span>
                  <span class="notification-item-title">{{ item.displayTitle }}</span>
                </div>
                <span class="notification-item-message">{{ item.displayMessage }}</span>
                <span class="notification-item-time">{{ formatNotificationAge(item.createdAtUtc) }}</span>
              </button>
            </li>
          </ul>
        </section>
      </div>
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
  text-transform: uppercase;
}

/* ── Navigation links ─────────────────────────────────────────────────────── */
.nav-links {
  display: flex;
  flex: 1;
}

.desktop-nav-links {
  display: flex;
  gap: 1rem;
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

.mobile-nav-sections {
  display: none;
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
.notification-item-top {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}
.notification-item-icon {
  font-size: 0.95rem;
  line-height: 1;
}
.notification-icon-shipment {
  color: #16a34a;
}
.notification-icon-margin {
  color: #f59e0b;
}
.notification-icon-default {
  color: var(--color-text-secondary);
}

.notification-severity-critical {
  color: #ef4444;
}

.notification-severity-warning {
  color: #f59e0b;
}

.notification-severity-info {
  color: #3b82f6;
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

.notification-groups {
  display: grid;
  gap: 0.7rem;
}

.notification-group-day {
  font-size: 0.72rem;
  font-weight: 700;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.03em;
  margin: 0;
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

.tap-target-44 {
  min-width: 44px;
  min-height: 44px;
}

@media (max-width: 1024px) {
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

  .desktop-nav-links {
    display: none;
  }

  .mobile-nav-sections {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 0 0.75rem;
  }

  .mobile-nav-section {
    border: 1px solid var(--color-border);
    border-radius: var(--radius-sm);
    background: color-mix(in srgb, var(--color-surface) 92%, var(--color-bg));
    overflow: hidden;
  }

  .mobile-section-toggle {
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.5rem 0.75rem;
    border: 0;
    background: transparent;
    color: var(--color-text);
    font-size: 0.92rem;
    font-weight: 700;
  }

  .mobile-section-links {
    display: flex;
    flex-direction: column;
    padding: 0.25rem 0.5rem 0.5rem;
    gap: 0.25rem;
  }

  .mobile-sub-link {
    width: 100%;
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.6rem 0.75rem;
    border-radius: var(--radius-sm);
    border: 1px solid transparent;
    color: var(--color-text-secondary);
    text-decoration: none;
    background: transparent;
    font-size: 0.95rem;
    font-weight: 500;
  }

  .mobile-sub-link:hover,
  .mobile-sub-link:focus-visible,
  .mobile-sub-link.router-link-active,
  .mobile-sub-link-active {
    border-color: var(--color-primary);
    color: var(--color-text);
    background: color-mix(in srgb, var(--color-primary) 14%, transparent);
    outline: none;
  }

  .mobile-sub-button {
    border: 1px solid transparent;
    cursor: pointer;
    background: transparent;
    text-align: left;
  }

  .mobile-sub-badge {
    margin-left: auto;
    min-width: 1.2rem;
    height: 1.2rem;
    border-radius: 999px;
    padding-inline: 0.28rem;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-size: 0.67rem;
    font-weight: 700;
    color: white;
    background: linear-gradient(135deg, #ff8a00, #ff3d00);
  }

  .nav-links.nav-open {
    transform: translateY(0);
    opacity: 1;
    visibility: visible;
  }

  .header-actions {
    order: 3;
  }
}

@media (max-width: 767px) {
  .notification-panel {
    right: 0.5rem;
    left: 0.5rem;
    top: auto;
    bottom: 0.5rem;
    width: auto;
    max-height: 75vh;
    border-radius: 1rem;
  }
}

@media (max-width: 640px) {
  .container {
    gap: 0.5rem;
    padding-inline: 0.5rem;
  }

  .logo-text {
    font-size: 1.05rem;
  }

  .header-actions {
    gap: 0.5rem;
    margin-left: auto;
  }

  .theme-toggle-wrap {
    display: none;
  }
}
</style>
