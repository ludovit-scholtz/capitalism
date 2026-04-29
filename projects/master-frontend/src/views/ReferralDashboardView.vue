<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  createAdditionalReferralCode,
  getReferralDashboard,
  getReferralProfile,
  syncReferralSubscriptionStatus,
  type ReferralDashboardRow,
} from '@/lib/referrals'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()

const rows = ref<ReferralDashboardRow[]>([])
const codes = ref<string[]>([])
const hasReferralProfile = ref(false)
const notice = ref('')
const errorMessage = ref('')

const totalStats = computed(() => {
  return rows.value.reduce(
    (acc, row) => {
      acc.direct += row.directRegistrations
      acc.second += row.secondLevelRegistrations
      acc.active += row.activeSubscriptions
      acc.secondActive += row.secondLevelActiveSubscriptions
      return acc
    },
    { direct: 0, second: 0, active: 0, secondActive: 0 },
  )
})

function reloadDashboard() {
  if (!auth.player?.email) {
    return
  }

  const profile = getReferralProfile(auth.player.email)
  hasReferralProfile.value = !!profile.referralIdentity
  codes.value = profile.referralCodes.map((entry) => entry.code)
  rows.value = getReferralDashboard(auth.player.email)
}

function createCode() {
  if (!auth.player?.email) {
    return
  }

  errorMessage.value = ''
  notice.value = ''

  try {
    const created = createAdditionalReferralCode(auth.player.email)
    notice.value = `New referral code generated: ${created.code}`
    reloadDashboard()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to create referral code.'
  }
}

onMounted(async () => {
  if (!auth.isAuthenticated) {
    await router.push('/login')
    return
  }

  await auth.fetchSubscription()

  if (auth.player?.email) {
    syncReferralSubscriptionStatus(auth.player.email, !!auth.subscription?.isActive)
  }

  reloadDashboard()
})
</script>

<template>
  <main class="dash-shell">
    <section class="dash-card">
      <header class="dash-header">
        <div>
          <p class="eyebrow">Referral Program</p>
          <h1>Referral Dashboard</h1>
          <p class="subtitle">
            Track registrations and active subscriptions per referral code, including second-level
            network activity.
          </p>
        </div>
        <div class="header-actions">
          <RouterLink class="ghost" to="/referrals/setup">Setup Code</RouterLink>
          <RouterLink class="ghost" to="/referrals/become">Become Referral</RouterLink>
        </div>
      </header>

      <section v-if="!hasReferralProfile" class="empty-state">
        <h2>Referral profile not active</h2>
        <p>
          Activate your referral profile first. You need to provide your name and tax domicile
          before creating or tracking referral codes.
        </p>
        <RouterLink class="primary" to="/referrals/become">Activate Now</RouterLink>
      </section>

      <template v-else>
        <section class="summary-grid" aria-label="Referral summary cards">
          <article class="summary-card">
            <p>Direct registrations</p>
            <strong>{{ totalStats.direct }}</strong>
          </article>
          <article class="summary-card">
            <p>Second-level registrations</p>
            <strong>{{ totalStats.second }}</strong>
          </article>
          <article class="summary-card">
            <p>Active subscriptions</p>
            <strong>{{ totalStats.active }}</strong>
          </article>
          <article class="summary-card">
            <p>Second-level active subs</p>
            <strong>{{ totalStats.secondActive }}</strong>
          </article>
        </section>

        <div class="codes-toolbar">
          <div class="code-list" aria-label="Owned referral codes">
            <span v-for="code in codes" :key="code" class="code-pill">{{ code }}</span>
          </div>
          <button type="button" class="primary" @click="createCode">Create Another Code</button>
        </div>

        <p v-if="notice" class="success" role="status">{{ notice }}</p>
        <p v-if="errorMessage" class="error" role="alert">{{ errorMessage }}</p>

        <div class="table-wrap">
          <table aria-label="Referral metrics">
            <thead>
              <tr>
                <th>Referral code</th>
                <th>Registered users</th>
                <th>Second-level registrations</th>
                <th>Active subscriptions</th>
                <th>Second-level active subs</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.code">
                <td class="code-col">{{ row.code }}</td>
                <td>{{ row.directRegistrations }}</td>
                <td>{{ row.secondLevelRegistrations }}</td>
                <td>{{ row.activeSubscriptions }}</td>
                <td>{{ row.secondLevelActiveSubscriptions }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </section>
  </main>
</template>

<style scoped>
.dash-shell {
  min-height: 100dvh;
  padding: 2rem 1rem 3rem;
}

.dash-card {
  width: min(1120px, 100%);
  margin: 0 auto;
  border: 1px solid var(--color-border);
  border-radius: 24px;
  background: var(--color-paper-strong);
  padding: 1.5rem;
  box-shadow: var(--shadow-soft);
  display: grid;
  gap: 1rem;
}

.dash-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.12em;
  font-size: 0.72rem;
  color: var(--color-accent-deep);
}

.subtitle {
  margin-top: 0.45rem;
  color: var(--color-muted);
  line-height: 1.6;
}

.header-actions {
  display: flex;
  gap: 0.6rem;
  align-self: start;
}

.primary,
.ghost {
  border-radius: 999px;
  padding: 0.65rem 1rem;
  text-decoration: none;
  border: none;
  font-weight: 700;
}

.primary {
  background: var(--color-ink);
  color: var(--color-paper);
}

.ghost {
  background: rgba(17, 41, 79, 0.08);
  color: var(--color-ink);
}

.empty-state {
  border: 1px dashed var(--color-border);
  border-radius: 18px;
  padding: 1rem;
  display: grid;
  gap: 0.7rem;
}

.summary-grid {
  display: grid;
  gap: 0.7rem;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
}

.summary-card {
  border: 1px solid var(--color-border);
  border-radius: 16px;
  padding: 0.95rem;
  background: #fff;
  display: grid;
  gap: 0.35rem;
}

.summary-card p {
  color: var(--color-muted);
  font-size: 0.82rem;
}

.summary-card strong {
  font-size: 1.5rem;
}

.codes-toolbar {
  display: flex;
  justify-content: space-between;
  gap: 0.8rem;
  flex-wrap: wrap;
}

.code-list {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.code-pill {
  border: 1px solid var(--color-border);
  border-radius: 999px;
  padding: 0.35rem 0.7rem;
  font-weight: 700;
  letter-spacing: 0.09em;
  background: #fff;
}

.table-wrap {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: 16px;
}

table {
  width: 100%;
  border-collapse: collapse;
  min-width: 780px;
}

th,
td {
  border-bottom: 1px solid var(--color-border);
  text-align: left;
  padding: 0.85rem;
  font-size: 0.9rem;
}

thead th {
  background: rgba(17, 41, 79, 0.06);
  font-size: 0.78rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.code-col {
  font-weight: 700;
  letter-spacing: 0.08em;
}

.error {
  color: #b0432c;
}

.success {
  color: #245f3d;
}
</style>
