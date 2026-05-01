<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import {
  fetchGameServers,
  fetchRankingLeaderboard,
  type GameServerSummary,
  type RankingLeaderboardEntryInfo,
} from '@/lib/masterApi'
import heroVideo from '@/assets/hero-video.webm'
import { formatHeartbeatDistance } from '@/lib/time'
import {
  formatProlongLabel,
  formatRenewalNote,
  formatStatusLabel,
  formatTierLabel,
} from '@/lib/subscription'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const { t } = useI18n()

const servers = ref<GameServerSummary[]>([])
const serversLoading = ref(true)
const serversError = ref('')

const ranking = ref<RankingLeaderboardEntryInfo[]>([])
const rankingLoading = ref(true)
const rankingError = ref('')

const prolongMonths = ref(1)
const prolongLoading = ref(false)
const prolongError = ref('')
const prolongSuccess = ref(false)
const startupPackLoading = ref(false)
const startupPackError = ref('')
const startupPackSuccess = ref(false)

const onlineCount = computed(() => servers.value.filter((server) => server.isOnline).length)
const rankingTop = computed(() => ranking.value.slice(0, 10))

const startupPackClaimedAtLabel = computed(() => {
  const claimedAt = auth.player?.startupPackClaimedAtUtc
  if (!claimedAt) {
    return ''
  }

  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(claimedAt))
})

function heartbeatLabel(server: GameServerSummary) {
  return formatHeartbeatDistance(server.lastHeartbeatAtUtc)
}

function formatPoints(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}

async function loadServers() {
  serversLoading.value = true
  serversError.value = ''

  try {
    servers.value = await fetchGameServers()
  } catch (error) {
    serversError.value = error instanceof Error ? error.message : t('home.unableToLoadServers')
  } finally {
    serversLoading.value = false
  }
}

async function loadRanking() {
  rankingLoading.value = true
  rankingError.value = ''

  try {
    ranking.value = await fetchRankingLeaderboard(10, 0)
  } catch (error) {
    rankingError.value = error instanceof Error ? error.message : t('rankingDashboard.loadError')
  } finally {
    rankingLoading.value = false
  }
}

async function handleProlong() {
  prolongLoading.value = true
  prolongError.value = ''
  prolongSuccess.value = false

  try {
    await auth.prolong(prolongMonths.value)
    prolongSuccess.value = true
  } catch (error: unknown) {
    prolongError.value = error instanceof Error ? error.message : t('home.prolongError')
  } finally {
    prolongLoading.value = false
  }
}

async function handleStartupPackClaim() {
  startupPackLoading.value = true
  startupPackError.value = ''
  startupPackSuccess.value = false

  try {
    await auth.claimStartupPackOffer()
    startupPackSuccess.value = true
  } catch (error: unknown) {
    startupPackError.value = error instanceof Error ? error.message : t('home.startupPack.error')
  } finally {
    startupPackLoading.value = false
  }
}

onMounted(() => {
  void loadServers()
  void loadRanking()
})
</script>

