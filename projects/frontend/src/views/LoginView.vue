<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const auth = useAuthStore()

const isRegister = ref(false)
const email = ref('')
const displayName = ref('')
const password = ref('')
const formError = ref<string | null>(null)

async function handleSubmit() {
  formError.value = null
  try {
    if (isRegister.value) {
      await auth.register(email.value, displayName.value, password.value)
    } else {
      await auth.login(email.value, password.value)
    }
    router.push('/')
  } catch (e: unknown) {
    formError.value = e instanceof Error ? e.message : 'An error occurred'
  }
}
</script>

<template>
  <div class="login-view container">
    <div class="auth-card">
      <h1>{{ isRegister ? t('auth.registerTitle') : t('auth.loginTitle') }}</h1>

      <form class="auth-form" @submit.prevent="handleSubmit">
        <div v-if="formError" class="error-message" role="alert">
          {{ formError }}
        </div>

        <div class="form-group">
          <label for="email">{{ t('auth.email') }}</label>
          <input
            id="email"
            v-model="email"
            type="email"
            required
            autocomplete="email"
          />
        </div>

        <div v-if="isRegister" class="form-group">
          <label for="displayName">{{ t('auth.displayName') }}</label>
          <input
            id="displayName"
            v-model="displayName"
            type="text"
            required
            autocomplete="name"
          />
        </div>

        <div class="form-group">
          <label for="password">{{ t('auth.password') }}</label>
          <input
            id="password"
            v-model="password"
            type="password"
            required
            minlength="8"
            autocomplete="current-password"
          />
        </div>

        <button type="submit" class="btn btn-primary" :disabled="auth.loading">
          {{ auth.loading ? t('common.loading') : isRegister ? t('auth.registerButton') : t('auth.loginButton') }}
        </button>
      </form>

      <p class="toggle-auth">
        {{ isRegister ? t('auth.haveAccount') : t('auth.noAccount') }}
        <button class="link-btn" @click="isRegister = !isRegister">
          {{ isRegister ? t('auth.loginButton') : t('auth.registerButton') }}
        </button>
      </p>
    </div>
  </div>
</template>

<style scoped>
.login-view {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: calc(100vh - 128px);
  padding: 2rem 1rem;
}

.auth-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 2.5rem;
  width: 100%;
  max-width: 420px;
}

.auth-card h1 {
  margin-bottom: 1.5rem;
  font-size: 1.5rem;
}

.auth-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.form-group label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.form-group input {
  padding: 0.625rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-bg);
  color: var(--color-text);
  font-size: 0.9375rem;
}

.form-group input:focus {
  outline: 2px solid var(--color-primary);
  outline-offset: -1px;
}

.error-message {
  background: rgba(248, 113, 113, 0.1);
  color: var(--color-danger);
  padding: 0.75rem;
  border-radius: var(--radius-sm);
  font-size: 0.875rem;
}

.toggle-auth {
  margin-top: 1.25rem;
  text-align: center;
  font-size: 0.875rem;
  color: var(--color-text-secondary);
}

.link-btn {
  background: none;
  border: none;
  color: var(--color-primary);
  cursor: pointer;
  font-size: inherit;
  text-decoration: underline;
}
</style>
