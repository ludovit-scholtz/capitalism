<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { createEmptysDraft, NEWS_EDITOR_LOCALES, pickGamesLocalization, upsertsLocalization } from '@/lib/news'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { usesStore } from '@/stores/news'
import type { GamesEntry, GamesFeed } from '@/types'
import OperationsNewsEditorPanel from '@/components/operations/OperationsNewsEditorPanel.vue'

const { t, locale } = useI18n()
const adminStore = useGameAdminStore()
const newsStore = usesStore()

const feed = ref<GamesFeed | null>(null)
const loading = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const actionError = ref<string | null>(null)
const searchQuery = ref('')
const typeFilter = ref<'ALL' | 'NEWS' | 'CHANGELOG'>('ALL')
const statusFilter = ref<'ALL' | 'DRAFT' | 'PUBLISHED'>('ALL')
const currentPage = ref(1)
const pageSize = 20
const activeLocale = ref<(typeof NEWS_EDITOR_LOCALES)[number]>('en')
const draft = ref(createEmptysDraft())
const selectedEntryId = ref<string | null>(null)

const filteredEntries = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  return (feed.value?.items ?? []).filter((entry) => {
    if (typeFilter.value !== 'ALL' && entry.entryType !== typeFilter.value) return false
    if (statusFilter.value !== 'ALL' && entry.status !== statusFilter.value) return false

    if (!query) return true

    const localized = pickGamesLocalization(entry.localizations, locale.value)
    return [localized?.title ?? '', localized?.summary ?? ''].join(' ').toLowerCase().includes(query)
  })
})

const totalPages = computed(() => Math.max(1, Math.ceil(filteredEntries.value.length / pageSize)))
const pagedEntries = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  return filteredEntries.value.slice(start, start + pageSize)
})
const selectedEntry = computed(() => feed.value?.items.find((entry) => entry.id === selectedEntryId.value) ?? null)
const canSave = computed(() =>
  draft.value.localizations.every((localization) => localization.title.trim() && localization.summary.trim()),
)

function setPage(page: number) {
  currentPage.value = Math.max(1, Math.min(page, totalPages.value))
}

function resetDraft(entry?: GamesEntry) {
  if (entry) {
    draft.value = {
      entryId: entry.id,
      entryType: entry.entryType,
      status: entry.status,
      localizations: entry.localizations.map((localization) => ({ ...localization })),
    }
    selectedEntryId.value = entry.id
  } else {
    draft.value = createEmptysDraft()
    selectedEntryId.value = null
  }

  NEWS_EDITOR_LOCALES.forEach((localeCode) => {
    draft.value = {
      ...draft.value,
      localizations: upsertsLocalization(draft.value.localizations, localeCode, { title: '', summary: '', htmlContent: '' }),
    }
  })

  activeLocale.value = 'en'
}

function updateDraft(value: typeof draft.value) {
  draft.value = value
}

function formatDate(value: string | null) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
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
    if (selectedEntryId.value) {
      const current = data.gameNewsFeed.items.find((entry) => entry.id === selectedEntryId.value)
      if (current) {
        resetDraft(current)
      }
    }
  } catch (caughtError) {
    error.value = caughtError instanceof Error ? caughtError.message : t('operations.news.loadFailed')
  } finally {
    loading.value = false
  }
}

async function saveEntry() {
  saving.value = true
  actionError.value = null
  actionMessage.value = null

  try {
    await adminStore.upsertGamesEntry(draft.value)
    await Promise.all([loadFeed(), newsStore.fetchUnreadCount()])
    actionMessage.value = t('admin.newsSaved')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.newsSaveFailed')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  resetDraft()
  await loadFeed()
})
</script>

