/**
 * Pure helper functions for the sell-building workflow.
 *
 * Extracted from SellBuildingView so the calculation logic can be unit-tested
 * independently of Vue reactivity.
 */

export const EMV_BASE_LOT_VALUE = 75_000
export const EMV_LEVEL_MULTIPLIER_BASE = 1.5
export const EMV_UNIT_BASE_VALUE = 20_000
export const EMV_DEFAULT_POPULATION_INDEX = 0.5

/** Maximum price factor before showing a "price is very high" warning (1.5 = 150%). */
export const EMV_HIGH_PRICE_WARNING_FACTOR = 1.5
export const MINIMUM_SALE_PRICE_FACTOR = 0.7

export interface BuildingEmvInput {
  level: number
  unitCount: number
  populationIndex?: number | null
}

/**
 * Compute the estimated market value (EMV) for a building.
 *
 * Formula: ((baseLotValue × levelMultiplier) + unitValue) × locationMultiplier
 *   - baseLotValue = 75,000
 *   - levelMultiplier = 1.5^(level − 1)
 *   - unitValue = unitCount × 20,000
 *   - locationMultiplier = 1 + populationIndex × 0.5 (default populationIndex = 0.5)
 *
 * Result is rounded to the nearest 1,000.
 */
export function computeEstimatedMarketValue(input: BuildingEmvInput): number {
  const levelMultiplier = Math.pow(EMV_LEVEL_MULTIPLIER_BASE, input.level - 1)
  const unitValue = input.unitCount * EMV_UNIT_BASE_VALUE
  const locationMultiplier = 1 + (input.populationIndex ?? EMV_DEFAULT_POPULATION_INDEX) * 0.5
  return Math.round(((EMV_BASE_LOT_VALUE * levelMultiplier + unitValue) * locationMultiplier) / 1_000) * 1_000
}

/**
 * Return true if the asking price is more than 150% of the estimated market value.
 * This triggers a "price may be too high" advisory warning — it is not a hard error.
 */
export function isAskingPriceTooHigh(askingPrice: number, estimatedMarketValue: number): boolean {
  return askingPrice > estimatedMarketValue * EMV_HIGH_PRICE_WARNING_FACTOR
}

export function computeMinimumSalePrice(estimatedMarketValue: number): number {
  return Math.round(estimatedMarketValue * MINIMUM_SALE_PRICE_FACTOR * 100) / 100
}

export function isAskingPriceBelowMinimum(askingPrice: number, estimatedMarketValue: number): boolean {
  return askingPrice < computeMinimumSalePrice(estimatedMarketValue)
}
