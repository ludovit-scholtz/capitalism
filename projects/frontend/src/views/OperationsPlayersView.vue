<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useGameAdminStore } from '@/stores/gameAdmin'
import type { GameAdminPlayer } from '@/types'

const { t, locale } = useI18n()
const router = useRouter()
const adminStore = useGameAdminStore()

type SortKey = 'name' | 'email' | 'lastSeen' | 'balance'

const searchQuery = ref('')
const sortKey = ref<SortKey>('name')
const sortAsc = ref(true)
const loading = ref(false)
const error = ref<string | null>(null)

const players = computed<GameAdminPlayer[]>(() => adminStore.dashboard?.players ?? [])

function formatDate(value: string | null) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(
    value,
  )
}

function toggleSort(key: SortKey) {
  if (sortKey.value === key) {
    sortAsc.value = !sortAsc.value
  } else {
    sortKey.value = key
    sortAsc.value = true
  }
}

const filteredPlayers = computed(() => {
  const q = searchQuery.value.toLowerCase()
  let list = players.value.filter((p) => {
    if (!q) return true
    return p.displayName.toLowerCase().includes(q) || p.email.toLowerCase().includes(q)
  })

  list = [...list].sort((a, b) => {
    let diff = 0
    if (sortKey.value === 'name') diff = a.displayName.localeCompare(b.displayName)
    else if (sortKey.value === 'email') diff = a.email.localeCompare(b.email)
    else if (sortKey.value === 'lastSeen')
      diff = (a.lastLoginAtUtc ?? '').localeCompare(b.lastLoginAtUtc ?? '')
    else if (sortKey.value === 'balance') diff = a.personalCash + a.totalCompanyCash - (b.personalCash + b.totalCompanyCash)
    return sortAsc.value ? diff : -diff
  })
  return list
})

async function load() {
  loading.value = true
  error.value = null
  try {
    await adminStore.fetchDashboard()
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('operations.players.loadFailed')
  } finally {
    loading.value = false
  }
}

function goToDetail(player: GameAdminPlayer) {
  router.push({ name: 'operations-player-detail', params: { id: player.id } })
}

onMounted(load)
</script>

<template>
  <div class="ops-players">
    <div class="ops-section-header">
      <h2>{{ t('operations.players.title') }}</h2>
      <p>{{ t('operations.players.subtitle') }}</p>
    </div>

    <!-- Search + sort controls -->
    <div class="ops-controls">
      <input
        v-model="searchQuery"
        class="form-input ops-search"
        :placeholder="t('operations.players.searchPlaceholder')"
      />
      <div class="ops-sort-group">
        <span class="ops-sort-label">{{ t('operations.players.sortBy') }}</span>
        <button
          v-for="sk in [
            { key: 'name', label: t('operations.players.sortName') },
            { key: 'email', label: t('operations.players.sortEmail') },
            { key: 'lastSeen', label: t('operations.players.sortLastSeen') },
            { key: 'balance', label: t('operations.players.sortBalance') },
          ]"
          :key="sk.key"
          type="button"
          class="ops-sort-btn"
          :class="{ active: sortKey === sk.key }"
          @click="toggleSort(sk.key as SortKey)"
        >
          {{ sk.label }}
          <template v-if="sortKey === sk.key">{{ sortAsc ? '↑' : '↓' }}</template>
        </button>
      </div>
    </div>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="load">{{ t('common.retry') }}</button>
    </div>
    <template v-else>
      <div class="ops-table-wrap">
        <table class="ops-table" aria-label="Player list">
          <thead>
            <tr>
              <th>{{ t('operations.players.sortName') }}</th>
              <th>{{ t('operations.players.sortEmail') }}</th>
              <th>{{ t('operations.players.companies') }}</th>
              <th>{{ t('operations.players.personalBalance') }}</th>
              <th>{{ t('operations.players.totalCompanyCash') }}</th>
              <th>{{ t('operations.players.sortLastSeen') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filteredPlayers.length === 0">
              <td colspan="7" class="ops-table-empty">{{ t('operations.players.noPlayers') }}</td>
            </tr>
            <tr
              v-for="player in filteredPlayers"
              :key="player.id"
              class="ops-player-row"
              @click="goToDetail(player)"
            >
              <td>
                <span class="ops-player-name">{{ player.displayName }}</span>
                <span v-if="player.role === 'ADMIN'" class="badge badge-warning badge-sm">ADMIN</span>
              </td>
              <td class="ops-table-secondary">{{ player.email }}</td>
              <td class="ops-table-secondary">{{ player.companyCount }}</td>
              <td>{{ formatCurrency(player.personalCash) }}</td>
              <td>{{ formatCurrency(player.totalCompanyCash) }}</td>
              <td class="ops-table-secondary">{{ formatDate(player.lastLoginAtUtc) }}</td>
              <td>
                <button type="button" class="btn btn-secondary btn-sm" @click.stop="goToDetail(player)">
                  {{ t('operations.players.viewDetail') }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>

<style scoped>
.ops-players {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.ops-section-header h2 {
  margin-bottom: 0.2rem;
}

.ops-section-header p {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.ops-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
}

.ops-search {
  flex: 1;
  min-width: 200px;
  max-width: 320px;
}

.ops-sort-group {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  align-items: center;
}

.ops-sort-label {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
  margin-right: 0.15rem;
}

.ops-sort-btn {
  padding: 0.38rem 0.8rem;
  font-size: 0.82rem;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: background 0.15s, color 0.15s;
}

.ops-sort-btn.active {
  color: var(--color-text);
  border-color: rgba(255, 255, 255, 0.25);
  background: rgba(255, 255, 255, 0.07);
}

.ops-loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}

.ops-error {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.75rem;
}

.ops-table-wrap {
  overflow-x: auto;
}

.ops-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.88rem;
}

.ops-table th {
  text-align: left;
  padding: 0.6rem 1rem;
  border-bottom: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  font-weight: 500;
  white-space: nowrap;
}

.ops-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  vertical-align: middle;
}

.ops-table tr:last-child td {
  border-bottom: none;
}

.ops-player-row {
  cursor: pointer;
  transition: background 0.1s;
}

.ops-player-row:hover td {
  background: rgba(255, 255, 255, 0.03);
}

.ops-player-name {
  font-weight: 500;
  margin-right: 0.5rem;
}

.ops-table-secondary {
  color: var(--color-text-secondary);
}

.ops-table-empty {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-secondary);
}

.badge-sm {
  font-size: 0.68rem;
  padding: 0.1rem 0.4rem;
}
</style>
