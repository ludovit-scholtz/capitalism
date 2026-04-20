<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  adjustGoldTokenBalance,
  fetchGoldTokenBalances,
  fetchGoldTokenTransactions,
  type GoldTokenBalanceInfo,
  type GoldTokenTransactionInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()

// ── State ──────────────────────────────────────────────────────────────────

const balances = ref<GoldTokenBalanceInfo[]>([])
const transactions = ref<GoldTokenTransactionInfo[]>([])
const balancesLoading = ref(false)
const txLoading = ref(false)
const balancesError = ref('')
const txError = ref('')

const searchQuery = ref('')
const selectedEmail = ref<string | null>(null)

const adjustAmount = ref('')
const adjustNote = ref('')
const adjustLoading = ref(false)
const adjustError = ref('')
const adjustSuccess = ref('')

const txFilterEmail = ref('')

// ── Computed ───────────────────────────────────────────────────────────────

const filteredBalances = computed(() => {
  if (!searchQuery.value.trim()) return balances.value
  const q = searchQuery.value.trim().toLowerCase()
  return balances.value.filter(
    (b) => b.email.toLowerCase().includes(q) || b.displayName.toLowerCase().includes(q),
  )
})

const adjustAmountNumber = computed(() => {
  const n = parseFloat(adjustAmount.value)
  return isNaN(n) ? null : n
})

const isDeduction = computed(
  () => adjustAmountNumber.value !== null && adjustAmountNumber.value < 0,
)

const selectedBalance = computed(() =>
  selectedEmail.value ? (balances.value.find((b) => b.email === selectedEmail.value) ?? null) : null,
)

// ── Data loading ───────────────────────────────────────────────────────────

async function loadBalances() {
  if (!auth.token) return
  balancesLoading.value = true
  balancesError.value = ''
  try {
    balances.value = await fetchGoldTokenBalances(auth.token)
  } catch (e) {
    balancesError.value = e instanceof Error ? e.message : 'Failed to load balances.'
  } finally {
    balancesLoading.value = false
  }
}

async function loadTransactions(email?: string) {
  if (!auth.token) return
  txLoading.value = true
  txError.value = ''
  try {
    transactions.value = await fetchGoldTokenTransactions(auth.token, email, 50)
  } catch (e) {
    txError.value = e instanceof Error ? e.message : 'Failed to load transaction history.'
  } finally {
    txLoading.value = false
  }
}

// ── Actions ────────────────────────────────────────────────────────────────

function selectUser(email: string) {
  selectedEmail.value = email
  adjustAmount.value = ''
  adjustNote.value = ''
  adjustError.value = ''
  adjustSuccess.value = ''
  void loadTransactions(email)
}

async function handleAdjust() {
  if (!auth.token || !selectedEmail.value) return

  const amount = adjustAmountNumber.value
  if (amount === null || amount === 0) {
    adjustError.value = 'Amount must be a non-zero number.'
    return
  }

  const note = adjustNote.value.trim()
  if (!note) {
    adjustError.value = 'An audit note is required. Please explain the reason for this adjustment.'
    return
  }

  adjustLoading.value = true
  adjustError.value = ''
  adjustSuccess.value = ''

  try {
    const updated = await adjustGoldTokenBalance(
      auth.token,
      selectedEmail.value,
      amount,
      note,
    )

    // Update the local balance display
    const idx = balances.value.findIndex((b) => b.email === selectedEmail.value)
    if (idx !== -1) {
      const existing = balances.value[idx]
      if (existing) {
        balances.value[idx] = { ...existing, goldTokenBalance: updated.goldTokenBalance }
      }
    }

    adjustSuccess.value = `✓ Balance updated to ${formatGold(updated.goldTokenBalance)} g`
    adjustAmount.value = ''
    adjustNote.value = ''

    // Refresh the transaction log for this user
    await loadTransactions(selectedEmail.value)
  } catch (e) {
    adjustError.value = e instanceof Error ? e.message : 'Adjustment failed.'
  } finally {
    adjustLoading.value = false
  }
}

async function handleTxFilter() {
  await loadTransactions(txFilterEmail.value.trim() || undefined)
}

// ── Formatting ─────────────────────────────────────────────────────────────

function formatGold(value: number): string {
  return value.toFixed(4)
}

