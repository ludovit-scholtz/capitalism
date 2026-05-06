<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { createEmptysDraft, NEWS_EDITOR_LOCALES, pickGamesLocalization, upsertsLocalization } from '@/lib/news'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { usesStore } from '@/stores/news'
import type { GamesEntry, GamesFeed } from '@/types'
import RichTextEditor from '@/components/admin/RichTextEditor.vue'

const { t, locale } = useI18n()
const adminStore = useGameAdminStore()
const newsStore = usesStore()

// ── State ─────────────────────────────────────────────────────────────────
const feed = ref<GamesFeed | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const actionError = ref<string | null>(null)

// ── Filters & pagination ──────────────────────────────────────────────────
const searchQuery = ref('')
const typeFilter = ref<'ALL' | 'NEWS' | 'CHANGELOG'>('ALL')
const statusFilter = ref<'ALL' | 'DRAFT' | 'PUBLISHED'>('ALL')
const pageSize = 10
const currentPage = ref(1)

const filteredEntries = computed(() => {
  const q = searchQuery.value.toLowerCase()
  return (feed.value?.items ?? []).filter((entry) => {
    if (typeFilter.value !== 'ALL' && entry.entryType !== typeFilter.value) return false
    if (statusFilter.value !== 'ALL' && entry.status !== statusFilter.value) return false
    if (q) {
      const localized = pickGamesLocalization(entry.localizations, locale.value)
      if (!localized?.title?.toLowerCase().includes(q) && !localized?.summary?.toLowerCase().includes(q)) return false
    }
    return true
  })
})

const totalPages = computed(() => Math.ceil(filteredEntries.value.length / pageSize))
const pagedEntries = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  return filteredEntries.value.slice(start, start + pageSize)
})

function setPage(page: number) {
  currentPage.value = Math.max(1, Math.min(page, totalPages.value))
}

// ── Composer ──────────────────────────────────────────────────────────────
const showComposer = ref(false)
const newsEditor = ref(createEmptysDraft())
const activeLocale = ref<(typeof NEWS_EDITOR_LOCALES)[number]>('en')
const activeLocalization = computed(() =>
  newsEditor.value.localizations.find((l) => l.locale === activeLocale.value),
)

function openComposer(entry?: GamesEntry) {
  if (entry) {
    newsEditor.value = {
      entryId: entry.id,
      entryType: entry.entryType,
      status: entry.status,
      localizations: entry.localizations.map((l) => ({ ...l })),
    }
  } else {
    newsEditor.value = createEmptysDraft()
  }
  activeLocale.value = 'en'
  showComposer.value = true
}

function closeComposer() {
  showComposer.value = false
}

function updateLocalization<K extends 'title' | 'summary' | 'htmlContent'>(key: K, value: string) {
  newsEditor.value = {
    ...newsEditor.value,
    localizations: upsertsLocalization(newsEditor.value.localizations, activeLocale.value, { [key]: value }),
  }
}

async function saveEntry() {
  actionError.value = null
  actionMessage.value = null
  try {
    await adminStore.upsertGamesEntry(newsEditor.value)
    await Promise.all([loadFeed(), newsStore.fetchUnreadCount()])
    actionMessage.value = t('admin.newsSaved')
    showComposer.value = false
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.newsSaveFailed')
  }
}

function canEditEntry(entry: GamesEntry) {
  return entry.targetServerKey !== null || adminStore.session?.isRootAdministrator || adminStore.session?.hasGlobalAdminRole
}

// ── Data ──────────────────────────────────────────────────────────────────
function formatDate(value: string | null) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function getLocalizedEntry(entry: GamesEntry) {
  return pickGamesLocalization(entry.localizations, locale.value)
}

async function loadFeed() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ gameNewsFeed: GamesFeed }>(`
      query OpsNewsManagerFeed {
        gameNewsFeed(includeDrafts: true) {
          unreadCount
          items {
            id
            entryType
            status
            targetServerKey
            createdByEmail
            updatedByEmail
            createdAtUtc
            updatedAtUtc
            publishedAtUtc
            isRead
            localizations { locale title summary htmlContent }
          }
        }
      }
    `)
    feed.value = data.gameNewsFeed
    currentPage.value = 1
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('operations.news.loadFailed')
  } finally {
    loading.value = false
  }
}

onMounted(loadFeed)
</script>

