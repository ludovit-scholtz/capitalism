<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useGameAdminStore } from '@/stores/gameAdmin'
import type { GameAdminPlayer } from '@/types'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const adminStore = useGameAdminStore()

const playerId = computed(() => route.params.id as string)
const player = computed<GameAdminPlayer | null>(
  () => adminStore.dashboard?.players.find((candidate) => candidate.id === playerId.value) ?? null,
)

const actionError = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const loading = ref(false)

function formatDate(value: string | null | undefined) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

async function impersonateAsPlayer() {
  if (!player.value) return

  actionError.value = null
  actionMessage.value = null

  try {
    await adminStore.startImpersonation(player.value.id, 'PERSON', null)
    actionMessage.value = t('admin.impersonationStarted')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.impersonationFailed')
  }
}

async function impersonateAsCompany(companyId: string) {
  if (!player.value) return

  actionError.value = null
  actionMessage.value = null

  try {
    await adminStore.startImpersonation(player.value.id, 'COMPANY', companyId)
    actionMessage.value = t('admin.impersonationStarted')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.impersonationFailed')
  }
}

async function toggleInvisible() {
  if (!player.value) return

  actionError.value = null
  actionMessage.value = null

  try {
    await adminStore.setPlayerInvisibleInChat(player.value.id, !player.value.isInvisibleInChat)
    actionMessage.value = player.value.isInvisibleInChat ? t('admin.playerVisible') : t('admin.playerInvisible')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.playerVisibilityFailed')
  }
}

async function toggleAdmin() {
  if (!player.value) return

  actionError.value = null
  actionMessage.value = null

  try {
    const isNowAdmin = player.value.role !== 'ADMIN'
    await adminStore.setLocalGameAdminRole(player.value.id, isNowAdmin)
    actionMessage.value = isNowAdmin ? t('admin.localAdminGranted') : t('admin.localAdminRemoved')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.localAdminFailed')
  }
}

onMounted(async () => {
  if (adminStore.dashboard) return

  loading.value = true
  try {
    await adminStore.fetchDashboard()
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="ops-player-detail">
    <button
      type="button"
      class="btn btn-ghost btn-sm ops-back-btn"
      @click="router.push('/operations/statistics/players')"
    >
      ← {{ t('operations.players.backToPlayers') }}
    </button>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="!player" class="ops-not-found card">
      <p>{{ t('operations.players.notFound') }}</p>
      <button type="button" class="btn btn-secondary" @click="router.push('/operations/statistics/players')">
        {{ t('operations.players.backToPlayers') }}
      </button>
    </div>
    <template v-else>
      <div class="ops-detail-header">
        <div>
          <h2 class="ops-detail-name">{{ player.displayName }}</h2>
          <p class="ops-detail-email">{{ player.email }}</p>
          <div class="ops-detail-badges">
            <span v-if="player.role === 'ADMIN'" class="badge badge-warning">ADMIN</span>
            <span v-if="player.isInvisibleInChat" class="badge badge-warning">Invisible</span>
          </div>
        </div>
      </div>

      <div v-if="actionError" class="ops-banner ops-banner-error">{{ actionError }}</div>
      <div v-else-if="actionMessage" class="ops-banner">{{ actionMessage }}</div>

      <div class="ops-detail-stats">
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.joined') }}</span>
          <span class="ops-stat-value ops-stat-date">{{ formatDate(player.createdAtUtc) }}</span>
        </div>
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.personalBalance') }}</span>
          <span class="ops-stat-value">{{ formatCurrency(player.personalCash) }}</span>
        </div>
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.companyEquity') }}</span>
          <span class="ops-stat-value">{{ formatCurrency(player.totalCompanyEquity ?? player.totalCompanyCash) }}</span>
        </div>
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.lastSeen') }}</span>
          <span class="ops-stat-value ops-stat-date">{{ formatDate(player.lastLoginAtUtc) }}</span>
        </div>
      </div>

      <div class="card ops-meta-panel">
        <div>
          <h3>{{ t('operations.players.cities') }}</h3>
          <p>{{ (player.cityNames ?? []).join(', ') || t('common.notAvailable') }}</p>
        </div>
        <div>
          <h3>{{ t('operations.players.companies') }}</h3>
          <p>{{ player.companies.length }}</p>
        </div>
      </div>

      <div v-if="player.companies.length > 0" class="card ops-companies-panel">
        <h3>{{ t('operations.players.companies') }}</h3>
        <ul class="ops-companies-list">
          <li v-for="company in player.companies" :key="company.id" class="ops-company-item">
            <div>
              <strong>{{ company.name }}</strong>
              <p>{{ formatCurrency(company.cash) }}</p>
            </div>
            <button type="button" class="btn btn-secondary btn-sm" @click="impersonateAsCompany(company.id)">
              {{ t('operations.players.impersonateCompany', { name: company.name }) }}
            </button>
          </li>
        </ul>
      </div>

      <div class="card ops-actions-panel">
        <h3>{{ t('operations.players.actions') }}</h3>
        <div class="ops-actions-grid">
          <button type="button" class="btn btn-secondary" @click="impersonateAsPlayer">
            {{ t('operations.players.impersonatePerson') }}
          </button>
          <button type="button" class="btn btn-secondary" @click="toggleInvisible">
            {{ t('operations.players.toggleInvisible') }}
          </button>
          <button type="button" class="btn btn-secondary" @click="toggleAdmin">
            {{ t('operations.players.toggleAdmin') }}
          </button>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.ops-player-detail {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.ops-back-btn {
  align-self: flex-start;
}

.ops-loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}

.ops-not-found {
  padding: 1.5rem;
  display: grid;
  gap: 0.75rem;
}

.ops-detail-email,
.ops-stat-label,
.ops-meta-panel p,
.ops-company-item p {
  color: var(--color-text-secondary);
}

.ops-detail-badges {
  display: flex;
  gap: 0.35rem;
  margin-top: 0.5rem;
}

.ops-banner {
  padding: 0.75rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.1);
}

.ops-banner-error {
  border-color: rgba(248, 113, 113, 0.4);
  background: rgba(248, 113, 113, 0.12);
}

.ops-detail-stats,
.ops-meta-panel {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
}

.ops-stat-card,
.ops-meta-panel,
.ops-companies-panel,
.ops-actions-panel {
  padding: 1.1rem 1.25rem;
}

.ops-stat-card {
  display: grid;
  gap: 0.3rem;
}

.ops-stat-value {
  font-size: 1.25rem;
  font-weight: 700;
}

.ops-stat-date {
  font-size: 0.95rem;
}

.ops-companies-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 0.75rem;
}

.ops-company-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  padding: 0.85rem 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.ops-company-item:last-child {
  border-bottom: none;
}

.ops-actions-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-top: 0.85rem;
}

@media (max-width: 720px) {
  .ops-company-item {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>
