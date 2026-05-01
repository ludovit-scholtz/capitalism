<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

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
const { t } = useI18n()

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

const navItems = computed(() => {
  const items = [
    { label: t('referralDashboard.setupCode'), to: '/referrals/setup' },
    { label: t('referralDashboard.becomeReferral'), to: '/referrals/become' },
    { label: t('common.backToPortal'), to: '/' },
  ]

  if (auth.isGameAdmin) {
    items.unshift({ label: t('nav.gameAdminDashboard'), to: '/game-admin' })
  }

  return items
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
    notice.value = t('referralDashboard.newCode', { code: created.code })
    reloadDashboard()
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t('referralDashboard.createCodeError')
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
  <main>
    <ViewJumbotron
      :kicker="t('home.referralDashboard')"
      :title="t('referralDashboard.title')"
      :subtitle="t('referralDashboard.subtitle')"
      variant="referral"
    />
    <ViewSubnav :items="navItems" aria-label="Referral navigation" />

    <section class="dash-shell min-h-dvh px-4 py-2 pb-12">
      <section
        class="dash-card mx-auto grid w-full gap-4 rounded-3xl border border-[var(--color-border)] bg-[var(--color-paper-strong)] p-6 shadow-[var(--shadow-soft)]"
      >
        <header class="dash-header flex flex-wrap justify-between gap-4">
          <div class="header-actions flex gap-2 self-start">
            <RouterLink
              class="ghost rounded-full bg-[rgba(17,41,79,0.08)] px-4 py-2.5 font-bold text-[var(--color-ink)] no-underline"
              to="/referrals/setup"
              >{{ t('referralDashboard.setupCode') }}</RouterLink
            >
            <RouterLink
              class="ghost rounded-full bg-[rgba(17,41,79,0.08)] px-4 py-2.5 font-bold text-[var(--color-ink)] no-underline"
              to="/referrals/become"
              >{{ t('referralDashboard.becomeReferral') }}</RouterLink
            >
          </div>
          <nav
            v-if="auth.isGameAdmin"
            class="flex flex-wrap gap-2 rounded-2xl border border-[var(--color-border)] bg-[var(--color-paper)] p-3"
            aria-label="Referral dashboard quick navigation"
          >
            <RouterLink
              class="rounded-full bg-[rgba(17,41,79,0.08)] px-3 py-1.5 text-sm font-semibold text-[var(--color-ink)] no-underline"
              to="/ranking/admin"
            >
              {{ t('home.rankingAdmin') }}
            </RouterLink>
            <RouterLink
              class="rounded-full bg-[rgba(17,41,79,0.08)] px-3 py-1.5 text-sm font-semibold text-[var(--color-ink)] no-underline"
              to="/gold-admin"
            >
              {{ t('home.goldAdmin') }}
            </RouterLink>
          </nav>
        </header>

        <section
          v-if="!hasReferralProfile"
          class="empty-state grid gap-3 rounded-2xl border border-dashed border-[var(--color-border)] p-4"
        >
          <h2>{{ t('referralDashboard.profileNotActive') }}</h2>
          <p>
            {{ t('referralDashboard.profileNotActiveText') }}
          </p>
          <RouterLink
            class="primary w-fit rounded-full bg-[var(--color-ink)] px-4 py-2.5 font-bold text-[var(--color-paper)] no-underline"
            to="/referrals/become"
            >{{ t('referralDashboard.activateNow') }}</RouterLink
          >
        </section>

        <template v-else>
          <section
            class="summary-grid grid grid-cols-[repeat(auto-fit,minmax(180px,1fr))] gap-3"
            :aria-label="t('referralDashboard.metricsAria')"
          >
            <article
              class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
            >
              <p class="text-[0.82rem] text-[var(--color-muted)]">
                {{ t('referralDashboard.directRegistrations') }}
              </p>
              <strong class="text-2xl">{{ totalStats.direct }}</strong>
            </article>
            <article
              class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
            >
              <p class="text-[0.82rem] text-[var(--color-muted)]">
                {{ t('referralDashboard.secondRegistrations') }}
              </p>
              <strong class="text-2xl">{{ totalStats.second }}</strong>
            </article>
            <article
              class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
            >
              <p class="text-[0.82rem] text-[var(--color-muted)]">
                {{ t('referralDashboard.activeSubscriptions') }}
              </p>
              <strong class="text-2xl">{{ totalStats.active }}</strong>
            </article>
            <article
              class="summary-card grid gap-1.5 rounded-2xl border border-[var(--color-border)] bg-white p-4"
            >
              <p class="text-[0.82rem] text-[var(--color-muted)]">
                {{ t('referralDashboard.secondActiveSubs') }}
              </p>
              <strong class="text-2xl">{{ totalStats.secondActive }}</strong>
            </article>
          </section>

          <div class="codes-toolbar flex flex-wrap justify-between gap-3">
            <div
              class="code-list flex flex-wrap gap-2"
              :aria-label="t('referralDashboard.referralCode')"
            >
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
              {{ t('referralDashboard.createAnotherCode') }}
            </button>
          </div>

          <p v-if="notice" class="success text-[#245f3d]" role="status">{{ notice }}</p>
          <p v-if="errorMessage" class="error text-[#b0432c]" role="alert">{{ errorMessage }}</p>

          <div class="table-wrap overflow-x-auto rounded-2xl border border-[var(--color-border)]">
            <table
              class="min-w-[780px] w-full border-collapse"
              :aria-label="t('referralDashboard.metricsAria')"
            >
              <thead>
                <tr>
                  <th
                    class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                  >
                    {{ t('referralDashboard.referralCode') }}
                  </th>
                  <th
                    class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                  >
                    {{ t('referralDashboard.registeredUsers') }}
                  </th>
                  <th
                    class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                  >
                    {{ t('referralDashboard.secondRegistrations') }}
                  </th>
                  <th
                    class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                  >
                    {{ t('referralDashboard.activeSubscriptions') }}
                  </th>
                  <th
                    class="border-b border-[var(--color-border)] bg-[rgba(17,41,79,0.06)] px-3.5 py-3 text-left text-[0.78rem] uppercase tracking-[0.08em]"
                  >
                    {{ t('referralDashboard.secondActiveSubs') }}
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
                <tr v-if="rows.length === 0">
                  <td
                    colspan="5"
                    class="border-b border-[var(--color-border)] px-3.5 py-3 text-center text-[0.9rem] text-[var(--color-muted)]"
                  >
                    {{ t('common.noData') }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>
      </section>
    </section>
  </main>
</template>
