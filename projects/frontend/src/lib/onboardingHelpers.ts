/**
 * Pure helper functions for the onboarding wizard.
 * Extracted so they can be unit-tested independently of Vue component state.
 */

import type { BuildingLot } from '@/types'

// ---------------------------------------------------------------------------
// Step navigation helpers
// ---------------------------------------------------------------------------

/**
 * Maps a numeric step value (1-5) to the URL query-param key used for that step.
 * Step 1 → 'industry', Step 2 → 'city', Step 3 → 'factory', Step 4 → 'shop', Step 5 → 'complete'.
 */
export function stepToKey(value: number): string {
  if (value === 2) return 'city'
  if (value === 3) return 'factory'
  if (value === 4) return 'shop'
  if (value === 5) return 'complete'
  return 'industry'
}

/**
 * Maps a URL query-param key back to the corresponding numeric step.
 * Falls back to step 1 for any unknown value (including null/undefined).
 */
export function keyToStep(value: unknown): number {
  if (value === 'city') return 2
  if (value === 'factory') return 3
  if (value === 'shop') return 4
  if (value === 'complete') return 5
  return 1
}

// ---------------------------------------------------------------------------
// Step reachability helpers
// ---------------------------------------------------------------------------

/** Options that describe the current wizard state for max-step calculation. */
export interface OnboardingStepState {
  hasCompletionResult: boolean
  isResumingConfigureStep: boolean
  onboardingCurrentStep: string | null | undefined
  hasLocalFactoryProgress: boolean
  selectedCityId: string
  selectedIndustry: string
}

/**
 * Returns the highest step the player is allowed to navigate to given the
 * current wizard state. This is a pure function so it can be tested without
 * Vue reactive state.
 */
export function getMaxReachableStep(state: OnboardingStepState): number {
  if (state.hasCompletionResult) return 5
  if (state.isResumingConfigureStep) return 5
  if (state.onboardingCurrentStep === 'SHOP_SELECTION') return 4
  if (state.hasLocalFactoryProgress) return 4
  if (state.selectedCityId) return 3
  if (state.selectedIndustry) return 2
  return 1
}

/**
 * Clamps `requestedStep` so it is within the range [1, maxReachable].
 */
export function clampStep(requestedStep: number, maxReachable: number): number {
  return Math.min(Math.max(requestedStep, 1), maxReachable)
}

// ---------------------------------------------------------------------------
// Lot recommendation helpers
// ---------------------------------------------------------------------------

/**
 * Filters available lots to those suitable for `buildingType` and not yet owned.
 */
export function getAvailableLots(lots: BuildingLot[], buildingType: string): BuildingLot[] {
  return lots.filter((lot) =>
    lot.suitableTypes
      .split(',')
      .map((type) => type.trim())
      .includes(buildingType),
  )
}

/**
 * Returns the IDs of the recommended lots for factory placement.
 * Prefers industrial-district lots sorted by ascending price; falls back to all
 * available (unowned) factory lots if no industrial district lots are found.
 *
 * @param availableFactoryLots - Lots already filtered to FACTORY-suitable, unowned lots
 * @param count - Maximum number of IDs to return (default 2)
 */
export function getRecommendedFactoryLotIds(
  availableFactoryLots: BuildingLot[],
  count = 2,
): string[] {
  const unowned = availableFactoryLots.filter((lot) => !lot.ownerCompanyId)
  const industrial = unowned
    .filter((lot) => /industrial/i.test(lot.district))
    .sort((a, b) => a.price - b.price)

  if (industrial.length > 0) {
    return industrial.slice(0, count).map((lot) => lot.id)
  }

  return unowned
    .sort((a, b) => a.price - b.price)
    .slice(0, count)
    .map((lot) => lot.id)
}

/**
 * Returns the IDs of the recommended lots for the first sales shop.
 * Prefers commercial/business-district lots sorted by ascending price; falls back
 * to all available (unowned) shop lots.
 *
 * @param availableShopLots - Lots already filtered to SALES_SHOP-suitable, unowned lots
 * @param count - Maximum number of IDs to return (default 2)
 */
export function getRecommendedShopLotIds(availableShopLots: BuildingLot[], count = 2): string[] {
  const unowned = availableShopLots.filter((lot) => !lot.ownerCompanyId)
  const commercial = unowned
    .filter((lot) => /(commercial|business)/i.test(lot.district))
    .sort((a, b) => a.price - b.price)

  if (commercial.length > 0) {
    return commercial.slice(0, count).map((lot) => lot.id)
  }

  return unowned
    .sort((a, b) => a.price - b.price)
    .slice(0, count)
    .map((lot) => lot.id)
}

// ---------------------------------------------------------------------------
// CTA enablement helpers
// ---------------------------------------------------------------------------

/** Returns true when the player may proceed past step 3 (factory selection). */
export function canProceedStep3(
  companyName: string,
  selectedFactoryLot: BuildingLot | null,
  startingCash: number,
): boolean {
  if (!companyName.trim()) return false
  if (!selectedFactoryLot) return false
  if (selectedFactoryLot.ownerCompanyId) return false
  if (startingCash < selectedFactoryLot.price) return false
  return true
}

/** Returns true when the player may proceed past step 4 (shop selection). */
export function canProceedStep4(
  selectedProductId: string,
  selectedShopLot: BuildingLot | null,
  availableCash: number,
): boolean {
  if (!selectedProductId) return false
  if (!selectedShopLot) return false
  if (selectedShopLot.ownerCompanyId) return false
  if (availableCash < selectedShopLot.price) return false
  return true
}
