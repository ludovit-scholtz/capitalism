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
  { name: 'operations-statistics', path: '/operations/statistics', label: computed(() => t('operations.nav.statistics')) },
  { name: 'operations-news', path: '/operations/news', label: computed(() => t('operations.nav.news')) },
  { name: 'operations-players', path: '/operations/players', label: computed(() => t('operations.nav.players')) },
  { name: 'operations-analytics', path: '/operations/analytics', label: computed(() => t('operations.nav.analytics')) },
]

function isActiveRoute(path: string) {
  return route.path.startsWith(path)
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.replace('/login')
    return
  }
  try {
    await adminStore.fetchSession()
  } catch {
    // errors handled in template
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

    <template v-else>
      <nav class="ops-subnav" role="navigation" aria-label="Operations sections">
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
    </template>
  </div>
</template>

<style scoped>
.operations-view {
  padding-top: 2rem;
  padding-bottom: 4rem;
}

.ops-header {
  margin-bottom: 1.75rem;
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

.ops-subnav {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  border-bottom: 1px solid var(--color-border);
  margin-bottom: 2rem;
  padding-bottom: 0;
}

.ops-nav-tab {
  padding: 0.6rem 1.1rem;
  font-size: 0.9rem;
  border-radius: var(--radius-sm) var(--radius-sm) 0 0;
  border: 1px solid transparent;
  border-bottom: none;
  color: var(--color-text-secondary);
  text-decoration: none;
  transition: color 0.15s, background 0.15s;
}

.ops-nav-tab:hover {
  color: var(--color-text);
  background: rgba(255, 255, 255, 0.04);
}

.ops-nav-tab.active {
  color: var(--color-text);
  background: var(--color-card);
  border-color: var(--color-border);
  border-bottom-color: var(--color-card);
  margin-bottom: -1px;
}

.ops-content {
  min-height: 20rem;
}

@media (max-width: 640px) {
  .ops-subnav {
    gap: 0.2rem;
  }
  .ops-nav-tab {
    padding: 0.5rem 0.75rem;
    font-size: 0.82rem;
  }
}
</style>
