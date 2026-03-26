<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import type { City, Building } from '@/types'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const companyId = computed(() => route.params.companyId as string)

const loading = ref(false)
const error = ref<string | null>(null)
const cities = ref<City[]>([])
const selectedType = ref('')
const selectedCityId = ref('')
const buildingName = ref('')
const submitting = ref(false)

const buildingTypes = [
  'MINE',
  'FACTORY',
  'SALES_SHOP',
  'RESEARCH_DEVELOPMENT',
  'APARTMENT',
  'COMMERCIAL',
  'MEDIA_HOUSE',
  'BANK',
  'EXCHANGE',
  'POWER_PLANT',
]

const canSubmit = computed(
  () => !!selectedType.value && !!selectedCityId.value && !!buildingName.value.trim(),
)

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }
  loading.value = true
  try {
    const data = await gqlRequest<{ cities: City[] }>(
      '{ cities { id name countryCode population } }',
    )
    cities.value = data.cities
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load cities'
  } finally {
    loading.value = false
  }
})

async function buyBuilding() {
  if (!canSubmit.value) return
  submitting.value = true
  error.value = null
  try {
    await gqlRequest<{ placeBuilding: Building }>(
      `mutation PlaceBuilding($input: PlaceBuildingInput!) {
        placeBuilding(input: $input) {
          id name type level
        }
      }`,
      {
        input: {
          companyId: companyId.value,
          cityId: selectedCityId.value,
          type: selectedType.value,
          name: buildingName.value,
        },
      },
    )
    router.push('/dashboard')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to buy building'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="buy-building-view container">
    <div class="page-nav">
      <RouterLink to="/dashboard" class="back-link">
        <span>←</span> {{ t('buildingDetail.backToDashboard') }}
      </RouterLink>
    </div>

    <div class="buy-card">
      <h1>{{ t('buildings.title') }}</h1>

      <div v-if="error" class="error-message" role="alert">{{ error }}</div>

      <div v-if="loading" class="loading">{{ t('common.loading') }}</div>

      <template v-else>
        <!-- Step 1: Select building type -->
        <div class="form-section">
          <h2>{{ t('buildings.selectType') }}</h2>
          <div class="type-grid">
            <button
              v-for="bType in buildingTypes"
              :key="bType"
              class="type-card"
              :class="{ selected: selectedType === bType }"
              @click="selectedType = bType"
            >
              <span class="type-icon">{{ t(`buildings.typeIcons.${bType}`) }}</span>
              <span class="type-name">{{ t(`buildings.types.${bType}`) }}</span>
              <span class="type-desc">{{ t(`buildings.typeDescriptions.${bType}`) }}</span>
            </button>
          </div>
        </div>

        <!-- Step 2: City and name -->
        <div v-if="selectedType" class="form-section">
          <div class="form-row">
            <div class="form-group">
              <label for="buildingName">{{ t('buildings.buildingName') }}</label>
              <input
                id="buildingName"
                v-model="buildingName"
                type="text"
                required
                maxlength="200"
                :placeholder="t('buildings.buildingNamePlaceholder')"
              />
            </div>

            <div class="form-group">
              <label for="city">{{ t('buildings.selectCity') }}</label>
              <div class="city-select-grid">
                <button
                  v-for="city in cities"
                  :key="city.id"
                  class="city-option"
                  :class="{ selected: selectedCityId === city.id }"
                  @click="selectedCityId = city.id"
                >
                  <span class="city-name">{{ city.name }}</span>
                  <span class="city-country">{{ city.countryCode }}</span>
                </button>
              </div>
            </div>
          </div>

          <div class="action-bar">
            <button
              class="btn btn-primary btn-lg"
              :disabled="!canSubmit || submitting"
              @click="buyBuilding"
            >
              {{ submitting ? t('common.loading') : t('buildings.buyNow') }}
            </button>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.buy-building-view {
  padding: 2rem 1rem;
  max-width: 900px;
}

.page-nav {
  margin-bottom: 1.5rem;
}

.back-link {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  text-decoration: none;
  transition: color 0.15s;
}

.back-link:hover {
  color: var(--color-primary);
  text-decoration: none;
}

.buy-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 2rem;
}

.buy-card h1 {
  font-size: 1.5rem;
  margin-bottom: 1.5rem;
}

.form-section {
  margin-bottom: 2rem;
}

.form-section h2 {
  font-size: 1.125rem;
  margin-bottom: 1rem;
}

.type-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 0.75rem;
}

.type-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.375rem;
  padding: 1.25rem 0.75rem;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  cursor: pointer;
  transition: all 0.2s ease;
  color: var(--color-text);
  text-align: center;
}

.type-card:hover {
  border-color: var(--color-primary);
  transform: translateY(-2px);
}

.type-card.selected {
  border-color: var(--color-primary);
  background: rgba(0, 71, 255, 0.08);
  box-shadow: 0 0 0 1px var(--color-primary);
}

.type-icon {
  font-size: 2rem;
}

.type-name {
  font-weight: 700;
  font-size: 0.875rem;
}

.type-desc {
  font-size: 0.6875rem;
  color: var(--color-text-secondary);
  line-height: 1.3;
}

.form-row {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group label {
  font-size: 0.875rem;
  font-weight: 600;
}

.form-group input {
  padding: 0.75rem 1rem;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  color: var(--color-text);
  font-size: 1rem;
}

.form-group input:focus {
  outline: none;
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(0, 71, 255, 0.15);
}

.form-group input::placeholder {
  color: var(--color-text-secondary);
}

.city-select-grid {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.city-option {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.5rem 1rem;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  color: var(--color-text);
  cursor: pointer;
  transition: all 0.15s ease;
}

.city-option:hover {
  border-color: var(--color-primary);
}

.city-option.selected {
  border-color: var(--color-primary);
  background: rgba(0, 71, 255, 0.08);
}

.city-name {
  font-weight: 600;
  font-size: 0.875rem;
}

.city-country {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.action-bar {
  display: flex;
  justify-content: flex-end;
  margin-top: 1.5rem;
}

.btn-lg {
  padding: 0.75rem 2rem;
  font-size: 1rem;
  font-weight: 600;
}

.error-message {
  background: rgba(248, 113, 113, 0.1);
  border: 1px solid rgba(248, 113, 113, 0.3);
  color: var(--color-danger);
  padding: 0.75rem 1rem;
  border-radius: var(--radius-md);
  font-size: 0.875rem;
  margin-bottom: 1rem;
}

.loading {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-secondary);
}

@media (max-width: 640px) {
  .type-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
