/**
 * Country flag SVG helpers.
 *
 * Imports a curated set of flag SVG strings from the `country-flag-icons`
 * package and exposes a map and a lookup function for use in Vue components.
 *
 * Flags are inline SVGs so they render without any additional HTTP requests
 * and work reliably across all platforms (including Windows where emoji flag
 * support is limited).
 */
import {
  AT,
  AE,
  BR,
  CN,
  CZ,
  DE,
  FR,
  GB,
  IN,
  JP,
  PL,
  RU,
  SK,
  SG,
  US,
} from 'country-flag-icons/string/3x2'

/** Pre-loaded SVG strings keyed by ISO 3166-1 alpha-2 country code (upper-case). */
export const FLAG_SVG_MAP: Record<string, string> = {
  AT,
  AE,
  BR,
  CN,
  CZ,
  DE,
  FR,
  GB,
  IN,
  JP,
  PL,
  RU,
  SK,
  SG,
  US,
}

/**
 * Returns the SVG markup string for the given ISO 3166-1 alpha-2 country code,
 * or `null` if no flag is available for the code.
 */
export function getFlagSvg(countryCode: string): string | null {
  return FLAG_SVG_MAP[countryCode.toUpperCase()] ?? null
}

/**
 * Maps locale codes to the ISO 3166-1 alpha-2 country code used for the flag
 * representing that language in the language switcher.
 */
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
