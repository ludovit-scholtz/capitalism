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
  <main class="dash-shell min-h-dvh px-4 py-8 pb-12">
    <section
      class="dash-card mx-auto grid w-full max-w-[1120px] gap-4 rounded-3xl border border-[var(--color-border)] bg-[var(--color-paper-strong)] p-6 shadow-[var(--shadow-soft)]"
    >
      <header class="dash-header flex flex-wrap justify-between gap-4">
        <div>
          <p
            class="eyebrow text-[0.72rem] uppercase tracking-[0.12em] text-[var(--color-accent-deep)]"
          >
            Referral Program
          </p>
          <h1>Referral Dashboard</h1>
          <p class="subtitle mt-2 leading-[1.6] text-[var(--color-muted)]">
            Track registrations and active subscriptions per referral code, including second-level
            network activity.
          </p>
        </div>
        <div class="header-actions flex gap-2 self-start">
          <RouterLink
            class="ghost rounded-full bg-[rgba(17,41,79,0.08)] px-4 py-2.5 font-bold text-[var(--color-ink)] no-underline"
            to="/referrals/setup"
            >Setup Code</RouterLink
          >
          <RouterLink
            class="ghost rounded-full bg-[rgba(17,41,79,0.08)] px-4 py-2.5 font-bold text-[var(--color-ink)] no-underline"
            to="/referrals/become"
            >Become Referral</RouterLink
          >
        </div>
      </header>

      <section
        v-if="!hasReferralProfile"
        class="empty-state grid gap-3 rounded-2xl border border-dashed border-[var(--color-border)] p-4"
      >
        <h2>Referral profile not active</h2>
        <p>
          Activate your referral profile first. You need to provide your name and tax domicile
          before creating or tracking referral codes.
        </p>
        <RouterLink
          class="primary w-fit rounded-full bg-[var(--color-ink)] px-4 py-2.5 font-bold text-[var(--color-paper)] no-underline"
          to="/referrals/become"
          >Activate Now</RouterLink
        >
      </section>

      <template v-else>
        <section
          class="summary-grid grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-3"
          aria-label="Referral summary cards"
        >
          <article
            class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
          >
            <p class="text-[0.82rem] text-[var(--color-muted)]">Direct registrations</p>
            <strong class="text-2xl">{{ totalStats.direct }}</strong>
          </article>
          <article
            class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
          >
            <p class="text-[0.82rem] text-[var(--color-muted)]">Second-level registrations</p>
            <strong class="text-2xl">{{ totalStats.second }}</strong>
          </article>
          <article
            class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
          >
            <p class="text-[0.82rem] text-[var(--color-muted)]">Active subscriptions</p>
            <strong class="text-2xl">{{ totalStats.active }}</strong>
          </article>
          <article
            class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
          >
            <p class="text-[0.82rem] text-[var(--color-muted)]">Second-level active subs</p>
            <strong class="text-2xl">{{ totalStats.secondActive }}</strong>
          </article>
        </section>

        <div class="codes-toolbar flex flex-wrap justify-between gap-3">
          <div class="code-list flex flex-wrap gap-2" aria-label="Owned referral codes">
            <span
              v-for="code in codes"
              :key="code"
              class="code-pill rounded-full border border-[var(--color-border)] bg-white px-3 py-1.5 font-bold tracking-[0.09em]"
              >{{ code }}</span
            >
          </div>
          <button
            type="button"
            class="primary rounded-full border-0 bg-[var(--color-ink)] px-4 py-2.5 font-bold text-[var(--color-paper)]"
            @click="createCode"
          >
            Create Another Code
          </button>
        </div>

        <p v-if="notice" class="success text-[#245f3d]" role="status">{{ notice }}</p>
        <p v-if="errorMessage" class="error text-[#b0432c]" role="alert">{{ errorMessage }}</p>

        <div class="table-wrap overflow-x-auto rounded-2xl border border-[var(--color-border)]">
          <table class="min-w-[780px] w-full border-collapse" aria-label="Referral metrics">
            <thead>
              <tr>
                <th
                  class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                >
                  Referral code
                </th>
                <th
                  class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                >
                  Registered users
                </th>
                <th
                  class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                >
                  Second-level registrations
                </th>
                <th
                  class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                >
                  Active subscriptions
                </th>
                <th
                  class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                >
                  Second-level active subs
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.code">
                <td
                  class="code-col border-b border-[var(--color-border)] px-3.5 py-3 text-left text-[0.9rem] font-bold tracking-[0.08em]"
                >
                  {{ row.code }}
                </td>
                <td
                  class="border-b border-[var(--color-border)] px-3.5 py-3 text-left text-[0.9rem]"
                >
                  {{ row.directRegistrations }}
                </td>
                <td
                  class="border-b border-[var(--color-border)] px-3.5 py-3 text-left text-[0.9rem]"
                >
                  {{ row.secondLevelRegistrations }}
                </td>
                <td
                  class="border-b border-[var(--color-border)] px-3.5 py-3 text-left text-[0.9rem]"
                >
                  {{ row.activeSubscriptions }}
                </td>
                <td
                  class="border-b border-[var(--color-border)] px-3.5 py-3 text-left text-[0.9rem]"
                >
                  {{ row.secondLevelActiveSubscriptions }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </section>
  </main>
</template>
