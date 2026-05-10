<script setup lang="ts">
import type { ApiKeyAuditLogInfo } from '@/lib/masterApi'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  entries: ApiKeyAuditLogInfo[]
  showPlayer?: boolean
}>()

const { t } = useI18n()

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso))
}
</script>

<template>
  <div class="overflow-x-auto rounded-xl border border-divider">
    <table class="min-w-full text-sm">
      <thead>
        <tr class="bg-surface text-left text-xs uppercase tracking-[0.08em] text-muted">
          <th v-if="props.showPlayer" class="px-4 py-3">{{ t('apiKeys.adminPlayer') }}</th>
          <th class="px-4 py-3">{{ t('apiKeys.auditOperation') }}</th>
          <th class="px-4 py-3">{{ t('apiKeys.auditScope') }}</th>
          <th v-if="!props.showPlayer" class="px-4 py-3">{{ t('apiKeys.auditResult') }}</th>
          <th class="px-4 py-3">{{ t('apiKeys.auditTimestamp') }}</th>
          <th v-if="!props.showPlayer" class="px-4 py-3">{{ t('apiKeys.auditIp') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="entry in props.entries" :key="entry.id" class="border-t border-divider/70">
          <td v-if="props.showPlayer" class="px-4 py-3 text-body">{{ entry.playerEmail }}</td>
          <td class="px-4 py-3 text-body">{{ entry.operationName }}</td>
          <td class="px-4 py-3 text-muted">{{ entry.scopeUsed }}</td>
          <td v-if="!props.showPlayer" class="px-4 py-3 text-muted">
            {{ entry.wasAllowed ? t('apiKeys.auditAllowed') : t('apiKeys.auditDenied') }}
          </td>
          <td class="px-4 py-3 text-muted">{{ formatDate(entry.occurredAtUtc) }}</td>
          <td v-if="!props.showPlayer" class="px-4 py-3 text-muted">
            {{ entry.ipAddress || t('apiKeys.ipUnavailable') }}
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
