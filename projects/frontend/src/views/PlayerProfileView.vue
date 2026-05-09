<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { gqlRequest } from '@/lib/graphql'
import { gqlRequest as gqlMasterRequest } from '@/lib/graphqlMasterServer'
import PlayerProfileTabsContent from '@/components/profile/PlayerProfileTabsContent.vue'
import type { PlayerProfile } from '@/components/profile/PlayerProfileTabsContent.vue'

const { t, locale } = useI18n()
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

// ── State ──────────────────────────────────────────────────────────────────────

const profile = ref<PlayerProfile | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

// Bio editing state (only for own profile)
const editingBio = ref(false)
const bioInput = ref('')
const bioSaving = ref(false)
const bioError = ref<string | null>(null)

// Display name editing state (only for own profile)
const editingDisplayName = ref(false)
const displayNameSuccessTimer = ref<ReturnType<typeof setTimeout> | null>(null)
const displayNameInput = ref('')
const displayNameSaving = ref(false)
const displayNameError = ref<string | null>(null)
const displayNameSuccess = ref(false)

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

const UPDATE_DISPLAY_NAME_MUTATION = `
  mutation UpdateDisplayName($displayName: String!) {
    updateDisplayName(input: { displayName: $displayName }) {
      playerId
      displayName
    }
  }
`

const UPDATE_PERSONAL_ACCOUNT_NAME_MASTER_MUTATION = `
  mutation UpdatePersonalAccountName($input: UpdatePersonalAccountNameInput!) {
    updatePersonalAccountName(input: $input) {
      playerId
      personalAccountName
    }
  }
`

// ── Functions ──────────────────────────────────────────────────────────────────

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
      displayNameInput.value = profile.value.displayName
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('playerProfile.loadFailed')
  } finally {
    loading.value = false
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

async function saveDisplayName() {
  if (!isOwnProfile.value) return
  const trimmed = displayNameInput.value.trim()
  if (!trimmed) return
  displayNameSaving.value = true
  displayNameError.value = null
  displayNameSuccess.value = false
  try {
    await gqlMasterRequest<{
      updatePersonalAccountName: { playerId: string; personalAccountName: string }
    }>(UPDATE_PERSONAL_ACCOUNT_NAME_MASTER_MUTATION, {
      input: { personalAccountName: trimmed },
    })

    const data = await gqlRequest<{
      updateDisplayName: { playerId: string; displayName: string }
    }>(UPDATE_DISPLAY_NAME_MUTATION, { displayName: trimmed })
    if (profile.value) {
      profile.value.displayName = data.updateDisplayName.displayName
    }
    if (auth.player) {
      auth.player.displayName = data.updateDisplayName.displayName
      auth.player.personalAccountName = data.updateDisplayName.displayName
    }
    editingDisplayName.value = false
    displayNameSuccess.value = true
    if (displayNameSuccessTimer.value) clearTimeout(displayNameSuccessTimer.value)
    displayNameSuccessTimer.value = setTimeout(() => { displayNameSuccess.value = false }, 3000)
  } catch (e) {
    displayNameError.value = e instanceof Error ? e.message : t('playerProfile.displayNameSaveError')
  } finally {
    displayNameSaving.value = false
  }
}

function cancelDisplayNameEdit() {
  editingDisplayName.value = false
  displayNameInput.value = profile.value?.displayName ?? ''
  displayNameError.value = null
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

function copyProfileUrl() {
  const url = window.location.href
  navigator.clipboard.writeText(url).catch(() => {
    // Fallback: no-op
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

onUnmounted(() => {
  if (displayNameSuccessTimer.value) clearTimeout(displayNameSuccessTimer.value)
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
        <p class="text-[0.75rem] font-bold tracking-[0.1em] uppercase text-brand mb-2">
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

          <!-- Display name editing (own profile only) -->
          <div v-if="isOwnProfile" class="max-w-[480px] mx-auto mb-3">
            <div v-if="!editingDisplayName" class="flex items-center justify-center gap-2">
              <button
                class="edit-display-name-btn text-xs text-brand hover:underline"
                @click="() => { editingDisplayName = true; displayNameInput = profile?.displayName ?? '' }"
              >
                {{ t('playerProfile.editDisplayName') }}
              </button>
              <span v-if="displayNameSuccess" class="text-xs text-good">✓ {{ t('playerProfile.displayNameSaved') }}</span>
            </div>
            <div v-else class="flex flex-col gap-2">
              <label class="text-xs text-muted font-medium">{{ t('playerProfile.displayNameLabel') }}</label>
              <div class="flex gap-2">
                <input
                  v-model="displayNameInput"
                  type="text"
                  maxlength="40"
                  class="display-name-input flex-1 bg-surface border border-divider rounded-lg px-3 py-2 text-sm text-body focus:outline-none focus:border-brand"
                  :placeholder="t('auth.displayNamePlaceholder')"
                />
              </div>
              <p class="display-name-real-name-warning text-xs text-amber-400">
                {{ t('playerProfile.displayNameRealNameWarning') }}
              </p>
              <p class="display-name-shared-note text-xs text-muted">
                {{ t('playerProfile.displayNameSharedAcrossServers') }}
              </p>
              <div class="flex items-center gap-2 justify-center">
                <span class="text-xs text-muted">{{ displayNameInput.length }}/40</span>
                <button class="btn btn-primary btn-sm" :disabled="displayNameSaving" @click="saveDisplayName">
                  {{ displayNameSaving ? t('common.saving') : t('common.save') }}
                </button>
                <button class="btn btn-secondary btn-sm" @click="cancelDisplayNameEdit">
                  {{ t('common.cancel') }}
                </button>
              </div>
              <p v-if="displayNameError" class="text-bad text-xs text-center">{{ displayNameError }}</p>
            </div>
          </div>

          <!-- Rank badge in hero -->
          <div v-if="profile.leaderboardRank > 0" class="text-2xl font-extrabold text-brand mb-2">
            {{ rankBadge(profile.leaderboardRank) }}
          </div>

          <!-- Bio section -->
          <div class="max-w-[560px] mx-auto mt-3">
            <div v-if="!editingBio" class="flex items-center justify-center gap-2">
              <p v-if="profile.bio" class="player-bio text-sm text-muted italic">
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
                <button class="btn btn-primary btn-sm" :disabled="bioSaving" @click="saveBio">
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
      <div v-else-if="error" class="flex flex-col items-center gap-3 py-12 text-center text-bad">
        <span class="text-4xl">⚠️</span>
        <p>{{ error }}</p>
        <button class="btn btn-secondary" @click="fetchProfile">
          {{ t('common.tryAgain') }}
        </button>
      </div>

      <!-- Not found -->
      <div v-else-if="!profile" class="flex flex-col items-center gap-3 py-12 text-center">
        <span class="text-4xl">🔍</span>
        <p class="text-xl font-bold">{{ t('playerProfile.notFound') }}</p>
        <RouterLink to="/leaderboard" class="btn btn-primary">
          {{ t('playerProfile.backToLeaderboard') }}
        </RouterLink>
      </div>

      <!-- Profile tabs content -->
      <PlayerProfileTabsContent
        v-else
        :profile="profile"
        :player-id="playerId"
        :is-own-profile="isOwnProfile"
      />
    </div>
  </div>
</template>

<style scoped>
.btn-sm {
  padding: 0.25rem 0.75rem;
  font-size: 0.8125rem;
}
</style>
