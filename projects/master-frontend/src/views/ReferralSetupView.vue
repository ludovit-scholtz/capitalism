<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { applyReferralCode, getReferralProfile, syncReferralSubscriptionStatus } from '@/lib/referrals'
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
  referralCode.value = referralCode.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 8)
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
  <main class="ref-shell">
    <section class="ref-card">
      <p class="eyebrow">Referral Program</p>
      <h1>Setup Referral Code</h1>
      <p class="subtitle">
        Enter the referral code that invited you. You can set this only once and it cannot be
        changed later.
      </p>

      <div class="setup-block">
        <label for="referral-code">Referral code</label>
        <input
          id="referral-code"
          v-model="referralCode"
          type="text"
          maxlength="8"
          placeholder="AB12CD34"
          :disabled="!!appliedCode"
          @input="normalizeCodeInput"
        />
        <p v-if="appliedCode" class="locked-note">Saved code: {{ appliedCode }}</p>
      </div>

      <p v-if="errorMessage" class="error" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="success" role="status">{{ successMessage }}</p>

      <div class="actions">
        <button type="button" class="primary" :disabled="!canSubmit" @click="submitCode">
          Save Referral Code
        </button>
        <RouterLink class="secondary" to="/referrals/dashboard">Open Referral Dashboard</RouterLink>
      </div>
    </section>
  </main>
</template>

<style scoped>
.ref-shell {
  min-height: 100dvh;
  display: grid;
  place-items: center;
  padding: 2rem 1rem;
}

.ref-card {
  width: min(680px, 100%);
  border: 1px solid var(--color-border);
  border-radius: 24px;
  background: var(--color-paper-strong);
  padding: 2rem;
  box-shadow: var(--shadow-soft);
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.12em;
  font-size: 0.72rem;
  color: var(--color-accent-deep);
}

h1 {
  font-size: clamp(1.6rem, 3vw, 2.2rem);
}

.subtitle {
  color: var(--color-muted);
  line-height: 1.6;
}

.setup-block {
  border: 1px dashed var(--color-border);
  border-radius: 18px;
  padding: 1rem;
  display: grid;
  gap: 0.5rem;
}

label {
  font-size: 0.9rem;
  font-weight: 600;
}

input {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 0.8rem 0.9rem;
  background: #fff;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  font-weight: 700;
}

.locked-note {
  color: #245f3d;
  font-size: 0.85rem;
}

.actions {
  display: flex;
  gap: 0.7rem;
  flex-wrap: wrap;
}

.primary,
.secondary {
  border-radius: 999px;
  padding: 0.72rem 1.1rem;
  font-weight: 700;
  text-decoration: none;
  border: none;
}

.primary {
  background: var(--color-ink);
  color: var(--color-paper);
}

.primary:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.secondary {
  background: rgba(17, 41, 79, 0.08);
  color: var(--color-ink);
}

.error {
  color: #b0432c;
}

.success {
  color: #245f3d;
}
</style>
