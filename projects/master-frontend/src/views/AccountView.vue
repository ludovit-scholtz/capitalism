<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { fetchMyGoldAccount, type PlayerGoldAccountInfo } from '@/lib/masterApi'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const account = ref<PlayerGoldAccountInfo | null>(null)
const loading = ref(false)
const errorMessage = ref('')

// Subscription prolong form
const prolonging = ref(false)
const prolongError = ref('')
const prolongSuccess = ref(false)
const startupPackClaiming = ref(false)

const MONTHLY_PRO_PRICE_GOLD = 0.137
const STARTUP_PACK_PRICE_GOLD = 0.274
const REFERRAL_DISCOUNT_MULTIPLIER = 0.9
const GOLD_PRICE_PRECISION = 10000

const subscription = computed(() => auth.subscription)
const hasReferralDiscount = computed(() => auth.player?.hasReferralDiscount === true)

function subscriptionStatusLabel(): string {
  const sub = subscription.value
  if (!sub || sub.status === 'NONE') return t('subscription.statusNoActive')
  if (sub.status === 'EXPIRED') return t('subscription.statusExpired')
  if (sub.isActive) return t('subscription.statusActive')
  return t('subscription.statusInactive')
}

function subscriptionExpiryLabel(): string {
  const sub = subscription.value
  if (!sub?.expiresAtUtc) return ''
  const days = sub.daysRemaining ?? 0
  if (days === 0) return t('subscription.expiresToday')
  if (days === 1) return t('subscription.expiresTomorrow')
  return t('subscription.expiresInDays', { days })
}

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
    errorMessage.value = e instanceof Error ? e.message : t('account.loadError')
  } finally {
    loading.value = false
  }
}

async function prolongSubscription() {
  prolonging.value = true
  prolongError.value = ''
  prolongSuccess.value = false
  try {
    await auth.prolong(1)
    prolongSuccess.value = true
  } catch (e: unknown) {
    prolongError.value = e instanceof Error ? e.message : t('home.prolongError')
  } finally {
    prolonging.value = false
  }
}

function applyReferralDiscount(price: number): number {
  if (!hasReferralDiscount.value) return price
  return Math.round(price * REFERRAL_DISCOUNT_MULTIPLIER * GOLD_PRICE_PRECISION) / GOLD_PRICE_PRECISION
}

function formatGoldPrice(value: number): string {
  return new Intl.NumberFormat(undefined, {
    minimumFractionDigits: 3,
    maximumFractionDigits: 4,
  }).format(value)
}

async function claimStartupPack() {
  startupPackClaiming.value = true
  prolongError.value = ''
  prolongSuccess.value = false
  try {
    await auth.claimStartupPackOffer()
    prolongSuccess.value = true
  } catch (e: unknown) {
    prolongError.value = e instanceof Error ? e.message : t('home.prolongError')
  } finally {
    startupPackClaiming.value = false
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    void router.push('/login')
    return
  }
  await loadAccount()
})

const navItems = ref<Array<{ label: string; to: string }>>([])

onMounted(() => {
  navItems.value = [
    { label: t('nav.account'), to: '/account' },
    { label: t('account.depositNav'), to: '/account/deposit' },
    { label: t('account.withdrawNav'), to: '/account/withdraw' },
  ]
  if (auth.isGameAdmin) {
    navItems.value.unshift({ label: t('home.goldAdmin'), to: '/gold-admin' })
    navItems.value.push({ label: t('goldAdmin.transferOps'), to: '/gold-transfers-admin' })
  }
})
</script>

