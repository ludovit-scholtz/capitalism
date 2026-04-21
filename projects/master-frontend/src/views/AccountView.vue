<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { fetchMyGoldAccount, type PlayerGoldAccountInfo } from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()

const account = ref<PlayerGoldAccountInfo | null>(null)
const loading = ref(false)
const errorMessage = ref('')

function formatGold(value: number): string {
  return new Intl.NumberFormat(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 4,
  }).format(value)
}

function formatDate(isoString: string): string {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(isoString))
}

function formatTxAmount(amount: number): string {
  const sign = amount > 0 ? '+' : ''
  return `${sign}${formatGold(amount)} g`
}

async function loadAccount() {
  if (!auth.token) return
  loading.value = true
  errorMessage.value = ''
  try {
    account.value = await fetchMyGoldAccount(auth.token)
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : 'Failed to load gold account.'
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }
  await loadAccount()
})
</script>

<template>
  <div class="account-shell">
    <header class="account-header">
      <div class="account-header-inner">
        <div>
          <p class="section-kicker">My Account</p>
          <h1>Gold Balance</h1>
          <p class="account-subtitle">
            Your tokenized gold holdings on the Capitalism Network.
          </p>
        </div>
        <nav class="account-nav">
          <a href="/" class="nav-back-btn">← Back to portal</a>
        </nav>
      </div>
    </header>

    <main class="account-main">
      <!-- Loading state -->
      <div v-if="loading" class="state-message" role="status" aria-live="polite">
        Loading your gold account…
      </div>

      <!-- Error state -->
      <div v-else-if="errorMessage" class="state-error" role="alert">
        {{ errorMessage }}
        <button type="button" class="retry-btn" @click="loadAccount">Retry</button>
      </div>

      <template v-else-if="account">
        <!-- Balance card -->
        <section class="gold-balance-card" aria-label="Gold balance">
          <div class="balance-icon" aria-hidden="true">⬛</div>
          <div class="balance-body">
            <p class="balance-kicker">Current balance</p>
            <p class="balance-amount" aria-label="Gold balance in grams">
              <span class="balance-number">{{ formatGold(account.goldTokenBalance) }}</span>
              <span class="balance-unit">g</span>
            </p>
            <p class="balance-subtext">
              1 gold token = 1 gram of real-world gold
            </p>
          </div>

          <!-- Zero-balance empty state -->
          <aside v-if="account.goldTokenBalance === 0" class="zero-state">
            <p class="zero-state-title">You don't have any gold yet</p>
            <p class="zero-state-copy">
              Mine gold ore in-game, trade on the exchange, or earn it through economic activity
              across any Capitalism server. Your cross-server balance is stored here.
            </p>
          </aside>
        </section>

        <!-- What is gold section -->
        <section class="gold-info-card" aria-label="What is gold">
          <h2>What is tokenized gold?</h2>
          <ul class="gold-facts">
            <li>
              <span class="fact-icon">🏅</span>
              <div>
                <strong>1 token = 1 gram of physical gold.</strong> Each token in your account is
                backed by real-world bullion, giving it intrinsic value beyond the game.
              </div>
            </li>
            <li>
              <span class="fact-icon">🌐</span>
              <div>
                <strong>Cross-server asset.</strong> Your gold balance lives on the master server,
                not on any single game shard. It remains yours across all Capitalism worlds.
              </div>
            </li>
            <li>
              <span class="fact-icon">📈</span>
              <div>
                <strong>Trade on the FX exchange.</strong> Use the in-game AMM pools to swap
                between city currencies and gold, or provide liquidity to earn fee rewards.
              </div>
            </li>
          </ul>
        </section>

        <!-- Recent transactions -->
        <section class="gold-section" aria-labelledby="tx-heading">
          <div class="gold-section-header">
            <h2 id="tx-heading">Recent transactions</h2>
            <span v-if="account.lastUpdatedAtUtc" class="last-updated">
              Last updated {{ formatDate(account.lastUpdatedAtUtc) }}
            </span>
          </div>

          <p v-if="account.recentTransactions.length === 0" class="state-message">
            No transactions yet. Transactions will appear here once your balance changes.
          </p>

          <div v-else class="tx-table-wrap">
            <table class="tx-table" aria-label="Recent gold transactions">
              <thead>
                <tr>
                  <th>Date</th>
                  <th class="col-amount">Amount</th>
                  <th>Balance after</th>
                  <th>Note</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="tx in account.recentTransactions" :key="tx.id">
                  <td class="col-date">{{ formatDate(tx.createdAtUtc) }}</td>
                  <td
                    class="col-amount"
                    :class="tx.amount > 0 ? 'amount-positive' : 'amount-negative'"
                  >
                    {{ formatTxAmount(tx.amount) }}
                  </td>
                  <td>{{ formatGold(tx.balanceAfter) }} g</td>
                  <td class="col-note">{{ tx.note ?? '—' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
    </main>
  </div>
</template>

<style scoped>
.account-shell {
  min-height: 100vh;
  background: #0a0a0f;
  color: #e8e8f0;
  font-family: var(--font-body, system-ui, sans-serif);
}

/* ── Header ────────────────────────────────────────────────────────── */
.account-header {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  border-bottom: 1px solid rgba(255, 215, 0, 0.2);
  padding: 2rem 1.5rem;
}

.account-header-inner {
  max-width: 900px;
  margin: 0 auto;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1.5rem;
}

.section-kicker {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: #ffd700;
  margin: 0 0 0.25rem;
}

h1 {
  font-size: 1.75rem;
  font-weight: 700;
  margin: 0 0 0.5rem;
  color: #ffd700;
}

.account-subtitle {
  font-size: 0.9rem;
  color: #a0a0b8;
  margin: 0;
}

.account-nav {
  flex-shrink: 0;
}

.nav-back-btn {
  display: inline-block;
  padding: 0.5rem 1rem;
  border: 1px solid rgba(255, 215, 0, 0.3);
  border-radius: 6px;
  color: #ffd700;
  text-decoration: none;
  font-size: 0.875rem;
  transition: background 0.15s;
}

.nav-back-btn:hover {
  background: rgba(255, 215, 0, 0.08);
}

/* ── Main layout ───────────────────────────────────────────────────── */
.account-main {
  max-width: 900px;
  margin: 0 auto;
  padding: 2rem 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

/* ── State messages ────────────────────────────────────────────────── */
.state-message {
  color: #a0a0b8;
  font-size: 0.9rem;
  text-align: center;
  padding: 2rem;
}

.state-error {
  display: flex;
  align-items: center;
  gap: 1rem;
  background: rgba(220, 38, 38, 0.1);
  border: 1px solid rgba(220, 38, 38, 0.3);
  border-radius: 8px;
  padding: 1rem 1.5rem;
  color: #f87171;
  font-size: 0.9rem;
}

.retry-btn {
  flex-shrink: 0;
  padding: 0.4rem 0.9rem;
  border: 1px solid rgba(248, 113, 113, 0.4);
  border-radius: 6px;
  background: transparent;
  color: #f87171;
  font: inherit;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background 0.15s;
}

.retry-btn:hover {
  background: rgba(248, 113, 113, 0.08);
}

/* ── Balance card ──────────────────────────────────────────────────── */
.gold-balance-card {
  background: linear-gradient(135deg, #1a1a0a 0%, #1e1e0d 50%, #1a1a2e 100%);
  border: 1px solid rgba(255, 215, 0, 0.35);
  border-radius: 16px;
  padding: 2.5rem;
  display: flex;
  gap: 2rem;
  align-items: flex-start;
  flex-wrap: wrap;
  box-shadow: 0 4px 32px rgba(255, 215, 0, 0.06);
}

.balance-icon {
  font-size: 3rem;
  filter: sepia(1) saturate(4) hue-rotate(8deg);
  flex-shrink: 0;
}

.balance-body {
  flex: 1;
  min-width: 200px;
}

.balance-kicker {
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: rgba(255, 215, 0, 0.7);
  margin: 0 0 0.4rem;
}

.balance-amount {
  display: flex;
  align-items: baseline;
  gap: 0.4rem;
  margin: 0 0 0.5rem;
}

.balance-number {
  font-size: clamp(2rem, 6vw, 3.5rem);
  font-weight: 800;
  color: #ffd700;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.balance-unit {
  font-size: 1.25rem;
  font-weight: 600;
  color: rgba(255, 215, 0, 0.7);
}

.balance-subtext {
  font-size: 0.85rem;
  color: #a0a0b8;
  margin: 0;
}

/* ── Zero state ────────────────────────────────────────────────────── */
.zero-state {
  width: 100%;
  background: rgba(255, 215, 0, 0.04);
  border: 1px dashed rgba(255, 215, 0, 0.2);
  border-radius: 10px;
  padding: 1.25rem 1.5rem;
  margin-top: 0.5rem;
}

.zero-state-title {
  font-size: 0.95rem;
  font-weight: 600;
  color: rgba(255, 215, 0, 0.8);
  margin: 0 0 0.5rem;
}

.zero-state-copy {
  font-size: 0.875rem;
  color: #a0a0b8;
  margin: 0;
  line-height: 1.6;
}

/* ── Info card ─────────────────────────────────────────────────────── */
.gold-info-card {
  background: #12121e;
  border: 1px solid #1e1e30;
  border-radius: 12px;
  padding: 1.5rem;
}

.gold-info-card h2 {
  font-size: 1rem;
  font-weight: 600;
  color: #e8e8f0;
  margin: 0 0 1rem;
}

.gold-facts {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.gold-facts li {
  display: flex;
  gap: 0.75rem;
  font-size: 0.9rem;
  color: #c0c0d0;
  line-height: 1.5;
}

.fact-icon {
  flex-shrink: 0;
  font-size: 1.1rem;
  margin-top: 0.05rem;
}

.gold-facts strong {
  color: #e8e8f0;
}

/* ── Transactions section ──────────────────────────────────────────── */
.gold-section {
  background: #12121e;
  border: 1px solid #1e1e30;
  border-radius: 12px;
  padding: 1.5rem;
}

.gold-section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
  gap: 1rem;
  flex-wrap: wrap;
}

.gold-section-header h2 {
  font-size: 1rem;
  font-weight: 600;
  color: #e8e8f0;
  margin: 0;
}

.last-updated {
  font-size: 0.78rem;
  color: #606078;
}

/* ── Table ─────────────────────────────────────────────────────────── */
.tx-table-wrap {
  overflow-x: auto;
}

.tx-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.tx-table th {
  text-align: left;
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid #1e1e30;
  color: #606078;
  font-weight: 500;
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  white-space: nowrap;
}

.tx-table td {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid #16162a;
  color: #c0c0d0;
  vertical-align: middle;
}

.tx-table tbody tr:last-child td {
  border-bottom: none;
}

.col-date {
  white-space: nowrap;
  color: #808098;
}

.col-amount {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
  white-space: nowrap;
}

.amount-positive {
  color: #4ade80;
}

.amount-negative {
  color: #f87171;
}

.col-note {
  color: #808098;
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