<template>
  <div class="pb-20">
    <section class="hero-video-wrapper relative isolate overflow-hidden border-b border-divider">
      <video autoplay muted playsinline class="hero-video">
        <source :src="heroVideo" type="video/webm" />
      </video>
      <div class="hero-video-overlay"></div>

      <div class="container relative z-10 flex min-h-[62vh] items-end py-14 lg:min-h-[70vh]">
        <div class="max-w-3xl rounded-2xl border border-divider/70 bg-card/85 p-6 shadow-[var(--shadow-lg)] lg:p-10">
          <p class="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{{ t('home.eyebrow') }}</p>
          <h1 class="hero-title mt-3 text-4xl font-bold leading-tight md:text-5xl">{{ t('home.title') }}</h1>
          <p class="mt-4 max-w-2xl text-sm text-muted md:text-base">{{ t('home.heroText') }}</p>

          <div class="mt-6 flex flex-wrap items-center gap-3">
            <RouterLink
              v-if="!auth.isAuthenticated"
              class="hero-cta btn btn-primary"
              to="/login"
            >
              {{ t('home.getStarted') }}
            </RouterLink>
            <RouterLink class="btn btn-secondary" :to="{ path: '/', hash: '#game-servers' }">
              {{ t('home.gameServers') }}
            </RouterLink>
            <RouterLink class="btn btn-secondary" to="/ranking">
              {{ t('home.ranking') }}
            </RouterLink>
          </div>
        </div>
      </div>
    </section>

    <main class="container grid gap-10 pt-8 lg:pt-10">
      <section class="grid gap-6 lg:grid-cols-[1.05fr_1.2fr]" aria-label="Portal overview">
        <article class="card p-6">
          <template v-if="auth.isAuthenticated">
            <p class="text-xs font-semibold uppercase tracking-[0.14em] text-muted">
              {{ t('home.yourAccount') }}
            </p>
            <h2 class="mt-2 text-2xl font-semibold">{{ t('home.subscription') }}</h2>

            <div class="mt-5 rounded-xl border border-divider bg-card-raised p-4">
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p class="text-xs uppercase tracking-[0.12em] text-muted">
                    {{ t('home.startupPack.kicker') }}
                  </p>
                  <h3 class="mt-1 text-lg font-semibold">{{ t('home.startupPack.title') }}</h3>
                </div>
                <span
                  :class="[
                    'rounded-full px-3 py-1 text-xs font-semibold',
                    auth.player?.canClaimStartupPack
                      ? 'bg-brand-subtle text-brand'
                      : 'bg-overlay text-muted',
                  ]"
                >
                  {{
                    auth.player?.canClaimStartupPack
                      ? t('home.startupPack.available')
                      : t('home.startupPack.claimed')
                  }}
                </span>
              </div>

              <p class="mt-3 text-sm text-muted">{{ t('home.startupPack.copy') }}</p>

              <ul class="mt-3 grid gap-2 pl-5 text-sm text-muted list-disc">
                <li>{{ t('home.startupPack.benefit1') }}</li>
                <li>{{ t('home.startupPack.benefit2') }}</li>
                <li>{{ t('home.startupPack.benefit3') }}</li>
              </ul>

              <div v-if="auth.player?.canClaimStartupPack" class="mt-4 flex flex-wrap items-center gap-3">
                <button class="btn btn-primary" :disabled="startupPackLoading" @click="handleStartupPackClaim">
                  {{ startupPackLoading ? t('home.startupPack.claiming') : t('home.startupPack.claimButton') }}
                </button>
                <p class="text-xs text-muted">{{ t('home.startupPack.oneClaim') }}</p>
              </div>
              <p v-else class="mt-4 text-xs text-muted">
                <template v-if="startupPackClaimedAtLabel">
                  {{ t('home.startupPack.claimedOn', { date: startupPackClaimedAtLabel }) }}
                </template>
                <template v-else>
                  {{ t('home.startupPack.alreadyClaimed') }}
                </template>
              </p>

              <p v-if="startupPackError" class="state-error mt-3" role="alert">{{ startupPackError }}</p>
              <p v-if="startupPackSuccess" class="mt-3 text-good" role="status">
                ✓ {{ t('home.startupPack.success') }}
              </p>
            </div>

            <div v-if="auth.subscription" class="mt-5 rounded-xl border border-divider bg-card-raised p-4">
              <div class="flex flex-wrap items-center gap-3">
                <span
                  :class="[
                    'tier-badge rounded-full px-3 py-1 text-xs font-semibold',
                    auth.subscription.tier === 'PRO' ? 'tier-pro bg-brand-subtle text-brand' : 'tier-free bg-overlay text-muted',
                  ]"
                >
                  {{ formatTierLabel(auth.subscription.tier, t) }}
                </span>
                <span
                  :class="[
                    'status-pill rounded-full px-3 py-1 text-xs font-semibold',
                    auth.subscription.isActive
                      ? 'status-online bg-[rgba(34,197,94,0.15)] text-good'
                      : 'status-offline bg-[rgba(248,113,113,0.15)] text-bad',
                  ]"
                >
                  {{ formatStatusLabel(auth.subscription, t) }}
                </span>
              </div>

              <p v-if="auth.subscription.isActive" class="mt-3 text-sm text-muted">
                {{ formatRenewalNote(auth.subscription, t) }}
              </p>

              <div v-if="auth.subscription.canProlong" class="mt-4 grid gap-3">
                <p class="text-sm text-muted">
                  {{ auth.subscription ? formatProlongLabel(auth.subscription, t) : '' }}
                </p>
                <div class="flex flex-wrap items-center gap-3">
                  <label class="text-sm text-muted" for="months-select">{{ t('home.months') }}</label>
                  <select id="months-select" v-model="prolongMonths" class="rounded-md border border-divider bg-card px-3 py-2 text-sm">
                    <option v-for="m in [1, 3, 6, 12]" :key="m" :value="m">
                      {{ m }} {{ m > 1 ? t('home.monthsPlural') : t('home.month') }}
                    </option>
                  </select>
                  <button class="btn btn-secondary" :disabled="prolongLoading" @click="handleProlong">
                    {{ prolongLoading ? t('home.processing') : t('home.confirm') }}
                  </button>
                </div>
                <p v-if="prolongError" class="state-error" role="alert">{{ prolongError }}</p>
                <p v-if="prolongSuccess" class="text-good" role="status">✓ {{ t('home.prolongSuccess') }}</p>
              </div>
            </div>
          </template>

          <template v-else>
            <p class="text-xs font-semibold uppercase tracking-[0.14em] text-muted">{{ t('home.howItWorks') }}</p>
            <h2 class="mt-2 text-2xl font-semibold">{{ t('home.playToEarn') }}</h2>
            <ul class="mt-4 grid gap-2 pl-5 text-sm text-muted list-disc">
              <li>{{ t('home.pitch1') }}</li>
              <li>{{ t('home.pitch2') }}</li>
              <li>{{ t('home.pitch3') }}</li>
            </ul>
            <p class="mt-4 text-sm text-muted">{{ t('home.ctaText') }}</p>
            <RouterLink class="btn btn-primary mt-4" to="/login">{{ t('home.registerFree') }}</RouterLink>
          </template>
        </article>

        <section id="game-servers" class="card p-6" aria-labelledby="server-list-heading">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p class="text-xs font-semibold uppercase tracking-[0.14em] text-muted">{{ t('home.liveRegistry') }}</p>
              <h2 id="server-list-heading" class="mt-2 text-2xl font-semibold">{{ t('home.gameServers') }}</h2>
            </div>
            <div class="flex items-center gap-3">
              <span class="rounded-full border border-divider px-3 py-1 text-xs text-muted">
                {{ t('home.activeServers') }}: {{ onlineCount }}
              </span>
              <button class="btn btn-secondary" type="button" @click="loadServers">
                {{ t('common.refresh') }}
              </button>
            </div>
          </div>

          <p v-if="serversLoading" class="state-message mt-4">{{ t('home.loadingServers') }}</p>
          <p v-else-if="serversError" class="state-error mt-4" role="alert">{{ serversError }}</p>
          <p v-else-if="servers.length === 0" class="state-message mt-4">{{ t('home.noServers') }}</p>

          <ul v-else class="mt-5 grid gap-4">
            <li
              v-for="server in servers"
              :key="server.id"
              class="rounded-xl border border-divider bg-card-raised p-4"
            >
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p class="text-lg font-semibold">{{ server.displayName }}</p>
                  <p class="text-sm text-muted">{{ server.region }} · {{ server.environment }} · v{{ server.version }}</p>
                </div>
                <span
                  :class="[
                    'status-pill rounded-full px-3 py-1 text-xs font-semibold',
                    server.isOnline
                      ? 'status-online bg-[rgba(34,197,94,0.15)] text-good'
                      : 'status-offline bg-[rgba(248,113,113,0.15)] text-bad',
                  ]"
                >
                  {{ server.isOnline ? t('home.online') : t('home.offline') }}
                </span>
              </div>

              <p class="mt-3 text-sm text-muted">{{ server.description || t('home.defaultDescription') }}</p>

              <dl class="mt-3 grid grid-cols-2 gap-3 text-sm md:grid-cols-4">
                <div>
                  <dt class="text-muted">{{ t('home.players') }}</dt>
                  <dd class="font-semibold">{{ server.playerCount }}</dd>
                </div>
                <div>
                  <dt class="text-muted">{{ t('home.companies') }}</dt>
                  <dd class="font-semibold">{{ server.companyCount }}</dd>
                </div>
                <div>
                  <dt class="text-muted">{{ t('home.tick') }}</dt>
                  <dd class="font-semibold">{{ server.currentTick }}</dd>
                </div>
                <div>
                  <dt class="text-muted">{{ t('home.heartbeat') }}</dt>
                  <dd class="font-semibold">{{ heartbeatLabel(server) }}</dd>
                </div>
              </dl>

              <div class="mt-4 flex flex-wrap items-center gap-3">
                <a class="btn btn-primary" :href="server.frontendUrl" rel="noreferrer" target="_blank">
                  {{ t('home.playOnServer') }}
                </a>
                <a class="text-sm text-brand hover:text-brand-hover" :href="server.graphqlUrl" rel="noreferrer" target="_blank">
                  GraphQL
                </a>
              </div>
            </li>
          </ul>
        </section>
      </section>

      <section id="ranking" class="card p-6" aria-labelledby="ranking-heading">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p class="text-xs font-semibold uppercase tracking-[0.14em] text-muted">{{ t('home.ranking') }}</p>
            <h2 id="ranking-heading" class="mt-2 text-2xl font-semibold">
              {{ t('rankingDashboard.leaderboard') }}
            </h2>
          </div>
          <div class="flex items-center gap-3">
            <button class="btn btn-secondary" type="button" @click="loadRanking">
              {{ t('common.refresh') }}
            </button>
            <RouterLink class="btn btn-primary" to="/ranking/bounties">{{ t('home.bounties') }}</RouterLink>
          </div>
        </div>

        <p v-if="rankingLoading" class="state-message mt-4">{{ t('common.loading') }}</p>
        <p v-else-if="rankingError" class="state-error mt-4" role="alert">{{ rankingError }}</p>
        <p v-else-if="rankingTop.length === 0" class="state-message mt-4">{{ t('common.loading') }}</p>

        <div v-else class="mt-5 overflow-x-auto rounded-xl border border-divider">
          <table class="min-w-full border-collapse text-sm" aria-label="Master ranking leaderboard preview">
            <thead>
              <tr class="bg-overlay/40 text-left text-xs uppercase tracking-[0.08em] text-muted">
                <th class="px-5 py-3">{{ t('rankingDashboard.rank') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.player') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.points') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.movement') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="entry in rankingTop" :key="entry.playerId" class="border-t border-divider/70">
                <td class="px-5 py-3 font-semibold">#{{ entry.globalRank }}</td>
                <td class="px-5 py-3">{{ entry.displayName }}</td>
                <td class="px-5 py-3">{{ formatPoints(entry.totalPoints) }}</td>
                <td class="px-5 py-3" :class="entry.rankMovement > 0 ? 'text-good' : entry.rankMovement < 0 ? 'text-bad' : 'text-muted'">
                  {{ entry.rankMovement > 0 ? `+${entry.rankMovement}` : entry.rankMovement }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  </div>
</template>

<style scoped>
.hero-video-wrapper {
  min-height: 62vh;
}

.hero-video {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.hero-video-overlay {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(140deg, rgba(13, 17, 23, 0.82), rgba(13, 17, 23, 0.58)),
    radial-gradient(circle at 75% 20%, rgba(0, 71, 255, 0.25), transparent 45%);
}

[data-theme='light'] .hero-video-overlay {
  background:
    linear-gradient(140deg, rgba(240, 244, 248, 0.88), rgba(240, 244, 248, 0.62)),
    radial-gradient(circle at 75% 20%, rgba(0, 71, 255, 0.12), transparent 45%);
}
</style>
