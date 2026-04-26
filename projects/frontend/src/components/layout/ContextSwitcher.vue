<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { buildAccountOptions, getActiveAccountName } from '@/lib/accountContext'
import type { City } from '@/types'

const emit = defineEmits<{ switched: [] }>()

const { t, locale } = useI18n()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)
const route = useRoute()
const router = useRouter()

const root = ref<HTMLElement | null>(null)
const isOpen = ref(false)
const switchingKey = ref<string | null>(null)
const cities = ref<City[]>([])

// ── City helpers ──────────────────────────────────────────────────────────────

function countryFlag(code: string): string {
  return [...code.toUpperCase()]
    .map((c) => String.fromCodePoint(0x1f1e6 - 65 + c.charCodeAt(0)))
    .join('')
}

async function loadCities() {
  try {
    const data = await gqlRequest<{ cities: City[] }>(
      `{ cities { id name countryCode currencyCode latitude longitude population } }`,
    )
    if (data?.cities) {
      cities.value = data.cities
    }
    if (!selectedCityId.value && cities.value.length > 0) {
      const first = cities.value[0]
      if (first) auth.switchCity(first.id)
    }
  } catch {
    /* ignore — best effort */
  }
}

const selectedCity = computed(() => cities.value.find((c) => c.id === selectedCityId.value))

/** Building count in each city for the currently active company (or all companies if PERSON). */
const buildingCountByCity = computed<Record<string, number>>(() => {
  const result: Record<string, number> = {}
  const companies = auth.player?.companies ?? []
  const activeCompanyId = auth.player?.activeCompanyId
  const filtered =
    auth.player?.activeAccountType === 'COMPANY' && activeCompanyId
      ? companies.filter((c) => c.id === activeCompanyId)
      : companies
  for (const company of filtered) {
    for (const b of company.buildings ?? []) {
      result[b.cityId] = (result[b.cityId] ?? 0) + 1
    }
  }
  return result
})

function selectCity(id: string) {
  auth.switchCity(id)
}

// ── Account helpers ───────────────────────────────────────────────────────────

const accountOptions = computed(() => buildAccountOptions(auth.player, auth.player?.companies ?? []))
const activeAccountName = computed(
  () => getActiveAccountName(auth.player, auth.player?.companies ?? []) ?? auth.player?.displayName ?? '',
)
const activeAccountBadgeKey = computed(() =>
  auth.player?.activeAccountType === 'COMPANY' ? 'accountSwitcher.companyBadge' : 'accountSwitcher.personBadge',
)

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function getRouteTarget(accountType: 'PERSON' | 'COMPANY', companyId: string | null) {
  const routeName = typeof route.name === 'string' ? route.name : null
  if (routeName === 'ledger' || routeName === 'company-settings' || routeName === 'buy-building') {
    if (accountType !== 'COMPANY' || !companyId) return { name: 'dashboard' as const }
    return { name: routeName, params: { ...route.params, companyId } }
  }
  if (routeName === 'building-detail' || routeName === 'bank-management') {
    return { name: 'dashboard' as const }
  }
  return null
}

async function switchAccount(accountType: 'PERSON' | 'COMPANY', companyId: string | null, key: string) {
  if (!auth.player) return
  if (
    auth.player.activeAccountType === accountType &&
    (accountType !== 'COMPANY' || auth.player.activeCompanyId === companyId)
  ) {
    closePanel()
    emit('switched')
    return
  }
  switchingKey.value = key
  try {
    await auth.switchAccountContext(accountType, companyId)
    closePanel()
    emit('switched')
    const routeTarget = getRouteTarget(accountType, companyId)
    if (routeTarget) await router.replace(routeTarget)
  } finally {
    switchingKey.value = null
  }
}

// ── Panel open/close ──────────────────────────────────────────────────────────

function togglePanel() {
  if (!auth.player) return
  isOpen.value = !isOpen.value
}

function closePanel() {
  isOpen.value = false
}

function handlePointerDown(event: MouseEvent) {
  if (!root.value || !(event.target instanceof Node) || root.value.contains(event.target)) return
  closePanel()
}

function handleEscape(event: KeyboardEvent) {
  if (event.key === 'Escape') closePanel()
}

onMounted(() => {
  loadCities()
  document.addEventListener('mousedown', handlePointerDown)
  document.addEventListener('keydown', handleEscape)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', handlePointerDown)
  document.removeEventListener('keydown', handleEscape)
})

defineExpose({ closePanel })
</script>

