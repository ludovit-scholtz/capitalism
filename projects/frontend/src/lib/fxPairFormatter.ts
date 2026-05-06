/**
 * FX currency pair formatting utilities.
 *
 * Conventions:
 * - A "pair" is always rendered as BASE/QUOTE (e.g. EUR/CZK, EUR/USD).
 * - Stronger currencies appear on the left (EUR > USD > GBP > ...).
 * - The "local" (weaker) currency appears on the right as the quote.
 */

/** Canonical ranking of strong/global currencies: lower index = stronger. */
const CURRENCY_STRENGTH: Record<string, number> = {
  EUR: 0,
  USD: 1,
  GBP: 2,
  JPY: 3,
  CHF: 4,
  CNY: 5,
  INR: 6,
  CZK: 7,
  PLN: 8,
  HUF: 9,
}

/**
 * Returns the numeric strength rank for a currency code.
 * Unknown currencies get a high rank (appear on the right / as quote).
 */
export function currencyStrength(code: string): number {
  return CURRENCY_STRENGTH[code.toUpperCase()] ?? 999
}

/**
 * Returns true if `base` is the stronger (left-side) currency relative to `quote`.
 */
export function isStrongerThan(base: string, quote: string): boolean {
  return currencyStrength(base) < currencyStrength(quote)
}

/**
 * Returns a canonical pair label: the stronger currency is always on the left.
 * E.g. formatPairLabel('CZK', 'EUR') → 'EUR/CZK'
 */
export function formatPairLabel(codeA: string, codeB: string): string {
  const a = codeA.toUpperCase()
  const b = codeB.toUpperCase()
  return isStrongerThan(a, b) ? `${a}/${b}` : `${b}/${a}`
}

/**
 * Returns the canonical base currency for a pair (the stronger one).
 */
export function pairBase(codeA: string, codeB: string): string {
  const a = codeA.toUpperCase()
  const b = codeB.toUpperCase()
  return isStrongerThan(a, b) ? a : b
}

/**
 * Returns the canonical quote currency for a pair (the weaker one).
 */
export function pairQuote(codeA: string, codeB: string): string {
  const a = codeA.toUpperCase()
  const b = codeB.toUpperCase()
  return isStrongerThan(a, b) ? b : a
}

/**
 * Returns all unique pair labels from a list of currency codes (paired against EUR).
 * EUR itself is filtered out (no EUR/EUR pair).
 */
export function buildEurPairList(currencyCodes: string[]): string[] {
  return currencyCodes
    .filter((code) => code.toUpperCase() !== 'EUR')
    .map((code) => formatPairLabel('EUR', code))
    .sort()
}

/**
 * Converts a EUR-based exchange rate to the display rate for a canonical pair label.
 *
 * **Important:** `eurToQuoteRate` must always express units of quote currency per 1 EUR
 * (e.g. 25.19 for EUR/CZK meaning "1 EUR = 25.19 CZK").
 *
 * - If the pair base is EUR (e.g. 'EUR/CZK'), the rate is returned unchanged.
 * - If the pair base is not EUR (e.g. 'USD/EUR'), the rate is inverted.
 */
export function rateForPair(pairLabel: string, eurToQuoteRate: number): number {
  const [base] = pairLabel.split('/')
  if (base?.toUpperCase() === 'EUR') return eurToQuoteRate
  // If the base is NOT EUR (e.g. USD/EUR), invert the rate.
  return eurToQuoteRate > 0 ? 1 / eurToQuoteRate : 0
}
