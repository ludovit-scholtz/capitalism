<template>
  <section class="media-houses-section" aria-labelledby="media-houses-heading">
    <h2 id="media-houses-heading" class="section-heading">📺 {{ t('cityMap.mediaHouses.title') }}</h2>
    <p class="section-subtitle">{{ t('cityMap.mediaHouses.subtitle') }}</p>
    <div v-if="loading" class="media-houses-loading">{{ t('common.loading') }}</div>
    <div v-else-if="mediaHouses.length === 0" class="media-houses-empty">
      <p>{{ t('cityMap.mediaHouses.empty') }}</p>
      <p class="hint">{{ t('cityMap.mediaHouses.emptyHint') }}</p>
    </div>
    <div v-else class="media-houses-grid">
      <div v-for="mh in mediaHouses" :key="mh.id" class="media-house-card" :class="{ 'mh-offline': mh.powerStatus === 'OFFLINE', 'mh-construction': mh.isUnderConstruction }">
        <div class="mh-channel-icon"><span v-if="mh.mediaType === 'TV'">📺</span><span v-else-if="mh.mediaType === 'RADIO'">📻</span><span v-else>📰</span></div>
        <div class="mh-info">
          <strong class="mh-name">{{ mh.name }}</strong>
          <div class="mh-badges">
            <span class="mh-type-badge">{{ mh.mediaType ?? '?' }}</span
            ><span v-if="mh.isGovernmentOwned" class="mh-gov-badge" :title="t('cityMap.mediaHouses.governmentOwned')">{{ t('cityMap.mediaHouses.govBadge') }}</span
            ><span v-if="mh.isUnderConstruction" class="mh-status-badge construction"> {{ t('cityMap.mediaHouses.underConstruction') }} </span
            ><span v-else-if="mh.powerStatus === 'OFFLINE'" class="mh-status-badge offline"> {{ t('cityMap.mediaHouses.offline') }} </span>
          </div>
          <div class="mh-owner">{{ t('cityMap.mediaHouses.owner') }}: {{ mh.ownerCompanyName }}</div>
          <div class="mh-effectiveness">
            {{ t('cityMap.mediaHouses.effectiveness') }}: <strong>×{{ mh.effectivenessMultiplier.toFixed(1) }}</strong
            ><span class="effectiveness-hint">
              {{ mh.mediaType === 'TV' ? t('cityMap.mediaHouses.tvHint') : mh.mediaType === 'RADIO' ? t('cityMap.mediaHouses.radioHint') : t('cityMap.mediaHouses.newspaperHint') }}
            </span>
          </div>
          <div class="mh-ranking">
            {{ t('cityMap.mediaHouses.contentRanking') }}: <strong>{{ mh.contentRanking.toFixed(0) }}%</strong>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { CityMediaHouseInfo } from '@/types'

defineProps<{
  mediaHouses: CityMediaHouseInfo[]
  loading: boolean
}>()

const { t } = useI18n()
</script>

<style scoped>
.media-houses-section {
  margin-top: 2.5rem;
}

.section-heading {
  font-size: 1.25rem;
  font-weight: 700;
  margin-bottom: 0.375rem;
  color: var(--color-text-primary);
}

.section-subtitle {
  font-size: 0.875rem;
  color: var(--color-text-muted);
  margin-bottom: 1rem;
}

.media-houses-loading,
.media-houses-empty {
  padding: 1.5rem;
  text-align: center;
  color: var(--color-text-muted);
}

.media-houses-empty .hint {
  font-size: 0.8rem;
  margin-top: 0.25rem;
}

.media-houses-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 1rem;
}

.media-house-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 1rem;
  display: flex;
  gap: 0.75rem;
  transition: box-shadow 0.15s;
}

.media-house-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.media-house-card.mh-offline {
  opacity: 0.6;
}

.media-house-card.mh-construction {
  border-color: var(--color-warning, #f59e0b);
}

.mh-channel-icon {
  font-size: 2rem;
  flex-shrink: 0;
  display: flex;
  align-items: flex-start;
  padding-top: 0.25rem;
}

.mh-info {
  flex: 1;
  min-width: 0;
}

.mh-name {
  display: block;
  font-weight: 600;
  font-size: 0.95rem;
  margin-bottom: 0.375rem;
}

.mh-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin-bottom: 0.375rem;
}

.mh-type-badge {
  background: var(--color-accent-alpha, rgba(59, 130, 246, 0.15));
  color: var(--color-accent, #3b82f6);
  font-size: 0.7rem;
  font-weight: 600;
  padding: 0.1em 0.5em;
  border-radius: 4px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.mh-gov-badge {
  background: rgba(245, 158, 11, 0.15);
  color: #f59e0b;
  font-size: 0.7rem;
  font-weight: 600;
  padding: 0.1em 0.5em;
  border-radius: 4px;
}

.mh-status-badge {
  font-size: 0.7rem;
  font-weight: 600;
  padding: 0.1em 0.5em;
  border-radius: 4px;
}

.mh-status-badge.construction {
  background: rgba(245, 158, 11, 0.15);
  color: #f59e0b;
}

.mh-status-badge.offline {
  background: rgba(239, 68, 68, 0.15);
  color: #ef4444;
}

.mh-owner {
  font-size: 0.8rem;
  color: var(--color-text-muted);
  margin-bottom: 0.25rem;
}

.mh-effectiveness {
  font-size: 0.85rem;
  margin-bottom: 0.25rem;
}

.effectiveness-hint {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  margin-left: 0.5em;
}

.mh-ranking {
  font-size: 0.85rem;
}
</style>
