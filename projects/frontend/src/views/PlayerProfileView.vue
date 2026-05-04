<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { formatCompactMoney } from '@/lib/currencyFormat'
import PlayerBadgeGrid from '@/components/profile/PlayerBadgeGrid.vue'
import type { PlayerBadge } from '@/components/profile/PlayerBadgeGrid.vue'
import RankHistoryChart from '@/components/profile/RankHistoryChart.vue'
import type { RankSnapshot } from '@/components/profile/RankHistoryChart.vue'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

// ── State ──────────────────────────────────────────────────────────────────────

interface PlayerHallOfFame {
  highestSingleTickRevenue: number
  highestSingleTickRevenueTick: number
  largestBuildingAcquisitionPrice: number
  largestBuildingAcquisitionName: string | null
  highestBrandQuality: number
  highestBrandQualityName: string | null
  accountAgeTicks: number
}

interface PlayerProfile {
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

const profile = ref<PlayerProfile | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

// Bio editing state (only for own profile)
const editingBio = ref(false)
const bioInput = ref('')
const bioSaving = ref(false)
const bioError = ref<string | null>(null)

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
const exportMenuOpen = ref(false)
const exportLoading = ref(false)
const exportSuccess = ref<string | null>(null)
const exportError = ref<string | null>(null)

// ── Computed ───────────────────────────────────────────────────────────────────

const playerId = computed(() => route.params.id as string)
const isOwnProfile = computed(() => auth.player?.id === playerId.value)

// ── Queries ────────────────────────────────────────────────────────────────────

const PLAYER_PROFILE_QUERY = `
  query GetPlayerProfile($playerId: UUID!) {
    playerProfile(playerId: $playerId) {
      playerId
      displayName
      bio
      createdAtUtc
      joinGameYear
      hasProSubscription
      totalWealthUsd
      totalCompanyEquityUsd
      companyCount
      leaderboardRank
      activeBuildingTypes
      citiesWithBuildings
      totalProductsSold
      hallOfFame {
        highestSingleTickRevenue
        highestSingleTickRevenueTick
        largestBuildingAcquisitionPrice
        largestBuildingAcquisitionName
        highestBrandQuality
        highestBrandQualityName
        accountAgeTicks
      }
    }
  }
`

const UPDATE_BIO_MUTATION = `
  mutation UpdatePlayerBio($bio: String) {
    updatePlayerBio(input: { bio: $bio }) {
      playerId
      bio
    }
  }
`

const PLAYER_BADGES_QUERY = `
  query GetPlayerBadges($playerId: UUID!) {
    playerBadges(playerId: $playerId) {
      id badgeType rarity unlockCondition unlockedAtUtc unlockedAtTick
    }
  }
`

const PLAYER_RANK_HISTORY_QUERY = `
  query GetPlayerRankHistory($playerId: UUID!, $limit: Int) {
    playerRankHistory(playerId: $playerId, limit: $limit) {
      snapshotTick snapshotUtc leaderboardRank wealthUsd percentileRank positionChange
    }
  }
`

const GENERATE_STATS_EXPORT_MUTATION = `
  mutation GenerateStatsExport($i: GenerateStatsExportInput!) {
    generateStatsExport(input: $i) {
      format fileName contentBase64
    }
  }
`

async function fetchProfile() {
  loading.value = true
  error.value = null
  try {
    const data = await gqlRequest<{ playerProfile: PlayerProfile | null }>(
      PLAYER_PROFILE_QUERY,
      { playerId: playerId.value },
    )
    profile.value = data.playerProfile
    if (profile.value) {
      bioInput.value = profile.value.bio ?? ''
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('playerProfile.loadFailed')
  } finally {
    loading.value = false
  }
}

async function fetchBadges() {
  if (badgesLoaded.value) return
  badgesLoading.value = true
  try {
    const data = await gqlRequest<{ playerBadges: PlayerBadge[] }>(
      PLAYER_BADGES_QUERY,
      { playerId: playerId.value },
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
    const data = await gqlRequest<{ playerRankHistory: RankSnapshot[] }>(
      PLAYER_RANK_HISTORY_QUERY,
      { playerId: playerId.value, limit: 365 },
    )
    rankSnapshots.value = data.playerRankHistory ?? []
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

async function exportStats(format: 'CSV' | 'HTML') {
  if (!auth.isAuthenticated) return
  exportMenuOpen.value = false
  exportLoading.value = true
  exportSuccess.value = null
  exportError.value = null
  try {
    const input: Record<string, unknown> = { format }
    // If exporting someone else's profile (admin case), pass their id
    if (!isOwnProfile.value) {
      input.playerId = playerId.value
    }
    const data = await gqlRequest<{
      generateStatsExport: { format: string; fileName: string; contentBase64: string }
    }>(GENERATE_STATS_EXPORT_MUTATION, { i: input })

    const { fileName, contentBase64 } = data.generateStatsExport
    const mimeType = format === 'HTML' ? 'text/html' : 'text/csv'
    const blob = new Blob(
      [Uint8Array.from(atob(contentBase64), (c) => c.charCodeAt(0))],
      { type: mimeType },
    )
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName
    anchor.click()
    URL.revokeObjectURL(url)
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

async function saveBio() {
  if (!isOwnProfile.value) return
  bioSaving.value = true
  bioError.value = null
  try {
    const data = await gqlRequest<{
      updatePlayerBio: { playerId: string; bio: string | null }
    }>(UPDATE_BIO_MUTATION, { bio: bioInput.value.trim() || null })
    if (profile.value) {
      profile.value.bio = data.updatePlayerBio.bio
    }
    editingBio.value = false
  } catch (e) {
    bioError.value = e instanceof Error ? e.message : t('playerProfile.bioSaveError')
  } finally {
    bioSaving.value = false
  }
}

function cancelBioEdit() {
  editingBio.value = false
  bioInput.value = profile.value?.bio ?? ''
  bioError.value = null
}

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

function formatJoinDate(createdAtUtc: string): string {
  return new Date(createdAtUtc).toLocaleDateString(
    locale.value === 'sk' ? 'sk-SK' : locale.value === 'de' ? 'de-DE' : 'en-US',
    { year: 'numeric', month: 'long', day: 'numeric' },
  )
}

function formatBrandQualityPercent(value: number): string {
  return `${Math.round(value * 100)}%`
}

function copyProfileUrl() {
  const url = window.location.href
  navigator.clipboard.writeText(url).catch(() => {
    // Fallback: select URL in address bar
  })
}

// ── Lifecycle ──────────────────────────────────────────────────────────────────

onMounted(async () => {
  auth.initFromStorage()
  if (auth.isAuthenticated) {
    await auth.fetchMe()
  }
  if (!playerId.value) {
    await router.push('/leaderboard')
    return
  }
  await fetchProfile()
})
</script>

<template>
  <div class="min-h-screen">
    <!-- Hero header -->
    <div
      class="border-b border-divider py-10 text-center"
      style="background: linear-gradient(160deg, #0d1117 0%, rgba(0, 71, 255, 0.12) 100%)"
    >
      <div class="container mx-auto px-4">
        <p
          class="text-[0.75rem] font-bold tracking-[0.1em] uppercase text-brand mb-2"
        >
          {{ t('playerProfile.eyebrow') }}
        </p>
        <template v-if="profile">
          <h1 class="text-3xl sm:text-4xl font-extrabold mb-1">
            {{ profile.displayName }}
            <span
              v-if="profile.hasProSubscription"
              class="pro-badge ml-2 align-middle text-sm font-bold bg-[color:var(--color-secondary)] text-black px-2 py-0.5 rounded-full"
              title="Pro subscriber"
            >⭐ Pro</span>
          </h1>
          <p class="text-muted text-sm mb-3">
            {{ t('playerProfile.joinedOn', { date: formatJoinDate(profile.createdAtUtc), year: profile.joinGameYear }) }}
          </p>

          <!-- Bio section -->
          <div class="max-w-[560px] mx-auto mt-3">
            <div v-if="!editingBio" class="flex items-center justify-center gap-2">
              <p
                v-if="profile.bio"
                class="player-bio text-sm text-muted italic"
              >
                "{{ profile.bio }}"
              </p>
              <p v-else-if="isOwnProfile" class="text-sm text-muted">
                {{ t('playerProfile.noBio') }}
              </p>
              <button
                v-if="isOwnProfile"
                class="edit-bio-btn text-xs text-brand hover:underline ml-1"
                @click="() => { editingBio = true; bioInput = profile?.bio ?? '' }"
              >
                {{ t('playerProfile.editBio') }}
              </button>
            </div>
            <div v-else class="flex flex-col gap-2">
              <textarea
                v-model="bioInput"
                maxlength="160"
                rows="2"
                class="w-full bg-surface border border-divider rounded-lg px-3 py-2 text-sm text-body focus:outline-none focus:border-brand resize-none"
                :placeholder="t('playerProfile.bioPlaceholder')"
              />
              <div class="flex items-center gap-2 justify-center">
                <span class="text-xs text-muted">{{ bioInput.length }}/160</span>
                <button
                  class="btn btn-primary btn-sm"
                  :disabled="bioSaving"
                  @click="saveBio"
                >
                  {{ bioSaving ? t('common.saving') : t('common.save') }}
                </button>
                <button class="btn btn-secondary btn-sm" @click="cancelBioEdit">
                  {{ t('common.cancel') }}
                </button>
              </div>
              <p v-if="bioError" class="text-bad text-xs text-center">{{ bioError }}</p>
            </div>
          </div>

          <!-- Share button -->
          <div class="mt-4 flex justify-center">
            <button
              class="share-profile-btn inline-flex items-center gap-1.5 text-xs text-muted border border-divider rounded-full px-3 py-1 hover:border-brand hover:text-body transition-colors"
              :title="t('playerProfile.shareTooltip')"
              @click="copyProfileUrl"
            >
              🔗 {{ t('playerProfile.share') }}
            </button>
          </div>
        </template>
        <template v-else-if="!loading">
          <h1 class="text-3xl font-extrabold">{{ t('playerProfile.notFound') }}</h1>
        </template>
        <template v-else>
          <div class="text-3xl font-extrabold opacity-30">…</div>
        </template>
      </div>
    </div>

    <!-- Content -->
    <div class="container mx-auto px-4 pt-8 pb-16">
      <!-- Loading -->
      <div v-if="loading" class="flex flex-col items-center gap-3 py-12 text-center">
        <span class="text-4xl">⏳</span>
        <p>{{ t('common.loading') }}</p>
      </div>

      <!-- Error -->
      <div
        v-else-if="error"
        class="flex flex-col items-center gap-3 py-12 text-center text-bad"
      >
        <span class="text-4xl">⚠️</span>
        <p>{{ error }}</p>
        <button class="btn btn-secondary" @click="fetchProfile">
          {{ t('common.tryAgain') }}
        </button>
      </div>

      <!-- Not found -->
      <div
        v-else-if="!profile"
        class="flex flex-col items-center gap-3 py-12 text-center"
      >
        <span class="text-4xl">🔍</span>
        <p class="text-xl font-bold">{{ t('playerProfile.notFound') }}</p>
        <RouterLink to="/leaderboard" class="btn btn-primary">
          {{ t('playerProfile.backToLeaderboard') }}
        </RouterLink>
      </div>

      <!-- Profile content -->
      <template v-else>
        <!-- Quick stats row -->
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 max-w-[900px] mx-auto mb-8">
          <!-- Rank -->
          <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
            <div class="text-2xl font-extrabold text-brand mb-0.5">
              {{ profile.leaderboardRank > 0 ? rankBadge(profile.leaderboardRank) : '—' }}
            </div>
            <div class="text-xs text-muted">{{ t('playerProfile.globalRank') }}</div>
          </div>
          <!-- Total wealth -->
          <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
            <div
              class="text-lg font-extrabold text-[color:var(--color-secondary)] mb-0.5 tabular-nums"
            >
              {{ formatMoney(profile.totalWealthUsd) }}
            </div>
            <div class="text-xs text-muted">{{ t('playerProfile.totalWealth') }}</div>
          </div>
          <!-- Companies -->
          <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
            <div class="text-2xl font-extrabold mb-0.5">{{ profile.companyCount }}</div>
            <div class="text-xs text-muted">{{ t('playerProfile.companies') }}</div>
          </div>
          <!-- Cities -->
          <div class="stat-card bg-card border border-divider rounded-xl p-4 text-center">
            <div class="text-2xl font-extrabold mb-0.5">
              {{ profile.citiesWithBuildings }}
            </div>
            <div class="text-xs text-muted">{{ t('playerProfile.cities') }}</div>
          </div>
        </div>

        <!-- ── Tab navigation ── -->
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

        <!-- ── Overview tab ── -->
        <div v-show="activeTab === 'overview'">
          <!-- Two-column layout -->
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
                <!-- Highest single-tick revenue -->
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
                  <span
                    v-if="profile.hallOfFame.highestSingleTickRevenueTick > 0"
                    class="text-xs text-muted"
                  >
                    {{
                      t('playerProfile.hof.atTick', {
                        tick: profile.hallOfFame.highestSingleTickRevenueTick,
                      })
                    }}
                  </span>
                </div>

                <div class="border-t border-divider" />

                <!-- Largest building acquisition -->
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
                  <span
                    v-if="profile.hallOfFame.largestBuildingAcquisitionName"
                    class="text-xs text-muted"
                  >
                    {{ profile.hallOfFame.largestBuildingAcquisitionName }}
                  </span>
                </div>

                <div class="border-t border-divider" />

                <!-- Highest brand quality -->
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
                  <span
                    v-if="profile.hallOfFame.highestBrandQualityName"
                    class="text-xs text-muted"
                  >
                    {{ profile.hallOfFame.highestBrandQualityName }}
                  </span>
                </div>

                <div class="border-t border-divider" />

                <!-- Account age -->
                <div class="hof-record flex flex-col gap-0.5">
                  <span class="text-xs text-muted uppercase tracking-wide">
                    {{ t('playerProfile.hof.accountAge') }}
                  </span>
                  <span class="text-lg font-extrabold tabular-nums">
                    {{
                      t('playerProfile.hof.ticks', {
                        n: profile.hallOfFame.accountAgeTicks.toLocaleString('en-US'),
                      })
                    }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ── Achievements tab ── -->
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

        <!-- ── Rank History tab ── -->
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

        <!-- ── Export Stats & Back link ── -->
        <div class="max-w-[900px] mx-auto mt-8 flex items-center justify-between flex-wrap gap-3">
          <RouterLink
            to="/leaderboard"
            class="text-sm text-muted hover:text-body hover:underline inline-flex items-center gap-1"
          >
            ← {{ t('playerProfile.backToLeaderboard') }}
          </RouterLink>

          <!-- Export button (authenticated only) -->
          <div v-if="auth.isAuthenticated && (isOwnProfile || auth.player?.role === 'ADMIN')" class="export-container relative">
            <button
              class="export-btn export-stats-btn btn btn-secondary inline-flex items-center gap-1.5 text-sm"
              :disabled="exportLoading"
              @click="exportMenuOpen = !exportMenuOpen"
            >
              <span>📥</span>
              {{ exportLoading ? t('playerProfile.exporting') : t('playerProfile.exportStats') }}
              <span v-if="!exportLoading">▾</span>
            </button>
            <!-- Dropdown menu -->
            <div v-if="exportMenuOpen" class="export-dropdown">
              <button class="export-option" @click="exportStats('CSV')">
                📊 {{ t('playerProfile.downloadCsv') }}
              </button>
              <button class="export-option" @click="exportStats('HTML')">
                📄 {{ t('playerProfile.downloadHtml') }}
              </button>
            </div>
          </div>
        </div>

        <!-- Export feedback -->
        <div class="max-w-[900px] mx-auto mt-2">
          <p v-if="exportSuccess" class="text-good text-sm text-right">✓ {{ exportSuccess }}</p>
          <p v-if="exportError" class="text-bad text-sm text-right">{{ exportError }}</p>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.btn-sm {
  padding: 0.25rem 0.75rem;
  font-size: 0.8125rem;
}

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

/* Export dropdown */
.export-container {
  position: relative;
}

.export-dropdown {
  position: absolute;
  right: 0;
  top: calc(100% + 6px);
  background: var(--color-surface-elevated, #1e293b);
  border: 1px solid var(--color-border, #334155);
  border-radius: 8px;
  padding: 4px;
  min-width: 180px;
  z-index: 50;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}

.export-option {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 8px 12px;
  background: transparent;
  border: none;
  border-radius: 6px;
  color: var(--color-text-primary, #f1f5f9);
  font-size: 13px;
  cursor: pointer;
  text-align: left;
  transition: background 0.1s ease;
}

.export-option:hover {
  background: rgba(59, 130, 246, 0.12);
}
</style>
