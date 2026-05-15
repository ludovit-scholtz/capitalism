<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'
import { gqlRequest } from '@/lib/graphql'
import type { GlobalEvent } from '@/types/game'

const { t } = useI18n()

const events = ref<GlobalEvent[]>([])
const dismissed = ref(false)

const QUERY = `
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

const bannerText = computed(() => {
  if (events.value.length === 0) return null
  if (events.value.length === 1) {
    const first = events.value[0]
    return first ? t('globalEvents.banner.activeShock', { title: first.title }) : null
  }
  return t('globalEvents.banner.multipleShocks', { count: events.value.length })
})

const severityClass = computed(() => {
  if (events.value.length === 0) return ''
  const worst = events.value.reduce((prev, curr) => {
    const order = ['MINOR', 'MODERATE', 'MAJOR', 'CATASTROPHIC']
    return order.indexOf(curr.severity) > order.indexOf(prev.severity) ? curr : prev
  })
  return `banner-${worst.severity.toLowerCase()}`
})

async function loadEvents() {
  try {
    const res = await gqlRequest<{ activeGlobalEvents: GlobalEvent[] }>(QUERY)
    events.value = res.activeGlobalEvents ?? []
  } catch {
    events.value = []
  }
}

onMounted(loadEvents)
</script>

<template>
  <div
    v-if="events.length > 0 && !dismissed"
    class="global-event-banner"
    :class="severityClass"
    role="alert"
  >
    <FontAwesomeIcon :icon="['fas', 'bolt']" class="banner-icon" aria-hidden="true" />
    <span class="banner-text">{{ bannerText }}</span>
    <button class="banner-dismiss" :aria-label="t('common.close')" @click="dismissed = true">
      <FontAwesomeIcon :icon="['fas', 'xmark']" />
    </button>
  </div>
</template>

<style scoped>
.global-event-banner {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.75rem;
  border-radius: 4px;
  font-size: 0.85rem;
  font-weight: 500;
  background: color-mix(in srgb, var(--color-warning) 20%, var(--color-surface));
  border: 1px solid var(--color-warning);
  color: var(--color-text);
}

.global-event-banner.banner-catastrophic {
  background: color-mix(in srgb, var(--color-danger) 20%, var(--color-surface));
  border-color: var(--color-danger);
}

.global-event-banner.banner-major {
  background: color-mix(in srgb, var(--color-warning) 20%, var(--color-surface));
  border-color: var(--color-warning);
}

.global-event-banner.banner-moderate {
  background: color-mix(in srgb, var(--color-info) 15%, var(--color-surface));
  border-color: var(--color-info);
}

.banner-icon {
  color: var(--color-warning);
  flex-shrink: 0;
}

.banner-text {
  flex: 1;
}

.banner-dismiss {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted);
  padding: 0.1rem 0.25rem;
  border-radius: 3px;
  flex-shrink: 0;
}

.banner-dismiss:hover {
  color: var(--color-text);
}
</style>
