<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import { gqlRequest } from '@/lib/graphql'
import type { GlobalEvent } from '@/types/game'

const { t } = useI18n()

const activeEvents = ref<GlobalEvent[]>([])
const historyEvents = ref<GlobalEvent[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

const ACTIVE_QUERY = `
  query {
    activeGlobalEvents {
      id eventType severity title description isActive
      startTick durationTicks affectedCityId
      affectedCity { id name }
      operatingCostMultiplier tradeRouteMultiplier rdMultiplier mineEfficiencyMultiplier
      createdAtUtc resolvedAtUtc triggeredByAdminId
    }
  }
`

const HISTORY_QUERY = `
  query {
    globalEventHistory(limit: 20) {
      id eventType severity title description isActive
      startTick durationTicks affectedCityId
      affectedCity { id name }
      operatingCostMultiplier tradeRouteMultiplier rdMultiplier mineEfficiencyMultiplier
      createdAtUtc resolvedAtUtc triggeredByAdminId
    }
  }
`

async function loadEvents() {
  loading.value = true
  error.value = null
  try {
    const [activeRes, historyRes] = await Promise.all([
      gqlRequest<{ activeGlobalEvents: GlobalEvent[] }>(ACTIVE_QUERY),
      gqlRequest<{ globalEventHistory: GlobalEvent[] }>(HISTORY_QUERY),
    ])
    activeEvents.value = activeRes.activeGlobalEvents ?? []
    historyEvents.value = historyRes.globalEventHistory ?? []
  } catch (e) {
    error.value = String(e)
  } finally {
    loading.value = false
  }
}

function severityClass(severity: string) {
  switch (severity) {
    case 'CATASTROPHIC':
      return 'severity-catastrophic'
    case 'MAJOR':
      return 'severity-major'
    case 'MODERATE':
      return 'severity-moderate'
    default:
      return 'severity-minor'
  }
}

function formatMultiplier(value: number) {
  const pct = Math.round((value - 1) * 100)
  if (pct === 0) return null
  return pct > 0 ? `+${pct}%` : `${pct}%`
}

function hasEffects(event: GlobalEvent) {
  return (
    event.operatingCostMultiplier !== 1 ||
    event.tradeRouteMultiplier !== 1 ||
    event.rdMultiplier !== 1 ||
    event.mineEfficiencyMultiplier !== 1
  )
}

onMounted(loadEvents)
</script>

<template>
  <div class="global-events-panel">
    <div class="panel-header">
      <FontAwesomeIcon :icon="['fas', 'bolt']" class="panel-icon" />
      <h3>{{ t('globalEvents.title') }}</h3>
    </div>

    <div v-if="loading" class="loading-state">
      <FontAwesomeIcon :icon="['fas', 'spinner']" spin />
    </div>

    <div v-else-if="error" class="error-state">{{ error }}</div>

    <template v-else>
      <section class="events-section">
        <h4 class="section-label">{{ t('globalEvents.active') }}</h4>
        <div v-if="activeEvents.length === 0" class="no-events">
          {{ t('globalEvents.noActiveEvents') }}
        </div>
        <div
          v-for="event in activeEvents"
          :key="event.id"
          class="event-card active-event"
          :class="severityClass(event.severity)"
        >
          <div class="event-header">
            <span class="severity-badge">{{ t(`globalEvents.severity.${event.severity}`) }}</span>
            <span class="event-type">{{ t(`globalEvents.type.${event.eventType}`) }}</span>
          </div>
          <div class="event-title">{{ event.title }}</div>
          <div class="event-description">{{ event.description }}</div>
          <div class="event-meta">
            <span class="event-scope">
              <template v-if="event.affectedCity">
                {{ t('globalEvents.affectedCity') }}: {{ event.affectedCity.name }}
              </template>
              <template v-else>{{ t('globalEvents.global') }}</template>
            </span>
            <span class="event-duration">
              {{ t('globalEvents.endsAtTick') }}:
              {{ event.startTick + event.durationTicks }}
            </span>
          </div>
          <div v-if="hasEffects(event)" class="event-effects">
            <span class="effects-label">{{ t('globalEvents.multipliers') }}:</span>
            <span
              v-if="formatMultiplier(event.operatingCostMultiplier)"
              class="effect-chip"
              :class="event.operatingCostMultiplier > 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.operatingCost') }}
              {{ formatMultiplier(event.operatingCostMultiplier) }}
            </span>
            <span
              v-if="formatMultiplier(event.tradeRouteMultiplier)"
              class="effect-chip"
              :class="event.tradeRouteMultiplier > 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.tradeRoute') }}
              {{ formatMultiplier(event.tradeRouteMultiplier) }}
            </span>
            <span
              v-if="formatMultiplier(event.rdMultiplier)"
              class="effect-chip"
              :class="event.rdMultiplier < 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.research') }}
              {{ formatMultiplier(event.rdMultiplier) }}
            </span>
            <span
              v-if="formatMultiplier(event.mineEfficiencyMultiplier)"
              class="effect-chip"
              :class="event.mineEfficiencyMultiplier < 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.mineEfficiency') }}
              {{ formatMultiplier(event.mineEfficiencyMultiplier) }}
            </span>
          </div>
        </div>
      </section>

      <section v-if="historyEvents.length > 0" class="events-section history-section">
        <h4 class="section-label">{{ t('globalEvents.history') }}</h4>
        <div
          v-for="event in historyEvents.filter((e) => !e.isActive)"
          :key="event.id"
          class="event-card resolved-event"
        >
          <div class="event-header">
            <span class="severity-badge resolved-badge">{{
              t(`globalEvents.severity.${event.severity}`)
            }}</span>
            <span class="event-type">{{ t(`globalEvents.type.${event.eventType}`) }}</span>
          </div>
          <div class="event-title">{{ event.title }}</div>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.global-events-panel {
  background: var(--color-card-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 1.25rem;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.panel-header h3 {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
}

.panel-icon {
  color: var(--color-warning);
}

.section-label {
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-muted);
  margin: 0 0 0.5rem 0;
}

.events-section {
  margin-bottom: 1.25rem;
}

.history-section {
  border-top: 1px solid var(--color-border);
  padding-top: 1rem;
}

.no-events {
  color: var(--color-text-muted);
  font-size: 0.9rem;
}

.event-card {
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 0.75rem;
  margin-bottom: 0.5rem;
  background: var(--color-surface);
}

.active-event.severity-catastrophic {
  border-color: var(--color-danger);
  background: color-mix(in srgb, var(--color-danger) 8%, var(--color-surface));
}

.active-event.severity-major {
  border-color: var(--color-warning);
  background: color-mix(in srgb, var(--color-warning) 8%, var(--color-surface));
}

.active-event.severity-moderate {
  border-color: var(--color-info);
}

.event-header {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin-bottom: 0.25rem;
}

.severity-badge {
  font-size: 0.7rem;
  padding: 0.1rem 0.4rem;
  border-radius: 3px;
  background: var(--color-warning);
  color: var(--color-btn-primary-text, #fff);
  font-weight: 600;
  text-transform: uppercase;
}

.resolved-badge {
  background: var(--color-text-muted);
}

.event-type {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.event-title {
  font-weight: 600;
  margin-bottom: 0.25rem;
}

.event-description {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.5rem;
}

.event-meta {
  display: flex;
  gap: 1rem;
  font-size: 0.8rem;
  color: var(--color-text-muted);
  margin-bottom: 0.5rem;
}

.event-effects {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  align-items: center;
}

.effects-label {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.effect-chip {
  font-size: 0.75rem;
  padding: 0.1rem 0.5rem;
  border-radius: 10px;
  font-weight: 600;
}

.effect-positive {
  background: color-mix(in srgb, var(--color-success) 15%, transparent);
  color: var(--color-success);
}

.effect-negative {
  background: color-mix(in srgb, var(--color-danger) 15%, transparent);
  color: var(--color-danger);
}

.loading-state,
.error-state {
  padding: 1rem;
  text-align: center;
  color: var(--color-text-muted);
}
</style>
