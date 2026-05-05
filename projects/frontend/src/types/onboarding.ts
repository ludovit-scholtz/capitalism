import type { Company } from './company'
import type { Building, BuildingLot } from './building'
import type { ProductType } from './world'

export interface OnboardingResult {
  company: Company
  factory: Building
  salesShop: Building
  selectedProduct: ProductType
  /** ISO 4217 currency code of the city where the player started (e.g. "CZK" for Prague). */
  cityCurrencyCode?: string
}

export type ProductAvailabilityReason = 'connected_upstream' | 'current_stock' | 'connected_and_stock'

export interface RankedProductResult {
  productType: ProductType
  rankingReason: 'connected' | 'manufacturing' | 'used_by_company' | 'catalog'
  rankingScore: number
  availabilityReason?: ProductAvailabilityReason
}

export interface OnboardingStartResult {
  company: Company
  factory: Building
  factoryLot: BuildingLot
  nextStep: string
}
