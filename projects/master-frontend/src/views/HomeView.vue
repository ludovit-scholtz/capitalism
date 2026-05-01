<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { fetchRankingLeaderboard, type RankingLeaderboardEntryInfo } from '@/lib/masterApi'
import heroVideo from '@/assets/hero-video.webm'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const auth = useAuthStore()

const ranking = ref<RankingLeaderboardEntryInfo[]>([])
const rankingLoading = ref(true)
const rankingError = ref('')

const rankingTop = computed(() => ranking.value.slice(0, 10))
const rankingPreview = computed(() => ranking.value.slice(0, 3))
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

          <section
            class="rounded-2xl border border-divider/70 bg-card/85 p-5 shadow-[var(--shadow-lg)] lg:p-6"
            aria-labelledby="landing-ranking-heading"
          >
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.14em] text-muted">
                  {{ t('home.ranking') }}
                </p>
                <h2 id="landing-ranking-heading" class="mt-2 text-xl font-semibold text-body">
                  {{ t('rankingDashboard.topCompetitors') }}
                </h2>
              </div>
              <button class="btn btn-secondary" type="button" @click="loadRanking">
                {{ t('common.refresh') }}
              </button>
            </div>

            <p v-if="rankingLoading" class="state-message mt-4">{{ t('common.loading') }}</p>
            <p v-else-if="rankingError" class="state-error mt-4" role="alert">{{ rankingError }}</p>

            <div v-else class="mt-4 grid gap-3">
              <article
                v-for="entry in rankingPreview"
                :key="entry.playerId"
                class="rounded-xl border border-divider bg-card-raised p-4"
              >
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <p class="text-xs uppercase tracking-[0.08em] text-muted">
                      #{{ entry.globalRank }}
                    </p>
                    <h3 class="mt-1 text-base font-semibold text-body">{{ entry.displayName }}</h3>
                  </div>
                  <span
                    class="text-sm font-semibold"
                    :class="
                      entry.rankMovement > 0
                        ? 'text-good'
                        : entry.rankMovement < 0
                          ? 'text-bad'
                          : 'text-muted'
                    "
                  >
                    {{ entry.rankMovement > 0 ? `+${entry.rankMovement}` : entry.rankMovement }}
                  </span>
                </div>
                <p class="mt-2 text-sm text-muted">{{ formatPoints(entry.totalPoints) }} pts</p>
              </article>
            </div>
          </section>
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
                <td class="px-5 py-3">{{ entry.displayName }}</td>
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
