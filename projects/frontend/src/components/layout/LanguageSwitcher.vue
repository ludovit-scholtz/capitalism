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
  <div class="language-switcher flex items-center">
    <div class="language-buttons flex gap-1 overflow-hidden rounded-sm border border-divider" role="group" :aria-label="t('languageSwitcher.label')">
      <button
        v-for="loc in SUPPORTED_LOCALES"
        :key="loc"
        :class="[
          'language-btn min-w-8 border-0 px-2 py-1 text-center text-xs font-semibold transition-colors',
          locale === loc
            ? 'active bg-brand text-white hover:bg-brand-hover'
            : 'bg-card-raised text-muted hover:bg-card hover:text-body',
        ]"
        @click="setLocale(loc)"
        :aria-pressed="locale === loc"
      >
        {{ t(`languages.${loc}`) }}
      </button>
    </div>
  </div>
</template>
