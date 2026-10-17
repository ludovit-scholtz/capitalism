<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import {
  TICK_TIMINGS_QUERY,
  formatPhaseDuration,
  nextSort,
  sortTickPhases,
  type SortDirection,
  type TickPhaseSortKey,
  type TickPhaseTiming,
} from '@/lib/tickTimings'

interface TickTimings {
  sampledTicks: number
  lastTick: number
  lastTotalMs: number
  averageTotalMs: number
  maxTotalMs: number
  phaseBudgetMs: number
  phases: TickPhaseTiming[]
}

const { t } = useI18n()

const timings = ref<TickTimings | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const sort = ref<{ key: TickPhaseSortKey; direction: SortDirection }>({ key: 'averageMs', direction: 'desc' })

const sortedPhases = computed(() => sortTickPhases(timings.value?.phases ?? [], sort.value.key, sort.value.direction))

const columns: Array<{ key: TickPhaseSortKey; labelKey: string }> = [
  { key: 'name', labelKey: 'operations.tickTimings.columns.phase' },
  { key: 'lastMs', labelKey: 'operations.tickTimings.columns.last' },
  { key: 'averageMs', labelKey: 'operations.tickTimings.columns.average' },
  { key: 'maxMs', labelKey: 'operations.tickTimings.columns.max' },
]

function ariaSort(key: TickPhaseSortKey) {
  if (sort.value.key !== key) return 'none'
  return sort.value.direction === 'asc' ? 'ascending' : 'descending'
}

function sortBy(key: TickPhaseSortKey) {
  sort.value = nextSort(sort.value, key)
}

async function load() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ tickPhaseTimings: TickTimings }>(TICK_TIMINGS_QUERY)
    timings.value = data.tickPhaseTimings
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="tick-timings card" data-testid="tick-phase-timings">
    <header class="tick-timings-header">
      <div>
        <h3>{{ t('operations.tickTimings.title') }}</h3>
        <p v-if="timings && timings.sampledTicks > 0">
          {{
            t('operations.tickTimings.summary', {
              ticks: timings.sampledTicks,
              average: formatPhaseDuration(timings.averageTotalMs),
              max: formatPhaseDuration(timings.maxTotalMs),
              budget: formatPhaseDuration(timings.phaseBudgetMs),
            })
          }}
        </p>
      </div>
      <button type="button" class="btn btn-secondary" :disabled="loading" @click="load">
        {{ t('operations.tickTimings.refresh') }}
      </button>
    </header>

    <p v-if="error" class="tick-timings-error">{{ error }}</p>
    <p v-if="loading && !timings">{{ t('common.loading') }}</p>
    <p v-else-if="!timings || timings.sampledTicks === 0" class="tick-timings-empty">
      {{ t('operations.tickTimings.empty') }}
    </p>
    <table v-else class="tick-timings-table">
      <thead>
        <tr>
          <th v-for="column in columns" :key="column.key" scope="col" :aria-sort="ariaSort(column.key)">
            <button type="button" class="tick-timings-sort" @click="sortBy(column.key)">
              {{ t(column.labelKey) }}
            </button>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="phase in sortedPhases" :key="phase.name" :class="{ 'tick-timings-over': phase.aboveBudget }">
          <th scope="row">
            {{ phase.name }}
            <span v-if="phase.aboveBudget" class="tick-timings-badge">{{ t('operations.tickTimings.overBudget') }}</span>
          </th>
          <td>{{ formatPhaseDuration(phase.lastMs) }}</td>
          <td>{{ formatPhaseDuration(phase.averageMs) }}</td>
          <td>{{ formatPhaseDuration(phase.maxMs) }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.tick-timings {
  padding: 16px;
  margin-top: 24px;
}

.tick-timings-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 16px;
}

.tick-timings-table {
  width: 100%;
  border-collapse: collapse;
}

.tick-timings-table th,
.tick-timings-table td {
  padding: 8px;
  text-align: left;
  border-bottom: 1px solid var(--color-border, rgba(255, 255, 255, 0.12));
}

.tick-timings-sort {
  background: none;
  border: 0;
  color: inherit;
  font: inherit;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
}

.tick-timings-over {
  background: rgba(239, 68, 68, 0.12);
}

.tick-timings-badge {
  margin-left: 8px;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 0.75rem;
  background: rgba(239, 68, 68, 0.3);
}

.tick-timings-error {
  color: #f87171;
}
</style>
