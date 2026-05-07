<script setup lang="ts">
import DOMPurify from 'dompurify'
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { pickGamesLocalization } from '@/lib/news'
import { useAuthStore } from '@/stores/auth'
import { usesStore } from '@/stores/news'
import type { GamesEntry } from '@/types'

const { t, locale } = useI18n()
const auth = useAuthStore()
const newsStore = usesStore()

const filter = ref<'ALL' | 'NEWS' | 'CHANGELOG' | 'MARKET_REPORT'>('ALL')
const viewError = ref<string | null>(null)
const actionMessage = ref<string | null>(null)
const actionError = ref<string | null>(null)
const markAllLoading = ref(false)
const currentPage = ref(1)
const pageSize = 10

/** IDs that were unread when the page first loaded – used to keep "" badges
 *  visible even after the background markRead call completes. */
const initiallyUnreadIds = ref<Set<string>>(new Set())

const filteredEntries = computed(() => {
  const items = newsStore.feed?.items ?? []
  if (filter.value === 'ALL') {
    return items
  }

  return items.filter((entry) => entry.entryType === filter.value)
})

const totalPages = computed(() => {
  const total = filteredEntries.value.length
  return Math.max(1, Math.ceil(total / pageSize))
})

const pagedEntries = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  return filteredEntries.value.slice(start, start + pageSize)
})

const pageFrom = computed(() => {
  if (filteredEntries.value.length === 0) return 0
  return (currentPage.value - 1) * pageSize + 1
})

const pageTo = computed(() => Math.min(currentPage.value * pageSize, filteredEntries.value.length))

const canGoPrev = computed(() => currentPage.value > 1)
const canGoNext = computed(() => currentPage.value < totalPages.value)
const hasUnreadEntries = computed(() => (newsStore.feed?.items ?? []).some((entry) => entry.status === 'PUBLISHED' && !entry.isRead))

function goToPrevPage() {
  if (!canGoPrev.value) {
    return
  }

  currentPage.value -= 1
}

function goToNextPage() {
  if (!canGoNext.value) {
    return
  }

  currentPage.value += 1
}

