<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import ContextSwitcherPanel from '@/components/layout/ContextSwitcherPanel.vue'
import CountryFlag from '@/components/common/CountryFlag.vue'
import { selectMainCity } from '@/lib/cityContext'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { buildAccountOptions, getActiveAccountName, type AccountOption } from '@/lib/accountContext'
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

async function loadCities() {
  try {
    const data = await gqlRequest<{ cities: City[] }>(`{ cities { id name countryCode currencyCode latitude longitude population } }`)
    if (data?.cities) {
      cities.value = data.cities
    }
    if (cities.value.length > 0) {
      const activeCity = selectedCityId.value ? cities.value.find((city) => city.id === selectedCityId.value) : null
      if (!activeCity) {
        const buildings = auth.player?.companies.flatMap((company) => company.buildings ?? []) ?? []
        const preferredCity = selectMainCity(cities.value, buildings)
        const fallbackCity = preferredCity ?? cities.value[0]
        if (fallbackCity) {
          auth.switchCity(fallbackCity.id)
        }
      }
    }
  } catch {
    /* ignore — best effort */
  }
}

const selectedCity = computed(() => cities.value.find((c) => c.id === selectedCityId.value) ?? cities.value[0] ?? null)

/** Building count in each city for the currently active company (or all companies if PERSON). */
const buildingCountByCity = computed<Record<string, number>>(() => {
  const result: Record<string, number> = {}
  const companies = auth.player?.companies ?? []
  const activeCompanyId = auth.player?.activeCompanyId
  const filtered = auth.player?.activeAccountType === 'COMPANY' && activeCompanyId ? companies.filter((c) => c.id === activeCompanyId) : companies
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
const activeAccountName = computed(() => getActiveAccountName(auth.player, auth.player?.companies ?? []) ?? auth.player?.displayName ?? '')
const activeAccountBadgeKey = computed(() => (auth.player?.activeAccountType === 'COMPANY' ? 'accountSwitcher.companyBadge' : 'accountSwitcher.personBadge'))

function formatCurrency(value: number): string {
  return new Intl.NumberFormat(locale.value, {
    style: 'currency',
    currency: selectedCity.value?.currencyCode ?? 'EUR',
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
  if (auth.player.activeAccountType === accountType && (accountType !== 'COMPANY' || auth.player.activeCompanyId === companyId)) {
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

function handleSwitchAccount(option: AccountOption) {
  return switchAccount(option.accountType, option.companyId, option.key)
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
    <button type="button" class="ctx-trigger" :aria-expanded="isOpen" aria-haspopup="menu" :aria-label="`${selectedCity?.name ?? '…'} · ${activeAccountName}`" @click="togglePanel">
      <!-- City segment -->
      <span class="ctx-city-seg">
        <CountryFlag
          v-if="selectedCity"
          :country-code="selectedCity.countryCode"
          size="sm"
          :title="selectedCity.countryCode"
          aria-hidden="true"
        />
        <span v-else class="ctx-cc-badge" aria-hidden="true">??</span>
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

    <ContextSwitcherPanel
      v-if="isOpen"
      :cities="cities"
      :selected-city-id="selectedCityId"
      :building-count-by-city="buildingCountByCity"
      :account-options="accountOptions"
      :switching-key="switchingKey"
      :format-currency="formatCurrency"
      @select-city="selectCity"
      @switch-account="handleSwitchAccount"
    />
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
  height: 2.25rem;
  padding: 0 0.75rem;
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

/* Country code badge — replaces emoji flag (unreliable on Windows) */
.ctx-cc-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 1.5rem;
  height: 1.125rem;
  border-radius: 3px;
  background: var(--color-surface-raised, rgba(255, 255, 255, 0.08));
  border: 1px solid var(--color-border);
  font-size: 0.5625rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
  line-height: 1;
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

/* ── Responsive overrides ────────────────────────────────────────────────── */
@media (max-width: 480px) {
  .ctx-city-name,
  .ctx-sep,
  .ctx-account-name,
  .ctx-account-badge {
    display: none;
  }

  .ctx-trigger {
    max-width: none;
    padding: 0 0.55rem;
  }
}
</style>
