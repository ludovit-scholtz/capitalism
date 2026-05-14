<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { fetchGameServers, type GameServerSummary } from '@/lib/masterApi'
import { formatHeartbeatDistance } from '@/lib/time'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

const { t } = useI18n()

const servers = ref<GameServerSummary[]>([])
const loading = ref(true)
const errorMessage = ref('')

const AUTO_REFRESH_MS = 30_000

const onlineCount = computed(() => servers.value.filter((server) => server.isOnline).length)
const navItems = computed(() => [
  { label: t('nav.home'), to: '/' },
  { label: t('nav.ranking'), to: '/ranking' },
  { label: t('nav.supportDashboard'), to: '/support' },
])

function heartbeatLabel(server: GameServerSummary) {
  return formatHeartbeatDistance(server.lastHeartbeatAtUtc)
}

async function loadServers() {
  loading.value = true
  errorMessage.value = ''

  try {
    servers.value = await fetchGameServers()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('home.unableToLoadServers')
  } finally {
    loading.value = false
  }
}

async function refreshServers() {
  errorMessage.value = ''
  try {
    servers.value = await fetchGameServers()
  } catch (err) {
    // Silently ignore background refresh errors to preserve last good state
    console.debug('[GameServersView] Auto-refresh failed:', err)
  }
}

let refreshInterval: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  void loadServers()
  refreshInterval = setInterval(() => void refreshServers(), AUTO_REFRESH_MS)
})

onUnmounted(() => {
  if (refreshInterval !== null) {
    clearInterval(refreshInterval)
    refreshInterval = null
  }
})
</script>

<template>
  <main>
    <ViewJumbotron
      :kicker="t('home.liveRegistry')"
      :title="t('home.gameServers')"
      :subtitle="`${t('home.activeServers')}: ${onlineCount}`"
      variant="servers"
    />
    <ViewSubnav :items="navItems" aria-label="Game server navigation" />

    <section class="container pb-16 pt-2 lg:pb-20 lg:pt-2">
      <section class="card p-6 lg:p-8" aria-labelledby="server-list-heading">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 id="server-list-heading" class="text-3xl font-semibold text-body">
              {{ t('home.gameServers') }}
            </h1>
            <p class="mt-2 text-sm text-muted">{{ t('home.activeServers') }}: {{ onlineCount }}</p>
          </div>
          <button class="btn btn-secondary" type="button" @click="loadServers">
            {{ t('common.refresh') }}
          </button>
        </div>

        <p v-if="loading" class="state-message mt-5">{{ t('home.loadingServers') }}</p>
        <p v-else-if="errorMessage" class="state-error mt-5" role="alert">{{ errorMessage }}</p>
        <p v-else-if="servers.length === 0" class="state-message mt-5">{{ t('home.noServers') }}</p>

        <ul v-else class="mt-6 grid gap-4">
          <li
            v-for="server in servers"
            :key="server.id"
            class="rounded-xl border border-divider bg-card-raised p-4"
          >
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p class="text-lg font-semibold">{{ server.displayName }}</p>
                <p class="text-sm text-muted">
                  {{ server.region }} · {{ server.environment }} · v{{ server.version }}
                </p>
              </div>
              <span
                :class="[
                  'rounded-full px-3 py-1 text-xs font-semibold',
                  server.isOnline
                    ? 'bg-[rgba(34,197,94,0.15)] text-good'
                    : 'bg-[rgba(248,113,113,0.15)] text-bad',
                ]"
              >
                {{ server.isOnline ? t('home.online') : t('home.offline') }}
              </span>
            </div>

            <p class="mt-3 text-sm text-muted">
              {{ server.description || t('home.defaultDescription') }}
            </p>

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
              <a
                class="btn btn-primary"
                :href="server.frontendUrl"
                target="_blank"
                rel="noreferrer"
              >
                {{ t('home.playOnServer') }}
              </a>
            </div>
          </li>
        </ul>
      </section>
    </section>
  </main>
</template>
