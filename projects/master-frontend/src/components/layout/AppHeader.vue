<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'

const auth = useAuthStore()
const { t } = useI18n()
const isMenuOpen = ref(false)

function closeMenu() {
  isMenuOpen.value = false
}

function toggleMenu() {
  isMenuOpen.value = !isMenuOpen.value
}

function logout() {
  auth.logout()
  closeMenu()
}
</script>

<template>
  <header
    class="app-header sticky top-0 z-[100] border-b border-divider bg-card/95 backdrop-blur-sm"
  >
    <div class="container flex h-16 items-center gap-6">
      <RouterLink to="/" class="logo-link shrink-0" @click="closeMenu">
        <span class="logo-text">CAPITALISM HQ</span>
      </RouterLink>

      <button
        class="ml-auto rounded-md p-2 text-muted transition-colors hover:bg-overlay hover:text-body md:hidden"
        :aria-expanded="isMenuOpen"
        :aria-label="t('app.toggleNavigation')"
        @click="toggleMenu"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          width="22"
          height="22"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <line x1="3" y1="12" x2="21" y2="12" />
          <line x1="3" y1="6" x2="21" y2="6" />
          <line x1="3" y1="18" x2="21" y2="18" />
        </svg>
      </button>

      <nav class="nav-links" :class="{ 'nav-open': isMenuOpen }">
        <RouterLink
          to="/"
          :title="t('nav.home')"
          :aria-label="t('nav.home')"
          class="nav-link"
          @click="closeMenu"
        >
          <svg
            class="nav-icon"
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M3 10.5 12 3l9 7.5" />
            <path d="M5 9.5V21h14V9.5" />
          </svg>
          <span class="inline-block md:hidden">{{ t('nav.home') }}</span>
        </RouterLink>

        <RouterLink
          to="/game-servers"
          :title="t('nav.gameServers')"
          :aria-label="t('nav.gameServers')"
          class="nav-link"
          @click="closeMenu"
        >
          <svg
            class="nav-icon"
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <rect x="3" y="4" width="18" height="6" rx="1.5" />
            <rect x="3" y="14" width="18" height="6" rx="1.5" />
            <path d="M7 7h.01M7 17h.01M11 7h6M11 17h6" />
          </svg>
          <span class="inline-block md:hidden">{{ t('nav.gameServers') }}</span>
        </RouterLink>

        <RouterLink
          to="/ranking"
          :title="t('nav.ranking')"
          :aria-label="t('nav.ranking')"
          class="nav-link"
          @click="closeMenu"
        >
          <svg
            class="nav-icon"
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M8 21h8" />
            <path d="M12 17v4" />
            <path d="M7 4h10v3a5 5 0 0 1-10 0V4Z" />
            <path d="M7 6H5a2 2 0 0 0 2 2" />
            <path d="M17 6h2a2 2 0 0 1-2 2" />
          </svg>
          <span class="inline-block md:hidden">{{ t('nav.ranking') }}</span>
        </RouterLink>

        <RouterLink
          to="/ranking/bounties"
          :title="t('nav.bounties')"
          :aria-label="t('nav.bounties')"
          class="nav-link"
          @click="closeMenu"
        >
          <svg
            class="nav-icon"
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="8" />
            <circle cx="12" cy="12" r="4" />
            <circle cx="12" cy="12" r="1.2" fill="currentColor" stroke="none" />
          </svg>
          <span class="inline-block md:hidden">{{ t('nav.bounties') }}</span>
        </RouterLink>

        <RouterLink
          to="/support"
          :title="t('nav.supportDashboard')"
          :aria-label="t('nav.supportDashboard')"
          class="nav-link"
          @click="closeMenu"
        >
          <svg
            class="nav-icon"
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="3" />
            <path
              d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33h.01a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51h.01a1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82v.01a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"
            />
          </svg>
          <span class="inline-block md:hidden">{{ t('nav.supportDashboard') }}</span>
        </RouterLink>

        <RouterLink
          to="/referrals/dashboard"
          :title="t('nav.referralDashboard')"
          :aria-label="t('nav.referralDashboard')"
          class="nav-link"
          @click="closeMenu"
        >
          <svg
            class="nav-icon"
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <circle cx="6" cy="12" r="2" />
            <circle cx="18" cy="6" r="2" />
            <circle cx="18" cy="18" r="2" />
            <path d="m8 12 8-6" />
            <path d="m8 12 8 6" />
          </svg>
          <span class="inline-block md:hidden">{{ t('nav.referralDashboard') }}</span>
        </RouterLink>

        <RouterLink
          to="/account"
          :title="t('nav.tokenizedGold')"
          :aria-label="t('nav.tokenizedGold')"
          class="nav-link"
          @click="closeMenu"
        >
          <svg
            class="nav-icon"
            xmlns="http://www.w3.org/2000/svg"
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <ellipse cx="12" cy="7" rx="6" ry="3" />
            <path d="M6 7v5c0 1.66 2.69 3 6 3s6-1.34 6-3V7" />
            <path d="M6 12v5c0 1.66 2.69 3 6 3s6-1.34 6-3v-5" />
          </svg>
          <span class="inline-block md:hidden">{{ t('nav.tokenizedGold') }}</span>
        </RouterLink>
      </nav>

      <div class="header-actions flex items-center gap-2">
        <template v-if="auth.isAuthenticated">
          <span class="hidden max-w-36 truncate text-sm text-muted lg:inline">{{
            auth.player?.displayName
          }}</span>
          <button
            class="btn btn-secondary h-9 w-9 p-0 justify-center"
            type="button"
            :title="t('home.signOut')"
            :aria-label="t('home.signOut')"
            @click="logout"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
              <path d="M16 17l5-5-5-5" />
              <path d="M21 12H9" />
            </svg>
          </button>
        </template>
        <RouterLink
          v-else
          to="/login"
          class="btn btn-primary h-9 px-3 gap-2 text-white"
          :title="t('login.signIn')"
          :aria-label="t('login.signIn')"
          @click="closeMenu"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            width="18"
            height="18"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
            <path d="M10 17l5-5-5-5" />
            <path d="M15 12H3" />
          </svg>
          <span class="hidden md:inline">{{ t('login.signIn') }}</span>
        </RouterLink>

        <ThemeToggle />
      </div>
    </div>
  </header>
