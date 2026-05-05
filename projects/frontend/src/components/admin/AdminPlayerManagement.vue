<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { usesStore } from '@/stores/news'
import type { AccountContextType, GameAdminPlayer } from '@/types'

const { t, locale } = useI18n()
const router = useRouter()
const auth = useAuthStore()
const adminStore = useGameAdminStore()
const newsStore = usesStore()

const actionError = ref<string | null>(null)
const actionMessage = ref<string | null>(null)

const canManageRootFeatures = computed(() => adminStore.session?.isRootAdministrator ?? false)

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatDate(value: string | null) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

async function startImpersonation(playerId: string, accountType: AccountContextType, companyId?: string | null) {
  actionError.value = null
  actionMessage.value = null
  try {
    const authPayload = await adminStore.startImpersonation(playerId, accountType, companyId)
    auth.applyAuthPayload(authPayload)
    await Promise.all([adminStore.fetchSession(), newsStore.fetchUnreadCount()])
    actionMessage.value = t('admin.impersonationStarted')
    await router.push('/dashboard')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.impersonationFailed')
  }
}

async function toggleInvisible(player: GameAdminPlayer) {
  actionError.value = null
  try {
    await adminStore.setPlayerInvisibleInChat(player.id, !player.isInvisibleInChat)
    actionMessage.value = player.isInvisibleInChat ? t('admin.playerVisible') : t('admin.playerInvisible')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.playerVisibilityFailed')
  }
}

async function toggleLocalAdmin(player: GameAdminPlayer) {
  actionError.value = null
  try {
    await adminStore.setLocalGameAdminRole(player.id, player.role !== 'ADMIN')
    actionMessage.value = player.role === 'ADMIN' ? t('admin.localAdminRemoved') : t('admin.localAdminGranted')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.localAdminFailed')
  }
}
</script>

