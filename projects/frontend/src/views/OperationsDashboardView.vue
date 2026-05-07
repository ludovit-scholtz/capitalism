<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useGameAdminStore } from '@/stores/gameAdmin'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const adminStore = useGameAdminStore()

const canAccessDashboard = computed(() => adminStore.session?.canAccessAdminDashboard ?? false)

const navLinks = [
  { name: 'operations-overview', path: '/operations/statistics', label: computed(() => t('operations.nav.overview')) },
  {
    name: 'operations-money-flow',
    path: '/operations/statistics/money-flow',
    label: computed(() => t('operations.nav.moneyFlow')),
  },
  {
    name: 'operations-product-analytics',
    path: '/operations/statistics/product-analytics',
    label: computed(() => t('operations.nav.productAnalytics')),
  },
  { name: 'operations-news', path: '/operations/statistics/news', label: computed(() => t('operations.nav.news')) },
  {
    name: 'operations-players',
    path: '/operations/statistics/players',
    label: computed(() => t('operations.nav.players')),
  },
]

function isActiveRoute(path: string) {
  return route.path === path || route.path.startsWith(`${path}/`)
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.replace('/login')
    return
  }

  try {
    await adminStore.fetchSession()
  } catch {
    // errors surface through child views
  }
})
</script>

<template>
  <div class="operations-view container">
    <header class="ops-header">
      <p class="ops-eyebrow">{{ t('operations.eyebrow') }}</p>
      <h1>{{ t('operations.title') }}</h1>
      <p class="ops-subtitle">{{ t('operations.subtitle') }}</p>
    </header>

    <div v-if="!canAccessDashboard && !adminStore.loadingSession" class="ops-locked card">
      <h2>{{ t('admin.accessDeniedTitle') }}</h2>
      <p>{{ t('admin.accessDeniedBody') }}</p>
    </div>

    <div v-else class="ops-shell">
      <nav class="ops-subnav card" role="navigation" aria-label="Operations sections">
        <RouterLink
          v-for="link in navLinks"
          :key="link.name"
          :to="link.path"
          class="ops-nav-tab"
          :class="{ active: isActiveRoute(link.path) }"
        >
          {{ link.label.value }}
        </RouterLink>
      </nav>

      <div class="ops-content">
        <RouterView />
      </div>
    </div>
  </div>
</template>

<style scoped>
.operations-view {
  padding-top: 1.5rem;
  padding-bottom: 4rem;
}

.ops-header {
  margin-bottom: 1.5rem;
}

.ops-eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.16em;
  font-size: 0.72rem;
  color: #ffc07a;
  margin-bottom: 0.35rem;
}

.ops-subtitle {
  color: var(--color-text-secondary);
  margin-top: 0.4rem;
  max-width: 65ch;
}

.ops-locked {
  padding: 1.5rem;
}

.ops-shell {
  display: grid;
  grid-template-columns: minmax(220px, 260px) minmax(0, 1fr);
  gap: 1.25rem;
  align-items: start;
}

.ops-subnav {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.75rem;
  position: sticky;
  top: 5rem;
}

.ops-nav-tab {
  padding: 0.75rem 0.95rem;
  font-size: 0.92rem;
  border-radius: var(--radius-md);
  color: var(--color-text-secondary);
  text-decoration: none;
  transition:
    color 0.15s,
    background 0.15s,
    border-color 0.15s;
  border: 1px solid transparent;
}

.ops-nav-tab:hover {
  color: var(--color-text);
  background: rgba(255, 255, 255, 0.04);
}

.ops-nav-tab.active {
  color: var(--color-text);
  background: rgba(255, 192, 122, 0.08);
  border-color: rgba(255, 192, 122, 0.25);
}

.ops-content {
  min-width: 0;
}

@media (max-width: 900px) {
  .ops-shell {
    grid-template-columns: 1fr;
  }

  .ops-subnav {
    position: static;
    flex-direction: row;
    overflow-x: auto;
    padding-bottom: 0.35rem;
  }

  .ops-nav-tab {
    white-space: nowrap;
  }
}
</style>
