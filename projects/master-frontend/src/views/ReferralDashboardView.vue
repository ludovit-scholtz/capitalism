<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

import {
  calculateReferralGoldTokens,
  createAdditionalReferralCode,
  getReferralDashboard,
  getReferralProfile,
  syncReferralSubscriptionStatus,
  type ReferralDashboardRow,
} from '@/lib/referrals'
import { useAuthStore } from '@/stores/auth'
import { fetchGameServers, type GameServerSummary } from '@/lib/masterApi'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const rows = ref<ReferralDashboardRow[]>([])
const codes = ref<string[]>([])
const hasReferralProfile = ref(false)
const hasAppliedCode = ref(false)
const notice = ref('')
const errorMessage = ref('')
const gameServers = ref<GameServerSummary[]>([])
const copiedLink = ref<string | null>(null)

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
  const items = [{ label: t('referralDashboard.becomeReferral'), to: '/referrals/become' }]

  if (!hasAppliedCode.value) {
    items.unshift({ label: t('referralDashboard.setupCode'), to: '/referrals/setup' })
  }

  items.unshift({ label: t('home.referralDashboard'), to: '/referrals/dashboard' })

  if (auth.isGameAdmin) {
    items.push({ label: t('nav.gameAdminDashboard'), to: '/game-admin' })
  }

  return items
})

const earnedGoldTokens = computed(() => calculateReferralGoldTokens(rows.value))

/** Returns a shareable referral link for a given code and game server. */
function buildReferralLink(code: string, frontendUrl: string): string {
  const base = frontendUrl.replace(/\/$/, '')
  return `${base}/?ref=${code}`
}

/** Returns the composite key used to track which link has been copied. */
function getCopiedKey(code: string, frontendUrl: string): string {
  return `${code}::${frontendUrl}`
}

/** Copies a referral link to clipboard and shows feedback. */
async function copyLink(code: string, frontendUrl: string) {
  const link = buildReferralLink(code, frontendUrl)
  const key = getCopiedKey(code, frontendUrl)
  const setAndClear = () => {
    copiedLink.value = key
    setTimeout(() => {
      if (copiedLink.value === key) {
        copiedLink.value = null
      }
    }, 2000)
  }
  try {
    await navigator.clipboard.writeText(link)
    setAndClear()
  } catch {
    // Fallback for browsers that block clipboard API
    const el = document.createElement('textarea')
    el.value = link
    el.style.position = 'fixed'
    el.style.opacity = '0'
    document.body.appendChild(el)
    el.select()
    document.execCommand('copy')
    document.body.removeChild(el)
    setAndClear()
  }
}

function isCopied(code: string, frontendUrl: string): boolean {
  return copiedLink.value === getCopiedKey(code, frontendUrl)
}

function reloadDashboard() {
  if (!auth.player?.email) {
    return
  }

  const profile = getReferralProfile(auth.player.email)
  hasReferralProfile.value = !!profile.referralIdentity
  hasAppliedCode.value = !!profile.appliedReferralCode
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

  // Load game servers for shareable links
  try {
    gameServers.value = await fetchGameServers()
  } catch {
    errorMessage.value = t('referralDashboard.serverLoadFailed')
  }
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
        <header class="dash-header grid gap-2">
          <h2 class="text-xl font-semibold">{{ t('referralDashboard.whyTitle') }}</h2>
          <p class="text-sm text-[var(--color-muted)]">{{ t('referralDashboard.whyDiscount') }}</p>
          <p class="text-sm text-[var(--color-muted)]">{{ t('referralDashboard.whyShare') }}</p>
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
                {{ t('referralDashboard.earnedGoldTokens') }}
              </p>
              <strong class="text-2xl">{{ earnedGoldTokens }}</strong>
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

          <!-- Shareable referral links section -->
          <section
            v-if="codes.length > 0 && gameServers.length > 0"
            class="referral-links-section grid gap-3 rounded-2xl border border-[var(--color-border)] bg-[var(--color-paper)] p-4"
            aria-label="Shareable referral links"
          >
            <h3 class="text-base font-semibold">{{ t('referralDashboard.shareLinksTitle') }}</h3>
            <p class="text-sm text-[var(--color-muted)]">{{ t('referralDashboard.shareLinksSubtitle') }}</p>
            <div class="grid gap-3">
              <div v-for="code in codes" :key="code" class="grid gap-2">
                <p class="text-xs font-semibold uppercase tracking-wider text-[var(--color-muted)]">
                  {{ t('referralDashboard.referralCode') }}: <span class="font-mono text-[var(--color-ink)]">{{ code }}</span>
                </p>
                <div v-for="server in gameServers" :key="server.id" class="flex items-center gap-2">
                  <span class="min-w-0 flex-1 truncate rounded-lg border border-[var(--color-border)] bg-[var(--color-paper-strong)] px-3 py-2 font-mono text-xs text-[var(--color-muted)]">
                    {{ buildReferralLink(code, server.frontendUrl) }}
                  </span>
                  <button
                    type="button"
                    class="copy-link-btn shrink-0 rounded-lg border border-[var(--color-border)] px-3 py-2 text-xs font-semibold transition-colors"
                    :class="isCopied(code, server.frontendUrl) ? 'border-green-500/40 bg-green-500/10 text-green-700' : 'bg-white hover:bg-[var(--color-paper-strong)]'"
                    :aria-label="t('referralDashboard.copyLink')"
                    @click="copyLink(code, server.frontendUrl)"
                  >
                    {{ isCopied(code, server.frontendUrl) ? t('referralDashboard.linkCopied') : t('referralDashboard.copyLink') }}
                  </button>
                </div>
              </div>
            </div>
          </section>

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
