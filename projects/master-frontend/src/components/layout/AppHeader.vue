<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { setLocale } from '@/i18n'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'

const auth = useAuthStore()
const { t, locale } = useI18n()
const isMenuOpen = ref(false)

const selectedLocale = computed({
  get: () => locale.value,
  set: (value: string) => {
    if (value === 'en' || value === 'sk' || value === 'de') {
      setLocale(value)
    }
  },
})

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
  <header class="app-header sticky top-0 z-[100] border-b border-divider bg-card/95 backdrop-blur-sm">
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
        >
          <line x1="3" y1="12" x2="21" y2="12" />
          <line x1="3" y1="6" x2="21" y2="6" />
          <line x1="3" y1="18" x2="21" y2="18" />
        </svg>
      </button>

      <nav class="nav-links" :class="{ 'nav-open': isMenuOpen }">
        <RouterLink to="/" class="nav-link" @click="closeMenu">{{ t('nav.home') }}</RouterLink>
        <RouterLink :to="{ path: '/', hash: '#game-servers' }" class="nav-link" @click="closeMenu">
          {{ t('nav.gameServers') }}
        </RouterLink>
        <RouterLink to="/ranking" class="nav-link" @click="closeMenu">{{ t('nav.ranking') }}</RouterLink>
        <RouterLink to="/ranking/bounties" class="nav-link" @click="closeMenu">
          {{ t('nav.bounties') }}
        </RouterLink>
        <RouterLink to="/referrals/dashboard" class="nav-link" @click="closeMenu">
          {{ t('nav.referralDashboard') }}
        </RouterLink>
        <RouterLink to="/account" class="nav-link" @click="closeMenu">
          {{ t('nav.tokenizedGold') }}
        </RouterLink>
      </nav>

      <div class="header-actions flex items-center gap-2">
        <label class="sr-only" for="master-language">{{ t('app.languageLabel') }}</label>
        <select
          id="master-language"
          v-model="selectedLocale"
          class="h-9 rounded-md border border-divider bg-card px-2 text-sm text-body"
          aria-label="Language"
        >
          <option value="en">EN</option>
          <option value="sk">SK</option>
          <option value="de">DE</option>
        </select>

        <template v-if="auth.isAuthenticated">
          <span class="hidden max-w-36 truncate text-sm text-muted lg:inline">{{ auth.player?.displayName }}</span>
          <button class="btn btn-secondary h-9 px-3" type="button" @click="logout">
            {{ t('home.signOut') }}
          </button>
        </template>
        <RouterLink v-else to="/login" class="btn btn-primary h-9 px-3" @click="closeMenu">
          {{ t('login.signIn') }}
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
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  font-weight: 500;
  text-decoration: none;
  transition: color 0.15s;
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
