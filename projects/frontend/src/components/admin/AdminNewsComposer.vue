<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import RichTextEditor from '@/components/admin/RichTextEditor.vue'
import { createEmptysDraft, NEWS_EDITOR_LOCALES, pickGamesLocalization, upsertsLocalization } from '@/lib/news'
import { gqlRequest } from '@/lib/graphql'
import { useGameAdminStore } from '@/stores/gameAdmin'
import { usesStore } from '@/stores/news'
import type { GamesEntry, GamesFeed } from '@/types'

const { t, locale } = useI18n()
const adminStore = useGameAdminStore()
const newsStore = usesStore()

const newsEditor = ref(createEmptysDraft())
const activeLocale = ref<(typeof NEWS_EDITOR_LOCALES)[number]>('en')
const adminFeed = ref<GamesFeed | null>(null)
const adminFeedLoading = ref(false)
const adminFeedError = ref<string | null>(null)
const actionError = ref<string | null>(null)
const actionMessage = ref<string | null>(null)

const activeLocalization = computed(() => newsEditor.value.localizations.find((localization) => localization.locale === activeLocale.value))
const latestEntries = computed(() => adminFeed.value?.items ?? [])

async function loadAdminFeed() {
  adminFeedLoading.value = true
  adminFeedError.value = null
  try {
    const data = await gqlRequest<{ gameNewsFeed: GamesFeed }>(
      `query AdminNewsFeed {
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
            localizations {
              locale
              title
              summary
              htmlContent
            }
          }
        }
      }`,
    )
    adminFeed.value = data.gameNewsFeed
  } catch (caughtError) {
    adminFeedError.value = caughtError instanceof Error ? caughtError.message : t('admin.newsLoadFailed')
  } finally {
    adminFeedLoading.value = false
  }
}

function resetComposer() {
  newsEditor.value = createEmptysDraft()
  activeLocale.value = 'en'
}

function editEntry(entry: GamesEntry) {
  newsEditor.value = {
    entryId: entry.id,
    entryType: entry.entryType,
    status: entry.status,
    localizations: entry.localizations.map((localization) => ({ ...localization })),
  }
  activeLocale.value = 'en'
}

function updateLocalization<K extends 'title' | 'summary' | 'htmlContent'>(key: K, value: string) {
  newsEditor.value = {
    ...newsEditor.value,
    localizations: upsertsLocalization(newsEditor.value.localizations, activeLocale.value, { [key]: value }),
  }
}

