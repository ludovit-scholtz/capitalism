<script setup lang="ts">
import DOMPurify from 'dompurify'
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { pickGameNewsLocalization } from '@/lib/news'
import { useAuthStore } from '@/stores/auth'
import { useNewsStore } from '@/stores/news'
import type { GameNewsEntry } from '@/types'

const { t, locale } = useI18n()
const auth = useAuthStore()
const newsStore = useNewsStore()

const filter = ref<'ALL' | 'NEWS' | 'CHANGELOG'>('ALL')
const viewError = ref<string | null>(null)

/** IDs that were unread when the page first loaded – used to keep "New" badges
 *  visible even after the background markRead call completes. */
const initiallyUnreadIds = ref<Set<string>>(new Set())

const entries = computed(() => {
  const items = newsStore.feed?.items ?? []
  if (filter.value === 'ALL') {
    return items
  }

  return items.filter((entry) => entry.entryType === filter.value)
})

function getLocalization(entry: GameNewsEntry) {
  return pickGameNewsLocalization(entry.localizations, locale.value)
}

function formatDate(value: string | null) {
  if (!value) {
    return t('common.notAvailable')
  }

  return new Intl.DateTimeFormat(locale.value, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function sanitizeHtml(html: string) {
  return DOMPurify.sanitize(html)
}

async function loadFeed() {
  viewError.value = null

  try {
    const feed = await newsStore.fetchFeed(false)
    if (auth.isAuthenticated) {
      const unreadEntryIds = feed.items
        .filter((entry) => entry.status === 'PUBLISHED' && !entry.isRead)
        .map((entry) => entry.id)

      // Capture unread state BEFORE markRead so the "New" badge stays
      // visible for the duration of the page visit even after the
      // background mark-read call updates the store.
      initiallyUnreadIds.value = new Set(unreadEntryIds)

      if (unreadEntryIds.length > 0) {
        await newsStore.markRead(unreadEntryIds)
      }
    }
  } catch (caughtError) {
    viewError.value = caughtError instanceof Error ? caughtError.message : t('news.loadFailed')
  }
}

onMounted(async () => {
  await loadFeed()
})
</script>

<template>
  <div class="news-view">
    <section class="news-hero">
      <div class="container news-hero-inner">
        <p class="news-eyebrow">{{ t('news.eyebrow') }}</p>
        <h1 class="news-title">{{ t('news.title') }}</h1>
        <p class="news-subtitle">{{ t('news.subtitle') }}</p>
        <div class="news-filter-row" role="tablist">
          <button type="button" class="news-filter" :class="{ active: filter === 'ALL' }" @click="filter = 'ALL'">
            {{ t('news.filterAll') }}
          </button>
          <button type="button" class="news-filter" :class="{ active: filter === 'NEWS' }" @click="filter = 'NEWS'">
            {{ t('news.filterNews') }}
          </button>
          <button type="button" class="news-filter" :class="{ active: filter === 'CHANGELOG' }" @click="filter = 'CHANGELOG'">
            {{ t('news.filterChangelog') }}
          </button>
        </div>
      </div>
    </section>

    <section class="container news-content">
      <div v-if="newsStore.loading" class="state-card">
        <p>{{ t('common.loading') }}</p>
      </div>

      <div v-else-if="viewError" class="state-card state-card-error">
        <p>{{ viewError }}</p>
        <button type="button" class="btn btn-secondary" @click="loadFeed">
          {{ t('common.tryAgain') }}
        </button>
      </div>

      <div v-else-if="entries.length === 0" class="state-card">
        <p class="state-title">{{ t('news.emptyTitle') }}</p>
        <p class="state-copy">{{ t('news.emptyBody') }}</p>
      </div>

      <div v-else class="news-entry-list">
        <article v-for="entry in entries" :key="entry.id" class="news-card" :class="{ 'news-card-unread': initiallyUnreadIds.has(entry.id) }">
          <div class="news-card-header">
            <div class="news-card-meta">
              <div class="news-card-pills">
                <span class="news-pill" :class="entry.entryType === 'CHANGELOG' ? 'news-pill-changelog' : 'news-pill-news'">
                  {{ entry.entryType === 'CHANGELOG' ? t('news.filterChangelog') : t('news.filterNews') }}
                </span>
                <span v-if="initiallyUnreadIds.has(entry.id)" class="news-unread-badge">{{ t('news.unread') }}</span>
              </div>
              <h2 class="news-card-title">{{ getLocalization(entry)?.title ?? t('news.untitled') }}</h2>
            </div>
            <p class="news-card-date">{{ formatDate(entry.publishedAtUtc ?? entry.updatedAtUtc) }}</p>
          </div>

          <p v-if="getLocalization(entry)?.summary" class="news-card-summary">
            {{ getLocalization(entry)?.summary }}
          </p>

          <div class="news-card-body" v-html="sanitizeHtml(getLocalization(entry)?.htmlContent ?? '')"></div>
        </article>
      </div>
    </section>
  </div>
</template>

<style scoped>
.news-view {
  padding-bottom: 4rem;
}

.news-hero {
  padding: 3.5rem 0 2rem;
  background: radial-gradient(circle at top left, rgba(255, 138, 0, 0.18), transparent 40%), radial-gradient(circle at top right, rgba(0, 71, 255, 0.18), transparent 42%);
  border-bottom: 1px solid var(--color-border);
}

.news-hero-inner {
  display: grid;
  gap: 1rem;
}

.news-eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.18em;
  color: #ffc07a;
  font-size: 0.72rem;
}

.news-title {
  font-size: clamp(2rem, 4vw, 3.2rem);
  line-height: 1.02;
}

.news-subtitle {
  max-width: 45rem;
  color: var(--color-text-secondary);
}

.news-filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.news-filter {
  padding: 0.7rem 1rem;
  border-radius: 999px;
  border: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.02);
  color: var(--color-text-secondary);
}

