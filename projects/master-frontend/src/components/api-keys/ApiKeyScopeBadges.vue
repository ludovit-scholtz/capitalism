<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  scopes: string[]
}>()

const { t } = useI18n()

function formatScopeLabel(scope: string): string {
  switch (scope) {
    case 'read-only':
      return t('apiKeys.scopes.readOnly')
    case 'bot-only':
      return t('apiKeys.scopes.botOnly')
    case 'trading-only':
      return t('apiKeys.scopes.tradingOnly')
    case 'company-bound':
      return t('apiKeys.scopes.companyBound')
    default:
      return scope
  }
}

function scopeBadgeClass(scope: string): string {
  switch (scope) {
    case 'read-only':
      return 'bg-brand/15 text-brand'
    case 'bot-only':
      return 'bg-good/15 text-good'
    case 'trading-only':
      return 'bg-warn/15 text-warn'
    case 'company-bound':
      return 'bg-accent/20 text-accent'
    default:
      return 'bg-surface text-muted'
  }
}
</script>

<template>
  <div class="flex flex-wrap gap-2">
    <span
      v-for="scope in props.scopes"
      :key="scope"
      class="inline-flex rounded-full px-2 py-1 text-xs font-medium"
      :class="scopeBadgeClass(scope)"
    >
      {{ formatScopeLabel(scope) }}
    </span>
  </div>
</template>