<template>
  <div v-if="actionError" class="player-mgmt-banner player-mgmt-banner-error">{{ actionError }}</div>
  <div v-else-if="actionMessage" class="player-mgmt-banner">{{ actionMessage }}</div>

  <section class="admin-grid admin-grid-wide">
    <article class="card admin-panel admin-panel-wide">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.governmentTitle') }}</h2>
          <p>{{ t('admin.governmentBody') }}</p>
        </div>
      </div>
      <div v-if="adminStore.dashboard?.governmentPlayer" class="admin-player-list">
        <article class="admin-player-card admin-gov-card">
          <div class="admin-player-topline">
            <div>
              <h3>{{ adminStore.dashboard.governmentPlayer.displayName }}</h3>
              <p>{{ adminStore.dashboard.governmentPlayer.email }}</p>
            </div>
            <div class="admin-player-badges">
              <span class="badge badge-warning">SYSTEM</span>
            </div>
          </div>
          <div class="admin-player-stats">
            <span>{{ t('admin.personalCash') }}: {{ formatCurrency(adminStore.dashboard.governmentPlayer.personalCash) }}</span>
            <span>{{ t('admin.companyCash') }}: {{ formatCurrency(adminStore.dashboard.governmentPlayer.totalCompanyCash) }}</span>
          </div>
          <div class="admin-player-actions">
            <button type="button" class="btn btn-primary" @click="startImpersonation(adminStore.dashboard.governmentPlayer.id, 'PERSON')">{{ t('admin.impersonateGovernment') }}</button>
            <button
              v-for="company in adminStore.dashboard.governmentPlayer.companies"
              :key="company.id"
              type="button"
              class="admin-company-pill"
              @click="startImpersonation(adminStore.dashboard.governmentPlayer.id, 'COMPANY', company.id)"
            >
              {{ company.name }} · {{ formatCurrency(company.cash) }}
            </button>
          </div>
        </article>
      </div>
      <p v-else class="admin-empty-state">{{ t('admin.governmentNotSeeded') }}</p>
    </article>
  </section>

  <section class="admin-grid admin-grid-wide">
    <article class="card admin-panel admin-panel-wide">
      <div class="admin-panel-header">
        <div>
          <h2>{{ t('admin.playersTitle') }}</h2>
          <p>{{ t('admin.playersBody') }}</p>
        </div>
      </div>
      <div class="admin-player-list">
        <article v-for="player in adminStore.dashboard?.players ?? []" :key="player.id" class="admin-player-card">
          <div class="admin-player-topline">
            <div>
              <h3>{{ player.displayName }}</h3>
              <p>{{ player.email }}</p>
            </div>
            <div class="admin-player-badges">
              <span class="badge" :class="player.role === 'ADMIN' ? 'badge-primary' : 'badge-success'">{{ player.role }}</span>
              <span v-if="player.isInvisibleInChat" class="badge badge-warning">{{ t('admin.invisibleLabel') }}</span>
            </div>
          </div>

          <div class="admin-player-stats">
            <span>{{ t('admin.personalCash') }}: {{ formatCurrency(player.personalCash) }}</span>
            <span>{{ t('admin.companyCash') }}: {{ formatCurrency(player.totalCompanyCash) }}</span>
            <span>{{ t('admin.lastSeen') }}: {{ formatDate(player.lastLoginAtUtc) }}</span>
          </div>

          <div class="admin-player-actions">
            <button type="button" class="btn btn-secondary" @click="startImpersonation(player.id, 'PERSON')">{{ t('admin.impersonatePerson') }}</button>
            <button type="button" class="btn btn-secondary" @click="toggleInvisible(player)">{{ player.isInvisibleInChat ? t('admin.makeVisible') : t('admin.makeInvisible') }}</button>
            <button v-if="canManageRootFeatures" type="button" class="btn btn-ghost" @click="toggleLocalAdmin(player)">
              {{ player.role === 'ADMIN' ? t('admin.removeLocalAdmin') : t('admin.grantLocalAdmin') }}
            </button>
          </div>

          <div v-if="player.companies.length > 0" class="admin-company-list">
            <button v-for="company in player.companies" :key="company.id" type="button" class="admin-company-pill" @click="startImpersonation(player.id, 'COMPANY', company.id)">
              {{ company.name }} · {{ formatCurrency(company.cash) }}
            </button>
          </div>
        </article>
      </div>
    </article>
  </section>
</template>

<style scoped>
.player-mgmt-banner {
  margin-bottom: 1rem;
  padding: 0.85rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.14);
}

.player-mgmt-banner-error {
  border-color: rgba(248, 113, 113, 0.45);
  background: rgba(248, 113, 113, 0.12);
}

.admin-panel {
  padding: 1.25rem;
}

.admin-panel-wide {
  padding: 1.4rem;
}

.admin-panel-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.admin-panel-header p {
  color: var(--color-text-secondary);
  margin-top: 0.3rem;
}

.admin-empty-state {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px dashed var(--color-border);
  color: var(--color-text-secondary);
}

.admin-player-list {
  display: grid;
  gap: 0.9rem;
}

.admin-player-card {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.02);
}

.admin-gov-card {
  border-color: rgba(255, 138, 0, 0.35);
  background: rgba(255, 138, 0, 0.06);
}

.admin-player-topline {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.admin-player-topline p,
.admin-player-stats {
  color: var(--color-text-secondary);
}

.admin-player-badges {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.admin-player-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  margin: 0.8rem 0;
  font-size: 0.88rem;
}

.admin-player-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.admin-company-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
  margin-top: 0.85rem;
}

.admin-company-pill {
  padding: 0.55rem 0.8rem;
  border-radius: 999px;
  border: 1px solid rgba(0, 71, 255, 0.38);
  background: rgba(0, 71, 255, 0.12);
  color: #cddcff;
}

@media (max-width: 720px) {
  .admin-panel-header,
  .admin-player-topline {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
