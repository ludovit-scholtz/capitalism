<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import type { Company } from '@/types'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const companies = ref<Company[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  try {
    const data = await gqlRequest<{ myCompanies: Company[] }>(
      `{ myCompanies {
        id name cash foundedAtUtc
        buildings { id name type level }
      } }`,
    )
    companies.value = data.myCompanies
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load dashboard'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="dashboard-view container">
    <h1>{{ t('dashboard.title') }}</h1>

    <div v-if="auth.player" class="player-info">
      <h2>{{ auth.player.displayName }}</h2>
      <p class="player-email">{{ auth.player.email }}</p>
    </div>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="router.go(0)">{{ t('common.tryAgain') }}</button>
    </div>

    <div v-else-if="companies.length === 0" class="empty-state">
      <p>{{ t('dashboard.noCompanies') }}</p>
      <RouterLink to="/onboarding" class="btn btn-primary">{{ t('dashboard.startOnboarding') }}</RouterLink>
    </div>

    <div v-else class="companies-grid">
      <div v-for="company in companies" :key="company.id" class="company-card">
        <h3>{{ company.name }}</h3>
        <p class="cash">${{ company.cash.toLocaleString() }}</p>
        <div class="buildings-list">
          <div v-for="building in company.buildings" :key="building.id" class="building-item">
            <span class="building-type">{{ building.type }}</span>
            <span class="building-name">{{ building.name }}</span>
            <span class="building-level">Lv.{{ building.level }}</span>
          </div>
          <p v-if="company.buildings.length === 0" class="no-buildings">{{ t('dashboard.noBuildings') }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dashboard-view {
  padding: 2rem 1rem;
}

.dashboard-view h1 {
  font-size: 1.75rem;
  margin-bottom: 1.5rem;
}

.player-info {
  margin-bottom: 2rem;
}

.player-info h2 {
  font-size: 1.25rem;
  margin-bottom: 0.25rem;
}

.player-email {
  color: var(--color-text-secondary);
  font-size: 0.875rem;
}

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
}

.empty-state p {
  margin-bottom: 1rem;
  color: var(--color-text-secondary);
}

.companies-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1rem;
}

.company-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
}

.company-card h3 {
  font-size: 1.125rem;
  margin-bottom: 0.5rem;
}

.cash {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--color-success);
  margin-bottom: 1rem;
}

.buildings-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.building-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  background: var(--color-bg);
  border-radius: var(--radius-sm);
  font-size: 0.875rem;
}

.building-type {
  background: var(--color-primary);
  color: #fff;
  padding: 0.125rem 0.5rem;
  border-radius: var(--radius-sm);
  font-size: 0.75rem;
  font-weight: 600;
}

.building-name {
  flex: 1;
}

.building-level {
  color: var(--color-text-secondary);
  font-size: 0.8125rem;
}

.no-buildings {
  color: var(--color-text-secondary);
  font-size: 0.875rem;
  font-style: italic;
}

.error-message {
  background: rgba(248, 113, 113, 0.1);
  color: var(--color-danger);
  padding: 1rem;
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  gap: 1rem;
}

.loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}
</style>
