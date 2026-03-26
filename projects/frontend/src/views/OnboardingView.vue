<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import type { City, ProductType, OnboardingResult } from '@/types'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const step = ref(1)
const loading = ref(false)
const error = ref<string | null>(null)

// Data
const industries = ref<string[]>([])
const cities = ref<City[]>([])
const products = ref<ProductType[]>([])

// Selections
const selectedIndustry = ref('')
const selectedCityId = ref('')
const selectedProductId = ref('')
const companyName = ref('')

const selectedCity = computed(() => cities.value.find((c) => c.id === selectedCityId.value))

const canProceedStep1 = computed(() => !!selectedIndustry.value)
const canProceedStep2 = computed(() => !!selectedCityId.value)
const canProceedStep3 = computed(() => !!selectedProductId.value && !!companyName.value)

onMounted(async () => {
  if (!auth.isAuthenticated) {
    router.push('/login')
    return
  }
  try {
    loading.value = true
    const [industriesData, citiesData] = await Promise.all([
      gqlRequest<{ starterIndustries: { industries: string[] } }>(
        '{ starterIndustries { industries } }',
      ),
      gqlRequest<{ cities: City[] }>(
        '{ cities { id name countryCode latitude longitude population resources { resourceType { name } abundance } } }',
      ),
    ])
    industries.value = industriesData.starterIndustries.industries
    cities.value = citiesData.cities
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load data'
  } finally {
    loading.value = false
  }
})

async function loadProducts() {
  loading.value = true
  try {
    const data = await gqlRequest<{ productTypes: ProductType[] }>(
      `query Products($industry: String) {
        productTypes(industry: $industry) {
          id name slug industry basePrice baseCraftTicks description
          recipes { resourceType { name } quantity }
        }
      }`,
      { industry: selectedIndustry.value },
    )
    products.value = data.productTypes
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Failed to load products'
  } finally {
    loading.value = false
  }
}

function nextStep() {
  if (step.value === 1 && canProceedStep1.value) {
    loadProducts()
    step.value = 2
  } else if (step.value === 2 && canProceedStep2.value) {
    step.value = 3
  }
}

function prevStep() {
  if (step.value > 1) step.value--
}

async function completeOnboarding() {
  loading.value = true
  error.value = null
  try {
    await gqlRequest<{ completeOnboarding: OnboardingResult }>(
      `mutation CompleteOnboarding($input: OnboardingInput!) {
        completeOnboarding(input: $input) {
          company { id name cash }
          factory { id name type }
          salesShop { id name type }
          selectedProduct { name industry }
        }
      }`,
      {
        input: {
          industry: selectedIndustry.value,
          cityId: selectedCityId.value,
          productTypeId: selectedProductId.value,
          companyName: companyName.value,
        },
      },
    )
    await auth.fetchMe()
    router.push('/dashboard')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : 'Onboarding failed'
  } finally {
    loading.value = false
  }
}

function formatIndustry(industry: string): string {
  return industry.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}
</script>