</template>

<style scoped>
.logo-link {
  display: flex;
  align-items: center;
  text-decoration: none;
}

.logo-text {
  border-top: 1px solid gold;
  border-bottom: 1px solid gold;
  background: linear-gradient(135deg, gold, orange);
  background-clip: text;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  font-size: 1.18rem;
  font-weight: 700;
  letter-spacing: -0.02em;
  white-space: nowrap;
}

.nav-links {
  display: flex;
  flex: 1;
  gap: 1.35rem;
}

.nav-link {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  font-weight: 500;
  text-decoration: none;
  transition: color 0.15s;
}

.nav-icon {
  flex: 0 0 auto;
}

.nav-link::after {
  content: '';
  position: absolute;
  right: 0;
  bottom: -2px;
  left: 0;
  height: 2px;
  transform: scaleX(0);
  border-radius: 1px;
  background: var(--color-primary);
  transition: transform 0.15s;
}

.nav-link:hover,
.nav-link.router-link-active {
  color: var(--color-text);
}

.nav-link:hover::after,
.nav-link.router-link-active::after {
  transform: scaleX(1);
}

@media (max-width: 900px) {
  .nav-links {
    position: absolute;
    top: 100%;
    right: 0;
    left: 0;
    display: flex;
    visibility: hidden;
    flex-direction: column;
    gap: 0;
    border-bottom: 1px solid var(--color-border);
    background: var(--color-surface);
    opacity: 0;
    transform: translateY(-100%);
    transition: all 0.25s ease;
    max-height: calc(100vh - 64px);
    overflow-y: auto;
  }

  .nav-links.nav-open {
    visibility: visible;
    opacity: 1;
    transform: translateY(0);
  }

  .nav-link {
    width: 100%;
    justify-content: flex-start;
    padding: 0.9rem 1.5rem;
  }

  .nav-link::after {
    display: none;
  }

  .header-actions {
    margin-left: auto;
  }
}

@media (max-width: 640px) {
  .header-actions {
    gap: 0.45rem;
  }
}
</style>
