<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  applyReferralCode,
  getReferralProfile,
  syncReferralSubscriptionStatus,
} from '@/lib/referrals'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()

const referralCode = ref('')
const errorMessage = ref('')
const successMessage = ref('')
const appliedCode = ref<string | null>(null)

const canSubmit = computed(() => !!referralCode.value.trim() && !appliedCode.value)

function loadProfile() {
  if (!auth.player?.email) {
    return
  }

  const profile = getReferralProfile(auth.player.email)
  appliedCode.value = profile.appliedReferralCode
}

function normalizeCodeInput() {
  referralCode.value = referralCode.value
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, '')
    .slice(0, 8)
}

function submitCode() {
  if (!auth.player?.email) {
    return
  }

  errorMessage.value = ''
  successMessage.value = ''

  try {
    const result = applyReferralCode(auth.player.email, referralCode.value)
    appliedCode.value = result.appliedReferralCode
    successMessage.value = 'Referral code saved. This selection is now locked for your account.'
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Unable to save referral code.'
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

  loadProfile()
})
</script>

<template>
  <main class="ref-shell grid min-h-dvh place-items-center px-4 py-8">
    <section class="ref-card flex w-full max-w-[680px] flex-col gap-4 rounded-3xl border border-[var(--color-border)] bg-[var(--color-paper-strong)] p-8 shadow-[var(--shadow-soft)]">
      <p class="eyebrow text-[0.72rem] uppercase tracking-[0.12em] text-[var(--color-accent-deep)]">Referral Program</p>
      <h1 class="text-[clamp(1.6rem,3vw,2.2rem)]">Setup Referral Code</h1>
      <p class="subtitle leading-[1.6] text-[var(--color-muted)]">
        Enter the referral code that invited you. You can set this only once and it cannot be
        changed later.
      </p>

      <div class="setup-block grid gap-2 rounded-[18px] border border-dashed border-[var(--color-border)] p-4">
        <label class="text-[0.9rem] font-semibold" for="referral-code">Referral code</label>
        <input
          id="referral-code"
          v-model="referralCode"
          type="text"
          maxlength="8"
          placeholder="AB12CD34"
          :disabled="!!appliedCode"
          class="rounded-xl border border-[var(--color-border)] bg-white px-4 py-3 font-bold uppercase tracking-[0.12em]"
          @input="normalizeCodeInput"
        />
        <p v-if="appliedCode" class="locked-note text-[0.85rem] text-[#245f3d]">Saved code: {{ appliedCode }}</p>
      </div>

      <p v-if="errorMessage" class="error text-[#b0432c]" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="success text-[#245f3d]" role="status">{{ successMessage }}</p>

      <div class="actions flex flex-wrap gap-3">
        <button type="button" class="primary rounded-full border-0 bg-[var(--color-ink)] px-4 py-3 font-bold text-[var(--color-paper)] disabled:cursor-not-allowed disabled:opacity-55" :disabled="!canSubmit" @click="submitCode">
          Save Referral Code
        </button>
        <RouterLink class="secondary rounded-full bg-[rgba(17,41,79,0.08)] px-4 py-3 font-bold text-[var(--color-ink)] no-underline" to="/referrals/dashboard">Open Referral Dashboard</RouterLink>
      </div>
    </section>
  </main>
</template>
