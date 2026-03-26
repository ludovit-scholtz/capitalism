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

const buildingTypeIcons: Record<string, string> = {
  MINE: '⛏️',
  FACTORY: '🏭',
  SALES_SHOP: '🏪',
  RESEARCH_DEVELOPMENT: '🔬',
  APARTMENT: '🏢',
  COMMERCIAL: '🏛️',
  MEDIA_HOUSE: '📺',
  BANK: '🏦',
  EXCHANGE: '📊',
  POWER_PLANT: '⚡',
}

function getBuildingIcon(type: string): string {
  return buildingTypeIcons[type] || '🏗️'
}

function formatBuildingType(type: string): string {
  return type.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  try {
    const data = await gqlRequest<{ myCompanies: Company[] }>(
      `{ myCompanies {
        id name cash foundedAtUtc
        buildings { id name type level units { id unitType gridX gridY level } }
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
    <div class="dashboard-header">
      <div>
        <h1>{{ t('dashboard.title') }}</h1>
        <div v-if="auth.player" class="player-info">
          <span class="player-name">{{ auth.player.displayName }}</span>
          <span class="player-email">{{ auth.player.email }}</span>
        </div>
      </div>
    </div>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

    <div v-else-if="error" class="error-message" role="alert">
      {{ error }}
      <button class="btn btn-secondary" @click="router.go(0)">{{ t('common.tryAgain') }}</button>
    </div>

    <div v-else-if="companies.length === 0" class="empty-state">
      <div class="empty-icon">🏗️</div>
      <p>{{ t('dashboard.noCompanies') }}</p>
      <RouterLink to="/onboarding" class="btn btn-primary btn-lg">{{ t('dashboard.startOnboarding') }}</RouterLink>
    </div>

    <div v-else class="companies-section">
      <div v-for="company in companies" :key="company.id" class="company-card">
        <div class="company-header">
          <div>
            <h2>{{ company.name }}</h2>
            <div class="company-meta">
              <span class="meta-item">
                <span class="meta-label">{{ t('dashboard.cash') }}</span>
                <span class="cash">${{ company.cash.toLocaleString() }}</span>
              </span>
              <span class="meta-item">
                <span class="meta-label">{{ t('dashboard.buildings') }}</span>
                <span>{{ company.buildings.length }}</span>
              </span>
            </div>
          </div>
          <RouterLink :to="`/buy-building/${company.id}`" class="btn btn-primary">
            {{ t('dashboard.buyBuilding') }}
          </RouterLink>
        </div>

        <div v-if="company.buildings.length === 0" class="no-buildings">
          <p>{{ t('dashboard.noBuildings') }}</p>
        </div>

        <div v-else class="buildings-grid">
          <RouterLink
            v-for="building in company.buildings"
            :key="building.id"
            :to="`/building/${building.id}`"
            class="building-card"
          >
            <div class="building-icon">{{ getBuildingIcon(building.type) }}</div>
            <div class="building-info">
              <span class="building-name">{{ building.name }}</span>
              <span class="building-type-label">{{ formatBuildingType(building.type) }}</span>
            </div>
            <div class="building-stats">
              <span class="building-level">Lv.{{ building.level }}</span>
              <span class="building-units">{{ building.units.length }} units</span>
            </div>
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dashboard-view {
  padding: 2rem 1rem;
}

.dashboard-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 2rem;
}

.dashboard-header h1 {
  font-size: 1.75rem;
  margin-bottom: 0.25rem;
}

.player-info {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.player-name {
  font-weight: 600;
  font-size: 0.9375rem;
}

.player-email {
  color: var(--color-text-secondary);
  font-size: 0.8125rem;
}

.empty-state {
  text-align: center;
  padding: 4rem 1rem;
}

.empty-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.empty-state p {
  margin-bottom: 1.5rem;
  color: var(--color-text-secondary);
  font-size: 1.125rem;
}

.btn-lg {
  padding: 0.75rem 1.75rem;
  font-size: 1rem;
}

.companies-section {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.company-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1.5rem;
}

.company-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1.25rem;
}

.company-header h2 {
  font-size: 1.25rem;
  margin-bottom: 0.5rem;
}

.company-meta {
  display: flex;
  gap: 1.5rem;
}

.meta-item {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.meta-label {
  font-size: 0.6875rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  font-weight: 600;
}

.cash {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--color-success);
}

.no-buildings {
  text-align: center;
  padding: 1.5rem;
  color: var(--color-text-secondary);
  font-style: italic;
}

.buildings-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 0.75rem;
}

.building-card {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem;
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  text-decoration: none;
  color: var(--color-text);
  transition: all 0.2s ease;
}

.building-card:hover {
  border-color: var(--color-primary);
  background: rgba(0, 71, 255, 0.04);
  transform: translateY(-1px);
  text-decoration: none;
}

.building-icon {
  font-size: 1.75rem;
  flex-shrink: 0;
}

.building-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.building-name {
  font-weight: 600;
  font-size: 0.9375rem;
}

.building-type-label {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.building-stats {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.125rem;
}

.building-level {
  background: var(--color-primary);
  color: #fff;
  padding: 0.125rem 0.5rem;
  border-radius: var(--radius-sm);
  font-size: 0.6875rem;
  font-weight: 700;
}

.building-units {
  font-size: 0.6875rem;
  color: var(--color-text-secondary);
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

@media (max-width: 640px) {
  .company-header {
    flex-direction: column;
    gap: 1rem;
  }

  .buildings-grid {
    grid-template-columns: 1fr;
  }
}
</style>