<template>
  <div v-if="auth.player" ref="root" class="ctx-switcher">
    <!-- Trigger -->
    <button
      type="button"
      class="ctx-trigger"
      :aria-expanded="isOpen"
      aria-haspopup="menu"
      :aria-label="`${selectedCity?.name ?? '…'} · ${activeAccountName}`"
      @click="togglePanel"
    >
      <!-- City segment -->
      <span class="ctx-city-seg">
        <span class="ctx-flag" aria-hidden="true">
          {{ selectedCity ? countryFlag(selectedCity.countryCode) : '🌍' }}
        </span>
        <span class="ctx-city-name">{{ selectedCity?.name ?? '…' }}</span>
      </span>

      <!-- Divider -->
      <span class="ctx-sep" aria-hidden="true">·</span>

      <!-- Account segment -->
      <span class="ctx-account-seg">
        <span class="ctx-account-name">{{ activeAccountName }}</span>
        <span class="ctx-account-badge">{{ t(activeAccountBadgeKey) }}</span>
      </span>

      <!-- Chevron -->
      <span class="ctx-chevron" :class="{ open: isOpen }" aria-hidden="true">
        <font-awesome-icon :icon="['fas', 'chevron-down']" />
      </span>
    </button>

    <!-- Dropdown panel -->
    <div v-if="isOpen" class="ctx-panel" role="menu" aria-label="City and account switcher">
      <!-- City section -->
      <div class="ctx-section-header">
        <font-awesome-icon :icon="['fas', 'location-dot']" />
        {{ t('common.city') }}
      </div>
      <div class="ctx-cities">
        <button
          v-for="city in cities"
          :key="city.id"
          type="button"
          class="ctx-city-option"
          :class="{ active: city.id === selectedCityId }"
          role="menuitemradio"
          :aria-checked="city.id === selectedCityId"
          @click="selectCity(city.id)"
        >
          <span class="ctx-city-flag" aria-hidden="true">{{ countryFlag(city.countryCode) }}</span>
          <span class="ctx-city-info">
            <span class="ctx-city-option-name">{{ city.name }}</span>
            <span class="ctx-city-option-meta">{{ city.countryCode }} · {{ city.currencyCode }}</span>
          </span>
          <span v-if="buildingCountByCity[city.id]" class="ctx-city-building-count" :title="t('dashboard.buildings')">
            {{ buildingCountByCity[city.id] }}
            <font-awesome-icon :icon="['fas', 'building']" class="ctx-city-building-icon" aria-hidden="true" />
          </span>
          <span v-if="city.id === selectedCityId" class="ctx-active-dot" aria-hidden="true"></span>
        </button>
      </div>

      <!-- Divider -->
      <div class="ctx-divider" aria-hidden="true"></div>

      <!-- Account section -->
      <div class="ctx-section-header">
        <font-awesome-icon :icon="['fas', 'building']" />
        {{ t('accountSwitcher.menuLabel') }}
      </div>
      <div class="ctx-accounts">
        <button
          v-for="option in accountOptions"
          :key="option.key"
          type="button"
          class="ctx-account-option"
          :class="{ active: option.isActive }"
          role="menuitemradio"
          :aria-checked="option.isActive"
          :disabled="switchingKey === option.key"
          @click="switchAccount(option.accountType, option.companyId, option.key)"
        >
          <span class="ctx-acc-icon" aria-hidden="true">
            <font-awesome-icon :icon="option.accountType === 'PERSON' ? ['fas', 'user'] : ['fas', 'building']" />
          </span>
          <span class="ctx-acc-main">
            <span class="ctx-acc-name">{{ option.name }}</span>
            <span class="ctx-acc-type">
              {{
                option.accountType === 'PERSON'
                  ? t('accountSwitcher.personalAccountHint')
                  : t('accountSwitcher.companyAccountHint')
              }}
            </span>
          </span>
          <span class="ctx-acc-meta">
            <span v-if="option.cash != null" class="ctx-acc-cash">
              {{ formatCurrency(option.cash) }}
            </span>
            <span v-if="option.isActive" class="ctx-active-label">{{ t('accountSwitcher.active') }}</span>
          </span>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* ── Wrapper ─────────────────────────────────────────────────────────────── */
.ctx-switcher {
  position: relative;
}

/* ── Trigger ─────────────────────────────────────────────────────────────── */
.ctx-trigger {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.375rem 0.625rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm, 6px);
  background: var(--color-surface-hover);
  color: var(--color-text);
  cursor: pointer;
  transition:
    border-color 0.15s,
    background 0.15s;
  min-width: 0;
  max-width: 17rem;
}

.ctx-trigger:hover,
.ctx-trigger:focus-visible {
  border-color: var(--color-primary);
  background: var(--color-surface-raised);
  outline: none;
}

.ctx-city-seg {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  min-width: 0;
}

.ctx-flag {
  font-size: 1rem;
  line-height: 1;
  flex-shrink: 0;
}