function getLocalization(entry: GamesEntry) {
  return pickGamesLocalization(entry.localizations, locale.value)
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
function shouldShowSummary(entry: GamesEntry): boolean {
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
  return t('news.filters')
}

/** Returns E2E locator class plus Tailwind color utilities for the pill. */
function entryTypePillClass(entryType: string): string {
  if (entryType === 'CHANGELOG') return 'news-pill-changelog bg-[rgba(0,71,255,0.16)] text-[#8db3ff]'
  if (entryType === 'MARKET_REPORT') return 'news-pill-market bg-[rgba(0,200,150,0.16)] text-[#7af5d9]'
  return 'news-pill-news bg-[rgba(0,200,83,0.16)] text-[#7af5a9]'
}

async function loadFeed() {
  viewError.value = null
  actionError.value = null
  currentPage.value = 1

  try {
    const feed = await newsStore.fetchFeed(false)
    if (auth.isAuthenticated) {
      const unreadEntryIds = feed.items.filter((entry) => entry.status === 'PUBLISHED' && !entry.isRead).map((entry) => entry.id)
      initiallyUnreadIds.value = new Set(unreadEntryIds)
    } else {
      initiallyUnreadIds.value = new Set()
    }
  } catch (caughtError) {
    viewError.value = caughtError instanceof Error ? caughtError.message : t('news.loadFailed')
  }
}

async function onClickMarkAllRead() {
  actionMessage.value = null
  actionError.value = null

  if (!auth.isAuthenticated || !hasUnreadEntries.value || markAllLoading.value) {
    return
  }

  if (!window.confirm(t('news.markAllReadConfirm'))) {
    return
  }

  markAllLoading.value = true
  try {
    await newsStore.markAllRead()
    initiallyUnreadIds.value = new Set()
    actionMessage.value = t('news.markAllReadSuccess')
  } catch (caughtError) {
    actionError.value = caughtError instanceof Error ? caughtError.message : t('news.markAllReadFailed')
  } finally {
    markAllLoading.value = false
  }
}

onMounted(async () => {
  await loadFeed()
})

const onClickFilterAll = () => {
  filter.value = 'ALL'
  currentPage.value = 1
}
const onClickFilterNews = () => {
  filter.value = 'NEWS'
  currentPage.value = 1
}
const onClickFilterChangelog = () => {
  filter.value = 'CHANGELOG'
  currentPage.value = 1
}
const onClickFilterMarketReport = () => {
  filter.value = 'MARKET_REPORT'
  currentPage.value = 1
}
</script>

<template>
  <div class="pb-16">
    <!-- Hero -->
    <section
      class="py-14 border-b border-divider [background:radial-gradient(circle_at_top_left,rgba(255,138,0,0.18),transparent_40%),radial-gradient(circle_at_top_right,rgba(0,71,255,0.18),transparent_42%)]"
    >
      <div class="container grid gap-4">
        <p class="text-[0.72rem] font-bold uppercase tracking-[0.18em] text-[#ffc07a]">
          {{ t('news.eyebrow') }}
        </p>
        <h1 class="text-[clamp(2rem,4vw,3.2rem)] leading-[1.02] font-bold">
          {{ t('news.title') }}
        </h1>
        <p class="text-muted max-w-2xl">{{ t('news.subtitle') }}</p>

        <!-- Filter pills -->
        <div class="flex flex-wrap gap-3" role="tablist">
          <button
            type="button"
            class="px-4 py-2.5 rounded-full border transition-colors cursor-pointer"
            :class="filter === 'ALL' ? 'bg-[rgba(255,138,0,0.18)] border-[rgba(255,138,0,0.5)] text-[#ffd7a3]' : 'border-divider text-muted'"
            @click="onClickFilterAll"
          >
            {{ t('news.filterAll') }}
          </button>
          <button
            type="button"
            class="px-4 py-2.5 rounded-full border transition-colors cursor-pointer"
            :class="filter === 'NEWS' ? 'bg-[rgba(255,138,0,0.18)] border-[rgba(255,138,0,0.5)] text-[#ffd7a3]' : 'border-divider text-muted'"
            @click="onClickFilterNews"
          >
            {{ t('news.filters') }}
          </button>
          <button
            type="button"
            class="px-4 py-2.5 rounded-full border transition-colors cursor-pointer"
            :class="filter === 'CHANGELOG' ? 'bg-[rgba(255,138,0,0.18)] border-[rgba(255,138,0,0.5)] text-[#ffd7a3]' : 'border-divider text-muted'"
            @click="onClickFilterChangelog"
          >
            {{ t('news.filterChangelog') }}
          </button>
          <button
            type="button"
            class="px-4 py-2.5 rounded-full border transition-colors cursor-pointer"
            :class="filter === 'MARKET_REPORT' ? 'bg-[rgba(0,200,150,0.18)] border-[rgba(0,200,150,0.5)] text-[#7af5d9]' : 'border-divider text-muted'"
            @click="onClickFilterMarketReport"
          >
            📊 {{ t('news.filterMarketReport') }}
          </button>
        </div>
        <button
          type="button"
          class="btn btn-secondary news-mark-all-read-btn w-fit"
          :disabled="!auth.isAuthenticated || !hasUnreadEntries || markAllLoading || newsStore.loading"
          @click="onClickMarkAllRead"
        >
          {{ markAllLoading ? t('common.loading') : t('news.markAllRead') }}
        </button>
      </div>
    </section>

    <!-- Content -->
    <section class="container pt-8">
      <div v-if="actionError" class="state-card state-card-error mb-5 p-4 rounded-2xl border border-[rgba(248,113,113,0.5)] bg-card" role="alert">
        {{ actionError }}
      </div>
      <div v-else-if="actionMessage" class="state-card mb-5 p-4 rounded-2xl border border-[rgba(34,197,94,0.5)] bg-card" role="status">
        {{ actionMessage }}
      </div>

      <!-- Loading -->
      <div v-if="newsStore.loading" class="state-card p-6 rounded-2xl border border-divider bg-card grid gap-4">
        <p>{{ t('common.loading') }}</p>
      </div>

      <!-- Error -->
      <div v-else-if="viewError" class="state-card state-card-error p-6 rounded-2xl border border-[rgba(248,113,113,0.5)] bg-card grid gap-4">
        <p>{{ viewError }}</p>
        <button type="button" class="btn btn-secondary" @click="loadFeed">
          {{ t('common.tryAgain') }}
        </button>
      </div>

      <!-- Empty -->
      <div v-else-if="filteredEntries.length === 0" class="state-card p-6 rounded-2xl border border-divider bg-card grid gap-4">
        <p class="text-lg font-bold">{{ t('news.emptyTitle') }}</p>
        <p v-if="filter === 'MARKET_REPORT'" class="text-muted">{{ t('news.marketReportEmptyBody') }}</p>
        <p v-else class="text-muted">{{ t('news.emptyBody') }}</p>
      </div>

      <!-- Entry list -->
      <div v-else class="grid gap-5">
        <div v-if="totalPages > 1" class="pagination-controls flex items-center justify-between gap-4 rounded-xl border border-divider bg-card px-4 py-3 max-sm:flex-col max-sm:items-stretch">
          <p class="text-sm text-muted">
            {{ t('news.paginationStatus', { from: pageFrom, to: pageTo, total: filteredEntries.length }) }}
          </p>
          <div class="flex items-center gap-2">
            <button type="button" class="btn btn-secondary" :disabled="!canGoPrev" @click="goToPrevPage">
              {{ t('news.previousPage') }}
            </button>
            <p class="text-sm text-muted min-w-30 text-center">
              {{ t('news.pageLabel', { page: currentPage, total: totalPages }) }}
            </p>
            <button type="button" class="btn btn-secondary" :disabled="!canGoNext" @click="goToNextPage">
              {{ t('news.nextPage') }}
            </button>
          </div>
        </div>

        <article
          v-for="entry in pagedEntries"
          :key="entry.id"
          class="news-card p-6 rounded-2xl border shadow-sm"
          :class="{
            'news-card-market border-[rgba(0,200,150,0.25)]': entry.entryType === 'MARKET_REPORT',
            'border-divider': entry.entryType !== 'MARKET_REPORT',
            'news-card-unread': initiallyUnreadIds.has(entry.id),
          }"
          :style="{
            background: entry.entryType === 'MARKET_REPORT' ? 'linear-gradient(180deg,rgba(0,200,150,0.04),rgba(255,255,255,0))' : 'linear-gradient(180deg,rgba(255,255,255,0.03),rgba(255,255,255,0))',
            borderLeft: initiallyUnreadIds.has(entry.id) ? '4px solid rgba(255,138,0,0.7)' : undefined,
          }"
        >
          <!-- Card header -->
          <div class="flex justify-between gap-4 mb-4 max-sm:flex-col">
            <div class="grid gap-2">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="inline-flex items-center px-[0.65rem] py-[0.3rem] rounded-full text-[0.74rem] uppercase tracking-[0.08em]" :class="entryTypePillClass(entry.entryType)">
                  {{ entryTypeLabel(entry.entryType) }}
                </span>
                <span
                  v-if="initiallyUnreadIds.has(entry.id)"
                  class="news-unread-badge inline-flex items-center px-[0.55rem] py-[0.2rem] rounded-full text-[0.7rem] uppercase tracking-[0.08em] bg-[rgba(255,138,0,0.18)] text-[#ffd7a3] border border-[rgba(255,138,0,0.35)]"
                >
                  {{ t('news.unread') }}
                </span>
              </div>
              <h2 class="mt-1.5 text-2xl font-bold">
                {{ getLocalization(entry)?.title ?? t('news.untitled') }}
              </h2>
            </div>
            <p class="text-muted text-[0.82rem] whitespace-nowrap max-sm:whitespace-normal">
              {{ formatDate(entry.publishedAtUtc ?? entry.updatedAtUtc) }}
            </p>
          </div>

          <p v-if="shouldShowSummary(entry)" class="mb-4 text-muted text-base">
            {{ getLocalization(entry)?.summary }}
          </p>

          <div class="news-card-body" v-html="sanitizeHtml(getLocalization(entry)?.htmlContent ?? '')"></div>
        </article>
      </div>
    </section>
  </div>