<template>
  <div class="ops-news">
    <div class="ops-news-header">
      <div>
        <h2>{{ t('operations.news.title') }}</h2>
        <p>{{ t('operations.news.subtitle') }}</p>
      </div>
      <button type="button" class="btn btn-primary" @click="openComposer()">{{ t('operations.news.compose') }}</button>
    </div>

    <!-- Feedback banners -->
    <div v-if="actionError" class="ops-banner ops-banner-error">{{ actionError }}</div>
    <div v-else-if="actionMessage" class="ops-banner">{{ actionMessage }}</div>

    <!-- Composer dialog -->
    <div v-if="showComposer" class="ops-composer-overlay" role="dialog" aria-modal="true">
      <div class="ops-composer-dialog card">
        <div class="ops-composer-header">
          <h3>{{ newsEditor.entryId ? t('operations.news.editEntry') : t('operations.news.compose') }}</h3>
          <button type="button" class="btn btn-ghost btn-sm" @click="closeComposer">{{ t('common.cancel') }}</button>
        </div>

        <div v-if="actionError" class="ops-banner ops-banner-error">{{ actionError }}</div>

        <div class="ops-inline-fields">
          <label class="form-label">
            {{ t('admin.entryType') }}
            <select v-model="newsEditor.entryType" class="form-select">
              <option value="NEWS">{{ t('news.filters') }}</option>
              <option value="CHANGELOG">{{ t('news.filterChangelog') }}</option>
            </select>
          </label>
          <label class="form-label">
            {{ t('admin.entryStatus') }}
            <select v-model="newsEditor.status" class="form-select">
              <option value="DRAFT">{{ t('admin.statusDraft') }}</option>
              <option value="PUBLISHED">{{ t('admin.statusPublished') }}</option>
            </select>
          </label>
        </div>

        <div class="locale-tabs">
          <button
            v-for="editorLocale in NEWS_EDITOR_LOCALES"
            :key="editorLocale"
            type="button"
            class="locale-tab"
            :class="{ active: activeLocale === editorLocale }"
            @click="activeLocale = editorLocale"
          >
            {{ editorLocale.toUpperCase() }}
          </button>
        </div>

        <label class="form-label">
          {{ t('admin.entryTitle') }}
          <input
            class="form-input"
            :value="activeLocalization?.title ?? ''"
            @input="updateLocalization('title', ($event.target as HTMLInputElement).value ?? '')"
          />
        </label>
        <label class="form-label">
          {{ t('admin.entrySummary') }}
          <textarea
            class="form-textarea"
            :value="activeLocalization?.summary ?? ''"
            @input="updateLocalization('summary', ($event.target as HTMLTextAreaElement).value ?? '')"
          ></textarea>
        </label>
        <label class="form-label">
          {{ t('admin.entryContent') }}
          <RichTextEditor
            :model-value="activeLocalization?.htmlContent ?? ''"
            @update:model-value="updateLocalization('htmlContent', $event)"
          />
        </label>

        <div class="ops-composer-actions">
          <button type="button" class="btn btn-primary" @click="saveEntry">{{ t('admin.saveEntry') }}</button>
          <button type="button" class="btn btn-ghost" @click="closeComposer">{{ t('common.cancel') }}</button>
        </div>
      </div>
    </div>

    <!-- Filters -->
    <div class="ops-news-filters">
      <input
        v-model="searchQuery"
        class="form-input ops-search"
        :placeholder="t('operations.news.searchPlaceholder')"
        @input="currentPage = 1"
      />
      <div class="ops-filter-group">
        <button
          v-for="tf in [
            { value: 'ALL', label: t('operations.news.filterAll') },
            { value: 'NEWS', label: t('operations.news.filterNews') },
            { value: 'CHANGELOG', label: t('operations.news.filterChangelog') },
          ]"
          :key="tf.value"
          type="button"
          class="ops-filter-btn"
          :class="{ active: typeFilter === tf.value }"
          @click="typeFilter = tf.value as 'ALL' | 'NEWS' | 'CHANGELOG'; currentPage = 1"
        >
          {{ tf.label }}
        </button>
      </div>
      <div class="ops-filter-group">
        <button
          v-for="sf in [
            { value: 'ALL', label: t('operations.news.filterAll') },
            { value: 'DRAFT', label: t('operations.news.filterDraft') },
            { value: 'PUBLISHED', label: t('operations.news.filterPublished') },
          ]"
          :key="sf.value"
          type="button"
          class="ops-filter-btn"
          :class="{ active: statusFilter === sf.value }"
          @click="statusFilter = sf.value as 'ALL' | 'DRAFT' | 'PUBLISHED'; currentPage = 1"
        >
          {{ sf.label }}
        </button>
      </div>
    </div>

    <!-- Table -->
    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="loadFeed">{{ t('common.retry') }}</button>
    </div>
    <template v-else>
      <div class="ops-table-wrap">
        <table class="ops-table" aria-label="News entries">
          <thead>
            <tr>
              <th>{{ t('admin.entryType') }}</th>
              <th>{{ t('admin.entryStatus') }}</th>
              <th>{{ t('admin.entryTitle') }}</th>
              <th>{{ t('admin.entrySummary') }}</th>
              <th>{{ t('admin.entryUpdated') }}</th>
              <th>{{ t('admin.entryScope') }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="pagedEntries.length === 0">
              <td colspan="7" class="ops-table-empty">{{ t('operations.news.noEntries') }}</td>
            </tr>
            <tr v-for="entry in pagedEntries" :key="entry.id">
              <td>
                <span class="badge badge-primary">{{ entry.entryType }}</span>
              </td>
              <td>
                <span class="badge" :class="entry.status === 'PUBLISHED' ? 'badge-success' : 'badge-warning'">
                  {{ entry.status }}
                </span>
              </td>
              <td class="ops-table-title">{{ getLocalizedEntry(entry)?.title ?? t('news.untitled') }}</td>
              <td class="ops-table-summary">{{ getLocalizedEntry(entry)?.summary }}</td>
              <td class="ops-table-date">{{ formatDate(entry.updatedAtUtc) }}</td>
              <td class="ops-table-scope">{{ entry.targetServerKey ?? t('admin.globalScope') }}</td>
              <td>
                <button
                  type="button"
                  class="btn btn-secondary btn-sm"
                  :disabled="!canEditEntry(entry)"
                  @click="openComposer(entry)"
                >
                  {{ t('operations.news.editEntry') }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="ops-pagination">
        <button type="button" class="btn btn-ghost btn-sm" :disabled="currentPage === 1" @click="setPage(currentPage - 1)">
          {{ t('news.previousPage') }}
        </button>
        <span class="ops-page-info">{{ t('news.pageLabel', { page: currentPage, total: totalPages }) }}</span>
        <button
          type="button"
          class="btn btn-ghost btn-sm"
          :disabled="currentPage === totalPages"
          @click="setPage(currentPage + 1)"
        >
          {{ t('news.nextPage') }}
        </button>
      </div>
      <p class="ops-pagination-status">
        {{ t('news.paginationStatus', { from: (currentPage - 1) * pageSize + 1, to: Math.min(currentPage * pageSize, filteredEntries.length), total: filteredEntries.length }) }}
      </p>
    </template>
  </div>
</template>

<style scoped>
.ops-news {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.ops-news-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.ops-news-header h2 {
  margin-bottom: 0.2rem;
}

.ops-news-header p {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.ops-banner {
  padding: 0.75rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.1);
  font-size: 0.9rem;
}

.ops-banner-error {
  border-color: rgba(248, 113, 113, 0.4);
  background: rgba(248, 113, 113, 0.1);
}

/* Composer overlay */
.ops-composer-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 2rem 1rem;
  z-index: 50;
  overflow-y: auto;
}

.ops-composer-dialog {
  width: 100%;
  max-width: 760px;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.ops-composer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.ops-inline-fields {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
}

.ops-inline-fields .form-label {
  flex: 1;
  min-width: 140px;
}

.locale-tabs {
  display: flex;
  gap: 0.35rem;
  border-bottom: 1px solid var(--color-border);
  margin-bottom: 0.25rem;
}

.locale-tab {
  padding: 0.4rem 0.75rem;
  font-size: 0.8rem;
  border-radius: var(--radius-sm) var(--radius-sm) 0 0;
  border: 1px solid transparent;
  border-bottom: none;
  color: var(--color-text-secondary);
  cursor: pointer;
  background: transparent;
}

.locale-tab.active {
  color: var(--color-text);
  border-color: var(--color-border);
  border-bottom-color: var(--color-card);
  background: var(--color-card);
  margin-bottom: -1px;
}

.ops-composer-actions {
  display: flex;
  gap: 0.75rem;
}

/* Filters */
.ops-news-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
}

.ops-search {
  flex: 1;
  min-width: 200px;
  max-width: 320px;
}

.ops-filter-group {
  display: flex;
  gap: 0.25rem;
}

.ops-filter-btn {
  padding: 0.4rem 0.85rem;
  font-size: 0.83rem;
  border-radius: var(--radius-sm);
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: background 0.15s, color 0.15s;
}

.ops-filter-btn.active,
.ops-filter-btn:hover {
  background: rgba(255, 255, 255, 0.07);
  color: var(--color-text);
}

.ops-filter-btn.active {
  border-color: rgba(255, 255, 255, 0.25);
}

/* Table */
.ops-table-wrap {
  overflow-x: auto;
}

.ops-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.88rem;
}

.ops-table th {
  text-align: left;
  padding: 0.6rem 1rem;
  border-bottom: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  font-weight: 500;
  white-space: nowrap;
}

.ops-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  vertical-align: top;
}

.ops-table tr:last-child td {
  border-bottom: none;
}

.ops-table tr:hover td {
  background: rgba(255, 255, 255, 0.02);
}

.ops-table-title {
  font-weight: 500;
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ops-table-summary {
  max-width: 280px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-text-secondary);
}

.ops-table-date,
.ops-table-scope {
  white-space: nowrap;
  color: var(--color-text-secondary);
}

.ops-table-empty {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-secondary);
}

/* Pagination */
.ops-pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
}

.ops-page-info {
  font-size: 0.88rem;
  color: var(--color-text-secondary);
}

.ops-pagination-status {
  text-align: center;
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.ops-loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}

.ops-error {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.75rem;
}
</style>