function formatDateTime(iso: string): string {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso))
}

function formatTxAmount(amount: number): string {
  return amount > 0 ? `+${formatGold(amount)}` : formatGold(amount)
}

// ── Lifecycle ──────────────────────────────────────────────────────────────

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }
  await Promise.all([loadBalances(), loadTransactions()])
})
</script>

<template>
  <div class="gold-admin-shell">
    <header class="gold-admin-header">
      <div class="gold-admin-header-inner">
        <div>
          <p class="section-kicker">Master Administration</p>
          <h1>Gold Token Management</h1>
          <p class="gold-admin-subtitle">
            View and adjust player gold token balances. Every change is recorded in the audit log.
          </p>
        </div>
        <nav class="gold-admin-nav">
          <a href="/" class="nav-back-btn">← Back to portal</a>
        </nav>
      </div>
    </header>

    <main class="gold-admin-main">
      <!-- Balance table -->
      <section class="gold-section" aria-labelledby="balances-heading">
        <div class="gold-section-header">
          <h2 id="balances-heading">Player Balances</h2>
          <button class="refresh-btn" type="button" :disabled="balancesLoading" @click="loadBalances">
            {{ balancesLoading ? 'Loading…' : 'Refresh' }}
          </button>
        </div>

        <div class="search-bar">
          <input
            v-model="searchQuery"
            type="search"
            placeholder="Search by email or name…"
            class="search-input"
            aria-label="Search players"
          />
        </div>

        <p v-if="balancesError" class="state-error" role="alert">{{ balancesError }}</p>
        <p v-else-if="balancesLoading && balances.length === 0" class="state-message">
          Loading balances…
        </p>
        <p v-else-if="filteredBalances.length === 0" class="state-message">No players found.</p>

        <div v-else class="balance-table-wrap">
          <table class="balance-table" aria-label="Player gold balances">
            <thead>
              <tr>
                <th>Email</th>
                <th>Display Name</th>
                <th class="col-balance">Balance (g)</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="row in filteredBalances"
                :key="row.playerId"
                :class="{ 'row-selected': selectedEmail === row.email }"
                @click="selectUser(row.email)"
              >
                <td class="col-email">{{ row.email }}</td>
                <td>{{ row.displayName }}</td>
                <td class="col-balance">
                  <span class="gold-badge">⚙ {{ formatGold(row.goldTokenBalance) }} g</span>
                </td>
                <td>
                  <button
                    class="select-btn"
                    type="button"
                    @click.stop="selectUser(row.email)"
                  >
                    Manage
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Adjust panel -->
      <section
        v-if="selectedEmail"
        class="gold-section adjust-panel"
        aria-labelledby="adjust-heading"
      >
        <h2 id="adjust-heading">
          Adjust balance for <span class="adjust-target-email">{{ selectedEmail }}</span>
        </h2>
        <p v-if="selectedBalance" class="current-balance-label">
          Current balance:
          <strong>{{ formatGold(selectedBalance.goldTokenBalance) }} g</strong>
        </p>

        <form class="adjust-form" @submit.prevent="handleAdjust">
          <div class="form-row">
            <label for="adjust-amount" class="form-label">
              Amount (g) — positive to add, negative to deduct
            </label>
            <input
              id="adjust-amount"
              v-model="adjustAmount"
              type="number"
              step="0.0001"
              placeholder="e.g. 10.5 or -5.0"
              class="form-input"
              :class="{ 'input-deduction': isDeduction }"
              required
            />
          </div>

          <div class="form-row">
            <label for="adjust-note" class="form-label">
              Note (audit log) <span class="required-badge" aria-hidden="true">*</span>
            </label>
            <input
              id="adjust-note"
              v-model="adjustNote"
              type="text"
              maxlength="500"
              placeholder="Reason for adjustment (required)…"
              class="form-input"
              required
              aria-required="true"
            />
          </div>

          <div class="form-actions">
            <button
              type="submit"
              class="adjust-btn"
              :class="{ 'adjust-btn--deduct': isDeduction }"
              :disabled="adjustLoading || !adjustAmount || !adjustNote.trim()"
            >
              <template v-if="adjustLoading">Processing…</template>
              <template v-else-if="isDeduction">Deduct Gold</template>
              <template v-else>Add Gold</template>
            </button>
            <button
              type="button"
              class="cancel-btn"
              @click="selectedEmail = null"
            >
              Cancel
            </button>
          </div>

          <p v-if="adjustError" class="form-error" role="alert">{{ adjustError }}</p>
          <p v-if="adjustSuccess" class="form-success" role="status">{{ adjustSuccess }}</p>
        </form>
      </section>

      <!-- Transaction history -->
      <section class="gold-section" aria-labelledby="tx-heading">
        <div class="gold-section-header">
          <h2 id="tx-heading">Transaction History</h2>
        </div>

        <div class="tx-filter-bar">
          <input
            v-model="txFilterEmail"
            type="email"
            placeholder="Filter by email…"
            class="search-input"
            aria-label="Filter transactions by email"
            @keyup.enter="handleTxFilter"
          />
          <button type="button" class="refresh-btn" @click="handleTxFilter">Filter</button>
          <button
            type="button"
            class="refresh-btn refresh-btn--ghost"
            @click="() => { txFilterEmail = ''; void loadTransactions() }"
          >
            Clear
          </button>
        </div>

        <p v-if="txError" class="state-error" role="alert">{{ txError }}</p>
        <p v-else-if="txLoading && transactions.length === 0" class="state-message">
          Loading transactions…
        </p>
        <p v-else-if="transactions.length === 0" class="state-message">
          No transactions found.
        </p>

        <div v-else class="tx-table-wrap">
          <table class="tx-table" aria-label="Gold token transaction log">
            <thead>
              <tr>
                <th>Date</th>
                <th>Player</th>
                <th class="col-amount">Amount (g)</th>
                <th>Before</th>
                <th>After</th>
                <th>Admin</th>
                <th>Note</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="tx in transactions" :key="tx.id">
                <td class="col-date">{{ formatDateTime(tx.createdAtUtc) }}</td>
                <td class="col-email">{{ tx.playerEmail }}</td>
                <td
                  class="col-amount"
                  :class="tx.amount > 0 ? 'amount-positive' : 'amount-negative'"
                >
                  {{ formatTxAmount(tx.amount) }}
                </td>
                <td>{{ formatGold(tx.balanceBefore) }}</td>
                <td>{{ formatGold(tx.balanceAfter) }}</td>
                <td class="col-email">{{ tx.adminEmail }}</td>
                <td class="col-note">{{ tx.note ?? '—' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </main>
  </div>
</template>

<style scoped>
.gold-admin-shell {
  min-height: 100vh;
  background: #0a0a0f;
  color: #e8e8f0;
  font-family: var(--font-body, system-ui, sans-serif);
}

.gold-admin-header {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  border-bottom: 1px solid rgba(255, 215, 0, 0.2);
  padding: 2rem 1.5rem;
}

.gold-admin-header-inner {
  max-width: 1200px;
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

.gold-admin-subtitle {
  font-size: 0.9rem;
  color: #a0a0b8;
  margin: 0;
  max-width: 520px;
}

.gold-admin-nav {
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

.gold-admin-main {
  max-width: 1200px;
  margin: 0 auto;
  padding: 2rem 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

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
}

h2 {
  font-size: 1.125rem;
  font-weight: 600;
  color: #e8e8f0;
  margin: 0;
}

.search-bar,
.tx-filter-bar {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.search-input {
  flex: 1;
  min-width: 200px;
  padding: 0.5rem 0.75rem;
  background: #1a1a2e;
  border: 1px solid #2e2e48;
  border-radius: 6px;
  color: #e8e8f0;
  font-size: 0.875rem;
}

.search-input::placeholder {
  color: #555570;
}

.refresh-btn {
  padding: 0.5rem 1rem;
  background: #1e1e30;
  border: 1px solid #2e2e48;
  border-radius: 6px;
  color: #e8e8f0;
  font-size: 0.875rem;
  cursor: pointer;
  transition: background 0.15s;
}

.refresh-btn:hover:not(:disabled) {
  background: #2a2a40;
}

.refresh-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.refresh-btn--ghost {
  background: transparent;
  color: #a0a0b8;
}

.state-message {
  color: #a0a0b8;
  font-size: 0.875rem;
  padding: 1rem 0;
}

.state-error {
  color: #ff6b6b;
  font-size: 0.875rem;
  padding: 0.75rem 1rem;
  background: rgba(255, 107, 107, 0.08);
  border-radius: 6px;
  border: 1px solid rgba(255, 107, 107, 0.2);
}

.balance-table-wrap,
.tx-table-wrap {
  overflow-x: auto;
}

.balance-table,
.tx-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.balance-table th,
.tx-table th {
  text-align: left;
  padding: 0.5rem 0.75rem;
  color: #a0a0b8;
  font-weight: 500;
  border-bottom: 1px solid #1e1e30;
  white-space: nowrap;
}

.balance-table td,
.tx-table td {
  padding: 0.6rem 0.75rem;
  border-bottom: 1px solid #161625;
  vertical-align: middle;
}

.balance-table tbody tr {
  cursor: pointer;
  transition: background 0.1s;
}

.balance-table tbody tr:hover {
  background: rgba(255, 215, 0, 0.04);
}

.balance-table tbody tr.row-selected {
  background: rgba(255, 215, 0, 0.08);
}

.col-email {
  color: #a0d4ff;
  font-size: 0.8125rem;
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.col-balance {
  text-align: right;
}

.gold-badge {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  background: rgba(255, 215, 0, 0.1);
  border: 1px solid rgba(255, 215, 0, 0.25);
  border-radius: 4px;
  color: #ffd700;
  font-family: monospace;
  font-size: 0.8125rem;
}

.select-btn {
  padding: 0.3rem 0.75rem;
  background: rgba(255, 215, 0, 0.1);
  border: 1px solid rgba(255, 215, 0, 0.25);
  border-radius: 4px;
  color: #ffd700;
  font-size: 0.8125rem;
  cursor: pointer;
  transition: background 0.15s;
}

.select-btn:hover {
  background: rgba(255, 215, 0, 0.2);
}

/* Adjust panel */
.adjust-panel {
  border-color: rgba(255, 215, 0, 0.2);
}

.adjust-target-email {
  color: #a0d4ff;
}

.current-balance-label {
  font-size: 0.875rem;
  color: #a0a0b8;
  margin: 0.25rem 0 1.25rem;
}

.current-balance-label strong {
  color: #ffd700;
}

.adjust-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 480px;
}

.form-row {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.form-label {
  font-size: 0.8125rem;
  color: #a0a0b8;
}

.required-badge {
  color: #ff8080;
  font-weight: 700;
  margin-left: 2px;
}

.form-input {
  padding: 0.5rem 0.75rem;
  background: #1a1a2e;
  border: 1px solid #2e2e48;
  border-radius: 6px;
  color: #e8e8f0;
  font-size: 0.875rem;
}

.form-input.input-deduction {
  border-color: rgba(255, 107, 107, 0.4);
}

.form-actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.adjust-btn {
  padding: 0.6rem 1.5rem;
  background: rgba(255, 215, 0, 0.15);
  border: 1px solid rgba(255, 215, 0, 0.4);
  border-radius: 6px;
  color: #ffd700;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s;
}

.adjust-btn:hover:not(:disabled) {
  background: rgba(255, 215, 0, 0.25);
}

.adjust-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.adjust-btn--deduct {
  background: rgba(255, 107, 107, 0.1);
  border-color: rgba(255, 107, 107, 0.3);
  color: #ff8080;
}

.adjust-btn--deduct:hover:not(:disabled) {
  background: rgba(255, 107, 107, 0.2);
}

.cancel-btn {
  padding: 0.6rem 1.25rem;
  background: transparent;
  border: 1px solid #2e2e48;
  border-radius: 6px;
  color: #a0a0b8;
  cursor: pointer;
  transition: background 0.15s;
}

.cancel-btn:hover {
  background: #1e1e30;
}

.form-error {
  color: #ff8080;
  font-size: 0.875rem;
}

.form-success {
  color: #6fffb0;
  font-size: 0.875rem;
}

/* Transaction table */
.col-date {
  white-space: nowrap;
  font-size: 0.8125rem;
  color: #a0a0b8;
}

.col-amount {
  text-align: right;
  font-family: monospace;
  font-weight: 600;
}

.amount-positive {
  color: #6fffb0;
}

.amount-negative {
  color: #ff8080;
}

.col-note {
  color: #a0a0b8;
  font-size: 0.8125rem;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