.news-filter.active {
  background: rgba(255, 138, 0, 0.18);
  border-color: rgba(255, 138, 0, 0.5);
  color: #ffd7a3;
}

.news-content {
  padding-top: 2rem;
}

.state-card {
  padding: 1.5rem;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  display: grid;
  gap: 1rem;
}

.state-card-error {
  border-color: rgba(248, 113, 113, 0.5);
}

.state-title {
  font-size: 1.1rem;
  font-weight: 700;
}

.state-copy {
  color: var(--color-text-secondary);
}

.news-entry-list {
  display: grid;
  gap: 1.25rem;
}

.news-card {
  padding: 1.5rem;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-border);
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.03), rgba(255, 255, 255, 0));
  box-shadow: var(--shadow-sm);
}

.news-card-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.news-card-meta {
  display: grid;
  gap: 0.5rem;
}

.news-card-pills {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.news-card-unread {
  border-left: 3px solid rgba(255, 138, 0, 0.7);
}

.news-unread-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.55rem;
  border-radius: 999px;
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  background: rgba(255, 138, 0, 0.18);
  color: #ffd7a3;
  border: 1px solid rgba(255, 138, 0, 0.35);
}

.news-card-title {
  margin-top: 0.4rem;
  font-size: 1.5rem;
}

.news-card-date {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
  white-space: nowrap;
}

.news-pill {
  display: inline-flex;
  align-items: center;
  padding: 0.3rem 0.65rem;
  border-radius: 999px;
  font-size: 0.74rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.news-pill-news {
  background: rgba(0, 200, 83, 0.16);
  color: #7af5a9;
}

.news-pill-changelog {
  background: rgba(0, 71, 255, 0.16);
  color: #8db3ff;
}

.news-card-summary {
  margin-bottom: 1rem;
  color: var(--color-text-secondary);
  font-size: 1rem;
}

.news-card-body :deep(p) + :deep(p) {
  margin-top: 0.85rem;
}

.news-card-body :deep(ul) {
  margin: 0.85rem 0 0.85rem 1.25rem;
}

@media (max-width: 720px) {
  .news-card-header {
    flex-direction: column;
  }

  .news-card-date {
    white-space: normal;
  }
}
</style>
