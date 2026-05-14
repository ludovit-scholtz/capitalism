<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatMoney } from '@/lib/currencyFormat'
import type { City, CityCompetitorEntry } from '@/types'

const { t } = useI18n()

const props = defineProps<{
  city: City
  cityId: string
}>()

const loading = ref(false)
const error = ref<string | null>(null)
const competitors = ref<CityCompetitorEntry[]>([])

function archetypeBadgeClass(archetype: string | null): string {
  if (!archetype) return 'arch-human'
  if (archetype === 'RAW_MATERIALS') return 'arch-raw'
  if (archetype === 'MANUFACTURER') return 'arch-manufacturer'
  if (archetype === 'RETAILER') return 'arch-retailer'
  if (archetype === 'FINANCIER') return 'arch-financier'
  return 'arch-conglomerate'
}

function trendSymbol(trend: CityCompetitorEntry['trend']): string {
  if (trend === 'UP') return '↗'
  if (trend === 'DOWN') return '↘'
  return '—'
}

async function loadCompetitors() {
  loading.value = true
  error.value = null
  try {
    const result = await gqlRequest<{ cityCompetitors: CityCompetitorEntry[] }>(
      `query CityCompetitors($cityId: UUID!, $lastNTicks: Int!) {
        cityCompetitors(cityId: $cityId, lastNTicks: $lastNTicks) {
          companyId
          companyName
          isNpc
          npcCompanyId
          archetype
          buildingCount
          estimatedRevenueLastTicks
          marketSharePercent
          trend
          marketShareByCategory {
            category
            sharePercent
          }
        }
      }`,
      { cityId: props.cityId, lastNTicks: 10 },
    )
    competitors.value = result.cityCompetitors ?? []
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

watch(
  () => props.cityId,
  () => {
    void loadCompetitors()
  },
)

onMounted(() => {
  void loadCompetitors()
})
</script>

<template>
  <section class="competitors-tab">
    <header class="competitors-header">
      <h2>{{ t('competitors.title') }}</h2>
      <p>{{ t('competitors.subtitle') }}</p>
    </header>

    <div v-if="loading" class="competitors-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="competitors-error">{{ error }}</div>
    <div v-else-if="competitors.length === 0" class="competitors-empty">{{ t('competitors.empty') }}</div>

    <div v-else class="competitors-table-wrap">
      <table class="competitors-table" :aria-label="t('competitors.title')">
        <thead>
          <tr>
            <th>{{ t('competitors.company') }}</th>
            <th>{{ t('competitors.archetype') }}</th>
            <th>{{ t('competitors.buildings') }}</th>
            <th>{{ t('competitors.revenue') }}</th>
            <th>{{ t('competitors.marketShare') }}</th>
            <th>{{ t('competitors.trend') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="entry in competitors" :key="entry.companyId">
            <td>{{ entry.companyName }}</td>
            <td>
              <span class="arch-badge" :class="archetypeBadgeClass(entry.archetype)">
                {{ entry.archetype ?? t('competitors.human') }}
              </span>
            </td>
            <td>{{ entry.buildingCount }}</td>
            <td>{{ formatMoney(entry.estimatedRevenueLastTicks, city.currencyCode) }}</td>
            <td>
              <div class="share-cell">
                <span>{{ entry.marketSharePercent.toFixed(1) }}%</span>
                <span class="share-bar"><span class="share-fill" :style="{ width: `${Math.min(100, Math.max(0, entry.marketSharePercent))}%` }" /></span>
              </div>
            </td>
            <td class="trend-cell">{{ trendSymbol(entry.trend) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.competitors-tab {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}
.competitors-header p {
  margin-top: 0.25rem;
  color: var(--color-text-secondary);
}
.competitors-loading,
.competitors-error,
.competitors-empty {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
}
.competitors-table-wrap {
  overflow-x: auto;
}
.competitors-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 760px;
}
.competitors-table th,
.competitors-table td {
  padding: 0.65rem 0.75rem;
  border-bottom: 1px solid var(--color-border);
  text-align: left;
}
.arch-badge {
  display: inline-block;
  border-radius: 999px;
  padding: 0.15rem 0.5rem;
  font-size: 0.72rem;
  font-weight: 600;
}
.arch-human { background: rgba(107, 114, 128, 0.22); }
.arch-raw { background: rgba(59, 130, 246, 0.22); }
.arch-manufacturer { background: rgba(34, 197, 94, 0.22); }
.arch-retailer { background: rgba(245, 158, 11, 0.22); }
.arch-financier { background: rgba(168, 85, 247, 0.22); }
.arch-conglomerate { background: rgba(99, 102, 241, 0.22); }
.share-cell {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}
.share-bar {
  width: 84px;
  height: 6px;
  border-radius: 999px;
  background: var(--color-border);
  overflow: hidden;
}
.share-fill {
  display: block;
  height: 100%;
  background: var(--color-primary);
}
.trend-cell {
  font-weight: 700;
}
</style>

