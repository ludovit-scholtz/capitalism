<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth'
import { setLocale } from '@/i18n'

const auth = useAuthStore()
const { t, locale } = useI18n()
const selectedLocale = computed({
  get: () => locale.value,
  set: (value: string) => {
    if (value === 'en' || value === 'sk' || value === 'de') {
      setLocale(value)
    }
  },
})

// Synchronously restore token from localStorage so child views see auth state immediately
auth.initFromStorage()

onMounted(() => {
  if (auth.isAuthenticated) {
    void auth.fetchProfile()
    void auth.fetchSubscription()
  }
})
</script>

<template>
  <div class="language-switcher">
    <label for="master-language">{{ t('app.languageLabel') }}</label>
    <select id="master-language" v-model="selectedLocale" aria-label="Language">
      <option value="en">English</option>
      <option value="sk">Slovensky</option>
      <option value="de">Deutsch</option>
    </select>
  </div>
  <RouterView />
</template>

<style scoped>
.language-switcher {
  position: fixed;
  top: 0.75rem;
  right: 0.75rem;
  z-index: 1000;
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.35rem 0.5rem;
  border-radius: 999px;
  border: 1px solid rgba(0, 0, 0, 0.2);
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(4px);
  font-size: 0.78rem;
}

.language-switcher select {
  border: 1px solid rgba(0, 0, 0, 0.25);
  border-radius: 999px;
  padding: 0.1rem 0.45rem;
  background: white;
}
</style>
