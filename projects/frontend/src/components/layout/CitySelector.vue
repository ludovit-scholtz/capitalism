<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import type { City } from '@/types'

const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

const cities = ref<City[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

async function loadCities() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ cities: City[] }>(
      `{ cities { id name countryCode currencyCode latitude longitude population } }`,
    )
    if (data?.cities) {
      cities.value = data.cities
    }

    // If no city is selected yet, select the first one
    if (!selectedCityId.value && cities.value.length > 0) {
      const firstCity = cities.value[0]
      if (firstCity) {
        auth.switchCity(firstCity.id)
      }
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load cities'
  } finally {
    loading.value = false
  }
}

function handleCityChange(event: Event) {
  const target = event.target as HTMLSelectElement | null
  if (target?.value) {
    auth.switchCity(target.value)
  }
}

onMounted(() => {
  if (cities.value.length === 0) {
    loadCities()
  }
})
</script>

<template>
  <div class="city-selector">
    <label for="city-select" class="city-selector-label"
      ><span>{{ $t('common.city') }}</span></label
    >
    <select
      id="city-select"
      :value="selectedCityId || ''"
      @change="handleCityChange"
      :disabled="loading || error !== null"
      class="city-selector-select"
    >
      <option v-if="!selectedCityId" value="">{{ $t('common.selectCity') }}</option>
      <option v-for="city in cities" :key="city.id" :value="city.id">
        {{ city.name }} ({{ city.currencyCode }})
      </option>
    </select>
    <div v-if="error" class="city-selector-error">{{ error }}</div>
  </div>
</template>

<style scoped>
.city-selector {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.city-selector-label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-secondary, #666);
}

.city-selector-select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border, #ddd);
  border-radius: 4px;
  background-color: var(--color-background, white);
  color: var(--color-text, #000);
  font-size: 0.875rem;
  cursor: pointer;
  transition: border-color 0.2s;
}

.city-selector-select:hover:not(:disabled) {
  border-color: var(--color-primary, #0066cc);
}

.city-selector-select:focus {
  outline: none;
  border-color: var(--color-primary, #0066cc);
  box-shadow: 0 0 0 2px rgba(0, 102, 204, 0.1);
}

.city-selector-select:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.city-selector-error {
  font-size: 0.75rem;
  color: var(--color-error, #d32f2f);
}
</style>
