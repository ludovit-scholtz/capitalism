<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import ViewJumbotron from '@/components/layout/ViewJumbotron.vue'
import ViewSubnav from '@/components/layout/ViewSubnav.vue'

import {
  applyReferralCode,
  getReferralProfile,
  syncReferralSubscriptionStatus,
} from '@/lib/referrals'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const referralCode = ref('')
const errorMessage = ref('')
const successMessage = ref('')
const appliedCode = ref<string | null>(null)

const canSubmit = computed(() => !!referralCode.value.trim() && !appliedCode.value)
const navItems = computed(() => {
  const items = [
    { label: t('home.referralDashboard'), to: '/referrals/dashboard' },
    { label: t('home.becomeReferral'), to: '/referrals/become' },
  ]

  if (!appliedCode.value) {
    items.unshift({ label: t('home.referralSetup'), to: '/referrals/setup' })
  }

  return items
})

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
    successMessage.value = t('referralSetup.success')
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : t('referralSetup.saveError')
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
  <main>
    <ViewJumbotron
      :kicker="t('home.referralSetup')"
      :title="t('referralSetup.title')"
      :subtitle="t('referralSetup.subtitle')"
      variant="referral"
    />
    <ViewSubnav :items="navItems" aria-label="Referral setup navigation" />

    <section class="ref-shell grid min-h-dvh place-items-center px-4 py-8">
      <section
        class="ref-card flex w-full max-w-[720px] flex-col gap-5 rounded-3xl border border-[var(--color-border)] bg-[var(--color-paper-strong)] p-8 shadow-[var(--shadow-soft)]"
      >
        <section
          class="grid gap-2 rounded-2xl border border-[var(--color-border)] bg-white text-black p-4"
        >
          <h2 class="text-lg font-semibold">{{ t('referralSetup.infoTitle') }}</h2>
          <p class="text-sm text-[var(--color-muted)]">{{ t('referralSetup.infoOneTime') }}</p>
          <p class="text-sm text-[var(--color-muted)]">
            {{ t('referralSetup.infoPromoDiscount') }}
          </p>
        </section>

        <div
          class="setup-block grid gap-2 rounded-[18px] border border-dashed border-[var(--color-border)] p-4"
        >
          <label class="text-[0.9rem] font-semibold" for="referral-code">{{
            t('referralSetup.codeLabel')
          }}</label>
          <input
            id="referral-code"
            v-model="referralCode"
            type="text"
            maxlength="8"
            :placeholder="t('referralSetup.codePlaceholder')"
            :disabled="!!appliedCode"
            class="rounded-xl border border-[var(--color-border)] bg-white text-black px-4 py-3 font-bold uppercase tracking-[0.12em]"
            @input="normalizeCodeInput"
          />
          <p v-if="appliedCode" class="locked-note text-[0.85rem] text-[#245f3d]">
            {{ t('referralSetup.savedCode', { code: appliedCode }) }}
          </p>
        </div>

        <p v-if="errorMessage" class="error text-[#b0432c]" role="alert">{{ errorMessage }}</p>
        <p v-if="successMessage" class="success text-[#245f3d]" role="status">
          {{ successMessage }}
        </p>

        <div class="actions flex flex-wrap gap-3">
          <button
            type="button"
            class="primary rounded-full border-0 bg-[var(--color-ink)] px-4 py-3 font-bold text-[var(--color-paper)] disabled:cursor-not-allowed disabled:opacity-55"
            :disabled="!canSubmit"
            @click="submitCode"
          >
            {{ t('referralSetup.saveButton') }}
          </button>
          <RouterLink
            class="secondary rounded-full bg-[rgba(17,41,79,0.08)] px-4 py-3 font-bold text-[var(--color-ink)] no-underline"
            to="/referrals/dashboard"
            >{{ t('referralSetup.openDashboard') }}</RouterLink
          >
        </div>
      </section>
    </section>
  </main>
</template>
