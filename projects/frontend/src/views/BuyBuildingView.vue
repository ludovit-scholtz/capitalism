<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import UiStateLoading from '@/components/ui/UiStateLoading.vue'
import UiStateError from '@/components/ui/UiStateError.vue'
import BuyBuildingSteps from '@/components/buildings/BuyBuildingSteps.vue'
import type { City } from '@/types'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const { selectedCityId } = storeToRefs(auth)

const loading = ref(false)
const error = ref<string | null>(null)
const cities = ref<City[]>([])
const selectedType = ref('')

const companyId = computed(() => route.params.companyId as string)

const buildingTypes = ['MINE', 'FACTORY', 'SALES_SHOP', 'RESEARCH_DEVELOPMENT', 'APARTMENT', 'COMMERCIAL', 'MEDIA_HOUSE', 'BANK', 'EXCHANGE', 'POWER_PLANT']

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }

  if (!auth.player) {
    await auth.fetchMe()
  }

  loading.value = true
  try {
    const citiesData = await gqlRequest<{ cities: City[] }>('{ cities { id name countryCode currencyCode population } }')
    cities.value = citiesData.cities

    if (!companyId.value) {
      error.value = t('cityMap.noCompany')
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load cities'
  } finally {
    loading.value = false
  }

  // Pre-select building type if passed as query param
  const typeParam = route.query.type as string | undefined
  if (typeParam && buildingTypes.includes(typeParam)) {
    selectedType.value = typeParam
  }

  // Auto-select city based on existing building usage
  if (!selectedCityId.value && cities.value.length > 0) {
    const cityUsage = new Map<string, number>()
    for (const company of auth.player?.companies ?? []) {
      for (const building of company.buildings ?? []) {
        cityUsage.set(building.cityId, (cityUsage.get(building.cityId) ?? 0) + 1)
      }
    }
    let preferredCityId: string | null = null
    let preferredCount = -1
    for (const [cityId, count] of cityUsage.entries()) {
      if (count > preferredCount) {
        preferredCount = count
        preferredCityId = cityId
      }
    }
    const preferredCity = preferredCityId ? cities.value.find((c) => c.id === preferredCityId) : null
    const fallbackCity = preferredCity ?? cities.value[0]
    if (fallbackCity) {
      auth.switchCity(fallbackCity.id)
    }
  }
})

function handlePurchase(buildingId: string, buildingType: string) {
  router.push(buildingType === 'BANK' ? `/bank/${buildingId}` : `/building/${buildingId}`)
}
</script>

<template>
  <div class="container py-8 px-4 max-w-4xl">
    <!-- Back nav -->
    <div class="mb-6">
      <RouterLink to="/dashboard" class="inline-flex items-center gap-1.5 text-sm text-muted hover:text-brand no-underline transition-colors">
        <span>←</span> {{ t('buildingDetail.backToDashboard') }}
      </RouterLink>
    </div>

    <!-- Main card -->
    <div class="bg-card border border-divider rounded-xl p-8">
      <h1 class="text-2xl font-bold mb-6">{{ t('buildings.title') }}</h1>

      <UiStateError v-if="error" :message="error" :retry-label="t('common.retry')" @retry="() => router.go(0)" />

      <UiStateLoading v-if="loading" :label="t('common.loading')" />

      <template v-else>
        <!-- Building market discovery banner -->
        <div class="mb-6 flex items-center justify-between gap-4 px-4 py-3 bg-[rgba(0,71,255,0.05)] border border-[rgba(0,71,255,0.2)] rounded-lg">
          <div class="flex items-center gap-2">
            <span class="text-xl">🏪</span>
            <div>
              <span class="font-semibold text-sm">{{ t('buildingMarket.title') }}</span>
              <span class="text-muted text-xs ml-2">{{ t('buildingMarket.subtitle') }}</span>
            </div>
          </div>
          <RouterLink to="/buildings/market" class="btn-market-link shrink-0 text-sm font-semibold text-brand hover:underline no-underline">
            {{ t('buildingMarket.tabMarket') }} →
          </RouterLink>
        </div>

        <!-- Step 1: Building type (hidden when ?type= is pre-selected in URL) -->
        <div v-if="!route.query.type || !buildingTypes.includes(String(route.query.type))" class="mb-8">
          <h2 class="text-lg font-semibold mb-3">{{ t('buildings.selectType') }}</h2>
          <div class="grid gap-3 [grid-template-columns:repeat(auto-fill,minmax(180px,1fr))]">
            <button
              v-for="bType in buildingTypes"
              :key="bType"
              class="type-card flex flex-col items-center gap-1.5 py-5 px-3 border-2 border-divider rounded-lg bg-page cursor-pointer transition-all duration-200 text-body text-center hover:border-brand hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
              :class="selectedType === bType ? 'selected border-brand bg-[rgba(0,71,255,0.08)] ring-1 ring-brand' : ''"
              @click="selectedType = bType"
            >
              <span class="text-3xl leading-none">{{ t(`buildings.typeIcons.${bType}`) }}</span>
              <span class="font-bold text-sm">{{ t(`buildings.types.${bType}`) }}</span>
              <span class="text-[0.6875rem] text-muted leading-snug">{{ t(`buildings.typeDescriptions.${bType}`) }}</span>
            </button>
          </div>
        </div>

        <!-- Steps 2 + 3: name, bank config, lot selection, purchase -->
        <BuyBuildingSteps
          v-if="selectedType"
          :cities="cities"
          :company-id="companyId"
          :selected-type="selectedType"
          @purchased="handlePurchase"
        />
      </template>
    </div>
  </div>
</template>
