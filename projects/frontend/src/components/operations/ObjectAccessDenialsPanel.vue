<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import {
  OBJECT_ACCESS_DENIALS_QUERY,
  denialReasonLabelKey,
  shortId,
  type ObjectAccessDenials,
} from '@/lib/objectDenials'

const { t, locale } = useI18n()

const report = ref<ObjectAccessDenials | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ objectAccessDenials: ObjectAccessDenials }>(OBJECT_ACCESS_DENIALS_QUERY, { limit: 50 })
    report.value = data.objectAccessDenials
  } catch (caught) {
    report.value = null
    error.value = caught instanceof Error ? caught.message : t('common.unknownError')
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="object-denials card" data-testid="object-access-denials">
    <header class="object-denials-header">
      <div>
        <h3>{{ t('operations.objectDenials.title') }}</h3>
        <p v-if="report && report.windowSize > 0">
          {{
            t('operations.objectDenials.summary', {
              window: report.windowSize,
              probes: report.probeSignals,
              total: report.totalRecorded,
            })
          }}
        </p>
      </div>
      <button type="button" class="btn btn-secondary" :disabled="loading" @click="load">
        {{ t('operations.objectDenials.refresh') }}
      </button>
    </header>

    <p v-if="error" class="object-denials-error">{{ error }}</p>
    <p v-if="loading && !report">{{ t('common.loading') }}</p>
    <p v-else-if="!error && (!report || report.windowSize === 0)" class="object-denials-empty">
      {{ t('operations.objectDenials.empty') }}
    </p>
    <template v-else-if="report">
      <ul class="object-denials-counts" :aria-label="t('operations.objectDenials.byReason')">
        <li v-for="count in report.byReason" :key="count.key">
          {{ t(denialReasonLabelKey(count.key)) }}: <strong>{{ count.count }}</strong>
        </li>
      </ul>
      <table class="object-denials-table">
        <thead>
          <tr>
            <th scope="col">{{ t('operations.objectDenials.columns.time') }}</th>
            <th scope="col">{{ t('operations.objectDenials.columns.object') }}</th>
            <th scope="col">{{ t('operations.objectDenials.columns.reason') }}</th>
            <th scope="col">{{ t('operations.objectDenials.columns.actor') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(denial, index) in report.recent"
            :key="`${denial.occurredAtUtc}-${denial.objectId}-${index}`"
            :class="{ 'object-denials-probe': denial.isProbeSignal }"
          >
            <td>{{ new Date(denial.occurredAtUtc).toLocaleString(locale) }}</td>
            <td>
              {{ denial.objectType }}
              <code :title="denial.objectId">{{ shortId(denial.objectId) }}</code>
            </td>
            <td>{{ t(denialReasonLabelKey(denial.reason)) }}</td>
            <td>
              <code :title="denial.actorUserId">{{ shortId(denial.actorUserId) }}</code>
              <span v-if="denial.apiKeyId" class="object-denials-badge">{{ t('operations.objectDenials.apiKey') }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </template>
  </section>
</template>

<style scoped>
.object-denials {
  padding: 16px;
  margin-top: 24px;
}

.object-denials-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 16px;
}

.object-denials-counts {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  padding: 0;
  margin: 0 0 16px;
  list-style: none;
}

.object-denials-table {
  width: 100%;
  border-collapse: collapse;
}

.object-denials-table th,
.object-denials-table td {
  padding: 8px;
  text-align: left;
  border-bottom: 1px solid var(--color-border, rgba(255, 255, 255, 0.12));
}

.object-denials-probe {
  background: rgba(239, 68, 68, 0.08);
}

.object-denials-badge {
  margin-left: 8px;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 0.75rem;
  background: rgba(59, 130, 246, 0.3);
}

.object-denials-error {
  color: #f87171;
}
</style>
