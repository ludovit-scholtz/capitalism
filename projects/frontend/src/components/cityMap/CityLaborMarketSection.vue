<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { useAuthStore } from '@/stores/auth'
import { formatGameTickTime } from '@/lib/gameTime'
import {
  CITY_LABOR_MARKETS_QUERY,
  formatWagePressure,
  payPosition,
  strikeRisk,
  unemploymentPercent,
  type CityLaborMarket,
} from '@/lib/laborMarket'

const props = defineProps<{
  cityId: string
  cityName: string
}>()

const { t, locale } = useI18n()
const auth = useAuthStore()

const markets = ref<CityLaborMarket[]>([])
const loading = ref(false)
const error = ref(false)

const companyId = computed(() => (auth.player?.activeAccountType === 'COMPANY' ? (auth.player.activeCompanyId ?? null) : null))
const market = computed(() => markets.value.find((row) => row.cityId === props.cityId) ?? null)
const hasCompany = computed(() => !!companyId.value && market.value?.payRatio != null)
const risk = computed(() => (market.value?.payRatio != null ? strikeRisk(market.value.payRatio) : 'NONE'))
const position = computed(() => (market.value?.payRatio != null ? payPosition(market.value.payRatio) : null))
const staffingPercent = computed(() => Math.round((market.value?.staffingFillRate ?? 1) * 100))
const settingsLink = computed(() => (companyId.value ? `/company/${companyId.value}/settings` : null))

function formatMultiplier(value: number | null | undefined): string {
  return value == null ? '—' : `×${value.toFixed(2)}`
}

let latestRequest = 0

async function load() {
  // Only the most recent request may write state, so a slow earlier response cannot show another company's figures.
  const requestId = ++latestRequest
  loading.value = true
  error.value = false
  try {
    const data = await gqlRequest<{ cityLaborMarkets: CityLaborMarket[] }>(CITY_LABOR_MARKETS_QUERY, { companyId: companyId.value })
    if (requestId !== latestRequest) return
    markets.value = data.cityLaborMarkets
  } catch {
    if (requestId !== latestRequest) return
    error.value = true
  } finally {
    if (requestId === latestRequest) loading.value = false
  }
}

watch(() => [props.cityId, companyId.value], load, { immediate: true })
</script>

<template>
  <section class="city-labor-section" data-testid="city-labor-market">
    <h3 class="labor-heading">{{ t('cityMap.labor.title') }}</h3>
    <p class="labor-subtitle">{{ t('cityMap.labor.subtitle', { city: cityName }) }}</p>

    <p v-if="loading && !market" class="labor-status">{{ t('cityMap.labor.loading') }}</p>
    <p v-else-if="error || !market" class="labor-status">{{ t('cityMap.labor.unavailable') }}</p>
    <template v-else>
      <dl class="labor-metrics">
        <div>
          <dt>{{ t('cityMap.labor.unemployment') }}</dt>
          <dd data-testid="labor-unemployment">{{ unemploymentPercent(market.unemploymentRate) }}%</dd>
        </div>
        <div>
          <dt>{{ t('cityMap.labor.wagePressure') }}</dt>
          <dd data-testid="labor-wage-pressure">{{ formatWagePressure(market.wageIndex) }}</dd>
        </div>
        <div>
          <dt>{{ t('cityMap.labor.averagePay') }}</dt>
          <dd>{{ formatMultiplier(market.averageSalaryMultiplier) }}</dd>
        </div>
        <div>
          <dt>{{ t('cityMap.labor.staffing') }}</dt>
          <dd>{{ staffingPercent }}%</dd>
        </div>
        <template v-if="hasCompany">
          <div>
            <dt>{{ t('cityMap.labor.yourPay') }}</dt>
            <dd data-testid="labor-your-pay">{{ formatMultiplier(market.yourSalaryMultiplier) }}</dd>
          </div>
          <div>
            <dt>{{ t('cityMap.labor.payRatio') }}</dt>
            <dd data-testid="labor-pay-ratio">{{ market.payRatio?.toFixed(2) }}</dd>
          </div>
        </template>
      </dl>

      <p v-if="!hasCompany" class="labor-status">{{ t('cityMap.labor.noCompany') }}</p>
      <template v-else>
        <p class="labor-position">
          <span v-if="position">{{ t(`cityMap.labor.pay.${position}`) }}</span>
          <span class="labor-risk" :class="`labor-risk-${risk.toLowerCase()}`" data-testid="labor-strike-risk">{{ t(`cityMap.labor.risk.${risk}`) }}</span>
        </p>
        <p v-if="risk !== 'NONE'" class="labor-hint" data-testid="labor-hint">
          {{ t('cityMap.labor.hint', { ratio: `${Math.round((market.payRatio ?? 1) * 100)}%` }) }}
          <RouterLink v-if="settingsLink" :to="settingsLink">{{ t('cityMap.labor.openSettings') }}</RouterLink>
        </p>

        <div v-if="market.affectedBuildings.length" class="labor-affected" data-testid="labor-affected-buildings">
          <h4>{{ t('cityMap.labor.affectedTitle') }}</h4>
          <ul>
            <li v-for="building in market.affectedBuildings" :key="building.buildingId">
              <RouterLink :to="`/building/${building.buildingId}`">{{ building.name }}</RouterLink>
              <span v-if="building.onStrike" class="labor-risk labor-risk-high" :title="`${building.strikeUntilTick}`">{{
                t('cityMap.labor.onStrike', { time: formatGameTickTime(building.strikeUntilTick ?? 0, locale) })
              }}</span>
              <span v-else class="labor-risk labor-risk-elevated">{{ t('cityMap.labor.understaffed', { percent: Math.round(building.staffingFactor * 100) }) }}</span>
            </li>
          </ul>
        </div>
      </template>
    </template>
  </section>
</template>

<style scoped>
.city-labor-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.labor-heading {
  margin: 0;
  font-size: 1.125rem;
  font-weight: 700;
}

.labor-subtitle,
.labor-status {
  margin: 0;
  color: var(--color-text-secondary);
}

.labor-metrics {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
  gap: 0.75rem;
  margin: 0;
}

.labor-metrics dt {
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

.labor-metrics dd {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 700;
}

.labor-position {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  margin: 0;
}

.labor-risk {
  padding: 0.125rem 0.625rem;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 600;
  background: rgba(34, 197, 94, 0.18);
}

.labor-risk-elevated {
  background: rgba(245, 158, 11, 0.22);
}

.labor-risk-high {
  background: rgba(239, 68, 68, 0.25);
}

.labor-hint {
  margin: 0;
}

.labor-affected ul {
  margin: 0.25rem 0 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.labor-affected li {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
}

.labor-affected h4 {
  margin: 0;
}
</style>
