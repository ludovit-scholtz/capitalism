<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { deriveAdvisorSuggestions, parseDismissedIds, type AdvisorBuilding, type AdvisorSuggestion, type AdvisorUnitStatus } from '@/lib/advisorRules'
import { formatTickDuration } from '@/lib/gameTime'

const props = defineProps<{
  buildings: AdvisorBuilding[]
  statuses: Record<string, AdvisorUnitStatus[] | undefined>
}>()

const { t, locale } = useI18n()

const STORAGE_KEY = 'advisor_dismissed'

function readDismissed(): string[] {
  try {
    return parseDismissedIds(sessionStorage.getItem(STORAGE_KEY))
  } catch {
    return []
  }
}

const dismissedIds = ref<string[]>(readDismissed())
const suggestions = computed(() => deriveAdvisorSuggestions(props.buildings, props.statuses, dismissedIds.value))

function dismiss(id: string) {
  dismissedIds.value = [...dismissedIds.value, id]
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(dismissedIds.value))
  } catch {
    // The dismissal still applies for this page view when storage is unavailable.
  }
}

function title(suggestion: AdvisorSuggestion): string {
  return t(`advisor.rules.${suggestion.rule}.title` as Parameters<typeof t>[0], { building: suggestion.buildingName })
}

function body(suggestion: AdvisorSuggestion): string {
  const duration = formatTickDuration(suggestion.ticks, locale.value)
  if (suggestion.rule === 'OUT_OF_STOCK_SHOP') {
    return t(`advisor.rules.OUT_OF_STOCK_SHOP.${suggestion.hasPurchaseUnit ? 'bodyWithPurchase' : 'bodyNoPurchase'}`, { duration })
  }
  return t(`advisor.rules.${suggestion.rule}.body` as Parameters<typeof t>[0], { duration })
}
</script>

<template>
  <section v-if="suggestions.length > 0" class="advisor-card mb-6 rounded-md border border-divider bg-white/5 px-6 py-4" aria-labelledby="advisor-card-title">
    <h2 id="advisor-card-title" class="mb-3 text-[0.8125rem] font-bold uppercase tracking-[0.06em] text-muted">{{ t('advisor.title') }}</h2>
    <ul class="m-0 flex list-none flex-col gap-4 p-0">
      <li v-for="suggestion in suggestions" :key="suggestion.id" class="advisor-suggestion flex flex-wrap items-start gap-3">
        <span class="mt-0.5 shrink-0 text-xl" aria-hidden="true">💡</span>
        <div class="flex-1 basis-64">
          <strong class="mb-1 block text-sm font-semibold">{{ title(suggestion) }}</strong>
          <p class="mb-2 text-[0.8125rem] leading-[1.45] text-muted">{{ body(suggestion) }}</p>
          <RouterLink :to="suggestion.linkPath" class="text-[0.8125rem] font-medium text-brand hover:underline">{{ t('advisor.openBuilding') }} →</RouterLink>
        </div>
        <button type="button" class="btn btn-secondary" @click="dismiss(suggestion.id)">{{ t('advisor.dismiss') }}</button>
      </li>
    </ul>
  </section>
</template>
