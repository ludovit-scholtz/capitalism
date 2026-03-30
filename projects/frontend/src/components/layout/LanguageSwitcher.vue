<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import {
  SUPPORTED_LOCALES,
  persistLocale,
  type SupportedLocale,
} from '@/i18n'

const { locale, t } = useI18n()

function setLocale(newLocale: SupportedLocale) {
  locale.value = newLocale
  persistLocale(newLocale)
  document.documentElement.lang = newLocale
}
</script>

<template>
  <div class="language-switcher">
    <div class="language-buttons" role="group" :aria-label="t('languageSwitcher.label')">
      <button
        v-for="loc in SUPPORTED_LOCALES"
        :key="loc"
        :class="['language-btn', { active: locale === loc }]"
        @click="setLocale(loc)"
        :aria-pressed="locale === loc"
      >
        {{ t(`languages.${loc}`) }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.language-switcher {
  display: flex;
  align-items: center;
}

.language-buttons {
  display: flex;
  gap: 0.25rem;
  border-radius: var(--radius-sm);
  overflow: hidden;
  border: 1px solid var(--color-border);
}

.language-btn {
  background: var(--color-surface-raised);
  color: var(--color-text-secondary);
  border: none;
  padding: 0.35rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
  min-width: 2rem;
  text-align: center;
}

.language-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text);
}

.language-btn.active {
  background: var(--color-primary);
  color: white;
}

.language-btn.active:hover {
  background: var(--color-primary-hover);
}
</style>
