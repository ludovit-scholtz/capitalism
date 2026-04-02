<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { fetchGameServers, type GameServerSummary } from '@/lib/masterApi'
import { formatHeartbeatDistance } from '@/lib/time'

const servers = ref<GameServerSummary[]>([])
const loading = ref(true)
const errorMessage = ref('')

const onlineCount = computed(() => servers.value.filter((server) => server.isOnline).length)

function heartbeatLabel(server: GameServerSummary) {
  return formatHeartbeatDistance(server.lastHeartbeatAtUtc)
}

async function loadServers() {
  loading.value = true
  errorMessage.value = ''

  try {
    servers.value = await fetchGameServers()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Unable to load game servers.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadServers()
})
</script>

<template>
  <main class="master-shell">
    <section class="hero-panel">
      <div class="hero-copy">
        <p class="eyebrow">Capitalism Network</p>
        <h1>One master website, multiple live economies.</h1>
        <p class="hero-text">
          Discover active game servers, compare their live heartbeat, and jump directly into the
          world that is currently running.
        </p>
      </div>

      <div class="hero-metrics">
        <article class="metric-card">
          <span class="metric-label">Active Servers</span>
          <strong>{{ onlineCount }}</strong>
        </article>
        <article class="metric-card">
          <span class="metric-label">Known Servers</span>
          <strong>{{ servers.length }}</strong>
        </article>
        <article class="metric-card">
          <span class="metric-label">Architecture</span>
          <strong>Master + Game</strong>
        </article>
      </div>
    </section>

    <section class="content-grid">
      <article class="pitch-card">
        <p class="section-kicker">How it works</p>
        <h2>Master infrastructure keeps discovery separate from simulation.</h2>
        <ul>
          <li>Master API stores the live registry of game servers.</li>
          <li>Each game server heartbeats into the master server on startup.</li>
          <li>The master frontend lists the servers and links players to the correct client.</li>
        </ul>
      </article>

      <section class="servers-panel" aria-labelledby="server-list-heading">
        <div class="servers-header">
          <div>
            <p class="section-kicker">Live registry</p>
            <h2 id="server-list-heading">Game servers</h2>
          </div>

          <button class="refresh-button" type="button" @click="loadServers">Refresh</button>
        </div>

        <p v-if="loading" class="state-message">Loading registered servers...</p>
        <p v-else-if="errorMessage" class="state-message state-error">{{ errorMessage }}</p>
        <p v-else-if="servers.length === 0" class="state-message">
          No servers have registered yet. Start a game server and it will appear here.
        </p>

        <ul v-else class="server-list">
          <li v-for="server in servers" :key="server.id" class="server-card">
            <div class="server-card-header">
              <div>
                <p class="server-name">{{ server.displayName }}</p>
                <p class="server-meta">{{ server.region }} · {{ server.environment }}</p>
              </div>
              <span :class="['status-pill', server.isOnline ? 'status-online' : 'status-offline']">
                {{ server.isOnline ? 'Online' : 'Offline' }}
              </span>
            </div>

            <p class="server-description">
              {{ server.description || 'Economic simulation shard registered with the master node.' }}
            </p>

            <dl class="server-stats">
              <div>
                <dt>Players</dt>
                <dd>{{ server.playerCount }}</dd>
              </div>
              <div>
                <dt>Companies</dt>
                <dd>{{ server.companyCount }}</dd>
              </div>
              <div>
                <dt>Tick</dt>
                <dd>{{ server.currentTick }}</dd>
              </div>
              <div>
                <dt>Heartbeat</dt>
                <dd>{{ heartbeatLabel(server) }}</dd>
              </div>
            </dl>

            <div class="server-links">
              <a class="launch-link" :href="server.frontendUrl" target="_blank" rel="noreferrer">
                Open game frontend
              </a>
              <a class="subtle-link" :href="server.graphqlUrl" target="_blank" rel="noreferrer">
                GraphQL
              </a>
            </div>
          </li>
        </ul>
      </section>
    </section>
  </main>
</template>

<style scoped>
.master-shell {
  width: min(1180px, calc(100vw - 2rem));
  margin: 0 auto;
  padding: 2rem 0 4rem;
}

.hero-panel {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(280px, 1fr);
  gap: 1.5rem;
  padding: 2rem;
  border: 1px solid var(--color-border);
  border-radius: 32px;
  background:
    radial-gradient(circle at top left, rgba(236, 145, 5, 0.22), transparent 36%),
    linear-gradient(135deg, rgba(18, 44, 83, 0.95), rgba(10, 22, 43, 0.94));
  box-shadow: 0 28px 60px rgba(8, 19, 37, 0.2);
}

