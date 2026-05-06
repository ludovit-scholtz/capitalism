/**
 * Country flag SVG helpers for the master frontend.
 *
 * Imports flag SVG strings from the `country-flag-icons` package and exposes
 * a lookup function for use in Vue components (e.g. LanguageSwitcher).
 *
 * Pre-loaded country codes:
 * - Active seeded cities:  SK, CZ, AT
 * - Language-switcher:     GB (English), SK (Slovak), DE (German), FR (French)
 * - Anticipated future cities referenced in the roadmap: CN, DE, IN, PL, US
 */
import {
  AT,
  CN,
  CZ,
  DE,
  FR,
  GB,
  IN,
  PL,
  SK,
  US,
} from 'country-flag-icons/string/3x2'

/** Pre-loaded SVG strings keyed by ISO 3166-1 alpha-2 country code. */
export const FLAG_SVG_MAP: Record<string, string> = {
  AT,
  CN,
  CZ,
  DE,
  FR,
  GB,
  IN,
  PL,
  SK,
  US,
}

/** Returns SVG markup for the given country code, or null if unavailable. */
export function getFlagSvg(countryCode: string): string | null {
  return FLAG_SVG_MAP[countryCode.toUpperCase()] ?? null
}

/** Maps locale codes to ISO 3166-1 alpha-2 country code for flag display. */
export const LOCALE_FLAG_MAP: Record<string, string> = {
  en: 'GB',
  sk: 'SK',
  de: 'DE',
  fr: 'FR',
}

/** Returns the country code used as the flag for a given locale, or null. */
export function getLocaleFlagCode(locale: string): string | null {
  return LOCALE_FLAG_MAP[locale] ?? null
}