function formatDate(value: string | null) {
  if (!value) return t('common.notAvailable')
  return new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function getLocalizedEntry(entry: GamesEntry) {
  return pickGamesLocalization(entry.localizations, locale.value)
}

function canEditEntry(entry: GamesEntry) {
  return entry.targetServerKey !== null || adminStore.session?.isRootAdministrator || adminStore.session?.hasGlobalAdminRole
}

async function saveEntry() {
  actionError.value = null
  actionMessage.value = null
  try {
    await adminStore.upsertGamesEntry(newsEditor.value)
    await Promise.all([loadAdminFeed(), newsStore.fetchUnreadCount()])
    actionMessage.value = t('admin.newsSaved')
    resetComposer()
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('admin.newsSaveFailed')
  }
}

onMounted(() => {
  loadAdminFeed()
})
</script>

<template>
  <article class="card admin-panel admin-panel-wide">
    <div class="admin-panel-header">
      <div>
        <h2>{{ t('admin.newsComposerTitle') }}</h2>
        <p>{{ t('admin.newsComposerBody') }}</p>
      </div>
      <button type="button" class="btn btn-ghost" @click="resetComposer">{{ t('admin.newEntry') }}</button>
    </div>

    <div v-if="actionError" class="composer-banner composer-banner-error">{{ actionError }}</div>
    <div v-else-if="actionMessage" class="composer-banner">{{ actionMessage }}</div>

    <div class="admin-composer-grid">
      <div class="admin-composer-form">
        <div class="admin-inline-fields">
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
          <input class="form-input" :value="activeLocalization?.title ?? ''" @input="updateLocalization('title', ($event.target as HTMLInputElement).value ?? '')" />
        </label>

        <label class="form-label">
          {{ t('admin.entrySummary') }}
          <textarea class="form-textarea" :value="activeLocalization?.summary ?? ''" @input="updateLocalization('summary', ($event.target as HTMLTextAreaElement).value ?? '')"></textarea>
        </label>

        <label class="form-label">
          {{ t('admin.entryContent') }}
          <RichTextEditor :model-value="activeLocalization?.htmlContent ?? ''" @update:model-value="updateLocalization('htmlContent', $event)" />
        </label>

        <div class="admin-composer-actions">
          <button type="button" class="btn btn-primary" @click="saveEntry">{{ t('admin.saveEntry') }}</button>
        </div>
      </div>

      <div class="admin-feed-list">
        <div v-if="adminFeedLoading" class="admin-empty-state">{{ t('common.loading') }}</div>
        <div v-else-if="adminFeedError" class="admin-empty-state">{{ adminFeedError }}</div>
        <article v-for="entry in latestEntries" :key="entry.id" class="admin-feed-card">
          <div class="admin-feed-topline">
            <span class="badge" :class="entry.status === 'PUBLISHED' ? 'badge-success' : 'badge-warning'">{{ entry.status }}</span>
            <span class="badge badge-primary">{{ entry.entryType }}</span>
          </div>
          <h3>{{ getLocalizedEntry(entry)?.title ?? t('news.untitled') }}</h3>
          <p>{{ getLocalizedEntry(entry)?.summary }}</p>
          <div class="admin-feed-meta">
            <span>{{ formatDate(entry.updatedAtUtc) }}</span>
            <span>{{ entry.targetServerKey ?? t('admin.globalScope') }}</span>
          </div>
          <button type="button" class="btn btn-secondary" :disabled="!canEditEntry(entry)" @click="editEntry(entry)">{{ t('admin.editEntry') }}</button>
        </article>
      </div>
    </div>
  </article>
</template>

<style scoped>
.composer-banner {
  margin-bottom: 1rem;
  padding: 0.85rem 1rem;
  border-radius: var(--radius-md);
  border: 1px solid rgba(34, 197, 94, 0.4);
  background: rgba(34, 197, 94, 0.14);
}

.composer-banner-error {
  border-color: rgba(248, 113, 113, 0.45);
  background: rgba(248, 113, 113, 0.12);
}

.admin-panel-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.admin-panel-header p {
  color: var(--color-text-secondary);
  margin-top: 0.3rem;
}

.admin-empty-state {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px dashed var(--color-border);
  color: var(--color-text-secondary);
}

.admin-composer-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(18rem, 0.75fr);
  gap: 1rem;
}

.admin-composer-form {
  display: grid;
  gap: 1rem;
}

.admin-inline-fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.locale-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem;
}

.locale-tab {
  padding: 0.55rem 0.8rem;
  border-radius: 999px;
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
}

.locale-tab.active {
  background: rgba(0, 71, 255, 0.18);
  border-color: rgba(0, 71, 255, 0.45);
  color: white;
}

.admin-composer-actions {
  display: flex;
  justify-content: flex-end;
}

.admin-feed-list {
  display: grid;
  gap: 0.8rem;
}

.admin-feed-card {
  padding: 1rem;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.02);
  display: grid;
  gap: 0.6rem;
}

.admin-feed-topline,
.admin-feed-meta {
  display: flex;
  gap: 0.6rem;
  flex-wrap: wrap;
  color: var(--color-text-secondary);
  font-size: 0.82rem;
}

@media (max-width: 1080px) {
  .admin-composer-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}

@media (max-width: 720px) {
  .admin-panel-header {
    flex-direction: column;
    align-items: stretch;
  }

  .admin-inline-fields {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
