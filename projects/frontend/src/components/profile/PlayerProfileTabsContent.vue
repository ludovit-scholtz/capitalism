<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { gqlRequest } from '@/lib/graphql'
import { formatCompactMoney } from '@/lib/currencyFormat'
import { useAuthStore } from '@/stores/auth'
import PlayerBadgeGrid from '@/components/profile/PlayerBadgeGrid.vue'
import type { PlayerBadge } from '@/components/profile/PlayerBadgeGrid.vue'
import RankHistoryChart from '@/components/profile/RankHistoryChart.vue'
import type { RankSnapshot } from '@/components/profile/RankHistoryChart.vue'
import { buildProfileStatsCsv } from '@/lib/profileStatsExport'

export interface PlayerHallOfFame {
  highestSingleTickRevenue: number
  highestSingleTickRevenueTick: number
  largestBuildingAcquisitionPrice: number
  largestBuildingAcquisitionName: string | null
  highestBrandQuality: number
  highestBrandQualityName: string | null
  accountAgeTicks: number
}

export interface PlayerProfile {
  playerId: string
  displayName: string
  bio: string | null
  createdAtUtc: string
  joinGameYear: number
  hasProSubscription: boolean
  totalWealthUsd: number
  totalCompanyEquityUsd: number
  companyCount: number
  leaderboardRank: number
  activeBuildingTypes: string[]
  citiesWithBuildings: number
  totalProductsSold: number
  hallOfFame: PlayerHallOfFame
}

const props = defineProps<{
  profile: PlayerProfile
  playerId: string
  isOwnProfile: boolean
}>()

const { t, locale } = useI18n()
const auth = useAuthStore()

// ── Tabs ───────────────────────────────────────────────────────────────────────
type ProfileTab = 'overview' | 'achievements' | 'rank-history'
const activeTab = ref<ProfileTab>('overview')

// ── Badges ─────────────────────────────────────────────────────────────────────
const badges = ref<PlayerBadge[]>([])
const badgesLoading = ref(false)
const badgesLoaded = ref(false)

// ── Rank History ───────────────────────────────────────────────────────────────
const rankSnapshots = ref<RankSnapshot[]>([])
const rankLoading = ref(false)
const rankLoaded = ref(false)

// ── Stats Export ───────────────────────────────────────────────────────────────
const exportLoading = ref(false)
const exportSuccess = ref<string | null>(null)
const exportError = ref<string | null>(null)

// ── Queries ────────────────────────────────────────────────────────────────────

const PLAYER_BADGES_QUERY = `
  query GetPlayerBadges($playerId: UUID!) {
    playerBadges(playerId: $playerId) {
      id badgeType rarity unlockCondition unlockedAtUtc unlockedAtTick
    }
  }
`

const PLAYER_RANK_HISTORY_QUERY = `
  query GetPlayerRankHistory($playerId: UUID!, $ticksBack: Int!) {
    rankHistory(playerId: $playerId, ticksBack: $ticksBack) {
      snapshotTick snapshotUtc leaderboardRank wealthUsd percentileRank positionChange
    }
  }
`

// ── Helpers ────────────────────────────────────────────────────────────────────

function formatMoney(value: number): string {
  return formatCompactMoney(value, 'USD', locale.value)
}

function formatBuildingType(type: string): string {
  return t(`buildings.types.${type}`, type)
}

function rankBadge(rank: number): string {
  if (rank === 1) return '🥇'
  if (rank === 2) return '🥈'
  if (rank === 3) return '🥉'
  return `#${rank}`
}

function formatBrandQualityPercent(value: number): string {
  return `${Math.round(value * 100)}%`
}

// ── Tab actions ────────────────────────────────────────────────────────────────

async function fetchBadges() {
  if (badgesLoaded.value) return
  badgesLoading.value = true
  try {
    const data = await gqlRequest<{ playerBadges: PlayerBadge[] }>(
      PLAYER_BADGES_QUERY,
      { playerId: props.playerId },
    )
    badges.value = data.playerBadges ?? []
    badgesLoaded.value = true
  } catch {
    badges.value = []
  } finally {
    badgesLoading.value = false
  }
}

