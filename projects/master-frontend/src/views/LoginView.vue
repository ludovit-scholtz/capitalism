<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const mode = ref<'login' | 'register'>('login')
const email = ref('')
const displayName = ref('')
const password = ref('')
const formError = ref('')

async function submit() {
  formError.value = ''
  try {
    if (mode.value === 'register') {
      await auth.register(email.value, displayName.value, password.value)
    } else {
      await auth.login(email.value, password.value)
    }
    await auth.fetchSubscription()
    await router.push('/')
  } catch (e: unknown) {
    formError.value = e instanceof Error ? e.message : t('login.genericError')
  }
}
</script>

<template>
  <main class="login-shell flex min-h-dvh items-center justify-center px-4 py-8">
    <div
      class="login-card w-full max-w-[440px] rounded-[32px] border border-[var(--color-border)] bg-[rgba(255,251,243,0.92)] p-10 shadow-[var(--shadow-soft)]"
    >
      <div class="login-brand mb-7">
        <p class="eyebrow text-[0.72rem] uppercase tracking-[0.14em] text-[var(--color-accent)]">
          {{ t('home.eyebrow') }}
        </p>
        <h1 class="mt-1.5 text-[2rem]">
          {{ mode === 'login' ? t('login.signIn') : t('login.createAccount') }}
        </h1>
        <p class="login-sub mt-2 text-[0.95rem] text-[var(--color-muted)]">
          {{
            mode === 'login'
              ? t('login.signInSub')
              : t('login.createSub')
          }}
        </p>
      </div>

      <form class="login-form flex flex-col gap-4" @submit.prevent="submit">
        <div class="field-group flex flex-col gap-1.5">
          <label for="email" class="text-sm font-medium text-[var(--color-ink)]">{{
            t('login.email')
          }}</label>
          <input
            id="email"
            v-model="email"
            type="email"
            autocomplete="email"
            placeholder="you@example.com"
            class="rounded-[14px] border border-[var(--color-border)] bg-[var(--color-paper-strong)] px-4 py-3 text-[var(--color-ink)] outline-none transition-colors focus:border-[var(--color-accent)]"
            required
          />
        </div>

        <div v-if="mode === 'register'" class="field-group flex flex-col gap-1.5">
          <label for="displayName" class="text-sm font-medium text-[var(--color-ink)]">{{
            t('login.displayName')
          }}</label>
          <input
            id="displayName"
            v-model="displayName"
            type="text"
            autocomplete="name"
            :placeholder="t('login.displayNamePlaceholder')"
            class="rounded-[14px] border border-[var(--color-border)] bg-[var(--color-paper-strong)] px-4 py-3 text-[var(--color-ink)] outline-none transition-colors focus:border-[var(--color-accent)]"
            required
          />
        </div>

        <div class="field-group flex flex-col gap-1.5">
          <label for="password" class="text-sm font-medium text-[var(--color-ink)]">{{
            t('login.password')
          }}</label>
          <input
            id="password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            placeholder="••••••••"
            class="rounded-[14px] border border-[var(--color-border)] bg-[var(--color-paper-strong)] px-4 py-3 text-[var(--color-ink)] outline-none transition-colors focus:border-[var(--color-accent)]"
            required
          />
        </div>

        <p
          v-if="formError"
          class="form-error rounded-[14px] bg-[rgba(176,67,44,0.08)] px-4 py-3 text-[0.9rem] text-[#a03826]"
          role="alert"
        >
          {{ formError }}
        </p>

        <button
          class="submit-btn mt-1 rounded-full bg-[var(--color-ink)] px-5 py-3 text-base font-bold text-[var(--color-paper)] transition duration-150 hover:-translate-y-px disabled:cursor-not-allowed disabled:opacity-50"
          type="submit"
          :disabled="auth.loading"
        >
          {{
            auth.loading
              ? t('login.wait')
              : mode === 'login'
                ? t('login.signIn')
                : t('login.createAccount')
          }}
        </button>
      </form>

      <p class="toggle-mode mt-5 text-center text-[0.9rem] text-[var(--color-muted)]">
        <span v-if="mode === 'login'">
          {{ t('login.noAccount') }}
          <button
            class="link-btn border-0 bg-transparent font-bold text-[var(--color-ink)] underline"
            type="button"
            @click="mode = 'register'"
          >
            {{ t('login.register') }}
          </button>
        </span>
        <span v-else>
          {{ t('login.haveAccount') }}
          <button
            class="link-btn border-0 bg-transparent font-bold text-[var(--color-ink)] underline"
            type="button"
            @click="mode = 'login'"
          >
            {{ t('login.signIn') }}
          </button>
        </span>
      </p>

      <p class="back-link mt-3 text-center text-[0.87rem] text-[var(--color-muted)]">
        <a class="transition-colors hover:text-[var(--color-ink)]" href="/">← {{
          t('login.backToDirectory')
        }}</a>
      </p>
    </div>
  </main>
</template>