<template>
  <div class="ops-news">
    <div class="ops-section-header">
      <div>
        <h2>{{ t('operations.news.title') }}</h2>
        <p>{{ t('operations.news.subtitle') }}</p>
      </div>
      <button type="button" class="btn btn-primary" @click="resetDraft()">{{ t('operations.news.compose') }}</button>
    </div>

    <div v-if="actionError" class="ops-banner ops-banner-error">{{ actionError }}</div>
    <div v-else-if="actionMessage" class="ops-banner">{{ actionMessage }}</div>

    <div class="ops-controls">
      <input
        v-model="searchQuery"
        class="form-input ops-search"
        :placeholder="t('operations.news.searchPlaceholder')"
        @input="setPage(1)"
      />
      <select v-model="typeFilter" class="form-select ops-filter" @change="setPage(1)">
        <option value="ALL">{{ t('operations.news.filterAll') }}</option>
        <option value="NEWS">{{ t('operations.news.filterNews') }}</option>
        <option value="CHANGELOG">{{ t('operations.news.filterChangelog') }}</option>
      </select>
      <select v-model="statusFilter" class="form-select ops-filter" @change="setPage(1)">
        <option value="ALL">{{ t('operations.news.filterAll') }}</option>
        <option value="DRAFT">{{ t('operations.news.filterDraft') }}</option>
        <option value="PUBLISHED">{{ t('operations.news.filterPublished') }}</option>
      </select>
    </div>

    <div v-if="loading" class="ops-loading">{{ t('common.loading') }}</div>
    <div v-else-if="error" class="ops-error card">
      <p>{{ error }}</p>
      <button type="button" class="btn btn-secondary" @click="loadFeed">{{ t('common.retry') }}</button>
    </div>
    <div v-else class="ops-news-layout">
      <section class="card ops-news-list-panel">
        <div class="ops-news-list-header">
          <h3>{{ t('operations.news.listTitle') }}</h3>
          <span>{{ t('operations.news.pageSummary', { page: currentPage, total: totalPages }) }}</span>
        </div>
        <div v-if="pagedEntries.length === 0" class="ops-empty-state">{{ t('operations.news.noEntries') }}</div>
        <button
          v-for="entry in pagedEntries"
          :key="entry.id"
          type="button"
          class="ops-entry-card"
          :class="{ active: selectedEntryId === entry.id }"
          @click="resetDraft(entry)"
        >
          <div class="ops-entry-topline">
            <span class="badge badge-primary">{{ entry.entryType }}</span>
            <span class="badge" :class="entry.status === 'PUBLISHED' ? 'badge-success' : 'badge-warning'">
              {{ entry.status }}
            </span>
          </div>
          <strong>{{ pickGamesLocalization(entry.localizations, locale)?.title ?? t('news.untitled') }}</strong>
          <p>{{ pickGamesLocalization(entry.localizations, locale)?.summary }}</p>
          <div class="ops-entry-meta">
            <span>{{ formatDate(entry.updatedAtUtc) }}</span>
            <span>{{ entry.targetServerKey ?? t('admin.globalScope') }}</span>
          </div>
        </button>

        <div class="ops-pagination">
          <button type="button" class="btn btn-ghost btn-sm" :disabled="currentPage === 1" @click="setPage(currentPage - 1)">
            {{ t('common.previous') }}
          </button>
          <button
            type="button"
            class="btn btn-ghost btn-sm"
            :disabled="currentPage === totalPages"
            @click="setPage(currentPage + 1)"
          >
            {{ t('common.next') }}
          </button>
        </div>
      </section>

      <OperationsNewsEditorPanel
        :draft="draft"
        :active-locale="activeLocale"
        :saving="saving"
        :can-save="canSave"
        :selected-entry="selectedEntry"
        @update:active-locale="activeLocale = $event"
        @update:draft="updateDraft"
        @save="saveEntry"
        @cancel="resetDraft(selectedEntry ?? undefined)"
      />
    </div>
  </div>
</template>

<style scoped>
.ops-news {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.ops-section-header,
.ops-controls,
.ops-news-list-header,
.ops-entry-topline,
.ops-entry-meta,
.ops-pagination {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.75rem;
  align-items: center;
}

.ops-section-header p,
.ops-news-list-header span,
.ops-entry-card p,
.ops-entry-meta {
  color: var(--color-text-secondary);
}

.ops-search {
  flex: 1;
  min-width: 220px;
}

.ops-filter {
  min-width: 170px;
}

.ops-loading {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-secondary);
}

.ops-error,
.ops-banner {
  padding: 0.9rem 1rem;
}

.ops-banner {
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.1);
}

.ops-banner-error {
  border-color: rgba(248, 113, 113, 0.4);
  background: rgba(248, 113, 113, 0.12);
}

.ops-news-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(340px, 0.9fr);
  gap: 1rem;
  align-items: start;
}

.ops-news-list-panel {
  padding: 1.25rem;
  display: grid;
  gap: 0.85rem;
}

.ops-entry-card {
  text-align: left;
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.02);
  display: grid;
  gap: 0.6rem;
  cursor: pointer;
}

.ops-entry-card.active {
  border-color: rgba(255, 192, 122, 0.4);
  background: rgba(255, 192, 122, 0.08);
}

.ops-entry-meta {
  font-size: 0.82rem;
}

.ops-empty-state {
  padding: 1rem;
  border: 1px dashed var(--color-border);
  border-radius: var(--radius-md);
  color: var(--color-text-secondary);
}

@media (max-width: 960px) {
  .ops-news-layout {
    grid-template-columns: 1fr;
  }
}
</style>
