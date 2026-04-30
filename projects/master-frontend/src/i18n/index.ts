import { createI18n } from 'vue-i18n'
import en from './locales/en'
import sk from './locales/sk'
import de from './locales/de'

const LOCALE_KEY = 'master_locale'

function detectLocale(): 'en' | 'sk' | 'de' {
  const stored = localStorage.getItem(LOCALE_KEY)
  if (stored === 'en' || stored === 'sk' || stored === 'de') {
    return stored
  }

  const preferred = navigator.languages?.[0] ?? navigator.language ?? 'en'
  const normalized = preferred.toLowerCase()
  if (normalized.startsWith('sk')) return 'sk'
  if (normalized.startsWith('de')) return 'de'
  return 'en'
}

export const i18n = createI18n({
  legacy: false,
  locale: detectLocale(),
  fallbackLocale: 'en',
  messages: {
    en,
    sk,
    de,
  },
})

export function setLocale(locale: 'en' | 'sk' | 'de') {
  i18n.global.locale.value = locale
  localStorage.setItem(LOCALE_KEY, locale)
  document.documentElement.lang = locale
}

document.documentElement.lang = i18n.global.locale.value
