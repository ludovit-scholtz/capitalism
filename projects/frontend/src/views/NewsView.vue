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

const filter = ref<'ALL' | 'NEWS' | 'CHANGELOG' | 'MARKET_REPORT'>('ALL')
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

/** Returns the plain text of an HTML string by stripping all tags. */
function htmlToPlainText(html: string): string {
  return html.replace(/<[^>]*>/g, '').trim()
}

/**
 * Returns true when the summary should be shown to the user.
 * For CHANGELOG entries the summary is hidden when it is empty or would merely
 * repeat the plain-text content that is already rendered in the HTML body.
 */
function shouldShowSummary(entry: GameNewsEntry): boolean {
  const loc = getLocalization(entry)
  if (!loc?.summary) return false
  if (entry.entryType === 'CHANGELOG') {
    const bodyText = htmlToPlainText(loc.htmlContent)
    return loc.summary.trim() !== bodyText
  }
  return true
}

function entryTypeLabel(entryType: string): string {
  if (entryType === 'CHANGELOG') return t('news.filterChangelog')
  if (entryType === 'MARKET_REPORT') return t('news.filterMarketReport')
  return t('news.filterNews')
}

function entryTypePillClass(entryType: string): string {
  if (entryType === 'CHANGELOG') return 'news-pill-changelog'
  if (entryType === 'MARKET_REPORT') return 'news-pill-market'
  return 'news-pill-news'
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
          <button
            type="button"
            class="news-filter news-filter-market"
            :class="{ active: filter === 'MARKET_REPORT' }"
            @click="filter = 'MARKET_REPORT'"
          >
            📊 {{ t('news.filterMarketReport') }}
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
        <p v-if="filter === 'MARKET_REPORT'" class="state-copy">{{ t('news.marketReportEmptyBody') }}</p>
        <p v-else class="state-copy">{{ t('news.emptyBody') }}</p>
      </div>

      <div v-else class="news-entry-list">
        <article
          v-for="entry in entries"
          :key="entry.id"
          class="news-card"
          :class="{
            'news-card-unread': initiallyUnreadIds.has(entry.id),
            'news-card-market': entry.entryType === 'MARKET_REPORT',
          }"
        >
          <div class="news-card-header">
            <div class="news-card-meta">
              <div class="news-card-pills">
                <span class="news-pill" :class="entryTypePillClass(entry.entryType)">
                  {{ entryTypeLabel(entry.entryType) }}
                </span>
                <span v-if="initiallyUnreadIds.has(entry.id)" class="news-unread-badge">{{ t('news.unread') }}</span>
              </div>
              <h2 class="news-card-title">{{ getLocalization(entry)?.title ?? t('news.untitled') }}</h2>
            </div>
            <p class="news-card-date">{{ formatDate(entry.publishedAtUtc ?? entry.updatedAtUtc) }}</p>
          </div>

          <p v-if="shouldShowSummary(entry)" class="news-card-summary">
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

.news-filter-market.active {
  background: rgba(0, 200, 150, 0.18);
  border-color: rgba(0, 200, 150, 0.5);
  color: #7af5d9;
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

.news-card-market {
  border-color: rgba(0, 200, 150, 0.25);
  background: linear-gradient(180deg, rgba(0, 200, 150, 0.04), rgba(255, 255, 255, 0));
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

.news-pill-market {
  background: rgba(0, 200, 150, 0.16);
  color: #7af5d9;
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

/* ── Market Report card body styles ───────────────────────────── */

.news-card-body :deep(.market-report) {
  display: grid;
  gap: 1.25rem;
}

.news-card-body :deep(.mr-summary) {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem 2.5rem;
  padding: 1rem 1.25rem;
  background: rgba(0, 200, 150, 0.06);
  border-radius: var(--radius-md, 0.5rem);
  border: 1px solid rgba(0, 200, 150, 0.15);
}

.news-card-body :deep(.mr-summary-item) {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.news-card-body :deep(.mr-label) {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--color-text-secondary, #aaa);
}

.news-card-body :deep(.mr-value) {
  font-size: 1rem;
  font-weight: 600;
}

.news-card-body :deep(.mr-value-highlight) {
  color: #7af5d9;
}

.news-card-body :deep(.mr-table) {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.news-card-body :deep(.mr-table th) {
  text-align: left;
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  font-size: 0.74rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--color-text-secondary, #aaa);
  white-space: nowrap;
}

.news-card-body :deep(.mr-table td) {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.news-card-body :deep(.mr-rank) {
  font-weight: 700;
  width: 2rem;
  text-align: center;
  color: var(--color-text-secondary, #aaa);
}

.news-card-body :deep(.mr-rank-top1) { color: #ffd700; }
.news-card-body :deep(.mr-rank-top2) { color: #c0c0c0; }
.news-card-body :deep(.mr-rank-top3) { color: #cd7f32; }

.news-card-body :deep(.mr-industry) {
  color: var(--color-text-secondary, #aaa);
  font-size: 0.78rem;
}

.news-card-body :deep(.mr-positive) { color: #7af5a9; font-weight: 600; }
.news-card-body :deep(.mr-neutral)  { color: #ffd7a3; font-weight: 600; }
.news-card-body :deep(.mr-negative) { color: #f87171; font-weight: 600; }

.news-card-body :deep(.mr-empty) {
  color: var(--color-text-secondary, #aaa);
  font-style: italic;
  padding: 1rem 0;
}

@media (max-width: 720px) {
  .news-card-header {
    flex-direction: column;
  }

  .news-card-date {
    white-space: normal;
  }

  .news-card-body :deep(.mr-table th:nth-child(4)),
  .news-card-body :deep(.mr-table td:nth-child(4)),
  .news-card-body :deep(.mr-table th:nth-child(6)),
  .news-card-body :deep(.mr-table td:nth-child(6)) {
    display: none;
  }
}
</style>
