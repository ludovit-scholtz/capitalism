<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { becomeReferral, getReferralProfile, syncReferralSubscriptionStatus } from '@/lib/referrals'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

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
      t('referralBecome.success')
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t('referralBecome.error')
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
        {{ t('home.becomeReferral') }}
      </p>
      <h1 class="text-[clamp(1.7rem,3vw,2.3rem)]">{{ t('referralBecome.title') }}</h1>
      <p class="subtitle leading-[1.65] text-[var(--color-muted)]">
        {{ t('referralBecome.subtitle') }}
      </p>

      <form class="become-form grid gap-3.5" @submit.prevent="submitBecomeReferral">
        <div class="field grid gap-1.5">
          <label class="text-[0.88rem] font-bold" for="full-name">{{ t('referralBecome.name') }}</label>
          <input
            id="full-name"
            v-model="fullName"
            type="text"
            :placeholder="t('referralBecome.namePlaceholder')"
            class="rounded-xl border border-[var(--color-border)] bg-white px-4 py-3"
            required
          />
        </div>

        <div class="field grid gap-1.5">
          <label class="text-[0.88rem] font-bold" for="tax-domicile">{{
            t('referralBecome.domicile')
          }}</label>
          <input
            id="tax-domicile"
            v-model="taxDomicile"
            type="text"
            :placeholder="t('referralBecome.domicilePlaceholder')"
            class="rounded-xl border border-[var(--color-border)] bg-white px-4 py-3"
            required
          />
        </div>

        <button
          type="submit"
          class="primary w-fit rounded-full border-0 bg-[var(--color-ink)] px-5 py-3 font-bold text-[var(--color-paper)]"
        >
          {{ t('referralBecome.activate') }}
        </button>
      </form>

      <p v-if="existingCode" class="generated-code font-bold tracking-[0.08em]">
        {{ t('referralBecome.primaryCode', { code: existingCode }) }}
      </p>
      <p v-if="errorMessage" class="error text-[#b0432c]" role="alert">{{ errorMessage }}</p>
      <p v-if="successMessage" class="success text-[#245f3d]" role="status">{{ successMessage }}</p>

      <RouterLink
        class="secondary w-fit rounded-full bg-[rgba(17,41,79,0.08)] px-5 py-3 font-bold text-[var(--color-ink)] no-underline"
        to="/referrals/dashboard"
        >{{ t('referralBecome.goDashboard') }}</RouterLink
      >
    </section>
  </main>
</template>
