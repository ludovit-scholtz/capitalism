<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AccountOption } from '@/lib/accountContext'
import type { City } from '@/types'

defineProps<{
  cities: City[]
  selectedCityId: string | null
  buildingCountByCity: Record<string, number>
  accountOptions: AccountOption[]
  switchingKey: string | null
  formatCurrency: (value: number) => string
  ccLabel: (code: string) => string
}>()

const emit = defineEmits<{
  selectCity: [id: string]
  switchAccount: [option: AccountOption]
}>()

const { t } = useI18n()
</script>

<template>
  <div class="ctx-panel" role="menu" aria-label="City and account switcher">
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
        @click="emit('selectCity', city.id)"
      >
        <span class="ctx-cc-badge" aria-hidden="true">{{ ccLabel(city.countryCode) }}</span>
        <span class="ctx-city-info">
          <span class="ctx-city-option-name">{{ city.name }}</span>
          <span class="ctx-city-option-meta">{{ city.currencyCode }}</span>
        </span>
        <span class="ctx-city-right">
          <span v-if="buildingCountByCity[city.id]" class="ctx-city-building-count" :title="t('dashboard.buildings')">
            <font-awesome-icon :icon="['fas', 'building']" aria-hidden="true" />
            {{ buildingCountByCity[city.id] }}
          </span>
        </span>
      </button>
    </div>

    <div class="ctx-divider" aria-hidden="true"></div>

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
        @click="emit('switchAccount', option)"
      >
        <span class="ctx-acc-icon" aria-hidden="true">
          <font-awesome-icon :icon="option.accountType === 'PERSON' ? ['fas', 'user'] : ['fas', 'building']" />
        </span>
        <span class="ctx-acc-main">
          <span class="ctx-acc-name">{{ option.name }}</span>
          <span class="ctx-acc-type">
            {{ option.accountType === 'PERSON' ? t('accountSwitcher.personalAccountHint') : t('accountSwitcher.companyAccountHint') }}
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
</template>

<style scoped>
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

.ctx-city-right {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 0.25rem;
  margin-left: auto;
}

.ctx-city-building-count {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  font-size: 0.6875rem;
  font-weight: 700;
  color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 14%, transparent);
  border-radius: 999px;
  padding: 0.15rem 0.45rem;
}

.ctx-divider {
  height: 1px;
  background: var(--color-border);
  margin: 0.4rem 0;
}

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

@media (max-width: 480px) {
  .ctx-panel {
    right: -3rem;
    width: min(22rem, calc(100vw - 0.5rem));
  }

  .ctx-cities {
    grid-template-columns: 1fr;
  }
}
</style>