.hero-copy h1 {
  font-size: clamp(2.6rem, 7vw, 4.4rem);
  line-height: 0.92;
  max-width: 10ch;
}

.hero-text {
  margin-top: 1rem;
  max-width: 54ch;
  color: rgba(245, 242, 235, 0.78);
  font-size: 1.05rem;
}

.eyebrow,
.section-kicker {
  text-transform: uppercase;
  letter-spacing: 0.14em;
  font-size: 0.72rem;
  color: var(--color-accent);
}

.hero-metrics {
  display: grid;
  gap: 1rem;
}

.metric-card {
  padding: 1rem 1.1rem;
  border-radius: 22px;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.15);
}

.metric-card strong {
  display: block;
  margin-top: 0.4rem;
  font-size: 1.8rem;
}

.metric-label {
  color: rgba(245, 242, 235, 0.74);
  font-size: 0.82rem;
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(260px, 0.9fr) minmax(0, 1.5fr);
  gap: 1.5rem;
  margin-top: 1.5rem;
}

.pitch-card,
.servers-panel {
  padding: 1.5rem;
  border-radius: 28px;
  border: 1px solid var(--color-border);
  background: rgba(255, 251, 243, 0.82);
  box-shadow: 0 20px 40px rgba(28, 40, 61, 0.1);
}

.pitch-card h2,
.servers-header h2 {
  margin-top: 0.45rem;
  font-size: 1.65rem;
  line-height: 1.05;
}

.pitch-card ul {
  margin-top: 1.1rem;
  padding-left: 1rem;
  color: var(--color-muted);
}

.pitch-card li + li {
  margin-top: 0.7rem;
}

.servers-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: end;
}

.refresh-button,
.launch-link,
.subtle-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.75rem 1rem;
  border-radius: 999px;
  border: 1px solid var(--color-border);
  transition: transform 0.18s ease, background-color 0.18s ease;
}

.refresh-button {
  background: var(--color-paper);
  color: var(--color-ink);
}

.refresh-button:hover,
.launch-link:hover,
.subtle-link:hover {
  transform: translateY(-1px);
  text-decoration: none;
}

.state-message {
  margin-top: 1rem;
  padding: 1rem;
  border-radius: 18px;
  background: rgba(18, 44, 83, 0.05);
  color: var(--color-muted);
}

.state-error {
  background: rgba(176, 67, 44, 0.08);
  color: #a03826;
}

.server-list {
  display: grid;
  gap: 1rem;
  margin-top: 1.2rem;
}

.server-card {
  padding: 1.2rem;
  border-radius: 22px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.9), rgba(248, 242, 232, 0.86));
  border: 1px solid rgba(17, 41, 79, 0.12);
}

.server-card-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.server-name {
  font-size: 1.25rem;
  font-weight: 700;
}

.server-meta,
.server-description {
  color: var(--color-muted);
}

.server-description {
  margin-top: 0.9rem;
}

.status-pill {
  height: fit-content;
  padding: 0.4rem 0.8rem;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 700;
}

.status-online {
  background: rgba(58, 140, 92, 0.14);
  color: #2d7d4e;
}

.status-offline {
  background: rgba(163, 91, 61, 0.14);
  color: #8d4b34;
}

.server-stats {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.75rem;
  margin-top: 1rem;
}

.server-stats div {
  padding: 0.8rem;
  border-radius: 18px;
  background: rgba(18, 44, 83, 0.04);
}

.server-stats dt {
  font-size: 0.74rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--color-muted);
}

.server-stats dd {
  margin-top: 0.35rem;
  font-weight: 700;
}

.server-links {
  display: flex;
  gap: 0.75rem;
  margin-top: 1rem;
}

.launch-link {
  background: var(--color-ink);
  color: var(--color-paper);
  border-color: var(--color-ink);
}

.subtle-link {
  background: transparent;
  color: var(--color-ink);
}

@media (max-width: 900px) {
  .hero-panel,
  .content-grid {
    grid-template-columns: 1fr;
  }

  .server-stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .master-shell {
    width: min(100vw - 1rem, 100%);
    padding-top: 1rem;
  }

  .hero-panel,
  .pitch-card,
  .servers-panel {
    padding: 1.1rem;
    border-radius: 24px;
  }

  .server-card-header,
  .server-links {
    flex-direction: column;
  }

  .server-stats {
    grid-template-columns: 1fr;
  }
}
</style>