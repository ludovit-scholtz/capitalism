<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { setLocale } from '@/i18n'
import CountryFlag from '@/components/common/CountryFlag.vue'
import { getLocaleFlagCode } from '@/lib/countryFlags'

const { locale, t } = useI18n()
const supportedLocales = ['en', 'sk', 'de'] as const

/** Pairs of [locale, flagCountryCode] for all supported locales. */
const localeFlagEntries = supportedLocales.map((loc) => ({
  loc,
  flagCode: getLocaleFlagCode(loc),
}))

function handleLocaleChange(nextLocale: (typeof supportedLocales)[number]) {
  setLocale(nextLocale)
}
</script>

<template>
  <div class="language-switcher flex items-center">
    <div
      class="language-buttons flex gap-1 overflow-hidden rounded-sm border border-divider"
      role="group"
      :aria-label="t('languageSwitcher.label')"
    >
      <button
        v-for="entry in localeFlagEntries"
        :key="entry.loc"
        :class="[
          'language-btn inline-flex items-center gap-1.5 border-0 px-2 py-1 text-xs font-semibold transition-colors',
          locale === entry.loc
            ? 'active bg-brand text-white hover:bg-brand-hover'
            : 'bg-card-raised text-muted hover:bg-card hover:text-body',
        ]"
        :aria-pressed="locale === entry.loc"
        @click="handleLocaleChange(entry.loc)"
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