<template>
  <div class="account-shell">
    <ViewJumbotron
      :kicker="t('account.kicker')"
      :title="t('account.title')"
      :subtitle="t('account.subtitle')"
      variant="gold"
    />
    <ViewSubnav :items="navItems" aria-label="Account navigation" />

    <main class="account-main">
      <!-- Loading state -->
      <div v-if="loading" class="state-message" role="status" aria-live="polite">
        {{ t('account.loading') }}
      </div>

      <!-- Error state -->
      <div v-else-if="errorMessage" class="state-error" role="alert">
        {{ errorMessage }}
        <button type="button" class="retry-btn" @click="loadAccount">
          {{ t('account.retry') }}
        </button>
      </div>

      <template v-else-if="account">
        <!-- Balance card -->
        <section class="gold-balance-card" aria-label="Gold balance">
          <div class="balance-icon" aria-hidden="true">⬛</div>
          <div class="balance-body">
            <p class="balance-kicker">{{ t('account.currentBalance') }}</p>
            <p class="balance-amount" aria-label="Gold balance in grams">
              <span class="balance-number">{{ formatGold(account.goldTokenBalance) }}</span>
              <span class="balance-unit">g</span>
            </p>
            <p class="balance-subtext">
              {{ t('account.ratio') }}
            </p>
          </div>

          <!-- Zero-balance empty state -->
          <aside v-if="account.goldTokenBalance === 0" class="zero-state">
            <p class="zero-state-title">{{ t('account.zeroTitle') }}</p>
            <p class="zero-state-copy">
              {{ t('account.zeroCopy') }}
            </p>
          </aside>
        </section>

        <!-- Subscription status panel -->
        <section class="subscription-card" aria-label="Pro subscription status">
          <div class="sub-header">
            <div>
              <p class="sub-kicker">{{ t('account.subscriptionKicker') }}</p>
              <h2 class="sub-title">{{ t('account.subscriptionTitle') }}</h2>
            </div>
            <span
              class="sub-tier-badge"
              :class="subscription?.tier === 'PRO' ? 'badge-pro' : 'badge-free'"
            >
              {{ subscription?.tier === 'PRO' ? t('subscription.tierPro') : t('subscription.tierFree') }}
            </span>
          </div>

          <div class="sub-status-row">
            <span class="sub-status-dot" :class="subscription?.isActive ? 'dot-active' : 'dot-inactive'" />
            <span class="sub-status-label">{{ subscriptionStatusLabel() }}</span>
            <span v-if="subscription?.isActive && subscription.expiresAtUtc" class="sub-expiry">
              {{ subscriptionExpiryLabel() }}
            </span>
          </div>

          <!-- Upgrade prompt for free users -->
          <template v-if="!subscription?.isActive">
            <p class="sub-upgrade-copy">{{ t('account.subscriptionUpgradeCopy') }}</p>
            <button type="button" class="sub-upgrade-btn" :disabled="prolonging" @click="prolongSubscription">
              {{
                t('account.buyMonthlyPro', { grams: formatGoldPrice(applyReferralDiscount(MONTHLY_PRO_PRICE_GOLD)) })
              }}
            </button>
          </template>

          <!-- Pro subscription controls -->
          <template v-else>
            <p class="sub-active-copy">{{ t('account.subscriptionActiveCopy') }}</p>

            <div v-if="subscription?.canProlong" class="sub-prolong-form">
              <div class="sub-prolong-controls">
                <button
                  type="button"
                  class="sub-prolong-btn"
                  :disabled="prolonging"
                  @click="prolongSubscription"
                >
                  {{
                    prolonging
                      ? t('home.processing')
                      : t('account.buyMonthlyPro', {
                          grams: formatGoldPrice(applyReferralDiscount(MONTHLY_PRO_PRICE_GOLD)),
                        })
                  }}
                </button>
              </div>
            </div>
          </template>

          <div class="sub-prolong-form">
            <p class="sub-upgrade-copy">
              {{
                t('account.startupPackPrice', {
                  grams: formatGoldPrice(applyReferralDiscount(STARTUP_PACK_PRICE_GOLD)),
                })
              }}
            </p>
            <button
              type="button"
              class="sub-prolong-btn"
              :disabled="startupPackClaiming || !auth.player?.canClaimStartupPack"
              @click="claimStartupPack"
            >
              {{
                startupPackClaiming
                  ? t('home.processing')
                  : auth.player?.canClaimStartupPack
                    ? t('home.startupPack.claimButton')
                    : t('home.startupPack.claimed')
              }}
            </button>
          </div>
          <p v-if="hasReferralDiscount" class="sub-success">{{ t('account.referralDiscountActive') }}</p>
          <p v-if="prolongError" class="sub-error" role="alert">{{ prolongError }}</p>
          <p v-if="prolongSuccess" class="sub-success">{{ t('home.prolongSuccess') }}</p>
        </section>

        <!-- What is gold section -->
        <section class="gold-info-card" aria-label="What is gold">
          <h2>{{ t('account.whatIsTitle') }}</h2>
          <ul class="gold-facts">
            <li>
              <span class="fact-icon">🏅</span>
              <div>
                <strong>{{ t('account.fact1Title') }}</strong> {{ t('account.fact1Body') }}
              </div>
            </li>
            <li>
              <span class="fact-icon">🌐</span>
              <div>
                <strong>{{ t('account.fact2Title') }}</strong> {{ t('account.fact2Body') }}
              </div>
            </li>
            <li>
              <span class="fact-icon">📈</span>
              <div>
                <strong>{{ t('account.fact3Title') }}</strong> {{ t('account.fact3Body') }}
              </div>
            </li>
          </ul>
        </section>

        <!-- Recent transactions -->
        <section class="gold-section" aria-labelledby="tx-heading">
          <div class="gold-section-header">
            <h2 id="tx-heading">{{ t('account.txTitle') }}</h2>
            <span v-if="account.lastUpdatedAtUtc" class="last-updated">
              {{ t('account.lastUpdated', { date: formatDate(account.lastUpdatedAtUtc) }) }}
            </span>
          </div>

          <p v-if="account.recentTransactions.length === 0" class="state-message">
            {{ t('account.noTx') }}
          </p>

          <div v-else class="tx-table-wrap">
            <table class="tx-table" aria-label="Recent gold transactions">
              <thead>
                <tr>
                  <th>{{ t('account.date') }}</th>
                  <th class="col-amount">{{ t('account.amount') }}</th>
                  <th>{{ t('account.balanceAfter') }}</th>
                  <th>{{ t('account.note') }}</th>
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
                  <td class="col-note">{{ tx.note ?? t('account.dash') }}</td>
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

