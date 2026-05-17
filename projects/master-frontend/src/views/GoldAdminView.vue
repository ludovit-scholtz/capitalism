<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import {
  adjustGoldTokenBalance,
  fetchGoldTokenBalances,
  fetchGoldTokenTransactions,
  revokePlayerSessions,
  type GoldTokenBalanceInfo,
  type GoldTokenTransactionInfo,
} from '@/lib/masterApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

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
const sessionRevokeLoadingPlayerId = ref<string | null>(null)

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
  selectedEmail.value
    ? (balances.value.find((b) => b.email === selectedEmail.value) ?? null)
    : null,
)

const navItems = computed(() => [
  { label: t('nav.gameAdminDashboard'), to: '/game-admin' },
  { label: t('goldAdmin.transferOps'), to: '/gold-transfers-admin' },
])

// ── Data loading ───────────────────────────────────────────────────────────

async function loadBalances() {
  if (!auth.token) return
  balancesLoading.value = true
  balancesError.value = ''
  try {
    balances.value = await fetchGoldTokenBalances(auth.token)
  } catch (e) {
    balancesError.value = e instanceof Error ? e.message : t('goldAdmin.loadBalancesError')
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
    txError.value = e instanceof Error ? e.message : t('goldAdmin.loadTxError')
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
    adjustError.value = t('goldAdmin.amountInvalid')
    return
  }

  const note = adjustNote.value.trim()
  if (!note) {
    adjustError.value = t('goldAdmin.noteRequired')
    return
  }

  adjustLoading.value = true
  adjustError.value = ''
  adjustSuccess.value = ''

  try {
    const updated = await adjustGoldTokenBalance(auth.token, selectedEmail.value, amount, note)

    // Update the local balance display
    const idx = balances.value.findIndex((b) => b.email === selectedEmail.value)
    if (idx !== -1) {
      const existing = balances.value[idx]
      if (existing) {
        balances.value[idx] = { ...existing, goldTokenBalance: updated.goldTokenBalance }
      }
    }

    adjustSuccess.value = t('goldAdmin.updateSuccess', {
      amount: formatGold(updated.goldTokenBalance),
    })
    adjustAmount.value = ''
    adjustNote.value = ''

    // Refresh the transaction log for this user
    await loadTransactions(selectedEmail.value)
  } catch (e) {
    adjustError.value = e instanceof Error ? e.message : t('goldAdmin.adjustFailed')
  } finally {
    adjustLoading.value = false
  }
}

async function handleTxFilter() {
  await loadTransactions(txFilterEmail.value.trim() || undefined)
}

async function handleRevokeSessions(playerId: string) {
  if (!auth.token || !confirm(t('goldAdmin.revokeSessionsConfirm'))) {
    return
  }

  sessionRevokeLoadingPlayerId.value = playerId
  try {
    await revokePlayerSessions(auth.token, playerId)
    adjustSuccess.value = t('goldAdmin.revokeSessionsSuccess')
  } catch (e) {
    adjustError.value = e instanceof Error ? e.message : t('goldAdmin.revokeSessionsError')
  } finally {
    sessionRevokeLoadingPlayerId.value = null
  }
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

  if (!auth.gameAdminChecked) {
    await auth.refreshGameAdminAccess()
  }

  if (!auth.isGameAdmin) {
    void router.push('/')
    return
  }

  await Promise.all([loadBalances(), loadTransactions()])
})
</script>

