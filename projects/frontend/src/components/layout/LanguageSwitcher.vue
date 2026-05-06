<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { SUPPORTED_LOCALES, persistLocale, type SupportedLocale } from '@/i18n'
import CountryFlag from '@/components/common/CountryFlag.vue'
import { getLocaleFlagCode } from '@/lib/countryFlags'

const { locale, t } = useI18n()

function setLocale(newLocale: SupportedLocale) {
  locale.value = newLocale
  persistLocale(newLocale)
  document.documentElement.lang = newLocale
}

/** Pairs of [locale, flagCountryCode] only for locales that have a flag mapping. */
const localeFlagEntries = SUPPORTED_LOCALES.map((loc) => ({
  loc,
  flagCode: getLocaleFlagCode(loc),
}))
</script>

<template>
  <div class="language-switcher flex items-center">
    <div class="language-buttons flex gap-1 overflow-hidden rounded-sm border border-divider" role="group" :aria-label="t('languageSwitcher.label')">
      <button
        v-for="entry in localeFlagEntries"
        :key="entry.loc"
        :class="[
          'language-btn inline-flex items-center gap-1.5 border-0 px-2 py-1 text-xs font-semibold transition-colors',
          locale === entry.loc ? 'active bg-brand text-white hover:bg-brand-hover' : 'bg-card-raised text-muted hover:bg-card hover:text-body',
        ]"
        @click="setLocale(entry.loc)"
        :aria-pressed="locale === entry.loc"
      >
        <CountryFlag
          v-if="entry.flagCode"
          :country-code="entry.flagCode"
          size="sm"
          :title="t(`languages.${entry.loc}`)"
        />
        {{ t(`languages.${entry.loc}`) }}
      </button>
    </div>
  </div>
</template>
