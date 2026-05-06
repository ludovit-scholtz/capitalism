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
const player = computed<GameAdminPlayer | null>(() => {
  return adminStore.dashboard?.players.find((p) => p.id === playerId.value) ?? null
})

const actionError = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const loading = ref(false)

function formatDate(value: string | null) {
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
  } catch (e) {
    actionError.value = e instanceof Error ? e.message : t('admin.impersonationFailed')
  }
}

async function impersonateAsCompany(companyId: string) {
  if (!player.value) return
  actionError.value = null
  actionMessage.value = null
  try {
    await adminStore.startImpersonation(player.value.id, 'COMPANY', companyId)
    actionMessage.value = t('admin.impersonationStarted')
  } catch (e) {
    actionError.value = e instanceof Error ? e.message : t('admin.impersonationFailed')
  }
}

async function toggleInvisible() {
  if (!player.value) return
  actionError.value = null
  actionMessage.value = null
  try {
    await adminStore.setPlayerInvisibleInChat(player.value.id, !player.value.isInvisibleInChat)
    actionMessage.value = player.value.isInvisibleInChat ? t('admin.playerVisible') : t('admin.playerInvisible')
  } catch (e) {
    actionError.value = e instanceof Error ? e.message : t('admin.playerVisibilityFailed')
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
  } catch (e) {
    actionError.value = e instanceof Error ? e.message : t('admin.localAdminFailed')
  }
}

onMounted(async () => {
  if (!adminStore.dashboard) {
    loading.value = true
    try {
      await adminStore.fetchDashboard()
    } finally {
      loading.value = false
    }
  }
})
</script>

<template>
  <div class="ops-player-detail">
    <button type="button" class="btn btn-ghost btn-sm ops-back-btn" @click="router.push('/operations/players')">
      ← {{ t('operations.players.backToPlayers') }}
    </button>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>

    <div v-else-if="!player" class="ops-not-found card">
      <p>Player not found.</p>
      <button type="button" class="btn btn-secondary" @click="router.push('/operations/players')">
        {{ t('operations.players.backToPlayers') }}
      </button>
    </div>

    <template v-else>
      <div class="ops-detail-header">
        <div>
          <h2 class="ops-detail-name">{{ player.displayName }}</h2>
          <p class="ops-detail-email">{{ player.email }}</p>
          <span v-if="player.role === 'ADMIN'" class="badge badge-warning">ADMIN</span>
          <span v-if="player.isInvisibleInChat" class="badge badge-warning">Invisible</span>
        </div>
      </div>

      <!-- Feedback banners -->
      <div v-if="actionError" class="ops-banner ops-banner-error">{{ actionError }}</div>
      <div v-else-if="actionMessage" class="ops-banner">{{ actionMessage }}</div>

      <!-- Stats cards -->
      <div class="ops-detail-stats">
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.personalBalance') }}</span>
          <span class="ops-stat-value">{{ formatCurrency(player.personalCash) }}</span>
        </div>
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.totalCompanyCash') }}</span>
          <span class="ops-stat-value">{{ formatCurrency(player.totalCompanyCash) }}</span>
        </div>
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.companies') }}</span>
          <span class="ops-stat-value">{{ player.companyCount }}</span>
        </div>
        <div class="ops-stat-card card">
          <span class="ops-stat-label">{{ t('operations.players.lastSeen') }}</span>
          <span class="ops-stat-value ops-stat-date">{{ formatDate(player.lastLoginAtUtc) }}</span>
        </div>
      </div>

      <!-- Companies list -->
      <div v-if="player.companies.length > 0" class="card ops-companies-panel">
        <h3>{{ t('operations.players.companies') }}</h3>
        <ul class="ops-companies-list">
          <li v-for="company in player.companies" :key="company.id" class="ops-company-item">
            <span class="ops-company-name">{{ company.name }}</span>
            <span class="ops-company-cash">{{ formatCurrency(company.cash) }}</span>
          </li>
        </ul>
      </div>

      <!-- Intervention actions -->
      <div class="card ops-actions-panel">
        <h3>{{ t('operations.players.actions') }}</h3>

        <div class="ops-actions-grid">
          <button type="button" class="btn btn-secondary" @click="impersonateAsPlayer">
            {{ t('operations.players.impersonatePerson') }}
          </button>

          <button
            v-for="company in player.companies"
            :key="company.id"
            type="button"
            class="btn btn-secondary"
            @click="impersonateAsCompany(company.id)"
          >
            {{ t('operations.players.impersonateCompany', { name: company.name }) }}
          </button>

          <button type="button" class="btn btn-secondary" @click="toggleInvisible">
            {{ t('operations.players.toggleInvisible') }}
            ({{ player.isInvisibleInChat ? 'ON' : 'OFF' }})
          </button>

          <button type="button" class="btn btn-secondary" @click="toggleAdmin">
            {{ t('operations.players.toggleAdmin') }}
            ({{ player.role === 'ADMIN' ? 'ON' : 'OFF' }})
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
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.ops-detail-header {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
}

.ops-detail-name {
  font-size: 1.5rem;
  font-weight: 700;
  margin-bottom: 0.15rem;
}

.ops-detail-email {
  color: var(--color-text-secondary);
  margin-bottom: 0.5rem;
}

.ops-banner {
  padding: 0.75rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.1);
  font-size: 0.9rem;
}

.ops-banner-error {
  border-color: rgba(248, 113, 113, 0.4);
  background: rgba(248, 113, 113, 0.1);
}

.ops-detail-stats {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 0.75rem;
}

.ops-stat-card {
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.ops-stat-label {
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
}

.ops-stat-value {
  font-size: 1.25rem;
  font-weight: 700;
}

.ops-stat-date {
  font-size: 0.92rem;
  font-weight: 400;
}

.ops-companies-panel {
  padding: 1.25rem;
}

.ops-companies-panel h3 {
  margin-bottom: 0.75rem;
  font-size: 0.95rem;
  font-weight: 600;
}

.ops-companies-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.ops-company-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  font-size: 0.9rem;
}

.ops-company-item:last-child {
  border-bottom: none;
}

.ops-company-name {
  font-weight: 500;
}

.ops-company-cash {
  color: var(--color-text-secondary);
}

.ops-actions-panel {
  padding: 1.25rem;
}

.ops-actions-panel h3 {
  margin-bottom: 0.75rem;
  font-size: 0.95rem;
  font-weight: 600;
}

.ops-actions-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
}
</style>