<template>
  <div class="gold-admin-shell">
    <ViewJumbotron
      :kicker="t('goldAdmin.kicker')"
      :title="t('goldAdmin.title')"
      :subtitle="t('goldAdmin.subtitle')"
      variant="admin"
    />
    <ViewSubnav :items="navItems" aria-label="Gold admin navigation" />

    <main class="gold-admin-main">
      <!-- Balance table -->
      <section class="gold-section" aria-labelledby="balances-heading">
        <div class="gold-section-header">
          <h2 id="balances-heading">{{ t('goldAdmin.balancesTitle') }}</h2>
          <button
            class="refresh-btn"
            type="button"
            :disabled="balancesLoading"
            @click="loadBalances"
          >
            {{ balancesLoading ? t('common.loading') : t('common.refresh') }}
          </button>
        </div>

        <div class="search-bar">
          <input
            v-model="searchQuery"
            type="search"
            :placeholder="t('goldAdmin.searchPlaceholder')"
            class="search-input"
            :aria-label="t('goldAdmin.searchAria')"
          />
        </div>

        <p v-if="balancesError" class="state-error" role="alert">{{ balancesError }}</p>
        <p v-else-if="balancesLoading && balances.length === 0" class="state-message">
          {{ t('goldAdmin.loadingBalances') }}
        </p>
        <p v-else-if="filteredBalances.length === 0" class="state-message">
          {{ t('goldAdmin.noPlayers') }}
        </p>

        <div v-else class="balance-table-wrap">
          <table class="balance-table" aria-label="Player gold balances">
            <thead>
              <tr>
                <th>{{ t('goldAdmin.email') }}</th>
                <th>{{ t('goldAdmin.displayName') }}</th>
                <th class="col-balance">{{ t('goldAdmin.balanceG') }}</th>
                <th>{{ t('goldAdmin.actions') }}</th>
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
                  <div class="flex flex-col gap-2">
                    <button class="select-btn" type="button" @click.stop="selectUser(row.email)">
                      {{ t('goldAdmin.manage') }}
                    </button>
                    <button
                      class="select-btn"
                      type="button"
                      :disabled="sessionRevokeLoadingPlayerId === row.playerId"
                      @click.stop="handleRevokeSessions(row.playerId)"
                    >
                      {{
                        sessionRevokeLoadingPlayerId === row.playerId
                          ? t('common.loading')
                          : t('goldAdmin.revokeSessions')
                      }}
                    </button>
                  </div>
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
          {{ t('goldAdmin.adjustFor', { email: selectedEmail }) }}
        </h2>
        <p v-if="selectedBalance" class="current-balance-label">
          {{ t('goldAdmin.currentBalance') }}
          <strong>{{ formatGold(selectedBalance.goldTokenBalance) }} g</strong>
        </p>

        <form class="adjust-form" @submit.prevent="handleAdjust">
          <div class="form-row">
            <label for="adjust-amount" class="form-label">
              {{ t('goldAdmin.amountLabel') }}
            </label>
            <input
              id="adjust-amount"
              v-model="adjustAmount"
              type="number"
              step="0.0001"
              :placeholder="t('goldAdmin.amountPlaceholder')"
              class="form-input"
              :class="{ 'input-deduction': isDeduction }"
              required
            />
          </div>

          <div class="form-row">
            <label for="adjust-note" class="form-label">
              {{ t('goldAdmin.noteLabel') }}
              <span class="required-badge" aria-hidden="true">{{ t('goldAdmin.required') }}</span>
            </label>
            <input
              id="adjust-note"
              v-model="adjustNote"
              type="text"
              maxlength="500"
              :placeholder="t('goldAdmin.notePlaceholder')"
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
              <template v-if="adjustLoading">{{ t('goldAdmin.processing') }}</template>
              <template v-else-if="isDeduction">{{ t('goldAdmin.deductGold') }}</template>
              <template v-else>{{ t('goldAdmin.addGold') }}</template>
            </button>
            <button type="button" class="cancel-btn" @click="selectedEmail = null">
              {{ t('goldAdmin.cancel') }}
            </button>
          </div>

          <p v-if="adjustError" class="form-error" role="alert">{{ adjustError }}</p>
          <p v-if="adjustSuccess" class="form-success" role="status">{{ adjustSuccess }}</p>
        </form>
      </section>

      <!-- Transaction history -->
      <section class="gold-section" aria-labelledby="tx-heading">
        <div class="gold-section-header">
          <h2 id="tx-heading">{{ t('goldAdmin.txTitle') }}</h2>
        </div>

        <div class="tx-filter-bar">
          <input
            v-model="txFilterEmail"
            type="email"
            :placeholder="t('goldAdmin.txFilterPlaceholder')"
            class="search-input"
            :aria-label="t('goldAdmin.txFilterAria')"
            @keyup.enter="handleTxFilter"
          />
          <button type="button" class="refresh-btn" @click="handleTxFilter">
            {{ t('goldAdmin.filter') }}
          </button>
          <button
            type="button"
            class="refresh-btn refresh-btn--ghost"
            @click="
              () => {
                txFilterEmail = ''
                void loadTransactions()
              }
            "
          >
            {{ t('goldAdmin.clear') }}
          </button>
        </div>

        <p v-if="txError" class="state-error" role="alert">{{ txError }}</p>
        <p v-else-if="txLoading && transactions.length === 0" class="state-message">
          {{ t('goldAdmin.loadingTx') }}
        </p>
        <p v-else-if="transactions.length === 0" class="state-message">{{ t('goldAdmin.noTx') }}</p>

        <div v-else class="tx-table-wrap">
          <table class="tx-table" aria-label="Gold token transaction log">
            <thead>
              <tr>
                <th>{{ t('account.date') }}</th>
                <th>{{ t('goldAdmin.player') }}</th>
                <th class="col-amount">{{ t('goldAdmin.balanceG') }}</th>
                <th>{{ t('goldAdmin.before') }}</th>
                <th>{{ t('goldAdmin.after') }}</th>
                <th>{{ t('goldAdmin.admin') }}</th>
                <th>{{ t('account.note') }}</th>
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
                <td class="col-note">{{ tx.note ?? t('account.dash') }}</td>
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
  margin: 0 auto;
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