</template>

<style scoped>
/* ── Only :deep() rules for v-html content remain – cannot be expressed as Tailwind ── */

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

.news-card-body :deep(.mr-rank-top1) {
  color: #ffd700;
}
.news-card-body :deep(.mr-rank-top2) {
  color: #c0c0c0;
}
.news-card-body :deep(.mr-rank-top3) {
  color: #cd7f32;
}

.news-card-body :deep(.mr-industry) {
  color: var(--color-text-secondary, #aaa);
  font-size: 0.78rem;
}

.news-card-body :deep(.mr-positive) {
  color: #7af5a9;
  font-weight: 600;
}
.news-card-body :deep(.mr-neutral) {
  color: #ffd7a3;
  font-weight: 600;
}
.news-card-body :deep(.mr-negative) {
  color: #f87171;
  font-weight: 600;
}

.news-card-body :deep(.mr-empty) {
  color: var(--color-text-secondary, #aaa);
  font-style: italic;
  padding: 1rem 0;
}

@media (max-width: 720px) {
  .news-card-body :deep(.mr-table th:nth-child(4)),
  .news-card-body :deep(.mr-table td:nth-child(4)),
  .news-card-body :deep(.mr-table th:nth-child(6)),
  .news-card-body :deep(.mr-table td:nth-child(6)) {
    display: none;
  }
}
</style>