async function fetchRankHistory() {
  if (rankLoaded.value) return
  rankLoading.value = true
  try {
    const data = await gqlRequest<{ rankHistory: RankSnapshot[] }>(
      PLAYER_RANK_HISTORY_QUERY,
      { playerId: props.playerId, ticksBack: 365 },
    )
    rankSnapshots.value = data.rankHistory ?? []
    rankLoaded.value = true
  } catch {
    rankSnapshots.value = []
  } finally {
    rankLoading.value = false
  }
}

async function switchTab(tab: ProfileTab) {
  activeTab.value = tab
  if (tab === 'achievements' && !badgesLoaded.value) {
    await fetchBadges()
  } else if (tab === 'rank-history' && !rankLoaded.value) {
    await fetchRankHistory()
  }
}

function triggerDownload(fileName: string, content: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

function exportCsv() {
  if (!auth.isAuthenticated || !props.isOwnProfile) return
  exportLoading.value = true
  exportSuccess.value = null
  exportError.value = null
  try {
    const csv = buildProfileStatsCsv({
      displayName: props.profile.displayName,
      leaderboardRank: props.profile.leaderboardRank,
      totalWealthUsd: props.profile.totalWealthUsd,
      totalCompanyEquityUsd: props.profile.totalCompanyEquityUsd,
      companyCount: props.profile.companyCount,
      totalProductsSold: props.profile.totalProductsSold,
      citiesWithBuildings: props.profile.citiesWithBuildings,
      activeBuildingTypes: props.profile.activeBuildingTypes,
      badgeTypes: badges.value.map((badge) => badge.badgeType),
      bestRank: (() => {
        const ranks = rankSnapshots.value.map((snapshot) => snapshot.leaderboardRank)
        return ranks.length > 0 ? Math.min(...ranks) : null
      })(),
    })
    const dateStr = new Date().toISOString().slice(0, 10)
    const safeDisplayName = props.profile.displayName.replace(/\s+/g, '_')
    triggerDownload(
      `${safeDisplayName}_stats_${dateStr}.csv`,
      csv,
      'text/csv;charset=utf-8',
    )
    exportSuccess.value = t('playerProfile.exportSuccess')
    setTimeout(() => {
      exportSuccess.value = null
    }, 4000)
  } catch (e) {
    exportError.value = e instanceof Error ? e.message : t('playerProfile.exportError')
  } finally {
    exportLoading.value = false
  }
}

function exportPdf() {
  if (!auth.isAuthenticated || !props.isOwnProfile) return
  window.print()
}
</script>

<template>
  <!-- Quick stats row -->
  <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 max-w-[900px] mx-auto mb-8">
    <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
      <div class="text-2xl font-extrabold text-brand mb-0.5">
        {{ profile.leaderboardRank > 0 ? rankBadge(profile.leaderboardRank) : '—' }}
      </div>
      <div class="text-xs text-muted">{{ t('playerProfile.globalRank') }}</div>
    </div>
    <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
      <div class="text-lg font-extrabold text-[color:var(--color-secondary)] mb-0.5 tabular-nums">
        {{ formatMoney(profile.totalWealthUsd) }}
      </div>
      <div class="text-xs text-muted">{{ t('playerProfile.totalWealth') }}</div>
    </div>
    <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
      <div class="text-2xl font-extrabold mb-0.5">{{ profile.companyCount }}</div>
      <div class="text-xs text-muted">{{ t('playerProfile.companies') }}</div>
    </div>
    <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
      <div class="text-2xl font-extrabold mb-0.5">{{ profile.citiesWithBuildings }}</div>
      <div class="text-xs text-muted">{{ t('playerProfile.cities') }}</div>
    </div>
  </div>

  <!-- Tab navigation -->
  <div class="max-w-[900px] mx-auto mb-6">
    <div class="profile-tabs" role="tablist">
      <button
        role="tab"
        :aria-selected="activeTab === 'overview'"
        class="profile-tab"
        :class="{ active: activeTab === 'overview' }"
        @click="switchTab('overview')"
      >
        📊 {{ t('playerProfile.tabOverview') }}
      </button>
      <button
        role="tab"
        :aria-selected="activeTab === 'achievements'"
        class="profile-tab"
        :class="{ active: activeTab === 'achievements' }"
        @click="switchTab('achievements')"
      >
        🏅 {{ t('playerProfile.tabAchievements') }}
      </button>
      <button
        role="tab"
        :aria-selected="activeTab === 'rank-history'"
        class="profile-tab"
        :class="{ active: activeTab === 'rank-history' }"
        @click="switchTab('rank-history')"
      >
        📈 {{ t('playerProfile.tabRankHistory') }}
      </button>
    </div>
  </div>

  <!-- Overview tab -->
  <div v-show="activeTab === 'overview'">
    <div class="grid grid-cols-1 md:grid-cols-2 gap-6 max-w-[900px] mx-auto">
      <!-- Left: Business overview -->
      <div class="flex flex-col gap-6">
        <!-- Industries -->
        <div class="bg-card border border-divider rounded-xl p-5">
          <h2 class="text-sm font-bold uppercase tracking-wide text-muted mb-3">
            🏭 {{ t('playerProfile.industries') }}
          </h2>
          <div v-if="profile.activeBuildingTypes.length > 0" class="flex flex-wrap gap-2">
            <span
              v-for="type in profile.activeBuildingTypes"
              :key="type"
              class="industry-tag text-xs font-semibold bg-surface border border-divider rounded-full px-3 py-1"
            >
              {{ formatBuildingType(type) }}
            </span>
          </div>
          <p v-else class="text-sm text-muted">{{ t('playerProfile.noIndustries') }}</p>
        </div>

        <!-- Sales stats -->
        <div class="bg-card border border-divider rounded-xl p-5">
          <h2 class="text-sm font-bold uppercase tracking-wide text-muted mb-3">
            📦 {{ t('playerProfile.salesStats') }}
          </h2>
          <div class="flex flex-col gap-2">
            <div class="flex justify-between items-center text-sm">
              <span class="text-muted">{{ t('playerProfile.totalProductsSold') }}</span>
              <span class="font-bold tabular-nums">
                {{
                  profile.totalProductsSold > 0
                    ? profile.totalProductsSold.toLocaleString(
                        locale === 'sk' ? 'sk-SK' : locale === 'de' ? 'de-DE' : 'en-US',
                        { maximumFractionDigits: 0 },
                      )
                    : '—'
                }}
              </span>
            </div>
            <div class="flex justify-between items-center text-sm">
              <span class="text-muted">{{ t('playerProfile.companyEquity') }}</span>
              <span class="font-bold tabular-nums text-[color:var(--color-secondary)]">
                {{ formatMoney(profile.totalCompanyEquityUsd) }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Right: Hall of Fame -->
      <div class="bg-card border border-divider rounded-xl p-5">
        <h2 class="text-sm font-bold uppercase tracking-wide text-muted mb-4">
          🏆 {{ t('playerProfile.hallOfFame') }}
        </h2>
        <div class="flex flex-col gap-4">
          <div class="hof-record flex flex-col gap-0.5">
            <span class="text-xs text-muted uppercase tracking-wide">
              {{ t('playerProfile.hof.highestRevenueTick') }}
            </span>
            <span class="text-lg font-extrabold text-[color:var(--color-secondary)] tabular-nums">
              {{
                profile.hallOfFame.highestSingleTickRevenue > 0
                  ? formatMoney(profile.hallOfFame.highestSingleTickRevenue)
                  : '—'
              }}
            </span>
            <span v-if="profile.hallOfFame.highestSingleTickRevenueTick > 0" class="text-xs text-muted">
              {{ t('playerProfile.hof.atTick', { tick: profile.hallOfFame.highestSingleTickRevenueTick }) }}
            </span>
          </div>

          <div class="border-t border-divider" />

          <div class="hof-record flex flex-col gap-0.5">
            <span class="text-xs text-muted uppercase tracking-wide">
              {{ t('playerProfile.hof.largestAcquisition') }}
            </span>
            <span class="text-lg font-extrabold tabular-nums">
              {{
                profile.hallOfFame.largestBuildingAcquisitionPrice > 0
                  ? formatMoney(profile.hallOfFame.largestBuildingAcquisitionPrice)
                  : '—'
              }}
            </span>
            <span v-if="profile.hallOfFame.largestBuildingAcquisitionName" class="text-xs text-muted">
              {{ profile.hallOfFame.largestBuildingAcquisitionName }}
            </span>
          </div>

          <div class="border-t border-divider" />

          <div class="hof-record flex flex-col gap-0.5">
            <span class="text-xs text-muted uppercase tracking-wide">
              {{ t('playerProfile.hof.highestBrandQuality') }}
            </span>
            <span class="text-lg font-extrabold text-good tabular-nums">
              {{
                profile.hallOfFame.highestBrandQuality > 0
                  ? formatBrandQualityPercent(profile.hallOfFame.highestBrandQuality)
                  : '—'
              }}
            </span>
            <span v-if="profile.hallOfFame.highestBrandQualityName" class="text-xs text-muted">
              {{ profile.hallOfFame.highestBrandQualityName }}
            </span>
          </div>

          <div class="border-t border-divider" />

          <div class="hof-record flex flex-col gap-0.5">
            <span class="text-xs text-muted uppercase tracking-wide">
              {{ t('playerProfile.hof.accountAge') }}
            </span>
            <span class="text-lg font-extrabold tabular-nums">
              {{ t('playerProfile.hof.ticks', { n: profile.hallOfFame.accountAgeTicks.toLocaleString('en-US') }) }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Achievements tab -->
  <div v-show="activeTab === 'achievements'">
    <div class="max-w-[900px] mx-auto">
      <div class="bg-card border border-divider rounded-xl p-5">
        <h2 class="text-sm font-bold uppercase tracking-wide text-muted mb-4">
          🏅 {{ t('playerProfile.achievementBadges') }}
        </h2>
        <PlayerBadgeGrid :badges="badges" :loading="badgesLoading" />
      </div>
    </div>
  </div>

  <!-- Rank History tab -->
  <div v-show="activeTab === 'rank-history'">
    <div class="max-w-[900px] mx-auto">
      <div class="bg-card border border-divider rounded-xl p-5">
        <h2 class="text-sm font-bold uppercase tracking-wide text-muted mb-4">
          📈 {{ t('playerProfile.rankHistory') }}
        </h2>
        <RankHistoryChart :snapshots="rankSnapshots" :loading="rankLoading" />
      </div>
    </div>
  </div>

  <!-- Export Stats & Back link -->
  <div class="max-w-[900px] mx-auto mt-8 flex items-center justify-between flex-wrap gap-3 export-section">
    <RouterLink
      to="/leaderboard"
      class="back-link text-sm text-muted hover:text-body hover:underline inline-flex items-center gap-1"
    >
      ← {{ t('playerProfile.backToLeaderboard') }}
    </RouterLink>

    <div v-if="auth.isAuthenticated && isOwnProfile" class="export-container flex items-center gap-2">
      <button class="export-btn btn btn-secondary text-sm" :disabled="exportLoading" @click="exportCsv">
        {{ exportLoading ? t('playerProfile.exporting') : t('playerProfile.exportCsv') }}
      </button>
      <button class="export-pdf-btn btn btn-secondary text-sm" @click="exportPdf">
        {{ t('playerProfile.exportPdf') }}
      </button>
    </div>
  </div>

  <div class="max-w-[900px] mx-auto mt-2 export-feedback">
    <p v-if="exportSuccess" class="text-good text-sm text-right">✓ {{ exportSuccess }}</p>
    <p v-if="exportError" class="text-bad text-sm text-right">{{ exportError }}</p>
  </div>
</template>

<style scoped>
/* Profile tabs */
.profile-tabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--color-border, #334155);
  padding-bottom: 0;
}

.profile-tab {
  padding: 8px 18px;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  color: var(--color-text-muted, #94a3b8);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s ease;
  margin-bottom: -1px;
}

.profile-tab:hover {
  color: var(--color-text-primary, #f1f5f9);
}

.profile-tab.active {
  color: #3b82f6;
  border-bottom-color: #3b82f6;
}

/* Export */
.export-container {
  flex-wrap: wrap;
}

@media print {
  .export-section,
  .export-feedback,
  .profile-tabs,
  .rank-filter-row,
  .rank-tooltip {
    display: none !important;
  }
}
</style>
