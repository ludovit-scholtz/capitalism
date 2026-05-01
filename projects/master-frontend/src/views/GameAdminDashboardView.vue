<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

onMounted(() => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }

  if (!auth.isGameAdmin) {
    void router.push('/')
  }
})

const navItems = computed(() => [
  { label: t('home.supportAdmin'), to: '/support/admin' },
  { label: t('home.rankingAdmin'), to: '/ranking/admin' },
  { label: t('home.goldAdmin'), to: '/gold-admin' },
  { label: t('common.backToPortal'), to: '/' },
])
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('gameAdmin.kicker')"
      :title="t('gameAdmin.title')"
      :subtitle="t('gameAdmin.subtitle')"
      variant="admin"
      :nav-links="navItems"
    />

    <section class="container grid gap-4 py-6 md:grid-cols-3">
      <RouterLink class="admin-card" to="/support/admin">
        <h2>{{ t('home.supportAdmin') }}</h2>
        <p>{{ t('gameAdmin.supportCopy') }}</p>
      </RouterLink>

      <RouterLink class="admin-card" to="/ranking/admin">
        <h2>{{ t('home.rankingAdmin') }}</h2>
        <p>{{ t('gameAdmin.rankingCopy') }}</p>
      </RouterLink>

      <RouterLink class="admin-card" to="/gold-admin">
        <h2>{{ t('home.goldAdmin') }}</h2>
        <p>{{ t('gameAdmin.goldCopy') }}</p>
      </RouterLink>
    </section>
  </main>
</template>

<style scoped>
.admin-card {
  border: 1px solid var(--color-border);
  border-radius: 16px;
  background: var(--color-surface);
  padding: 1rem;
  color: var(--color-text);
  transition: transform 0.15s ease;
}

.admin-card:hover {
  transform: translateY(-2px);
}

.admin-card h2 {
  font-size: 1rem;
  font-weight: 700;
}

.admin-card p {
  margin-top: 0.5rem;
  font-size: 0.9rem;
  color: var(--color-text-secondary);
}
</style>
