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
    errorMessage.value = error instanceof Error ? error.message : 'Unable to activate referral profile.'
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
  <main class="become-shell">
    <section class="become-card">
      <p class="eyebrow">Referral Program</p>
      <h1>Become a Referral Partner</h1>
      <p class="subtitle">
        To become a referral partner, fill in your legal name and tax domicile. After activation,
        your first 8-character code is generated automatically.
      </p>

      <form class="become-form" @submit.prevent="submitBecomeReferral">
        <div class="field">
          <label for="full-name">Name</label>
          <input id="full-name" v-model="fullName" type="text" placeholder="Ludovit Scholtz" required />
        </div>

        <div class="field">
          <label for="tax-domicile">Tax domicile</label>
          <input id="tax-domicile" v-model="taxDomicile" type="text" placeholder="Slovakia" required />
        </div>

        <button type="submit" class="primary">Activate Referral Profile</button>
      </form>

      <p v-if="existingCode" class="generated-code">Your primary code: {{ existingCode }}</p>
      <p v-if="errorMessage" class="error" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="success" role="status">{{ successMessage }}</p>

      <RouterLink class="secondary" to="/referrals/dashboard">Go to Referral Dashboard</RouterLink>
    </section>
  </main>
</template>

<style scoped>
.become-shell {
  min-height: 100dvh;
  display: grid;
  place-items: center;
  padding: 2rem 1rem;
}

.become-card {
  width: min(720px, 100%);
  border: 1px solid var(--color-border);
  border-radius: 24px;
  background: var(--color-paper-strong);
  padding: 2rem;
  box-shadow: var(--shadow-soft);
  display: grid;
  gap: 1rem;
}

.eyebrow {
  text-transform: uppercase;
  letter-spacing: 0.12em;
  font-size: 0.72rem;
  color: var(--color-accent-deep);
}

h1 {
  font-size: clamp(1.7rem, 3vw, 2.3rem);
}

.subtitle {
  color: var(--color-muted);
  line-height: 1.65;
}

.become-form {
  display: grid;
  gap: 0.9rem;
}

.field {
  display: grid;
  gap: 0.4rem;
}

label {
  font-size: 0.88rem;
  font-weight: 700;
}

input {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 0.8rem 0.9rem;
  background: #fff;
}

.primary,
.secondary {
  border-radius: 999px;
  padding: 0.78rem 1.2rem;
  text-decoration: none;
  border: none;
  width: fit-content;
  font-weight: 700;
}

.primary {
  background: var(--color-ink);
  color: var(--color-paper);
}

.secondary {
  background: rgba(17, 41, 79, 0.08);
  color: var(--color-ink);
}

.generated-code {
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
