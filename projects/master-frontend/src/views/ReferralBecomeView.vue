<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { becomeReferral, getReferralProfile, syncReferralSubscriptionStatus } from '@/lib/referrals'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()

const fullName = ref('')
const taxDomicile = ref('')
const existingCode = ref<string | null>(null)
const successMessage = ref('')
const errorMessage = ref('')

function submitBecomeReferral() {
  if (!auth.player?.email) {
    return
  }

  errorMessage.value = ''
  successMessage.value = ''

  try {
    becomeReferral(auth.player.email, fullName.value, taxDomicile.value)
    const profile = getReferralProfile(auth.player.email)
    existingCode.value = profile.referralCodes[0]?.code ?? null
    successMessage.value =
      'Referral profile is active. Your first code was generated automatically, and you can create more in the dashboard.'
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : 'Unable to activate referral profile.'
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
    const profile = getReferralProfile(auth.player.email)
    if (profile.referralIdentity) {
      fullName.value = profile.referralIdentity.fullName
      taxDomicile.value = profile.referralIdentity.taxDomicile
      existingCode.value = profile.referralCodes[0]?.code ?? null
    }
  }
})
</script>

<template>
  <main class="become-shell grid min-h-dvh place-items-center px-4 py-8">
    <section
      class="become-card grid w-full max-w-[720px] gap-4 rounded-3xl border border-[var(--color-border)] bg-[var(--color-paper-strong)] p-8 shadow-[var(--shadow-soft)]"
    >
      <p class="eyebrow text-[0.72rem] uppercase tracking-[0.12em] text-[var(--color-accent-deep)]">
        Referral Program
      </p>
      <h1 class="text-[clamp(1.7rem,3vw,2.3rem)]">Become a Referral Partner</h1>
      <p class="subtitle leading-[1.65] text-[var(--color-muted)]">
        To become a referral partner, fill in your legal name and tax domicile. After activation,
        your first 8-character code is generated automatically.
      </p>

      <form class="become-form grid gap-3.5" @submit.prevent="submitBecomeReferral">
        <div class="field grid gap-1.5">
          <label class="text-[0.88rem] font-bold" for="full-name">Name</label>
          <input
            id="full-name"
            v-model="fullName"
            type="text"
            placeholder="Ludovit Scholtz"
            class="rounded-xl border border-[var(--color-border)] bg-white px-4 py-3"
            required
          />
        </div>

        <div class="field grid gap-1.5">
          <label class="text-[0.88rem] font-bold" for="tax-domicile">Tax domicile</label>
          <input
            id="tax-domicile"
            v-model="taxDomicile"
            type="text"
            placeholder="Slovakia"
            class="rounded-xl border border-[var(--color-border)] bg-white px-4 py-3"
            required
          />
        </div>

        <button
          type="submit"
          class="primary w-fit rounded-full border-0 bg-[var(--color-ink)] px-5 py-3 font-bold text-[var(--color-paper)]"
        >
          Activate Referral Profile
        </button>
      </form>

      <p v-if="existingCode" class="generated-code font-bold tracking-[0.08em]">
        Your primary code: {{ existingCode }}
      </p>
      <p v-if="errorMessage" class="error text-[#b0432c]" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="success text-[#245f3d]" role="status">{{ successMessage }}</p>

      <RouterLink
        class="secondary w-fit rounded-full bg-[rgba(17,41,79,0.08)] px-5 py-3 font-bold text-[var(--color-ink)] no-underline"
        to="/referrals/dashboard"
        >Go to Referral Dashboard</RouterLink
      >
    </section>
  </main>
</template>
