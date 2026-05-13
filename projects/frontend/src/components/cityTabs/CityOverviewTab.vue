<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { City, BuildingLot, Company } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  city: City
  lots: BuildingLot[]
  companies: Company[]
}>()

const availableLots = computed(() => props.lots.filter((lot) => !lot.ownerCompanyId).length)
</script>

<template>
  <section class="city-overview-tab">
    <h2 class="section-heading">{{ t('cityMap.tabs.overviewHeading') }}</h2>
    <p class="section-subtitle">{{ t('cityMap.tabs.overviewDescription', { city: city.name }) }}</p>

    <div class="overview-grid">
      <article class="overview-card">
        <span class="label">{{ t('cityMap.tabs.population') }}</span>
        <strong>{{ city.population.toLocaleString() }}</strong>
      </article>
      <article class="overview-card">
        <span class="label">{{ t('cityMap.tabs.currency') }}</span>
        <strong>{{ city.currencyCode }}</strong>
      </article>
      <article class="overview-card">
        <span class="label">{{ t('cityMap.tabs.availableLots') }}</span>
        <strong>{{ availableLots }}</strong>
      </article>
      <article class="overview-card">
        <span class="label">{{ t('cityMap.tabs.activeCompanies') }}</span>
        <strong>{{ companies.length }}</strong>
      </article>
    </div>
  </section>
</template>

<style scoped>
.city-overview-tab {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.section-heading {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
}

.section-subtitle {
  margin: 0;
  color: var(--color-text-secondary);
}

.overview-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
}

.overview-card {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  background: var(--color-card);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 0.875rem 1rem;
}

.label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}
</style>
