<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { fetchRankingLeaderboard, fetchGameServers, type RankingLeaderboardEntryInfo, type GameServerSummary } from '@/lib/masterApi'
import heroVideo from '@/assets/hero-video.webm'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const auth = useAuthStore()

const ranking = ref<RankingLeaderboardEntryInfo[]>([])
const rankingLoading = ref(true)
const rankingError = ref('')

const servers = ref<GameServerSummary[]>([])

const rankingTop = computed(() => ranking.value.slice(0, 10))
const teaserServers = computed(() =>
  servers.value.filter((s) => s.isOnline).slice(0, 3),
)
const navItems = computed(() => {
  const items = [
    { label: t('nav.gameServers'), to: '/game-servers' },
    { label: t('nav.ranking'), to: '/ranking' },
    { label: t('nav.supportDashboard'), to: '/support' },
  ]

  if (auth.isGameAdmin) {
    items.push({ label: t('nav.gameAdminDashboard'), to: '/game-admin' })
  }

  return items
})

function formatPoints(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value)
}

function getPlayerAlias(entry: RankingLeaderboardEntryInfo): string {
  return entry.personalAccountName || entry.displayName
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

onMounted(() => {
  void loadRanking()
  void fetchGameServers()
    .then((data) => {
      servers.value = data
    })
    .catch((err) => {
      // Teaser section silently skips if servers can't load — non-critical widget
      console.debug('[HomeView] Active Servers teaser failed to load:', err)
    })
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
        <div
          class="grid w-full items-end gap-6 lg:grid-cols-[minmax(0,1.45fr)_minmax(320px,0.75fr)]"
        >
          <div
            class="max-w-3xl rounded-2xl border border-divider/70 bg-card/85 p-6 shadow-[var(--shadow-lg)] lg:p-10"
          >
            <p class="text-xs font-semibold uppercase tracking-[0.16em] text-muted">
              {{ t('home.eyebrow') }}
            </p>
            <h1 class="hero-title mt-3 text-4xl font-bold leading-tight md:text-5xl">
              {{ t('home.title') }}
            </h1>
            <p class="mt-4 max-w-2xl text-sm text-muted md:text-base">
              {{ t('home.heroText') }}
            </p>

            <div class="mt-6 flex flex-wrap items-center gap-3">
              <RouterLink class="btn btn-primary" to="/ranking">
                {{ t('rankingDashboard.openFull') }}
              </RouterLink>
              <RouterLink class="btn btn-secondary" to="/ranking/bounties">
                {{ t('home.bounties') }}
              </RouterLink>
            </div>
          </div>
        </div>
      </div>
    </section>

    <ViewSubnav :items="navItems" aria-label="Home section navigation" />

    <main class="container pt-2 lg:pt-2">
      <section id="ranking" class="card p-6" aria-labelledby="ranking-heading">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p class="text-xs font-semibold uppercase tracking-[0.14em] text-muted">
              {{ t('home.ranking') }}
            </p>
            <h2 id="ranking-heading" class="mt-2 text-2xl font-semibold">
              {{ t('rankingDashboard.leaderboard') }}
            </h2>
          </div>
          <RouterLink class="btn btn-primary" to="/ranking">
            {{ t('rankingDashboard.openFull') }}
          </RouterLink>
        </div>

        <p v-if="rankingLoading" class="state-message mt-4">{{ t('common.loading') }}</p>
        <p v-else-if="rankingError" class="state-error mt-4" role="alert">{{ rankingError }}</p>
        <p v-else-if="rankingTop.length === 0" class="state-message mt-4">
          {{ t('common.noData') }}
        </p>

        <div v-else class="mt-5 overflow-x-auto rounded-xl border border-divider">
          <table
            class="min-w-full border-collapse text-sm"
            aria-label="Master ranking leaderboard top 10"
          >
            <thead>
              <tr class="bg-overlay/40 text-left text-xs uppercase tracking-[0.08em] text-muted">
                <th class="px-5 py-3">{{ t('rankingDashboard.rank') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.player') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.points') }}</th>
                <th class="px-5 py-3">{{ t('rankingDashboard.movement') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="entry in rankingTop"
                :key="entry.playerId"
                class="border-t border-divider/70"
              >
                <td class="px-5 py-3 font-semibold">#{{ entry.globalRank }}</td>
                <td class="px-5 py-3">{{ getPlayerAlias(entry) }}</td>
                <td class="px-5 py-3">{{ formatPoints(entry.totalPoints) }}</td>
                <td
                  class="px-5 py-3"
                  :class="
                    entry.rankMovement > 0
                      ? 'text-good'
                      : entry.rankMovement < 0
                        ? 'text-bad'
                        : 'text-muted'
                  "
                >
                  {{ entry.rankMovement > 0 ? `+${entry.rankMovement}` : entry.rankMovement }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Active Servers teaser -->
      <section class="mt-10 lg:mt-12" aria-labelledby="servers-teaser-heading">
        <div class="mb-5 flex flex-wrap items-center justify-between gap-3">
          <div>
            <p class="text-xs font-semibold uppercase tracking-[0.14em] text-muted">
              {{ t('home.liveRegistry') }}
            </p>
            <h2 id="servers-teaser-heading" class="mt-2 text-2xl font-semibold">
              {{ t('home.activeServers') }}
            </h2>
          </div>
          <RouterLink class="btn btn-secondary" to="/game-servers">
            {{ t('home.viewAllServers') }}
          </RouterLink>
        </div>

        <p v-if="teaserServers.length === 0" class="state-message servers-teaser-empty">
          {{ t('home.noActiveServers') }}
        </p>

        <ul v-else class="servers-teaser-list grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <li
            v-for="server in teaserServers"
            :key="server.id"
            class="server-teaser-card rounded-xl border border-divider bg-card p-4"
          >
            <div class="flex items-start justify-between gap-2">
              <p class="font-semibold">{{ server.displayName }}</p>
              <span class="server-online-dot" aria-label="Online" />
            </div>
            <dl class="mt-3 grid grid-cols-3 gap-2 text-sm">
              <div>
                <dt class="text-xs text-muted">👥 {{ t('home.players') }}</dt>
                <dd class="font-semibold">{{ server.playerCount }}</dd>
              </div>
              <div>
                <dt class="text-xs text-muted">⏱ {{ t('home.tick') }}</dt>
                <dd class="font-semibold">{{ server.currentTick }}</dd>
              </div>
              <div>
                <dt class="text-xs text-muted">🏢 {{ t('home.companies') }}</dt>
                <dd class="font-semibold">{{ server.companyCount }}</dd>
              </div>
            </dl>
            <a
              class="btn btn-primary mt-4 w-full text-center text-xs"
              :href="server.frontendUrl"
              target="_blank"
              rel="noreferrer"
            >
              {{ t('home.playOnServer') }}
            </a>
          </li>
        </ul>
      </section>

      <!-- Feature highlights -->
      <section class="mt-10 lg:mt-12" aria-labelledby="features-heading">
        <div class="mb-6 text-center">
          <h2 id="features-heading" class="text-2xl font-bold text-body lg:text-3xl">
            {{ t('home.featuresTitle') }}
          </h2>
          <p class="mt-2 text-sm text-muted">{{ t('home.featuresSubtitle') }}</p>
        </div>

        <div class="feature-grid">
          <article class="feature-card" aria-label="Economic Simulation feature">
            <div class="feature-icon" aria-hidden="true">📈</div>
            <h3 class="feature-title">{{ t('home.feat1Title') }}</h3>
            <p class="feature-desc">{{ t('home.feat1Desc') }}</p>
          </article>
          <article class="feature-card" aria-label="Stock Exchange feature">
            <div class="feature-icon" aria-hidden="true">🏛️</div>
            <h3 class="feature-title">{{ t('home.feat2Title') }}</h3>
            <p class="feature-desc">{{ t('home.feat2Desc') }}</p>
          </article>
          <article class="feature-card" aria-label="Power Grid feature">
            <div class="feature-icon" aria-hidden="true">⚡</div>
            <h3 class="feature-title">{{ t('home.feat3Title') }}</h3>
            <p class="feature-desc">{{ t('home.feat3Desc') }}</p>
          </article>
          <article class="feature-card" aria-label="Research and Development feature">
            <div class="feature-icon" aria-hidden="true">🔬</div>
            <h3 class="feature-title">{{ t('home.feat4Title') }}</h3>
            <p class="feature-desc">{{ t('home.feat4Desc') }}</p>
          </article>
        </div>

        <div class="mt-6 text-center">
          <RouterLink class="btn btn-secondary" to="/docs">
            {{ t('home.learnMoreDocs') }}
          </RouterLink>
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

.feature-grid {
  display: grid;
  gap: 1.25rem;
  grid-template-columns: repeat(2, 1fr);
}

@media (max-width: 640px) {
  .feature-grid {
    grid-template-columns: 1fr;
  }
}

.feature-card {
  border-radius: 1rem;
  border: 1px solid var(--color-divider);
  background: var(--color-card);
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.feature-icon {
  font-size: 1.75rem;
  line-height: 1;
}

.feature-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-body);
}

.feature-desc {
  font-size: 0.875rem;
  line-height: 1.65;
  color: var(--color-muted);
}

.server-online-dot {
  display: inline-block;
  flex-shrink: 0;
  width: 0.625rem;
  height: 0.625rem;
  border-radius: 50%;
  background: var(--color-good);
  animation: pulse-dot 2s ease-in-out infinite;
  margin-top: 0.35rem;
}

@keyframes pulse-dot {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.55; transform: scale(1.25); }
}
</style>
