/**
 * FX currency pair formatting utilities.
 *
 * Conventions:
 * - A "pair" is rendered as WEAKER+STRONGER (e.g. CZKUSD, CZKEUR).
 * - Stronger currencies follow roadmap hierarchy: USD > EUR > CNY > GBP > INR > CZK.
 * - No slash separator is used in the pair code.
 */

/** Canonical ranking of strong/global currencies: lower index = stronger. */
const CURRENCY_STRENGTH: Record<string, number> = {
  USD: 0,
  EUR: 1,
  CNY: 2,
  GBP: 3,
  INR: 4,
  CZK: 5,
}

/**
 * Returns the numeric strength rank for a currency code.
 * Unknown currencies get a high rank (appear on the right / as quote).
 */
export function currencyStrength(code: string): number {
  return CURRENCY_STRENGTH[code.toUpperCase()] ?? 999
}

/**
 * Returns true if `base` is stronger than `quote`.
 */
export function isStrongerThan(base: string, quote: string): boolean {
  return currencyStrength(base) < currencyStrength(quote)
}

/**
 * Returns canonical pair code as WEAKER then STRONGER (no separator).
 * E.g. formatPairLabel('CZK', 'EUR') → 'CZKEUR'
 */
export function formatPairLabel(codeA: string, codeB: string): string {
  const a = codeA.toUpperCase()
  const b = codeB.toUpperCase()
  return isStrongerThan(a, b) ? `${b}${a}` : `${a}${b}`
}

/**
 * Returns weaker currency for a pair.
 */
export function pairWeaker(codeA: string, codeB: string): string {
  const a = codeA.toUpperCase()
  const b = codeB.toUpperCase()
  return isStrongerThan(a, b) ? b : a
}

/**
 * Returns stronger currency for a pair.
 */
export function pairStronger(codeA: string, codeB: string): string {
  const a = codeA.toUpperCase()
  const b = codeB.toUpperCase()
  return isStrongerThan(a, b) ? a : b
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
 * Converts a EUR-based exchange rate to the display rate for a canonical pair code.
 *
 * **Important:** `eurToQuoteRate` must always express units of quote currency per 1 EUR
 * (e.g. 25.19 for EUR/CZK meaning "1 EUR = 25.19 CZK").
 *
 * - If the pair code starts with EUR (e.g. "EURUSD"), the rate is returned unchanged.
 * - If the pair code starts with a non-EUR currency (e.g. "CZKEUR"), the rate is inverted.
 */
export function rateForPair(pairLabel: string, eurToQuoteRate: number): number {
  const base = pairLabel.slice(0, 3).toUpperCase()
  if (base === 'EUR') return eurToQuoteRate
  return eurToQuoteRate > 0 ? 1 / eurToQuoteRate : 0
}

/**
 * Extracts the non-EUR quote currency from a compact EUR pair code.
 * Supports "EURUSD" -> "USD" and "CZKEUR" -> "CZK".
 */
export function extractQuoteCurrencyFromEurPair(pairLabel: string): string | null {
  const normalized = pairLabel.toUpperCase()
  if (normalized.length !== 6) return null
  if (normalized.startsWith('EUR')) return normalized.slice(3)
  if (normalized.endsWith('EUR')) return normalized.slice(0, 3)
  return null
}
