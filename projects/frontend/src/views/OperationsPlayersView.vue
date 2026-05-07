<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useGameAdminStore } from '@/stores/gameAdmin'
import {
  filterAndSortOperationsPlayers,
  type OperationsPlayerListItem,
  type OperationsPlayersSortKey,
} from '@/lib/operationsDashboard'

const { t, locale } = useI18n()
const router = useRouter()
const adminStore = useGameAdminStore()

const searchQuery = ref('')
const selectedCity = ref('ALL')
const selectedCompany = ref('ALL')
const sortKey = ref<OperationsPlayersSortKey>('balance')
const sortAsc = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)

const players = computed<OperationsPlayerListItem[]>(() => adminStore.dashboard?.players ?? [])

const cityOptions = computed(() =>
  [...new Set(players.value.flatMap((player) => player.cityNames ?? []))].sort((left, right) => left.localeCompare(right)),
)

const companyOptions = computed(() =>
  [...new Set(players.value.flatMap((player) => player.companies.map((company) => company.name)))].sort((left, right) => left.localeCompare(right)),
)

const filteredPlayers = computed(() =>
  filterAndSortOperationsPlayers(players.value, {
    searchQuery: searchQuery.value,
    selectedCity: selectedCity.value,
    selectedCompany: selectedCompany.value,
    sortKey: sortKey.value,
    sortAsc: sortAsc.value,
  }),
)

function formatDate(value: string | null | undefined) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function toggleSort(key: OperationsPlayersSortKey) {
  if (sortKey.value === key) {
    sortAsc.value = !sortAsc.value
    return
  }

  sortKey.value = key
  sortAsc.value = false
}

async function loadPlayers() {
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

function goToDetail(player: OperationsPlayerListItem) {
  void router.push({ name: 'operations-player-detail', params: { id: player.id } })
}

onMounted(loadPlayers)
</script>

<template>
  <div class="ops-players">
    <div class="ops-section-header">
      <div>
        <h2>{{ t('operations.players.title') }}</h2>
        <p>{{ t('operations.players.subtitle') }}</p>
      </div>
    </div>

    <div class="ops-controls">
      <input
        v-model="searchQuery"
        class="form-input ops-search"
        :placeholder="t('operations.players.searchPlaceholder')"
      />
      <select v-model="selectedCity" class="form-select ops-filter">
        <option value="ALL">{{ t('operations.players.filterAllCities') }}</option>
        <option v-for="city in cityOptions" :key="city" :value="city">{{ city }}</option>
      </select>
      <select v-model="selectedCompany" class="form-select ops-filter">
        <option value="ALL">{{ t('operations.players.filterAllCompanies') }}</option>
        <option v-for="company in companyOptions" :key="company" :value="company">{{ company }}</option>
      </select>
      <div class="ops-sort-group">
        <button
          v-for="option in [
            { key: 'balance', label: t('operations.players.sortBalance') },
            { key: 'companyEquity', label: t('operations.players.sortCompanyEquity') },
            { key: 'joinDate', label: t('operations.players.sortJoinDate') },
          ]"
          :key="option.key"
          type="button"
          class="ops-sort-btn"
          :class="{ active: sortKey === option.key }"
          @click="toggleSort(option.key as OperationsPlayersSortKey)"
        >
          {{ option.label }}<template v-if="sortKey === option.key">{{ sortAsc ? ' ↑' : ' ↓' }}</template>
        </button>
      </div>
    </div>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="loadPlayers">{{ t('common.retry') }}</button>
    </div>
    <template v-else>
      <div class="ops-table-wrap">
        <table class="ops-table" aria-label="Player list">
          <thead>
            <tr>
              <th>{{ t('operations.players.sortName') }}</th>
              <th>{{ t('operations.players.sortEmail') }}</th>
              <th>{{ t('operations.players.cities') }}</th>
              <th>{{ t('operations.players.companies') }}</th>
              <th>{{ t('operations.players.joined') }}</th>
              <th>{{ t('operations.players.personalBalance') }}</th>
              <th>{{ t('operations.players.companyEquity') }}</th>
              <th>{{ t('operations.players.sortLastSeen') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filteredPlayers.length === 0">
              <td colspan="9" class="ops-table-empty">{{ t('operations.players.noPlayers') }}</td>
            </tr>
            <tr
              v-for="player in filteredPlayers"
              :key="player.id"
              class="ops-player-row"
              @click="goToDetail(player)"
            >
              <td>
                <div class="ops-player-name">{{ player.displayName }}</div>
                <span v-if="player.role === 'ADMIN'" class="badge badge-warning badge-sm">ADMIN</span>
              </td>
              <td class="ops-table-secondary">{{ player.email }}</td>
              <td class="ops-table-secondary">{{ (player.cityNames ?? []).join(', ') || t('common.notAvailable') }}</td>
              <td class="ops-table-secondary">{{ player.companies.map((company) => company.name).join(', ') }}</td>
              <td class="ops-table-secondary">{{ formatDate(player.createdAtUtc) }}</td>
              <td>{{ formatCurrency(player.personalCash) }}</td>
              <td>{{ formatCurrency(player.totalCompanyEquity ?? player.totalCompanyCash) }}</td>
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

.ops-section-header p {
  color: var(--color-text-secondary);
  margin-top: 0.25rem;
}

.ops-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
}

.ops-search {
  flex: 1;
  min-width: 240px;
}

.ops-filter {
  min-width: 180px;
}

.ops-sort-group {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.ops-sort-btn {
  padding: 0.45rem 0.8rem;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.ops-sort-btn.active {
  color: var(--color-text);
  background: rgba(255, 255, 255, 0.06);
}

.ops-loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}

.ops-error {
  padding: 1.5rem;
  display: grid;
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

.ops-table th,
.ops-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  text-align: left;
  vertical-align: top;
}

.ops-player-row {
  cursor: pointer;
}

.ops-player-row:hover td {
  background: rgba(255, 255, 255, 0.03);
}

.ops-player-name {
  font-weight: 600;
  margin-bottom: 0.2rem;
}

.ops-table-secondary {
  color: var(--color-text-secondary);
}

.ops-table-empty {
  text-align: center;
  color: var(--color-text-secondary);
  padding: 2rem;
}

.badge-sm {
  font-size: 0.68rem;
  padding: 0.1rem 0.4rem;
}
</style>