<template>
  <div class="onboarding-view container">
    <div class="onboarding-card">
      <h1>{{ t('onboarding.title') }}</h1>
      <p class="subtitle">{{ t('onboarding.subtitle') }}</p>

      <div class="progress-bar">
        <div class="progress-step" :class="{ active: step >= 1, done: step > 1 }">1</div>
        <div class="progress-line" :class="{ active: step > 1 }"></div>
        <div class="progress-step" :class="{ active: step >= 2, done: step > 2 }">2</div>
        <div class="progress-line" :class="{ active: step > 2 }"></div>
        <div class="progress-step" :class="{ active: step >= 3 }">3</div>
      </div>

      <div v-if="error" class="error-message" role="alert">{{ error }}</div>

      <!-- Step 1: Choose Industry -->
      <div v-if="step === 1" class="step-content">
        <h2>{{ t('onboarding.step1Title') }}</h2>
        <div class="industry-grid">
          <button
            v-for="ind in industries"
            :key="ind"
            class="industry-card"
            :class="{ selected: selectedIndustry === ind }"
            @click="selectedIndustry = ind"
          >
            <span class="industry-icon">{{
              ind === 'FURNITURE' ? '🪑' : ind === 'FOOD_PROCESSING' ? '🍞' : '💊'
            }}</span>
            <span class="industry-name">{{ formatIndustry(ind) }}</span>
          </button>
        </div>
        <button class="btn btn-primary" :disabled="!canProceedStep1" @click="nextStep">
          {{ t('common.next') }}
        </button>
      </div>

      <!-- Step 2: Choose City -->
      <div v-if="step === 2" class="step-content">
        <h2>{{ t('onboarding.step2Title') }}</h2>
        <div class="city-grid">
          <button
            v-for="city in cities"
            :key="city.id"
            class="city-card"
            :class="{ selected: selectedCityId === city.id }"
            @click="selectedCityId = city.id"
          >
            <span class="city-name">{{ city.name }}</span>
            <span class="city-info">{{ city.countryCode }} · Pop. {{ city.population.toLocaleString() }}</span>
            <span class="city-resources">
              {{ city.resources.map((r) => r.resourceType.name).join(', ') }}
            </span>
          </button>
        </div>
        <div class="step-actions">
          <button class="btn btn-secondary" @click="prevStep">{{ t('common.back') }}</button>
          <button class="btn btn-primary" :disabled="!canProceedStep2" @click="nextStep">
            {{ t('common.next') }}
          </button>
        </div>
      </div>

      <!-- Step 3: Choose Product & Name Company -->
      <div v-if="step === 3" class="step-content">
        <h2>{{ t('onboarding.step3Title') }}</h2>

        <div class="form-group">
          <label for="companyName">{{ t('onboarding.companyName') }}</label>
          <input id="companyName" v-model="companyName" type="text" required maxlength="200" />
        </div>

        <div class="form-group">
          <label for="product">{{ t('onboarding.selectProduct') }}</label>
          <div class="product-grid">
            <button
              v-for="prod in products"
              :key="prod.id"
              class="product-card"
              :class="{ selected: selectedProductId === prod.id }"
              @click="selectedProductId = prod.id"
            >
              <span class="product-name">{{ prod.name }}</span>
              <span class="product-price">${{ prod.basePrice }}</span>
              <span class="product-recipe">
                {{ prod.recipes.map((r) => `${r.quantity}× ${r.resourceType.name}`).join(', ') }}
              </span>
            </button>
          </div>
        </div>

        <div v-if="selectedCity" class="summary">
          <h3>{{ t('onboarding.summary') }}</h3>
          <p>{{ t('onboarding.summaryText', { company: companyName, city: selectedCity.name, industry: formatIndustry(selectedIndustry) }) }}</p>
        </div>

        <div class="step-actions">
          <button class="btn btn-secondary" @click="prevStep">{{ t('common.back') }}</button>
          <button
            class="btn btn-primary"
            :disabled="!canProceedStep3 || loading"
            @click="completeOnboarding"
          >
            {{ loading ? t('common.loading') : t('onboarding.startPlaying') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.onboarding-view {
  display: flex;
  justify-content: center;
  padding: 2rem 1rem;
}

.onboarding-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 2.5rem;
  width: 100%;
  max-width: 640px;
}

.onboarding-card h1 {
  font-size: 1.75rem;
  margin-bottom: 0.25rem;
}

.subtitle {
  color: var(--color-text-secondary);
  margin-bottom: 1.5rem;
}

.progress-bar {
  display: flex;
  align-items: center;
  gap: 0;
  margin-bottom: 2rem;
}

.progress-step {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  font-size: 0.875rem;
  background: var(--color-surface-raised);
  color: var(--color-text-secondary);
  border: 2px solid var(--color-border);
}

.progress-step.active {
  background: var(--color-primary);
  color: #fff;
  border-color: var(--color-primary);
}

.progress-step.done {
  background: var(--color-success);
  border-color: var(--color-success);
  color: #fff;
}

.progress-line {
  flex: 1;
  height: 2px;
  background: var(--color-border);
}

.progress-line.active {
  background: var(--color-success);
}

.step-content h2 {
  font-size: 1.25rem;
  margin-bottom: 1rem;
}

.industry-grid,
.city-grid,
.product-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 0.75rem;
  margin-bottom: 1.5rem;
}

.industry-card,
.city-card,
.product-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1rem;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-bg);
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s;
  color: var(--color-text);
  text-align: center;
}

.industry-card:hover,
.city-card:hover,
.product-card:hover {
  border-color: var(--color-primary);
}

.industry-card.selected,
.city-card.selected,
.product-card.selected {
  border-color: var(--color-primary);
  background: rgba(19, 127, 236, 0.1);
}

.industry-icon {
  font-size: 2rem;
}

.industry-name,
.city-name,
.product-name {
  font-weight: 600;
  font-size: 0.9375rem;
}

.city-info,
.product-price {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.city-resources,
.product-recipe {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  margin-bottom: 1rem;
}

.form-group label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.form-group input {
  padding: 0.625rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-bg);
  color: var(--color-text);
  font-size: 0.9375rem;
}

.summary {
  background: var(--color-surface-raised);
  border-radius: var(--radius-md);
  padding: 1rem;
  margin-bottom: 1rem;
}

.summary h3 {
  font-size: 0.9375rem;
  margin-bottom: 0.5rem;
}

.summary p {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

.step-actions {
  display: flex;
  gap: 0.75rem;
  justify-content: flex-end;
}

.error-message {
  background: rgba(248, 113, 113, 0.1);
  color: var(--color-danger);
  padding: 0.75rem;
  border-radius: var(--radius-sm);
  font-size: 0.875rem;
  margin-bottom: 1rem;
}
</style>
