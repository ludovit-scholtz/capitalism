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
    const data = await gqlRequest<{ cities: City[] }>(`{ cities { id name countryCode currencyCode latitude longitude population } }`)
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
  <div class="city-selector flex flex-col gap-2">
    <label for="city-select" class="city-selector-label text-sm font-medium text-muted"
      ><span>{{ $t('common.city') }}</span></label
    >
    <select
      id="city-select"
      :value="selectedCityId || ''"
      @change="handleCityChange"
      :disabled="loading || error !== null"
      class="city-selector-select cursor-pointer rounded border border-divider bg-page px-3 py-2 text-sm text-body transition-colors hover:border-brand focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/20 disabled:cursor-not-allowed disabled:opacity-60"
    >
      <option v-if="!selectedCityId" value="">{{ $t('common.selectCity') }}</option>
      <option v-for="city in cities" :key="city.id" :value="city.id">{{ city.name }} ({{ city.currencyCode }})</option>
    </select>
    <div v-if="error" class="city-selector-error text-xs text-danger">{{ error }}</div>
  </div>
</template>