.ctx-city-name {
  font-size: 0.8125rem;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ctx-sep {
  color: var(--color-text-secondary);
  font-size: 0.75rem;
  flex-shrink: 0;
}

.ctx-account-seg {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  min-width: 0;
  flex: 1;
}

.ctx-account-name {
  font-size: 0.8125rem;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-width: 0;
  flex: 1;
}

.ctx-account-badge {
  font-size: 0.625rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.ctx-chevron {
  font-size: 0.6875rem;
  color: var(--color-text-secondary);
  flex-shrink: 0;
  transition: transform 0.2s;
}

.ctx-chevron.open {
  transform: rotate(180deg);
}

/* ── Panel ───────────────────────────────────────────────────────────────── */
.ctx-panel {
  position: absolute;
  top: calc(100% + 0.4rem);
  right: 0;
  width: min(22rem, calc(100vw - 1rem));
  padding: 0.5rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 8px);
  background: var(--color-surface);
  box-shadow: 0 16px 40px rgba(0, 0, 0, 0.26);
  z-index: 200;
}

/* ── Section header ──────────────────────────────────────────────────────── */
.ctx-section-header {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.35rem 0.5rem;
  font-size: 0.6875rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-secondary);
}

/* ── Cities ──────────────────────────────────────────────────────────────── */
.ctx-cities {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.2rem;
  margin-bottom: 0.2rem;
}

.ctx-city-option {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.625rem;
  border: 1px solid transparent;
  border-radius: var(--radius-sm, 6px);
  background: transparent;
  color: var(--color-text);
  cursor: pointer;
  text-align: left;
  transition:
    background 0.12s,
    border-color 0.12s;
  position: relative;
}

.ctx-city-option:hover,
.ctx-city-option:focus-visible {
  background: var(--color-surface-hover);
  outline: none;
}

.ctx-city-option.active {
  background: var(--color-surface-hover);
  border-color: var(--color-primary);
}

.ctx-city-flag {
  font-size: 1.1rem;
  line-height: 1;
  flex-shrink: 0;
}

.ctx-city-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 0;
}

.ctx-city-option-name {
  font-size: 0.8125rem;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ctx-city-option-meta {
  font-size: 0.6875rem;
  color: var(--color-text-secondary);
}

.ctx-active-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-primary);
  position: absolute;
  top: 0.4rem;
  right: 0.4rem;
}

.ctx-city-building-count {
  display: inline-flex;
  align-items: center;
  gap: 0.2rem;
  font-size: 0.6875rem;
  font-weight: 700;
  color: var(--color-primary);
  background: rgba(var(--color-primary-rgb, 212, 163, 0), 0.12);
  border-radius: 999px;
  padding: 0.1rem 0.4rem;
  flex-shrink: 0;
  margin-left: auto;
}

.ctx-city-building-icon {
  font-size: 0.6rem;
  opacity: 0.85;
}

/* ── Divider ─────────────────────────────────────────────────────────────── */
.ctx-divider {
  height: 1px;
  background: var(--color-border);
  margin: 0.4rem 0;
}

/* ── Accounts ────────────────────────────────────────────────────────────── */
.ctx-accounts {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.ctx-account-option {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.55rem 0.625rem;
  border: 0;
  border-radius: var(--radius-sm, 6px);
  background: transparent;
  color: var(--color-text);
  cursor: pointer;
  text-align: left;
  transition: background 0.12s;
}

.ctx-account-option:hover,
.ctx-account-option:focus-visible {
  background: var(--color-surface-hover);
  outline: none;
}

.ctx-account-option.active {
  background: var(--color-surface-hover);
}

.ctx-account-option:disabled {
  opacity: 0.6;
  cursor: wait;
}

.ctx-acc-icon {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  width: 1.25rem;
  text-align: center;
  flex-shrink: 0;
}

.ctx-acc-main {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  flex: 1;
  min-width: 0;
}

.ctx-acc-name {
  font-size: 0.875rem;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ctx-acc-type {
  font-size: 0.6875rem;
  color: var(--color-text-secondary);
}

.ctx-acc-meta {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.1rem;
  flex-shrink: 0;
}

.ctx-acc-cash {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.ctx-active-label {
  font-size: 0.6875rem;
  font-weight: 700;
  color: var(--color-primary);
}

/* ── Responsive overrides ────────────────────────────────────────────────── */
@media (max-width: 480px) {
  .ctx-city-name,
  .ctx-sep {
    display: none;
  }

  .ctx-account-name {
    max-width: 6rem;
  }

  .ctx-panel {
    right: -3rem;
    width: min(22rem, calc(100vw - 0.5rem));
  }

  .ctx-cities {
    grid-template-columns: 1fr;
  }
}
</style>
