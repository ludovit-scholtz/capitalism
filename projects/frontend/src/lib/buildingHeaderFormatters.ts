/**
 * Pure formatting helpers for the BuildingHeaderFinancials component.
 * These are locale-independent for testability; the component passes the locale when needed.
 */

/**
 * Formats an absolute monetary value using the given ISO 4217 currency code.
 * Returns "—" for null, NaN, or non-finite values.
 */
export function fmtBuildingAmount(value: number | null, locale = 'en', currencyCode = 'EUR'): string {
  if (value === null || !isFinite(value) || isNaN(value)) return '—'
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: currencyCode,
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(Math.abs(value))
}

/**
 * Formats a profit/loss value with a sign prefix using the given ISO 4217 currency code.
 * Returns "—" for null, NaN, or non-finite values.
 * Zero profit has no sign prefix.
 */
export function fmtBuildingProfit(value: number | null, locale = 'en', currencyCode = 'EUR'): string {
  if (value === null || !isFinite(value) || isNaN(value)) return '—'
  const sign = value > 0 ? '+' : value < 0 ? '-' : ''
  const formatted = new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: currencyCode,
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(Math.abs(value))
  return `${sign}${formatted}`
}

/**
 * Returns the CSS class key for a profit/loss value.
 * Positive → 'bh-positive', Negative → 'bh-negative', Zero → 'bh-neutral', null → ''.
 */
export function profitClass(value: number | null): string {
  if (value === null) return ''
  if (value > 0) return 'bh-positive'
  if (value < 0) return 'bh-negative'
  return 'bh-neutral'
}

/**
 * Returns true when the building has meaningful financial data to display.
 * All three totals must be non-null and at least one must be non-zero.
 */
export function hasFinancialData(
  revenue: number | null,
  costs: number | null,
  profit: number | null,
): boolean {
  return (
    revenue !== null &&
    costs !== null &&
    profit !== null &&
    (revenue > 0 || costs > 0 || profit !== 0)
  )
}
