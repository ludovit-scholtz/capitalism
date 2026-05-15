<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import { gqlRequest } from '@/lib/graphql'
import type { GlobalEvent } from '@/types/game'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const auth = useAuthStore()

const activeEvents = ref<GlobalEvent[]>([])
const historyEvents = ref<GlobalEvent[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const activeTab = ref<'active' | 'history'>('active')

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
  const [activeResult, historyResult] = await Promise.allSettled([
    gqlRequest<{ activeGlobalEvents: GlobalEvent[] }>(ACTIVE_QUERY),
    gqlRequest<{ globalEventHistory: GlobalEvent[] }>(HISTORY_QUERY),
  ])
  activeEvents.value =
    activeResult.status === 'fulfilled' ? (activeResult.value.activeGlobalEvents ?? []) : []
  historyEvents.value =
    historyResult.status === 'fulfilled' ? (historyResult.value.globalEventHistory ?? []) : []
  loading.value = false
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

    <!-- Tab navigation -->
    <div class="tab-bar">
      <button
        class="tab-btn"
        :class="{ 'tab-btn-active': activeTab === 'active' }"
        @click="activeTab = 'active'"
      >
        {{ t('globalEvents.active') }}
      </button>
      <button
        class="tab-btn"
        :class="{ 'tab-btn-active': activeTab === 'history' }"
        @click="activeTab = 'history'"
      >
        {{ t('globalEvents.history') }}
      </button>
    </div>

    <div v-if="loading" class="loading-state">
      <FontAwesomeIcon :icon="['fas', 'spinner']" spin />
    </div>

    <div v-else-if="error" class="error-state">{{ error }}</div>

    <template v-else>
      <!-- Active events tab -->
      <section v-if="activeTab === 'active'" class="events-section">
        <div
          v-if="activeEvents.filter((e) => e.isActive).length === 0"
          class="no-events ge-empty-state"
        >
          {{ t('globalEvents.noActiveEvents') }}
        </div>
        <div
          v-for="event in activeEvents.filter((e) => e.isActive)"
          :key="event.id"
          class="event-card active-event"
          :class="severityClass(event.severity)"
        >
          <div class="event-header">
            <span class="severity-badge ge-severity-badge">{{
              t(`globalEvents.severity.${event.severity}`)
            }}</span>
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
              class="effect-chip ge-multiplier-chip"
              :class="event.operatingCostMultiplier > 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.operatingCost') }}
              {{ formatMultiplier(event.operatingCostMultiplier) }}
            </span>
            <span
              v-if="formatMultiplier(event.tradeRouteMultiplier)"
              class="effect-chip ge-multiplier-chip"
              :class="event.tradeRouteMultiplier > 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.tradeRoute') }}
              {{ formatMultiplier(event.tradeRouteMultiplier) }}
            </span>
            <span
              v-if="formatMultiplier(event.rdMultiplier)"
              class="effect-chip ge-multiplier-chip"
              :class="event.rdMultiplier < 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.research') }}
              {{ formatMultiplier(event.rdMultiplier) }}
            </span>
            <span
              v-if="formatMultiplier(event.mineEfficiencyMultiplier)"
              class="effect-chip ge-multiplier-chip"
              :class="event.mineEfficiencyMultiplier < 1 ? 'effect-negative' : 'effect-positive'"
            >
              {{ t('globalEvents.mineEfficiency') }}
              {{ formatMultiplier(event.mineEfficiencyMultiplier) }}
            </span>
          </div>
        </div>
      </section>

      <!-- History tab -->
      <section v-if="activeTab === 'history'" class="events-section history-section">
        <div
          v-if="historyEvents.filter((e) => !e.isActive).length === 0"
          class="no-events ge-empty-state"
        >
          {{ t('globalEvents.noHistory') }}
        </div>
        <div
          v-for="event in historyEvents.filter((e) => !e.isActive)"
          :key="event.id"
          class="event-card resolved-event"
        >
          <div class="event-header">
            <span class="severity-badge ge-severity-badge resolved-badge">{{
              t(`globalEvents.severity.${event.severity}`)
            }}</span>
          </div>
          <div class="event-title">{{ event.title }}</div>
        </div>
      </section>
    </template>

    <!-- Admin section: trigger event -->
    <div v-if="auth.isAdmin" class="ge-admin-section">
      <h4 class="section-label">{{ t('globalEvents.admin.title') }}</h4>
      <button class="btn btn-warning ge-trigger-btn" @click="() => {}">
        <FontAwesomeIcon :icon="['fas', 'bolt']" class="mr-1" />
        {{ t('globalEvents.admin.trigger') }}
      </button>
    </div>
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

.tab-bar {
  display: flex;
  gap: 0.25rem;
  margin-bottom: 1rem;
  border-bottom: 1px solid var(--color-border);
  padding-bottom: 0.5rem;
}

.tab-btn {
  background: none;
  border: none;
  padding: 0.35rem 0.75rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
  color: var(--color-text-muted);
  font-weight: 500;
}

.tab-btn:hover {
  background: var(--color-hover);
  color: var(--color-text);
}

.tab-btn-active {
  background: var(--color-primary);
  color: #fff;
}

.ge-admin-section {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--color-border);
}

.ge-trigger-btn {
  font-size: 0.85rem;
}
</style>