/* ── Subscription card ─────────────────────────────────────────────── */
.subscription-card {
  background: #12121e;
  border: 1px solid #1e1e30;
  border-radius: 12px;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.sub-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.sub-kicker {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: #606078;
  margin: 0 0 0.2rem;
}

.sub-title {
  font-size: 1rem;
  font-weight: 600;
  color: #e8e8f0;
  margin: 0;
}

.sub-tier-badge {
  padding: 0.3rem 0.75rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  flex-shrink: 0;
}

.badge-pro {
  background: rgba(255, 215, 0, 0.15);
  border: 1px solid rgba(255, 215, 0, 0.4);
  color: #ffd700;
}

.badge-free {
  background: rgba(96, 96, 120, 0.2);
  border: 1px solid #2e2e4a;
  color: #808098;
}

.sub-status-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
}

.sub-status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.dot-active {
  background: #4ade80;
  box-shadow: 0 0 6px rgba(74, 222, 128, 0.5);
}

.dot-inactive {
  background: #606078;
}

.sub-status-label {
  color: #c0c0d0;
}

.sub-expiry {
  margin-left: auto;
  font-size: 0.8rem;
  color: #808098;
}

.sub-upgrade-copy,
.sub-active-copy {
  font-size: 0.875rem;
  color: #a0a0b8;
  line-height: 1.5;
  margin: 0;
}

.sub-upgrade-btn {
  display: inline-block;
  padding: 0.6rem 1.25rem;
  background: rgba(255, 215, 0, 0.12);
  border: 1px solid rgba(255, 215, 0, 0.35);
  border-radius: 8px;
  color: #ffd700;
  font-size: 0.875rem;
  font-weight: 600;
  text-decoration: none;
  align-self: flex-start;
  transition: background 0.15s;
}

.sub-upgrade-btn:hover {
  background: rgba(255, 215, 0, 0.2);
}

.sub-prolong-form {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.sub-prolong-label {
  font-size: 0.8rem;
  color: #808098;
}

.sub-prolong-controls {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.sub-months-input {
  width: 64px;
  padding: 0.4rem 0.6rem;
  background: #0a0a1a;
  border: 1px solid #2e2e4a;
  border-radius: 6px;
  color: #e8e8f0;
  font: inherit;
  font-size: 0.875rem;
}

.sub-months-unit {
  font-size: 0.85rem;
  color: #808098;
}

.sub-prolong-btn {
  padding: 0.4rem 1rem;
  background: rgba(255, 215, 0, 0.1);
  border: 1px solid rgba(255, 215, 0, 0.3);
  border-radius: 6px;
  color: #ffd700;
  font: inherit;
  font-size: 0.875rem;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s;
}

.sub-prolong-btn:hover:not(:disabled) {
  background: rgba(255, 215, 0, 0.18);
}

.sub-prolong-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.sub-error {
  font-size: 0.825rem;
  color: #f87171;
  margin: 0;
}

.sub-success {
  font-size: 0.825rem;
  color: #4ade80;
  margin: 0;
}
</style>
