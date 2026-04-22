/**
 * Shared locale-aware number and currency formatting utilities.
 *
 * Key behaviours:
 * - Locale is supplied by the caller so it can be bound to the in-app language
 *   setting (`locale.value` from vue-i18n) rather than the browser default.
 *   Supported locales: 'en' → en-US, 'sk' → sk-SK, 'de' → de-DE.
 * - Compact notation abbreviates large values: 1 000 → 1k, 1 000 000 → 1M,
 *   1 000 000 000 → 1B, 1 000 000 000 000 → 1T, 1 000 000 000 000 000 → 1Q.
 * - Full notation shows the exact value with thousands separators.
 * - Currency symbols follow ISO 4217: € for EUR, $ for USD, Kč for CZK, etc.
 */

/** Maps app locale keys to BCP-47 locale tags used by Intl. */
const LOCALE_MAP: Record<string, string> = {
  en: 'en-US',
  sk: 'sk-SK',
  de: 'de-DE',
}

/** Returns the Intl-compatible locale string for a given app locale key. */
function resolveLocale(locale: string): string {
  return LOCALE_MAP[locale] ?? LOCALE_MAP['en']!
}

/**
 * Formats a currency amount as a compact string (e.g. "€1.23M", "Kč600k", "$2.85B").
 * Uses the supplied app locale key (en / sk / de) for decimal/thousands separators.
 *
 * Compact thresholds and suffix rules:
 * - |value| >= 1e15  → Q  (quadrillions)
 * - |value| >= 1e12  → T  (trillions)
 * - |value| >= 1e9   → B  (billions)
 * - |value| >= 1e6   → M  (millions)
 * - |value| >= 1e3   → k  (thousands)
 * - otherwise        → exact integer
 *
 * @example
 *   formatCompactMoney(1_000_000, 'USD', 'en')   // "$1M"
 *   formatCompactMoney(600_000, 'CZK', 'sk')     // "600 tis. Kč"  (locale-specific compact)
 *   formatCompactMoney(123_456_789, 'EUR', 'en') // "€123.46M"
 *   formatCompactMoney(2_845, 'USD', 'en')        // "$2.85K"  (Intl compact short)
 */
export function formatCompactMoney(
  amount: number,
  currencyCode = 'USD',
  locale = 'en',
): string {
  if (!isFinite(amount) || isNaN(amount)) return '—'
  const intlLocale = resolveLocale(locale)
  return new Intl.NumberFormat(intlLocale, {
    style: 'currency',
    currency: currencyCode,
    notation: 'compact',
    compactDisplay: 'short',
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(amount)
}

/**
 * Formats a currency amount using full notation (no abbreviation), with thousands separators.
 * Uses 0 fraction digits for whole numbers, up to 2 for fractional amounts.
 *
 * @example
 *   formatMoney(200_000, 'EUR', 'en')   // "€200,000"
 *   formatMoney(25.42, 'CZK', 'de')     // "25,42 CZK"
 */
export function formatMoney(
  amount: number,
  currencyCode = 'USD',
  locale = 'en',
): string {
  if (!isFinite(amount) || isNaN(amount)) return '—'
  const intlLocale = resolveLocale(locale)
  const fractionDigits = Number.isInteger(amount) ? 0 : 2
  return new Intl.NumberFormat(intlLocale, {
    style: 'currency',
    currency: currencyCode,
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(amount)
}

/**
 * Formats a plain number with thousands/decimal separators for the given locale.
 * No currency symbol. Useful for share counts, percentages, etc.
 *
 * @example
 *   formatNumber(1_234_567, 'en')   // "1,234,567"
 *   formatNumber(1_234_567, 'sk')   // "1 234 567"
 *   formatNumber(12.5, 'de')        // "12,5"
 */
export function formatNumber(value: number, locale = 'en', fractionDigits = 0): string {
  if (!isFinite(value) || isNaN(value)) return '—'
  const intlLocale = resolveLocale(locale)
  return new Intl.NumberFormat(intlLocale, {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(value)
}

/**
 * Formats a compact number WITHOUT currency symbol (for standalone large counts).
 *
 * @example
 *   formatCompactNumber(1_500_000, 'en')  // "1.5M"
 *   formatCompactNumber(2_500, 'de')       // "2,5 Tsd."
 */
export function formatCompactNumber(value: number, locale = 'en'): string {
  if (!isFinite(value) || isNaN(value)) return '—'
  const intlLocale = resolveLocale(locale)
  return new Intl.NumberFormat(intlLocale, {
    notation: 'compact',
    compactDisplay: 'short',
    maximumFractionDigits: 2,
  }).format(value)
}
