<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { setLocale } from '@/i18n'
import CountryFlag from '@/components/common/CountryFlag.vue'
import { getLocaleFlagCode } from '@/lib/countryFlags'

const { locale, t } = useI18n()
const supportedLocales = ['en', 'sk', 'de'] as const

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
        v-for="localeCode in supportedLocales"
        :key="localeCode"
        :class="[
          'language-btn inline-flex items-center gap-1.5 border-0 px-2 py-1 text-xs font-semibold transition-colors',
          locale === localeCode
            ? 'active bg-brand text-white hover:bg-brand-hover'
            : 'bg-card-raised text-muted hover:bg-card hover:text-body',
        ]"
        :aria-pressed="locale === localeCode"
        @click="handleLocaleChange(localeCode)"
      >
        <CountryFlag
          v-if="getLocaleFlagCode(localeCode)"
          :country-code="getLocaleFlagCode(localeCode)!"
          size="sm"
          :title="t(`languages.${localeCode}`)"
        />
        {{ t(`languages.${localeCode}`) }}
      </button>
    </div>
  </div>
</template>
