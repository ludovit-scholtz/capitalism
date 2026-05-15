<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { computeCityUnlockProgress, formatEstimatedTicksLabel } from '@/lib/cityExpansion'
import { formatMoney } from '@/lib/currencyFormat'
import type { CityUnlockStatus } from '@/types'

defineProps<{
  statuses: CityUnlockStatus[]
}>()

const { locale, t } = useI18n()

function formatAmount(amount: number, currency: string): string {
  return formatMoney(amount, currency, locale.value)
}
</script>

<template>
  <section v-if="statuses.length > 0" class="statement-card city-expansion-panel">
    <div class="city-expansion-panel__header">
      <div>
        <p class="city-expansion-panel__eyebrow">{{ t('cityExpansion.eyebrow') }}</p>
        <h2 class="statement-title">🌍 {{ t('cityExpansion.title') }}</h2>
      </div>
      <p class="city-expansion-panel__hint">{{ t('cityExpansion.subtitle') }}</p>
    </div>

    <div class="city-expansion-grid">
      <article v-for="status in statuses" :key="status.cityId" class="city-expansion-card">
        <div class="city-expansion-card__top">
          <div>
            <h3 class="city-expansion-card__title">{{ status.cityName }}</h3>
            <p class="city-expansion-card__currency">{{ status.countryCode }} · {{ status.currency }}</p>
          </div>
          <span
            class="city-expansion-card__badge"
            :class="status.isUnlocked ? 'city-expansion-card__badge--unlocked' : 'city-expansion-card__badge--locked'"
          >
            {{ status.isUnlocked ? t('cityExpansion.unlockedBadge') : t('cityExpansion.lockedBadge') }}
          </span>
        </div>

        <div class="city-expansion-progress">
          <div class="city-expansion-progress__track" aria-hidden="true">
            <span class="city-expansion-progress__fill" :style="{ width: `${computeCityUnlockProgress(status)}%` }"></span>
          </div>
          <span class="city-expansion-progress__label">
            {{
              status.isUnlocked
                ? t('cityExpansion.progressComplete')
                : t('cityExpansion.progressLabel', { percent: computeCityUnlockProgress(status) })
            }}
          </span>
        </div>

        <dl class="city-expansion-metrics">
          <div>
            <dt>{{ t('cityExpansion.currentNetWorth') }}</dt>
            <dd>{{ formatAmount(status.currentNetWorth, status.currency) }}</dd>
          </div>
          <div>
            <dt>{{ t('cityExpansion.requiredNetWorth') }}</dt>
            <dd>{{ formatAmount(status.requiredNetWorth, status.currency) }}</dd>
          </div>
          <div>
            <dt>{{ t('cityExpansion.estimatedTicks') }}</dt>
            <dd>
              {{
                status.isUnlocked
                  ? t('cityExpansion.availableNow')
                  : status.estimatedTicksToUnlock != null
                    ? t('cityExpansion.estimatedTicksValue', { ticks: formatEstimatedTicksLabel(status.estimatedTicksToUnlock, locale) })
                    : t('cityExpansion.estimateUnavailable')
              }}
            </dd>
          </div>
        </dl>
      </article>
    </div>
  </section>
</template>

<style scoped>
.city-expansion-panel {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.city-expansion-panel__header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
  flex-wrap: wrap;
}

.city-expansion-panel__eyebrow {
  margin: 0 0 0.3rem;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-primary);
}

.city-expansion-panel__hint {
  margin: 0;
  max-width: 22rem;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
}

.city-expansion-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr));
  gap: 1rem;
}

.city-expansion-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: 1rem;
  background: color-mix(in srgb, var(--color-card) 88%, transparent);
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.city-expansion-card__top {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  align-items: flex-start;
}

.city-expansion-card__title {
  margin: 0;
  font-size: 1rem;
}

.city-expansion-card__currency {
  margin: 0.2rem 0 0;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}

.city-expansion-card__badge {
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
  white-space: nowrap;
}

.city-expansion-card__badge--unlocked {
  background: rgba(34, 197, 94, 0.15);
  color: #16a34a;
}

.city-expansion-card__badge--locked {
  background: rgba(245, 158, 11, 0.15);
  color: #d97706;
}

.city-expansion-progress {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.city-expansion-progress__track {
  width: 100%;
  height: 0.7rem;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.18);
  overflow: hidden;
}

.city-expansion-progress__fill {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, var(--color-primary), var(--color-secondary));
}

.city-expansion-progress__label {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.city-expansion-metrics {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(8rem, 1fr));
  gap: 0.75rem;
  margin: 0;
}

.city-expansion-metrics dt {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  margin-bottom: 0.2rem;
}

.city-expansion-metrics dd {
  margin: 0;
  font-size: 0.88rem;
  font-weight: 600;
}
</style>
