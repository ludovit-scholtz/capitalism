<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'

const { t } = useI18n()

interface CityResource {
  abundance: number
  resourceType: {
    name: string
    slug: string
    emoji: string
  }
}

interface City {
  id: string
  name: string
  countryCode: string
  population: number
  currencyCode: string
  baseSalaryPerManhour: number
  latitude: number
  longitude: number
  resources: CityResource[]
}

const cities = ref<City[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const CITIES_QUERY = `
  {
    cities {
      id
      name
      countryCode
      population
      currencyCode
      baseSalaryPerManhour
      latitude
      longitude
      resources {
        abundance
        resourceType {
          name
          slug
          emoji
        }
      }
    }
  }
`

const TOP_RESOURCES_DISPLAY_LIMIT = 4

const COUNTRY_FLAGS: Record<string, string> = {
  SK: '🇸🇰',
  CZ: '🇨🇿',
  AT: '🇦🇹',
  US: '🇺🇸',
  GB: '🇬🇧',
  CN: '🇨🇳',
  IN: '🇮🇳',
  DE: '🇩🇪',
  PL: '🇵🇱',
}

function getFlag(countryCode: string): string {
  return COUNTRY_FLAGS[countryCode] ?? '🌍'
}

function formatPopulation(population: number): string {
  if (population >= 1_000_000) {
    return `${(population / 1_000_000).toFixed(1)}M`
  }
  if (population >= 1_000) {
    return `${(population / 1_000).toFixed(0)}K`
  }
  return population.toString()
}

function topResources(city: City): CityResource[] {
  return [...city.resources].sort((a, b) => b.abundance - a.abundance).slice(0, TOP_RESOURCES_DISPLAY_LIMIT)
}

async function fetchCities() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ cities: City[] }>(CITIES_QUERY)
    cities.value = data.cities.sort((a, b) => b.population - a.population)
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('cities.loadFailed')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void fetchCities()
})
</script>

<template>
  <div class="min-h-screen">
    <!-- Hero -->
    <div
      class="border-b border-divider py-12 text-center"
      style="background: linear-gradient(160deg, #0d1117 0%, rgba(0, 71, 255, 0.14) 100%)"
    >
      <div class="container mx-auto px-4">
        <p class="text-[0.75rem] font-bold tracking-[0.1em] uppercase text-brand mb-2">
          {{ t('cities.eyebrow') }}
        </p>
        <h1
          class="text-4xl sm:text-[2.25rem] font-extrabold mb-3"
          style="
            background: linear-gradient(135deg, var(--color-primary), var(--color-secondary));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
          "
        >
          {{ t('cities.title') }}
        </h1>
        <p class="text-base text-muted max-w-[540px] mx-auto">{{ t('cities.subtitle') }}</p>
      </div>
    </div>

    <!-- Content -->
    <div class="container mx-auto px-4 pt-10 pb-16">
      <!-- Loading state -->
      <div v-if="loading" class="flex flex-col items-center gap-3 py-12 text-center">
        <span class="text-4xl">⏳</span>
        <p>{{ t('common.loading') }}</p>
      </div>

      <!-- Error state -->
      <div v-else-if="error" class="flex flex-col items-center gap-3 py-12 text-center text-bad">
        <span class="text-4xl">⚠️</span>
        <p>{{ error }}</p>
        <button class="btn btn-secondary" @click="fetchCities">{{ t('common.tryAgain') }}</button>
      </div>

      <!-- Cities grid -->
      <div
        v-else
        class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 max-w-[1100px] mx-auto"
      >
        <div
          v-for="city in cities"
          :key="city.id"
          class="city-card bg-card border border-divider rounded-xl p-5 hover:border-brand transition-colors"
        >
          <!-- City header -->
          <div class="flex items-center gap-3 mb-4">
            <span class="text-3xl" :aria-label="city.countryCode">{{ getFlag(city.countryCode) }}</span>
            <div class="flex-1 min-w-0">
              <h2 class="text-lg font-bold truncate">{{ city.name }}</h2>
              <p class="text-[0.78rem] text-muted">{{ city.countryCode }} · {{ city.currencyCode }}</p>
            </div>
          </div>

          <!-- Key metrics -->
          <div class="metrics-grid grid grid-cols-2 gap-2 mb-4">
            <div class="metric-item bg-surface rounded-lg p-2.5">
              <p class="text-[0.7rem] text-muted uppercase tracking-wide mb-0.5">
                {{ t('cities.population') }}
              </p>
              <p class="text-base font-bold">{{ formatPopulation(city.population) }}</p>
            </div>
            <div class="metric-item bg-surface rounded-lg p-2.5">
              <p class="text-[0.7rem] text-muted uppercase tracking-wide mb-0.5">
                {{ t('cities.baseSalary') }}
              </p>
              <p class="text-base font-bold">
                {{ city.baseSalaryPerManhour }} {{ city.currencyCode }}/h
              </p>
            </div>
          </div>

          <!-- Top resources -->
          <div v-if="city.resources.length > 0">
            <p class="text-[0.7rem] text-muted uppercase tracking-wide mb-2">
              {{ t('cities.topResources') }}
            </p>
            <div class="flex flex-wrap gap-1.5">
              <span
                v-for="res in topResources(city)"
                :key="res.resourceType.slug"
                class="resource-chip inline-flex items-center gap-1 bg-surface border border-divider rounded-full px-2 py-0.5 text-[0.72rem]"
                :title="`${res.resourceType.name} — ${Math.round(res.abundance * 100)}%`"
              >
                <span>{{ res.resourceType.emoji }}</span>
                <span class="text-muted">{{ Math.round(res.abundance * 100) }}%</span>
              </span>
            </div>
          </div>

          <!-- City map link -->
          <RouterLink
            :to="`/city/${city.id}`"
            class="mt-4 flex items-center justify-center gap-2 btn btn-secondary btn-sm w-full"
          >
            🗺️ {{ t('cities.viewMap') }}
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.city-card {
  display: flex;
  flex-direction: column;
}

.metric-item {
  text-align: center;
}

.resource-chip {
  cursor: default;
  user-select: none;
}
</style>
